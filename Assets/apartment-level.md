# Suspect Unknown — The Apartment Level

Design and implementation plan for the first playable level, `Assets/Apartment/Apartment.unity`. It is the tutorial job: the player breaks into a tired clerk's apartment and steals a laptop and a cellphone. It also delivers the Phase 3 pieces in `roadmap.md` that every later job needs: loot, a fail state, a results screen, and saved progress.

Read `CLAUDE.md` first, then decisions 6, 7, 9, 13, 14, 18, 19, 21, 22 and 23 in `roadmap_npc.md`, and `Assets/doors-and-lockpicking.md`. Each step ends with a check to pass in play mode before moving on.

> Written 2026-10-13. Already in place: the street and building, doors with lockpicking (`Apartment House Door`), the roof that fades when the player walks inside (`InteriorReveal`), and the light setup described in section 5.

---

## 1. What the level is for

1. **Teach the game** by design rather than text: movement, crouching, noise, light and shadow, lockpicking, suspicion, and the decision window.
2. **Steal two items.** The **laptop** sits in the living room, where the clerk sleeps in the armchair in the corner. The **cellphone** is in the bedroom. The laptop unlocks the shop; the cellphone unlocks the next targets.
3. **Fail fairly.** If the clerk reaches Alerted and the player does not break line of sight in time, the clerk calls the police and the job fails.
4. **Teach that being seen leaves a description.** Every look the clerk gets at the player feeds the witness model (3.6). The newspaper then prints what the clerk remembers the burglar wearing, so the player learns early that NPCs remember their latest appearance, and how well depends on how good a look they got.

### 1.1 The route and what each part teaches

| Part        | What happens                                                                                         | What it teaches                                                                                                |
| ----------- | ---------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------- |
| Street      | Walk from the start to the building between street lamps                                             | Moving, crouching, sprinting is loud                                                                           |
| Front door  | `Apartment House Door`, lock level 1                                                                 | The lockpick prompt and minigame (already built)                                                               |
| Hallway     | Pools of light from the room lights, dark gaps between them                                          | Light makes you easier to see                                                                                  |
| Living room | The clerk dozes in the armchair in the corner; the laptop is in the same room                        | The vision cone, suspicion, the sleep rhythm, the decision window                                              |
| Kitchen run | Now and then the clerk gets up and walks to the kitchen for a snack, then back to the armchair       | Reading a routine: the walk is the most dangerous moment, the snack at the fridge the safest                   |
| Bedroom     | The cellphone; reaching it crosses the clerk's line of sight                                         | Timing a crossing with the sleep rhythm or the snack run                                                       |
| Getaway     | Back out through the front door to the `GetawayVan` on the street; stand at its back doors for 2.5 s | Leaving is part of the job; every job ends at the van                                                          |
| Newspaper   | The headline, and what the clerk remembers the burglar wearing                                       | Being seen is evidence: a long, close, well-lit look gives a sharp description, a glimpse a vague or wrong one |

Text is limited to small world-space control prompts on the street (move, crouch, sprint). Everything else is taught by what the player sees happen.

---

## 2. The clerk

### 2.1 Mindset

At work, a tired clerk barely notices anything (decision 22). At home it is the opposite: there is no ordinary reason for a stranger to be in their apartment, so being seen inside is alarming on its own, even while doing nothing else. The clerk's suspicion therefore rises much faster than an ordinary civilian's.

This is done through the existing rules rather than around them:

- **Same rise rate.** Decision 22 keeps one rise rate, 0.5, for every archetype. The clerk is faster because of their **conduct weights**, not a different rate.
- **The apartment is a private zone.** A `Zone` of type `Staff` covers the interior, so the player carries `InStaffArea` while inside (decision 6, context). For the clerk at home that flag weighs heavily; a customer in a shop would weigh it lightly.
- **A separate archetype,** `ApartmentClerk`, so the store's `TiredClerk` keeps its own tuning.

Starting values, to be tuned in playtests:

