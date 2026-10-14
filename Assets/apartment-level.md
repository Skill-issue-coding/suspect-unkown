# Suspect Unknown — The Apartment Level

Design and implementation plan for the first playable level, `Assets/Apartment/Apartment.unity`. It is the tutorial job: the player breaks into a tired clerk's apartment and steals a laptop and a cellphone. It also delivers the Phase 3 pieces in `roadmap.md` that every later job needs: loot, a fail state, a results screen, and saved progress.

Read `CLAUDE.md` first, then decisions 6, 9, 18, 19, 21 and 22 in `roadmap_npc.md`, and `Assets/doors-and-lockpicking.md`. Each step ends with a check to pass in play mode before moving on.

> Written 2026-10-13. Already in place: the street and building, doors with lockpicking (`Apartment House Door`), the roof that fades when the player walks inside (`InteriorReveal`), and the light setup described in section 5.

---

## 1. What the level is for

1. **Teach the game** by design rather than text: movement, crouching, noise, light and shadow, lockpicking, suspicion, and the decision window.
2. **Steal two items.** The **laptop** sits in the living room, right next to the half-asleep clerk. The **cellphone** is in the bedroom. The laptop unlocks the shop; the cellphone unlocks the next targets.
3. **Fail fairly.** If the clerk reaches Alerted and the player does not break line of sight in time, the clerk calls the police and the job fails.

### 1.1 The route and what each part teaches

| Part | What happens | What it teaches |
| --- | --- | --- |
| Street | Walk from the start to the building between street lamps | Moving, crouching, sprinting is loud |
| Front door | `Apartment House Door`, lock level 1 | The lockpick prompt and minigame (already built) |
| Hallway | Pools of light from the room lights, dark gaps between them | Light makes you easier to see |
| Living room | The clerk dozes on the couch; the laptop is next to them | The vision cone, suspicion, the sleep rhythm, the decision window |
| Bedroom | The cellphone; reaching it crosses the clerk's line of sight | Timing a crossing with the sleep rhythm |
| Getaway | Back out through the front door to the `GetawayVan` on the street; stand at its back doors for 2.5 s | Leaving is part of the job; every job ends at the van |

Text is limited to small world-space control prompts on the street (move, crouch, sprint). Everything else is taught by what the player sees happen.

---

## 2. The clerk

### 2.1 Mindset

At work, a tired clerk barely notices anything (decision 22). At home it is the opposite: there is no ordinary reason for a stranger to be in their apartment, so being seen inside is alarming on its own, even while doing nothing else. The clerk's suspicion therefore rises much faster than an ordinary civilian's.

This is done through the existing rules rather than around them:

- **Same rise rate.** Decision 22 keeps one rise rate, 0.5, for every archetype. The clerk is faster because of their **conduct weights**, not a different rate.
- **The apartment is a private zone.** A `Zone` of type `Staff` covers the interior, so the player carries `InStaffArea` while inside (decision 6, context). For the clerk at home that flag weighs heavily; a customer in a shop would weigh it lightly.
- **A separate archetype,** `TiredClerkHome`, so the store's `TiredClerk` keeps its own tuning.

Starting values, to be tuned in playtests:

| Weight or value | `TiredClerk` (store) | `TiredClerkHome` | Effect |
| --- | --- | --- | --- |
| `inStaffArea` | low | 0.6 | Full view inside reaches Alerted in about 2.5 s |
| `lockpicking` | 1.0 | 1.0 | Same everywhere (decision 22) |
| `stealing` | — | 1.0 | Taking the laptop in view is the worst thing to be seen doing |
| `sprinting` | 0.1 | 0.4 | Running indoors is odd |
| Hearing | normal | higher | A sprint indoors wakes them |

### 2.2 The sleep rhythm and the semi-dynamic cone

The clerk is half asleep, not asleep. Perception keeps running the whole time; what changes is **how much the clerk can see**, and the vision cone shows it.

