# Suspect Unknown — NPC & AI Roadmap

Companion to `roadmap.md` and `heist-game-concept.md`. Those cover the whole game; this one covers the NPC slice only: guards, civilians, clerks, and cameras, and everything they perceive, decide, and remember. It records the architecture decisions made so far, why they were made, and the order to build in.

Covers the systems side of the split in `roadmap.md`. Integration seams with the player, world, and art side have their own section.

> Written 2026-09-21 after the base scaffolding commit (`8f8bd04`), and revised the same day after cross-checking it against `roadmap.md` and the concept document. Revised again on 2026-09-23 after M1's perception core was built and the per-archetype region weights design replaced fixed cast points, and kept current through M3 (2026-09-29), the M4 light rework, and the M5 redesign around doors, anomalies, and abilities (both 2026-10-05). See the changelog at the end. Update the status column in the file tables as files get implemented.

---

## The end goal

Every observer in a location runs on the same perception pipeline. They differ only in their data and in what they do with what they see:

- A **guard** patrols, notices, investigates a last-known position, pursues, and reads behaviour that civilians miss.
- A **civilian** roams, notices, visibly deliberates, then calls, gets scared or walks away — and the player can escape the deliberation by getting out of sight before it ends.
- A **clerk** is a civilian with worse eyes and worse memory.
- A **camera** is an observer without a body: perfect retention, visual features only, fixed arc. It raises no alarm itself; a guard watching its feed does.
- Every one of them produces **witness evidence** that feeds the police profile.

Being seen is not failure. NPCs grow suspicious of what the player is **doing**, not of the player existing, so walking normally through a public area stays safe.

The player should be able to look at a room and predict who will see them, how well, and what each NPC will do about it. The NPC system is judged by that, not by how clever it is internally.

The NPC system is finished when its share of these `roadmap.md` criteria passes. Of these phases, only Phase 4 is a hard **GATE** in `roadmap.md`; the rest are done-when criteria or gate checks.

| Phase | Done when (NPC share)                                                                                                                                                                                                    |
| ----- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 2     | Crouching behind a half-height occluder with a gap underneath (a table top, not a solid counter) breaks the ray to the head while the ray to the feet still lands, and the partial detection shows in the debug readout. |
| 3     | You can get caught, and there is tension before you are. The Curious state lasts long enough to react to.                                                                                                                |
| 4     | **GATE.** Every verb (opening a door, the breaker, one device ability) visibly moves an NPC or changes what it perceives, and the vision cones are readable without the debug view.                                      |
| 5     | A player can look at a room and correctly guess which civilian is the dangerous one.                                                                                                                                     |
| 6     | A player can predict who saw them well and who did not, purely from how the encounter played out.                                                                                                                        |
| 10    | Guards read behaviour. Cameras see only within their arc and from their mounted height.                                                                                                                                  |

---

## Architecture decisions

### 1. Data in ScriptableObjects, behaviour in components

Guards, civilians, and clerks do not differ only in detection rate. They differ on two separate axes:

| Axis                                                                                                                                                        | Guard vs civilian       | Tool                              |
| ----------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------- | --------------------------------- |
| **Numbers** — view distance, FOV, observer skill, suspicion rise and decay, conduct weights, memory retention, feature-visibility mask, hearing sensitivity | Same code, other values | `NpcArchetype` ScriptableObject   |
| **Reaction** — patrol, investigate, pursue vs a decision tree                                                                                               | Different code entirely | A separate MonoBehaviour per role |

**Rule:** if code ever branches on which archetype it is (`if (archetype.isGuard)`), that branch belongs in a component, not in the ScriptableObject.

**Rule:** never write runtime state into a ScriptableObject. The asset is shared by every NPC using it and keeps its values between play sessions in the editor. Current suspicion, last-known position, and timers live on the MonoBehaviour.

### 2. Composition over inheritance

Every NPC prefab is a set of shared perception components plus exactly one behaviour component. There is no `Npc` base class with `Guard` and `Civilian` subclasses.

The proof that this works: **a camera is perception plus witness, with no suspicion, no behaviour, no NavMesh agent, and no hearing.** If adding cameras in Phase 10 requires touching guard code, perception has leaked into the guard.

### 3. Three independent state axes

| Axis                        | Values                                                                                                      | Driven by           | Owner                         |
| --------------------------- | ----------------------------------------------------------------------------------------------------------- | ------------------- | ----------------------------- |
| **Stance** (posture)        | Standing, Crouching                                                                                         | Input               | Player (`PlayerStance`)       |
| **Alert** (concept 6.1)     | Idle → Curious → Investigating → Alerted                                                                    | The suspicion float | Every NPC with `NpcSuspicion` |
| **Activity** (what they do) | Civilian: Roaming, Deliberating, Calling, Scared … Guard: Patrolling, Investigating, Pursuing, Reporting … | Decision tree / FSM | Per role, as separate enums   |

These do not replace each other. A civilian can be `Alerted` and `Calling` at the same time; a guard who is `Investigating` is still standing. Collapsing them into one enum produces a combinatorial explosion of states. Keep three fields.

**Reporting is an activity, not an alert level.** A guard radioing in and a civilian making a discreet call are things NPCs _do_ once alerted, so they live in `GuardActivity` and `CivilianActivity`. The ladder stops at Alerted, as in `roadmap.md` Phase 3.

`Roaming` (civilian wandering) and `Patrolling` (guard following a route) mean different things, which is why guard and civilian activities are two enums rather than one shared one.

**Stance is posture only.** Sprinting is a speed, and concept 6.1 treats running as a noise source rather than a posture, so it lives on `PlayerMotor.IsSprinting` rather than in `Stance`, and NPCs read it as a conduct flag (decision 6). Wall-flatten (concept 6.1) is deferred: it is added later, as a `WallFlatten` stance with its own `fStance` factor, only if playtests show crouch-cover is not enough. The motor sprints only while standing, so crouching still beats sprinting.

### 4. Where types live, and which way dependencies point

Gameplay code is split into four assemblies, with dependencies pointing one way only:

```
NPC  ──►  Player  ──►  Core
 └──────────────────►  Core
```

- **Core** holds what both sides need and nobody owns: `NoiseBus`, `NoiseEvent`, `Zone`, `GameplayLight`, and the world objects (`Anomaly`, `Door`, `Breaker`, `Device`, and the `Ability` asset type). Core references no gameplay assembly. These are passive: readers query them, they never look up the player. A device does not check the player's ability charges; the player side checks and spends a charge, then calls the device (decision 20).
- **Player** owns every type NPCs read about the player: `Stance`, `PerceptionTargets`, `PlayerConduct`.
- **NPC** reads Player and Core. Nothing reads NPC except scenes and tests.

Within NPC, an enum used by two or more NPC systems lives in `Scripts/Data/`; an enum owned by one system lives in that system's folder. That is why `NpcAlertState` is in `Data/` while `CivilianActivity` is in `Civilian/`.

If NPCs ever need a posture of their own (a civilian crouching under a desk), they get a separate `NpcStance`. NPC crouching is a behaviour output while player crouching is input, and sharing the enum would fake a symmetry that is not there.

### 5. The player exposes what NPCs perceive about it

Two player-owned components carry everything perception needs, so NPC code never reaches into the player's internals:

- `PerceptionTargets` exposes every body point on the player, as a flat list and grouped by `BodyRegion` (Head, UpperBody, Hands, LowerBody, Feet). A body point is a `PerceptionPoint` on a bone that carries a trigger `BoxCollider`; NPC perception casts to the collider's bounds centre, so it follows the animated rig. NPC code never guesses bone names, and it keeps working when `PlayerStance` shrinks the capsule.
- `PlayerConduct` exposes what the player is doing right now: sprinting, carrying loot, using a tool, and so on as features land. See decision 6.

The player exposes only _where_ body parts are. How much each part matters is the observer's business (decision 13), so `PerceptionPoint` carries a region and nothing else.

### 6. Suspicion comes from conduct, not from being seen

Concept 6.1: suspicion accumulates "while you are perceived doing something **abnormal**". Visibility alone never raises it. This is what makes social stealth (concept 6.7), the work uniform (concept 7.2), and "being seen is not failure" (concept 4) possible.

Abnormality is judged by the NPC from three inputs:

1. **Conduct** — from `PlayerConduct`: sprinting, carrying loot, using a tool.
2. **Context** — from the `Zone` the player is in: public or staff. Identical movement is fine in a public aisle and abnormal in a staff corridor, unless the player is in uniform.
3. **Archetype** — per-conduct weights in `NpcArchetype`. A guard weighs conduct differently from a customer.

The suspicion rise per tick is roughly `visibility × abnormality`. The same conduct-and-context reading feeds two other consumers, so it is computed once and carried on the `Observation`:

- the witness **Behaviour** feature slot (concept 6.2: "moved abnormally, entered staff area"), and
- decision-tree input 1, **severity** (concept 6.8: moving oddly is not a crowbar in a display case).

**As built.** Conduct and context share one representation: `ConductFlags`, a `[Flags]` enum on the player side. Context is a flag like any other (`InStaffArea`), set by `PlayerConduct` from the `Zone` triggers the player is inside. NPCs therefore see one set of flags and weigh each through one mechanism, `ConductWeights` on the archetype. The uniform exception becomes one extra condition in `PlayerConduct` once wardrobe exists.

Abnormality is computed by the **perceiver**, with its own archetype, at tick time, and carried on the `Observation`. Set flags combine as `1 − Π(1 − w)`: one fully abnormal act gives 1, two acts at 0.6 give 0.84, and adding an act never lowers the result. Because the perceiver judges, a camera's observation carries abnormality judged by the camera's weights, and that is what the guard at the monitor receives (decision 11).

**Extended 2026-10-05: abnormal world state counts too.** Suspicion comes from abnormal conduct **or** abnormal world state: an open door, lights that went out (decision 18). Both are things only a robber causes. It still never comes from the player merely being seen. Sound was already a third source (decision 10); like sound, an anomaly sets a suspicion floor rather than rising every frame, and on its own never pushes an NPC past Investigating.

**Extended 2026-10-16: a described outfit counts for guards** (decision 25). This is the one deliberate exception to "never from being seen": a guard who sees the player wearing what the police description names grows suspicious from the match alone. It joins abnormality as one more term, weighted per archetype (civilians 0), and on its own never pushes past Investigating.

### 7. Perception is stateless; two components accumulate, separately

`NpcPerception` answers "what can I see right now, and what is the player doing" once per tick and nothing else. Two different things accumulate over time, in two different places:

- **`NpcSuspicion`** — how alarmed this NPC is. Rises with abnormal conduct in view, decays otherwise.
- **`NpcWitness`** — how well this NPC remembers the player. This is where `f_duration` lives: concept 6.2 defines it as a factor in _evidence quality_ ("a glance and a ten-second stare must not produce the same evidence"), not in alarm.

A calm civilian who watches the player sprint past for ten seconds has low suspicion and excellent evidence. That is the "calm witnesses are the dangerous ones" inversion from concept 6.8, and it only works if the two accumulators stay separate. Only abnormal conduct feeds the witness (decision 22, changed 2026-10-22): staring at a player who walks normally teaches nothing.

### 8. `IPerceiver`, not `IObserver`

`System.IObserver<T>` already exists, so the interface is `IPerceiver`, with `Observation` as its payload. `roadmap.md` has been updated to match.

### 9. The decision window is a real state

The 1.5–3 second deliberation from concept 6.8 is `CivilianActivity.Deliberating`, with a pending reaction and a visible timer on `CivilianBehaviour`. It is not a coroutine hidden inside the tree evaluation. The player has to be able to see it and interrupt it, so the rest of the game has to be able to read it.

A reaction that hides itself (pretending not to see, looking like `Working` while still feeding the witness) was planned as the exception and dropped on 2026-10-19: every reaction now shows its icon (thrift shop C6b).

### 10. Noise travels through the NavMesh

`PlayerNoise` currently resizes a `SphereCollider`, so sound passes through walls. It will be replaced by the static `NoiseBus` in Core: the player publishes a `NoiseEvent`, and each `NpcHearing` decides whether it heard it.

- The **event radius** is loudness, set by the source (sprinting, a dropped item, a drill; quiet shoes shrink it).
- The archetype's **hearing sensitivity** is a multiplier on the listener side.
- An NPC hears the event when the NavMesh path length to it is at most `radius × sensitivity`. Sound goes around corners instead of through them.

A static bus means no singleton GameObject has to exist in every scene.

### 11. Cameras have no suspicion; a guard watches the feed

A camera records evidence (perfect retention, visual features only) but does not raise alarms by itself. A `SecurityMonitor` station relays its cameras' observations to the guard posted at it, and that guard's `NpcSuspicion` decides whether to act.

This is what gives the concept's camera verbs their meaning. "Tamper with a camera so it shows an empty aisle" only matters if someone is watching. A guard at the monitor is not patrolling, which is concept 6.4's finite attention. A camera with nobody at the monitor still testifies after the job.

### 12. Assembly definitions now, for Player and NPC together

`roadmap.md` Phase 0 says to agree assembly definitions before gameplay code; the codebase is still small, so now is the cheapest moment. Do them together, as the four assemblies in decision 4 plus an editor-only assembly for `Player/Scripts/Editor/`.

Two reasons beyond compile time:

- About a hundred template and asset-pack scripts live in `Assembly-CSharp`. Gameplay assemblies stop recompiling alongside them.
- The one-way dependency rules above become compiler errors instead of conventions.

An assembly-definition assembly cannot reference `Assembly-CSharp`, so NPC cannot get one alone; it would lose access to `PlayerStance`. Each assembly also needs references to the packages it uses (Input System, AI Navigation, Animation Rigging, Cinemachine).

The shared editor menu paths (`MenuPaths`) already live in their own editor-only assembly, `SuspectUnknown.Editor`, in `Assets/Editor/`. When `Player.Editor` and `NPC.Editor` arrive, they reference it.

### 13. Geometry is shared; attention is per archetype

Two different questions decide how well an NPC sees the player:

1. **Geometry** — which body points have a clear line from the eye. Pure physics, identical for every observer.
2. **Attention** — which body regions this observer cares about. A guard watches the whole body; a civilian looks at faces and clothing and never at feet; a tired clerk sees only faces over the counter.

Geometry lives on the player (decision 5). Attention lives in `NpcArchetype` as a `RegionWeights` block: one weight per `BodyRegion`, 0 to 1. A weight of 0 means the region is ignored and no rays are cast at it.

`NpcPerception` casts to every point in each attended region. Each region's fraction is its clear points divided by its cast points, and visibility is the weighted average of the region fractions. Partial cover therefore reads as partial per region: three of seven upper-body points visible past a doorframe is 43 %, not a yes or no.

**Keep this separate from `FeatureVisibility` (M7).** Region weights decide what draws the eye, and so affect detection now. The feature mask decides what a witness remembers, and so affects evidence later. A civilian can notice someone by their legs and still only remember the jacket. Two knobs, two places.

### 14. `Observation` carries no arrays

`Observation` is a struct copied into every consumer. An array field would be shared by reference between every copy, so the next tick would overwrite observations that `NpcWitness` is still holding. Per-point and per-region detail that only the debug view needs stays on `NpcPerception`, exposed read-only with a `Debug` prefix (`DebugPointClear`, `DebugRegionFraction`). M2 and later code never reads those.

What `Observation` does carry is one float per region (five plain fields, not an array): the share of that region's points with a clear line, 0 to 1, and 0 for a region the archetype ignores. The witness reads the slot's own fraction, so any glimpse of the feet exposes the footwear and a clear face teaches fully even with the legs behind a counter. Changed 2026-10-17 from a one-bit-per-region mask, which said only seen or not.

### 15. Patrol is coverage-driven: points of interest, not fixed routes

Guards do not walk authored routes. They serve **points of interest** (POIs). Each POI holds a **coverage** value from 0 to 1 that rises while a guard is inside its radius and drains over time. How fast it drains is its importance: a safe drains fast, a public lobby slowly. An idle guard repeatedly picks the POI that needs it most, walks there, dwells until it is covered, and picks again.

Scoring, per guard, over every unclaimed POI:

```
need       = 1 − coverage
importance = baseDecayRate × DecayMultiplier
distance   = NavMesh path length to the POI
score      = need × importance / (1 + distance / distanceScale)
```

Need comes first, so a fully covered POI scores 0. Distance discounts but never forbids: a neglected POI far away still beats a covered one next door. Path length is computed only when choosing, never every frame.

**Why.** The Phase 8 optimiser needs a target it can change. With fixed routes it would have to generate routes, which are hard to produce and hard to explain. With POIs its output is two things: which POIs exist, and each one's `DecayMultiplier`. "The back door now drains twice as fast, because you used it on three jobs" is one number, and it maps straight onto the intel change log. Phase 9 poisoning works the same way: using the front door on low-value jobs raises the front-door POI, guard time moves there, and the back door goes quiet.

**The POI never learns.** `baseDecayRate` is level design. `DecayMultiplier` is set from the `SecurityPlan` when the job loads, and the learning behind it lives in the save (Phase 7) and the model (Phase 8), the same pattern as the suspicion skill hooks.

**Guardrails:**

1. **Predictability.** Scoring has no randomness, ties go to the lower index, and POIs are visible in debug. The player learns "the safe gets checked often" the way they would learn a route.
2. **Commitment.** A guard re-chooses only after arriving and dwelling, or when an alert state interrupts. Never halfway, or two similar scores make it thrash.
3. **Claims.** A POI a guard is heading for scores zero for every other guard, so guards spread out instead of piling onto the neediest POI.
4. **Budget.** If total drain exceeds what the guards can cover, every POI empties and guards run in circles. Total decay is the Phase 8 fixed budget, and the solvability check still applies: coverage must never make a job impossible.

**Consequences.** There is no `ReturningToPost`: after an investigation the guard just picks again, and the nearest neglected POI wins naturally. A static guard post is one POI with high decay and a long dwell. POIs detect guards by distance, not triggers, since the `Volume` layer collides with `Player` only. Each POI knows its `Zone`, because the Phase 8 Markov model works over zones.

**Extended 2026-10-16: base and candidate POIs** (decision 24). A level has **base** POIs, always active and placed by the developers, and **candidate** POIs, also placed by the developers but inactive until the security plan switches them on at load and sets their `DecayMultiplier`. The optimiser never creates POIs at arbitrary positions: candidates keep every POI on the guard's NavMesh, give each a precomputed list of the rooms and entrances it covers, and give the change log a name to use. A POI can also carry a **runtime** drain factor that is not the plan's, such as the office draining 1.5 times faster once its safe has been opened (`Assets/thriftshop-level.md` 2.3): `Importance = baseDecayRate × DecayMultiplier × runtime factor`.

### 16. Animator controllers are built from code, around stances

NPC animator controllers are generated by editor builders and never edited by hand. The builder is the source of truth: transition settings, thresholds, and variants are reviewable in git, and a bad experiment is one rebuild away from undone. A rebuild resets the existing asset in place, so its GUID, and every prefab's reference to it, survives.