| Weight or value      | `TiredClerk` (store) | `ApartmentClerk` | Effect                                                                                                                                                                    |
| -------------------- | -------------------- | ---------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `inStaffArea`        | 0.25                 | 0.8              | Full view inside reaches Alerted (0.9) in about 2.3 s: 0.8 × 0.5 = 0.4 per second                                                                                         |
| `lockpicking`        | 1.0                  | 1.0              | Same everywhere (decision 22)                                                                                                                                             |
| `stealing`           | —                    | 1.0              | Taking the laptop in view is the worst thing to be seen doing                                                                                                             |
| `sprinting`          | 0.1                  | 0.4              | Running indoors is odd                                                                                                                                                    |
| `hearingSensitivity` | 0.6                  | 12               | A sprint step (9 m radius) carries 108 m of NavMesh path; closed doors still block it (decision 10)                                                                       |
| `hearingSuspicion`   | 0.4                  | 0.85             | A sprint (alarm 0.3) sets up to 0.26, past Curious out to about 23 m of path: a sprint in the bathroom or kitchen wakes them. Under `alertedAt`, so no sound alone alerts |

### 2.2 The routine: dozing in the armchair, the snack run, and the semi-dynamic cone

The clerk spends most of the level asleep in the armchair in the corner of the living room, and now and then gets up to fetch a snack from the kitchen. They are half asleep, not asleep. Perception keeps running the whole time; what changes is **how much the clerk can see**, and the vision cone shows it.

**In the armchair: the sleep rhythm.**

| State                   | Cone                                 | Lasts                           |
| ----------------------- | ------------------------------------ | ------------------------------- |
| **Dozing**              | Small: about 0.35 × range, 0.5 × FOV | 5–8 s                           |
| **Stirring** (the tell) | Grows from small to full             | about 0.6 s: head lifts, a yawn |
| **Awake**               | Full archetype range and FOV         | 2–3 s, looking around           |

- After the awake spell the clerk dozes again.
- **Anything above Idle wakes them** (a noise, something glimpsed in the small cone): the cone opens to full, and they stay awake for about 10 s, looking toward the stimulus.
- **After a scare the dozes get shorter,** so careless play gets harder.

**The snack run.** Starting values, to be tuned in playtests:

| Phase                     | What the clerk does                                               | Cone                            | Lasts       |
| ------------------------- | ----------------------------------------------------------------- | ------------------------------- | ----------- |
| **Getting up** (the tell) | Only from a doze: stretches and stands up                         | Grows from small to full        | about 1.5 s |
| **To the kitchen**        | Walks the NavMesh to a fixed snack point at the fridge or counter | Full, facing the way they walk  | the walk    |
| **Snacking**              | Stands facing the fridge or counter, back to the room             | Full, but pointed at the fridge | 4–6 s       |
| **Back**                  | Walks back and sits down                                          | Full                            | the walk    |

- **The timer:** a run starts every 45–75 s at random, counted only while dozing. The tell gives the player time to hide.
- **The walk is the danger, the snack is the opening.** Walking with the full cone sweeps the route; at the fridge the cone points at the wall, which is the moment to slip past behind them.
- **Anything above Idle during the run** works as in the armchair: the clerk stops, turns toward the stimulus and stays awake for about 10 s, then carries on with the run. Alerted starts the decision window (2.3) wherever they are.
- **After the run** the clerk sits down awake (the awake spell) before dozing again.
- **No doors on the route.** NPCs never use doors (decision 19): a closed door blocks the NavMesh. The living room and the kitchen are joined without a door, so the route is always open. The Apartment needs a `NavMeshSurface` bake covering the living room, the kitchen and the route between.
- **The kitchen is part of the staff zone** (`StaffArea` has three boxes: living room and bedroom, kitchen, and the left wing), so being seen there during a run is as alarming as in the living room.

This changes decision 21 (cones for guards and cameras only): the clerk has a cone too, because this level teaches the cone the player later reads on guards. Ordinary civilians still have none.

**How the cone changes size.** Range and FOV live in the archetype, which holds tuning only (decision 1). The change is runtime state on the NPC:

- `NpcPerception` gains a **view scale** (range scale and FOV scale, both 1 by default), set by the role behaviour and eased over the stirring time. Perception uses `ViewDistance × rangeScale` and `FovDegrees × fovScale` wherever it reads range and FOV today (`InViewCone`, the distance and angle factors).
- `NpcVisionCone` keeps its fan and ray directions built once at **full** size (they are fixed after `Start`). It casts rays only to the scaled range and writes distances as fractions of the full range as before, so the shader cuts the cone at the scaled range with no change. It passes `_RangeScale` and `_FovScale` to the shader every frame. The shader samples the distance falloff at distance ÷ `_RangeScale`, uses `_HalfFov × _FovScale` for the angle falloff, and cuts outside that scaled half-angle with a short fade. `_HalfFov` itself stays the full half-angle, because it also maps each pixel to its ray in `_RayData`, and the rays always span the full cone. Built in B3.
- **Rule from decision 21:** where the cone and perception disagree, perception is the truth and the cone gets fixed. Check both scales against the debug rays.

### 2.3 The decision window and the call

Reaching Alerted is the clerk's equivalent of a guard starting to chase. The same window runs in the armchair and on the snack run: the clerk stops where they are and never chases.

1. **Deliberating** (decision 9): the clerk reaches for the phone and a progress indicator appears above them. Progress fills over about 2.5 s **while the clerk can see the player**, and drains while they cannot.
   - Full: the clerk starts **Calling**.
   - Empty: the clerk lost them. They go back to awake, looking around, and suspicion decays as normal.
2. **Calling:** the phone goes to the ear. After a short beat for the animation (about 1.5 s) the call connects and the job fails with `PoliceCalled`. The call itself cannot be interrupted in this level.

The window runs on sight rather than on suspicion dropping, because suspicion only starts decaying after the archetype's decay delay, which can be longer than the window. Breaking line of sight is the action the level teaches, so it is what the window measures.

`CivilianActivity` gains `Dozing`. The clerk's states are `Dozing`, `Working` (awake in the armchair, looking around, and snacking), `Roaming` (walking to the kitchen and back), `Deliberating` and `Calling`.

### 2.4 Where it lives

`ResidentBehaviour` is the clerk's role behaviour (decision 2: shared perception components plus one role behaviour). It is a separate class from `CivilianBehaviour`, because its routine is a sleep cycle in one seat with one fixed errand (the snack run), not walking between spots, and its reaction is fixed rather than chosen by a reaction tree. If M6's decision window turns out to share real code with this one, extract it then.

---

## 3. The job flow

### 3.1 Outcome events

`JobOutcome` (Core) is a static class with two events, `Failed(FailReason)` and `Completed`, and methods to raise them. `FailReason` is `Caught` (a guard caught the player) and `PoliceCalled` (a civilian finished a call).

- NPC code calls into Core, so dependencies stay NPC → Player → Core.
- `GuardBehaviour.Caught` raises `JobOutcome.Fail(FailReason.Caught)`. This replaces the test-bench `ReloadOnCaught`.
- **Only the first outcome counts.** A failure after success, or a second failure, is ignored.
- **Trap: static state survives scene reloads,** and survives play-mode entry when domain reload is off. Reset the "ended" flag on every scene load (`SceneManager.sceneLoaded`, registered from a `[RuntimeInitializeOnLoadMethod]`).

### 3.2 Loot and the bag

- **`LootItem`** (Core, ScriptableObject): display name, take time, where it is carried (`LootCarry`: `Bag` or `Pocket`), what it unlocks (`ProgressUnlock`: `Shop`, `Targets`, flags) and its money value. Data only.
- **`LootPickup`** (Core, interactables): the world object. Reach trigger, outline and `WorldPrompt`, the same pattern as the door. `Take()` raises a static `Taken(LootPickup)` event and hides it.
- **`PlayerLootInteraction`** (Player): hold E to take an item in reach, for the item's take time. The player carries the `Stealing` conduct flag during the hold.
- **`JobObjectives`** (Core, one per level): lists the required items, counts the ones taken, and raises `Changed`. Exposes `Collected`, `Required`, `IsComplete` (every required item taken), and each slot through `RequiredItem(i)` and `IsTaken(i)`. Required items are the minimum to leave: in this level that is the laptop and the cellphone. A level with nothing required is complete from the start.
- **Extra loot, from this level on.** Anything not on the required list is optional: worth money, never needed to leave. The apartment has cash spread through it.
- **Two kinds of money, one currency.** _Cash_ is pocket change lying around: carried in the pockets, never takes bag space. _Money_ is large stacks (from the thrift shop on): carried in the bag, so it has to be carried to the van, in several round trips if the bag is full. Both add to the same `money` total in the save.
- **The bag holds two items, in every job.** A hard limit, not a per-level setting: each bag item takes one slot, and pocket cash takes none.