| State | Cone | Lasts |
| --- | --- | --- |
| **Dozing** | Small: about 0.35 × range, 0.5 × FOV | 5–8 s |
| **Stirring** (the tell) | Grows from small to full | about 0.6 s: head lifts, a yawn |
| **Awake** | Full archetype range and FOV | 2–3 s, looking around |

- After the awake spell the clerk dozes again.
- **Anything above Idle wakes them** (a noise, something glimpsed in the small cone): the cone opens to full, and they stay awake for about 10 s, looking toward the stimulus.
- **After a scare the dozes get shorter,** so careless play gets harder.

This changes decision 21 (cones for guards and cameras only): the clerk has a cone too, because this level teaches the cone the player later reads on guards. Ordinary civilians still have none.

**How the cone changes size.** Range and FOV live in the archetype, which holds tuning only (decision 1). The change is runtime state on the NPC:

- `NpcPerception` gains a **view scale** (range scale and FOV scale, both 1 by default), set by the role behaviour and eased over the stirring time. Perception uses `ViewDistance × rangeScale` and `FovDegrees × fovScale` wherever it reads range and FOV today (`InViewCone`, the distance and angle factors).
- `NpcVisionCone` keeps its fan and ray directions built once at **full** size (they are fixed after `Start`). It casts rays to the scaled range, writes distances as fractions of the full range as now, and passes the scale to the shader. The shader cuts beyond the scaled range and outside the scaled half-angle (the existing `_HalfFov` input), and evaluates the falloff curves against the scaled range, so brightness still matches perception.
- **Rule from decision 21:** where the cone and perception disagree, perception is the truth and the cone gets fixed. Check both scales against the debug rays.

### 2.3 The decision window and the call

Reaching Alerted is the clerk's equivalent of a guard starting to chase.

1. **Deliberating** (decision 9): the clerk reaches for the phone and a progress indicator appears above them. Progress fills over about 2.5 s **while the clerk can see the player**, and drains while they cannot.
   - Full: the clerk starts **Calling**.
   - Empty: the clerk lost them. They go back to awake, looking around, and suspicion decays as normal.
2. **Calling:** the phone goes to the ear. After a short beat for the animation (about 1.5 s) the call connects and the job fails with `PoliceCalled`. The call itself cannot be interrupted in this level.

The window runs on sight rather than on suspicion dropping, because suspicion only starts decaying after the archetype's decay delay, which can be longer than the window. Breaking line of sight is the action the level teaches, so it is what the window measures.

`CivilianActivity` gains `Dozing`. The clerk's states are `Dozing`, `Working` (used for awake and looking around), `Deliberating` and `Calling`.

### 2.4 Where it lives

`ResidentBehaviour` is the clerk's role behaviour (decision 2: shared perception components plus one role behaviour). It is a separate class from `CivilianBehaviour`, because its routine is a sleep cycle in one spot, not walking between spots, and its reaction is fixed rather than chosen by a reaction tree. If M6's decision window turns out to share real code with this one, extract it then.

---

## 3. The job flow

### 3.1 Outcome events

`JobOutcome` (Core) is a static class with two events, `Failed(FailReason)` and `Completed`, and methods to raise them. `FailReason` is `Caught` (a guard caught the player) and `PoliceCalled` (a civilian finished a call).

- NPC code calls into Core, so dependencies stay NPC → Player → Core.
- `GuardBehaviour.Caught` raises `JobOutcome.Fail(FailReason.Caught)`. This replaces the test-bench `ReloadOnCaught`.
- **Only the first outcome counts.** A failure after success, or a second failure, is ignored.
- **Trap: static state survives scene reloads,** and survives play-mode entry when domain reload is off. Reset the "ended" flag on every scene load (`SceneManager.sceneLoaded`, registered from a `[RuntimeInitializeOnLoadMethod]`).

### 3.2 Loot and the bag

