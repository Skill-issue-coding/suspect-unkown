# Suspect Unknown — Doors and Lockpicking

Implementation guide for M5 step 2, parts 2.3 to 2.5, in `roadmap_npc.md`. It covers the player-only door, lock levels gated by skills, a lockpicking minigame in the style of the Dying Light games, how NPCs react to all of it, and the guard who slams an open door shut.

Read `CLAUDE.md` first, then decisions 6, 18, 19 and 20 in `roadmap_npc.md`. This document follows the same working style: each part ends with a check to pass in play mode before moving on.

> Written 2026-10-09. Steps 2.1 (`Anomaly`, `AnomalyKind`) and 2.2 (`NpcAnomalySight`, anomaly suspicion, the guard walking to a noticed anomaly) are already built. Everything here builds on them.

---

## 1. The design

### 1.1 Doors belong to the player

The joke at the heart of this feature is a genre convention made into a rule the NPCs believe: **only the robber opens doors.**

- **NPCs never use doors.** They walk only through open areas. To them a closed door is a wall: it blocks their NavMesh, so it blocks their paths and their hearing (sound travels along the NavMesh, decision 10).
- **A closed door also blocks sight and light.** The leaf is on the `Occluder` layer.
- **An open door is evidence.** It raises an `Anomaly` of kind `OpenDoor` (decision 18). An NPC who sees it grows suspicious; staff care, customers barely do.
- **An open door is also a route.** It opens the NavMesh, so a guard chasing the player can now follow through it, and sound now carries through it.
- **The player can close a door behind them.** Closing clears the anomaly. Hiding your traces is a real choice.
- **A guard who finds an open door** walks over, plays a confused animation, and **slams** it shut. Slamming closes the door, clears the anomaly, and makes a noise nearby NPCs glance at.

### 1.2 Lock levels and skills

Every interactable in the game is unlocked by buying skills, the way the Payday series works. For doors this means:

- **Each door has a lock level, 1 to 3.** Level 1 is a basic lock; higher levels need more skill.
- **Hard gate.** The player cannot attempt a lock above their Lockpicking skill level at all. The prompt says what is needed ("Requires Lockpicking 2").
- **Harder game within the gate.** A higher lock has a smaller sweet spot, a narrower margin around it, and slips sooner. A level 3 lock is a real test even for a player who has the skill.
- **Picks are unlimited and never break.** Failure costs time and noise, not items.
- **Once picked, a door stays unlocked** for the rest of the heist. If a guard slams it, the player can open it again without picking, but opening it raises the anomaly again.

The skill system itself is Phase 7 work (shop and save). Until then a placeholder component on the player holds the Lockpicking level, see 3.3.

### 1.3 The lockpicking minigame

Modelled on Dying Light's lockpicking: find the sweet spot by feel, then turn.

- **The pick** swings across a half circle, from −90° to +90°. Mouse X or the left stick moves it.
- **Tension** (hold a button) turns the lock. How far the lock can turn depends on how close the pick is to the **sweet spot**:
  - Within the sweet spot, the lock turns all the way and the door unlocks.
  - Inside the margin around it, the lock turns partway, less the further off you are.
  - Outside the margin, the lock barely moves.
- **While tension is held, the pick cannot move.** Release to reposition. This is what makes it a search.
- **Binding.** When the lock stops short, the pick strains (the view shakes). Keep forcing it, and after a short time the pick **slips**: the lock snaps back to the start with a metallic rattle.
- **The sweet spot is random per door, but fixed for the heist.** A slip doesn't move it, so the player learns from each attempt.
- **While picking, the player stands still.** Cancel or move to stop. Picking can be restarted at any time.

### 1.4 Lockpicking is suspicious and noisy