- **Root motion is off.** The `NavMeshAgent` moves the NPC and the role behaviour turns it; the animator is visual only. Clips bake rotation and height into the pose. Gait clips leave position XZ unbaked, so the builder can measure their real speed from root motion, which is then discarded.
- **Clips are grouped into stances** (`ClipSet`): one or more interchangeable clips for one pose. A stance with several clips becomes a variant tree, and the role's animation component picks a whole-number variant at a moment the switch cannot show (on arrival at a POI, just before a one-shot). Picks never repeat the previous one. Adding a variant is a clip in `GuardClips` plus a count in the params class.
- **Standing stances step into turn clips** on a signed `TurnRate`, with a double-speed pair beyond the clip's own rate so the feet keep up with quick turns. Turn clips keep root rotation unbaked, so the code turns the body and the clip only supplies the footwork.
- **Locomotion is a Speed tree in metres per second.** The behaviour's movement speeds must equal the measured gait speeds, or the feet slide.
- **Every transition is interruptible by its destination,** with a fixed duration in seconds, so a state that flips back mid-blend reverses smoothly instead of finishing and popping.
- **Parameter names live in params classes** shared by the builder and the runtime: `NpcAnimatorParams` for what every NPC has, one class per role for the rest. Counts are per role, never shared.

### 17. Light is sampled from the scene's lights, not authored volumes

The light level NPCs read comes from the actual lamps. `GameplayLight` (Core) marks a Point or Spot `Light` as one that counts for gameplay and registers it in a static list. `PlayerLighting` samples every registered light at the player's chest about ten times a second: URP-like distance falloff and spot cone, a shadow ray against `Occluder` for each light that casts shadows, a sum normalised to 0–1, and an ambient floor. NPCs read the cached `Level` through `NpcArchetype.LightFactor`, which maps darkness to the archetype's `darkVisibilityFactor` rather than to zero.

**Why.** The first plan (concept 6.1) was trigger volumes tagged with a light level. Those duplicate the lighting by hand: they drift whenever a lamp moves, have to be placed twice (now, and again after the Phase 11 lighting pass), and when they drift the player hides in a shadow they can see and is spotted anyway. Sampling the lights makes the picture and the gameplay agree by construction, and the breaker verb becomes "turn these lights off" with no extra data.

**Rules:**

1. **Light is the player's property.** It is computed once per player tick, not once per NPC, so cost stays flat as NPCs are added.
2. **Gameplay follows the picture.** A light with shadows off lights the player through walls on screen, so it skips the shadow ray too.
3. **Only lights with `GameplayLight` count.** Decorative lights stay out.
4. **Point and Spot only.** Directional and bounce light are folded into the ambient floor.
5. **Breaker lights must be Mixed or Realtime.** Toggling a Baked light changes the number but not the picture.
6. **Fixture meshes stay off `Occluder`,** or a lamp shadows itself.

It stays explainable for the course: the level is a sum of named terms, and the debug readout prints the level, the raw sum, and how many lights are lit and shadowed.

### 18. Anomalies: world state only a robber causes

An **anomaly** is a world object in a state that only the player causes: an open door, a tripped breaker, later an emptied display case. NPCs notice anomalies the way they notice the player, and react to the world instead of only to the player's body.

- `Anomaly` (Core) is a passive component with a static registry, like `GameplayLight`. It exposes whether it is active, the point an NPC must see, and its kind (`AnomalyKind`). The object that owns it (a `Door`, a `Breaker`) switches it on and off.
- NPCs check every raised anomaly on their perception tick, cheap to expensive like the player rays: the cone, then one linecast to the anomaly's point. Noticing one raises an event; it does not go on the `Observation`, which is about the player (decision 14).
- **Noticing and suspicion are separate.** Every NPC notices raised anomalies; how suspicious one makes it is the archetype's kind weight. A weight of 0 means "notices, but is not suspicious": the tired clerk and customers see an open door without growing suspicious. Do not skip weight-0 kinds in the sight check; the list of raised anomalies is almost always empty, so checking all of them costs nothing.
- **Suspicion:** noticing sets a floor of `anomalySuspicion × kind weight`, per archetype, and makes the anomaly the stimulus and the guard's last-known position. Like sound, an anomaly alone never pushes past Investigating (decision 6).
- **What an NPC does about an anomaly is its role's business** (decision 1). Open doors: the guard slams them; civilians, the tired clerk included, ignore them (clerk door chores dropped 2026-10-21).
- **Resolution is an errand** (decision 20): one NPC of the right role claims the anomaly, walks to it, plays a reaction, and fixes it through `Resolve()`. A claim works like a POI claim (decision 15), so two NPCs never fix the same thing.
- **Witnesses:** being seen _causing_ an anomaly is conduct. For doors that is the `Lockpicking` flag (decision 19), which raises suspicion in whoever sees it. The pick itself goes to the job log for adaptive security, not to the witness model (decision 23). An `OpeningDoor` flag for doors without a lock waits until a level needs one.

**Why.** Only the robber opens doors or trips breakers, so these are the clearest evidence the game has, and they tell the player that their actions leave traces even when nobody saw them. One mechanism covers doors, the breaker, and later loot, instead of one special case each.

### 19. Doors: guards pass through, an open door is evidence

Changed 2026-10-17 (was "Doors are the player's"). Civilians walk only in open areas and never use doors: a closed door is a wall to them, blocking their paths. Guards may open a closed door they need to pass, and close it again behind them. A door is evidence only when the **player** opens it and leaves it open.

- **Guards pass through.** A guard-usable door has a `NavMeshLink` across it on a NavMesh area `Door`. The guard crosses links itself (`autoTraverseOffMeshLink` off): it stops, opens the door with `OpenForPassage()`, which raises no anomaly and leaves the lock as it was (guards carry keys), walks across, and closes the door once it is outside the reach on the far side. The check is the reach's bounds, not its trigger: reaches are on `Volume`, which collides with the player only, and switch off while the door is open.
- **Which doors get a link:** only doors guards need inside a level. In the thrift shop that is the `StaffRoom Door`, the office's only way in. Exterior doors get none, so guards stay inside.
- **Civilians and hearing exclude `Door`.** Civilian agents leave the `Door` area out of their area mask, and hearing's path query does too, so a closed door still blocks paths for civilians and blocks sound for everyone (decision 10). An open door is uncarved floor, so sound carries through it.
- **An open door the player leaves is an anomaly** (decision 18). It also opens the NavMesh. A guard who notices it while not chasing plays a confused animation and slams it shut, which closes the path and clears the anomaly. The tone stays comedic: guards cannot stand an open door.
- **A chasing guard ignores doors as evidence.** It runs through open doors without slamming them and opens closed ones without stopping to close them, so shutting yourself in is no escape.
- Doors are built with a `NavMeshObstacle` that carves while the door is closed. The door leaf is ignored by the NavMesh bake, or it would be baked in as a permanent wall.
- The leaf is on `Occluder`, so a closed door also blocks sight and light.
- **Locks have levels, 1 to 3, gated by skill.** All interactables are unlocked by buying skills, as in the Payday series. A lock above the player's Lockpicking level cannot be attempted at all; within it, a higher level is a harder minigame. Picks are unlimited and never break. A picked door stays unlocked for the heist.
- **Lockpicking is a minigame in the style of Dying Light:** swing the pick to find a hidden sweet spot, apply tension to turn the lock, and force it at the wrong angle and the pick slips with a rattle. It is very suspicious when seen (the `Lockpicking` conduct flag, which every archetype weighs highly) and noisy when it goes wrong.
- For slamming, the guard resolves the door's **anomaly**, and the door slams itself in response; the same mechanism serves the breaker and staff errands. For passage the guard calls the `Door` it is crossing directly (NPC → Core).
- `Door.prefab` carries an inactive `Entrance` child for the job log (decision 24), switched on only for exterior doors.
- **A double door is one `Door` with `secondHinge` set**, not two doors: one lock, one anomaly, one obstacle, one pair of reach triggers, one `Entrance` and one guard link spanning the whole doorway. The second leaf swings the mirrored angle. Picking it once opens both leaves (jewelry store main entrance).

This replaces the earlier "jam a door" verb, which assumed NPCs walk through doors. Full design of the door and the minigame: `Assets/doors-and-lockpicking.md`. Guard passage is built as thrift shop step T2b (`Assets/thriftshop-level.md`).

### 20. Verbs are abilities used at in-world devices

There is no physical throwing. Every verb is a **bought ability** with a limited number of uses per heist, used by reaching an in-world **device**: a router, the breaker, a desk phone. The tone is cartoony and relatable, and every ability must move an NPC or change what an NPC perceives (concept 6.7).

- `Ability` (Core) is a ScriptableObject holding data only: name, uses per heist, description. The remaining uses are runtime state on the player (`PlayerAbilities`), never on the asset (decision 1). The shop (Phase 7) decides which abilities the player owns.
- `Device` (Core) is the world object. It names the `Ability` it needs and exposes `Activate()`. It never looks up the player: the player side checks a charge, spends it, and calls `Activate()`, so dependencies stay Player → Core.
- Activating a device raises an event. NPC role behaviours subscribe and respond.
- **Responses are errands:** a claimed target, a point to walk to, a reaction clip, a duration, then back to normal. Slamming a door, resetting the breaker, complaining to the clerk about the internet, and answering a prank call are all the same mechanism with different data. This is the one shared piece; there is no general scripting system, and each ability is its own small, named effect.
- Until the interaction system exists, devices are triggered with `[ContextMenu]`, calling the same `Activate()` the interaction will call.

Abilities that act on civilians (disable the internet, so customers go and complain to the clerk) need civilians and arrive in M6. M5 proves the system with an ability that acts on the guard.

### 21. The vision cone is drawn as a light

The vision cone is a player-facing gameplay element, not a debug view: a glowing cone on the floor, like a torch beam, colored by alert state. The player reads danger from it at a glance.

- **Guards and cameras only,** with one exception. Civilians have no cone; their suspicion indicator is enough, and a room of customers with cones would be unreadable (decision 22). The exception is the clerk at home in the apartment level, the tutorial job: their cone teaches the player to read cones before the first guard. It is **semi-dynamic**: small while the clerk dozes, full while awake. The size is runtime state on the NPC (a view scale on `NpcPerception`, never written into the archetype), and the cone follows it without rebuilding the fan. Design in `Assets/apartment-level.md`, section 2.2.

