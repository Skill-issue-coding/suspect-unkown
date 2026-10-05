# Suspect Unknown — NPC & AI Roadmap

Companion to `roadmap.md` and `heist-game-concept.md`. Those cover the whole game; this one covers the NPC slice only: guards, civilians, clerks, and cameras, and everything they perceive, decide, and remember. It records the architecture decisions made so far, why they were made, and the order to build in.

Covers the systems side of the split in `roadmap.md`. Integration seams with the player, world, and art side have their own section.

> Written 2026-09-21 after the base scaffolding commit (`8f8bd04`), and revised the same day after cross-checking it against `roadmap.md` and the concept document. Revised again on 2026-09-23 after M1's perception core was built and the per-archetype region weights design replaced fixed cast points, and kept current through M3 (2026-09-29) and the M4 light rework (2026-10-05). See the changelog at the end. Update the status column in the file tables as files get implemented.

---

## The end goal

Every observer in a location runs on the same perception pipeline. They differ only in their data and in what they do with what they see:

- A **guard** patrols, notices, investigates a last-known position, pursues, and reads behaviour that civilians miss.
- A **civilian** roams, notices, visibly deliberates, then calls, flees, freezes, films, walks away, or pretends not to have seen — and the player can interrupt the deliberation.
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
| 4     | **GATE.** Every verb (thrown object, breaker, jammed door) visibly moves an NPC or changes what it perceives.                                                                                                            |
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
| **Activity** (what they do) | Civilian: Roaming, Deliberating, Calling, Fleeing … Guard: Patrolling, Investigating, Pursuing, Reporting … | Decision tree / FSM | Per role, as separate enums   |

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

- **Core** holds what both sides need and nobody owns: `NoiseBus`, `NoiseEvent`, `Zone`, `GameplayLight`. Core references no gameplay assembly. Zones and gameplay lights are passive: readers query them, they never look up the player.
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

### 7. Perception is stateless; two components accumulate, separately

`NpcPerception` answers "what can I see right now, and what is the player doing" once per tick and nothing else. Two different things accumulate over time, in two different places:

- **`NpcSuspicion`** — how alarmed this NPC is. Rises with abnormal conduct in view, decays otherwise.
- **`NpcWitness`** — how well this NPC remembers the player. This is where `f_duration` lives: concept 6.2 defines it as a factor in _evidence quality_ ("a glance and a ten-second stare must not produce the same evidence"), not in alarm.

A calm civilian who stares for ten seconds has low suspicion and excellent evidence. That is exactly the "calm witnesses are the dangerous ones" inversion from concept 6.8, and it only works if the two accumulators stay separate.

### 8. `IPerceiver`, not `IObserver`

`System.IObserver<T>` already exists, so the interface is `IPerceiver`, with `Observation` as its payload. `roadmap.md` has been updated to match.

### 9. The decision window is a real state

The 1.5–3 second deliberation from concept 6.8 is `CivilianActivity.Deliberating`, with a pending reaction and a visible timer on `CivilianBehaviour`. It is not a coroutine hidden inside the tree evaluation. The player has to be able to see it and interrupt it, so the rest of the game has to be able to read it.

The exception that proves the rule: `PretendingNotToSee` must look like `Working` from outside while it keeps feeding the witness. It is the one reaction whose indicator should lie.

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

What `Observation` does carry is a bitmask with one bit per region: on when at least one point in that region had a clear line. That is exactly what M7 needs, since any glimpse of the feet exposes the footwear to evidence.

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
│   └── Volumes/        Zone, ZoneType
├── Player/Scripts/
│   ├── Editor/         MapPlayerPerceptionPoints …
│   ├── Locomotion/     PlayerStance …
│   ├── Noise/          PlayerNoise
│   └── Perception/     BodyRegion, PerceptionPoint, PerceptionTargets, CharacterAppearance, PlayerConduct, PlayerLighting, PlayerLightingDebug
└── NPC/
    ├── Art/Guard/Animation Clips/   one folder per stance: Patrolling, Watching, Investigating, Pursuing, Transitions, Turn
    ├── Animators/                   GuardAnimator.controller (generated, never edited by hand)
    ├── Archetypes/                  ScriptableObject instances (.asset)
    ├── Prefabs/                     Guards/Guard.prefab, UI/OffscreenIndicator.prefab
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
| `CivilianRole.Manager.asset`           | Calm, notices more than a customer: the dangerous witness from concept 6.8 | vision block | M6        |
| `CivilianRole.DistractedShopper.asset` | Phone out, tiny cone: filler that makes a room readable                    | vision block | M6        |
| `SecurityCamera.asset`                 | Visual features only, no memory decay                                      | vision block | M8        |

