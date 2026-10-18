# Suspect Unknown — Interactables refactor plan

Plan to replace the separate reach scripts for loot and doors with one general `Interactable`, plus small behaviour scripts that decide what happens after the interaction. Written 2026-10-18. The glass cutter (in progress) is the third interaction type and should be built on this design, not on the old pattern.

**Read `CLAUDE.md` first.** Its rules apply to this work: guidance over finished files, small numbered steps with a check, one type per file with a purpose comment, `[SerializeField] private` with a `[Tooltip]` on every field, events subscribed in `OnEnable` and unsubscribed in `OnDisable`, nothing that does not change every frame in `Update`, no `GetComponent` or `Find*` in a per-frame path. Dependencies point one way: **NPC → Player → Core**. Core references nothing; Player never references NPC types.

---

## 1. Why

`PlayerLootInteraction` and `PlayerDoorInteraction` repeat the same machinery:

- a `Dictionary` of reach counts and a `Nearest()` pick,
- showing and hiding a `WorldPrompt` and an `InteractOutline` for the nearest reach,
- the trap where a thing that switches itself off sends no `OnTriggerExit` (loot solves it with the static `LootPickup.Taken`, doors with `Door.Opened`).

They differ in three things only: what E does (hold against press), whether the prompt is greyed out (a door above the player's lockpick skill), and what happens afterwards. The glass cutter would be a third copy of the same code. One general `Interactable` removes the duplication and the missing-`OnTriggerExit` trap in one place.

## 2. Current state (files involved)

| File | Role today |
| --- | --- |
| `Assets/Core/Scripts/Loot/LootPickup.cs` | Loot trigger; owns `item`, `prompt`, `outline`, `promptOffset`; `Take()`; static `Taken` event |
| `Assets/Core/Interactables/Scripts/Lockpick/DoorReach.cs` | Door reach trigger (one per side); owns the door-side `prompt` |
| `Assets/Core/Interactables/Scripts/Lockpick/Door.cs` | The door: lock, swing, anomaly; switches its `DoorReach`es off while open; `Opened` event; `PromptPoint` from the anomaly |
| `Assets/Core/Interactables/Scripts/WorldPrompt.cs` | The world-space E hint; `Show(follower, target, text, blocked)`, `SetProgress`, `Hide` |
| `Assets/Core/Interactables/Scripts/InteractOutline.cs` | Rim highlight; `SetVisible`; shared by both sides of one door |
| `Assets/Player/Scripts/Interaction/PlayerLootInteraction.cs` | Tracks pickups in reach, hold-to-take, `IsTaking` (read by `PlayerConduct` for `Stealing`) |
| `Assets/Player/Scripts/Interaction/PlayerDoorInteraction.cs` | Tracks door reaches, press-to-open, or begins lockpicking when locked; greys the prompt when the lock is above the player's skill |
| `Assets/Player/Scripts/Interaction/PlayerLockpicking.cs` | The lockpick session and view; `IsPicking`, `Ended` |

Prefabs that reference `InteractOutline`: `Door`, `Cash`, `Laptop`, `Phone`, `PocketWatch`, `PearlNecklace`, `DiamondRing`, `JeweledEgg`, `VintageCamera` (all in `Assets/Core/Interactables/Prefabs/`).

Other readers to check before removing anything (a search found references in `PlayerConduct`, `PickupToast`, `CarryHud`, `JobRewards`, `JobObjectives`, `Door`, and the docs `doors-and-lockpicking.md`, `apartment-level.md`, `roadmap_npc.md`).

## 3. Target design

### 3.1 `Interactable` (Core, one per reach spot)

The shared part of every interaction: a trigger the player stands in, the prompt and outline shown for it, and a way to use it.

```csharp
[RequireComponent(typeof(Collider))]
public class Interactable : MonoBehaviour
{
    [Header("Presentation")]
    [SerializeField] WorldPrompt prompt;       // shown by the player side, never by this
    [SerializeField] InteractOutline outline;  // optional; outlined while it is the nearest in reach
    [SerializeField] Transform promptTarget;   // optional; where the prompt's line points. Else this transform + promptOffset
    [SerializeField] Vector3 promptOffset;     // local space, used when promptTarget is empty
    [SerializeField] string useText = "E";     // text in the prompt box

    [Header("Use")]
    [SerializeField] float holdSeconds;        // 0 = press. Above 0 = hold; the prompt's ring fills over this time

    public WorldPrompt Prompt { get; }
    public InteractOutline Outline { get; }
    public Vector3 PromptPoint { get; }
    public string UseText { get; }
    public float HoldSeconds { get; }          // a behaviour may overwrite it in Awake (loot: item.TakeSeconds)

    public bool IsBlockedFor(GameObject interactor);  // asks the behaviour; trigger events only, never per frame
    public void Use(GameObject interactor);           // calls the behaviour

    public void SetReachable(bool reachable);         // trigger on or off; raises Withdrawn when turned off
    public event Action<Interactable> Withdrawn;      // raised by SetReachable(false) and by OnDisable
}
```

Behaviour of the class:

- **Awake:** cache the collider; find the single behaviour with `GetComponentInParent<IInteractionBehaviour>()`; if none, `Debug.LogWarning($"{name} …", this)` and `enabled = false`. Warn if the collider is not a trigger (it would block the player). If `outline` is empty, find it once with `GetComponentInChildren<InteractOutline>()`.
- **`Withdrawn`** replaces both `LootPickup.Taken` (for the player side) and `Door.Opened` (for the player side). `OnDisable` raises it too, so a pickup that is deactivated needs no special hookup.
- **`InteractOutline` stays its own component.** A door has two `Interactable`s (one per side) sharing one outline on the leaf; merging the outline code into `Interactable` would make the two sides fight over one fade. Nine prefabs also reference it by GUID.

### 3.2 `IInteractionBehaviour` (Core, own file)

```csharp
public interface IInteractionBehaviour
{
    bool IsBlockedFor(GameObject interactor);  // the greyed lock state
    void Interact(GameObject interactor);      // what happens after the interaction
}
```

One interface with three implementers (loot, door, glass cutter). The behaviour decides both whether the interaction is allowed for this interactor and what it does, so "blocked" can depend on the player's skills without Core knowing about them.

| Behaviour | Folder | `IsBlockedFor` | `Interact` |
| --- | --- | --- | --- |
| `LootPickup` (existing) | Core | never | `Take()`: announce, then hide |
| `DoorInteraction` (new) | Player | the door's lock level is above the interactor's `PlayerSkills.LockpickingLevel` | open if unlocked, else `PlayerLockpicking.Begin(door)` |
| `GlassCutterInteraction` (new, Marcus) | Player | per the cutter's rules | begin the glass cutter view |

A behaviour that needs player services (skills, lockpicking, a view) lives in `Assets/Player/Scripts/Interaction/`, because Core must not reference Player. A Player-folder component on a door prefab is fine.

### 3.3 `PlayerInteraction` (Player)

Replaces `PlayerLootInteraction` and `PlayerDoorInteraction`. It does the duplicated work once:

- `OnTriggerEnter` and `OnTriggerExit` keep a `Dictionary<Interactable, int>` of reach counts (trigger events arrive through the `CharacterController`, see `PlayerConduct`); `Interactable.Withdrawn` drops an interactable that switches itself off.
- `Nearest()` and a single `SetShown` show and hide the prompt and outline for the nearest. Prompt and outline are updated only on trigger events and when a view ends, never per frame.
- A **press** (`HoldSeconds` is 0) calls `Use` on `PlayerInputs.InteractPressed`. A **hold** fills `Prompt.SetProgress` over `HoldSeconds` while E is held, then calls `Use`. Only the hold progress runs in `Update`, and only while holding.
- Raises `HoldStarted(Interactable)` and `HoldEnded(Interactable)`, so `PlayerConduct` can map a held `LootPickup` to the `Stealing` conduct flag (see 5, question 2).
- `IsBusy` (true while a view such as the lockpick or the glass cutter is open) suspends prompts and input; views call `Refresh()` when they end (see 5, question 3).

### 3.4 Rules that do not change

- `IsTaking` (Stealing) and `IsPicking` (Lockpicking) stay what NPC suspicion reads through `PlayerConduct`. Nothing in the NPC code changes.
- `LootPickup.Taken` stays static: `JobObjectives`, `JobRewards`, `PickupToast` and `CarryHud` listen to it.
- The van stash (G) uses a different input action and stays separate (`PlayerVanStash`). It does not go through `Interactable`.

## 4. Steps

Each step ends with a check to pass in play mode before the next. Do them in order.

### I0. Agree the open questions (section 5)

Decide questions 1 to 5 with a short note in this file. Nothing is built until they are settled.

### I1. `Interactable` and `IInteractionBehaviour`

New files in `Assets/Core/Interactables/Scripts/`, one type per file, each opening with a purpose comment. No player code yet.
_Check:_ the project compiles. Adding `Interactable` to a test object with no behaviour logs the "no behaviour" warning and disables itself; a non-trigger collider logs the trigger warning.

### I2. `PlayerInteraction`, tested on one pickup

Add `PlayerInteraction` to the player **beside** the old scripts. Make `LootPickup` implement `IInteractionBehaviour` and add an `Interactable` to **one** test pickup. Disable `PlayerLootInteraction` on the player while testing so the two do not both show a prompt.
_Check:_ on the test pickup, the prompt, outline and hold ring behave exactly as before; releasing E resets the ring; walking out of reach hides both; taking it announces `LootPickup.Taken` once.

### I3. Move loot over

For every loot prefab (`Cash`, `Laptop`, `Phone`, `PocketWatch`, `PearlNecklace`, `DiamondRing`, `JeweledEgg`, `VintageCamera`, and any scene-placed `LootDisplay` pickups): add an `Interactable` and reassign the prompt and outline that `LootPickup` held. Remove `prompt`, `outline`, `promptOffset` and `PromptPoint` from `LootPickup` (they now live on the `Interactable`; these are fields on a different component, so `FormerlySerializedAs` does not carry them over and they must be reassigned). `LootPickup` sets `HoldSeconds` from `item.TakeSeconds` in `Awake`. Wire `PlayerConduct` to `HoldStarted` and `HoldEnded`. Delete `PlayerLootInteraction`. Edit prefabs in prefab mode, not on scene instances.
_Check:_ the apartment and the thrift shop play as before: taking cash, the till and a bag item; the Stealing conduct flag is true only during a hold; the loot toast, carry HUD and objectives still react.

### I4. Move doors over

New `DoorInteraction` (Player) implementing `IInteractionBehaviour`: open if unlocked, begin lockpicking if locked and within skill, `IsBlockedFor` true when the lock is above the skill. `Door` holds `Interactable[]` instead of `DoorReach[]`; its `SetReachable` calls `SetReachable` on each. Replace `DoorReach` with `Interactable` on the `Door` prefab (both sides; the outline stays on the leaf, found once in `Awake`). `PlayerLockpicking` sets `PlayerInteraction.IsBusy` and refreshes when it ends. Delete `DoorReach` and `PlayerDoorInteraction`. Check every other reader of `Door.Opened` before removing anything.
_Check:_ an unlocked door opens with E; a locked door within skill starts the lockpick; a locked door above skill shows the greyed prompt with the lock; an open door offers nothing; closing it again restores the prompt on both sides; the two sides of one door never flicker the outline.

### I5. Glass cutter on the new base

The cutter follows the lockpick split: `GlassCutterModel` (rules), `GlassCutterTuning`, `GlassCutterView` (Core, draws only), and `PlayerGlassCutting` (Player, runs the session). `GlassCutterInteraction` implements `IInteractionBehaviour` and calls `PlayerGlassCutting.Begin`. `PlayerGlassCutting` marks `PlayerInteraction` busy while the view is open and clears it when it ends, as `PlayerLockpicking` does; the view cannot, because Core must not reference Player. If the cutter is finished before I2 to I4, build it against the interfaces in 3.1 and 3.2 from the start and finish the migration afterwards.
_Check:_ the glass pane shows the same prompt and outline as everything else; E starts the cutter view; closing the view brings the prompt back.

### I6. Docs

Record the decision (one `Interactable`, one behaviour per interaction, `PlayerInteraction`) in `roadmap_npc.md` or the roadmap file that covers player interaction, update the file tables, update `doors-and-lockpicking.md` and `apartment-level.md` where they name `DoorReach`, `PlayerDoorInteraction` or `PlayerLootInteraction`, and add a dated changelog line.

## 5. Open questions (agreed 2026-10-18)

1. **Prompt point for doors.** Use `promptTarget` (an authored Transform empty). ✅
2. **Conduct during a hold.** `PlayerInteraction` raises `HoldStarted`/`HoldEnded`; `PlayerConduct` maps `LootPickup` to `Stealing`. ✅
3. **A view is open.** `PlayerInteraction.IsBusy` plus `Refresh()`, used by both views. ✅
4. **Other keys.** Van stash stays separate (`PlayerVanStash`, G key). ✅
5. **Where the glass cutter's behaviour lives.** Player folder (`Assets/Player/Scripts/Interaction/`). ✅

## 6. Tidy-up found on the way

`WorldPrompt.Show` (`WorldPrompt.cs`, lines 104 to 109) sets `label.text` and calls `SetBlocked` twice. Harmless; remove the duplicate lines during I2.