**The job HUD** (`JobHud.prefab`, nested in `PlayerHud`; dark chamfered panels, thin lines, one sodium-orange accent):

| Corner                   | Shows                                                                                                                                                                                                                         | Driven by                                                                         |
| ------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------- |
| Top left, job card       | Job tag and title (set per level), the elapsed time, and a checklist of the required items, ticked as each is taken                                                                                                           | `JobClock`, `ObjectiveListHud` with `ObjectiveRow`                                |
| Top right                | Status tag (UNDETECTED, SUSPICIOUS, SEARCHING, ALARM): the most alarmed NPC in the level                                                                                                                                      | `AlertStatusHud`, fed by `JobAlert`; every NPC reports through `NpcAlertReporter` |
| Bottom left, carry panel | The bag icon, filling as required loot is taken; a tree of the bag's two slots under "Bag", each showing its item or "Empty", so the panel never changes size; the pocket cash total; "+ ITEM" above the panel on each pickup | `LootBagHud`, `CarryHud`, `PickupToast`                                           |
| Bottom right             | Ability slots with uses left                                                                                                                                                                                                  | Placeholder until abilities exist                                                 |

Still to come with the van and the thrift shop: a carry model that refuses a third bag item and is emptied at the van on each trip (the bag list in `CarryHud` moves into it), and the bag icon's fill changing from collected over required to slots used.

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

| Outcome                 | Examples                                                                                                                              |
| ----------------------- | ------------------------------------------------------------------------------------------------------------------------------------- |
| Caught or police called | "Burglar caught during break-in", "Thief caught in the act", "Sleepy tenant foils clumsy burglar"                                     |
| Success                 | "Mystery burglary baffles neighbours", "Laptop and phone vanish from sleeping tenant's flat", "Police clueless after daring break-in" |

Phase 6 reuses this newspaper prefab for the witness report, so build it as a prefab with replaceable text from the start.

**The witness sketch** (Track W, W7). The paper's grey picture box shows the player as the clerk remembers them: the player model in the first frame of its idle pose, dressed in the description from the witness model (3.6), printed in grayscale on the same grey background. Grayscale hides colors, so the caption under the photo names them: "Witness description: brown cap, red jacket, possibly grey trousers." The image depends on the outcome:

| Outcome | Image | Title and caption |
| --- | --- | --- |
| Caught | The sketch as a mugshot, with height lines behind the figure | "Image of the caught criminal", with the description caption |
| Completed, seen | The sketch as a wanted poster, with no height lines | "Police seek this person", with the description caption |
| Completed, never seen | No figure: the grey "?" placeholder | "No witnesses came forward" |

The sketch shows every outfit slot the clerk can describe in its leading style and color. A slot the clerk cannot describe shows a flat silhouette, like an unfinished police sketch, and a slot the clerk is unsure of fades toward the paper's grey. The player learns the same lesson on both outcomes: what the witness saw is what the paper prints.

### 3.6 The witness: what the clerk remembers

This level is where the full witness model (`roadmap_npc.md` M7, decision 23) is built and first taught, on its one witness. The model is about **appearance only**: what the player looked like. What the player _did_ (which door, which route) is the job log's business and feeds adaptive security in Phase 8, not the witness.

**Feature slots.** The four categories of `PlayerCustomization`: **Head**, **Chest**, **Legs**, **Feet**. Each part name carries a style and a color (`Chest_002_Black` is style 002, color Black), and the witness holds a separate belief for each. A poor look can get the color right and the style wrong, or leave only "something black".

| Slot  | Seen through body region |
| ----- | ------------------------ |
| Head  | Head                     |
| Chest | UpperBody (and Hands)    |
| Legs  | LowerBody                |
| Feet  | Feet                     |