Create these from `Assets > Create > Suspect Unknown > NPC Archetype`. Each asset gains values as its milestone adds a block to `NpcArchetype`. Observer skill values are from concept 6.2 and arrive with the M7 witness block.

Vision block starting values. Region weights are head / upper body / hands / lower body / feet.

| Archetype         | View distance | FOV | Eye height       | Crouch factor | Tick rate | Region weights              |
| ----------------- | ------------- | --- | ---------------- | ------------- | --------- | --------------------------- |
| SecurityGuard     | 18            | 110 | 1.7              | 0.6           | 10        | 1.0 / 1.0 / 0.8 / 0.8 / 0.6 |
| Civilian          | 12            | 120 | 1.65             | 0.5           | 5         | 1.0 / 0.8 / 0.3 / 0.2 / 0   |
| TiredClerk        | 8             | 80  | 1.6              | 0.4           | 4         | 1.0 / 0.5 / 0 / 0 / 0       |
| Manager           | 14            | 110 | 1.7              | 0.55          | 8         | 1.0 / 1.0 / 0.6 / 0.4 / 0.2 |
| DistractedShopper | 6             | 60  | 1.6              | 0.3           | 3         | 1.0 / 0.4 / 0 / 0 / 0       |
| SecurityCamera    | 20            | 70  | 0 (root at lens) | 0.8           | 10        | 1.0 / 1.0 / 0.6 / 0.6 / 0.3 |

The guard's hand weight is high because tool use is conduct guards read. The camera's feet weight is low because a ceiling camera sees headwear well and footwear badly. Lower tick rates for civilians make them slower to notice and cheaper to run.

Suspicion block starting values. Conduct weights are sprinting / in staff area.

| Archetype         | Conduct   | Rise   | Decay  | Delay  | Curious | Investigating | Alerted | Hysteresis |
| ----------------- | --------- | ------ | ------ | ------ | ------- | ------------- | ------- | ---------- |
| SecurityGuard     | 0.6 / 0.8 | 0.5    | 0.1    | 2.0    | 0.2     | 0.5           | 0.9     | 0.05       |
| Civilian          | 0.3 / 0.2 | 0.3    | 0.2    | 1.0    | 0.25    | 0.55          | 0.9     | 0.05       |
| TiredClerk        | 0.2 / 0.6 | 0.2    | 0.3    | 0.5    | 0.3     | 0.6           | 0.9     | 0.05       |
| Manager           | 0.4 / 0.9 | 0.35   | 0.15   | 1.5    | 0.2     | 0.5           | 0.9     | 0.08       |
| DistractedShopper | 0.1 / 0.1 | 0.15   | 0.4    | 0.5    | 0.3     | 0.6           | 0.95    | 0.03       |
| SecurityCamera    | 0.5 / 0.8 | unused | unused | unused | unused  | unused        | unused  | unused     |

The clerk barely notices running but is staff, so a stranger in the back room stands out. The manager cares most about the staff area. The camera has no `NpcSuspicion` (decision 11), so only its conduct weights matter: they define what the guard at the monitor reads through the feed. Its other suspicion values copy the guard's so the asset stays valid.

Hearing block starting values. Sensitivity multiplies how far a sound carries to this NPC (decision 10); suspicion is what a sound at full loudness and alarm 1 adds.

| Archetype         | Sensitivity | Suspicion |
| ----------------- | ----------- | --------- |
| SecurityGuard     | 1.0         | 0.5       |
| Civilian          | 0.8         | 0.3       |
| TiredClerk        | 0.6         | 0.2       |
| Manager           | 0.9         | 0.35      |
| DistractedShopper | 0.4         | 0.1       |
| SecurityCamera    | 0           | 0         |

