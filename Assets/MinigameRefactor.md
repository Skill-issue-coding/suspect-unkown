# Suspect Unknown — Minigame refactor

The second half of the interactables refactor (`InteractablesRefactorPlan.md`). That one gave every reach spot one `Interactable` and a behaviour per interaction. This one does the same for what happens after E: one session runner and one shared screen, plus a small part per minigame. Done 2026-10-20, ahead of the safe-opening minigame, which is the third one and should be built on this design.

**Read `CLAUDE.md` first.** Dependencies still point one way: **NPC → Player → Core**.

---

## 1. Why

`PlayerLockpicking` and `PlayerGlassCutting` repeated the same session code: mark `PlayerInteraction` busy, suspend movement, save and set the cursor, reset tension and cancel, end on cancel, hold the finished view for `successHold`, then undo all of it. `LockpickView` and `GlassCutterView` repeated the same presentation: the background blur fade, the camera push-in from progress, and the scale punch on completion. Safe opening would have been a third copy of both.

They differ only in: the model's rules, how input feeds the model each frame, whether the cursor is locked (mouse delta) or free (pointer), what happens to the world on completion, the conduct flag NPCs see, and what the view draws.

## 2. Design

| File | Folder | Role |
| --- | --- | --- |
| `IMinigameModel` | `Core/Interactables/Scripts/Minigame/` | `Progress` (0–1) and `IsComplete`. Every model implements it |
| `MinigameScreen` | `Core/Interactables/Scripts/Minigame/` | On the view prefab root. The screen camera, push-in, completion punch and blur. `Open(model)` activates the view, `Close()` hides it |
| `IMinigame` | `Player/Scripts/Interaction/` | One per minigame: `Screen`, `Model`, `LocksCursor`, `Conduct`, `Tick`, `Complete`, `Stop` |
| `PlayerMinigame` | `Player/Scripts/Interaction/` | One per player. Runs whichever `IMinigame` is active: busy, movement, cursor, cancel, success hold. `IsPlaying`, `Conduct`, `Begin`, `End` |
| `PlayerLockpicking`, `PlayerGlassCutting` | `Player/Scripts/Interaction/` | Implement `IMinigame`. Build the model, feed input, act on the door or the glass |
| `LockpickView`, `GlassCutterView` | Core | Draw only what is their own. `[RequireComponent(typeof(MinigameScreen))]` |

The session, in order:

1. The interaction behaviour (`DoorInteraction`, `GlassCutterInteraction`) calls the minigame's own `Begin(target)`.
2. The minigame builds its model, then calls `PlayerMinigame.Begin(this)`. That opens the screen (which runs the view's `Awake` on first use), marks the player busy and sets the cursor.
3. The minigame starts its own view (`view.Begin(model, …)`).
4. Each frame `PlayerMinigame` calls `Tick`. When `Model.IsComplete`, it calls `Complete` once, holds the view for `successHold`, then ends.
5. On end (completed, cancelled, or the job ending through `PlayerJobEnd`), `PlayerMinigame` calls `Stop`, closes the screen and gives the player back.

Rules that did not change:

- Conduct: `PlayerConduct` now reads `PlayerMinigame.Conduct`, which is the active minigame's flag (`Lockpicking`, `GlassCutting`) or `None`. Nothing in the NPC code changed.
- Each model keeps its own vocabulary. `LockpickModel` implements `IMinigameModel` explicitly (`Progress` is `LockTurn`, `IsComplete` is `IsOpen`).
- The lockpick HUD stays `LockpickHud`, owned by `LockpickView`. A minigame that needs a HUD owns its own.

### 2.1 One prefab for every minigame (2026-10-20)

Every minigame view, the blur volume and the minigame HUDs live in `Assets/Core/Interactables/Prefabs/Minigames.prefab`, which is nested in `PlayerHud.prefab`. Every scene already has PlayerHud, so a scene needs no minigame wiring of its own.

```
PlayerHud
  JobHud                     sort 0
  Minigames                  [MinigameRig]
    BlurVolume               global Volume, LockpickBlur profile, priority 10, weight 0
    Screens                  overlay Canvas, sort 5, 1920x1080 scaler
      LockpickMinigameView     (nested prefab, off)
      GlassCutterMinigameView  (nested prefab, off)
    LockpickHud              (nested prefab) own overlay canvas, sort 10
    GlassCutterHud           (nested prefab) own overlay canvas, sort 10; same layout as LockpickHud
  Newspaper                  sort 100
```

`GlassCutterHud` (2026-10-20) mirrors `LockpickHud`: title, status line, a CUT meter from `Progress`, a "Follow the line" hint (lit until done) and a "Hold to cut" hint (lit while the aim is on the line), and a tip after two misaligned strains. `GlassCutterModel.HasTension` was added for it. `GlassCutterView.hud` references it. The glass cutter's key and rim lights sit at the same offsets from the glass as the lockpick lights from the lock.

- `MinigameRig` registers itself as `MinigameRig.Current`. `PlayerLockpicking` and `PlayerGlassCutting` have no `view` field any more: on their first `Begin` they find their view once with `MinigameRig.Current.GetComponentInChildren<…>(true)`. `Player.prefab` cannot reference objects inside another prefab's instance, which is why the lookup exists.
- `LockpickView.hud` is a serialized reference to the `LockpickHud` beside it, and `LockpickHud.Current` is gone. The HUD sits beside the view, not inside it, so it can still fade out after the view hides.
- Each 3D rig keeps its own render layer (`Lockpicking` 20, `GlassCutter` 21) so the cameras do not film each other's rigs. Layers 22 and 23 belong to the Synty cockpit; use 18, 19 or 24 and up for a new rig.
- Removed from the scenes: the `LockpickMinigameView` and `GlassCutterMinigameView` instances under `HUD`, the `LockpickBlurVolume` object, and the `view` and `blurVolume` overrides. The scene `HUD` canvas stays, because it holds `OffscreenIndicatorHUD`.
- The `LockpickBlur` profile is still in `Assets/Testing/TestingScene/`. If you move it, do so inside Unity.

Removed: `PlayerLockpicking.IsPicking`, `Cancel()`, `Ended` (no listeners) and `successHold`; `PlayerGlassCutting.IsCutting`, `End()` and `successHold`; the blur, camera and punch fields on both views (`lockCamera`, `cutterCamera`, `cutterRoot`, `openPunch`, `completePunch`, `pushIn*`, `blur*`). Their values were moved to the new `MinigameScreen` on both view prefabs, the blur volume as a scene override in `Apartment`, `Thriftshop` and `TestingScene`, and `PlayerMinigame` was added to `Player.prefab`.

## 3. Adding safe opening

1. **Data:** `SafeTuning` (ScriptableObject), like `LockpickTuning`.
2. **Rules:** `SafeModel : IMinigameModel` in `Core/Interactables/Scripts/Safe/`. Plain class, no scene objects.
3. **Conduct:** as built (2026-10-20), `PlayerSafeCracking.Conduct` is `Lockpicking`, as `thriftshop-level.md` 2.3 asks, so no flag or weight was added. A separate `SafeCracking` flag would need a weight in every archetype and a recheck of the reaction-tree thresholds.
4. **Session:** `PlayerSafeCracking : MonoBehaviour, IMinigame` beside the others. `[RequireComponent(typeof(PlayerMinigame))]`. Copy the shape of `PlayerLockpicking`. `LocksCursor` true if the dial reads mouse delta like the pick.
5. **World:** a `Safe` (Core) that opens when asked, and a `SafeInteraction : IInteractionBehaviour` (Player) that calls `PlayerSafeCracking.Begin(safe)`.
6. **View:** a `SafeMinigameView` prefab: duplicate `LockpickMinigameView.prefab` in Unity (not the file explorer), put the dial meshes and camera mask on a free render layer (see 2.1), and give it a `SafeView` that draws the dial. Set `MinigameScreen`'s camera, punch root and push-in. Open `Minigames.prefab`, add the view under `Screens` (inactive), and set its `MinigameScreen.blurVolume` to `BlurVolume`. `PlayerSafeCracking` finds it through `MinigameRig.Current`, as `PlayerLockpicking` does. No scene changes.

_Check:_ E at the safe opens its view with blur and the cursor as chosen; Escape closes it and gives the player back; completing opens the safe, holds the view for a moment, then closes; NPCs see the `SafeCracking` flag only while the view is open.
