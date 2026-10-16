# Suspect Unknown — The Thrift Shop Level

Design and implementation plan for the second job, `Assets/Thriftshop/Thriftshop.unity`. It is the first real heist: the shop is open at dusk, a tired clerk works the register, customers browse, and a guard patrols. The register till is required; everything else is optional and randomised. It is also where the guard first meets the player in a real level, and where adaptive security (concept 6.4, `roadmap.md` Phase 8) is first built: security learns from the player's earlier completed runs of this level and places extra guard points of interest where the player tends to go.

Read `CLAUDE.md` first, then decisions 6, 15, 18, 19, 22, 23, 24 and 25 in `roadmap_npc.md`, and `Assets/apartment-level.md` for everything this level reuses (loot, the van, the newspaper, the save, the witness). Each step ends with a check to pass in play mode before moving on.

> Written 2026-10-16. Already in place: the street, the shop building (`Houses.fbx`), the `GetawayVan`, the player, the HUD, the `SketchBooth`, and empty `Loot`, `Cash`, `Doors` and `CivilianSpots` groups. Left over from copying the apartment and to be removed: `SnackPoint` and the single `StaffArea` zone.
>
> **Status (2026-10-17):** T1 is done and checked: nine room zones, `PlayerRoom` logging room changes with hysteresis, four `Entrance`s logging inbound crossings, and `JobLayout` numbering both. The NavMesh is baked (T2 still needs the guard door link, T2b). `InteriorReveal` fades the upper floor while the player is inside, as in the apartment. The sun is a low red-orange sunset matched to the `OasisSunset` skybox; T3 (interior lights, rebake) is still open. Changes made on the way: the room list grew from seven to ten, room and entrance names moved into `JobLayout` (Core), room tracking moved into its own `PlayerRoom`, and guards may now open doors to pass through (1.1, decision 19).

---

## 1. What the level is for

1. **Introduce the guard.** The first NPC who patrols, reads the cone the apartment taught, and catches. A catch fails the job at once (`FailReason.Caught`).
2. **The first real heist.** One required item, the register till, and a randomised spread of bonus loot, as in Payday's stores: the layout is learnable, what is worth taking changes every run.
3. **Teach the bag and the van.** The bag holds two items. With more than two worth taking, the player learns (optionally) to fill the bag, walk to the van, empty it with **G**, and go back for more.
4. **Introduce adaptive security.** Security watches the player's completed runs of this level. The first run has one guard serving the developers' base POIs. After one completed run, one extra POI appears where the player tends to go (the door they always use, the room they always cross). After two completed runs a second guard is hired and two extra POIs appear.
5. **Close the witness loop.** The police description saved by the apartment (W8) is now acted on: a guard who sees the player in a described outfit grows suspicious (decision 25). The counter is to change clothes **between** jobs; the player can never change appearance during a job (decision 23).

### 1.1 The layout and what each part teaches

Seen from above, the shop is one large public floor with staff rooms along the back wall.

Each area is one room of the job log (section 4), numbered by its place in `JobLayout`'s room list. Room 0 is the street.

| Room | Area           | Where                                                | Zone type | What is there                                         | What it teaches                                          |
| ---- | -------------- | ---------------------------------------------------- | --------- | ----------------------------------------------------- | -------------------------------------------------------- |
| 0    | Street         | Outside every zone                                   | none      | The van, street lamps, the four entrances             | Round trips: leaving is safe, coming back is a choice    |
| 1    | Shop front     | Behind the front window, right of the shoe section   | Public    | Window displays, customers                            | Social stealth: walking normally in an open shop is safe |
| 2    | Shoe section   | The left strip, front to back; the left front door   | Public    | Shoe wall and bench, clothing racks                   | Social stealth                                           |
| 3    | Shop middle    | The two gondola rows                                 | Public    | Gondolas, loot displays                               | The shop "fades" toward the staff area                   |
| 4    | Emergency exit | The right pocket between the register and the office | Public    | The emergency exit door, wall shelves, a loot display | A side way in, away from the register                    |
| 5    | Shop back      | Along the staff wall                                 | Public    | Furniture corner, racks; the doorways to staff rooms  | The risk grows as you go deeper                          |
| 6    | Register       | Behind the counter, front right                      | Staff     | The register till (required)                          | Timing the clerk's errands                               |
| 7    | Trash room     | Back left                                            | Staff     | Bins, the clerk's bin run                             | A quiet side way in, with a lockpick                     |
| 8    | Stockroom      | Back middle, open to the shop back (no front wall)   | Staff     | Bonus loot, the clerk's restocking                    | Greed: the best bonus loot is in staff rooms             |
| 9    | Office         | Back right, behind the StaffRoom door only           | Staff     | The safe (bonus money), a base POI                    | The greed ratchet: a long hold next to a guard's POI     |