The shopper is on their phone, so sound barely reaches them. The camera hears nothing (decision 2); its zeros keep the asset valid.

### NPC/Scripts/Data/

| File                   | Type                    | Purpose                                                                    | Status                      | Milestone      |
| ---------------------- | ----------------------- | -------------------------------------------------------------------------- | --------------------------- | -------------- |
| `NpcArchetype.cs`      | ScriptableObject        | Every tuning number for one archetype                                      | working (vision, suspicion) | M1, M2, M4, M7 |
| `RegionWeights.cs`     | `[Serializable]` struct | Per-region attention weights for one observer (decision 13)                | working                     | M1             |
| `ConductWeights.cs`    | `[Serializable]` struct | Per-flag abnormality weights and the combine formula (decision 6)          | working                     | M2             |
| `NpcAlertState.cs`     | enum                    | Idle, Curious, Investigating, Alerted                                      | declared                    | M2             |
| `Observation.cs`       | struct                  | What a perceiver reports each tick it sees the player                      | working (M1, M2 fields)     | M1, M2         |
| `IPerceiver.cs`        | interface               | `Perceived` event and `TryGetLatest`, shared by NPC perception and cameras | working                     | M1             |
| `FeatureSlot.cs`       | enum                    | Witness feature slots                                                      | declared                    | M7             |
| `FeatureVisibility.cs` | `[Serializable]` struct | Per-slot visibility mask for one observer                                  | declared                    | M7             |

### NPC/Scripts/Perception/

| File                    | Type          | Purpose                                                                                          | Status       | Milestone |
| ----------------------- | ------------- | ------------------------------------------------------------------------------------------------ | ------------ | --------- |
| `NpcPerception.cs`      | MonoBehaviour | Vision cone, per-point linecasts, quality factors, conduct reading, light from `PlayerLighting`  | working      | M1, M2, M4 |
| `NpcPerceptionDebug.cs` | MonoBehaviour | Gizmos, floor cone, per-point rays, on-screen readout                                            | working      | M1        |
| `NpcSuspicion.cs`       | MonoBehaviour | Rise from strength × abnormality, delayed decay, alert states with hysteresis, skill multipliers | working      | M2        |
| `NpcHeadLook.cs`        | MonoBehaviour | Animation Rigging head and neck aim; executes `LookAt`/`ClearLook`, the role behaviour decides   | working      | M3        |
| `NpcHearing.cs`         | MonoBehaviour | `NoiseBus` subscriber, NavMesh path-length check                                                 | stub         | M4        |

### NPC/Scripts/Guard/

| File                 | Type          | Purpose                                                                                                                             | Status  | Milestone |
| -------------------- | ------------- | ----------------------------------------------------------------------------------------------------------------------------------- | ------- | --------- |
| `GuardBehaviour.cs`  | MonoBehaviour | Activity from alert state, POI selection and claims, last-known position, search, pursuit, catch, body rotation, head-look requests | working | M3        |
| `GuardActivity.cs`   | enum          | Patrolling, Watching, Investigating, Pursuing, Reporting (Reporting unused until M8)                                                | working | M3        |
| `GuardAnimation.cs`  | MonoBehaviour | Guard-only animator parameters: Watching, Searching, GiveUp, and idle and give-up variant picks                                     | working | M3        |
| `PointOfInterest.cs` | MonoBehaviour | Coverage that fills while a guard is near and drains by importance; static registry (decision 15)                                   | working | M3        |
| `SecurityMonitor.cs` | MonoBehaviour | Relays camera observations to the guard posted at it                                                                                | —       | M8        |

### NPC/Scripts/Civilian/

| File                   | Type             | Purpose                                                           | Status   | Milestone |
| ---------------------- | ---------------- | ----------------------------------------------------------------- | -------- | --------- |
| `CivilianBehaviour.cs` | MonoBehaviour    | Idle routine, decision window, executing the outcome              | stub     | M6        |
| `CivilianActivity.cs`  | enum             | Roaming, Working, Deliberating, the reactions, PretendingNotToSee | declared | M6        |
| `CivilianRole.cs`      | enum             | Customer, Employee, Manager                                       | declared | M6        |
| `ReactionTree.cs`      | ScriptableObject | Root of a decision tree                                           | stub     | M6        |
| `ReactionNode.cs`      | ScriptableObject | Becomes abstract, with branch and leaf subclasses                 | stub     | M6        |
| `PanicContagion.cs`    | MonoBehaviour    | Spreads panic with distance falloff and a depth cap               | stub     | M6        |