A slot gets evidence from an observation only if its region was visible (the region bitmask on `Observation`, decision 14). Feet hidden behind a sofa means nothing is learned about the shoes.

**Quality** of one observation, from concept 6.2:

```
q = f_dist · f_angle · f_light · f_duration · f_stance · observerSkill · featureVisibility
```

`f_dist`, `f_angle`, `f_light` and `f_stance` already come with every `Observation`. `f_duration` grows over one continuous sighting, so a glance and a long stare differ. `observerSkill` and the per-slot `featureVisibility` come from the archetype's new witness block; the clerk at home is a poor observer (skill about 0.4).

**Beliefs.** Each slot holds a log-odds belief over styles and one over colors (`FeatureBelief`). Each observation adds evidence for the value the witness _thinks_ it saw, weighted by `q` and by the look's length. Confidence in a value is its share of all scores, with values never seen counted at the even start (10 colors, 3 styles).

**Confusion.** Below a quality threshold (`clearLook`, 0.35) a look can take in a wrong value, more often the worse the look. Colors come from one `ConfusionMatrix` asset (`ColorConfusion`) shared by every slot, since black reads as grey on a jacket and on shoes alike: each color lists its likely mistakes with weights (black as grey, blue or brown). Styles have no similar neighbours, so a wrong style is any other style. The chance of being wrong is capped (`worstWrongChance`, 0.5, at most 0.6) so the truth stays the single most likely recall: a poor witness is vague and sometimes wrong, never consistently wrong. Without the cap, many poor looks made a mistake outscore the truth.

**Memory and the latest appearance.** Beliefs decay between sightings at the archetype's retention rate; the clerk forgets fast. Each observation reads the player's appearance at that moment, so a fresh good look outweighs an old poor one, and a mid-job change of clothes (later levels) gives a description of what the player wore when last seen well.

**The report.** At job end, `NpcWitness` produces a `WitnessReport`: per slot the most likely style and color with a confidence. The newspaper shows it as a sketch of the player (3.4), with a caption that turns confidence into words: certain, "probably", "possibly", or left out. Several witnesses (later levels) merge in log-odds.

**What it deliberately does not do yet.**

- **Saved, but not yet acted on.** The police description is saved when a job is completed and loaded at job start (W8). A failed job saves nothing, as with progress (3.5): otherwise a failure could leave the player described so well that the level turns unwinnable: the start of Phase 7's police profile. Nothing in the apartment reacts to it yet; guards and cameras that look for a described outfit come with Phase 7.
- **Burned clothes are temporary** (Phase 7): an item counts as burned for one or two jobs, or until newer witness evidence describes something different. Never permanently. The saved description already behaves this way: it fades every job, and new evidence outweighs it.
- **The sketch is one still image.** No animation and no live camera: one render at job end (W7).

### 3.5 Saved progress

A small JSON file, `progress.json` in `Application.persistentDataPath`, written with `JsonUtility`:

```json
{ "shopUnlocked": true, "targetsUnlocked": true, "money": 670, "completedJobs": ["Apartment"] }
```