- **`LootItem`** (Core, ScriptableObject): display name and what it unlocks (`ProgressUnlock`: `Shop`, `Targets`). Data only.
- **`LootPickup`** (Core, interactables): the world object. Reach trigger, outline and `WorldPrompt`, the same pattern as the door. `Take()` hides it and raises a static `Taken(LootItem)` event.
- **`PlayerLootInteraction`** (Player): hold E for about 1 s to take an item in reach. The player carries the new `Stealing` conduct flag during the hold.
- **`JobObjectives`** (Core, one per level): lists the required items, counts the ones taken, and raises `Changed`. Exposes `Collected`, `Required` and `IsComplete` (every required item taken). Required items are the minimum to leave: in this level that is both items. Later levels require less and let the player take as much extra loot as they dare; a level with nothing required is complete from the start.
- **`LootBagHud`** (bottom-left of the screen): a bag icon shown as an outline when the level starts, filling as items are taken. Fill is `Collected / Required`, so in this level the laptop fills half and the cellphone fills the rest. The fill eases toward the new value (smooth what is displayed). The items themselves are hidden; carrying is not visible on the character for now.
Carry weight and bag capacity from Phase 3 come later; the fill formula then changes to space used over capacity.

### 3.3 The getaway van

Every job ends at the `GetawayVan`, as in Payday: the player stands at its open back doors for a few seconds, the doors close with the player inside, and the van drives off while the newspaper slides in.

**The extraction zone.**

- A trigger on the `Volume` layer at the van's back doors. Only the player's CharacterController counts, as for zones and the roof fade.
- **Locked until the objectives are complete** (`JobObjectives.IsComplete`). In this level that means both items. Standing in a locked zone does nothing.
- **The timer:** while the player is inside an unlocked zone, progress rises from 0 to 1 over about 2.5 s. Outside, it **drains back down at the same speed**, so stepping out briefly costs a little, not everything.
- **The job can still fail during the countdown.** A guard's catch or the clerk's call still ends it. The moment progress reaches 1 the job is won: the van calls `JobOutcome.Complete()` right away, so any failure after that is ignored (only the first outcome counts).

**The indicator.** The player must see where to stand and how far along they are.

- A marking on the ground behind the van, the size of the zone: a world-space canvas lying flat, with a dashed outline in the same style as the interaction prompt, and a fill that grows with progress.
- **Locked:** dim and grey, with the lock icon from the door prompt.
- **Unlocked:** bright and gently pulsing, so a player with a full bag notices it from the street.
- **Counting:** the fill follows progress; it eases on display only (smooth what is displayed, never the timer itself).
- State changes happen on events (objectives completed, player entered or left), never by polling every frame. Only the fill updates per frame, and only while progress is changing.

**The departure.** One sequence, run once:

1. **Lock in:** `JobOutcome.Complete()`. The player's controls are suspended.
2. **Into the van:** the player is moved to a cargo point inside the van over about 0.4 s, then hidden. The CharacterController is switched off before moving, or it fights the move. The player object stays active, so nothing that refers to it breaks.
3. **Doors close:** the two back doors swing shut. They need hinge pivots in the model, the same problem the house doors had: set the origin at the hinge in Blender, or put each door under an empty at the hinge.
4. **Drive away:** the van accelerates along its forward axis (or a short list of waypoints) out of the street.
5. **Camera follows the van:** a second Cinemachine camera aimed at the van, with a higher priority, takes over when the doors close. Cinemachine blends to it, and the player's camera setup is never touched.
6. **Newspaper:** `JobOutcomeScreen` waits a success delay (about 1.5 s, a new field) before the paper comes in, so it slides in over the driving van.

**Where it lives.** `GetawayVan` (Core) owns the zone, the timer, the doors, the drive and the van camera, and raises a `Departed` event at lock-in. `PlayerExtraction` (Player) listens to that event and does the player's part: suspend controls, move into the cargo point, hide. Dependencies stay Player → Core: the van never looks up the player.

### 3.4 Results screen: the newspaper

One newspaper, two outcomes. `JobOutcomeScreen` listens to both events.

**On failure:**

