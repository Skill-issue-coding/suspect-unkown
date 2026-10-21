# Suspect Unknown — The Thrift Shop Level

Design and implementation plan for the second job, `Assets/Thriftshop/Thriftshop.unity`. It is the first real heist: the shop is open at dusk, a tired clerk works the register, customers browse, and a guard patrols. Nothing is strictly required: the story item, the owner's plan for robbing the jewelry store, is in the office safe, and the van opens once the player has taken the plan or any bag item (changed 2026-10-21: the register till is removed). It is also where the guard first meets the player in a real level, and where adaptive security (concept 6.4, `roadmap.md` Phase 8) is first built: security learns from the player's earlier completed runs of this level and places extra guard points of interest where the player tends to go.

Read `CLAUDE.md` first, then decisions 6, 15, 18, 19, 22, 23, 24 and 25 in `roadmap_npc.md`, and `Assets/apartment-level.md` for everything this level reuses (loot, the van, the newspaper, the save, the witness). Each step ends with a check to pass in play mode before moving on.

> Written 2026-10-16. Already in place: the street, the shop building (`Houses.fbx`), the `GetawayVan`, the player, the HUD, the `SketchBooth`, and empty `Loot`, `Cash`, `Doors` and `CivilianSpots` groups. Left over from copying the apartment and to be removed: `SnackPoint` and the single `StaffArea` zone.
>
> **Status (2026-10-17):** T1 is done and checked: nine room zones, `PlayerRoom` logging room changes with hysteresis, four `Entrance`s logging inbound crossings, and `JobLayout` numbering both. The NavMesh is baked, and guards pass through the StaffRoom door (T2b, 2026-10-19). `InteriorReveal` fades the upper floor while the player is inside, as in the apartment. The sun is a low red-orange sunset matched to the `OasisSunset` skybox; T3 (interior lights, `GameplayLight`, rebake) is done (2026-10-21). G1 and G2 are done (2026-10-21). C9's customer personalities are dropped (2026-10-21). G3 is done except the money stacks (2026-10-21): the safe is an anomaly the guard closes with the slam clip, the guard who saw it stays on edge, and the office POI drains 1.5 times faster. The level's post-processing profile, `ThriftShopVolume`, has a sunset grade (warmer white balance and color filter, violet shadows and orange highlights in split toning); bloom, depth of field, vignette and the other effects are untouched. S1 (`JobLog`) is done and checked. S2 (`ActionRecord`, `ActionStore`, `ActionRecorder`, the Action Record menu) and S2b (Fake Runs) are done and checked. S3 (`ActionModel`) and S4 (`SecurityPlanner`) are done, with their EditMode tests passing. Until L2, runs are completed with Test Complete on `JobOutcomeScreen`: `JobObjectives` still lists the apartment's laptop and cellphone, which are not in the scene. C1 (`NpcDecisionWindow`), C2 (`CivilianSpawner`, the clerk's errands, the police call) and C5 (civilian animation) are done and checked (2026-10-19); the C3 witness check is postponed. Spots carry no weights: the clerk picks evenly and the register's long stay keeps them there most of the time. Detection tuning is left to playtesting. The rest of the civilian system (M6: witnessing, door chores, animation, the reaction tree, panic contagion, personalities) moved into this level as C3–C9 and section 2.4 on 2026-10-19. C6 and C6b are done and checked (2026-10-19); on the way the flee outcome was replaced by scared (civilians freeze, cover their face and calm down after a while), and the exit-distance input went with it. Changes made on the way: the room list grew from seven to ten, room and entrance names moved into `JobLayout` (Core), room tracking moved into its own `PlayerRoom`, and guards may now open doors to pass through (1.1, decision 19).

---

## 1. What the level is for

1. **Introduce the guard.** The first NPC who patrols, reads the cone the apartment taught, and catches. A catch fails the job at once (`FailReason.Caught`).
2. **The first real heist.** A story item, the jewelry store plan in the office safe, and a randomised spread of bonus loot, as in Payday's stores: the layout is learnable, what is worth taking changes every run. Which bonus loot the player can reach depends on the tools they own (3.4).
3. **Teach the bag and the van.** The bag holds two items. With more than two worth taking, the player learns (optionally) to fill the bag, walk to the van, empty it with **G**, and go back for more.
4. **Introduce adaptive security.** Security watches the player's completed runs of this level. The first run has one guard serving the developers' base POIs. After one completed run, one extra POI appears where the player tends to go (the door they always use, the room they always cross). After two completed runs a second guard is hired and two extra POIs appear; after three, a third extra POI.
5. **Close the witness loop.** The police description saved by the apartment (W8) is now acted on: a guard who sees the player in a described outfit grows suspicious (decision 25). The counter is to change clothes **between** jobs; the player can never change appearance during a job (decision 23).
6. **Make civilians matter.** This is the first level that is not a tutorial, so the whole civilian system (M6) is built and played here, not only the apartment clerk's fixed police call: customers and staff who witness, a visible decision window, and reactions that differ in what they cost and what they teach the witness model (2.4). Concept 6.8's "calm witnesses are the dangerous ones" is the done-when.
7. **Carry the story.** The plan in the safe is what unlocks the jewelry store (3.5). As in Payday, the job can be finished without it; only the next target stays locked.

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
| 6    | Register       | Behind the counter, front right                      | Staff     | The clerk's counter (the till was removed 2026-10-21) | Timing the clerk's errands                               |
| 7    | Trash room     | Back left                                            | Staff     | Bins, the clerk's bin run                             | A quiet side way in, with a lockpick                     |
| 8    | Stockroom      | Back middle, open to the shop back (no front wall)   | Staff     | Bonus loot, the clerk's restocking                    | Greed: the best bonus loot is in staff rooms             |
| 9    | Office         | Back right, behind the StaffRoom door only           | Staff     | The safe (the plan, bonus money), a base POI          | The greed ratchet: a long hold next to a guard's POI     |

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
| Customers   | `Civilian`      | `CivilianBehaviour`, Customer role            | No   | 3–5                                          |
| Guard       | `SecurityGuard` | `GuardBehaviour`                              | Yes  | 1, or 2 after two completed runs (section 5) |

**Civilians when Alerted** open the decision window and, if it fills, act on the outcome the reaction tree chose (2.4); a call fails the job with `PoliceCalled`. This is the apartment clerk's window (`apartment-level.md` 2.3) used by every civilian: it fills while the civilian sees the player and drains while not. It is the `Deliberating` state of the reaction tree. The window is extracted from `ResidentBehaviour` into a shared component (`NpcDecisionWindow`), as `apartment-level.md` 2.4 foresaw, so the clerk at home, the clerk at work and customers share one implementation.

**The tired clerk** works the register and leaves it on errands, which change who is watching the shop floor, the trash room and the stockroom:

| Spot       | Where                               | Stay    | Station    |
| ---------- | ----------------------------------- | ------- | ---------- |
| Register   | Behind the counter, facing the shop | 20–35 s | `Register` |
| Bin run    | The trash room                      | 6–10 s  | `Bin`      |
| Restocking | The stockroom                       | 8–12 s  | `Restock`  |