- **Being seen picking a lock is very abnormal.** It sets a new conduct flag, `Lockpicking` (decision 6). Every archetype weighs it highly, so an NPC who watches you pick grows suspicious fast: a guard at close range goes from calm to Investigating in about a second.
- **Picking scratches.** A faint sound every second or so, harmless on its own (alarm 0): a nearby guard glances toward it but does not grow suspicious.
- **A slip rattles.** A short, louder sound with some alarm: a guard within a few metres turns Curious.
- **The witness model remembers it.** In M7, `Lockpicking` fills the **Method** slot ("picked a lock", concept 6.2). Seen once, it follows the player's profile.

### 1.5 Later, not now

Other ways to open a door (a crowbar: fast and loud; keycards; a drill) are not specified here. The design leaves room for them: a door only knows `Open()`, `Close()` and its lock level, so any opener on the player side can call `Open()` after its own minigame or timer. Add them when a tool needs them.

---

## 2. Architecture

### 2.1 Where everything lives

Dependencies point one way: NPC → Player → Core. Core references nothing.

| Assembly | File                                                                                                                          | Type             | Purpose                                                                                                                                      |
| -------- | ----------------------------------------------------------------------------------------------------------------------------- | ---------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| Core     | `Core/Scripts/World/Door.cs`                                                                                                  | MonoBehaviour    | The door: open and closed state, lock level and sweet spot, the NavMesh obstacle, the leaf swing, raising and clearing its anomaly, the slam |
| Core     | `Core/Scripts/World/DoorReach.cs`                                                                                             | MonoBehaviour    | A trigger volume in front of the door (both sides) that marks where the player can use it                                                    |
| Core     | `Core/Scripts/World/Anomaly.cs`                                                                                               | MonoBehaviour    | Already exists. Gains a claim, a resolve point, and `Resolve()` (step 2.4)                                                                   |
| Player   | `Player/Scripts/Interaction/PlayerSkills.cs`                                                                                  | MonoBehaviour    | **Placeholder** for Phase 7: the player's skill levels, set in the inspector                                                                 |
| Player   | `Player/Scripts/Interaction/PlayerDoorInteraction.cs`                                                                         | MonoBehaviour    | Tracks doors in reach, handles the interact input, starts lockpicking or opens and closes                                                    |
| Player   | `Player/Scripts/Interaction/LockpickTuning.cs`                                                                                | ScriptableObject | Difficulty per lock level and the shared feel values                                                                                         |
| Player   | `Player/Scripts/Interaction/LockpickModel.cs`                                                                                 | plain C# class   | The minigame's rules, with no Unity dependencies beyond maths. Unit tested                                                                   |
| Player   | `Player/Scripts/Interaction/PlayerLockpicking.cs`                                                                             | MonoBehaviour    | Runs a lockpicking session: input to model, noise, conduct, opening the door                                                                 |
| Player   | `Player/Scripts/UI/LockpickView.cs`                                                                                           | MonoBehaviour    | The minigame's screen: lock, pick, shake. Draws only                                                                                         |
| Player   | `Perception/ConductFlags.cs`, `PlayerConduct.cs`                                                                              | existing         | Gain the `Lockpicking` flag (step 2.5)                                                                                                       |
| NPC      | `Data/ConductWeights.cs`                                                                                                      | existing         | Gains a `lockpicking` weight (step 2.5)                                                                                                      |
| NPC      | `Guard/GuardBehaviour.cs`                                                                                                     | existing         | Gains the door errand: claim, walk, confused beat, slam (step 2.4)                                                                           |
| NPC      | `Guard/GuardAnimation.cs`, `Core/Animator/GuardAnimatorParams.cs`, `Editor/GuardClips.cs`, `Editor/GuardLocomotionBuilder.cs` | existing         | A Confused one-shot (decision 16)                                                                                                            |

Why the split looks like this:

- **The door knows nothing about the player or the skills.** It holds its lock level and opens when asked. Who may ask, and after which minigame, is the player's business. That is what keeps Core free of dependencies, and it is what lets a crowbar or a keycard open the same door later.
- **The minigame rules are a plain class.** `LockpickModel` takes time, pick movement and tension, and reports the lock's state. It has no `MonoBehaviour`, no input and no UI, so it can be tested in a few lines with the Unity Test Framework (concept 8: pure logic is painful to debug through gameplay).
- **Model, controller, view.** `LockpickModel` decides, `PlayerLockpicking` connects it to input, noise, conduct and the door, and `LockpickView` only draws. The view can be replaced without touching the rules.
- **The guard never references `Door`.** It resolves the **anomaly**, and the door listens for that. The same mechanism will let staff reset the breaker in step 3.

### 2.2 Interactables and skills in general

All interactables will be gated by skills, but only doors exist now. Build the door version, with `PlayerSkills` holding one value. Do not build a general "interactable" framework yet (CLAUDE.md: no frameworks for later). When the second interactable arrives (the breaker, a router), look at what the two genuinely share and extract only that.

---

## 3. Implementation

Work through the parts in order. Each one ends with a check.

### 3.1 Part 2.3a — The door

**Purpose:** a door that is a wall to NPCs when closed and a route plus an anomaly when open. No player interaction yet; test with context menus.

**Prefab structure** (`Core/Prefabs/Door.prefab` or wherever world prefabs go):

```
Door                 Door, Anomaly, NavMeshObstacle
├── Hinge            empty; the leaf rotates around this
│   └── Leaf         the mesh and a BoxCollider; layer Occluder; NavMeshModifier (Ignore From Build)
└── Reach            BoxCollider (trigger) covering both sides of the doorway; layer Volume; DoorReach
```

**Fields on `Door`:**

| Field             | Type              | Default | Meaning                                            |
| ----------------- | ----------------- | ------- | -------------------------------------------------- |
| `hinge`           | `Transform`       | —       | Rotates to swing the leaf                          |
| `openAngle`       | `float`           | 100     | Degrees the leaf swings when open                  |
| `swingDuration`   | `float`           | 0.35    | Seconds to open or close normally                  |
| `slamDuration`    | `float`           | 0.12    | Seconds to close when slammed                      |
| `lockLevel`       | `int` (Range 1–3) | 1       | Lockpicking skill needed, and how hard the lock is |
| `startsLocked`    | `bool`            | true    | False for a door that is shut but not locked       |
| `slamNoiseRadius` | `float`           | 8       | Metres a slam carries along the NavMesh            |

**Public surface:**

```csharp
public int LockLevel => lockLevel;
public bool IsOpen { get; private set; }
public bool IsLocked { get; private set; }
public float SweetSpot { get; private set; }   // degrees, −80 to 80; set once in Awake

public void Open();    // unlocks, swings open, obstacle off, anomaly raised
public void Close();   // swings shut, obstacle on, anomaly cleared
public void Slam();    // Close, faster, plus a NoiseEvent
```

**What each method does:**

- `Awake`: cache the `Anomaly` and the `NavMeshObstacle` (`[RequireComponent]` both). Set `IsLocked = startsLocked`. Pick `SweetSpot = Random.Range(-80f, 80f)`. Set the obstacle enabled, since doors start closed.
- `Open()`: return if already open. `IsOpen = true`, `IsLocked = false`, `obstacle.enabled = false`, swing the hinge to `openAngle`, `anomaly.Raise()`.
- `Close()`: return if already closed. `IsOpen = false`, `obstacle.enabled = true`, swing the hinge back to 0, `anomaly.Clear()`.
- `Slam()`: the same as `Close()` with `slamDuration`, then `NoiseBus.Publish(new NoiseEvent(transform.position, slamNoiseRadius, 0f))`. Alarm 0: NPCs glance at a slam but don't grow suspicious of it.
- Add `[ContextMenu]` to `Open`, `Close` and `Slam` for testing.

**The swing** uses PrimeTween, which is already in `Assets/Plugins`:

```csharp
Tween.LocalRotation(hinge, Quaternion.Euler(0f, angle, 0f), duration, Ease.OutCubic);
```

Check the exact PrimeTween method name in its documentation for the installed version.