- The picture fades to greyscale: a Volume with Color Adjustments saturation −100, weight eased from 0 to 1.
- Time slows from 1 to about 0.2. PrimeTween's `Tween.GlobalTimeScale(endValue, duration)` tweens it on unscaled time. Every other tween on the screen itself must use unscaled time.
- The newspaper slides and rotates in over the screen as an overlay canvas, with a random headline for the reason.
- A restart button or key reloads the scene. Set the time scale back to 1 before reloading.

**On success:** no greyscale and no slow motion. The newspaper waits a success delay while the van drives away (3.3), then comes in with a positive headline for the robber, and a continue button returns to the main menu.

Headlines come from a `HeadlineSet` ScriptableObject per outcome, picked at random:

| Outcome | Examples |
| --- | --- |
| Caught or police called | "Burglar caught during break-in", "Thief caught in the act", "Sleepy tenant foils clumsy burglar" |
| Success | "Mystery burglary baffles neighbours", "Laptop and phone vanish from sleeping tenant's flat", "Police clueless after daring break-in" |

Phase 6 reuses this newspaper prefab for the witness report, so build it as a prefab with replaceable text from the start.

### 3.5 Saved progress

A small JSON file, `progress.json` in `Application.persistentDataPath`, written with `JsonUtility`:

```json
{ "shopUnlocked": true, "targetsUnlocked": true, "completedJobs": ["Apartment"] }
```

- `ProgressSave` is the plain serializable data; `ProgressStore` (static) loads and saves it.
- **Written on job success only.** Taking an item and then failing unlocks nothing.
- The Phase 7 save system (police profile, wardrobe, money) grows from this file rather than replacing it.

---

## 4. Implementation steps

The work splits into two tracks that can run side by side. **Track A** is the job flow (Core and Player). **Track B** is the clerk (NPC). Step A1 is the seam between them, so it comes first.

### Track A — the job flow

**Status (2026-10-14):** A1–A5 are done and checked in play mode. A6 is next. Progress notes are in `handovers/apartment-job-flow.md`.

**A1. `JobOutcome` and `FailReason`.** The static events from 3.1, the scene-load reset, and `GuardBehaviour.Caught` raising `Fail(Caught)`. Delete `ReloadOnCaught`.
*Check:* on the test bench, the guard catches the player and one failure is logged. A second catch logs nothing.

**A2. The newspaper and `JobOutcomeScreen`.** Greyscale volume, slow motion, the sliding newspaper, headlines from `HeadlineSet`, restart and continue. Add a `[ContextMenu]` for each outcome to test with.
*Check:* both context menu entries play their sequence; restart plays at normal speed afterwards.

**A3. Conduct flags `Lockpicking` and `Stealing`.** Add each to `ConductFlags` together with the code that sets it: `PlayerLockpicking` while picking, `PlayerLootInteraction` while taking (A4). Add the matching weight to `ConductWeights` and set it in every archetype asset; a new field starts at 0 otherwise. Lockpicking is 1.0 for every archetype (decision 22).
*Check:* the conduct debug readout shows `Lockpicking` while picking the front door.

**A4. Loot.** `LootItem`, `LootPickup` and `PlayerLootInteraction`. Create the laptop and cellphone assets and place both pickups.
*Check:* holding E takes each item, it disappears, and `Stealing` shows during the hold only.

**A5. `JobObjectives` and `LootBagHud`.**
*Check:* the bag starts as an outline, is half full after one item and full after both, and `IsComplete` turns true with the second item.

**A6. `ProgressSave` and `ProgressStore`.**
*Check:* a `[ContextMenu]` success writes `progress.json` with both unlocks. After a failure, it is unchanged.

**A7. The getaway van, part 1: zone, timer and indicator.** `GetawayVan` with the locked or unlocked zone, the rising and draining timer, and the ground indicator. For now, reaching 1 just calls `JobOutcome.Complete()`.
*Check:* with one item the marking is grey and locked and standing in it does nothing. With both it pulses; standing in it fills it in about 2.5 s; stepping out drains it; a full timer plays the success newspaper. The clerk calling during the countdown still fails the job.