- `ProgressSave` is the plain serializable data; `ProgressStore` (static) loads and saves it, and treats a missing or broken file as a fresh save.
- `JobRewards` (on the level's `Job` object) adds up the value and unlocks of everything taken, required or extra. On `JobOutcome.Completed` it loads the save, adds them, records the job by scene name, and saves.
- **Written on job success only.** Taking an item and then failing unlocks nothing and earns nothing. A replayed job pays again but is listed once.
- Tools/Suspect Unknown/Progress logs, reveals or deletes the file while testing.
- The Phase 7 save system (police profile, wardrobe) grows from this file rather than replacing it.

---

## 4. Implementation steps

The work splits into two tracks that can run side by side. **Track A** is the job flow (Core and Player). **Track B** is the clerk (NPC). Step A1 is the seam between them, so it comes first.

### Track A — the job flow

**Status (2026-10-14):** A1–A7 are done and checked in play mode, with the job HUD (3.2) and cash in the level. A7 is next. Progress notes are in `handovers/apartment-job-flow.md`.

**A1. `JobOutcome` and `FailReason`.** The static events from 3.1, the scene-load reset, and `GuardBehaviour.Caught` raising `Fail(Caught)`. Delete `ReloadOnCaught`.
_Check:_ on the test bench, the guard catches the player and one failure is logged. A second catch logs nothing.

**A2. The newspaper and `JobOutcomeScreen`.** Greyscale volume, slow motion, the sliding newspaper, headlines from `HeadlineSet`, restart and continue. Add a `[ContextMenu]` for each outcome to test with.
_Check:_ both context menu entries play their sequence; restart plays at normal speed afterwards.

**A3. Conduct flags `Lockpicking` and `Stealing`.** Add each to `ConductFlags` together with the code that sets it: `PlayerLockpicking` while picking, `PlayerLootInteraction` while taking (A4). Add the matching weight to `ConductWeights` and set it in every archetype asset; a new field starts at 0 otherwise. Lockpicking is 1.0 for every archetype (decision 22).
_Check:_ the conduct debug readout shows `Lockpicking` while picking the front door.

**A4. Loot.** `LootItem`, `LootPickup` and `PlayerLootInteraction`. Create the laptop and cellphone assets and place both pickups.
_Check:_ holding E takes each item, it disappears, and `Stealing` shows during the hold only.

**A5. `JobObjectives` and `LootBagHud`.**
_Check:_ the bag starts as an outline, is half full after one item and full after both, and `IsComplete` turns true with the second item.

**A6. `ProgressSave` and `ProgressStore`.**
_Check:_ a `[ContextMenu]` success writes `progress.json` with both unlocks. After a failure, it is unchanged.

**A7. The getaway van, part 1: zone, timer and indicator.** `GetawayVan` with the locked or unlocked zone, the rising and draining timer, and the ground indicator. For now, reaching 1 just calls `JobOutcome.Complete()`.
_Check:_ with one item the marking is grey and locked and standing in it does nothing. With both it pulses; standing in it fills it in about 2.5 s; stepping out drains it; a full timer plays the success newspaper. The clerk calling during the countdown still fails the job.

**A8. The getaway van, part 2: the departure.** `Departed` event, `PlayerExtraction`, door hinges, door close, the drive, the van camera, and the success delay on `JobOutcomeScreen`.
_Check:_ at lock-in the player steps into the van and disappears, the doors swing shut, the camera follows the van out of the street, and the newspaper slides in over it. No errors from NPCs or the camera while the player is hidden.

### Track B — the clerk

**Status (2026-10-14):** B1–B6 are done and checked in play mode: the clerk sees, dozes, wakes, makes snack runs, and calls the police. B7 is done too: animations, and a ring in the HUD's status tag that fills with the decision window (`DecisionWindowHud`), visible even with the clerk off screen. Track B is complete; Track W is next.

**B1. Data.** `ApartmentClerk.asset` holds the values in 2.1, and the `StaffArea` zone covers the interior; both exist. Remaining: an `ApartmentClerk` prefab variant of `Civilian` (archetype set, `CivilianBehaviour` off until B4) seated in the armchair in the corner of the living room.
_Check:_ with only the shared perception components on the clerk, standing in view inside reaches Alerted in about 2.3 s. The same spot outside the zone raises nothing.

**B2. View scale on perception.** `NpcPerception` gains the range and FOV scale from 2.2.
_Check:_ at scale 0.35 the debug rays stop at the shorter range, and detection only happens inside it.

**B3. View scale on the cone.** `NpcVisionCone` and the shader follow the scale. Put a cone on the clerk.
_Check:_ at both scales, the cone's edge and the debug rays agree; easing between them shows no popping.

**B4. `ResidentBehaviour`: the sleep rhythm.** In the armchair: dozing, stirring and awake on timers, with the view scale eased between states, waking on anything above Idle, and shorter dozes after a scare. Add `Dozing` to `CivilianActivity`.
_Check:_ the player can stand at the edge of the room while the clerk dozes; a sprint wakes them; the tell is visible before every waking.

**B5. The snack run.** Bake a `NavMeshSurface` over the living room, the kitchen and the route between (no door on it). Add a snack point in the kitchen. `ResidentBehaviour` gains the run from 2.2: the timer counted while dozing, the getting-up tell, the walk, snacking with the back to the room, the walk back, and the awake spell after sitting down. Anything above Idle on the way pauses the run as in the armchair.
_Check:_ the clerk gets up only from a doze and only after the tell, walks to the kitchen and back without getting stuck, faces the fridge while snacking, and slipping past behind them then goes unnoticed. Being seen on the walk raises suspicion as fast as in the armchair.

**B6. The window and the call.** Deliberating with sight-driven progress, then Calling, then `JobOutcome.Fail(PoliceCalled)`, in the armchair or on the run.
_Check:_ seen, then ducking out of sight within the window, ends with the clerk awake and searching. Staying in view fails the job with the police headline. The same holds when seen during the snack run.

**B7. Presentation.** Mixamo clips: asleep in the armchair, head lift and yawn, stretching and standing up, walking, snacking at the fridge, sitting down, reaching for the phone, phone call. The suspicion indicator and a progress ring during the window.
_Check:_ someone who has not seen the code can tell from the animation alone when the clerk is about to wake, about to get up, and about to call.

### Track W — the witness (right after Track B)

Built on the clerk once they can see and react. Design in 3.6; done-when from `roadmap_npc.md` M7.

**Status (2026-10-15):** W5 is built (memory half-life per archetype; the clerk's is 90 s). W6 is done and checked: at job end every witness's report merges into the police description and is logged ("no witnesses came forward" when nobody saw the player), and the seven merge tests pass in EditMode (`Assets/Testing/Editor/WitnessMergeTests.cs`). W1–W4 are done and checked in play mode: the clerk builds per-slot beliefs about style and color from every sighting, learns nothing about slots whose regions were hidden, and stays vague but mostly right on poor looks. With skill 0.4 he needs about 6 s of a steady, close, lit look to be sure (about 90 % on colors). Memory retention moved from W2 to W5, where it is first used; the slot-to-region mapping is a fixed rule in `NpcWitness`, not archetype data. W7.1–W7.4 are done and checked: the `SketchBooth` (layer 17, one per job scene) poses a mannequin in idle frame 0, dresses it from the police description (see-through when unsure, a silhouette when unknown, the nearest existing part when the combination does not exist), renders it once at job end after a warm-up render at load, and hands the caption to the newspaper through `JobNews` (Core). W7.5 is built and verified on all three outcomes: the paper's photo is now square (225.5 × 225.5, `WitnessSketch.renderTexture` 512 × 512 in `Core/UI/Textures`), shown through the `Grayscale` Canvas Shader Graph, with height lines when caught, none when the player got away, and the "?" with "No witnesses came forward" when nobody saw them; the fake text column under the photo became the real caption. W8 (the police record: `PoliceStore`, `police.json`, loaded at job start, faded by half per completed job and merged when the job is completed; a failed job saves nothing) is written and compiles but not yet tested; the test steps are in `handovers/witness-model.md`. The W7 playtest check is still open.

**W1. The player's appearance.** `CharacterAppearance` (Player) holds the four slots' style and color, read from `PlayerCustomization`'s active parts, and is read at each sighting.
_Check:_ a debug readout shows the current outfit as four style and color pairs; changing a part in play mode updates it.

**W2. Witness data.** `FeatureSlot` enum; the archetype's witness block: observer skill and per-slot feature visibility (`FeatureVisibility`).
_Check:_ the clerk's archetype shows the block with skill about 0.4; `OnValidate` warns when every slot's visibility is 0.

**W3. Beliefs.** `FeatureBelief` in log-odds; `NpcWitness` adds evidence per observation weighted by quality, with `f_duration` growing over one continuous sighting. A live readout of each slot's leading style, color and confidence.
_Check:_ a long, close, lit look drives every visible slot to high confidence in the true values; a glance barely moves them; crouched behind cover, Feet and Legs stay unknown.

**W4. Confusion.** One `ConfusionMatrix` for colors shared by every slot, and a uniform wrong style; below the quality threshold the observed value is sometimes a mistake, with a capped chance so the truth stays the most likely recall.
_Check:_ repeated poor looks from the edge of the cone produce some wrong colors (black as dark grey), never wildly wrong ones; good looks stay correct.

**W5. Memory.** The archetype's memory half-life; every belief fades toward the even start at that rate (0 never forgets).
_Check:_ seen well, then hidden for a while: confidence fades in the readout. Seen again, it climbs back, and the description follows the latest good look.

**W6. The report.** `WitnessReport` at job end: per slot the style and color evidence in log-odds, and the leading values with their confidence. Every witness in the job merges into one police description (log-odds add). Unit tests for the merge (Unity Test Framework).
_Check:_ the report matches the readout's last state; the merge tests pass.

**W7. The witness sketch.** The newspaper's picture box shows the description as a figure (design in 3.4, "The witness sketch"):

- **The sketch booth.** A mannequin copy of the player model stands out of sight, on its own render layer, held in the first frame of the player's idle clip (an Animator with one idle state at speed 0).
- **Dressing.** For each slot, the mannequin shows the part named by the report's leading style and color (`Chest_001_Red`, the naming that `PlayerAppearance` reads). An unknown slot shows a flat silhouette, and a low-confidence slot fades toward the paper's grey.
- **One render.** When `JobWitnesses` produces the description, a camera that sees only the sketch layer renders the mannequin once into a RenderTexture, then switches off.
- **The paper.** A `RawImage` in the picture box shows the RenderTexture through a grayscale Canvas Shader Graph material, on the existing grey background. The backdrop is height lines when caught and a wanted poster when completed. The title and caption follow the outcome table. The caption names each slot's colors with confidence words: certain, "probably" or "possibly", or the slot left out. "No witnesses came forward" keeps the "?" placeholder.
- **Dependencies.** The dressing and render code is NPC: it reads `WitnessReport` and the player's part names. The newspaper (Core) only holds the RawImage and a RenderTexture asset, and receives the caption text through a Core hook, so Core never references witness types. The sketch layer goes in the CLAUDE.md layers table.

_Check:_ someone who played once can tell from the paper alone how good a look the clerk got, and says unprompted that they would change clothes (roadmap Phase 6 gate check).

**W8. The saved description.** When a job is completed, the job's police description is saved (its own JSON file beside `progress.json`, so a broken file never costs the player's progress); at job start it is loaded. A failed job still produces the description for the mugshot but saves nothing, as with progress, so a failure never makes the next attempt harder. Before saving, the loaded description fades by one job and the new one merges into it, so old evidence counts for less each job and newer evidence that describes something different takes over (burns are temporary). A missing or broken file loads as "no description". The data is plain values (slot, style, color, scores), owned by the NPC side, so Core and Player stay free of NPC types.
_Check:_ finish a job after a good look, then start again: the log (and a readout line) shows the loaded description. Get caught: the next start shows the record unchanged. Change clothes, get seen well and finish: at the next start, the changed slots follow the new outfit. A completed run with no sightings leaves the old description, one job fainter. Delete the file: the next start loads "no description" without errors.

### Together

**C1. Teaching pass.** `GameplayLight` on the room lights and street spots (the scene has none yet, so the light level always reads as dark). World-space control prompts on the street. Place the laptop so that taking it needs the dozing window.
_Check:_ a first-time player finishes without instructions in two or three tries, and can say why they failed when they do.

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

- **Clerk tuning.** Are 2.3 s to Alerted inside, a 2.5 s window and a 0.35 dozing range fair for a first level? Playtest.
- **Snack run defaults.** A run every 45–75 s, only from a doze, with a 1.5 s getting-up tell, 4–6 s at the fridge with the back to the room, and the same decision window if the player is seen on the way. These were chosen as defaults when the snack run was added; confirm or change them in playtests.
- **Witness numbers.** The quality threshold for confusion, the confusion tables, the clerk's retention, and the confidence cut-offs for "probably" and "possibly". Playtest against the W7 check.
- **Does the call need a counter?** Concept 6.8 suggests knocking the phone away. Not in this level; the window is the counter.
- **Results screen content.** The success newspaper could list what was taken and, after Phase 6, what the witnesses said.