### NPC/Scripts/UI/

| File                           | Type          | Purpose                                                                                              | Status  | Milestone |
| ------------------------------ | ------------- | ---------------------------------------------------------------------------------------------------- | ------- | --------- |
| `NpcSuspicionIndicator.cs`     | MonoBehaviour | World-space suspicion telegraph above the NPC: filling arc, state icon, arrow to last-known position | working | M2        |
| `OffscreenIndicatorHUD.cs`     | MonoBehaviour | Screen-space layer for edge indicators, one per scene; static `Instance`                             | working | M2        |
| `OffscreenIndicatorElement.cs` | MonoBehaviour | Draws one edge-of-screen indicator; placed by `NpcSuspicionIndicator`                                | working | M2        |

### NPC/Scripts/Animation/ and Core/Animator/

| File                                   | Type          | Purpose                                                                            | Status  | Milestone |
| -------------------------------------- | ------------- | ---------------------------------------------------------------------------------- | ------- | --------- |
| `Animation/NpcAnimator.cs`             | MonoBehaviour | Feeds `Speed` (agent velocity) and `TurnRate` (yaw change) to every NPC's animator | working | M3        |
| `Core/Animator/NpcAnimatorParams.cs`   | static class  | Parameter names every NPC shares: `Speed`, `TurnRate`                              | working | M3        |
| `Core/Animator/GuardAnimatorParams.cs` | static class  | Guard-only parameters and variant counts                                           | working | M3        |

### NPC/Scripts/Editor/ and Core/Editor/ (animator builders)

| File                                  | Type            | Purpose                                                                                         | Status  | Milestone |
| ------------------------------------- | --------------- | ----------------------------------------------------------------------------------------------- | ------- | --------- |
| `Editor/GuardLocomotionBuilder.cs`    | static class    | Rebuilds `GuardAnimator.controller` from `GuardClips`: Locomotion, Watching, Searching, GiveUp  | working | M3        |
| `Editor/GuardClips.cs`                | readonly struct | Every guard clip path, grouped into stances. The one place to edit when clips change            | working | M3        |
| `Core/Editor/AnimatorBuildUtility.cs` | static class    | Shared builder helpers: load and measure clips, trees, states, interruptible transitions, reset | working | M3        |
| `Core/Editor/ClipSet.cs`              | readonly struct | One stance: interchangeable clips and the parameter that picks between them                     | working | M3        |
| `Core/Editor/Gait.cs`                 | readonly struct | One moving speed: a clip and its speed measured from root motion                                | working | M3        |
| `Core/Editor/TurnSet.cs`              | readonly struct | Left and right turn clips and their measured turn rates                                         | working | M3        |

### NPC/Scripts/Witness/

| File                 | Type             | Purpose                                                             | Status   | Milestone |
| -------------------- | ---------------- | ------------------------------------------------------------------- | -------- | --------- |
| `NpcWitness.cs`      | MonoBehaviour    | Turns observations into beliefs; owns `f_duration` and memory decay | stub     | M7        |
| `FeatureBelief.cs`   | class            | Log-odds belief over one feature slot                               | declared | M7        |
| `WitnessReport.cs`   | class            | One witness's statement at job end                                  | declared | M7        |
| `ConfusionMatrix.cs` | ScriptableObject | Wrong-value table for one feature slot                              | stub     | M7        |

### Core/Scripts/