**A8. The getaway van, part 2: the departure.** `Departed` event, `PlayerExtraction`, door hinges, door close, the drive, the van camera, and the success delay on `JobOutcomeScreen`.
*Check:* at lock-in the player steps into the van and disappears, the doors swing shut, the camera follows the van out of the street, and the newspaper slides in over it. No errors from NPCs or the camera while the player is hidden.

### Track B — the clerk

**B1. Data.** Create `TiredClerkHome` from `TiredClerk` with the values in 2.1. Add a `Zone` of type `Staff` covering the interior; it can share the `InteriorVolume` object and collider that drive the roof fade.
*Check:* with only the shared perception components on the clerk, standing in view inside reaches Alerted in about 2.5 s. The same spot outside the zone raises nothing.

**B2. View scale on perception.** `NpcPerception` gains the range and FOV scale from 2.2.
*Check:* at scale 0.35 the debug rays stop at the shorter range, and detection only happens inside it.

**B3. View scale on the cone.** `NpcVisionCone` and the shader follow the scale. Put a cone on the clerk.
*Check:* at both scales, the cone's edge and the debug rays agree; easing between them shows no popping.

**B4. `ResidentBehaviour`: the sleep rhythm.** Dozing, stirring and awake on timers, with the view scale eased between states, waking on anything above Idle, and shorter dozes after a scare. Add `Dozing` to `CivilianActivity`.
*Check:* the player can stand at the edge of the room while the clerk dozes; a sprint wakes them; the tell is visible before every waking.

**B5. The window and the call.** Deliberating with sight-driven progress, then Calling, then `JobOutcome.Fail(PoliceCalled)`.
*Check:* seen, then ducking out of sight within the window, ends with the clerk awake and searching. Staying in view fails the job with the police headline.

**B6. Presentation.** Mixamo clips: sleeping on the couch, head lift and yawn, reaching for the phone, phone call. The suspicion indicator and a progress ring during the window.
*Check:* someone who has not seen the code can tell from the animation alone when the clerk is about to wake and when they are about to call.

### Together

**C1. Teaching pass.** `GameplayLight` on the room lights and street spots (the scene has none yet, so the light level always reads as dark). World-space control prompts on the street. Place the laptop so that taking it needs the dozing window.
*Check:* a first-time player finishes without instructions in two or three tries, and can say why they failed when they do.

**C2. Docs.** Update the status columns in `roadmap_npc.md`, tick Phase 3 items in `roadmap.md`, and add changelog lines.

---

## 5. Lighting notes for this level

Fixed on 2026-10-13: shadows were flickering in the street and switching on and off in the rooms.

- **Cause.** URP on desktop draws at most **32 additional-light shadow maps per frame**. A point light uses 6, a spot light uses 1. The scene asked for about 68, so URP kept the shadows of the lights nearest the camera and dropped the rest, and the set changed as the camera moved.
- **Fix.** The lamp-head glow lights cast no shadows. One light per room keeps realtime shadows (Kitchen 1, LivingRoom 1, Bedroom 1, Hallway 1), plus the 8 street spots: 32 in total. The other room lights and the two outdoor `Light Post Point (1)` lights are Baked, so they keep baked shadows at no runtime cost. The `PC_High` additional-light shadow atlas is 4096.
- **Budget rule:** with every realtime shadowed light counted (6 per point, 1 per spot), stay at 32 or fewer. A new shadowed light means another one goes Baked or loses its shadows.
- **Rebake after changing a light's mode.** URP reads the mode from the last bake, so a light switched to Baked keeps casting realtime shadows until the scene is rebaked.
- **Baked lights reach characters through light probes only.** Add a Light Probe Group to the rooms before baking.

---

## 6. Open questions

- **Clerk tuning.** Are 2.5 s to Alerted inside, a 2.5 s window and a 0.35 dozing range fair for a first level? Playtest.
- **Does the call need a counter?** Concept 6.8 suggests knocking the phone away. Not in this level; the window is the counter.
- **Results screen content.** The success newspaper could list what was taken and, after Phase 6, what the witnesses said.