Errands use the existing `CivilianSpot` mechanism. Spots have no weights (decided 2026-10-19): the clerk picks a free spot evenly, and the register's stay, about three times an errand's, keeps the clerk there most of the time. Each spot's `CivilianStation` picks the clip the clerk plays there. The clerk at work is calm (decision 22): ordinary oddness barely registers. `Stealing` (taking loot in view) and `Lockpicking` weigh 1.0, so being seen taking loot in front of the clerk opens the window within about two seconds. The clerk ignores open doors, like every civilian (C4 dropped 2026-10-21). There is no manager (dropped 2026-10-19, with the `DistractedShopper` and `Manager` archetypes: nothing in the game needs them). The clerk has a reaction tree of their own, separate from the customers' (C6).

**The guard** patrols the base POIs and any adaptive POIs (section 5), opens the StaffRoom door to reach the office and closes it behind them, slams doors the player has left open (M5 step 2.4; not while chasing, when they run straight through), investigates the open safe and closes it (section 2.3), and catches. The guard weighs `InStaffArea` heavily and also recognises a described outfit (decision 25). Guards spawn only inside the shop, at authored spawn candidates.

### 2.3 The safe

The safe in the office holds the bonus money: one to three money stacks (bag items, `LootCarry.Bag`), rolled at load like the rest of the bonus loot.

- **Opening it** is a combination dial (changed 2026-10-20 from the door lockpick): turn the dial either way until it catches, which it does only on the next number of the combination (the dial jumps, the handle kicks, the readout turns green), and hold tension to test it. The turning direction does not matter (changed the same day: the right-left-right rule and the wider catch zone made the hint too vague). A test at the number sets it; near it the test binds and, held too long, slips. Numbers already set stay set if the player backs off, so leaving when a guard comes costs only the number in progress. Being seen at it is `Lockpicking` conduct. Each stack is then taken with a hold, like any loot.
- **An open safe is an anomaly,** a new `AnomalyKind.SafeOpen`. A guard who notices it:
  1. investigates it (the anomaly becomes the stimulus and last-known position, decision 18),
  2. has their **baseline suspicion raised for the rest of the job** to just under the point where Curious calms back to Idle (`curiousAt − hysteresis − onEdgeMargin`, 0.11 for `SecurityGuard`), so they keep patrolling but any further stimulus tips them into Curious quickly. Not just under `curiousAt` itself: between the two, a guard who once reached Curious could never calm down. `NpcSuspicion.Baseline` only goes up, and a sighting raises it even mid-chase or when another guard has the errand,
  3. closes the safe as an errand, the same claim-walk-react-`Resolve()` mechanism as slamming a door.
- **The office POI drains 1.5 times faster** for the rest of the job once the safe has been opened, so guards visibly check the office more often. This is a runtime factor on the POI, separate from the security plan's `DecayMultiplier`.
- The baseline is per guard: only the guard who noticed the safe is on edge.

### 2.4 Civilian reactions

The rest of M6 (`roadmap_npc.md`), built here, with concept 6.8 and decision 22 as the rules. Civilians witness all the time and alarm only on criminal acts; what they do next is a reaction tree, so the player can read a room and guess who is dangerous.