| File                     | Type          | Purpose                                                                                                                    | Status   | Milestone |
| ------------------------ | ------------- | -------------------------------------------------------------------------------------------------------------------------- | -------- | --------- |
| `Animation/Dangle.cs`    | MonoBehaviour | Procedural pendulum for small hanging accessories (the guard's keys). No physics; runs after the animated pose             | working  | M3        |
| `Noise/NoiseEvent.cs`    | struct        | Position, radius (loudness), source                                                                                        | declared | M4        |
| `Noise/NoiseBus.cs`      | static class  | Publish and subscribe                                                                                                      | declared | M4        |
| `Volumes/ZoneType.cs`    | enum          | Public, Staff. Public first, so a forgotten zone is harmless                                                               | working  | M2        |
| `Volumes/Zone.cs`        | MonoBehaviour | Trigger volume on the `Volume` layer, public or staff; several colliders make complex shapes. Zone id arrives with Phase 8 | working  | M2        |
| `Lighting/GameplayLight.cs` | MonoBehaviour | Marks a Point or Spot light as one gameplay counts; static registry and unshadowed contribution at a point (decision 17) | working  | M4        |

### Player/Scripts/ (what NPCs read)

| File                                  | Type           | Purpose                                                                                                  | Status   | Milestone |
| ------------------------------------- | -------------- | -------------------------------------------------------------------------------------------------------- | -------- | --------- |
| `Perception/BodyRegion.cs`            | enum           | Head, UpperBody, Hands, LowerBody, Feet. No explicit values: the region bitmask indexes by `(int)region` | working  | M1        |
| `Perception/PerceptionPoint.cs`       | MonoBehaviour  | One body point: its region, and its collider's bounds centre                                             | working  | M1        |
| `Perception/PerceptionTargets.cs`     | MonoBehaviour  | Every body point, flat and by region. Fixed after `Awake`                                                | working  | M1        |
| `Perception/CharacterAppearance.cs`   | struct         | Placeholder for the player's feature vector. Shape to agree at the art and systems seam                  | declared | M7        |
| `Editor/MapPlayerPerceptionPoints.cs` | static class   | Adds `PerceptionPoint` to the 17 bone colliders in `Player.prefab` by bone name                          | working  | M1        |
| `Perception/ConductFlags.cs`          | `[Flags]` enum | Sprinting, InStaffArea. Powers of two; a flag is added only with its producer                            | working  | M2        |
| `Perception/PlayerConduct.cs`         | MonoBehaviour  | What the player is doing right now, including the staff-area flag from zone triggers, counted per zone   | working  | M2        |
| `Perception/PlayerLighting.cs`        | MonoBehaviour  | Light level at the player, sampled from every `GameplayLight` on a tick with shadow rays (decision 17)   | working  | M4        |
| `Perception/PlayerLightingDebug.cs`   | MonoBehaviour  | On-screen light readout and a level-tinted sphere at the sample point; computes nothing itself           | working  | M4        |
| `Locomotion/PlayerStance.cs`          | MonoBehaviour  | Owns `Stance`: Standing and Crouching only                                                               | working  | done      |
| `Noise/PlayerNoise.cs`                | MonoBehaviour  | Movement noise; migrates to `NoiseBus`                                                                   | working  | M4        |

### Prefab composition

| Prefab   | Components                                                                                                                                                                                                                                                             |
| -------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Guard    | `NpcPerception`, `NpcPerceptionDebug`, `NpcSuspicion`, `NpcHeadLook`, `GuardBehaviour`, `GuardAnimation`, `NpcAnimator`, `NavMeshAgent`; the model has an Animator and a `RigBuilder` with head and neck aim constraints. `NpcHearing` joins in M4, `NpcWitness` in M7 |
| Civilian | `NpcPerception`, `NpcSuspicion`, `NpcHearing`, `NpcWitness`, `CivilianBehaviour`, `NavMeshAgent`                                                                                                                                                                       |
| Clerk    | Same as Civilian with the `TiredClerk` archetype and no patrol                                                                                                                                                                                                         |
| Camera   | `NpcPerception`, `NpcWitness` — nothing else                                                                                                                                                                                                                           |
| Monitor  | `SecurityMonitor`, linked to cameras; a guard is posted at it                                                                                                                                                                                                          |

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
3. [x] `Observation` and `IPerceiver` — who perceived, when, the player's position, visibility, the region bitmask, and each quality factor. The appearance field is reserved with a placeholder `CharacterAppearance` struct, even though nothing fills it until M7.
4. [x] `NpcPerception` **and** `NpcPerceptionDebug`, in the same sitting rather than one after the other. A perception system you cannot see is one you cannot tune.

Implementation notes, as built:

- Check cheap to expensive and bail at the first failure: range → FOV angle → `Physics.Linecast(eye, point, occluderMask, QueryTriggerInteraction.Ignore)` per body point. Triggers are ignored so `Zone` volumes never block sight.
- The FOV check flattens both the NPC's forward and the direction to the player onto the horizontal plane. Without that, a player standing close in front reads as outside the cone, because the player's root is at their feet and the eye is 1.65 m up.
- Cast to every point in each attended region (decision 13). Visibility is the weighted average of region fractions. That _is_ the partial detection Phase 2 asks for.
- Tick at the archetype's tick rate (10 Hz for guards), staggered in eight buckets by a static spawn counter. `GetInstanceID()` is not used: its `int` conversion is deprecated in current Unity.
- Keep each quality factor (`fDistance`, `fAngle`, `fLight`, `fStance`) as its own readable value rather than pre-multiplying, so the readout can print them individually. `fLight` stayed at 1.0 until M4 (decision 17). `f_duration` is not here; it belongs to the witness (decision 7).
- `IPerceiver` exposes results only: the `Perceived` event on every tick the player is seen, and `TryGetLatest` for the last tick. Consumers never trigger a cast.
- The debug view never casts a ray of its own. Rays are coloured per point (clear, blocked, or grey for an ignored region) and the readout shows each region's fraction, coloured from red to green.

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
- [x] A suspicion indicator above the NPC (concept 6.5, in-world telegraphing). As built: a world-space canvas that copies the camera rotation, hidden at zero so a calm room shows nothing. A filling arc shows suspicion, and an icon shows the state (`?`, `?!`, `!`). The arc rolls in the screen plane to point at the **last-known** position from the latest observation, not the live player, so it never leaks where the player really is, and it shows where M3's guard will investigate. Displayed values are smoothed, since perception ticks at 10 Hz. When the NPC is off screen, an edge indicator on a screen-space HUD takes over: it sits on the screen border on the line toward the NPC, points at it, and shows the same icon, colour, fill, and fade, so the player knows someone off screen is suspicious.

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

- `NoiseEvent` and `NoiseBus` (in Core).
- `NpcHearing`: `NavMesh.CalculatePath` length compared against `radius × sensitivity` (decision 10). `NpcArchetype` gains a hearing-sensitivity value.
- `PlayerNoise` migrates from resizing a `SphereCollider` to publishing on `NoiseBus`.
- `NpcHeadLook` turns toward heard noise.
- [x] Light from the scene's lights (decision 17). `GameplayLight` in Core, `PlayerLighting` samples it, and `NpcPerception` turns the level into `fLight` through `NpcArchetype.LightFactor`. `PlayerLightingDebug` shows the level, the raw sum, and lit and shadowed light counts, with a sphere at the sample point tinted by level. Putting `GameplayLight` on the right lamps is level design. `LightVolume` was replaced before any level used it.

Implementation notes for light, as built:

- `GameplayLight.Contribution` checks cheap to expensive: light disabled, then range by `sqrMagnitude`, then falloff, then the spot cone by comparing cosines. It returns raw Unity intensity units; only `PlayerLighting` normalises.
- A spot light with equal inner and outer angles is a hard edge, not zero: `Mathf.InverseLerp` returns 0 when its ends are equal, so that case is handled separately.
- `PlayerLighting` runs the shadow ray only after a light passes the cheap contribution check, so out-of-range lights cost no raycast. The gizmo calls the same `IsShadowed`, so the debug view cannot disagree with gameplay.
- A missing `PlayerLighting` warns and reads as full light rather than disabling the NPC: full light is a safe default, unlike a missing stance.

**Done when:** running behind a wall is heard around the corner but not through it, and stepping into shadow visibly lowers `f_light` in the readout.

**Status:** the light half passed on the test bench: shadow lowers `fLight`, a wall between lamp and player blocks it, and switching a lamp off drops `fLight` within a tick.

### M5 — Verbs move NPCs (Phase 4, hard gate)

NPC-side support for the three Phase 4 verbs:

- **Thrown object** → a `NoiseEvent` → a guard leaves their patrol to investigate.
- **Breaker** → the zone's `GameplayLight` lamps switch off → `f_light` drops for every NPC looking at a player there (decision 17). Breaker lamps must be Mixed or Realtime.
- **Jammed door** → `NavMeshObstacle` carving → an investigating guard takes the long way round.

Plus: guards pursue across vault and climb routes via `NavMeshLink`, and the near miss is designed rather than accidental — suspicion decays, investigation is interruptible, and the player always has a recovery move.

**Done when:** each verb visibly moves an NPC or changes what an NPC perceives. The overall Phase 4 gate is that someone outside the team plays three jobs and wants a fourth; if the loop is flat here, fix it here rather than moving on to civilians.

### M6 — Civilians (Phase 5)

- `ReactionTree` and `ReactionNode` as ScriptableObjects, with `ReactionNode` split into branch and leaf subclasses, so archetypes are data rather than code.
- Tree inputs from concept 6.8, in priority order: **severity** (from the conduct reading in decision 6), role, whether they think the player saw them, social proof, distance to the player and to an exit, and personality only as a tiebreaker.
- Coarse branching in the tree, weighted scores at the leaves, then sample softly instead of taking the top result.
- The decision window as the `Deliberating` state: a pending reaction, a timer, an indicator showing which way the civilian is leaning, and body language (reaching for a phone, backing away, freezing) as the primary signal.
- Three outcomes first — call, flee, walk away — and one player counter-action during the window. `PretendingNotToSee` and the rest follow.
- `PanicContagion` with distance falloff and a capped cascade depth. Panic publishes a `NoiseEvent`, so the path from one frightened civilian to an alerted guard is traceable.
- Two civilian archetypes whose personality reads from their idle behaviour.

**Done when:** a player can look at a room and correctly guess which civilian is the dangerous one.

### M7 — The witness model (Phase 6)

- `FeatureSlot` and `FeatureVisibility` filled in. `NpcArchetype` gains its witness block: observer skill, feature mask, and **memory retention** (the clerk forgets fast, the guard retains longer, the camera never forgets — concept 6.2).
- `NpcWitness` consumes each `Observation` into one `FeatureBelief` per slot, combined in log-odds. It accumulates `f_duration` per encounter and applies memory decay between encounters.
- Quality is the full product from concept 6.2: `f_dist · f_angle · f_light · f_duration · f_stance · observerSkill · featureVisibility`.
- The region bitmask from M1 decides which slots were actually seen: feet blocked means lower-body features go unobserved. This is what `f_stance` means in practice, and it is also why a ceiling camera sees headwear well and footwear badly. The region fractions on `NpcPerception` can later scale quality per slot, if a glimpse and a full view need to differ.
- The **Behaviour** slot is filled from the conduct reading (decision 6), and only by observers whose mask includes it.
- `ConfusionMatrix` per feature slot. Below a quality threshold, the reported value is sampled from the confusion distribution, so a red jacket gets described as orange.
- `WitnessReport` at job end, with the log-odds merge across witnesses.
- Reactions feed evidence differently: a frozen civilian gets a long, clean look; a fleeing one gives a poor, error-prone description.
- A hook for a player-side confidence multiplier (concept 7.4 Face: "witnesses observe you at reduced confidence"). No skill logic here.
- Unit tests for the merge using the Unity Test Framework. It is pure logic and painful to debug through gameplay.

**Done when:** a player can predict who saw them well and who did not, purely from how the encounter played out.

### M8 — Cameras, trained guards, and security placement (Phases 8 and 10)

- The `SecurityCamera` prefab: `NpcPerception` and `NpcWitness` only. Visual-only feature mask, a mounted height, a fixed arc, no memory decay.
- A 3D view cone for cameras. `NpcPerception` flattens forward for the FOV check, so camera pitch is ignored. A camera pointing straight down has a zero flattened forward, `Vector3.Angle` returns 0, and the camera sees all 360°. Until then, mount test cameras at an angle.
- `SecurityMonitor`: relays its cameras' observations to the guard posted at it, whose `NpcSuspicion` acts on them (decision 11).
- Guards register behavioural features that civilians never notice.
- POIs, camera mounts, and the monitor post expose which `Zone`s they cover, so the Phase 8 placement optimiser can treat them as candidates in the `SecurityPlan`. For POIs the optimiser sets presence and `DecayMultiplier` (decision 15).

**Done when:** cameras work without a single change to guard code, and tampering with a camera fools the guard at the monitor.

---

## Integration seams

| Seam                     | NPC side needs                                                    | Player, world, and art side provides                                                                                     |
| ------------------------ | ----------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| Phase 1 gate             | A camera and occlusion the player can judge sightlines through    | Dither-fade occluder, low interior walls                                                                                 |
| `Stance`                 | Posture only: Standing, Crouching                                 | Done. Sprinting is `PlayerMotor.IsSprinting`                                                                             |
| Assembly definitions     | NPC → Player → Core, no cycles                                    | Agreement on the assembly split, and the Player assembly's package references                                            |
| `PerceptionTargets`      | Body points that follow the animated rig, grouped by region       | A trigger `BoxCollider` on each mapped bone, on every outfit. Renaming a bone means updating `MapPlayerPerceptionPoints` |
| `PlayerConduct`          | What the player is doing, readable each tick                      | Hooks from the controller and interactions (sprint, loot, tools)                                                         |
| `CharacterAppearance`    | The player's feature vector at the moment of each observation     | Outfit state mapped to both meshes and features. **Agree this early.**                                                   |
| Vision cone on the floor | A debug cone during M1                                            | The final look, agreed together                                                                                          |
| NPC animation            | Patrol, investigate, run; civilian phone reach, back away, freeze | Mixamo retargeting onto the NPC rig                                                                                      |
| Zones                    | The component and its debug gizmo                                 | Placement during level design                                                                                            |
| Gameplay lights          | `GameplayLight` and `PlayerLighting` with their debug readouts    | During the lighting pass: `GameplayLight` on every lamp that should hide or reveal, shadows on, fixtures off `Occluder`, breaker lamps Mixed or Realtime |
| Witness Report screen    | `WitnessReport` with per-feature confidence                       | The newspaper screen that presents it                                                                                    |

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
| Panic contagion empties the building                                      | Cap the cascade depth                                                                                                                                                                                       |
| Cameras need guard code                                                   | Perception has leaked into the guard. The M8 done-when catches it.                                                                                                                                          |
| Curious is too short, so there is no tension                              | The Phase 3 gate check: lengthen it before doing anything else                                                                                                                                              |
| An array in `Observation` shared between copies                           | Decision 14. Debug detail stays on `NpcPerception`.                                                                                                                                                         |
| An archetype with empty curves or all-zero weights is blind with no error | `OnValidate` restores empty curves and warns on all-zero weights                                                                                                                                            |
| A camera pointing straight down sees 360°                                 | M8 adds a 3D cone for cameras. Mount test cameras at an angle.                                                                                                                                              |
| A player prefab rework drops a perception component                       | `NpcPerception` warns when `PlayerConduct` is missing, but suspicion then silently never rises. Consider `[RequireComponent(typeof(PlayerConduct))]` on `PerceptionTargets`, so Unity refuses to remove it. |
| A visible shadow does not hide the player                                 | Decision 17. Usually a lamp without `GameplayLight`, with shadows off, or with a fixture on `Occluder`. Select the player and read the gizmo lines: green is lit, red is shadowed.                         |
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
- **Social proof.** Is the contagion broadcast enough for a civilian to know someone else is panicking, or do NPCs need to perceive each other?
- ~~**Reading conduct through a feed.**~~ Answered by decision 6: yes for suspicion. The camera's own conduct weights define what the monitor guard reads. The camera's visual-only mask still keeps behaviour out of the evidence (M7).
- ~~**Cameras and light.**~~ Answered by decision 17: yes. Cameras use `NpcPerception`, so `f_light` applies to them automatically, and the breaker counters them too. The camera archetype's `darkVisibilityFactor` sets how much.
- **Baked lights in builds.** Not yet verified in this Unity version: does a Baked light keep its `Light` component and parameters in a player build? `GameplayLight` assumes it does.

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