- **The mesh is a static fan, built once** at full range from the archetype's range and FOV. **Cutting happens in the shader** (`NPC/Shaders/VisionCone.shadergraph` with a Custom Function in `VisionCone.hlsl`). Each frame the CPU casts the rays and writes one pixel per ray into a small float texture, `_RayData`: R is the wall distance, G where low cover starts, B where its shadow ends, all as fractions of range. A is free, reserved for the corner refinement in the known issue below. No mesh is rebuilt per frame.
- **Walls:** rays at eye height against `Occluder`. The shader blends the two nearest rays, so slanted wall faces read as straight lines, and fades over a width in metres (`edgeSoftness`, about a body's width) by averaging samples on both sides. The fade is centred on the wall line, so the cone shows neither less nor much more than the guard sees. Partial visibility near corners is real in perception too (some body points blocked, some not).
- **Low cover is shown, not hidden.** An eye-height ray passes over a counter, but a crouched player behind it is hidden. A second ray at crouched-head height finds the cover's front, a short ray down finds its top, and similar triangles give where its shadow ends: `e = s × (eye − crouchHead) / (eye − coverTop)`. The shader dims that strip (`_SafeDim`) instead of cutting it out. Using the cover's front edge is conservative: the strip can be shorter than the truly safe area, never longer. The strip blends between rays only where both see cover; next to open floor it uses the nearer ray, or it would smear into a sliver.
- **Brightness is perception's own falloff.** The archetype's distance and angle curves are baked once into small textures, and the cone's brightness is `fDistance × fAngle`, the same product perception uses. Where the cone is faint, detection is weak. `edgeVisibility` keeps the outline faintly visible. Curves arrive at zero with a flat tangent, or the eye sees a crease at the edge.
- **Color follows the alert state,** eased on display only, set through a `MaterialPropertyBlock` so no material is copied per NPC.
- The overlay approximates perception; it does not drive it. Tune the crouch height so that "crouch-safe" really is safe, and check it against the debug rays. Where the two disagree, perception is the truth and the overlay is fixed.
- It updates in `LateUpdate`, after the NPC has turned. Measure the cost before lowering the update rate or the ray count; lowering the update rate makes walls wobble while the guard turns.
- **CPU:** a cone off screen casts no rays. `isVisible` comes from last frame's culling, so a cone entering the screen shows last frame's walls for one frame, at the edge. Ray directions are built once in `Start` and shared with the fan, so the drawn cone and the cast rays never disagree; range and FOV are therefore fixed after `Start` and changes need a restart. In the editor the Scene view counts as a camera, so close it when profiling.
- **Known issue: corners flicker** while the guard turns or walks. The rays are fixed to the cone and sweep across the world; at a corner, neighbouring rays disagree sharply and the blended wedge changes shape as a ray passes the corner. Planned fix: where two neighbouring rays differ by more than a threshold, binary-search the exact corner angle with a few extra rays and store it in A, and have the shader step exactly there. A few extra raycasts per corner.

### 22. Civilians witness; they rarely alarm

Civilians are in the store for their own reasons: customers shop and the tired clerk works the register. They are not watching for thieves. Their importance to the player is **what they remember**, not how alarmed they get.

- **Only abnormal conduct feeds the witness** (changed 2026-10-22). Perception runs all the time, but a sighting adds evidence only while the observer's archetype reads the player's conduct as abnormal (abnormality above 0: sprinting, lockpicking, taking loot, a staff area). Someone who just sees the player walk by is not a witness. Each abnormal tick counts on its own: watching calmly after the act teaches nothing more. This replaces concept 6.8's "calm witnesses are the dangerous ones": a calm civilian is dangerous only if they watched the act itself. Evidence and suspicion stay separate accumulators (decision 7): a witness can gather a clear look at a sprint that never makes them Curious.
- **Suspicion only rises on clearly criminal acts,** and only the decision window at Alerted turns it into a reaction (call, get scared, walk away…). Civilians never flee (changed 2026-10-19): a scared civilian freezes, covers their face and sees nothing until they calm down. Criminal acts, each added as a conduct flag together with what produces it: lockpicking (M5), taking loot, smashing glass, a visible tool, and using a device that is not a door (a router, a car alarm, a desk phone). A mask would be suspicious if masks are ever added.
- **Ordinary oddness barely registers.** Sprinting in front of a customer takes about 5 seconds of full view to make them Curious. Open doors do not make customers suspicious at all.
- **One rise rate for everyone.** Every archetype uses the guard's rise rate of 0.5, so a conduct weight means the same thing for every observer (weight × 0.5 is suspicion per second at full visibility). That is how lockpicking is exactly as fast for a civilian as for a guard, and it keeps the numbers explainable for the report. Civilians still notice less in practice, because they see less far and fade faster with distance.
- **Roles differ in data and in what they do** (decisions 1 and 18): civilians ignore open doors; only guards deal with them. (A manager who investigated them was dropped on 2026-10-19, and the tired clerk's door chore on 2026-10-21.)
- **At home, the same clerk is the opposite of calm** (`Assets/apartment-level.md`). The apartment interior is a `Staff` zone, and the `ApartmentClerk` archetype weighs `InStaffArea` heavily, so a stranger seen inside is alarming on its own. Still the shared rise rate; only the weights differ. Their role behaviour, `ResidentBehaviour`, runs a sleep rhythm in an armchair, an occasional snack run to the kitchen along the NavMesh, and a fixed reaction: a decision window that fills while they can see the player, then a police call that fails the job.
- **The alert ladder means less for civilians:** Curious is a glance while carrying on; Investigating is stopping to look; Alerted opens the decision window.
- **No vision cones** (decision 21). The suspicion indicator is enough.
- **Hearing stays as tuned in M4:** a nearby sprint gives a small floor but never Curious; a lockpick slip next to a customer makes them Curious.
- **A thin slice of the witness model comes with civilians** (M6 step 2): `NpcWitness` accumulates exposure, how long and how well each NPC has seen the player, with a debug readout. Without it, "which civilian is dangerous" cannot be tested. Feature slots, confusion matrices and the report stay in M7.

### 23. The witness model is appearance only; behaviour goes to the job log

Two models, two questions. The **witness model** (M7) answers "what did the burglar look like?" from what NPCs saw. **Adaptive security** (Phase 8) answers "what does the burglar do?" from the job log: entry points, routes, the doors they always pick, and it moves guard POIs to match. Behaviour and method are therefore not witness slots, even though concept 6.2 first listed them there.

- **Why two:** the job log records what the player did whether or not anyone saw it, which a witness-fed behaviour model could never learn; and keeping the witness model to appearance keeps its evidence one kind, combined one way (log-odds). Two separate models, a Bayesian evidence model and a Markov route model with an optimiser, are also clearer to explain in the AI report.
- **Slots** are the player's customization categories: **Head, Chest, Legs, Feet**. Each part is remembered as a **style and a color separately** (`Chest_002_Black` is style 002, color Black), each with its own belief and its own confusion table, so a witness can get the color right and the cut wrong.
- **Burns are temporary** (Phase 7): an item confirmed into the police profile counts as burned for one or two jobs, or until newer witness evidence describes something different. Never permanently.
- **First built in the apartment level** (`Assets/apartment-level.md` 3.6, Track W), right after the clerk, with the clerk as the only witness and the newspaper as the report.
- **Appearance is fixed during a job** (2026-10-16). The player can never change clothes mid-heist; outfits change only between jobs, in the menu's wardrobe and shop. The counter to a burned outfit is therefore changing it before the next job. Concept 7.2's mid-job layering is cut.

### 24. Adaptive security: predict, then place, at load, from completed runs

Concept 6.4, first built in the thrift shop (`Assets/thriftshop-level.md` sections 4 and 5).

- **The job log** records what the player did in the run: room transitions (each room is a `Zone`, numbered by its place in the level's `JobLayout`; the street is room 0; `PlayerRoom` reports a change only after 0.5 s in the new room) and inbound crossings at each `Entrance`. It is kept whether or not anyone saw it (decision 23).
- **Only completed runs teach security.** On `JobOutcome.Completed` the location's stored counts decay by 0.8 and the run is added, in `actions.json`, a file of its own. A failed run is forgotten, so failing never makes the next try harder (the same rule as progress and the police record).
- **Stage A, prediction:** a first-order Markov chain over rooms with Laplace smoothing, a Dirichlet-multinomial over entrances, and forward propagation to a predicted occupancy per room.
- **Stage B, placement:** greedy maximum coverage over candidate POIs (decision 15), scored on room occupancy and entrance probability. **Deterministic:** no sampling, ties to the lower index, so the same history always gives the same plan.
- **The budget is per level, as data** (2026-10-18): a list of `SecurityTier`s in a `SecurityBudget` asset (Core), referenced by the level's `SecurityLayout` and by the main menu's `MapLocation` so both read the same numbers, each giving the guards and the adaptive POIs from a completed-run count. The guard count never shrinks. The thrift shop's tiers: 0 runs → 1 guard, 0 POIs; 1 → 1, 1; 2 → 2, 2; 3 or more → 2, 3. The planner takes the tier as an argument and knows no level.
- **It runs once, at load,** in `SecurityDirector`, which also spawns the guards at random free spawns of the level. Where a guard starts carries no plan information, since guards pick POIs by importance. Security never learns during a run.
- **Explainable by construction:** each pick writes one line from its marginal-gain step ("Guards now check Side door: you come in through Emergency exit on 64 % of runs, drains 2.0× faster"; the record holds decayed sums, so the line quotes probabilities, not run counts). The lines feed the debug overlay and the AI report; the player sees only "Raised security" on the main menu.
- **Solvability is held by authoring** while the budget is this small: no candidate covers the required loot, and none covers more than one entrance. A tier's adaptive POIs stay below the entrance count.
- **Data in Core, model in NPC.** `JobLog`, `ActionRecord` and `ActionStore` are plain data in Core, so the main menu can read them; `ActionModel` (the prediction, named for what it models: the player's actions), `SecurityPlanner` and `SecurityDirector` are NPC.
- **The layout is Core, the candidates are NPC** (2026-10-17). `JobLayout` (Core, on the `Job` object) lists the rooms as `Zone`s and the entrances as `Entrance`s and numbers both, so `JobLog` and `ActionStore` can size and check against it. `SecurityLayout` (NPC) holds only the guard spawn candidates and candidate POIs. The saved record keeps the room and entrance names and is dropped when they differ from the layout's.

### 25. Guards and civilians recognise a described outfit

The police description saved at the end of a completed job (Track W, W8) is loaded at job start. When an NPC observes the player, the match between the description and the player's current `CharacterAppearance` (per slot: confidence × color match × a style bonus, averaged over the four slots, with confidences under "possibly" counted as 0) times the archetype's `recognition` weight joins abnormality (decision 6). The guard and the shop clerk weigh it 1, customers 0.9, the apartment clerk and cameras 0 (changed 2026-10-22: civilians recognise too). It has no ceiling (changed 2026-10-22): recognition on its own can take an NPC all the way to Alerted, as fast as match × weight allows, so a guard chases and a civilian opens the decision window from sight alone.

- **A burned outfit is known on sight** (2026-10-22). A worn part is burned when the description matches its color at 60 % or more (the sketch's `certainAt`, printed without "probably") and its style at 50 % or more (`WitnessReport.IsBurned`, the one rule shared by recognition and the wardrobe). With two or more burned parts every observer whose `recognition` is above 0 weighs the outfit at least 0.9 (`JobWitnesses.BurnedRecognition`), so even a weak overall match alerts quickly.

- **Why:** it closes the witness loop. Being seen well on one job costs something on the next, which is the burn of concept 6.3, and the counter (change clothes between jobs) already exists.
- **Burns stay temporary:** the description fades by half every completed job, and new evidence describing something else takes over.
- **Dependencies:** the match is NPC code reading `WitnessReport`; Core and Player never see the description.

### 26. Only the player's floor is shown; the others fade

In a building with several floors, a player on one floor must not learn what another holds: where the loot is, where the guards walk. `BuildingFloors` (Core, one per level) keeps a continuous view level, 1 on floor 1 and 1.5 halfway up the stairs to floor 2, and fades everything from it through the OccluderWall shader's `_Hide` dither (`RendererFade`). Built for the jewelry store and reused for the bank.

- **Which floor the player is on** comes from `FloorVolume` triggers on the `Volume` layer (the newest one entered wins). On `FloorStairs` the player's height drives the view level instead, so the floors fade step by step rather than switching at the top. Anywhere else a change of floor (a door, the garage shutter) fades over `fadeDuration` (0.5 s).
- **The building** is grouped per floor in the Blender model (`Floor_Lower`, `Floor_Base`, `Floor_Upper`, `Floor_Roof`). A floor above the player fades to its `belowHide`; renderers centred in its `fullHideArea` fade fully. In the jewelry store the ground floor straight over the lower floor (cut at the lower floor's east wall) fades fully, and the rest of the ground floor to 0.5, so the player in the basement still sees the shape of the shop. The tops of the player's own floor's walls (`WallTops_*`) fade out too, so the camera sees over them. While the player is outside, the whole building shows.
- **Props and NPCs fade out fully** whenever their floor is not the player's, half-faded part included: props through `BuildingFloors`' prop roots (floor from height, once), NPCs through `FloorHideable` (floor re-checked on a 0.5 s tick, since they change floor only on stairs). The vision cone and the suspicion indicator, edge arrow included, fade with their NPC; a hidden cone casts no rays.
- **Shadows stay:** a fully hidden building part draws shadows only, so the floor below stays as dark as it was lit. A hidden prop or NPC is not drawn at all.
- **Materials:** everything that fades uses an OccluderWall material. Props, NPCs and floor slabs use `OccluderPropMaterial` (no player-sphere fade); walls that should open around the player keep `OccluderBaseMaterial`. Transparent glass fades its alpha instead, as it cannot dither.
- **Why:** concept 2 fixes the camera, so a floor above would cover the floor below. Hiding only the floors above would still show guards and loot through the cut; fading them by floor keeps each floor's information on that floor.
- **Dependencies:** all of it is Core. NPC code reads `FloorHideable.Hide`; Player code is untouched. Levels without `BuildingFloors` are unaffected: `FloorHideable` turns itself off.

---

## File structure

Status as of this revision:

- **stub** — Unity template, no logic
- **declared** — the type exists with no members (enums have their values)
- **working** — implemented
- **—** — does not exist yet

```
Assets/
├── Editor/             MenuPaths, SuspectUnknown.Editor.asmdef
├── Core/Scripts/
│   ├── Animation/      Dangle
│   ├── Lighting/       GameplayLight
│   ├── Noise/          NoiseBus, NoiseEvent
│   ├── Rendering/      RendererFade (decision 26)
│   ├── Volumes/        Zone, ZoneType, BuildingFloors, BuildingFloor, FloorVolume, FloorStairs, FloorHideable
│   └── World/          Anomaly, AnomalyKind, Door, Breaker, Device, Ability (M5)
├── Player/Scripts/
│   ├── Editor/         MapPlayerPerceptionPoints …
│   ├── Locomotion/     PlayerStance …
│   ├── Noise/          PlayerNoise
│   └── Perception/     BodyRegion, PerceptionPoint, PerceptionTargets, CharacterAppearance, PlayerConduct, PlayerLighting, PlayerLightingDebug
└── NPC/
    ├── Art/Guard/Animation Clips/   the old Mixamo guard clips, one folder per stance; unused since 2026-10-20 (the guard plays NPC.fbx's Guard_* takes)
    ├── Animators/                   GuardAnimator.controller (generated, never edited by hand)
    ├── Archetypes/                  ScriptableObject instances (.asset)
    ├── Materials/                   VisionCone.mat
    ├── Prefabs/                     Guards/Guard.prefab, UI/OffscreenIndicator.prefab
    ├── Shaders/                     VisionCone.shadergraph, VisionCone.hlsl (decision 21)
    └── Scripts/
        ├── Animation/               NpcAnimator
        ├── Core/Animator/           NpcAnimatorParams, GuardAnimatorParams
        ├── Core/Editor/             AnimatorBuildUtility, ClipSet, Gait, TurnSet
        ├── Editor/                  GuardLocomotionBuilder, GuardClips
        ├── Data/
        ├── Perception/
        ├── Guard/
        ├── Civilian/
        ├── UI/
        └── Witness/
```

### NPC/Archetypes/

| Asset                                  | Notes                                                                      | Status       | Milestone |
| -------------------------------------- | -------------------------------------------------------------------------- | ------------ | --------- |
| `SecurityGuard.asset`                  | Observer skill 1.0, long retention, behaviour features visible             | vision block | M1        |
| `Civilian.asset`                       | Observer skill 0.6, no behaviour reading                                   | vision block | M1+       |
| `TiredClerk.asset`                     | Observer skill 0.4, fast memory decay, obvious visual features only        | vision block | M1+       |
| `SecurityCamera.asset`                 | Visual features only, no memory decay                                      | vision block | M8        |

Create these from `Assets > Create > Suspect Unknown > NPC Archetype`. Each asset gains values as its milestone adds a block to `NpcArchetype`. Observer skill values are from concept 6.2 and arrive with the M7 witness block.

Vision block starting values. Region weights are head / upper body / hands / lower body / feet.

| Archetype         | View distance | FOV | Eye height       | Crouch factor | Tick rate | Region weights              |
| ----------------- | ------------- | --- | ---------------- | ------------- | --------- | --------------------------- |
| SecurityGuard     | 18            | 110 | 1.7              | 0.6           | 10        | 1.0 / 1.0 / 0.8 / 0.8 / 0.6 |
| Civilian          | 12            | 120 | 1.65             | 0.5           | 5         | 1.0 / 0.8 / 0.3 / 0.2 / 0   |
| TiredClerk        | 8             | 80  | 1.6              | 0.4           | 4         | 1.0 / 0.5 / 0 / 0 / 0       |
| SecurityCamera    | 20            | 70  | 0 (root at lens) | 0.8           | 10        | 1.0 / 1.0 / 0.6 / 0.6 / 0.3 |

The guard's hand weight is high because tool use is conduct guards read. The camera's feet weight is low because a ceiling camera sees headwear well and footwear badly. Lower tick rates for civilians make them slower to notice and cheaper to run.

Suspicion block values. Conduct weights are sprinting / in staff area; `lockpicking` (M5 step 2.5) and `stealing` (apartment step A4) follow, both 1.0 for every archetype. **Every archetype shares the guard's rise rate of 0.5** (decision 22), so a weight means the same thing everywhere: weight × 0.5 is suspicion per second at full visibility.

| Archetype         | Conduct    | Rise   | Decay  | Delay  | Curious | Investigating | Alerted | Hysteresis |
| ----------------- | ---------- | ------ | ------ | ------ | ------- | ------------- | ------- | ---------- |
| SecurityGuard     | 0.6 / 0.8  | 0.5    | 0.1    | 2.0    | 0.2     | 0.5           | 0.9     | 0.05       |
| Civilian          | 0.1 / 0.1  | 0.5    | 0.2    | 1.0    | 0.25    | 0.55          | 0.9     | 0.05       |
| TiredClerk        | 0.1 / 0.25 | 0.5    | 0.3    | 0.5    | 0.3     | 0.6           | 0.9     | 0.05       |
| SecurityCamera    | 0.5 / 0.8  | unused | unused | unused | unused  | unused        | unused  | unused     |

Anomaly block values (decision 18). The floor a noticed open door sets is `anomalySuspicion × openDoor`; weight 0 means the NPC still notices the door but is not suspicious of it.

| Archetype         | anomalySuspicion | openDoor | Floor | What it does about an open door   |
| ----------------- | ---------------- | -------- | ----- | --------------------------------- |
| SecurityGuard     | 0.6              | 1.0      | 0.6   | Investigates, slams it            |
| TiredClerk        | 0.4              | 0        | 0     | Ignores it                        |
| Civilian          | 0.3              | 0        | 0     | Ignores it                        |
| SecurityCamera    | 0.6              | 1.0      | 0.6   | What the monitor guard reads (M8) |

Civilians barely react to running (about 5 seconds of sprinting right in front of a customer before they are Curious) and react to lockpicking exactly as fast as a guard at the same visibility. The clerk is staff, so a stranger in the back room stands out a little. The camera has no `NpcSuspicion` (decision 11), so only its conduct weights matter: they define what the guard at the monitor reads through the feed. Its other suspicion values copy the guard's so the asset stays valid.

Hearing and light block values, as tuned in M4. Sensitivity multiplies how far a sound carries to this NPC (decision 10); hearing suspicion is the floor a sound at full loudness and alarm 1 sets. Dark factor is `fLight` in full darkness (decision 17). The last two columns use the formula in the M4 notes: a sprint is radius 9, alarm 0.3; a "crash" is a reference loud sound of radius 12 and alarm 1, such as a noisy ability effect or panic (M6). Nothing publishes one yet.

| Archetype         | Sensitivity | Hearing suspicion | Dark factor | Sprint: Curious within | Crash: Curious / Investigating within |
| ----------------- | ----------- | ----------------- | ----------- | ---------------------- | ------------------------------------- |
| SecurityGuard     | 3.0         | 0.7               | 0.3         | 1.3 m                  | 25.7 m / 10.3 m                       |
| Civilian          | 1.5         | 0.5               | 0.25        | never                  | 9 m / never                           |
| TiredClerk        | 0.6         | 0.4               | 0.2         | never                  | 1.8 m / never                         |
| SecurityCamera    | 0           | 0                 | 0.15        | never                  | never                                 |

Only the guard reacts to footsteps; for everyone else the sound that counts is a crash, and civilians do not investigate (their reaction is the M6 tree). Every hearing suspicion is below that archetype's `alertedAt`, so a sound alone never alerts. The camera hears nothing (decision 2); its zeros keep the asset valid, and its low dark factor makes the breaker a strong counter to cameras.

### NPC/Scripts/Data/

| File                   | Type                    | Purpose                                                                    | Status                                      | Milestone      |
| ---------------------- | ----------------------- | -------------------------------------------------------------------------- | ------------------------------------------- | -------------- |
| `NpcArchetype.cs`      | ScriptableObject        | Every tuning number for one archetype                                      | working (vision, light, hearing, suspicion) | M1, M2, M4, M7 |
| `RegionWeights.cs`     | `[Serializable]` struct | Per-region attention weights for one observer (decision 13)                | working                                     | M1             |
| `ConductWeights.cs`    | `[Serializable]` struct | Per-flag abnormality weights and the combine formula (decision 6)          | working                                     | M2             |
| `AnomalyWeights.cs`    | `[Serializable]` struct | Per-kind anomaly weights for one observer (decision 18)                    | working                                     | M5             |
| `NpcAlertState.cs`     | enum                    | Idle, Curious, Investigating, Alerted                                      | declared                                    | M2             |
| `Observation.cs`       | struct                  | What a perceiver reports each tick it sees the player                      | working (M1, M2 fields)                     | M1, M2         |
| `HeardNoise.cs`        | struct                  | A sound as one NPC heard it: position, loudness along the NavMesh, alarm   | working                                     | M4             |
| `IPerceiver.cs`        | interface               | `Perceived` event and `TryGetLatest`, shared by NPC perception and cameras | working                                     | M1             |
| `FeatureVisibility.cs` | `[Serializable]` struct | Per-slot visibility mask for one observer                                  | working                                     | M7             |

### NPC/Scripts/Perception/

| File                    | Type          | Purpose                                                                                          | Status  | Milestone  |
| ----------------------- | ------------- | ------------------------------------------------------------------------------------------------ | ------- | ---------- |
| `NpcPerception.cs`      | MonoBehaviour | Vision cone, per-point linecasts, quality factors, conduct reading, light from `PlayerLighting`  | working | M1, M2, M4 |
| `NpcPerceptionDebug.cs` | MonoBehaviour | Gizmos, floor cone, per-point rays, on-screen readout                                            | working | M1         |
| `NpcSuspicion.cs`       | MonoBehaviour | Rise from strength × abnormality, delayed decay, alert states with hysteresis, skill multipliers, a baseline decay never goes below (`RaiseBaseline`, thrift shop G3) | working | M2         |
| `NpcHeadLook.cs`        | MonoBehaviour | Animation Rigging head and neck aim; executes `LookAt`/`ClearLook`, the role behaviour decides   | working | M3         |
| `NpcHearing.cs`         | MonoBehaviour | `NoiseBus` subscriber, NavMesh path-length check, raises `Heard`; verdict readout and path gizmo | working | M4         |
| `NpcAnomalySight.cs`    | MonoBehaviour | Checks active anomalies on the perception tick (range, cone, linecast); raises `Noticed`         | working | M5         |

### NPC/Scripts/Guard/

| File                 | Type          | Purpose                                                                                                                             | Status  | Milestone |
| -------------------- | ------------- | ----------------------------------------------------------------------------------------------------------------------------------- | ------- | --------- |
| `GuardBehaviour.cs`  | MonoBehaviour | Activity from alert state, POI selection and claims, last-known position, search, pursuit, catch, body rotation, head-look requests; anomaly errands (door, safe) and the on-edge baseline after an open safe (thrift shop G3) | working | M3        |
| `GuardActivity.cs`   | enum          | Patrolling, Watching, Investigating, Pursuing, Reporting (Reporting unused until M8)                                                | working | M3        |
| `GuardAnimation.cs`  | MonoBehaviour | Guard-only animator parameters: Watching, Searching, GiveUp, Slam (on starting an errand), and idle and give-up variant picks       | working | M3        |
| `PointOfInterest.cs` | MonoBehaviour | Coverage that fills while a guard is near and drains by importance (base × plan multiplier × alarm factor); static registry (decision 15) | working | M3        |
| `PoiSafeAlarm.cs`    | MonoBehaviour | On a POI: raises its alarm factor (1.5) when its `Safe` opens, so guards check the office more often (thrift shop G3)                | working | M5        |
| `SecurityMonitor.cs` | MonoBehaviour | Relays camera observations to the guard posted at it                                                                                | —       | M8        |

### NPC/Scripts/Civilian/

| File                      | Type             | Purpose                                                                                                                                                                       | Status                 | Milestone |
| ------------------------- | ---------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------- | --------- |
| `CivilianBehaviour.cs`    | MonoBehaviour    | Idle routine and carrying out the outcome (Scared, Avoiding); `Activity` also reports the window's Deliberating and Calling; raises `ActivityChanged`                         | working                | M6, Thrift shop C6 |
| `CivilianRandomOutfit.cs` | MonoBehaviour    | Picks a random outfit for the civilian's role at spawn; cosmetic only                                                                                                         | working                | M6        |
| `CivilianSpot.cs`         | MonoBehaviour    | A place where civilians of one role browse or work, with its `CivilianStation` (the dwell clip); static registry and claims; no pick weights                                 | working                | M6        |
| `CivilianStation.cs`      | enum             | Shelf, Rack, Table, Shoes, Register, Bin, Restock, Tidy: the dwell clip at a spot. Order is the Dwell tree's threshold                                                         | working                | Thrift shop C5 |
| `CivilianActivity.cs`     | enum             | Roaming, Working, Deliberating, the reactions (Calling, Scared, Avoiding), `Dozing` for the resident. `PretendingNotToSee` is not built (2026-10-19) and leaves the enum. `Fleeing` removed and `Frozen` renamed `Scared` (2026-10-19) | declared               | M6        |
| `ReactionInput.cs`        | enum             | What a `ReactionBranch` tests: Severity, PlayerDistance. Saved by number in branch assets, so new inputs go at the end                                                         | working                | Thrift shop C6 |
| `ReactionContext.cs`      | struct           | The tree's inputs, gathered once when the window opens; severity is the archetype's abnormality for the conduct seen                                                          | working                | Thrift shop C6 |
| `ResidentBehaviour.cs`    | MonoBehaviour    | The clerk at home: sleep rhythm in the armchair driving the view scale, the snack run to the kitchen; reads `NpcDecisionWindow` for the police call (`Assets/apartment-level.md`) | —                      | Apartment |
| `NpcDecisionWindow.cs`    | MonoBehaviour    | The shared decision window (decision 22): opens on reaching Alerted, fills while the NPC sees the player, drains while not; opening samples the archetype's `ReactionTree` (`PendingOutcome`); full starts a call that fails the job (`PoliceCalled`) or raises `Decided` for any other outcome | working                | Thrift shop C1, C6 |
| `NpcWindowReporter.cs`    | MonoBehaviour    | Reports the open window to `JobAlert` for the HUD ring (replaces `ResidentWindowReporter`)                                                                                    | working                | Thrift shop C1 |
| `CivilianSpawner.cs`      | MonoBehaviour    | Spawns a level's civilians once at load: per group a random count at random free points, role and archetype applied after `Instantiate`; debug seed, gizmos and spot warnings  | working                | Thrift shop C2 |
| `CivilianSpawnGroup.cs`   | Serializable     | One spawn group: prefab, role, archetype, count range and points                                                                                                              | working                | Thrift shop C2 |
| `CivilianRole.cs`         | enum             | Customer, Employee (Manager dropped 2026-10-19)                                                                                                                                             | declared               | M6        |
| `ReactionTree.cs`         | ScriptableObject | Root of a civilian's reaction tree, referenced by the archetype; walks to a leaf and samples it (`TrySample`)                                                                 | working                | Thrift shop C6 |
| `ReactionNode.cs`         | ScriptableObject | Abstract node: `ReactionBranch` tests one input against a threshold, `ReactionLeaf` samples weighted outcomes (`ReactionWeight`) in proportion                               | working                | Thrift shop C6 |

### NPC/Scripts/UI/

| File                           | Type          | Purpose                                                                                                                                                 | Status  | Milestone |
| ------------------------------ | ------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- | ------- | --------- |
| `NpcSuspicionIndicator.cs`     | MonoBehaviour | World-space suspicion telegraph above the NPC: filling arc, state icon, arrow to last-known position; fades with the NPC's floor (decision 26)          | working | M2        |
| `NpcVisionCone.cs`             | MonoBehaviour | Player-facing cone drawn as light: static fan, rays written to `_RayData`, cut and softened in the shader, crouch-safe strip, alert color (decision 21); fades with the NPC's floor and casts nothing while hidden (decision 26) | working | M5        |
| `OffscreenIndicatorHUD.cs`     | MonoBehaviour | Screen-space layer for edge indicators, one per scene; static `Instance`                                                                                | working | M2        |
| `OffscreenIndicatorElement.cs` | MonoBehaviour | Draws one edge-of-screen indicator; placed by `NpcSuspicionIndicator`                                                                                   | working | M2        |

### NPC/Scripts/Animation/ and Core/Animator/

| File                                   | Type          | Purpose                                                                                              | Status  | Milestone |
| -------------------------------------- | ------------- | ---------------------------------------------------------------------------------------------------- | ------- | --------- |
| `Animation/NpcAnimator.cs`             | MonoBehaviour | Feeds `Speed` (agent velocity) and `TurnRate` (yaw change) to every NPC's animator                   | working | M3        |
| `Animation/NpcFacing.cs`               | MonoBehaviour | Turns the body toward a requested facing, else the walking direction; shared by guards and civilians | working | M6        |
| `Animation/NavMeshAgentExtensions.cs`  | static class  | `HasArrived()`: the one arrival check every NPC behaviour uses                                       | working | M6        |
| `Animation/CivilianAnimation.cs`       | MonoBehaviour | Sets `Dwelling` and `Station` from `CivilianBehaviour`, the phone pair from `NpcDecisionWindow`, and fades the Phone layer | working | Thrift shop C5 |
| `Core/Animator/NpcAnimatorParams.cs`   | static class  | Parameter names every NPC shares: `Speed`, `TurnRate`, and the Phone layer's `Deliberating`, `Calling` | working | M3        |
| `Core/Animator/CivilianAnimatorParams.cs` | static class | Civilian-only parameters: `Station` (float), `Dwelling`                                            | working | Thrift shop C5 |
| `Core/Animator/GuardAnimatorParams.cs` | static class  | Guard-only parameters and variant counts                                                             | working | M3        |

### NPC/Scripts/Editor/ and Core/Editor/ (animator builders)

| File                                  | Type            | Purpose                                                                                         | Status  | Milestone |
| ------------------------------------- | --------------- | ----------------------------------------------------------------------------------------------- | ------- | --------- |
| `Editor/GuardLocomotionBuilder.cs`    | static class    | Rebuilds `GuardAnimator.controller` from `GuardClips`: Locomotion, Watching, Searching, GiveUp  | working | M3        |
| `Editor/GuardClips.cs`                | readonly struct | Every guard clip (takes in `NPC.fbx`), grouped into stances. The one place to edit when clips change | working | M3        |
| `Editor/CivilianLocomotionBuilder.cs` | static class    | Rebuilds `CivilianAnimator.controller`: Standing speed tree, Dwell tree over `Station`, Phone layer | working | Thrift shop C5 |
| `Editor/NpcClipImport.cs`             | static class    | Loop and root-baking import settings for the civilian and guard clips in `NPC.fbx`, and the clip loader both builders use; no-op when unchanged | working | Thrift shop C5, G |
| `Core/Editor/AnimatorBuildUtility.cs` | static class    | Shared builder helpers: measure clips, trees, states, interruptible transitions, reset, the shared Phone layer and upper-body mask | working | M3        |
| `Core/Editor/ClipSet.cs`              | readonly struct | One stance: interchangeable clips and the parameter that picks between them                     | working | M3        |
| `Core/Editor/Gait.cs`                 | readonly struct | One moving speed: a clip and its speed measured from root motion                                | working | M3        |
| `Core/Editor/TurnSet.cs`              | readonly struct | Left and right turn clips and their measured turn rates                                         | working | M3        |

### NPC/Scripts/Security/

| File                  | Type          | Purpose                                                                                                                  | Status  | Milestone   |
| --------------------- | ------------- | ------------------------------------------------------------------------------------------------------------------------ | ------- | ----------- |
| `ActionModel.cs`      | class         | Stage A: transition chain over room changes, entrance Dirichlet, predicted occupancy per room, from an `ActionRecord`   | working | Thrift shop |
| `SecurityPlanner.cs`  | static class  | Stage B: greedy coverage over candidate POIs for a given `SecurityTier`, drain multipliers, explanation lines             | working | Thrift shop |
| `SecurityCandidate.cs`| struct        | Authoring side of a candidate: POI, covered `Zone`s and `Entrance`s as scene references                                   | working | Thrift shop |
| `PoiCandidate.cs`     | class         | One candidate as the planner sees it: name, covered rooms and entrances                                                   | working | Thrift shop |
| `SecurityPick.cs`     | struct        | One chosen candidate: index, gain, decay multiplier, explanation                                                          | working | Thrift shop |
| `SecurityPlan.cs`     | class         | What the planner hands the level: guard count, picks in order and every candidate's standalone gain (the `SecurityPlan` seam in `roadmap.md`) | working | Thrift shop |
| `SecurityDebugOverlay.cs` | MonoBehaviour | Developer panel toggled by F3: entrance bars, occupancy, candidate gains and picks, explanation lines, transition heatmap, world labels on picked POIs | working | Thrift shop |
| `SecurityDirector.cs` | MonoBehaviour | Builds the plan at load, spawns guards at random free spawns, activates candidate POIs                                    | working | Thrift shop |
| `SecurityLayout.cs`   | MonoBehaviour | Guard spawns, candidate POIs and the level's tiers, with validation and gizmos                                            | working | Thrift shop |

### NPC/Scripts/Witness/

| File                 | Type             | Purpose                                                                                                     | Status                                      | Milestone |
| -------------------- | ---------------- | ----------------------------------------------------------------------------------------------------------- | ------------------------------------------- | --------- |
| `NpcWitness.cs`      | MonoBehaviour    | Exposure in M6 (time seen × quality); beliefs, `f_duration` and memory decay in M7                          | working (beliefs, confusion, decay, report) | M6, M7    |
| `FeatureBelief.cs`   | class            | Log-odds belief over one property of one slot (style or color)                                              | working                                     | M7        |
| `WitnessReport.cs`   | class            | One description: per-slot style and color beliefs; a witness's memory and its job-end copy                  | working                                     | M7        |
| `JobWitnesses.cs`    | static class     | The job's witnesses; merges their reports into the police description at job end                            | working                                     | M7        |
| `SuspectSketch.cs`   | MonoBehaviour    | The witness sketch: dresses the booth's mannequin from the description, renders it once, writes the caption | working                                     | M7        |
| `ConfusionMatrix.cs` | ScriptableObject | Wrong-color table, one asset (`ColorConfusion`) shared by every slot                                        | working                                     | M7        |

### Core/Scripts/

| File                        | Type             | Purpose                                                                                                                                                                                                                     | Status  | Milestone       |
| --------------------------- | ---------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------- | --------------- |
| `Animation/Dangle.cs`       | MonoBehaviour    | Procedural pendulum for small hanging accessories (the guard's keys). No physics; runs after the animated pose                                                                                                              | working | M3              |
| `Noise/NoiseEvent.cs`       | struct           | Position, radius (loudness), alarm                                                                                                                                                                                          | working | M4              |
| `Noise/NoiseBus.cs`         | static class     | Publish and subscribe                                                                                                                                                                                                       | working | M4              |
| `Volumes/ZoneType.cs`       | enum             | Public, Staff. Public first, so a forgotten zone is harmless                                                                                                                                                                | working | M2              |
| `Volumes/Zone.cs`           | MonoBehaviour    | Trigger volume on the `Volume` layer, public or staff; several colliders make complex shapes. Its room number is handed out by `JobLayout` (decision 24)                                                                    | working | M2, Thrift shop |
| `Volumes/Entrance.cs`       | MonoBehaviour    | Doorway from the street: counts inbound crossings (street side in, room side out), opens into a `Zone`; a child of `Door.prefab`, inactive unless exterior                                                                  | working | Thrift shop     |
| `Volumes/InteriorReveal.cs` | MonoBehaviour    | Fades a building's roof or upper floor through the OccluderWall shader while the player is inside                                                                                                                           | working | Apartment       |
| `Volumes/BuildingFloors.cs` | MonoBehaviour    | One per level: the continuous view level from floor volumes, stairs and outside; fades the building per floor (`belowHide`, `fullHideArea`, wall tops), the prop roots and registered `FloorHideable`s (decision 26)          | working | Jewelry store   |
| `Volumes/BuildingFloor.cs`  | `[Serializable]` struct | One floor's data: root, wall tops, height, `belowHide`, `fullHideArea`                                                                                                                                                 | working | Jewelry store   |
| `Volumes/FloorVolume.cs`    | MonoBehaviour    | Trigger on the `Volume` layer for the space one floor fills; reports the player entering and leaving                                                                                                                        | working | Jewelry store   |
| `Volumes/FloorStairs.cs`    | MonoBehaviour    | Trigger on a flight of stairs; while the player is on it, their height drives the view level                                                                                                                               | working | Jewelry store   |
| `Volumes/FloorHideable.cs`  | MonoBehaviour    | On NPC prefabs: fades the NPC with its floor; floor from height on a slow tick; off in levels without `BuildingFloors`                                                                                                      | working | Jewelry store   |
| `Rendering/RendererFade.cs` | class            | Fades one renderer: `_Hide` on OccluderWall submeshes, alpha on transparent glass; shadows only or not drawn when fully hidden                                                                                              | working | Jewelry store   |
| `Job/JobLayout.cs`          | MonoBehaviour    | The level's rooms (`Zone`s) and entrances in index order; numbers them in `Awake`; checks the scene against the lists                                                                                                       | working | Thrift shop     |
| `Job/JobLog.cs`             | static class     | This run's room transitions and inbound crossings (decision 24); begun and ended by the owning `JobLayout`                 | working | Thrift shop |
| `Job/JobLogReadout.cs`      | MonoBehaviour    | On-screen readout of the job log by room and entrance name; rebuilt only when the log records                             | working | Thrift shop |
| `Actions/ActionRecord.cs` | `[Serializable]` class | One location's decayed counts, room and entrance names, completed runs; `For`, `Fits`, `AddRun`               | working | Thrift shop |
| `Actions/ActionSave.cs`  | `[Serializable]` class | Root of `actions.json`: one record per location                                                                 | working | Thrift shop |
| `Actions/ActionStore.cs` | static class     | Loads and saves `actions.json`; a missing or broken file loads empty                                                     | working | Thrift shop |
| `Actions/ActionRecorder.cs` | MonoBehaviour | On `Job`: on `Completed`, decays this location's record and adds the run; replaces a record that no longer fits the layout | working | Thrift shop |
| `Actions/SecurityTier.cs` | `[Serializable]` struct | One row of a level's budget: guards and adaptive POIs from a completed-run count; `For` picks the row | working | Thrift shop |
| `Actions/SecurityBudget.cs` | ScriptableObject | A level's tiers as one shared asset: `TierFor`, `IsRaised`, validation of its own data. Read by `SecurityLayout` and the main menu | working | Thrift shop |
| `Lighting/GameplayLight.cs` | MonoBehaviour    | Marks a Point or Spot light as one gameplay counts; static registry and unshadowed contribution at a point (decision 17)                                                                                                    | working | M4              |
| `World/Anomaly.cs`          | MonoBehaviour    | World state only a robber causes; static registry, active flag, point to see, kind (decision 18)                                                                                                                            | working | M5              |
| `World/AnomalyKind.cs`      | enum             | OpenDoor, SafeOpen (raised by `Safe`, thrift shop G3); PowerOut joins with the breaker. A kind is added only with its producer                                                                                                       | working | M5              |
| `World/Door.cs`             | MonoBehaviour    | Door with a lock: carves the NavMesh while closed, raises an anomaly when the player opens it, slams when an NPC resolves that anomaly; guard passage (`OpenForPassage`, `InReach`; decision 19, thrift shop T2b); optional second leaf for double doors. Lives in `Core/Interactables/` | working | M5              |
| `World/Breaker.cs`          | MonoBehaviour    | Switches a set of lamps; an anomaly while tripped; reset by staff                                                                                                                                                           | —       | M5              |
| `World/Device.cs`           | MonoBehaviour    | In-world object an ability is used at; names its `Ability`, exposes `Activate()`, raises `Activated` (decision 20)                                                                                                          | —       | M5              |
| `World/Ability.cs`          | ScriptableObject | Data for one bought ability: name, uses per heist, description. No runtime state                                                                                                                                            | —       | M5              |

### Player/Scripts/ (what NPCs read)

| File                                  | Type           | Purpose                                                                                                                            | Status   | Milestone   |
| ------------------------------------- | -------------- | ---------------------------------------------------------------------------------------------------------------------------------- | -------- | ----------- |
| `Perception/BodyRegion.cs`            | enum           | Head, UpperBody, Hands, LowerBody, Feet. No explicit values: the per-region arrays on `NpcPerception` index by `(int)region`                           | working  | M1          |
| `Perception/PerceptionPoint.cs`       | MonoBehaviour  | One body point: its region, and its collider's bounds centre                                                                       | working  | M1          |
| `Perception/PerceptionTargets.cs`     | MonoBehaviour  | Every body point, flat and by region. Fixed after `Awake`                                                                          | working  | M1          |
| `Perception/CharacterAppearance.cs`   | struct         | The player's appearance: style and color per slot (Head, Chest, Legs, Feet), read from `PlayerCustomization`                       | declared | M7          |
| `Perception/FeatureSlot.cs`           | enum           | The outfit parts a witness can describe: Head, Chest, Legs, Feet (decision 23). Moved from NPC so `CharacterAppearance` can use it | declared | M7          |
| `Editor/MapPlayerPerceptionPoints.cs` | static class   | Adds `PerceptionPoint` to the 17 bone colliders in `Player.prefab` by bone name                                                    | working  | M1          |
| `Perception/ConductFlags.cs`          | `[Flags]` enum | Sprinting, InStaffArea, Lockpicking, Stealing. Powers of two; a flag is added only with its producer                               | working  | M2, M5      |
| `Perception/PlayerConduct.cs`         | MonoBehaviour  | What the player is doing right now, including the staff-area flag from zone triggers, counted per zone                             | working  | M2          |
| `Perception/PlayerLighting.cs`        | MonoBehaviour  | Light level at the player, sampled from every `GameplayLight` on a tick with shadow rays (decision 17)                             | working  | M4          |
| `Abilities/PlayerAbilities.cs`        | MonoBehaviour  | Remaining uses per owned ability for this heist; checks and spends a charge, then calls `Device.Activate()`                        | —        | M5          |
| `Perception/PlayerLightingDebug.cs`   | MonoBehaviour  | On-screen light readout and a level-tinted sphere at the sample point; computes nothing itself                                     | working  | M4          |
| `Job/PlayerRoom.cs`                   | MonoBehaviour  | The room the player is in, with 0.5 s hysteresis; reports each settled change to `JobLog`. Not read by NPCs                 | working  | Thrift shop |
| `Locomotion/PlayerStance.cs`          | MonoBehaviour  | Owns `Stance`: Standing and Crouching only                                                                                         | working  | done        |
| `Noise/PlayerNoise.cs`                | MonoBehaviour  | A footstep on `NoiseBus` every stride; louder and alarming when sprinting, quieter when crouching                                  | working  | M4          |

### Prefab composition

| Prefab   | Components                                                                                                                                                                                                                                                                           |
| -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Guard    | `NpcPerception`, `NpcPerceptionDebug`, `NpcSuspicion`, `NpcHeadLook`, `GuardBehaviour`, `GuardAnimation`, `NpcAnimator`, `NpcHearing`, `NpcAnomalySight`, `NpcFacing`, `NpcWitness`, `FloorHideable`, `NavMeshAgent`; the model has an Animator and a `RigBuilder` with head and neck aim constraints |
| Civilian | `NpcPerception`, `NpcPerceptionDebug`, `NpcSuspicion`, `NpcHearing`, `NpcAnomalySight`, `NpcFacing`, `NpcWitness`, `CivilianBehaviour`, `CivilianRandomOutfit`, `FloorHideable`, `NavMeshAgent`, the suspicion indicator; no vision cone (decision 22)                                                |
| Clerk    | Same as Civilian with the `TiredClerk` archetype and no patrol                                                                                                                                                                                                                       |
| Camera   | `NpcPerception`, `NpcWitness` — nothing else                                                                                                                                                                                                                                         |
| Monitor  | `SecurityMonitor`, linked to cameras; a guard is posted at it                                                                                                                                                                                                                        |

---

## Milestones

Ordered by dependency. **Perception comes before behaviour.** A guard state machine with no perception feeding it is driven by nothing and cannot be tested, however tempting it is to start there because it is "the AI".

**How this maps to `roadmap.md`.** The milestones interleave Phases 2 and 3: suspicion (M2, Phase 3) comes before hearing and light (M4, Phase 2), because it only needs M1 and it is what makes the guard in M3 testable. Phase 2 is not a hard gate, so this is allowed.

**Prototype jobs use a guard.** Phases 3 and 4 build and test the loop with a guard, but the first tier's only opposition is civilians, whose behaviour arrives in Phase 5 (M6). So the prototype job runs on the test bench with a guard, and tier 1 content is built after M6. This is a conscious choice, not an accident of ordering.

### M0 — Groundwork

- [x] Scaffolding committed (`8f8bd04`)
- [x] Git index restored after the accidental `git rm -r --cached .`
- [x] `NoiseBus` and `NoiseEvent` moved from `NPC/Scripts/Noise/` to `Core/Scripts/Noise/` (decision 4)
- [ ] Assembly definitions: Core, Player, Player.Editor, NPC, with the dependency direction from decision 4 (decision 12). Started: `SuspectUnknown.Editor` holds `MenuPaths`.
- [ ] Add `.DS_Store` to `.gitignore`, and untrack the 4 already committed with `git rm --cached` on those paths only
- [ ] Review `.gitattributes`: it has a single `filter=lfs` line for a repo that already holds FBX, `.blend`, and PNG files
- [ ] Confirm the Phase 1 gate. The dither-fade occluder shader exists; wall-flatten is deferred and no longer part of the done-when. Perception work assumes the player can judge sightlines.
- [x] Move sprinting out of `Stance`: it is now `PlayerMotor.IsSprinting`, and `PlayerNoise` reads it (decision 3)
- [x] Test bench in `Assets/Testing/TestingScene.unity`: a floating table slab on `Occluder` (0.7–1.1 m, so the feet stay visible underneath) and one stationary guard with its root at floor level. No NavMesh.
- [x] Physics layers: `Occluder`, `Player`, `NPC`, and `Volume` (zones; collides with `Player` only). Walls and occluders go on `Occluder`; the player's bone colliders are on `Player`; the floor is on neither.

**Done when:** the test bench reproduces the Phase 2 scenario, even though nothing detects anything yet. Build the scenario before the code that passes it.

A solid counter cannot pass the Phase 2 done-when as written: it hides the feet whether the player stands or crouches. The occluder needs a gap underneath.

### M1 — Perception core (Phase 2 done-when)

1. [x] `PerceptionTargets` — every body point on the player rig, grouped by region. `BodyRegion`, `PerceptionPoint`, and the `MapPlayerPerceptionPoints` editor tool came with it.
2. [x] `NpcArchetype` — the vision block: view distance, FOV, eye height, distance and angle falloff curves, crouch factor, region weights, tick rate. Data before logic, so step 4 is tunable from the inspector from the first frame.
3. [x] `Observation` and `IPerceiver` — who perceived, when, the player's position, visibility, the region fractions, and each quality factor. The appearance field is reserved with a placeholder `CharacterAppearance` struct, even though nothing fills it until M7.
4. [x] `NpcPerception` **and** `NpcPerceptionDebug`, in the same sitting rather than one after the other. A perception system you cannot see is one you cannot tune.

Implementation notes, as built:

- Check cheap to expensive and bail at the first failure: range → FOV angle → `Physics.Linecast(eye, point, occluderMask, QueryTriggerInteraction.Ignore)` per body point. Triggers are ignored so `Zone` volumes never block sight.
- The FOV check flattens both the NPC's forward and the direction to the player onto the horizontal plane. Without that, a player standing close in front reads as outside the cone, because the player's root is at their feet and the eye is 1.65 m up.
- Cast to every point in each attended region (decision 13). Visibility is the weighted average of region fractions. That _is_ the partial detection Phase 2 asks for.
- Tick at the archetype's tick rate (10 Hz for guards), staggered in eight buckets by a static spawn counter. `GetInstanceID()` is not used: its `int` conversion is deprecated in current Unity.
- Keep each quality factor (`fDistance`, `fAngle`, `fLight`, `fStance`) as its own readable value rather than pre-multiplying, so the readout can print them individually. `fLight` stayed at 1.0 until M4 (decision 17). `f_duration` is not here; it belongs to the witness (decision 7).
- `IPerceiver` exposes results only: the `Perceived` event on every tick the player is seen, and `TryGetLatest` for the last tick. Consumers never trigger a cast.
- The debug view never casts a ray of its own. Rays are colored per point (clear, blocked, or grey for an ignored region) and the readout shows each region's fraction, colored from red to green.

**Done when:** crouching behind the half-height occluder reliably breaks the ray to the head while the ray to the feet still lands, and the partial detection is visible in the debug readout.

**Status:** passed on the test bench, first with one cast point per region and again after the region weights refactor: the guard in the open shows all regions at 100 %, crouching behind the slab shows head 0 % and feet 100 %, and the `Civilian` archetype shows its feet rays grey and `ignored`. **M1 is done.**

### M2 — Conduct, suspicion, and the alert ladder (Phase 3)

- [x] `PlayerConduct` with the conduct that exists so far (sprinting; loot and tools as Phase 3 adds them).
- [x] `Zone` in Core, on a new `Volume` layer that collides with `Player` only, and an L-shaped staff zone on the test bench.
- [x] `NpcPerception` reads conduct into each `Observation`, and computes abnormality using the archetype's conduct weights (decision 6).
- [x] `NpcArchetype` suspicion block: conduct weights, rise rate, decay rate, decay delay, one threshold per alert state, and hysteresis.
- [x] `NpcSuspicion` rises by `strength × abnormality × rise rate`, where strength is visibility times the quality factors. It decays after a delay when nothing abnormal is in view, including while the player is seen behaving normally, and raises `AlertStateChanged(from, to)` on each change. A spike can skip states.
- [x] Use a lower threshold on the way down than on the way up, so an NPC hovering at a boundary does not flicker between states.
- [x] Leave hooks for skills: rise and decay multipliers that something else can set later (concept 7.4 Ghost, "slower suspicion accumulation in shadow"; Face, "talk down a Curious NPC"). No skill logic here.
- [x] A suspicion indicator above the NPC (concept 6.5, in-world telegraphing). As built: a world-space canvas that copies the camera rotation, hidden at zero so a calm room shows nothing. A filling arc shows suspicion, and an icon shows the state (`?`, `?!`, `!`). The arc rolls in the screen plane to point at the **last-known** position from the latest observation, not the live player, so it never leaks where the player really is, and it shows where M3's guard will investigate. Displayed values are smoothed, since perception ticks at 10 Hz. When the NPC is off screen, an edge indicator on a screen-space HUD takes over: it sits on the screen border on the line toward the NPC, points at it, and shows the same icon, color, fill, and fade, so the player knows someone off screen is suspicious.

**Done when:** walking normally in view of a guard raises nothing; sprinting, or walking into a staff zone, fills suspicion over time rather than instantly; breaking line of sight lets it drain; and the current state is readable above the NPC. Per the Phase 3 gate check: if there is no tension, lengthen Curious before doing anything else.

**Status:** passed on the test bench with the guard archetype, including the Phase 3 tension check. **M2 is done.**

### M3 — The guard (Phases 2–3)

- [x] NavMeshSurface bake on the test bench, collecting physics colliders and excluding the `Player`, `NPC`, `Volume`, and `UI` layers. A `NavMeshAgent` on the guard.
- [x] `PointOfInterest` (decision 15): radius, fill rate, base decay rate, dwell time, runtime coverage, decay multiplier, and claim. Gizmos show radius, coverage, and the facing the guard adopts while dwelling. Three POIs on the test bench, one of them important.
- [x] `GuardBehaviour` maps alert state to `GuardActivity` every frame: Idle → Patrolling (serving POIs); Curious → Watching (stop and turn toward the last-known position); Investigating → walk to the last-known position and search; Alerted → Pursuing. Reporting waits for M8, when there is someone to report to. Giving up is just Idle again: the guard pauses for the give-up clip, then picks the next POI.
- [x] `Watching` is added to `GuardActivity`, and `ReturningToPost` removed.
- [x] Guard tuning (speeds, catch radius, search sweep, distance scale, sight grace) lives on `GuardBehaviour`, since only guards use it. It moves to a `GuardProfile` ScriptableObject if a second guard type ever differs.
- [x] The pursue speed stays below the player's sprint, so escape is possible.
- [x] Catching raises a `Caught` event, and needs sight on the tick it happens. `ReloadOnCaught` (test bench only) reloads the scene; the Phase 3 fail state hooks into the same event later.
- [x] Behaviour before animation: the guard slid until its logic was right, then got a code-built animator controller (decision 16).
- [x] `NpcHeadLook` via Animation Rigging, so the guard visibly turns its head before its body. Turning toward noise follows in M4.
- [x] Investigation is interruptible: alert state drives the activity every frame, so there is no scripted sequence to finish.
- [x] Being caught ends the job (the Phase 3 basic fail state). Being _seen_ does not (concept 9: detection must be recoverable).

Implementation notes, as built:

- **Rotation is the guard's, not the agent's.** `agent.updateRotation` is off. Each activity asks for a facing (last-known position, POI forward, search sweep); otherwise the guard faces the agent's `desiredVelocity` above 0.25 m/s. One `SmoothDampAngle` applies it, capped at the turn speed. The head smooths faster than the body, so it leads.
- **Sight is debounced for behaviour, raw for catching.** A sighting counts as lost only after 0.3 s without one, so the cone's edge does not flicker the guard between chasing and searching. The catch still needs sight on the current tick.
- **Pursuit re-paths on every observation** and turns auto braking off while chasing, so the guard does not slow down near a moving target.
- **The last-known position** is the same point the M2 indicator's arrow shows, so what the player sees and where the guard goes always agree.
- **The keys on the guard's belt** use `Dangle`, a procedural pendulum, rather than physics joints, which would have needed runtime detaching and animation-physics sync.
- **The debug label** above each guard shows its activity, target POI, and whether it is dwelling or searching.

**Done when:** a guard patrols its POIs, visiting the important one more often, notices you doing something abnormal, pauses, walks to where you were, and gives up and resumes patrolling if you are gone — or catches you if you are not.

**Status:** passed on the test bench with animation, including planted feet at every speed and the Phase 3 tension check. **M3 is done.**

### M4 — Hearing and light (the rest of Phase 2)

- [x] `NoiseEvent` and `NoiseBus` (in Core). A `NoiseEvent` carries position, radius, and alarm (0 harmless, 1 unmistakable), so ordinary footsteps can be heard without raising anything.
- [x] `NpcHearing`: `NavMesh.CalculatePath` length compared against `radius × sensitivity` (decision 10). `NpcArchetype` gains hearing sensitivity and hearing suspicion.
- [x] `PlayerNoise` migrates from resizing a `SphereCollider` to publishing a footstep on `NoiseBus` every stride. Only sprinting carries alarm.
- [x] `NpcHeadLook` turns toward heard noise: every audible sound earns the guard a short glance.
- [x] Light from the scene's lights (decision 17). `GameplayLight` in Core, `PlayerLighting` samples it, and `NpcPerception` turns the level into `fLight` through `NpcArchetype.LightFactor`. `PlayerLightingDebug` shows the level, the raw sum, and lit and shadowed light counts, with a sphere at the sample point tinted by level. Putting `GameplayLight` on the right lamps is level design. `LightVolume` was replaced before any level used it.

Implementation notes for hearing, as built:

- `NpcHearing` rejects by straight-line distance first (a path is never shorter), snaps the sound onto the NavMesh within 1 m, then measures the path with a reused `NavMeshPath` and a fixed corner buffer. It raises `Heard` with a `HeardNoise`: loudness is 1 at the source and 0 at the edge of this NPC's reach.
- A sound sets a suspicion **floor** of `alarm × loudness × hearingSuspicion` rather than adding to suspicion, so repeated footsteps don't stack, and it resets the decay delay like a sighting.
- A sound becomes the stimulus and the guard's last-known position only while the player is out of sight, and only if it is alarming. Ordinary footsteps earn a glance but never move the last-known position, or a watching guard tracks a quiet player through walls step by step (changed 2026-10-08; it used to be "or the NPC is already alert").
- **A sound alone never alerts.** Every archetype keeps `hearingSuspicion` below `alertedAt`, so the most a sound can do is make an NPC investigate. This is held in data rather than code.
- The reaction radius for a state threshold `T` is `reach × (1 − T / (alarm × hearingSuspicion))`, with `reach = radius × sensitivity`. Use it to tune: the guard turns Curious at a sprint within about 1.3 m, and investigates a reference loud sound (radius 12, alarm 1) within about 10 m.
- The debug readout shows the last verdict (heard, too far, no path, off NavMesh) with loudness or path length against reach; the hearing gizmo draws the measured path.

Implementation notes for light, as built:

- `GameplayLight.Contribution` checks cheap to expensive: light disabled, then range by `sqrMagnitude`, then falloff, then the spot cone by comparing cosines. It returns raw Unity intensity units; only `PlayerLighting` normalises.
- A spot light with equal inner and outer angles is a hard edge, not zero: `Mathf.InverseLerp` returns 0 when its ends are equal, so that case is handled separately.
- `PlayerLighting` runs the shadow ray only after a light passes the cheap contribution check, so out-of-range lights cost no raycast. The gizmo calls the same `IsShadowed`, so the debug view cannot disagree with gameplay.
- A missing `PlayerLighting` warns and reads as full light rather than disabling the NPC: full light is a safe default, unlike a missing stance.

**Done when:** running behind a wall is heard around the corner but not through it, and stepping into shadow visibly lowers `f_light` in the readout.

**Status:** passed on the test bench. Sprinting around a wall's end is heard, through a wall with no path it is not, and a sprint close behind the guard makes it Curious. Shadow lowers `fLight`, a wall between lamp and player blocks it, and switching a lamp off drops `fLight` within a tick. **M4 is done.**

### M5 — Verbs move NPCs (Phase 4, hard gate)

Redesigned 2026-10-05 (decisions 18–21): there is no physical throwing and no jammed door. NPCs never use doors, verbs are abilities used at in-world devices, and the vision cone becomes a player-facing light. The interaction system is player-side work that does not exist yet, so devices and doors are triggered with `[ContextMenu]` placeholders that call the same methods the interaction will. No level exists yet; everything is tested on the test bench.

1. [x] **Vision cone overlay** (decision 21). `NpcVisionCone`: eye-height ray fan cut by walls, crouch-safe shading behind low cover, color by alert state, matching the archetype's range and angle. Readable without the debug view. Built in two passes: first a mesh cut per frame, then reworked so the mesh is static and the shader cuts it from a ray texture, which removed the per-frame mesh work and gave soft, straight edges. Passed on the test bench; corner flicker remains as a known issue in decision 21.
   - [x] **CPU pass.** Profiled with 8 guards in the editor, using the `NpcVisionCone.Rays` and `NpcVisionCone.Upload` profiler markers, which stay in the code. Baseline: 0.91 ms per frame for all 8 cones (0.86 ms rays, of which 0.4 ms was the script's own work rather than physics; 0.03 ms texture upload). Two changes: cones off screen cast no rays (`isVisible`), and ray directions are built once in `Start` and rotated by one quaternion per frame, instead of `Quaternion.Euler` and `TransformDirection` per ray. Result: 0.45–0.50 ms (rays 0.30–0.45 ms, upload 0.01–0.02 ms), roughly half. Batching the rays with `RaycastCommand` was not needed; revisit only if a full level with real NPC counts shows otherwise.
2. [ ] **Anomalies and doors** (decisions 18, 19). Full design and steps for 2.3–2.5 in `Assets/doors-and-lockpicking.md`.
   - [x] 2.1 `Anomaly` and `AnomalyKind` in Core: a registry of raised anomalies only, so the usual case (nothing wrong) costs NPCs nothing.
   - [x] 2.2 `NpcAnomalySight` on NPCs checks raised anomalies on the perception tick through `NpcPerception.InViewCone`. The archetype gains anomaly suspicion and per-kind weights (`AnomalyWeights`); a noticed anomaly sets a suspicion floor and becomes the stimulus and the guard's last-known position. The guard only moves to a position it actually knows.
   - [x] 2.3 A `Door` with a lock level, reach, interaction, and the lockpicking minigame. Built for the apartment (`Apartment House Door`); ticked 2026-10-17.
   - [x] 2.4 The guard's errand: claim the open door, walk over, look confused, slam it. Built as thrift shop step G2; ticked 2026-10-21.
   - [x] 2.5 `Lockpicking` conduct when the player is seen picking a lock. Built as apartment step A3: `PlayerConduct` reads `PlayerLockpicking.IsPicking` (since 2026-10-20 `PlayerMinigame.Conduct`, see `MinigameRefactor.md`).
3. [ ] **Breaker** (decisions 17, 18). Trips a set of lamps: `f_light` drops for everyone looking into the zone, and the tripped breaker is an anomaly. Staff notice the dark, walk to the breaker, and reset it. Breaker lamps must be Mixed or Realtime.
4. [ ] **Abilities at devices** (decision 20). `Ability` and `Device` in Core, `PlayerAbilities` on the player with uses per heist, and the errand mechanism on the guard. One ability that acts on the guard, chosen from the ideas list, proves it end to end.
       Optional abilities, built after the first one proves the system. Each is a device plus an errand (decision 20):
   - [ ] **Call the guard's mum** (desk phone). The guard walks to the phone and is stuck in the call, facing the wall. Guard only.
   - [ ] **Page the guard** (store PA microphone). "Security to the loading dock, your car is being towed" sends the guard to the far end. Guard only.
   - [ ] **Leak the group chat** (router, staff Wi-Fi). The guard stops and stares at his phone, head down, and his cone shrinks while he scrolls. Guard only. Needs one extra errand field: a perception modifier while busy, applied to the archetype's view distance and angle and shown by the cone (decision 21).
   - [ ] **Clog the toilet** (staff restroom). The clerk runs over with a plunger and stays to fix it. Needs staff NPCs, so it lands with M6.
   - [ ] **Car alarm** (key fob on a parked car). Staff and customers turn to the window and face away from the shop floor. Changes facing, not position. Needs civilians, so it lands with M6.

5. [ ] **Pursuit across vaults.** Guards follow over counters via `NavMeshLink`, first with built-in traversal, then with a vault clip in the guard's animator builder (decision 16). An opened door also opens a pursuit route (decision 19).
6. [ ] **Near-miss pass.** Suspicion decays, investigation and errands are interruptible, the player always has a recovery move, and Curious lasts long enough to react to.

**Done when:** opening a door, tripping the breaker, and one device ability each visibly move an NPC or change what it perceives, and an outsider reads the guards from their cones and indicators alone. The overall Phase 4 gate is that someone outside the team plays three jobs and wants a fourth; if the loop is flat here, fix it here rather than moving on to civilians.

### M6 — Civilians (Phase 5)

**Started before the M5 gate, deliberately (2026-10-09).** M5 is not finished: doors (2.3–2.5, written up in `Assets/doors-and-lockpicking.md`), the breaker, abilities, vault pursuit and the near-miss pass remain, and the Phase 4 gate has not been played. Civilians are built in parallel so both team members have independent work. The gate still applies: if the loop turns out flat when M5 is done, fixing it comes before finishing M6.

Civilians follow decision 22: they witness all the time and alarm only on criminal acts.

**Moved into the thrift shop (2026-10-19).** The thrift shop is the first full level, so everything below after step 1 is built and played there, as Track C steps C3–C9 in `Assets/thriftshop-level.md` (section 2.4 describes the design). The shared decision window (`NpcDecisionWindow`) and the spawner are already built (C1, C2).

1. [x] **A civilian that exists.** Body rotation moves out of `GuardBehaviour` into a shared `NpcFacing`. `CivilianSpot` marks where civilians browse or work (not POIs: those are security coverage for Phase 8). `CivilianBehaviour` runs the idle routine: pick a free spot for the role, walk there, stay, repeat. The tired clerk has one register spot with a long dwell. Until the decision window exists, Curious means a glance while carrying on and Investigating or above means stopping to look. The prefab has the shared perception components and no vision cone. Customers get a random outfit for their role (`CivilianRandomOutfit`), so staff read as staff. Passed on the test bench.
2. [x] **Witness slice.** `NpcWitness` accumulates exposure per NPC from each `Observation`: time seen, weighted by observation quality. A debug readout per civilian. M7 builds the full model on top. Built on 2026-10-09 (`NpcWitness` on guards and civilians, readout line in `NpcPerceptionDebug`); checked 2026-10-22 as thrift shop C3.
3. ~~**Door chores.**~~ Dropped 2026-10-21: the clerk does not close doors. Only guards deal with an open door (decision 18).

Then the reactions:

- `ReactionTree` and `ReactionNode` as ScriptableObjects, with `ReactionNode` split into branch and leaf subclasses, so archetypes are data rather than code.
- Tree inputs from concept 6.8, in priority order: **severity** (from the conduct reading in decision 6; the criminal acts in decision 22), role, whether they think the player saw them, and distance to the player. (Personality dropped 2026-10-21; social proof went with contagion and distance to an exit with fleeing, both 2026-10-19.)
- Coarse branching in the tree, weighted scores at the leaves, then sample softly instead of taking the top result.
- The decision window as the `Deliberating` state: a pending reaction, a timer, an indicator showing which way the civilian is leaning, and body language (reaching for a phone, backing away, freezing) as the primary signal.
- Three outcomes — call, scared, walk away. The player's counter-action is the window itself: get out of sight before it fills (decided 2026-10-19). There is no verb that stops a call, and pretending not to see is not built.
- Panic is individual (decided 2026-10-19): no contagion between civilians and no panic noise for guards.
- The clerk's own tree. (Customer personalities telegraphed by idles dropped 2026-10-21: role tells the player who is dangerous.)

**Done when:** a player can look at a room and correctly guess which civilian is the dangerous one.

### M7 — The witness model (Phase 6)

First built in the apartment level as Track W (`Assets/apartment-level.md` 3.6), with the clerk as the only witness. Appearance only (decision 23).

- `FeatureSlot` (Head, Chest, Legs, Feet) and `FeatureVisibility` filled in, and `CharacterAppearance` on the player: each slot's style and color, read from `PlayerCustomization`. `NpcArchetype` gains its witness block: observer skill, feature mask, and **memory retention** (the clerk forgets fast, the guard retains longer, the camera never forgets — concept 6.2).
- `NpcWitness` consumes each `Observation` into `FeatureBelief`s per slot, one over styles and one over colors, combined in log-odds. It accumulates `f_duration` per encounter and applies memory decay between encounters.
- Quality is the full product from concept 6.2: `f_dist · f_angle · f_light · f_duration · f_stance · observerSkill · featureVisibility`.
- The region fractions on `Observation` decide which slots were seen and how well: feet blocked means lower-body features go unobserved, and a half-hidden head teaches half as much as a clear one. This is what `f_stance` means in practice, and it is also why a ceiling camera sees headwear well and footwear badly.
- No Behaviour or Method slot: what the player does goes to the job log and adaptive security (decision 23).
- One `ConfusionMatrix` for colors, shared by every slot (a color is mistaken the same way on any garment); a wrong style is any other style, since styles have no similar neighbours. Below a quality threshold the value taken in is sometimes a mistake, so a red jacket gets described as purple. The chance of a mistake is capped so the truth stays the most likely recall: a poor witness is vague, not consistently wrong.
- `WitnessReport` at job end, with the log-odds merge across witnesses.
- Reactions feed evidence differently: a calm witness who never reaches Alerted keeps a long, clean look at whatever abnormal conduct they watch (only that teaches anything, decision 22); a scared one covers their face and gathers nothing while scared.
- A hook for a player-side confidence multiplier (concept 7.4 Face: "witnesses observe you at reduced confidence"). No skill logic here.
- Unit tests for the merge using the Unity Test Framework. It is pure logic and painful to debug through gameplay.

**Done when:** a player can predict who saw them well and who did not, purely from how the encounter played out.

### M8 — Cameras, trained guards, and security placement (Phases 8 and 10)

- The `SecurityCamera` prefab: `NpcPerception` and `NpcWitness` only. Visual-only feature mask, a mounted height, a fixed arc, no memory decay.
- A 3D view cone for cameras. `NpcPerception` flattens forward for the FOV check, so camera pitch is ignored. A camera pointing straight down has a zero flattened forward, `Vector3.Angle` returns 0, and the camera sees all 360°. Until then, mount test cameras at an angle.
- `SecurityMonitor`: relays its cameras' observations to the guard posted at it, whose `NpcSuspicion` acts on them (decision 11).
- Guards register behavioural features that civilians never notice.
- POIs, camera mounts, and the monitor post expose which `Zone`s they cover, so the Phase 8 placement optimiser can treat them as candidates in the `SecurityPlan`. For POIs the optimiser sets presence and `DecayMultiplier` (decision 15). The POI part, with the job log, the model and the guard count, is built first in the thrift shop (decision 24, `Assets/thriftshop-level.md` Track S); cameras and the monitor join the candidate set here.

**Done when:** cameras work without a single change to guard code, and tampering with a camera fools the guard at the monitor.

---

## Integration seams

| Seam                     | NPC side needs                                                    | Player, world, and art side provides                                                                                                                     |
| ------------------------ | ----------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Phase 1 gate             | A camera and occlusion the player can judge sightlines through    | Dither-fade occluder, low interior walls                                                                                                                 |
| `Stance`                 | Posture only: Standing, Crouching                                 | Done. Sprinting is `PlayerMotor.IsSprinting`                                                                                                             |
| Assembly definitions     | NPC → Player → Core, no cycles                                    | Agreement on the assembly split, and the Player assembly's package references                                                                            |
| `PerceptionTargets`      | Body points that follow the animated rig, grouped by region       | A trigger `BoxCollider` on each mapped bone, on every outfit. Renaming a bone means updating `MapPlayerPerceptionPoints`                                 |
| `PlayerConduct`          | What the player is doing, readable each tick                      | Hooks from the controller and interactions (sprint, loot, tools)                                                                                         |
| `CharacterAppearance`    | The player's feature vector at the moment of each observation     | Outfit state mapped to both meshes and features. **Agree this early.**                                                                                   |
| Vision cone on the floor | `NpcVisionCone`, drawn as light (decision 21)                     | The material and colors, agreed together so the cone fits the palette                                                                                    |
| Doors and devices        | `Door`, `Breaker`, `Device`, and the NPC errands that answer them | The interaction system that opens doors and uses devices, and their placement in levels                                                                  |
| NPC animation            | Patrol, investigate, run; civilian phone reach, back away, cower in fear | Mixamo retargeting onto the NPC rig                                                                                                                      |
| Zones                    | The component and its debug gizmo                                 | Placement during level design                                                                                                                            |
| Gameplay lights          | `GameplayLight` and `PlayerLighting` with their debug readouts    | During the lighting pass: `GameplayLight` on every lamp that should hide or reveal, shadows on, fixtures off `Occluder`, breaker lamps Mixed or Realtime |
| Witness Report screen    | `WitnessReport` with per-feature confidence                       | The newspaper screen that presents it                                                                                                                    |

---

## Risks

| Risk                                                                      | Mitigation                                                                                                                                                                                                  |
| ------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Building the guard FSM before perception exists                           | M1 before M3. Nothing to drive an FSM means nothing to test it with.                                                                                                                                        |
| Perception is invisible, so it cannot be tuned                            | Build the debug readout alongside perception, not after it                                                                                                                                                  |
| Suspicion from mere visibility kills social stealth                       | Decision 6: suspicion rises from abnormal conduct only. The M2 done-when checks it.                                                                                                                         |
| Linecast cost grows with NPC count                                        | Cheap-to-expensive checks, per-archetype tick rates, staggered per NPC, no casts at zero-weight regions. Worst case is 17 casts per tick per guard.                                                         |
| Runtime state written into a ScriptableObject                             | Decision 1. State lives on components.                                                                                                                                                                      |
| One enum trying to hold stance, alert, and activity                       | Decision 3. Three fields.                                                                                                                                                                                   |
| Suspicion and evidence merged into one number                             | Decision 7. Two accumulators, or calm witnesses stop being dangerous.                                                                                                                                       |
| A dependency cycle once assembly definitions exist                        | Decision 4. Shared types go in Core; Core references nothing.                                                                                                                                               |
| The decision window buried inside tree evaluation                         | Decision 9. `Deliberating` is a real, readable state.                                                                                                                                                       |
| Cameras need guard code                                                   | Perception has leaked into the guard. The M8 done-when catches it.                                                                                                                                          |
| Curious is too short, so there is no tension                              | The Phase 3 gate check: lengthen it before doing anything else                                                                                                                                              |
| An array in `Observation` shared between copies                           | Decision 14. Debug detail stays on `NpcPerception`.                                                                                                                                                         |
| An archetype with empty curves or all-zero weights is blind with no error | `OnValidate` restores empty curves and warns on all-zero weights                                                                                                                                            |
| A camera pointing straight down sees 360°                                 | M8 adds a 3D cone for cameras. Mount test cameras at an angle.                                                                                                                                              |
| A player prefab rework drops a perception component                       | `NpcPerception` warns when `PlayerConduct` is missing, but suspicion then silently never rises. Consider `[RequireComponent(typeof(PlayerConduct))]` on `PerceptionTargets`, so Unity refuses to remove it. |
| The vision cone promises safety that perception does not give             | Decision 21. Perception is the truth; the overlay is tuned against the debug rays until "crouch-safe" really is safe.                                                                                       |
| Abilities grow into a scripting framework                                 | Decision 20. Each ability is a small named effect; the only shared piece is the errand.                                                                                                                     |
| A visible shadow does not hide the player                                 | Decision 17. Usually a lamp without `GameplayLight`, with shadows off, or with a fixture on `Occluder`. Select the player and read the gizmo lines: green is lit, red is shadowed.                          |
| The player spawns after NPCs start                                        | `NpcPerception` finds the player once in `Start`. Replace with a static accessor on `PerceptionTargets` when spawning arrives.                                                                              |

---

## Open questions

- ~~**Body-point weighting.**~~ Answered by decision 13: per archetype, per region.
- ~~**Tick rate.**~~ Answered: per archetype, on `NpcArchetype`.
- **Wall-flatten.** Deferred. Add it if playtests show crouching behind low walls is not enough cover. It would need a `WallFlatten` stance, wall detection and sideways movement, and a narrower-silhouette `fStance` factor on `NpcArchetype`.
- **Region weights in playtest.** The starting values in the archetype table are guesses. Does a civilian ignoring feet entirely feel fair, or should it be low rather than 0?
- **Is coverage visible to the player?** Only in debug for now. Showing it in the final game (a safe that visibly gets checked, a patrol rhythm) helps predictability; hiding it keeps the adaptive security mysterious. Affects the "predict who sees you" goal.
- **Which conduct counts as abnormal, and where.** The first set of conduct flags and their weights per archetype and zone type.
- **Does `NpcStance` exist?** Only if civilians crouch or hide as part of a reaction.
- **Decision window length.** Likely 1.5–3 seconds, probably different per reaction. A playtest question.
- ~~**Reading conduct through a feed.**~~ Answered by decision 6: yes for suspicion. The camera's own conduct weights define what the monitor guard reads. Behaviour never becomes witness evidence for any observer (decision 23).
- ~~**Cameras and light.**~~ Answered by decision 17: yes. Cameras use `NpcPerception`, so `f_light` applies to them automatically, and the breaker counters them too. The camera archetype's `darkVisibilityFactor` sets how much.
- **Baked lights in builds.** Not yet verified in this Unity version: does a Baked light keep its `Light` component and parameters in a player build? `GameplayLight` assumes it does.
- **Which abilities ship.** Kept so far (concept 6.7, and optional in M5 step 4): disable the internet, call the guard's mum, page the guard, leak the group chat, clog the toilet, car alarm. "Call the guard's mum" and "page the guard" act on the guard alone, so one of them is the M5 ability. The car alarm is the one that changes facing rather than position: everyone turns to the window.
- **What "caught" means without a guard.** Tier 1 has only civilians. A discreet call starts a police timer; what happens when it runs out? Partly answered by the apartment level: there, a completed call fails the job at once through `JobOutcome.Fail(PoliceCalled)`. A timer for store jobs is still open.
- **What a guard's `Reporting` does.** Who receives the report: other guards, a global alarm, or the police timer?
- **Takedowns and knock-and-hide** (concept 6.7). In no phase and not on the cut list. In scope or not? A takedown would add an unconscious state and body discovery.

---

## Changelog

- **2026-09-21** — First version.
- **2026-09-21** — Revised after cross-checking against `roadmap.md` and the concept document:
  - Suspicion rises from abnormal conduct, not from visibility; `PlayerConduct` and `Zone` added (decision 6).
  - `f_duration` moved from suspicion to the witness (decision 7).
  - Cameras have no suspicion; `SecurityMonitor` relays their feed to a guard (decision 11).
  - `Reporting` removed from the alert ladder; it stays an activity.
  - Stance defined as posture only: `WallFlatten` needed, `Sprinting` becomes conduct.
  - Hearing split into event loudness and listener sensitivity.
  - Memory retention added to the witness block.
  - `PretendingNotToSee` added to `CivilianActivity`.
  - Assembly definitions now instead of deferred; `NoiseBus` and `NoiseEvent` moved to `Core`.
  - Wording: only Phase 4 is a hard gate among the phases listed.
- **2026-09-23** — M1 perception core built, then refactored to per-archetype region weights. M1 done-when passed on both versions:
  - `PerceptionTargets` exposes every body point, grouped by `BodyRegion`; `PerceptionPoint` and the `MapPlayerPerceptionPoints` editor tool added (decision 5).
  - Region weights on `NpcArchetype` replace per-point weights and cast-point priorities on the player (decision 13).
  - `Observation` carries no arrays; debug detail stays on `NpcPerception` (decision 14).
  - `IPerceiver` settled as the `Perceived` event plus `TryGetLatest`.
  - FOV check flattened to the horizontal plane; stagger by spawn counter instead of `GetInstanceID()`.
  - Phase 2 done-when clarified: the occluder needs a gap underneath. Test bench built with a table slab.
  - Archetype assets created for guard, civilian, clerk, manager, distracted shopper, and camera, with vision values.
  - `MenuPaths` moved into the `SuspectUnknown.Editor` assembly.
  - Camera 3D cone added to M8; body-point weighting and tick rate questions answered.
  - Wall-flatten deferred: removed from the `Stance` target and the Phase 1 done-when, kept as an open question.
- **2026-09-24** — Sprinting moved out of `Stance` into `PlayerMotor.IsSprinting`. `PlayerNoise` polls stance and sprint each frame. `PlayerConduct` (M2) reads the motor directly, so no adapter is needed.
- **2026-09-24** — M2 steps 1–4: `ConductFlags` and `PlayerConduct` (zone context folded in as `InStaffArea`), `Zone` and `ZoneType` on a new `Volume` layer, `ConductWeights` and the suspicion block on `NpcArchetype`, and conduct plus abnormality on `Observation`, computed by the perceiver. The feed question is answered by decision 6.
- **2026-09-24** — M2 done: `NpcSuspicion` with delayed decay and hysteresis, `NpcSuspicionIndicator` in the new `NPC/Scripts/UI/` folder, and suspicion in the debug readout.
- **2026-09-24** — Edge-of-screen suspicion indicators (`OffscreenIndicatorHUD`, `OffscreenIndicatorElement`). Player prefab moved to the modular model; `PerceptionTargets` must not collect points from inactive skeletons.
- **2026-09-24** — Patrol redesigned as coverage-driven points of interest (decision 15), replacing `PatrolRoute`. `GuardActivity` gains `Watching` and loses `ReturningToPost`. Phase 8 placement targets POIs and their decay multipliers.
- **2026-09-29** — M3 done. The guard patrols POIs, watches, investigates, searches, pursues, gives up, and catches, all driven by alert state each frame:
  - `GuardBehaviour` owns body rotation (smoothed, from `desiredVelocity`), debounced sight for chasing, raw sight for catching, and the give-up pause.
  - Animator controllers are built from code around stances, with variants, turn-in-place blends, measured gait speeds, and interruptible transitions (decision 16). `GuardLocomotionBuilder`, `GuardClips`, `AnimatorBuildUtility`, and the stance structs added.
  - `NpcAnimator`, `GuardAnimation`, and the two params classes drive the animator; `NpcHeadLook` turns the head ahead of the body via Animation Rigging.
  - `Dangle` gives the guard's keys a procedural swing.
  - The player moved to a modular, correctly scaled model; perception points are mapped onto its active skeleton only.
  - File tables, prefab composition, and folder tree updated; the broken UI table repaired.
- **2026-10-05** — Light reworked (decision 17). Authored `LightVolume` triggers are replaced by sampling the scene's lights: `GameplayLight` in Core, `PlayerLighting` and `PlayerLightingDebug` on the player, and `NpcPerception` reading the level through `LightFactor`. The light half of M4 passed on the test bench. The M5 breaker now switches lamps off, the `Volume` layer holds zones only, and the cameras-and-light question is answered.
- **2026-10-05** — M4 done. Hearing through the NavMesh (`NoiseBus`, `NpcHearing`, `HeardNoise`, `PlayerNoise` on the bus) with glances, suspicion floors, and last-known positions from sound. Hearing values retuned for every archetype with the reaction-radius formula, so only the guard reacts to footsteps and no sound alone can alert. File tables, prefab composition, and the hearing table updated, with a dark-factor column added.
- **2026-10-05** — M5 redesigned after design answers. Decision 6 extended: abnormal world state raises suspicion too. New decisions: anomalies (18), doors are the player's (19), verbs are abilities used at in-world devices (20), and the vision cone is drawn as light (21). The thrown object and the jammed door are gone; M5 is now the cone, doors, the breaker, abilities, vault pursuit, and the near-miss pass. Planned files added to the tables; open questions added for abilities, the caught state, reporting, and takedowns.
- **2026-10-05** — Optional M5 abilities listed under step 4: call the guard's mum, page the guard, leak the group chat (guard only), clog the toilet and car alarm (with M6). The group chat adds a perception modifier to errands.
- **2026-10-08** — Fix: a watching guard turned to face a quiet player through walls, because every footstep heard while alert moved the last-known position. Now only alarming sounds move it, in both `GuardBehaviour.OnHeard` and `NpcSuspicion.LastStimulus`; ordinary footsteps still earn a glance.
- **2026-10-09** — M5 step 1 done: the vision cone (decision 21 rewritten as built). Static fan built once; the CPU writes wall, cover-start and shadow-end distances per ray into `_RayData`; the shader cuts, blends between rays, softens edges over a width in metres, dims the crouch-safe strip, and fades by the archetype's distance and angle curves. Corner flicker recorded as a known issue with its planned fix. CPU pass (profile, skip off-screen cones, batch rays) added under step 1.
- **2026-10-09** — Vision cone CPU pass done: profiler markers added, off-screen cones skip their rays, and ray directions are cached. 8 cones went from 0.91 ms to 0.45–0.50 ms per frame in the editor. `RaycastCommand` batching not needed for now.
- **2026-10-09** — M5 steps 2.1 and 2.2 done (anomalies, anomaly sight, the guard walking to a noticed anomaly). Doors redesigned: locks have levels 1–3 gated by a Lockpicking skill, opened through a Dying Light style minigame with unlimited picks; being seen picking is the new `Lockpicking` conduct flag (replacing `OpeningDoor`). Decision 19 extended. Steps 2.3–2.5 written up for implementation in `Assets/doors-and-lockpicking.md`.
- **2026-10-09** — M6 started in parallel with the rest of M5, recorded as a deliberate exception to the Phase 4 gate order.
- **2026-10-09** — Civilians designed (decision 22): they witness all the time and alarm only on criminal acts. Every archetype shares the guard's rise rate of 0.5; civilian sprint weights 0.1, lockpicking 1.0 everywhere. Decision 18: noticing an anomaly is separate from being suspicious of it, and roles decide what to do (clerk shuts doors as a chore, manager investigates). Decision 21: cones for guards and cameras only. M6 gains three first steps: a civilian that exists (`NpcFacing`, `CivilianSpot`), a thin witness slice (exposure), and door chores.
- **2026-10-09** — M6 step 1 done: `NpcFacing` extracted from `GuardBehaviour` and shared, `CivilianSpot`, `CivilianBehaviour` idle routine with the placeholder reaction, `HasArrived()` shared by guard and civilians, and `CivilianRandomOutfit` picking a role outfit per civilian.
- **2026-10-09** — M6 step 2 built: `NpcWitness` counts exposure (seconds of a perfect view) per perception tick from the observation's strength, on guards and civilians, with a readout line. Play-mode check pending. Work paused at M6 step 3 (door chores), which waits for Marcus's doors.
- **2026-10-13** — Apartment level planned in `Assets/apartment-level.md`: the tutorial job, with a tired clerk at home who is quick to alarm (`TiredClerkHome`, the interior as a `Staff` zone), a sleep rhythm, a sight-driven decision window, and a police call that fails the job. Decision 21: the clerk at home is the one civilian with a cone, semi-dynamic through a runtime view scale on `NpcPerception`. Decision 22 gains the resident line. `ResidentBehaviour` and `Dozing` added to the civilian table; the "caught without a guard" question is partly answered.
- **2026-10-14** — Apartment job flow steps A1–A5 done (`Assets/apartment-level.md`, handover in `handovers/apartment-job-flow.md`). On the NPC side: `GuardBehaviour.Catch()` now calls `JobOutcome.Fail(Caught)`, replacing `ReloadOnCaught`. The `Lockpicking` conduct flag (M5 step 2.5) is in, and so is the new `Stealing` flag, set while the player holds E on loot (`PlayerLootInteraction.IsTaking`). Each flag has a `ConductWeights` field that defaults to 1.0. The archetype assets do not serialize the two new weights yet and rely on that default.
- **2026-10-14** — The clerk-at-home archetype is `ApartmentClerk` (planned as `TiredClerkHome`), with `inStaffArea` 0.8 and `sprinting` 0.4: full view inside the apartment reaches Alerted in about 2.3 s. `Assets/apartment-level.md` 2.1 updated to match.
- **2026-10-14** — Apartment clerk redesigned: mostly asleep in an armchair in the corner of the living room, with an occasional snack run to the kitchen (a tell before getting up, full cone on the walk, back to the room while snacking). The Apartment gains a NavMesh; the route must cross no closable door (decision 19). Track B gains step B5, the snack run (`Assets/apartment-level.md` 2.2, 4).
- **2026-10-14** — Decision 23: the witness model is appearance only (Head, Chest, Legs, Feet, each remembered as style and color separately); behaviour and method go to the job log and adaptive security instead. Burns in the Phase 7 profile are temporary. The full witness model is built first in the apartment level as Track W, right after the clerk. M7 and the `CharacterAppearance` row updated.
- **2026-10-14** — Apartment clerk B1–B3 done: `ApartmentClerk` prefab variant in the armchair; view scale on `NpcPerception` (`SetViewScale`, scaled `ViewDistance` and `FovDegrees` used by every sight check, debug cone and readout follow it, test context menus); the vision cone follows the scale through new `_RangeScale` and `_FovScale` shader inputs, with `_HalfFov` kept at the full angle for the ray lookup. The clerk has a cone (decision 21).
- **2026-10-14** — Apartment clerk B4–B5 done: `ResidentBehaviour` (with `ResidentPhase`) runs the doze rhythm in the armchair with an eased view scale, wakes on anything above Idle (a scare pauses the routine and looks toward the stimulus, and shortens later dozes), and makes snack runs to a `SnackPoint` by the fridge along the NavMesh. `CivilianActivity` gains `Dozing`. The clerk's hearing is raised (sensitivity 12, suspicion 0.85) so a sprint anywhere in the open flat wakes them.
- **2026-10-14** — Apartment clerk B6 done: the decision window opens on reaching Alerted (not on being Alerted, so it does not reopen while suspicion lingers), fills while the clerk sees the player and drains while not; full starts an uninterruptible call that fails the job with `PoliceCalled` after 1.5 s. Works in the armchair and on the snack run. Exposes `WindowProgress` and `IsCalling` for the ring and the HUD.
- **2026-10-14** — Apartment clerk B7 animations: ten hand-made clips in `NPC.blend` (sleep, stir, awake, stand up, tired walk, snack at the counter, sit down, standing idle, phone reach, phone call), exported to `Assets/NPC/Art/Clerk/Animations/Clerk_Animations.fbx` with a rest-pose `Clerk_Rig.fbx` that defines the avatar (the animation file's own default skeleton is posed). Root baked based on Original, so seated and leaning poses keep their place. `ClerkLocomotionBuilder` builds `ClerkAnimator`: a base layer following `ResidentPhase` and a Phone layer masked to head and arms, faded in by `ResidentAnimation` (a Humanoid layer at full weight with no clip holds its masked parts in the neutral muscle pose).
- **2026-10-14** — Track B complete. `ResidentWindowRing` draws the decision window around the clerk's suspicion indicator (red and pulsing while calling). The HUD's status tag is live: every NPC reports its alert state through `NpcAlertReporter` to the Core board `JobAlert`, which keeps the highest (`JobAlertLevel`: Undetected, Suspicious, Searching, Alarm), and `AlertStatusHud` shows it. Dependencies stay NPC to Core, and any number of NPCs works the same way.
- **2026-10-14** — The decision-window ring moved from above the clerk's head to the HUD, so it shows while the clerk is off screen: `ResidentWindowReporter` (NPC) reports the open window to `JobAlert`, and `DecisionWindowHud` (Core) draws the fullest open window as a ring at the end of the status tag. `ResidentWindowRing` removed.
- **2026-10-15** — Apartment Track W1–W4 done and checked: `PlayerAppearance` reads the outfit into `CharacterAppearance`, carried in every `Observation`; the archetype's witness block (`observerSkill`, `FeatureVisibility`); `FeatureBelief` (log-odds, overflow-safe confidence) and per-slot style and color beliefs in `NpcWitness`, with `f_duration` per unbroken sighting and slots learned only through visible regions. Color confusion uses one shared `ConfusionMatrix` asset instead of one per slot, a wrong style is any other style, and the chance of a mistake is capped (without the cap, many poor looks made a mistake outscore the truth). Memory retention moves to W5, where it is first used. M7 and the file tables updated.
- **2026-10-15** — Apartment W5 built: `memoryHalfLife` on the archetype (clerk 90 s, civilian 120, manager 180, guard 300, camera 0 = never forgets); `NpcWitness` fades every belief toward the even start when evidence arrives or the beliefs are read, never per frame. Track W gains W8: the job's police description is saved at job end and loaded at job start, fading one step per job and merged with new evidence, the first part of the Phase 7 police profile.
- **2026-10-15** — Apartment W7 redesigned: instead of a sentence, the newspaper shows a witness sketch, the player model in its idle pose dressed in the police description, rendered once into a RenderTexture and printed in grayscale, with a caption naming the colors. Caught gives a mugshot ("Image of the caught criminal"), completed and seen a wanted poster ("Police seek this person"), completed and never seen the "?" placeholder ("No witnesses came forward").
- **2026-10-15** — Apartment W6 done and checked: `FeatureBelief` stores scores in a serializable array and merges by adding log-odds; `WitnessReport` holds a description (per-slot style and color beliefs) and is both a witness's memory and its job-end copy; `JobWitnesses` merges every witness's report into the police description when `JobOutcome` ends the job. Seven EditMode tests for the merge, fade and confidence (`Assets/Testing/Editor/WitnessMergeTests.cs`) pass.
- **2026-10-15** — Apartment W7.1–W7.4 done and checked: `SketchBooth` prefab (Sketch layer 17, added to the CLAUDE.md layers table) with a mannequin of the player model held in frame 0 of `standing_idle` through a PlayableGraph; `SuspectSketch` dresses it from a `WitnessReport` (part lookup by name through the shared `OutfitFeature.TryParse`, nearest existing part as fallback, `SketchUnsure` and `SketchUnknown` material swaps), warms up its shader variants at load, renders once into a RenderTexture when `JobWitnesses.Described` fires, and writes the hedged caption to the new Core hook `JobNews`.
- **2026-10-15** — Apartment W7.5 built: `JobOutcomeScreen` shows the witness sketch from `JobNews` (a RawImage with the new `Grayscale` Canvas Shader Graph material), with mugshot height lines when the job failed, a wanted caption when it succeeded, and the "?" when nobody saw the player; it listens to `JobNews.Changed` so the order of the end-of-job events does not matter. The newspaper photo is square and the RenderTexture moved to `Core/UI/Textures/WitnessSketch.renderTexture` (512 × 512), so Core owns the seam and the NPC booth writes into it.
- **2026-10-16** — Thrift shop planned in `Assets/thriftshop-level.md`: an open shop at dusk with a tired clerk, customers and the first guard in a real level; a required register till and randomised bonus loot; the two-slot bag with van stashing (G). New decision 24: adaptive security from completed runs only, a Markov chain over rooms and a Dirichlet over entrances, deterministic greedy placement over authored candidate POIs at load, one adaptive POI per guard, a second guard after two completed runs. New decision 25: guards recognise a described outfit (decision 6 extended as its one exception to "never from being seen"). Decision 15: base and candidate POIs, and a runtime drain factor. Decision 23: appearance is fixed during a job. The open safe is a new anomaly kind, `SafeOpen`, that raises the noticing guard's baseline to just under Curious for the rest of the job.
- **2026-10-15** — Apartment W8 changed: the police record is saved only when a job is completed, as `progress.json` is. A failed job still produces the description (the mugshot) but leaves the record unchanged, so a failure cannot make the level unwinnable. `JobWitnesses` listens to `Failed` and `Completed` separately.
- **2026-10-17** — Thrift shop T1 done and checked. Rooms: nine `Zone`s (shop front, shoe section, shop middle, emergency exit, shop back, register, trash room, stockroom, office) plus the street, ten states instead of the planned seven. `JobLayout` (Core) lists rooms as `Zone`s and entrances as `Entrance`s and numbers both in `Awake`, so the number lives in one place; object names are the display names. Room and entrance names moved out of `SecurityLayout`, which keeps only spawn and POI candidates (decision 24). `PlayerRoom` (Player) tracks the current room with 0.5 s hysteresis instead of `PlayerConduct`. `Entrance` (Core) counts inbound crossings; `Door.prefab` carries an inactive `Entrance` child for exterior doors. The saved security record will compare room and entrance names, not counts.
- **2026-10-17** — Decision 19 changed: guards open doors they need to pass (a `NavMeshLink` on a `Door` area, `OpenForPassage()` with no anomaly, closed again once past the far reach) and ignore the lock; civilians and hearing still treat closed doors as walls. Only a door the player leaves open is evidence; a chasing guard runs through doors without slamming or closing them. Needed because the thrift shop's office is reachable only through the `StaffRoom Door`. Built as thrift shop step T2b. M5 2.3 ticked (the door was already built for the apartment).
- **2026-10-17** — Thrift shop S1 done and checked: `JobLog` (Core, static) counts room transitions and inbound crossings for the run. Its lifetime follows `JobLayout` (`Begin` in `Awake`, `End` in `OnDestroy`), and only the layout that began it may end it, since restarts load scenes asynchronously and the old layout can be destroyed after the new one has begun. `PlayerRoom` and `Entrance` call it directly; `PlayerRoom.RoomChanged` was removed as unused. `JobLogReadout` shows it by name.
- **2026-10-17** — Witness quality is per slot (decision 14 changed): `Observation` carries five region fractions instead of the region bitmask. `NpcWitness` scales each slot's look by its own region's fraction (the chest by the better of the torso and the hands) in place of the whole-body visibility, through the new `Observation.ViewingConditions` (distance × angle × light × stance). Suspicion is unchanged: `Strength` is still `visibility × ViewingConditions`. Play-mode check still to run.
- **2026-10-17** — Thrift shop S2 built, play-mode check still to run: `SecurityRecord`, `SecuritySave` and `SecurityStore` (Core) save `security.json`; `SecurityRecorder` on `Job` decays the record by 0.8 and adds the run on `Completed` only, and starts over with a warning when the saved room or entrance names no longer match the layout. Tools → Security Record logs, reveals and deletes it. Fake Runs split out as S2b.
- **2026-10-17** — Thrift shop S2 done and checked: a completed run writes `security.json` with counts equal to the readout, a failed run leaves it untouched, a second run decays the stored counts by 0.8 and adds the new run, a reordered room list discards the record with a warning, and a deleted record starts fresh.
- **2026-10-17** — Thrift shop S2b done and checked: `SecurityFakeRunsWindow` (Tools → Security Record → Fake Runs Window) adds completed runs through a chosen entrance, each an entry and a transition from the street into the room behind it. The save path moved into `SecurityRecorder.RecordRun`, shared by real and faked runs, so both decay the same way; `JobLayout.EntranceRoom` gives the room behind an entrance from list positions, so it works in edit mode. `PlayerRoom.RoomChanged` deleted, as S1 had recorded.
- **2026-10-17** — Thrift shop S3 planned: the prediction class is `ActionModel` (was `SecurityModel`), named for what it models. The transition chain is over room changes: the diagonal stays 0 and only off-diagonal cells are smoothed, since the job log never records staying in a room (`Assets/thriftshop-level.md` 5.1).
- **2026-10-17** — The record of the player's actions is renamed to match `ActionModel`: `SecurityRecord`, `SecuritySave`, `SecurityStore`, `SecurityRecorder`, `SecurityMenu` and `SecurityFakeRunsWindow` become `ActionRecord`, `ActionSave`, `ActionStore`, `ActionRecorder`, `ActionMenu` and `ActionFakeRunsWindow`, in `Core/Scripts/Actions/`; the file is `actions.json` and the menu Tools → Action Record. What responds to the record keeps "Security": `SecurityLayout`, `SecurityPlanner`, `SecurityDirector`, `SecurityDebugOverlay` and "Raised security". Older changelog lines keep the old names.
- **2026-10-18** — Thrift shop S3 done: `ActionModel` (NPC, pure logic) builds the transition chain over room changes (diagonal 0, Laplace smoothing off the diagonal), the entrance Dirichlet, and occupancy seeded from the entrances, propagated T steps, averaged and normalised without the street. α and T are constructor parameters, so S5's director holds them as tuning. Seven EditMode tests (`ActionModelTests`) pass, including the hand-checked uniform values for an empty record. New file table for `NPC/Scripts/Security/`.
- **2026-10-18** — Thrift shop S4 done: `SecurityPlanner` (static, NPC) picks candidate POIs greedily by marginal coverage gain (`weight × remaining miss × d`, d = 0.6), ties to the lower index, one adaptive POI per guard from the first completed run, drain multiplier `1 + gain / firstGain`. Plain-data input (`PoiCandidate`) and output (`SecurityPick`, `SecurityPlan`), so it is tested without a scene. Explanation lines quote the model's probabilities, since the record holds decayed sums rather than a run history (decision 24 and `Assets/thriftshop-level.md` 5.3 updated). `SecurityPlannerTests` pass.
- **2026-10-18** — Thrift shop S4b done: the adaptive-security budget is per level. `SecurityTier` (NPC) holds guards and adaptive POIs from a completed-run count; `SecurityLayout` holds the level's tiers, exposes `TierFor` and warns on bad tiers (none at 0, unsorted, shrinking guards, more guards than spawns, more POIs than candidates); `SecurityPlanner.Plan` takes a `SecurityTier` and the hard-coded `GuardCount`/`AdaptivePoiCount` are gone. Guards will spawn at random free spawns instead of the spawn nearest a pick (the spawn carries no plan information). The thrift shop's tiers: 0 runs → 1 guard, 0 POIs; 1 → 1, 1; 2 → 2, 2; 3 or more → 2, 3. Decision 24 and thriftshop-level.md 5.2, 5.3 updated; `SecurityCandidate` and `SecurityLayout` marked working.
- **2026-10-18** — Thrift shop S5 done and checked: `SecurityDirector` (NPC) loads the action record (a record that no longer fits the layout plans as a first run), builds the `ActionModel` and the candidates, looks up the level's `SecurityTier`, runs `SecurityPlanner`, switches on the chosen candidate POIs (multiplier set before activation) and spawns the guards at random free spawns, with a debug `spawnSeed`. Checked with Fake Runs: 0 runs gives 1 guard and no adaptive POIs, 2 runs through the emergency exit give 2 guards and 2 POIs, 3 runs give 3 POIs. Next: S6 (debug overlay, which needs getters for the model and the plan) and S7.
- **2026-10-18** — Thrift shop S6 done and checked: `SecurityDebugOverlay` (NPC, IMGUI) opens with F3 (new `SecurityDebug` input action, `PlayerInputs.SecurityDebugPressed`) and shows the plan's numbers: entrance probabilities, room occupancy with heatmap indices, every candidate's standalone gain with its pick mark and multiplier, the explanation lines, the room-transition heatmap, and a world label above each picked POI. `SecurityPlan` gained `StandaloneGains` (the gain of each candidate as the only POI) so the overlay can show unpicked candidates and a first run; `SecurityDirector` gained read-only getters for the record, model, candidates and plan. The panel data is built once on first open because the plan is fixed for the run. Next: S7 (the shared tier asset, so the main menu reads the same tiers as the level) and the X2 adaptation pass.
- **2026-10-18** — Thrift shop S7 done and checked: the tiers moved out of the scene into a shared `SecurityBudget` asset (Core, `ThriftshopSecurityBudget`), with `SecurityTier` moved to Core beside it. `SecurityLayout` reads the asset through `TierFor` and now only validates what needs the scene (guards against spawns, POIs against candidates); the asset validates its own data. The main menu's `MapBoard` loads `actions.json` once and `MapLocation.SecurityLine` shows "Raised security" in red once `IsRaised(completedRuns)` (more guards than a first run); a location with no budget, like the apartment, shows its typed text. `SecurityBudgetTests` added. Track S is complete apart from the X2 adaptation playtest.
- **2026-10-19** — Thrift shop C1 done and checked: the decision window is extracted from `ResidentBehaviour` into `NpcDecisionWindow` (NPC), shared by the apartment clerk and every civilian (`Civilian.prefab` carries it); `ResidentWindowReporter` is replaced by `NpcWindowReporter`. The apartment plays exactly as before.
- **2026-10-19** — Thrift shop C2 partly done: `CivilianSpawner` and `CivilianSpawnGroup` spawn customers (and any other role) once at load from random free points, with a debug seed, gizmos and a warning when a role has fewer `CivilianSpot`s than it can spawn. Checked: the count rolls in range, civilians walk between spots, the clerk works the register and leaves on errands. Still to do: `CivilianSpot` has no weight field yet, and the police-call check (taking the till in view of the clerk and of a customer) is not run. An older duplicate, `CivillianSpawner`, is to be deleted.
- **2026-10-19** — The rest of M6 moved into the thrift shop as Track C steps C3–C9: the witness check, door chores, civilian animation, `ReactionTree` and the first tree, outcomes and one counter-action, `PanicContagion`, and roles with telegraphed personalities. New section 2.4 in `Assets/thriftshop-level.md` and a manager in the cast; four civilian open questions added. M6's step list and done-when are unchanged.
- **2026-10-19** — Thrift shop C2 done and checked: the police call works for the clerk and customers. `CivilianSpot` gets no pick weights (decided): spots are picked evenly, and the register's 20–35 s stay against 6–12 s errands keeps the clerk there most of the time. C3's witness check is postponed. Detection speed is left to playtesting the archetype values.
- **2026-10-19** — Thrift shop C5 done and checked: civilian animation. `CivilianClipImport` sets loop and root baking on `NPC.fbx`'s clips; `CivilianLocomotionBuilder` builds `CivilianAnimator` (Standing speed tree, a Dwell tree over a `Station` float with one clip per `CivilianStation`, the Phone layer); `CivilianAnimation` drives it from `CivilianBehaviour.IsDwelling` and `Station` and from `NpcDecisionWindow`. The Phone layer and upper-body mask moved from `ClerkLocomotionBuilder` into `AnimatorBuildUtility.AddPhoneLayer`, with a shared `AddTransition`; `Deliberating` and `Calling` moved from `ResidentAnimatorParams` to `NpcAnimatorParams`. The apartment clerk is unchanged. `CivilianBehaviour`'s `using UnityEditor` is now inside `#if UNITY_EDITOR`.
- **2026-10-19** — Civilians never flee (decision 22 updated). The flee outcome is replaced by **scared**: the civilian freezes in place, covers their face, looks at the floor and shakes, sees nothing meanwhile (`SetViewScale(0, 0)`, so no evidence and a natural suspicion decay), and after a random time between a minimum and a maximum goes back to their routine. A scared witness gathers nothing; the clean look now belongs to pretending not to see (M7 note updated). `CivilianActivity` loses `Fleeing` and renames `Frozen` to `Scared`; the exit-distance input and `CivilianExit` are dropped with the flee. Thrift shop C6 started (`ReactionInput`, `ReactionContext`): the tree samples once when the window opens, a call stays on `NpcDecisionWindow`, other outcomes go to `CivilianBehaviour` through `Decided`. `Assets/thriftshop-level.md` 2.4, C6–C8 and the open questions updated; C8's panic trigger is now a scared civilian, and whether panic makes a noise is an open question.
- **2026-10-19** — Thrift shop C6 built through step 6: `ReactionTree`, `ReactionNode`, `ReactionBranch`, `ReactionLeaf`, `ReactionWeight`, `ReactionInput`, `ReactionContext`; the archetype's `reactionTree` field (empty means always call; `Civilian` and `StoreClerk` use `CustomerReactions` for now); `NpcDecisionWindow.PendingOutcome`, `Decided` and the readout properties; `CivilianBehaviour` carrying out Scared and Avoiding and reporting the window through `Activity`; three new clips in `NPC.fbx` (`React_Freeze` renamed `React_Deliberating`, new `React_Scared` and `React_AvoidWalk`) and the reaction states in `CivilianLocomotionBuilder`. The apartment plays as before. Still to do: the C6 check (step 7). New step C6b in `Assets/thriftshop-level.md`: icons on the suspicion indicator (the lean while deliberating, since every civilian now deliberates with the same clip), a police-only ring, and a reaction toast in the job HUD; its step 1 (`ActivityChanged`) is done.
- **2026-10-19** — Thrift shop C6b step 2 done and checked: reaction icons on `NpcSuspicionIndicator` and its screen-edge copy (`ReactionIcons` sprite asset). Two rules changed: the HUD ring shows only a call under way, filling over the call's 1.5 s (`NpcDecisionWindow.CallProgress`), never a deciding window; and the status tag reads ALARM only for a guard pursuing or a civilian calling (new step 3b: `NpcAlertReporter` caps suspicion at SEARCHING and reads `GuardBehaviour.ActivityChanged` and a new `NpcDecisionWindow.CallStarted`). `JobAlert.Peak` and the debrief follow the same rule; the apartment resident now shows SEARCHING while deciding.
- **2026-10-19** — Thrift shop C6b steps 3, 3b and 4 done and checked (`NpcDecisionWindow.CallProgress` and `CallStarted`, `JobReaction`, `JobAlert.Reacted`, `NpcReactionReporter`). New step 2c from playtesting: `ICivilianActivity` (implemented by `CivilianBehaviour` and `ResidentBehaviour`) so the apartment resident shows reaction icons too, a lean toward a call shown red, and the call itself pulsing red with its progress as the fill. Known issue for after C6b: a window that drains while the civilian is still Alerted falls back to a red "!", which reads as an alarm.
- **2026-10-19** — C6b step 3 settled after playtesting: the call-only ring that filled over the call's 1.5 s appeared too late to act on, so the ring is back to showing a window leaning toward a call (orange, filling with the window) and the call itself (full, red, pulsing). Every lean on the indicator is orange; only the call is red. `NpcDecisionWindow.CallProgress` is removed (nothing reads it).
- **2026-10-19** — Thrift shop C6b step 5 done and checked: `ReactionToast` on `JobHud.prefab` under the status tag. Added from playtesting: `CIVILIAN ABOUT TO CALL` (orange) when a window leaning toward a call opens and `CIVILIAN LOST SIGHT` when it drains without one (`JobReaction.AboutToCall`, `LostSight`), so the toast warns while the player can still act, not only once the call is under way.
- **2026-10-19** — Thrift shop C6b step 2d: every indicator symbol is an icon from the one sprite asset (`ReactionIcons`, now seven icons): eye when curious; flashlight and handcuffs for guards; a staring eye for civilians from investigating up, orange even when Alerted outside the window, which settles the drained-window known issue above.
- **2026-10-19** — The reaction toast announces guards as well: `GuardReactionReporter` (NPC, on `Guard.prefab`) maps `GuardBehaviour.ActivityChanged` onto new `JobReaction` values (GuardCurious, GuardInvestigating, GuardChasing, GuardLostYou, GuardGaveUp). `ReactionToast` colors lines by stake: red for a call or a chase, orange for the warnings, off-white for the rest.
- **2026-10-19** — Civilian scope cut. The `Manager` and `DistractedShopper` archetypes are deleted (nothing in the game needs them), so the manager's door chore, tree and open question go, and the archetype tuning tables lose their rows. The clerk gets a reaction tree of their own (C9). Pretend not to see is not built, and the player's counter-action during a decision window is getting out of sight in time, with no verb that stops a call (C7 settled). Panic is individual: no contagion and no panic noise (C8 dropped), and social proof leaves the tree inputs. Code still to follow: `CivilianRole.Manager` (and the outfit list that reads it), `CivilianActivity.PretendingNotToSee` and the `PanicContagion` stub.
- **2026-10-19** — Baseline reaction trees set up for the C6 check: `CivilianTree` (customers) and `StoreClerk_Distance` (the clerk, part of C9), both splitting odd behaviour from crime on severity 0.8, then on distance (4 m for customers, 3 m for the clerk's crimes). Values and odds in `Assets/thriftshop-level.md` under C6. Thresholds then settled off every producible severity: customers 0.8 (sprinting and the staff area are odd behaviour), the clerk 0.78 (sprinting is odd, the staff area is crime).
- **2026-10-19** — Thrift shop C6 and C6b done and checked. The scope-cut code clean-up is done: `CivilianRole.Manager` and its outfit list, `CivilianActivity.PretendingNotToSee` and the `PanicContagion` stub are gone. Next: T2b (guards pass through doors).
- **2026-10-19** — Thrift shop T2b done and checked (decision 19 as built): NavMesh area `Door` (cost 2), excluded by civilian and resident agents and by `NpcHearing`, so a closed door blocks civilians' paths and sound; a `GuardLink` `NavMeshLink` on `Door.prefab`, active on the `StaffRoom Door`; `Door.OpenForPassage` (no anomaly, lock unchanged), `SwingSeconds`, `InReach`; guards cross links themselves (`autoTraverseOffMeshLink` off, `GuardBehaviour.CrossLink`), wait for the swing unless pursuing, and close a door they opened once out of its reach; `NpcAnimator` reads the body's movement on a link so the legs keep walking. A link should be narrow and short, since a crossing is a straight line. Next: G2 (slamming doors), which unblocks C4.
- **2026-10-20** — The guard plays the Blender clips in `NPC.fbx` (`Guard_Idle_1/2`, `Guard_Watch`, `Guard_Search`, `Guard_GiveUp_1/2`, `Guard_Walk`, `Guard_InvestigateWalk`, `Guard_Run`, `Guard_TurnLeft/Right`) instead of the Mixamo files; none were missing. `CivilianClipImport` became `NpcClipImport`: it also sets the guard clips' import settings (gaits keep Position XZ unbaked, turns keep Root Rotation unbaked) and loads clips for both builders, so `AnimatorBuildUtility.LoadClip` is gone. The Blender gaits are in place, so `GuardClips`' fallback speeds are their stride speeds measured from the planted foot: 0.93, 1.68 and 3.38 m/s, the same as `Guard.prefab`. `Guard_DoorSlam` is imported but not played yet.
- **2026-10-20** — Turn rate fix: `AnimatorBuildUtility.MeasureTurnRate` read `averageAngularSpeed`, which is in radians per second, as degrees, so the guard's turn thresholds sat at ±1.35 and ±2.69 while `NpcAnimator` sets `TurnRate` in degrees per second; any turn played the turn clip at double speed. It now converts, and the thresholds are ±77 and ±154 °/s.
- **2026-10-20** — Minigame refactor (`Assets/MinigameRefactor.md`): `PlayerMinigame` runs every minigame session and `MinigameScreen` holds the shared camera, blur, push-in and punch; `PlayerLockpicking` and `PlayerGlassCutting` implement `IMinigame`, and both models implement `IMinigameModel`. `PlayerConduct` reads `PlayerMinigame.Conduct` instead of `IsPicking` and `IsCutting`; NPC code unchanged. Safe opening is to be built on it.
- **2026-10-20** — `Minigames.prefab` (nested in `PlayerHud.prefab`) now holds every minigame view, the blur volume and `LockpickHud`, under a `MinigameRig` the player's minigames find their view through. Scenes no longer carry minigame views, a blur volume, or `view` references. Details in `Assets/MinigameRefactor.md` 2.1.
- **2026-10-21** — Thrift shop T3 (dusk lighting), G1 and G2 done; M5 2.4 (guard slams an open door) ticked. C9's customer personalities dropped, with personality as a tree input: role (clerk against customer) is what tells the player who is dangerous. Next: G3 (the safe as an anomaly).
- **2026-10-21** — The guard plays `Guard_DoorSlam` when resolving an anomaly (an open door, and the open safe in G3): a `Slam` trigger and one-shot state in `GuardLocomotionBuilder`, fired by `GuardAnimation` on the rising edge of `GuardBehaviour.IsResolving`. The clip is imported as a hold by `NpcClipImport`. `confusedDuration` is timed to the clip's impact, so the door or safe shuts when the guard's hand hits it.
- **2026-10-21** — Thrift shop G3, steps 1–5 done and checked: `AnomalyKind.SafeOpen` with an `AnomalyWeights.safeOpen` weight (guard 1, civilians and resident 0). `Safe` requires an `Anomaly`, raises it on opening and closes itself on `Resolved`; once cracked it reopens with a plain interact, no minigame (`IsCracked`). It raises `Opened` on every opening. The safe's `Anomaly` offsets follow the Blender-rotated root: local −Y points out of the front and +Z up. The guard closes the safe with the existing door errand and the slam clip; `confusedDuration` is 1.45 s, the clip's measured hand impact. `NpcSuspicion.RaiseBaseline` keeps a guard who saw the open safe at `curiousAt − hysteresis − onEdgeMargin` (0.11 for `SecurityGuard`) for the rest of the job, set before `OnNoticed`'s early returns so a chasing guard is affected too. `PointOfInterest.AlarmFactor` and `PoiSafeAlarm` make the office POI drain 1.5× faster once the safe opens, kept apart from the plan's `DecayMultiplier`. Bug fixed on the way: `Interactable.SetReachable` threw when called before that interactable's `Awake` (the safe's and the glass cutter's loot from their own `Awake`). The exception disabled `Safe`, so it never subscribed to `Resolved` and the guard slammed in a loop. Still to do: the money stacks (step 6) and the safe placed in the thrift shop's office.
- **2026-10-21** — Thrift shop G3 done and checked: the safe holds the jewelry store plan (`fixedContents`) and 1–2 money stacks (`moneyStacks`, the first `minStacks`–`maxStacks` live, set to 1 and 2), reachable only while open, and is placed in the office with `PoiSafeAlarm` on the office POI. This closes the "still to do" of the G3 entry above.
- **2026-10-21** — Thrift shop L1 and L2 done and checked. L1: `LootSlot` (on `LootDisplay.prefab`) and `LootSpawner` on `Job` roll 3–4 live displays per run with an unseeded `System.Random` and a debug seed; a slot that is not live switches off whole. L2: the jewelry store plan unlocks the jewelry store; `JobObjectives.storyItemsOptional` lets the thrift shop's van leave once the plan or any bag item is taken, while the apartment keeps its required items; the safe hides the plan once its unlock is saved and pays up to two stacks instead. Checked on the way: `completedRuns` is per location, so an apartment run never raises the thrift shop's security.
- **2026-10-21** — Thrift shop safe contents follow the jewelry store unlock (`thriftshop-level.md` 2.3, 3.1, L2): while locked, the safe holds the plan and one money stack; once unlocked, no plan and one or two stacks, so a replay still pays for cracking it. Money stacks roll their count only; their value is fixed on the `MoneyStack` item (decision 1).
- **2026-10-21** — Clerk door chores dropped (thrift shop C4, M6 step 3): civilians, the tired clerk included, ignore open doors, and only guards resolve them. Decision 18's role notes, the anomaly table and the code comments that mentioned the chore are updated.
- **2026-10-21** — Thrift shop L3 done and checked: `LootBag` (Core, on `Job` in both levels) owns the bag items, two slots, with static `Active` and `Changed`. `LootPickup.IsBlockedFor` refuses a bag item while the bag is full; `IInteractionBehaviour.BlockedText` (default null) says why, and `Interactable.Refuse` raises the static `Interactable.Refused` when the player presses E on a blocked interactable. `PickupToast` moved to beside the player at screen centre and shows refusals in red ("BAG FULL"); a locked door has no blocked text and toasts nothing. `CarryHud` and `LootBagHud` read the bag instead of their own list and `JobObjectives`.
- **2026-10-21** — Thrift shop L4 done and checked: stashing at the van. `LootBag.Stash()` empties the bag; `VanStash` (Core) is a Volume-layer trigger at `GetawayVan.prefab`'s side door holding the G prompt; a new `Stash` input action (G) raises `PlayerInputs.StashPressed`; `PlayerVanStash` (Player) shows the prompt while the bag holds something and stashes on G, so the van never looks up the player. `LootBag` keeps no stashed list (`thriftshop-level.md` 3.3 updated): `JobRewards` counts on take, so the debrief lists stashed and carried items alike.
- **2026-10-22** — Witnesses learn only from abnormal conduct (decision 22 changed): `NpcWitness` ignores every sighting whose abnormality is 0, so an NPC that just sees the player is not a witness, and each abnormal tick counts on its own. Concept 6.8's calm-witness inversion now means a calm civilian who watches the act; concept 6.8, decision 7's note, the M7 reaction note and `thriftshop-level.md` (goal 6, 2.4, C3's check) are updated to match.
- **2026-10-22** — Thrift shop L7 done and checked: story items in the main menu. `StoryItemDisplay` (MainMenu) pops an OP copy in with a scale overshoot and a shake, and hides it by shrinking; `MapLocation.storyItem` names the copy and `MapBoard.Select` shows it once the save has the target's `Requires` flag. The phone is now the thrift shop's OP copy instead of a `ShowWhenUnlocked` prop; the plan note on the main board stays `ShowWhenUnlocked`. `thriftshop-level.md` 3.5 says "pops in" instead of "fades in".
- **2026-10-22** — Forgettable (concept 7.4, "witnesses observe you at reduced confidence") scales the job's merged police description: `JobWitnesses` fades the merged report by `PlayerSkills.WitnessEvidenceFactor` (0.8 with the skill) before it is shown and saved. Individual witnesses keep their full look; only what reaches the police is reduced. Earlier records keep their strength, and recognition reads the reduced record.
- **2026-10-22** — Thrift shop R1 and R2 done and checked (`WitnessReport.Match` with EditMode tests, `Observation.recognition`, the cap in `NpcSuspicion`). Decision 25 changed: civilians recognise too (customers and the shop clerk 0.6, the guard 0.8), and a burned outfit, two or more worn parts the record is sure of (color 60 %, style 50 %), alerts on sight with a recognition weight of at least 0.9 for every NPC that recognises. `WitnessReport.BurnedCount`, `JobWitnesses.IsBurned` and `Observation.burned` added, with four more EditMode tests (thrift shop 6, R2b).
- **2026-10-22** — Recognition has no ceiling (decision 25 changed again): on its own it can take an NPC to Alerted, as fast as match × weight allows; burned outfits only raise the weight. `Observation.burned` removed again. The burned rule moved into `WitnessReport` (`IsBurned`, `BurnedColorAt`, `BurnedStyleAt`, `BurnedPartsToKnow`) so the main menu's `WardrobeConcept` uses it too: its BURNED label and police profile now show the leading part per slot with color and style confidence apart, instead of their average, which read 78 % and 75 % for parts the game rated 65 % and 59 %. `WitnessReport.DescribeTop` and `FeatureBelief.GetRanked` went with the old display. Archetype weights are now guard and clerk 1, customers 0.9.
- **2026-10-22** — Thrift shop R2b (burned outfits) and C3 (the witness slice, M6 step 2) checked and passed.
- **2026-10-22** — Thrift shop L5 done and checked; `thriftshop-level.md` 3.4 changed to match. The tools are `PlayerSkills`' gear, read from the save's `Gear_*` flags, so the `PlayerTools` placeholder is dropped; the hammer is bought, never owned from the start. `GlassCutterInteraction.glassTier` decides the case: tier 1 (smash tables) opens with the hammer or the glass breaker, tier 2 (`LootDisplay.prefab`) with the glass cutter. A smash's `NoiseEvent` comes from the player's position: published at the glass, a metre up inside the case's carved NavMesh hole, it lay beyond `NpcHearing`'s 1 m search for walkable floor and no guard heard it. A source that makes a sound must publish it within 1 m of the NavMesh.
- **2026-10-22** — Thrift shop L6 done and checked: smash tables (`DisplayTable.prefab`), four in the level with their own `LootSpawner` (1–2 live), each holding two cases (`ThriftCase` 500, or `JewelryCase` 1,500 one time in 15 on three tables), opened by holding E with the hammer or glass breaker. `LootSlot` gains dormant content, so a table that is not live stays as decor; the new `LootChoice` (Core: `Even`, `ByValue` at 1 / value, or `Weighted`) picks what every display and table holds, in its own `Awake`. `GlassCutterInteraction.enableOnDone` is a list, which also stops four of a display's five items being taken through the glass. A tier-1 smash hold is `Stealing` conduct.
- **2026-10-22** — Thrift shop glass tools changed (merged from the other branch; `thriftshop-level.md` 3.4, L5): tier-1 glass needs the hammer, and the glass breaker is an upgrade bought after it that shortens the smash hold (2.5 s to 1.5 s) and halves the noise; the shop sells hammer → glass breaker → glass cutter as a chain, and the new Better cutter skill widens the cutting minigame's cone. `ProgressUnlock`'s numbers were shifted by the merge and restored: a new flag takes the next free bit (`Skill_BetterCutter` is `1 << 17`).
- **2026-10-25** — Decision 26 added: only the player's floor is shown in buildings with several floors (jewelry store first, the bank next). New in Core: `BuildingFloors`, `BuildingFloor`, `FloorVolume`, `FloorStairs`, `FloorHideable` (Volumes) and `RendererFade` (Rendering). The ground floor over the lower floor fades fully, the rest to half; the player's own floor's wall tops fade out; stairs fade by the player's height, doors and the garage over 0.5 s; props and NPCs on another floor fade out fully, and `NpcVisionCone` and `NpcSuspicionIndicator` fade with their NPC. Props, NPC prefabs and floor slabs moved to `OccluderPropMaterial` (and Occluder versions of the cash and plan materials) in every level. The jewelry store model gained floor roots, wall-top groups and lower-floor wall copings, and its ground floor is cut at the lower floor's east wall.
- **2026-10-25** — OccluderWall's player fade (`OccluderFadeMask`) gains a depth limit: a fragment fades only when it is closer to the camera than the fade target, measured along the camera's forward direction (with the existing `Softness` as the falloff). Before, the mask worked on the ground plane only, so it also faded geometry below and behind the player, such as the lower floor's walls seen from the ground floor. The fade target under the player was moved to suit it. Material values unchanged.
- **2026-10-29** — Double doors: `Door` gains an optional `secondHinge` that swings the mirrored angle, so a double door is one door with one lock, anomaly and entrance. The jewelry store's `EntranceLeft` and `EntranceRight` are merged into `MainEntrance`, whose `Entrance` opens into `MainEntranceArea` and is entrance 2 in the `JobLayout`.