- **Witnessing (M6 step 2).** `NpcWitness` accumulates what each civilian has seen, calm or not, with the debug readout. It exists on civilians already; this level plays it. Customers, clerk and manager all feed the police description (W8).
- **The tree.** `ReactionTree` and `ReactionNode` as ScriptableObjects, branch and leaf subclasses, one tree per archetype, so a role is data and not code. Coarse branching on the inputs in this order: severity of what was witnessed, role, whether they think the player saw them, distance to the player. Weighted scores at the leaves, then sampled softly, not the top result.
- **Outcomes in this level:** discreet call (visible phone, so interruptible; starts the police timer and fails the job with `PoliceCalled` when it connects), scared (freezes in place, covers their face, looks at the floor and shakes; sees nothing while scared, so the witness gathers no evidence and suspicion decays naturally; after a random time between a minimum and a maximum they calm down and go back to their routine), avoid or walk away (almost nothing). Pretend not to see is not built (decided 2026-10-19: too much work for what it adds now), so the long, clean look of a calm witness comes from civilians who see the player without reaching Alerted. Civilians never flee (changed 2026-10-19: scared replaces panic flee, so no civilian leaves during a run). Film and confront stay out until a level needs them. The outcome decides the **evidence** the witness keeps, not only the alarm (concept 6.8's table).
- **The decision window is the `Deliberating` state:** a pending reaction, a timer, the indicator showing which way the civilian leans, and body language. It stays on `NpcDecisionWindow`; the tree picks the outcome, the window times it. Changed 2026-10-19: every civilian deliberates with the same clip (`React_Deliberating`, tense and undecided), so the **icon on the suspicion indicator shows the lean** (C6b), and the body shows the outcome once it happens (the phone at the ear, cowering with the face covered, the scared walk away).
- **The counter-action is the window itself** (decided 2026-10-19). The player's answer to a civilian deciding is to get out of sight before the window fills: it drains while they cannot see the player, and the orange phone, the HUD ring and `CIVILIAN ABOUT TO CALL` say when it matters (C6b). There is no intimidate or knock-the-phone verb; once a call is under way nothing stops it.
- **Panic is individual** (decided 2026-10-19). A scared civilian scares no one else and makes no sound a guard can hear: there is no contagion and no panic `NoiseEvent`. Each civilian reacts to what they saw themselves.
- **Animation.** Civilian locomotion, browse, register-work and the phone reach go through an animator builder like `NPCLocomotionBuilder`, never a hand-edited controller. The body language above is the animation list.

---

## 3. Loot

### 3.1 What is taken

| Loot                     | Carry  | Where                                       | Needs                          | Required | Notes                                                              |
| ------------------------ | ------ | ------------------------------------------- | ------------------------------ | -------- | ------------------------------------------------------------------ |
| Jewelry store plan       | Pocket | The office safe, always                     | Cracking the safe              | Story    | Unlocks the jewelry store (`ProgressUnlock.JewelryStore`); never moves |
| Money stacks             | Bag    | The office safe                             | Cracking the safe              | No       | 1–3 stacks, value rolled                                           |
| Loot display items       | Bag    | The glass `LootDisplay`s, as placed today   | The glass cutter (upgrade)     | No       | Vintage watch, record player and similar; silent to open           |
| Smash table items        | Bag    | Glass-topped tables like `DisplayTable`     | The hammer, or the glass breaker | No     | Smashed open: loud with the hammer, quieter with the glass breaker |
| Cash                     | Pocket | Scattered                                   | Nothing                        | No       | As in the apartment; takes no bag slot                             |

The register till is removed (2026-10-21), so the register is only where the clerk works.

**What is required** (changed 2026-10-21). Nothing is a hard requirement, as in Payday: the van unlocks once the player has taken the plan **or** any bag item, so a run can end without the story item. The plan is what unlocks the next target: without it the run still pays and still counts as a completed run for adaptive security, but the jewelry store stays locked. Only the apartment keeps hard required items (the tutorial). The plan is a pocket item, so it never competes with bonus loot for the two bag slots. `ProgressUnlock.JewelryStore` is added together with the plan (L2).

### 3.2 The randomised roll

- **`LootSlot`** (Core): a hand-placed `LootDisplay` or smash table (or other authored spot) that already holds its loot pickup. The developers choose the item and the place; the roll only decides which slots are live this run. A slot that is not live is switched off with its pickup.
- **`LootSpawner`** (Core, one per level, on the `Job` object; the roll does not go on `JobLayout`, which only numbers rooms and entrances): a `randomized` toggle.
  - **Off:** every slot stays as authored, so a level can ship with fixed loot.
  - **On:** at load, picks `count` slots at random and switches the rest off. `count` is rolled from a range, base 3 and up to 4 (`minCount`, `maxCount` fields), so the value per run stays fair.
  - A fresh roll every run, from an unseeded `System.Random`; a debug seed field in the inspector repeats a roll while testing.
- **Deferred:** weighted item pools per slot and spawning pickups from `LootItem`s. Not built until a level needs a slot whose item varies; hand-placed displays cover this level.
- The plan is a placed `LootPickup` inside the safe, not a slot. The money stacks are the safe's own roll (G3), not slots.
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

### 3.4 Glass tools

Decided 2026-10-21. Two kinds of glass case, and three tools in two lines. Each case is opened by one line only:

| Tool          | Owned                    | Opens                         | Noise                             |
| ------------- | ------------------------ | ----------------------------- | --------------------------------- |
| Hammer        | From the start (base)    | Smash tables only             | Loud: a smash every NPC in range hears, alarming |
| Glass breaker | Bought; replaces the hammer | Smash tables only          | Quieter: a shorter hearing range  |
| Glass cutter  | Bought                   | `LootDisplay`s only           | Silent (the glass-cutting minigame, as built) |

- **`LootDisplay`s stay exactly as they are,** with the glass-cutting minigame and the item that becomes reachable once the circle is cut. What changes is the gate: without the glass cutter the display's prompt is greyed ("Needs glass cutter") and the minigame never starts.
- **Smash tables** are a new case, designed like `DisplayTable`: a glass top over one bag item. Hold E to smash (short), which breaks the glass, emits a `NoiseEvent` at the table (loudness from the tool), and makes the item reachable. The hammer can never open a `LootDisplay`, and the glass cutter never opens a smash table.
- **Seen smashing** is `Stealing` conduct: a civilian or guard who sees it reacts as to any theft. The noise reaches guards who cannot see it, so a smash is a trade: fast, but it brings someone.
- **Ownership.** Nothing connects the shop to the player yet (`ShopConcept` and `ShopItem` are a main-menu concept with no save). Until the shop saves what was bought, a placeholder on the player, `PlayerTools` (like `PlayerSkills`), holds the smash tool (hammer or glass breaker) and whether the glass cutter is owned, set in the inspector. When the shop saves, only `PlayerTools`' source changes.

### 3.5 The story thread

Decided 2026-10-21. Each job steals the story item that opens the next one, so the levels form one chain. Every job after the apartment can be finished without its story item; only the next target stays locked until it is stolen on a completed run.

| Job           | Story item                                                                                     | Unlocks           |
| ------------- | ---------------------------------------------------------------------------------------------- | ----------------- |
| Apartment     | The clerk's cellphone (required, with the laptop, which unlocks the shop)                      | The thrift shop   |
| Thrift shop   | The jewelry store plan: the owner's rough sketch of the store, arrows to the exits, in the office safe | The jewelry store |
| Jewelry store | A note with the number of a safe deposit box at the bank                                       | The bank          |

The apartment's tenant is the tired clerk who works the thrift shop's register, so the phone leads the player to the shop. The plan shows the thrift shop's owner is about to rob the jewelry store himself; the player gets there first.

**In the main menu** (decided 2026-10-21):

- **The board.** Every story **note** stolen on a completed run (the jewelry store plan, the deposit box note) hangs on the main board, below the Settings and Quit buttons, and stays there. The phone does not hang on the board.
- **The objective position (OP)** is the spot on the map table where the phone sits today. When the player selects a target on the map, the OP shows the story item that led to it, a copy that fades in and gives a quick shake to draw the eye:

  | Selected      | At the OP                                     |
  | ------------- | --------------------------------------------- |
  | Apartment     | Nothing (the phone is not shown either)       |
  | Thrift shop   | The phone, once the apartment is completed    |
  | Jewelry store | The jewelry store plan, once it is stolen     |
  | Bank          | The deposit box note, once it is stolen       |

  Each target always shows its own item, also when it is replayed later. A locked target shows nothing, since its item has not been stolen. Deselecting hides the copy; the board's note never moves.

---

## 4. The job log

The record of what the player did, kept whether or not anyone saw it (decision 23). It is the only input to adaptive security.

### 4.1 Rooms and entrances

- **Rooms are the Markov states.** Each room in 1.1 is one `Zone` volume (several boxes on one object for odd shapes). The street is room 0, the implicit room when the player is inside no zone. Zones of one level must not overlap. This replaces the single `StaffArea` zone left over from the apartment: each room's zone carries its own type.
- **`JobLayout`** (Core, on the `Job` object): the level's rooms as a list of `Zone`s and its entrances as a list of `Entrance`s, in index order. A zone's room number is its place in the list plus one, an entrance's index its place in the list, handed out in `Awake` before the first physics step; the object names are the display names. It is the one place that numbers rooms and entrances, so nothing can disagree. `OnValidate` warns on empty or duplicate slots; `Start` (and a "Check Scene" context menu that works in edit mode) warns about any active zone or entrance in the scene that is not listed, and any entrance opening into an unlisted room. In a level without a `JobLayout` (the apartment) every zone reads as the street, harmlessly.
- **`PlayerRoom`** (Player): the room the player is in, with hysteresis. The raw room is the newest zone the player is still inside, else the street; it becomes the current room only after the player has stayed in it for 0.5 s, so brushing a boundary (the register counter, the stockroom's open front) logs nothing, and A → B → C taken quickly is recorded as A → C. Reports each settled change to `JobLog`. Assumes the player starts on the street.
- **`Entrance`** (Core): a trigger straddling one doorway to the street, with the `Zone` it opens into. A crossing counts as **inbound** when the player enters the trigger from the street side and leaves it on the room side (the entrance's local +Z points into the room). Every inbound crossing counts, so round trips through the same door reinforce it. Indoor doors are never entrances: moving between rooms is a room transition. `Door.prefab` carries an inactive `Entrance` child, switched on for exterior doors (the door's forward faces the street, so the child is turned 180°); the front doorways have their own.
- **`SecurityLayout`** (NPC, one per level, built with S4): the guard spawn candidates and the adaptive POI candidates, read alongside `JobLayout`.

### 4.2 What is recorded

`JobLog` (Core, static) counts, for the current run. Its lifetime follows the level's `JobLayout`, not scene loads: `JobLayout.Awake` calls `Begin(this, rooms, entrances)`, which sizes and clears the counts, and its `OnDestroy` calls `End(this)`. Only the layout that began the log may end it, because on an async scene load (restarts use `LoadSceneAsync`) the old layout can be destroyed after the new one has begun. A level without a layout records nothing. A `sceneLoaded` reset is deliberately not used: it fires after the new scene's `Awake`s and would wipe the fresh log.

- **Room transitions:** `transitions[from × rooms + to]`, incremented whenever the player's room changes. A flat array, because `JsonUtility` serializes neither 2D arrays nor dictionaries.
- **Entrance crossings:** `entries[entrance]`, every inbound crossing.

`PlayerRoom` calls `JobLog.RecordRoomChange(from, to)` when a room change settles; each inbound `Entrance` calls `JobLog.RecordEntry(index)`. Out-of-range indices (an unlisted entrance is −1) are ignored. `JobLogReadout` (Core, on the `Job` object) shows every transition and entrance used so far by name; its text is rebuilt only when `JobLog.Recorded` fires, never per frame. Loot order, tools and detections (concept 6.4) are not logged yet: nothing in this level uses them.

### 4.3 What is saved

`actions.json`, a file of its own beside `progress.json` and `police.json`, so a broken file never costs the player's progress. One `ActionRecord` per location, keyed by scene name:

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
- **Who writes it:** `ActionRecorder` (Core, on the `Job` object) on `JobOutcome.Completed`: load the save, find the record for the scene name, replace it with a warning if it no longer fits the layout, create it if missing, then `AddRun(decay)` and save. `decay` is a field on the recorder, 0.8.
- **`ActionRecord`, `ActionSave` and `ActionStore` live in Core,** plain data with no NPC types, so the main menu can read them. `ActionRecord.For(location, layout)` builds an empty record with the layout's names; `Fits(layout)` compares names in order; `AddRun` decays, adds the run from `JobLog`, and counts the run. `ActionStore` follows `ProgressStore`: a missing or broken file loads as an empty save, a failed write warns instead of throwing. The model that reads them lives in NPC.
- Tools/Suspect Unknown/Action Record logs the file as JSON, reveals the save folder, or deletes it. Faking completed runs through a chosen entrance comes in S2b.

---

## 5. Adaptive security

Concept 6.4, built for one location. Two stages, two kinds of problem: **predict** where the player will be, then **place** guard coverage there. It runs once, when the level loads, before the first frame of the heist. It never learns during a run (decision 24).

### 5.1 Stage A: prediction

From the `ActionRecord`:

- **Room transitions, a first-order Markov chain over room changes.** `P[i][j] = (C[i][j] + α) / Σ_{k≠i} (C[i][k] + α)` for `j ≠ i`, and `P[i][i] = 0`, with Laplace smoothing α = 0.5 so unseen transitions keep a small chance. The diagonal stays 0 because the job log never records staying in a room, so smoothing it would invent probability no run could support. The decay of 0.8 per job is already in the counts, so recent runs dominate.
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
- **How many:** set per level, as data. `SecurityLayout` holds a list of `SecurityTier`s (`fromCompletedRuns`, `guards`, `adaptivePois`); the tier with the highest `fromCompletedRuns` not above the player's completed runs applies (`SecurityTier.For`). The director looks the tier up and hands it to the planner, which stays pure. `SecurityLayout.OnValidate` warns when a level breaks the rules: no tier at 0, tiers out of order, fewer guards than the tier before (the guard count never shrinks), a tier with no guards, more guards than spawns, or more adaptive POIs than candidates. The thrift shop's tiers:

  | Completed runs | Guards | Adaptive POIs      |
  | -------------- | ------ | ------------------ |
  | 0              | 1      | 0 (base POIs only) |
  | 1              | 1      | 1                  |
  | 2              | 2      | 2                  |
  | 3 or more      | 2      | 3                  |

  Another level defines its own tiers and its own spawn count: nothing in the planner or the director is specific to the thrift shop.

- **Drain.** Each chosen POI's `DecayMultiplier` is `1 + gain / bestGain` (between 1 and 2), so the strongest pick drains twice as fast as its base rate. "The side entrance now drains twice as fast" is one number.
- **Solvability by authoring.** No candidate covers the register, and none covers more than one entrance. With at most three picks out of four entrances, at least one entrance always stays without an adaptive POI. A tier that picks as many POIs as there are entrances would break this, so keep the largest `adaptivePois` below the entrance count.

### 5.3 Applying the plan

`SecurityDirector` (NPC, one per level) runs in `Awake`:

1. Loads the record, looks up the level's tier for its completed runs, and builds the plan (stages A and B) with that tier.
2. Spawns the guards, each at a random free spawn from `SecurityLayout`'s spawns (a spawn is used once; `System.Random` with a debug seed field). Where a guard starts carries no plan information: guards pick POIs by importance, so the plan's effect does not depend on the spawn. Keep spawns away from the street entrances by authoring. Spawning in `Awake` means the guards' own `Awake` runs at once and their `Start` runs with everyone else's, so `NpcPerception` still finds the player.
3. Activates the chosen candidate POIs and sets their `DecayMultiplier`.
4. Writes the explanation: one line per pick from its marginal-gain step, phrased in POI terms, for example "Guards now check Side door: you come in through Emergency exit on 64 % of runs, drains 2.0× faster." The record holds decayed sums, not a run history, so the line quotes the model's probabilities rather than a count of runs. The lines go to the debug overlay and the log; the AI report quotes them.

### 5.4 Visibility

- **To the player:** nothing in the level itself. POIs are never shown in game. In the main menu, the thrift shop's security line on the map board reads **"Raised security"** in red once the level has more guards than a first run (the second guard, after two completed runs). `MapBoard` loads `actions.json` once in `Awake`; `MapLocation.SecurityLine` asks the location's `SecurityBudget` (`IsRaised(completedRuns)`), the same asset the level's `SecurityLayout` reads, so the menu and the level share one set of tiers. A location without a budget (the apartment) shows its typed text. The menu cannot compare the saved room names with the layout, so after a layout change it reads "Raised security" until the next completed run replaces the record.
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

**T2b. Guards pass through doors** (decision 19). Done and checked 2026-10-19: NavMesh area `Door` (index 3, cost 2), unticked on the civilian and resident agents; an inactive `GuardLink` child with a `NavMeshLink` on `Door.prefab`, active on the `StaffRoom Door` only; `Door.OpenForPassage`, `SwingSeconds` and `InReach` (reach bounds cached in `Awake`, since an open door's triggers are off); `GuardBehaviour.CrossLink` and `CloseBehindWhenClear`; `NpcAnimator` takes the speed from the body's movement while on a link; `NpcHearing` leaves out the `Door` area. Keep a link narrow and short (a crossing walks a straight line that ignores the NavMesh): a wide link lets the body clip the frame, long end points cut through what lies beyond the door. A `NavMeshLink` across the `StaffRoom Door` on a new NavMesh area `Door`; civilian agents and hearing paths exclude that area. `Door.OpenForPassage()` opens without an anomaly and leaves the lock as it was. The guard crosses links itself (`autoTraverseOffMeshLink` off): stop, open, walk across, complete the link, and close the door once outside the far reach's bounds. A chasing guard does not stop to close it.
_Check:_ the guard reaches the office POI through the closed door and the door is shut again behind them, with no anomaly raised. The clerk never paths through it. A sprint behind the closed door is not heard in the office. Chased, the player gains nothing by closing themselves in.

**T3. Dusk lighting.** Done 2026-10-21. Sun, interior and street lights, `GameplayLight` on the lamps that count, light probes, within the 32-shadow budget.
_Check:_ the player's light readout is clearly lower in the shop back than at the front; no shadows flicker as the camera moves.

### Track G — the guard

**G1. A guard in the level.** Done 2026-10-21 (the hand-placed guard was later replaced by S5's spawns). The `SecurityGuard` prefab placed by hand, three or four base POIs (the office important), catch failing the job.
_Check:_ the guard patrols, visits the office most, catches the player standing in the stockroom, and the caught newspaper plays.

**G2. Slamming doors** (M5 step 2.4). Done and checked 2026-10-21. The guard claims an open door's anomaly, walks over, plays the confused beat, and calls `Resolve()`; the door slams. A pursuing guard ignores open doors and runs through.
_Check:_ pick the right side door and leave it open: the guard comes, looks confused and slams it. Two guards never go for the same door. Chased through an open door, the guard follows without slamming it.

**G3. The safe.** The minigame is built (2026-10-20): `Safe` (Core) with a lock level and a combination rolled per heist, `SafeInteraction`, `PlayerSafeCracking` (an `IMinigame`, on `Player.prefab`), `SafeModel`, `SafeView` and `SafeHud`, `SafeTuning` (`DefaultSafeTuning`), and `SafeMinigameView.prefab` with `SafeHud.prefab` in `Minigames.prefab`, on render layer 18 (`SafeCracking`). The dial has 20 numbers (cut from 40 the same day: 9° between numbers was too fine to aim for) printed by `SafeView`'s Rebuild Dial Face; the handle turns −90° about its local Y on opening (+90° turns it up into the dial). Done 2026-10-21: `AnomalyKind.SafeOpen` (guard weight 1, civilians 0); `Safe` raises it on opening and closes on `Resolved`, and a cracked safe reopens without the minigame; the guard closes it with the same errand as a door, playing `Guard_DoorSlam`; `NpcSuspicion.RaiseBaseline` keeps the guard who saw it on edge (2.3); `PoiSafeAlarm` on the office POI raises `PointOfInterest.AlarmFactor` to 1.5 on `Safe.Opened`, a factor apart from the plan's `DecayMultiplier`. Still to do:
- **Contents.** The jewelry store plan (always) and one to three money stacks (`LootCarry.Bag`) inside the safe, replacing the single `enableOnDone` field with an array. `Safe` rolls how many stacks are live in `Awake` and switches the rest off, makes the live ones reachable in `Open()` and unreachable again in `Close()`, so a closed safe hides what is still inside.
- **The safe in the level.** Place `Safe.prefab` in the office, check its `Anomaly` gizmos (grey sphere on the door, green one on the NavMesh in front), and add `PoiSafeAlarm` to the office POI with the safe assigned.
_Check:_ open the safe and hide: the guard comes, closes it, and from then on reaches Curious from a glance that would not have made them Curious before. The office POI's drain readout is 1.5 times its base rate.

### Track C — civilians

**C1. The shared decision window.** Done and checked 2026-10-19. Extract the window from `ResidentBehaviour` into `NpcDecisionWindow`; the apartment clerk uses it unchanged. `NpcWindowReporter` replaces `ResidentWindowReporter`.
_Check:_ the apartment plays exactly as before (B6 check).

**C2. Customers and the clerk at work.** Done and checked 2026-10-19. `CivilianSpawner` (groups of prefab, role, archetype, count range and points) spawns customers and the clerk at load. Customers browse with `CivilianBehaviour`. The clerk has the register, bin run and restocking spots, picked evenly (no weights; the register's long stay does the work). Every civilian opens the window when Alerted and calls the police when it fills.
_Check:_ the clerk spends most of the time at the register and leaves for each errand now and then. Taking the till in front of the clerk opens their window; staying in view fails the job with the police headline; ducking out of sight in time does not.

**C3. Witness slice check** (M6 step 2). Postponed 2026-10-19; nothing to build, only the check to run. Play `NpcWitness` on customers and the clerk: the readout shows exposure growing only while the civilian sees the player, calm or not.
_Check:_ walk past a calm customer in clear view for 10 s: their exposure rises and they stay Idle. Break line of sight and it stops rising. The police description after the job matches who saw you.

**C4. Door chores.** Dropped 2026-10-21: the clerk does not close doors. Every civilian ignores an open door; only guards deal with one (G2).

**C5. Civilian animation.** Done and checked 2026-10-19. An animator builder for civilians on `NPC.fbx`: locomotion, idle, browse, register work, and the phone reach and call (a masked layer, as the clerk's, faded in by the window state). As built: `CivilianClipImport` sets the clips' loop and root baking; `CivilianLocomotionBuilder` (Tools → NPC → Civilian → Rebuild Locomotion) builds `CivilianAnimator` with a Standing speed tree (`NPC_Idle`, `NPC_Walk` at 1.2 m/s), a Dwell tree over `Station` (one clip per `CivilianStation`, set on each `CivilianSpot`), and the Phone layer shared with the clerk through `AnimatorBuildUtility.AddPhoneLayer`; `CivilianAnimation` sets `Dwelling`, `Station`, `Deliberating` and `Calling` and fades the Phone layer. The `React_*` clips and `NPC_Run` are imported but unused until C6–C7.
_Check:_ a customer walks, browses a display and idles with no foot sliding; when the window fills they visibly reach for a phone, and the call is readable from animation alone.

**C6. `ReactionTree` and the first tree.** Done and checked 2026-10-19 (customer and clerk trees, below). `ReactionTree`, `ReactionNode` (branch and leaf subclasses) as ScriptableObjects, the inputs in 2.4, soft sampling at the leaves, and a debug readout of the inputs, scores and chosen outcome per civilian. One tree for customers with three outcomes: call, scared, walk away. The tree samples once, when the window opens, so the lean can be shown for the whole window; a call stays on `NpcDecisionWindow` (it owns the police timer), any other outcome is raised as `Decided` and carried out by `CivilianBehaviour`. The window is the `Deliberating` state. Scared blinds the civilian with `NpcPerception.SetViewScale(0, 0)` until they calm down. Animation through `CivilianLocomotionBuilder`: `CivilianBehaviour.Activity` (which also reports the window's Deliberating and Calling) drives an int `Activity` parameter, and the base layer enters `React_Deliberating` (played once and held), `React_Scared` (looping cower with the face covered, shaking) and `React_AvoidWalk` (looping scared walk on `NPC_Walk`'s legs) from any state; the Phone layer now fades in for the call only.
_Check:_ the same crime in view of the same customer from different distances produces different outcomes across runs, but never an outcome the readout scores near 0.

**The baseline trees** (set up 2026-10-19, in `NPC/Archetypes/Reactions/`; checked in C6, tuning left to playtesting). Severity is the archetype's abnormality for the conduct seen, `1 − Π(1 − w)` over `conductWeights`: for both archetypes sprinting is 0.75 and stealing or lockpicking 1.0; in the staff area is 0.6 for a customer and 0.8 for the clerk; sprinting in the staff area 0.9 and 0.95. Both see 10 m, so a window opens with the player 0 to 10 m away. Odd behaviour reaches Alerted too (about 3 s of a sprint in clear view at a rise rate of 0.6), so the root splits odd behaviour from crime.

`CivilianTree` on `Civilian` (customers): nothing to gain, so distance decides fear against calling.

| Severity | Distance | Leaf | Weights | Odds |
| --- | --- | --- | --- | --- |
| below 0.8 (odd) | below 4 m | `OddClose` | Scared 3, Avoiding 2 | 60 % scared, 40 % walk away |
| below 0.8 (odd) | 4 m or more | `OddFar` | Avoiding 3, Calling 0.5 | 86 % walk away, 14 % call |
| 0.8 or more (crime) | below 4 m | `CrimeClose` | Scared 3, Calling 1 | 75 % scared, 25 % call |
| 0.8 or more (crime) | 4 m or more | `CrimeFar` | Calling 4, Avoiding 1 | 80 % call, 20 % walk away |

`StoreClerk_Distance` on `StoreClerk` (the clerk's tree; the asset is the whole tree despite its name): staff, so theft means a call at almost any distance, and only being cornered scares them. Odd behaviour has no distance branch, since distance changes nothing there.

| Severity | Distance | Leaf | Weights | Odds |
| --- | --- | --- | --- | --- |
| below 0.78 (odd) | any | `OddBehaviour` | Avoiding 3, Calling 1 | 75 % walk away, 25 % call |
| 0.78 or more (crime) | below 3 m | `CrimeClose` | Scared 1, Calling 1 | 50 % scared, 50 % call |
| 0.78 or more (crime) | 3 m or more | `CrimeFar` | Calling 4, Scared 1 | 80 % call, 20 % scared |

**Where the thresholds fall** (settled 2026-10-19). A threshold must not equal a severity the archetype can produce, or float rounding decides the branch; both sit at least 0.02 from every value.

| Conduct | Customer (0.8) | Clerk (0.78) |
| --- | --- | --- |
| In the staff area | 0.6, odd | 0.8, crime: the back room is serious for staff |
| Sprinting | 0.75, odd | 0.75, odd |
| Sprinting in the staff area | 0.9, crime | 0.95, crime |
| Stealing or lockpicking | 1.0, crime | 1.0, crime |

A change to `conductWeights` on either archetype moves these severities, so recheck the thresholds with it.

**C6b. Reactions readable.** Added, done and checked 2026-10-19. The player reads what each civilian is doing, and what the shop as a whole is doing, without the debug readout. Five steps:

1. `CivilianBehaviour.ActivityChanged(from, to)`, raised once per change from `LateUpdate` (the window's states have no single setter), silent for the role assignment at spawn. Done and checked 2026-10-19. Steps 3, 3b and 4 (the call-only ring, ALARM for a chase or a call, `JobReaction` and `JobAlert.Reacted`) are done and checked too.
2. **Icons on the suspicion indicator.** For civilians, `NpcSuspicionIndicator` shows the activity instead of "?", "?!", "!": while deliberating, the pending outcome's icon (the lean) with the window's progress as the fill; then a phone (calling), a cower (scared) or an arrow away (walking away). The screen-edge copy shows the same icon. Guards and the resident keep the alert-state symbols. Icons come from a TMP sprite asset, since the HUD font has no such glyphs. Done and checked 2026-10-19: `ReactionIcons.png` (Phone with signal arcs, Scared, Avoid; flat white like the HUD set) and `ReactionIcons.asset` in `NPC/Art/Textures/UI`, sprite pivots Bottom Left so the glyphs sit on the baseline, on both `SuspicionIndicator.prefab` and `OffscreenIndicator.prefab`.
2c. **The resident and the call read as danger** (added 2026-10-19, from playtesting the apartment). Two fixes to step 2:
   - The resident showed only the alert symbols, since it has no `CivilianBehaviour`, yet it is the civilian whose call fails the job. A small NPC interface, `ICivilianActivity` (`Activity` and `ActivityChanged`), is implemented by `CivilianBehaviour` and `ResidentBehaviour` (which gains the same `LateUpdate` compare); the indicator and `NpcReactionReporter` find it with `GetComponentInParent`. Only guards keep the alert symbols. `NpcReactionReporter` also goes on `ApartmentClerk.prefab`.
   - The call reads as "too late": once it is under way nothing stops it (1.5 s), so the phone **pulses** red with a full fill, as the HUD ring does. Every lean stays orange, the call's included, because hiding during the window still drains it; the warning that a call is coming is the orange phone together with the HUD ring (step 3).

   _Check:_ in the apartment, the resident's indicator shows the orange phone while deciding, with the HUD ring filling, and the pulsing red phone during the call, on screen and at the screen edge, and the call announces `JobAlert: Calling`; in the thrift shop, every lean is orange and only the call is red.
2d. **Every symbol is an icon** (added 2026-10-19). The alert symbols "?", "?!" and "!" are replaced by icons from the same sprite asset: an eye (the HUD's `IconEye`) when curious, for every NPC; for guards a flashlight when investigating and handcuffs when alerted; for civilians and the resident a staring eye (an eye with alarm rays) from investigating up. A civilian who is Alerted outside its window, which only happens after a window drains, shows the stare in orange, not a red symbol, which settles the known issue of a drained window reading as an alarm. `ReactionIcons.png` holds all seven icons in one row: Phone, Scared, Avoid, Eye, Stare, Flashlight, Handcuffs.
3. **The ring shows a window leaning toward a call** (settled 2026-10-19, after trying a call-only ring). `NpcWindowReporter` reports a window while its pending outcome is a call, and the call under way. The ring appears the moment the orange phone does and fills with the window's progress, orange; once the call is under way it sits full, red and pulsing. A window leaning toward scared or walking away never shows the ring. The resident has no tree, so every window of theirs leans toward a call and shows the ring. A call-only ring that filled over the call's 1.5 s was tried and dropped: it appeared only when it was already too late to act on.
3b. **ALARM means a chase or a call** (added 2026-10-19). The job HUD's status tag reads ALARM only while a guard is pursuing the player or a civilian is calling the police; suspicion alone caps at SEARCHING, so a scared or walking-away civilian who is still Alerted no longer reads as ALARM. `NpcAlertReporter` reads the optional `GuardBehaviour` (`ActivityChanged`, Pursuing) and `NpcDecisionWindow` (a new `CallStarted` event). `JobAlert.Peak`, and so the debrief, follows the same rule. In the apartment the resident now shows SEARCHING while deciding and ALARM once calling.
4. **Reactions reach Core.** `JobReaction` (Core enum: Scared, CalmedDown, Avoiding, Calling, mirroring the outcomes as `JobAlertLevel` mirrors `NpcAlertState`) and a `JobAlert.Reacted` event, fed by an NPC-side reporter that maps `ActivityChanged`. Core never sees an NPC type.
5. **`ReactionToast`** (Core, on `JobHud.prefab` under the status tag, in the style of `PickupToast`'s "+ CASH"): `CIVILIAN SCARED`, `CIVILIAN CALMED DOWN`, `CIVILIAN WALKING AWAY`, and `POLICE CALL IN PROGRESS` in red. Added after playtesting (2026-10-19): `CIVILIAN ABOUT TO CALL` in orange the moment a window leaning toward a call opens, while getting out of sight still works, and `CIVILIAN LOST SIGHT` when such a window drains without a call (`JobReaction.AboutToCall` and `LostSight`, detected by `NpcReactionReporter` from `ActivityChanged` and the window's `PendingOutcome`). A lean toward scared or walking away gets no toast; the icon covers it. Guards announce their steps too (added 2026-10-19), through `GuardReactionReporter` on `Guard.prefab` reading `GuardBehaviour.ActivityChanged`: `GUARD CURIOUS` (Watching), `GUARD INVESTIGATING` (orange), `GUARD CHASING YOU` (red), `GUARD LOST YOU` (Pursuing to Investigating) and `GUARD GAVE UP` (back to Patrolling after investigating or a chase). A glance that ends without investigating announces nothing, so an ordinary patrol does not fill the toast. Only the police call stays up; a chase line fades like the rest, since the status tag already reads ALARM. Same-kind events within the hold merge into a count (`2 CIVILIANS SCARED`); another kind replaces the line; the police call overrides everything. The toast is a copy of `PickupToast`'s fade, not a shared toast framework; extract one only when a third toast appears.

_Check:_ each civilian state shows its icon on screen and at the screen edge, and guards are unchanged; a window leaning toward a call shows the ring from its first moment and fills it, a window leaning elsewhere never does, and a call turns it full, red and pulsing; a scared civilian leaves the status tag at SEARCHING, a guard chase or a call turns it to ALARM, and losing the chase brings it back to SEARCHING; each reaction shows its toast, a window leaning toward a call shows `CIVILIAN ABOUT TO CALL` and, if the player hides in time, `CIVILIAN LOST SIGHT`, two scares within the hold read `2 CIVILIANS SCARED`, the police call overrides, and the pickup toast still works beside it.

**C7. Outcomes and the counter-action.** Settled 2026-10-19, nothing to build. The counter-action is getting out of sight while the window drains (2.4), which C1 and C6b already provide; pretend not to see is not built. A scared civilian already gathers no evidence (C6).

**C8. Panic contagion.** Dropped 2026-10-19: panic is individual, with no spreading and no noise (2.4). The `PanicContagion` stub goes.

**C9. Roles and personality.** Dropped 2026-10-21. The clerk's own reaction tree is set up (2026-10-19, `StoreClerk_Distance`, values under C6); customer personalities telegraphed by idles will not be built. Role (clerk against customer) is what tells the player who is dangerous.

### Track L — loot

**L1. `LootSlot` and `LootSpawner`.** Hand-placed loot displays as slots; `LootSpawner` with a `randomized` toggle, a count range (3–4), a fresh roll per run and a debug seed. Randomization off leaves every slot live.
_Check:_ with the toggle off every display holds its item. With it on, three restarts give three different sets of live displays, each with 3 or 4 items; the same debug seed gives the same set.

**L2. The plan and the way out.** Changed 2026-10-21: the till is removed and nothing is hard-required. The jewelry store plan (`LootItem`, pocket, `ProgressUnlock.JewelryStore`) in the safe. `JobObjectives` stops listing the apartment's laptop and cellphone, and extraction unlocks once the plan **or** any bag item is taken (the apartment keeps its required items). Remove the till from the scene.
_Check:_ the van zone stays grey until the plan or a bag item is taken. A completed run with the plan unlocks the jewelry store on the map; a completed run without it pays but leaves the jewelry store locked.

**L3. `LootBag`.** Two slots, the "Bag full" prompt, the HUD's bag list and icon driven by slots used.
_Check:_ with two items in the bag a third shows "Bag full" and cannot be taken; cash still can.

**L4. Stashing at the van.** The side-door stash point, the G prompt, `PlayerVanStash`, and the `Stash` input action.
_Check:_ fill the bag, walk to the van's side door, press G: the bag empties, the HUD shows two empty slots, and the extraction countdown never started. On completion, the debrief lists every item, stashed or carried.

**L5. Tool gates** (3.4). `PlayerTools` (Player, placeholder): the smash tool (hammer or glass breaker) and whether the glass cutter is owned. `GlassCutterInteraction` blocks the `LootDisplay` (greyed prompt, "Needs glass cutter") without the cutter.
_Check:_ with the cutter unticked, every `LootDisplay` shows a greyed prompt and cannot be started; ticked, they work as today.

**L6. Smash tables** (3.4). A `DisplayTable`-style prefab with a glass top over one bag item, a short hold to smash, the glass breaking, a `NoiseEvent` whose loudness comes from the smash tool, and `Stealing` conduct while smashing. Smash tables can be `LootSlot`s.
_Check:_ smash a table with the hammer out of sight: a guard in hearing range comes to investigate. With the glass breaker, the same smash is heard from a shorter distance. The item inside is taken like any loot.

**L7. Story items in the main menu** (3.5). The stolen notes hang on the main board below Settings and Quit, shown from the save like `ShowWhenUnlocked`. Each `MapLocation` names the story item that led to it; selecting it fades a copy in at the OP with a quick shake, and deselecting hides it. The apartment has none.
_Check:_ after a completed apartment run, selecting the thrift shop fades the phone in at the OP and shakes it; selecting the apartment shows nothing. After a completed thrift shop run with the plan, the plan hangs on the board, and selecting the jewelry store shows a copy at the OP while the board's stays put. A thrift shop run without the plan adds nothing to the board.

### Track S — adaptive security

**S1. `JobLog`.** Done and checked 2026-10-17. Room transitions and inbound crossings for the current run, begun and ended by `JobLayout` (4.2), with `JobLogReadout`.
_Check:_ walk street → front right → shop front → shop middle → shop back → stockroom: the readout shows those transitions and one crossing. A restart empties it and keeps it active. The apartment records nothing and shows no errors.

**S2. `ActionRecord` and `ActionStore`.** Done and checked 2026-10-17. `ActionRecord`, `ActionSave`, `ActionStore`, `ActionRecorder` on `Job`, and the Action Record menu (Log, Reveal, Delete).
_Check:_

1. Complete a run, then Log Record: Thriftshop with `completedRuns` 1, the room and entrance names, and counts equal to the readout.
2. Get caught: `actions.json` is unchanged (its timestamp too).
3. Complete again the same way: every old count is 0.8 times what it was plus the new run (Street → Shop front goes from 1 to 1.8), `completedRuns` 2.
4. Swap two zones in `JobLayout.rooms` and complete: a "layout changed" warning, and the record restarts at `completedRuns` 1. Swap them back.
5. Delete Record, then complete: a fresh file.

**S2b. Fake Runs.** Done and checked 2026-10-17 (`ActionFakeRunsWindow`, Tools → Action Record → Fake Runs Window; it saves through `ActionRecorder.RecordRun`, the same path as a real run). A small editor window under Action Record: choose an entrance of the open scene's `JobLayout` and a number of runs, and add that many completed runs through it (an entry at the entrance and a transition from the street into the room it opens into, decayed as real runs are). Needed for the S5 check; S3 and S4 tests build their records in code.
_Check:_ fake two runs through the emergency exit: Log Record shows `completedRuns` 2, the emergency exit entry at 1.8, and Street → Emergency exit at 1.8.

**S3. Stage A** (`ActionModel`, NPC, pure logic: the model of the player's actions). Done 2026-10-18; seven tests in `Assets/Testing/Editor/ActionModelTests.cs` pass. Transition probabilities with Laplace smoothing, the entrance Dirichlet, forward propagation. EditMode tests: rows sum to 1; unseen transitions are above 0 and the diagonal is 0; a record of three runs through one entrance gives that entrance the highest probability; occupancy sums to 1 and leaves the street out.
_Check:_ the tests pass.

**S4. Stage B** (`SecurityPlanner`, NPC, pure logic). Done 2026-10-18: `SecurityPlanner` with `PoiCandidate`, `SecurityPick` and `SecurityPlan`; `SecurityPlannerTests` pass. Greedy marginal gain over candidates, deterministic ties, the drain multipliers, and the explanation lines. The budget is no longer in the planner (changed 2026-10-18): `Plan` takes a `SecurityTier`. EditMode tests: zero completed runs pick nothing; one run picks the candidate at the most used entrance; two runs pick two different candidates; the same record always gives the same plan; a tier sets the guard count and number of picks, a tier asking for more POIs than candidates picks them all, a negative count picks none.
_Check:_ the tests pass.

**S4b. `SecurityTier` and the per-level budget.** Done 2026-10-18 (the tiers moved into the shared `SecurityBudget` asset by S7). `SecurityTier` (Core, `[Serializable]` struct) with the static lookup `For`; `SecurityLayout` reads its budget asset through `TierFor` and validates guards against spawns and POIs against candidates. `Tier_FollowsCompletedRuns` covers the thrift shop's table. The scene's tiers are 0 → 1 guard, 0 POIs; 1 → 1, 1; 2 → 2, 2; 3 → 2, 3.
_Check:_ the Console shows no `SecurityLayout` warnings with those tiers; each rule warns when broken on purpose.

**S5. `SecurityDirector`.** Done and checked 2026-10-18. Builds the plan at load with the level's tier, spawns the guards at random free spawns, activates the chosen POIs and sets their multipliers. Remove the hand-placed guard from G1.
_Check:_ with Tools → Fake Runs (two runs through the right side door): two guards spawn at random spawns, and one of them visibly spends its time at the right side door.

**S6. The debug overlay.** Done and checked 2026-10-18. `SecurityDebugOverlay` (NPC, on the `Security` object, IMGUI) toggled by F3 (`SecurityDebug` action, `PlayerInputs.SecurityDebugPressed`); hides the job HUD while open. `SecurityDirector` exposes `Record`, `Model`, `Candidates` and `Plan`; `SecurityPlan` carries `StandaloneGains` so every candidate's gain shows, not only the picks'. It shows the entrance bars, occupancy per room with its heatmap index (street = 0), every candidate with its standalone gain and its pick mark, the explanation lines, the room-transition heatmap, and in the world a label above each picked POI with its multiplier, rooms and entrance. The text, heatmap and marker data are built once, on first open, since the plan never changes during a run.
_Check:_ the overlay's matrix, entrance bars and chosen POIs match the record and the plan in the log.

**S7. "Raised security" in the main menu.** Done and checked 2026-10-18. `SecurityBudget` (Core `ScriptableObject`, asset `ThriftshopSecurityBudget`) holds the tiers, `TierFor` and `IsRaised`, and validates its own data; `SecurityLayout` and the thrift shop's `MapLocation` both reference it. `SecurityBudgetTests` pass.
_Check:_ after two completed runs the thrift shop's security line reads "Raised security" in red; with a deleted `actions.json` it reads as before.

### Track R — recognition

**R1. The match** (NPC, pure logic). `match` from a `WitnessReport` and a `CharacterAppearance`, with EditMode tests: the same outfit as a confident description gives a high match; a different chest color drops that slot to 0; a description under `vagueAt` gives 0.
_Check:_ the tests pass.

**R2. Recognition in suspicion.** The archetype's `recognition` weight (guard about 0.6, civilians 0, set in every asset), the description loaded at job start, `match × recognition` combined into abnormality, capped at Investigating on its own. A readout line shows the current match.
_Check:_ finish the apartment after a good look, then start the thrift shop in the same clothes: a guard who sees the player walking normally on the shop floor grows suspicious and comes over. Change clothes in the menu and start again: the same walk raises nothing.

### Together

**X1. Teaching pass.** Make the clerk's errands the opening on the way to the office; put the best bonus loot in the stockroom and office so the van trip is worth it; check the first guard can be read from the cone alone.
_Check:_ a player who finished the apartment finishes the thrift shop in two or three tries, and at least one in three playtesters makes a van trip without being told.

**X2. Adaptation pass.** Play three completed runs through the same side door.
_Check:_ on the third run the player notices without being told that the guards watch "their" door, and can say why. This is the `roadmap.md` Phase 8 done-when in miniature.

**X3. Docs.** Update the status columns in `roadmap_npc.md`, tick the Phase 8 items this level covers in `roadmap.md`, and add changelog lines.

---

## 8. Open questions

- **Clerk errands.** Are 20–35 s at the register, with bin runs and restocking in between, enough openings? Playtest.
- **The bonus count.** Is 3–4 live displays plus 1–3 money stacks enough to make a second van trip tempting but not required?
- **Recognition strength.** Is 0.6 for guards right, and is "never past Investigating on its own" too kind? A burned outfit should feel costly without making the level unwinnable.
- **Smoothing and propagation.** α = 0.5 and T = 6 are starting values. With only a few runs of data, does the first adaptive POI land where the player expects it to?
- **Two guards and the open safe.** The raised baseline is per guard. Should the guard who finds the safe open put the other guard on edge too (the first use of `Reporting`)?
- **How long a civilian stays scared.** The minimum and maximum seconds; long enough to read and to use, short enough that scaring a customer is not a free pass. Playtest.
- **Smash noise.** How loud the hammer and the glass breaker are, and whether a smashed table stays behind as an anomaly a guard notices later. Playtest.
- **Telling the story in the debrief.** How the newspaper and the debrief mention the stolen plan, so the player knows what it unlocked (3.5).