**Entrances.** Two at the front, open during opening hours: the left one into the shoe section, the right one into the shop front. One on the left side into the trash room, locked, lock level 1. One on the right side into the emergency exit pocket, locked, lock level 1. The front entrances are static open doorways with **no** `Door` component and no anomaly: they are open because the shop is open, not because a robber opened them. The two side entrances are `Door`s, so opening one raises an `OpenDoor` anomaly (decisions 18 and 19).

**Inside, guards may pass through doors; civilians may not** (decision 19, changed 2026-10-17). The office is closed off by the `StaffRoom Door`, its only way in. A guard who needs to pass a closed door opens it without raising an anomaly and closes it once past it, so a door left open is still evidence that only the player causes. The clerk never uses doors, so every route the clerk walks (the register, the bin run through the trash room's open doorway, restocking in the stockroom, which has no front wall) is open floor. The two exterior doors have no guard link: guards stay inside.

---

## 2. Setting and the cast

### 2.1 Dusk

Evening, sunset light from a low directional sun, the shop's interior lights on, street lamps coming on. The interior lights get `GameplayLight`; the shop back is deliberately dimmer than the front. The shadow budget from `apartment-level.md` section 5 applies: at most 32 realtime additional-light shadow maps (6 per point light, 1 per spot), and the vision cones do not count toward it.

### 2.2 Who is in the shop

| NPC         | Archetype       | Role behaviour                                | Cone | Count                                        |
| ----------- | --------------- | --------------------------------------------- | ---- | -------------------------------------------- |
| Tired clerk | `TiredClerk`    | `CivilianBehaviour` with the clerk's spots    | No   | 1                                            |
| Customers   | `Civilian`      | `CivilianBehaviour`, `DistractedShopper` role | No   | 3–5                                          |
| Guard       | `SecurityGuard` | `GuardBehaviour`                              | Yes  | 1, or 2 after two completed runs (section 5) |

**Civilians when Alerted** open the decision window and, if it fills, call the police, which fails the job with `PoliceCalled`. This is the apartment clerk's window (`apartment-level.md` 2.3) used by every civilian: it fills while the civilian sees the player and drains while not. The full reaction tree (M6) is still to come; this level uses the window alone. The window is extracted from `ResidentBehaviour` into a shared component (`NpcDecisionWindow`), as `apartment-level.md` 2.4 foresaw, so the clerk at home, the clerk at work and customers share one implementation.

**The tired clerk** works the register and leaves it on errands, which are the player's openings to take the till:

| Spot       | Where                               | Stay    | Weight           |
| ---------- | ----------------------------------- | ------- | ---------------- |
| Register   | Behind the counter, facing the shop | 20–35 s | Most of the time |
| Bin run    | The trash room                      | 6–10 s  | Now and then     |
| Restocking | The stockroom                       | 8–12 s  | Now and then     |

Errands use the existing `CivilianSpot` mechanism; the clerk's spots get weights so the register wins most picks. The clerk at work is calm (decision 22): ordinary oddness barely registers. `Stealing` (taking the till in view) and `Lockpicking` weigh 1.0, so being seen taking the till in front of the clerk opens the window within about two seconds. Door chores (the clerk shutting an open side door without suspicion, M6 step 3) are optional in this level.

**The guard** patrols the base POIs and any adaptive POIs (section 5), opens the StaffRoom door to reach the office and closes it behind them, slams doors the player has left open (M5 step 2.4; not while chasing, when they run straight through), investigates the open safe and closes it (section 2.3), and catches. The guard weighs `InStaffArea` heavily and also recognises a described outfit (decision 25). Guards spawn only inside the shop, at authored spawn candidates.

### 2.3 The safe

The safe in the office holds the bonus money: one to three money stacks (bag items, `LootCarry.Bag`), rolled at load like the rest of the bonus loot.

- **Opening it** is a lockpick at lock level 1 (the same minigame as doors), so being seen opening it is `Lockpicking` conduct. Each stack is then taken with a hold, like any loot.
- **An open safe is an anomaly,** a new `AnomalyKind.SafeOpen`. A guard who notices it:
  1. investigates it (the anomaly becomes the stimulus and last-known position, decision 18),
  2. has their **baseline suspicion raised for the rest of the job** to just under `curiousAt`, so they keep patrolling but any further stimulus tips them into Curious quickly,
  3. closes the safe as an errand, the same claim-walk-react-`Resolve()` mechanism as slamming a door.
- **The office POI drains 1.5 times faster** for the rest of the job once the safe has been opened, so guards visibly check the office more often. This is a runtime factor on the POI, separate from the security plan's `DecayMultiplier`.
- The baseline is per guard: only the guard who noticed the safe is on edge.

---

## 3. Loot

### 3.1 What is taken

| Loot          | Carry  | Where                                                 | Required | Notes                                                      |
| ------------- | ------ | ----------------------------------------------------- | -------- | ---------------------------------------------------------- |
| Register till | Bag    | The register, always                                  | **Yes**  | The only required item; never moves                        |
| Money stacks  | Bag    | The safe                                              | No       | 1–3 stacks, value rolled                                   |
| Bonus items   | Bag    | Authored slots in the shop back, stockroom and office | No       | Vintage watch, guitar, record player, fur coat and similar |
| Cash          | Pocket | Scattered                                             | No       | As in the apartment; takes no bag slot                     |

The till's `ProgressUnlock` is `None` until the next job exists; the unlock flag is added together with that job (no speculative enum values).

### 3.2 The randomised roll

- **`LootSlot`** (Core): an authored spot with a pool of `LootItem`s, each with a weight, and an empty weight.
- **`LootSpawner`** (Core, one per level, on the `Job` object): at load, picks a fixed number of slots to fill (a range, for example 4–6, so the value per run stays fair), then picks an item for each from its pool, and instantiates the item's pickup. A fresh roll every run, from an unseeded `System.Random`; a debug seed field in the inspector repeats a roll while testing.
- **`LootItem`** gains the pickup prefab it spawns as.
- The required till is a placed `LootPickup`, not a slot.
- **Security does not see the roll.** The security model never uses where loot spawned (section 5): loot that moves every run would only add noise to the route model.

### 3.3 The bag and the van

The rule from `apartment-level.md` 3.2, finished here:

- **The bag holds two items.** Each bag item takes one slot; pocket cash takes none.
- **A third item is refused.** With the bag full, the loot prompt shows "Bag full" and the hold does not start.
- **`LootBag`** (Core, on the `Job` object) owns what is in the bag and what is stashed in the van, and raises `Changed`. `CarryHud`'s bag list moves into it, and `LootBagHud`'s icon fill changes from objectives collected to slots used.
- **Stashing at the van.** Near the van's **side door** with a non-empty bag, a "G" prompt appears (the same `WorldPrompt` style as doors). Pressing G empties the bag into the van. The stash point is the side door, not the back doors, so standing to stash never starts the extraction countdown by accident.
- **Objectives count when taken,** not when stashed. Leaving in the van stashes whatever is still in the bag.
- **Failing loses everything.** As before, a failed job saves nothing (`apartment-level.md` 3.5).
- **Input.** A new `Stash` action (G) in `PlayerInputs`. `PlayerVanStash` (Player) reads it and calls the van; the van never looks up the player, so dependencies stay Player → Core.

---

## 4. The job log

The record of what the player did, kept whether or not anyone saw it (decision 23). It is the only input to adaptive security.

### 4.1 Rooms and entrances

- **Rooms are the Markov states.** Each room in 1.1 is one `Zone` volume (several boxes on one object for odd shapes). The street is room 0, the implicit room when the player is inside no zone. Zones of one level must not overlap. This replaces the single `StaffArea` zone left over from the apartment: each room's zone carries its own type.
- **`JobLayout`** (Core, on the `Job` object): the level's rooms as a list of `Zone`s and its entrances as a list of `Entrance`s, in index order. A zone's room number is its place in the list plus one, an entrance's index its place in the list, handed out in `Awake` before the first physics step; the object names are the display names. It is the one place that numbers rooms and entrances, so nothing can disagree. `OnValidate` warns on empty or duplicate slots; `Start` (and a "Check Scene" context menu that works in edit mode) warns about any active zone or entrance in the scene that is not listed, and any entrance opening into an unlisted room. In a level without a `JobLayout` (the apartment) every zone reads as the street, harmlessly.
- **`PlayerRoom`** (Player): the room the player is in, with hysteresis. The raw room is the newest zone the player is still inside, else the street; it becomes the current room only after the player has stayed in it for 0.5 s, so brushing a boundary (the register counter, the stockroom's open front) logs nothing, and A → B → C taken quickly is recorded as A → C. Raises `RoomChanged(from, to)`. Assumes the player starts on the street.
- **`Entrance`** (Core): a trigger straddling one doorway to the street, with the `Zone` it opens into. A crossing counts as **inbound** when the player enters the trigger from the street side and leaves it on the room side (the entrance's local +Z points into the room). Every inbound crossing counts, so round trips through the same door reinforce it. Indoor doors are never entrances: moving between rooms is a room transition. `Door.prefab` carries an inactive `Entrance` child, switched on for exterior doors (the door's forward faces the street, so the child is turned 180°); the front doorways have their own.
- **`SecurityLayout`** (NPC, one per level, built with S4): the guard spawn candidates and the adaptive POI candidates, read alongside `JobLayout`.

### 4.2 What is recorded

`JobLog` (Core, static, reset on scene load like `JobAlert`) counts, for the current run:

- **Room transitions:** `transitions[from × rooms + to]`, incremented whenever the player's room changes. A flat array, because `JsonUtility` serializes neither 2D arrays nor dictionaries.
- **Entrance crossings:** `entries[entrance]`, every inbound crossing.

`PlayerRoom` reports room changes to `JobLog` through `RoomChanged`; each `Entrance` reports its own crossings. Loot order, tools and detections (concept 6.4) are not logged yet: nothing in this level uses them.

### 4.3 What is saved

`security.json`, a file of its own beside `progress.json` and `police.json`, so a broken file never costs the player's progress. One `SecurityRecord` per location, keyed by scene name:

```json
{
  "locations": [
    {
      "location": "Thriftshop",
      "completedRuns": 2,
      "rooms": ["Street", "Shop front", "...", "Office"],
      "entrances": ["Front left", "Front right", "Trash room door", "Emergency exit"],
      "transitions": [0.0, 1.8, ...],
      "entries": [0.0, 0.0, 0.0, 1.8]
    }
  ]
}
```

- **Written on completion only.** On `JobOutcome.Completed`, the stored counts are decayed and the run is added: `C ← 0.8 · C + run`, the same for `entries`, and `completedRuns` goes up by one. A failed run teaches security nothing and never makes the next try harder, the same rule as progress and the police record.
- **A layout change discards the record.** If the stored room or entrance **names** differ from the level's `JobLayout` (in count or in order), the record is dropped with a warning and the level plays as a first run. Comparing names rather than counts catches rooms that were reordered without being added or removed.
- **`SecurityRecord` and `SecurityStore` live in Core,** plain data with no NPC types, so the main menu can read them. The model that reads them lives in NPC.
- Tools/Suspect Unknown/Security logs, reveals or deletes the file, and can fake a number of completed runs through a chosen entrance, for testing.

---

## 5. Adaptive security

Concept 6.4, built for one location. Two stages, two kinds of problem: **predict** where the player will be, then **place** guard coverage there. It runs once, when the level loads, before the first frame of the heist. It never learns during a run (decision 24).

### 5.1 Stage A: prediction

From the `SecurityRecord`:

- **Room transitions, a first-order Markov chain.** `P[i][j] = (C[i][j] + α) / Σ_k (C[i][k] + α)`, with Laplace smoothing α = 0.5 so unseen transitions keep a small chance. The decay of 0.8 per job is already in the counts, so recent runs dominate.
- **Entrances, a Dirichlet-multinomial.** `p(e) = (entries[e] + α) / Σ (entries + α)`.
- **Occupancy.** Seed a distribution over rooms from the entrances (each entrance puts `p(e)` on the room it opens into), propagate it forward T = 6 steps through `P`, and average the steps. The street is left out of the result, since guards stay inside.

The output is two distributions: how likely each entrance is, and how much of the run the player is expected to spend in each room.

### 5.2 Stage B: placement

Greedy maximum coverage over **authored candidate POIs** (decision 15, extended by decision 24).

- **Candidates** are `PointOfInterest`s placed by the developers and inactive until the plan switches them on: about 8–10 of them, one at each entrance and a few per room. Each lists the rooms and entrances it covers.
- **Score of a set S of chosen candidates:**

  ```
  covered(x | S) = 1 − Π_{c ∈ S, c covers x} (1 − d)        d = 0.6, a candidate's detection chance
  Score(S)       = Σ_rooms occupancy[r] · covered(r | S) + Σ_entrances p(e) · covered(e | S)
  ```

- **Greedy:** repeatedly add the candidate with the largest marginal gain. Deterministic: no sampling, ties go to the lower index. The objective is submodular, so greedy carries the (1 − 1/e) guarantee.
- **How many:** as many adaptive POIs as there are guards, and only from the first completed run on:

  | Completed runs | Guards | Adaptive POIs      |
  | -------------- | ------ | ------------------ |
  | 0              | 1      | 0 (base POIs only) |
  | 1              | 1      | 1                  |
  | 2 or more      | 2      | 2                  |

  This is the fixed budget for this level: it grows per completed run and is capped at two. The guard count never shrinks.

- **Drain.** Each chosen POI's `DecayMultiplier` is `1 + gain / bestGain` (between 1 and 2), so the strongest pick drains twice as fast as its base rate. "The side entrance now drains twice as fast" is one number.
- **Solvability by authoring.** No candidate covers the register, and none covers more than one entrance. With at most two picks out of four entrances, at least two entrances always stay without an adaptive POI.

### 5.3 Applying the plan

`SecurityDirector` (NPC, one per level) runs in `Awake`:

1. Loads the record, builds the plan (stages A and B).
2. Spawns the guards. Guard _i_ spawns at the spawn candidate nearest (straight line) to the _i_-th chosen POI, or at the first spawn candidates on a first run. Spawning in `Awake` means the guards' own `Awake` runs at once and their `Start` runs with everyone else's, so `NpcPerception` still finds the player.
3. Activates the chosen candidate POIs and sets their `DecayMultiplier`.
4. Writes the explanation: one line per pick from its marginal-gain step, phrased in POI terms, for example "Guards now check the side entrance: used on 2 of your last 2 runs." The lines go to the debug overlay and the log; the AI report quotes them.

### 5.4 Visibility

- **To the player:** nothing in the level itself. POIs are never shown in game. In the main menu, the thrift shop's security line on the map board reads **"Raised security"** in red once the second guard is hired (two or more completed runs), read from `SecurityStore`. `MapLocation`'s fixed security string becomes a computed one for this location.
- **To developers and the presentation:** an in-game debug overlay (`SecurityDebugOverlay`, NPC, toggled by a debug key through `PlayerInputs`) shows the transition matrix as a heatmap, the entrance probabilities, the occupancy per room, every candidate with its score and gain, the chosen POIs with their multipliers, and the explanation lines. In the world it marks each active POI with its coverage.

---

## 6. Guards recognise a described outfit

Decision 25. The police description saved by the apartment (W8) is loaded at job start, and guards now act on it.

- **Match.** For each slot, the description's leading color and style with their confidence. When the player's current appearance matches:

  ```
  slotMatch = confidence × [color matches] × (0.5 + 0.5 × [style matches])
  match     = mean of slotMatch over the four slots
  ```

  Confidences under the "possibly" cut-off (`vagueAt`, 20 %) count as 0, so a description the newspaper could not print cannot be recognised.

- **Effect.** `match × recognition` (a new archetype weight: guard about 0.6, every civilian 0) joins the abnormality on the `Observation` and combines with conduct as `1 − Π(1 − w)` (decision 6). Recognition on its own never pushes past Investigating, like sound and anomalies: a guard who recognises an outfit comes over for a closer look; conduct on top of that tips them into pursuit.
- **The counter** is changing clothes between jobs. The description already fades by half every completed job (W8), and new evidence describing something different takes over, so a burn is temporary.
- **Dependencies.** The description and the match are NPC code (`WitnessReport`, `PoliceStore`); the observation already carries `CharacterAppearance`. Core and Player never see the description.

---

## 7. Implementation steps

Five tracks. **T** comes first because the others are tested in it; **G**, **C** and **L** can then run side by side; **S** needs the rooms from T1 and the guard from G1; **R** can start any time after G1.

### Track T — the level

**T1. Rooms, zones and entrances.** Done 2026-10-17. One `Zone` per room with its type, `PlayerRoom`, `Entrance` triggers at the four doorways, and `JobLayout` listing and numbering them (4.1). Remove `SnackPoint` and the apartment's `StaffArea`. Two side `Door`s at lock level 1; the front doorways have no `Door`.
_Check:_ the conduct readout shows `InStaffArea` in exactly the staff rooms; a debug log prints each room change and each inbound crossing, and walking out through a front doorway logs no crossing.

**T2. NavMesh.** Baked 2026-10-17. A `NavMeshSurface` bake over the shop floor and the staff rooms. The doors carve while closed.
_Check:_ a guard placed by hand can reach every room except the street and the office; opening a side door opens a path through it.

**T2b. Guards pass through doors** (decision 19). A `NavMeshLink` across the `StaffRoom Door` on a new NavMesh area `Door`; civilian agents and hearing paths exclude that area. `Door.OpenForPassage()` opens without an anomaly and leaves the lock as it was. The guard crosses links itself (`autoTraverseOffMeshLink` off): stop, open, walk across, complete the link, and close the door once outside the far reach's bounds. A chasing guard does not stop to close it.
_Check:_ the guard reaches the office POI through the closed door and the door is shut again behind them, with no anomaly raised. The clerk never paths through it. A sprint behind the closed door is not heard in the office. Chased, the player gains nothing by closing themselves in.

**T3. Dusk lighting.** Sun, interior and street lights, `GameplayLight` on the lamps that count, light probes, within the 32-shadow budget.
_Check:_ the player's light readout is clearly lower in the shop back than at the front; no shadows flicker as the camera moves.

### Track G — the guard

**G1. A guard in the level.** The `SecurityGuard` prefab placed by hand, three or four base POIs (the office important), catch failing the job.
_Check:_ the guard patrols, visits the office most, catches the player standing in the stockroom, and the caught newspaper plays.

**G2. Slamming doors** (M5 step 2.4). The guard claims an open door's anomaly, walks over, plays the confused beat, and calls `Resolve()`; the door slams. A pursuing guard ignores open doors and runs through.
_Check:_ pick the right side door and leave it open: the guard comes, looks confused and slams it. Two guards never go for the same door. Chased through an open door, the guard follows without slamming it.

**G3. The safe.** `Safe` (Core) with a lock level, the lockpick minigame, money stacks inside, and an `Anomaly` of the new kind `SafeOpen`. The guard investigates it, raises their baseline to just under `curiousAt`, and closes the safe as an errand. The office POI drains 1.5 times faster once the safe has been opened.
_Check:_ open the safe and hide: the guard comes, closes it, and from then on reaches Curious from a glance that would not have made them Curious before. The office POI's drain readout is 1.5 times its base rate.

### Track C — civilians

**C1. The shared decision window.** Extract the window from `ResidentBehaviour` into `NpcDecisionWindow`; the apartment clerk uses it unchanged.
_Check:_ the apartment plays exactly as before (B6 check).

**C2. Customers and the clerk at work.** Customers browse with `CivilianBehaviour`. The clerk has the register, bin run and restocking spots with weights. Every civilian opens the window when Alerted and calls the police when it fills.
_Check:_ the clerk spends most of the time at the register and leaves for each errand now and then. Taking the till in front of the clerk opens their window; staying in view fails the job with the police headline; ducking out of sight in time does not.

### Track L — loot

**L1. `LootSlot` and `LootSpawner`.** Authored slots, weighted pools, a fixed fill count, a fresh roll per run and a debug seed.
_Check:_ three restarts give three different spreads with the same number of items; the same debug seed gives the same spread.

**L2. The till and the objective.** The required till at the register, `JobObjectives` listing it, extraction unlocking when it is taken.
_Check:_ the van zone stays grey until the till is taken.

**L3. `LootBag`.** Two slots, the "Bag full" prompt, the HUD's bag list and icon driven by slots used.
_Check:_ with two items in the bag a third shows "Bag full" and cannot be taken; cash still can.

**L4. Stashing at the van.** The side-door stash point, the G prompt, `PlayerVanStash`, and the `Stash` input action.
_Check:_ fill the bag, walk to the van's side door, press G: the bag empties, the HUD shows two empty slots, and the extraction countdown never started. On completion, the debrief lists every item, stashed or carried.

### Track S — adaptive security

**S1. `JobLog`.** Room transitions and inbound crossings for the current run, reset on scene load, with a readout line.
_Check:_ walk street → front left → shop front → shop back → stockroom: the readout shows those four transitions and one crossing.

**S2. `SecurityRecord` and `SecurityStore`.** The JSON file, the decay-and-add on completion, the layout check, and the Tools menu.
_Check:_ complete a run: `security.json` holds it with `completedRuns` 1. Fail a run: the file is unchanged. Complete again: the old counts are 0.8 times what they were, plus the new run. Change the room list (add one, or swap two): the next load warns and plays as a first run.

**S3. Stage A** (`SecurityModel`, NPC, pure logic). Transition probabilities with Laplace smoothing, the entrance Dirichlet, forward propagation. EditMode tests: rows sum to 1; unseen transitions are above 0; a record of three runs through one entrance gives that entrance the highest probability; occupancy sums to 1 and leaves the street out.
_Check:_ the tests pass.

**S4. Stage B** (`SecurityPlanner`, NPC, pure logic). Greedy marginal gain over candidates, deterministic ties, the budget table, the drain multipliers, and the explanation lines. EditMode tests: zero completed runs pick nothing; one run picks the candidate at the most used entrance; two runs pick two different candidates; the same record always gives the same plan.
_Check:_ the tests pass.

**S5. `SecurityDirector`.** Builds the plan at load, spawns the guards at spawn candidates, activates the chosen POIs and sets their multipliers. Remove the hand-placed guard from G1.
_Check:_ with Tools → Fake Runs (two runs through the right side door): two guards spawn, and one of them visibly spends its time at the right side door.

**S6. The debug overlay.**
_Check:_ the overlay's matrix, entrance bars and chosen POIs match the record and the plan in the log.

**S7. "Raised security" in the main menu.**
_Check:_ after two completed runs the thrift shop's security line reads "Raised security" in red; with a deleted `security.json` it reads as before.

### Track R — recognition

**R1. The match** (NPC, pure logic). `match` from a `WitnessReport` and a `CharacterAppearance`, with EditMode tests: the same outfit as a confident description gives a high match; a different chest color drops that slot to 0; a description under `vagueAt` gives 0.
_Check:_ the tests pass.

**R2. Recognition in suspicion.** The archetype's `recognition` weight (guard about 0.6, civilians 0, set in every asset), the description loaded at job start, `match × recognition` combined into abnormality, capped at Investigating on its own. A readout line shows the current match.
_Check:_ finish the apartment after a good look, then start the thrift shop in the same clothes: a guard who sees the player walking normally on the shop floor grows suspicious and comes over. Change clothes in the menu and start again: the same walk raises nothing.

### Together

**X1. Teaching pass.** Place the till so the clerk's errands are the opening; put the best bonus loot in the stockroom and office so the van trip is worth it; check the first guard can be read from the cone alone.
_Check:_ a player who finished the apartment finishes the thrift shop in two or three tries, and at least one in three playtesters makes a van trip without being told.

**X2. Adaptation pass.** Play three completed runs through the same side door.
_Check:_ on the third run the player notices without being told that the guards watch "their" door, and can say why. This is the `roadmap.md` Phase 8 done-when in miniature.

**X3. Docs.** Update the status columns in `roadmap_npc.md`, tick the Phase 8 items this level covers in `roadmap.md`, and add changelog lines.

---

## 8. Open questions

- **Clerk errands.** Are 20–35 s at the register, with bin runs and restocking in between, enough openings? Playtest.
- **The bonus count.** Is 4–6 bonus items plus 1–3 money stacks enough to make a second van trip tempting but not required?
- **Recognition strength.** Is 0.6 for guards right, and is "never past Investigating on its own" too kind? A burned outfit should feel costly without making the level unwinnable.
- **Smoothing and propagation.** α = 0.5 and T = 6 are starting values. With only a few runs of data, does the first adaptive POI land where the player expects it to?
- **Two guards and the open safe.** The raised baseline is per guard. Should the guard who finds the safe open put the other guard on edge too (the first use of `Reporting`)?
- **Which job the till unlocks.** Decided with the next level.