**The NavMesh obstacle**, on the door root, not the leaf, so it does not swing with it:

- Shape Box, sized to fill the doorway.
- **Carve** on, **Carve Only Stationary** on.
- Enabled while closed, disabled while open.

**Traps:**

- **The NavMesh bake collects physics colliders.** Without a `NavMeshModifier` set to **Ignore From Build** on the leaf, the leaf is baked into the NavMesh as a permanent wall, and opening the door changes nothing for NPCs. Only the obstacle should block.
- **The doorway must be walkable in the bake.** With the leaf ignored, the bake sees an open doorway, and the obstacle closes it at runtime. If the bake leaves a gap anyway (a doorway narrower than the agent radius allows), widen the doorway or lower the agent radius.
- **The anomaly's sight point must be in open air**, in the middle of the doorway, not inside the leaf or the frame. A linecast that ends inside a collider hits it, and the anomaly would never be seen (step 2.2). The default offset of 1.2 m up works if the door's origin is centred in the frame.
- **Carving takes effect over the next frames,** and agents whose path crosses the change repath on their own (`NavMeshAgent.autoRepath`). Do not call `NavMesh.CalculatePath` yourself after opening.

**Debug:** in `OnDrawGizmos`, draw the doorway (the obstacle's box) green when open and red when closed. The anomaly already draws its own sphere.

**Check:**

1. Bake the NavMesh with a closed door between two rooms. The doorway is walkable in the baked mesh, and the obstacle cuts it in play mode.
2. A guard investigating something on the far side walks around (or, with no other route, stops at the door).
3. **Open** (context menu): the leaf swings, the NavMesh opens, the anomaly's sphere turns red, and the guard's cone now passes through the doorway. A guard who sees it investigates the door (step 2.2).
4. **Close**: the leaf swings back, the NavMesh closes, the anomaly clears.
5. **Slam**: a fast close, and nearby NPCs glance at the door (hearing readout: heard, alarm 0).
6. With the door closed, a sprint on the far side is not heard through it (`NoPath` or `TooFar`). With it open, it is.

### 3.2 Part 2.3b — Reach and interaction

**Purpose:** the player can open and close doors when standing at them. Locked doors start lockpicking (3.4); until then, they just say "Locked".

**`DoorReach`** (Core) is a trigger on the `Volume` layer, which collides with the player only. It holds a reference to its `Door` (`GetComponentInParent` in `Awake`) and nothing else. The trigger covers both sides of the doorway, about 1.2 m deep on each side.

**`PlayerDoorInteraction`** (Player) tracks which doors are in reach, the same way `PlayerConduct` tracks zones:

- `OnTriggerEnter` and `OnTriggerExit`: if the other collider has a `DoorReach`, count its door in or out of a `Dictionary<Door, int>`. Counting handles several colliders per reach.
- **Nearest door:** if more than one door is in reach, use the closest. Only compute this when the interact button is pressed, not every frame.
- **Interact pressed:**
  - Door open → `Close()`.
  - Door closed and unlocked → `Open()`.
  - Door locked and `door.LockLevel > skills.LockpickingLevel` → show "Requires Lockpicking N" and do nothing else.
  - Door locked and within skill → start lockpicking (3.4). Until 3.4 exists, log "Locked".
- **Prompt:** a small screen-space text ("E: Open", "E: Pick lock", "Requires Lockpicking 2") shown while a door is in reach. Update it from the trigger events and after each interaction, not every frame.

**Input:** add an **Interact** action (Button) to the player's input actions asset, and `OnInteract(InputValue)` to `PlayerInputs`, following the pattern of the existing actions. Never use the legacy `Input` class.

**`PlayerSkills`** is a placeholder:

```csharp
// Placeholder until the Phase 7 skill shop and save exist: the player's skill levels, set in the
// inspector. Reserved by the roadmap; replace its source, keep its getters
public class PlayerSkills : MonoBehaviour
{
    [SerializeField, Range(0, 3), Tooltip("Highest lock level the player may pick. 0 cannot pick at all")]
    private int lockpickingLevel = 1;

    public int LockpickingLevel => lockpickingLevel;
}
```

**Traps:**

- Trigger events on the player arrive through the `CharacterController` (see the comment in `PlayerConduct`). Do not add a `Rigidbody` to the player.
- A reach collider disabled while the player is inside never sends `OnTriggerExit`. Doors are level geometry, so this is accepted, as for zones.
- **Do not close a door on the player.** If the player stands in the doorway itself, closing would push the leaf into them. Make the reach trigger stop short of the doorway's centre, or check for overlap before closing.

**Check:**

1. Walk up to an unlocked door: the prompt says "E: Open". Press: it opens. Press again: it closes.
2. A level 2 locked door with Lockpicking 1: "Requires Lockpicking 2", nothing happens.
3. A level 1 locked door: "E: Pick lock", which logs "Locked" until 3.4.
4. Walk away: the prompt disappears.
5. Two doors close together: the nearer one responds.

### 3.3 Part 2.3c — The lockpicking rules

**Purpose:** the minigame's logic as a plain class, tested before any UI exists.

**`LockpickTuning`** (ScriptableObject, data only, decision 1):

| Field         | Type                 | Default   | Meaning                                                                           |
| ------------- | -------------------- | --------- | --------------------------------------------------------------------------------- |
| `levels`      | `LockLevelTuning[3]` | see below | One entry per lock level                                                          |
| `pickSpeed`   | `float`              | 0.25      | Degrees of pick movement per pixel of mouse movement                              |
| `stickSpeed`  | `float`              | 180       | Degrees per second at full stick                                                  |
| `returnSpeed` | `float`              | 3         | How fast the lock springs back when tension is released, in full turns per second |
| `slipLockout` | `float`              | 0.4       | Seconds after a slip before tension works again                                   |

`LockLevelTuning` is a `[Serializable]` struct:

| Field       | Level 1 | Level 2 | Level 3 | Meaning                                                 |
| ----------- | ------- | ------- | ------- | ------------------------------------------------------- |
| `sweetSpot` | 12°     | 7°      | 3.5°    | Half-width of the zone where the lock turns all the way |
| `margin`    | 40°     | 30°     | 20°     | Half-width beyond the sweet spot where it turns partway |
| `turnSpeed` | 1.5     | 1.2     | 1.0     | Full turns per second while turning freely              |
| `slipTime`  | 1.0 s   | 0.7 s   | 0.5 s   | How long the lock can bind before the pick slips        |

These are starting values; tune them in play mode. In `OnValidate`, warn if `sweetSpot` exceeds `margin` or any value is zero or negative.

**`LockpickModel`** (plain C# class):

```csharp
public class LockpickModel
{
    public float PickAngle { get; private set; }   // −90 to 90
    public float LockTurn { get; private set; }    // 0 = rest, 1 = open
    public bool IsBinding { get; private set; }
    public bool IsOpen => LockTurn >= 1f;

    public event Action Slipped;

    public LockpickModel(LockLevelTuning level, float returnSpeed, float slipLockout, float sweetSpotAngle);
    public void Tick(float deltaTime, float pickDelta, bool tension);
}
```

The rules inside `Tick`, in order:

1. **During a slip lockout,** count it down, let the lock spring back, ignore tension.
2. **Without tension:** move the pick by `pickDelta`, clamped to ±90°. Spring the lock back toward 0 at `returnSpeed`. Reset the binding timer.
3. **With tension:** the pick does not move. Work out how far the lock may turn:

   ```
   off = |PickAngle − sweetSpotAngle|
   maxTurn = off <= sweetSpot ? 1
           : clamp01(1 − (off − sweetSpot) / margin)
   ```

   Move `LockTurn` toward `maxTurn` at `turnSpeed`.

4. **Binding:** if tension is held, the lock has reached `maxTurn` and `maxTurn` is below 1, the lock is stuck. Set `IsBinding` and add to the binding timer. When it reaches `slipTime`: `LockTurn = 0`, start the lockout, raise `Slipped`.
5. **Open:** when `LockTurn` reaches 1, the lock is open. The controller ends the session.

**Unit tests** (Unity Test Framework, Edit Mode, in a test assembly):

- Pick on the sweet spot, tension held long enough: `IsOpen`.
- Pick inside the margin: the lock stops partway, `IsBinding` becomes true, and after `slipTime` `Slipped` fires once and `LockTurn` is 0.
- Pick outside the margin: the lock barely moves.
- With tension held, `pickDelta` does not move the pick.
- Release tension: the lock springs back to 0 and the binding timer resets.
- During the lockout, tension does nothing.

**Check:** all tests pass. No scene needed.

### 3.4 Part 2.3d — Playing it

**Purpose:** connect the model to input, the door, noise, and the screen.

**Input:** add two actions to the player's input actions asset:

- **LockpickMove** (Value, Vector2): mouse delta and the left stick.
- **LockpickTension** (Button): left mouse button and the right trigger.

Add `OnLockpickMove` and `OnLockpickTension` to `PlayerInputs`, as for the existing actions. Cancel is the Interact button again, or any movement input.

**`PlayerLockpicking`** (Player):

| Field             | Type             | Default | Meaning                                               |
| ----------------- | ---------------- | ------- | ----------------------------------------------------- |
| `tuning`          | `LockpickTuning` | —       | The asset from 3.3                                    |
| `scratchInterval` | `float`          | 1.2     | Seconds between faint scratching sounds while picking |
| `scratchRadius`   | `float`          | 1.5     | Metres a scratch carries; alarm 0                     |
| `slipRadius`      | `float`          | 3       | Metres a slip carries                                 |
| `slipAlarm`       | `float`          | 0.5     | How suspicious a slip sounds on its own               |

**Public surface:** `bool IsPicking`, `Begin(Door door)`, `Cancel()`, and the current `LockpickModel` for the view to read.

- `Begin(door)`: create a `LockpickModel` from the door's level tuning and `door.SweetSpot`, subscribe to `Slipped`, set `IsPicking`, suspend movement, show the view.
- `Update` (continuous input, so `Update` is right here): read the pick input (mouse delta times `pickSpeed`, plus stick times `stickSpeed × deltaTime`), call `model.Tick`. Every `scratchInterval`, publish a scratch noise. If `model.IsOpen`: `door.Open()`, then end the session.
- On `Slipped`: publish `new NoiseEvent(door position, slipRadius, slipAlarm)`, and shake the view.
- `Cancel()` and the end of the session: unsubscribe, clear `IsPicking`, restore movement, hide the view.
- `PlayerDoorInteraction` calls `Begin` instead of logging "Locked".

**Suspending movement:** the player must not walk away mid-pick with the view open. Add a way for `PlayerMotor` to ignore move input (a public flag or a method), and set it in `Begin` and clear it at the end. Movement input while picking counts as Cancel.

**Noise values, and what they do to the guard** (sensitivity 3, hearing suspicion 0.7; formula in the M4 notes of `roadmap_npc.md`):

| Sound                | Radius | Alarm | Guard reaction                     |
| -------------------- | ------ | ----- | ---------------------------------- |
| Scratch              | 1.5 m  | 0     | Glances, within 4.5 m of path      |
| Slip                 | 3 m    | 0.5   | Curious within about 3.9 m of path |
| Slam (the door, 3.1) | 8 m    | 0     | Glances                            |

**`LockpickView`** (uGUI on a screen-space overlay canvas, TextMeshPro for text):

- A lock face (a circle) rotated by `LockTurn × 90°`, and the pick rotated by `PickAngle`.
- While `IsBinding`, shake the pick a few pixels, growing with the binding time.
- On a slip, a short PrimeTween shake of the whole lock.
- It reads the model in `LateUpdate` and decides nothing.

**Check:**

1. At a level 1 door, press Interact: the view opens and the player stops.
2. Move the pick, hold tension away from the sweet spot: the lock turns a little, binds, shakes, and slips. A slip rattles (hearing readout of a nearby guard: heard, alarm 0.5).
3. Find the sweet spot: the lock turns fully, the view closes, and the door swings open.
4. The sweet spot stays the same between attempts on the same door, and differs between doors.
5. Cancel mid-pick: the view closes and the player can move.
6. A level 3 lock with Lockpicking 3: noticeably harder than level 1.
7. A guard near the door while you pick out of its sight: it glances at the scratches, and turns Curious if you slip close to it.

### 3.5 Part 2.4 — The guard slams the door

**Purpose:** a guard who notices an open door walks to it, looks confused, and slams it. Decision 20 calls this an **errand**: a claimed target, a place to go, a reaction, then back to normal.

**`Anomaly` gains three things:**

```csharp
[SerializeField, Tooltip("Where an NPC stands to deal with this, in local space. On the NavMesh, in front of the doorway")]
private Vector3 resolveOffset = new Vector3(0f, 0f, 1f);

public Vector3 ResolvePoint => transform.TransformPoint(resolveOffset);

public Object ClaimedBy { get; private set; }
public bool TryClaim(Object claimant);   // true if unclaimed or already claimed by this claimant
public void Release(Object claimant);    // only the claimant can release

public event Action Resolved;
public void Resolve();                   // raises Resolved; the owner does the actual fixing
```

- `Clear()` also clears `ClaimedBy`, so a door the player closes frees the guard who was on its way.
- Claims work like POI claims (decision 15): only one NPC deals with each anomaly. Others still notice it and grow suspicious, but do not walk to it.
- Both sides of a door need dealing with from the side the guard is on. One `resolveOffset` puts the resolve point on one side; if guards approach from both sides in your levels, choose the side nearer the guard instead (two offsets, pick the nearer).

**`Door` listens:** in `OnEnable`, `anomaly.Resolved += Slam`; in `OnDisable`, unsubscribe.

**`GuardBehaviour` gains the errand** as a sub-phase of Investigating, like searching:

- **Field:** `private Anomaly errand;`
- **`OnNoticed(anomaly)`** (from step 2.2): if the guard does not see the player and has no errand, try `anomaly.TryClaim(this)`. If it succeeds, `errand = anomaly`, and the last-known position becomes `anomaly.ResolvePoint` instead of the sight point. If another guard holds the claim, do not set a last-known position at all: this guard only grows suspicious.
- **On arrival** (`UpdateInvestigating`, where `arrived` becomes true), if there is an errand and it is still raised:
  1. Face the anomaly's sight point.
  2. Play the Confused animation.
  3. After `confusedDuration` (about 1.5 s), call `errand.Resolve()`. The door slams.
  4. Release the claim, clear `errand`.
  5. The anomaly is cleared, so `Noticed` stops, suspicion decays after its delay, and the guard gives up and goes back to patrolling.
- **If the errand is cleared by someone else** on the way (the player closed the door), drop it and search the spot as usual.
- **Interruptible** (the near miss, M5 step 6): if the guard sees the player at any point, release the claim and clear the errand. The player matters more than a door.
- **`OnDisable`:** release the claim.
- **Debug label:** show "(resolving OpenDoor)" while on an errand.

**Animation (decision 16):** never edit the animator by hand.

- Import a confused clip (Mixamo "Looking Around" or "Scratching Head"). Bake rotation and position into the pose, as for the other guard clips (CLAUDE.md, Animation).
- Add it to `GuardClips` as a one-shot, with a `Confused` trigger in `GuardAnimatorParams`.
- Add the state and its interruptible transitions in `GuardLocomotionBuilder`, then rebuild.
- `GuardAnimation` gets `PlayConfused()`.

**Check:**

1. Open a door (context menu) in a guard's view, out of the player's sight. The guard walks to the resolve point, faces the door, looks confused, and slams it.
2. The slam's noise makes another NPC nearby glance.
3. After the slam, suspicion drains and the guard returns to patrolling.
4. Two guards see the same door: one goes, the other only turns suspicious and does not walk to it.
5. Close the door yourself while the guard is on the way: it searches the spot instead.
6. Let the guard see you on the way: it drops the errand and comes for you.

### 3.6 Part 2.5 — Lockpicking is conduct

**Purpose:** an NPC who sees the player picking a lock grows suspicious fast, and later remembers the method.

- **`ConductFlags`:** add `Lockpicking = 1 << 2`. Keep the powers of two.
- **`PlayerConduct.Current`:** set the flag while `PlayerLockpicking.IsPicking`. Use `GetComponent` in `Awake` (both are on the player root), not per frame.
- **`ConductWeights`:** add a `lockpicking` field, a switch arm in `Get`, and a constructor parameter.
- **Archetype values:** everyone recognises lockpicking. Fill these in on each asset, since a new field starts at its initializer value:

| Archetype                      | lockpicking |
| ------------------------------ | ----------- |
| SecurityGuard                  | 1.0         |
| CivilianRole.Manager           | 1.0         |
| TiredClerk                     | 1.0         |
| Civilian                       | 1.0         |
| CivilianRole.DistractedShopper | 1.0         |
| SecurityCamera                 | 1.0         |

Every archetype shares the rise rate of 0.5 (decision 22), so a civilian reacts to lockpicking exactly as fast as a guard at the same visibility. At abnormality 1 and full strength, that rate takes it from calm to Investigating in about one second and to Alerted in under two. At a distance, in shadow, or partly hidden, `Strength` is lower and it takes longer, which is the stealth.

- **Witness (M7):** `Lockpicking` maps to the **Method** slot ("picked a lock"). Nothing to build now; note it in M7.

**Check:**

1. Pick a lock in a guard's view: the readout shows `conduct Lockpicking`, suspicion climbs fast, and the guard reaches Investigating within about a second at close range.
2. Pick in its view but in deep shadow and crouched: noticeably slower.
3. Stop picking: the flag clears, and suspicion decays after the delay.
4. A customer watching from the same spot as a guard: suspicion rises just as fast. A distracted shopper reacts only if you are inside its small cone.

---

## 4. Docs to update as parts pass

In `roadmap_npc.md`:

- M5 step 2: check off 2.3, 2.4 and 2.5 as each passes, with a short status line.
- File tables: add `Door`, `DoorReach` (Core); `PlayerSkills`, `PlayerDoorInteraction`, `LockpickTuning`, `LockpickModel`, `PlayerLockpicking`, `LockpickView` (Player). Update `Anomaly`, `ConductFlags`, `ConductWeights`, `GuardBehaviour`, and the guard animation files.
- Changelog lines with the date.

In `CLAUDE.md`: if a new physics layer is added, add it to the layer table. This design needs none: the reach uses `Volume`, the leaf uses `Occluder`.

---

## 5. Open questions

- **Doors without a lock.** `startsLocked = false` makes a closed door that opens without picking. Should opening such a door in an NPC's view also be conduct (an `OpeningDoor` flag)? Not built until a level needs unlocked doors.
- **Does a slammed door lock again?** Proposed: no, it stays unlocked, but opening it raises the anomaly again.
- **Does the guard who slams the door look through it first?** Decided for now: no, only the confused beat and the slam. Revisit if playtests find it too forgiving.
- **Controller feel.** The stick speed and the sweet spot sizes need tuning on a gamepad, not only with a mouse.
- **The skill tree.** Phase 7 decides how Lockpicking levels are bought and saved. `PlayerSkills` is the only thing that changes when it arrives.
- **Staff and open doors.** The tired clerk notices open doors but is not suspicious of them: it shuts them as a chore. The manager investigates and shuts them. Both errands arrive with civilians in M6 (step 3), using the same claim and `Resolve()` as the guard (decision 22).
