# Suspect Unknown — NPC & AI Roadmap

Companion to `roadmap.md` and `heist-game-concept.md`. Those cover the whole game; this one covers the NPC slice only: guards, civilians, clerks, and cameras, and everything they perceive, decide, and remember. It records the architecture decisions made so far, why they were made, and the order to build in.

**Owner:** Emil (the systems side of the ownership split in `roadmap.md`). Integration seams with Marcus have their own section.

> Written 2026-09-21 after the base scaffolding commit (`8f8bd04`), and revised the same day after cross-checking it against `roadmap.md` and the concept document. See the changelog at the end. Update the status column in the file tables as files get implemented.

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

| Phase | Done when (NPC share)                                                                                                                          |
| ----- | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| 2     | Crouching behind a half-height counter breaks the ray to the head while the ray to the feet still lands, and the partial detection shows in the debug readout. |
| 3     | You can get caught, and there is tension before you are. The Curious state lasts long enough to react to.                                      |
| 4     | **GATE.** Every verb (thrown object, breaker, jammed door) visibly moves an NPC or changes what it perceives.                                   |
| 5     | A player can look at a room and correctly guess which civilian is the dangerous one.                                                           |
| 6     | A player can predict who saw them well and who did not, purely from how the encounter played out.                                             |
| 10    | Guards read behaviour. Cameras see only within their arc and from their mounted height.                                                        |

---

## Architecture decisions

### 1. Data in ScriptableObjects, behaviour in components

Guards, civilians, and clerks do not differ only in detection rate. They differ on two separate axes:

| Axis                                                                                                                   | Guard vs civilian       | Tool                              |
| ---------------------------------------------------------------------------------------------------------------------- | ----------------------- | --------------------------------- |
| **Numbers** — view distance, FOV, observer skill, suspicion rise and decay, conduct weights, memory retention, feature-visibility mask, hearing sensitivity | Same code, other values | `NpcArchetype` ScriptableObject   |
| **Reaction** — patrol, investigate, pursue vs a decision tree                                                          | Different code entirely | A separate MonoBehaviour per role |

**Rule:** if code ever branches on which archetype it is (`if (archetype.isGuard)`), that branch belongs in a component, not in the ScriptableObject.

**Rule:** never write runtime state into a ScriptableObject. The asset is shared by every NPC using it and keeps its values between play sessions in the editor. Current suspicion, last-known position, and timers live on the MonoBehaviour.

### 2. Composition over inheritance

Every NPC prefab is a set of shared perception components plus exactly one behaviour component. There is no `Npc` base class with `Guard` and `Civilian` subclasses.

The proof that this works: **a camera is perception plus witness, with no suspicion, no behaviour, no NavMesh agent, and no hearing.** If adding cameras in Phase 10 requires touching guard code, perception has leaked into the guard.

### 3. Three independent state axes

| Axis                        | Values                                                                                                         | Driven by                   | Owner                             |
| --------------------------- | -------------------------------------------------------------------------------------------------------------- | --------------------------- | --------------------------------- |
| **Stance** (posture)        | Standing, Crouching, WallFlatten                                                                               | Input                       | Player (`PlayerStance`)           |
| **Alert** (concept 6.1)     | Idle → Curious → Investigating → Alerted                                                                       | The suspicion float         | Every NPC with `NpcSuspicion`     |
| **Activity** (what they do) | Civilian: Roaming, Deliberating, Calling, Fleeing … Guard: Patrolling, Investigating, Pursuing, Reporting …   | Decision tree / FSM         | Per role, as separate enums       |

These do not replace each other. A civilian can be `Alerted` and `Calling` at the same time; a guard who is `Investigating` is still standing. Collapsing them into one enum produces a combinatorial explosion of states. Keep three fields.

**Reporting is an activity, not an alert level.** A guard radioing in and a civilian making a discreet call are things NPCs *do* once alerted, so they live in `GuardActivity` and `CivilianActivity`. The ladder stops at Alerted, as in `roadmap.md` Phase 3.

`Roaming` (civilian wandering) and `Patrolling` (guard following a route) mean different things, which is why guard and civilian activities are two enums rather than one shared one.

**Stance is posture only.** `PlayerStance` currently has `Sprinting` and lacks `WallFlatten`. Sprinting is a speed, and concept 6.1 treats running as a noise source rather than a posture, so it moves out of `Stance` and becomes a conduct flag (decision 6). `WallFlatten` is required by concept 6.1 and by the Phase 1 done-when ("move along a wall"). Both changes are to Marcus's controller code, and `Stance.Sprinting` is used by `PlayerMotor` and `PlayerNoise`, so this is listed as an integration seam rather than done unilaterally.

### 4. Where types live, and which way dependencies point

Gameplay code is split into four assemblies, with dependencies pointing one way only:

```
NPC  ──►  Player  ──►  Core
 └──────────────────►  Core
```

- **Core** holds what both sides need and nobody owns: `NoiseBus`, `NoiseEvent`, `Zone`, `LightVolume`. Core references no gameplay assembly. Zones and light volumes are passive: readers query them, they never look up the player.
- **Player** owns every type NPCs read about the player: `Stance`, `PerceptionTargets`, `PlayerConduct`.
- **NPC** reads Player and Core. Nothing reads NPC except scenes and tests.

Within NPC, an enum used by two or more NPC systems lives in `Scripts/Data/`; an enum owned by one system lives in that system's folder. That is why `NpcAlertState` is in `Data/` while `CivilianActivity` is in `Civilian/`.

If NPCs ever need a posture of their own (a civilian crouching under a desk), they get a separate `NpcStance`. NPC crouching is a behaviour output while player crouching is input, and sharing the enum would fake a symmetry that is not there.

### 5. The player exposes what NPCs perceive about it

Two player-owned components carry everything perception needs, so NPC code never reaches into the player's internals:

- `PerceptionTargets` exposes head, torso, and feet transforms. NPC perception casts to them. NPC code never guesses bone names, and it keeps working when `PlayerStance` shrinks the capsule.
- `PlayerConduct` exposes what the player is doing right now: sprinting, carrying loot, using a tool, and so on as features land. See decision 6.

### 6. Suspicion comes from conduct, not from being seen

Concept 6.1: suspicion accumulates "while you are perceived doing something **abnormal**". Visibility alone never raises it. This is what makes social stealth (concept 6.7), the work uniform (concept 7.2), and "being seen is not failure" (concept 4) possible.

Abnormality is judged by the NPC from three inputs:

1. **Conduct** — from `PlayerConduct`: sprinting, carrying loot, using a tool.
2. **Context** — from the `Zone` the player is in: public or staff. Identical movement is fine in a public aisle and abnormal in a staff corridor, unless the player is in uniform.
3. **Archetype** — per-conduct weights in `NpcArchetype`. A guard weighs conduct differently from a customer.

The suspicion rise per tick is roughly `visibility × abnormality`. The same conduct-and-context reading feeds two other consumers, so it is computed once and carried on the `Observation`:

- the witness **Behaviour** feature slot (concept 6.2: "moved abnormally, entered staff area"), and
- decision-tree input 1, **severity** (concept 6.8: moving oddly is not a crowbar in a display case).

### 7. Perception is stateless; two components accumulate, separately

`NpcPerception` answers "what can I see right now, and what is the player doing" once per tick and nothing else. Two different things accumulate over time, in two different places:

- **`NpcSuspicion`** — how alarmed this NPC is. Rises with abnormal conduct in view, decays otherwise.
- **`NpcWitness`** — how well this NPC remembers the player. This is where `f_duration` lives: concept 6.2 defines it as a factor in *evidence quality* ("a glance and a ten-second stare must not produce the same evidence"), not in alarm.

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

---

## File structure

Status as of this revision:

- **stub** — Unity template, no logic
- **declared** — the type exists with no members (enums have their values)
- **working** — implemented
- **—** — does not exist yet

```
Assets/
├── Core/Scripts/
│   ├── Noise/          NoiseBus, NoiseEvent
│   └── Volumes/        Zone, LightVolume
├── Player/Scripts/
│   ├── Locomotion/     PlayerStance …
│   ├── Noise/          PlayerNoise
│   └── Perception/     PerceptionTargets, PlayerConduct
└── NPC/
    ├── Art/Guard/Animation Clips/   guard_run_forward.fbx
    ├── Archetypes/                  ScriptableObject instances (.asset)
    └── Scripts/
        ├── Data/
        ├── Perception/
        ├── Guard/
        ├── Civilian/
        └── Witness/
```

### NPC/Archetypes/

| Asset                  | Notes                                                                    | Status | Milestone |
| ---------------------- | ------------------------------------------------------------------------ | ------ | --------- |
| `SecurityGuard.asset`  | Observer skill 1.0, long retention, behaviour features visible           | —      | M1        |
| `Civilian.asset`       | Observer skill 0.6, no behaviour reading                                 | —      | M1+       |
| `TiredClerk.asset`     | Observer skill 0.4, fast memory decay, obvious visual features only      | —      | M1+       |
| `SecurityCamera.asset` | Visual features only, no memory decay                                    | —      | M8        |

Create these from `Assets > Create > Scriptable Objects > NpcArchetype` once the class has fields. Each asset gains values as its milestone adds a block to `NpcArchetype`. Observer skill values are from concept 6.2.

### NPC/Scripts/Data/

| File                   | Type                    | Purpose                                                           | Status   | Milestone     |
| ---------------------- | ----------------------- | ----------------------------------------------------------------- | -------- | ------------- |
| `NpcArchetype.cs`      | ScriptableObject        | Every tuning number for one archetype                             | stub     | M1, M2, M4, M7 |
| `NpcAlertState.cs`     | enum                    | Idle, Curious, Investigating, Alerted                             | declared | M2            |
| `Observation.cs`       | struct                  | What a perceiver reports each tick it sees the player             | declared | M1, M2        |
| `IPerceiver.cs`        | interface               | Shared by NPC perception and cameras                              | declared | M1            |
| `FeatureSlot.cs`       | enum                    | Witness feature slots                                             | declared | M7            |
| `FeatureVisibility.cs` | `[Serializable]` struct | Per-slot visibility mask for one observer                         | declared | M7            |

### NPC/Scripts/Perception/

| File                    | Type          | Purpose                                                   | Status | Milestone |
| ----------------------- | ------------- | --------------------------------------------------------- | ------ | --------- |
| `NpcPerception.cs`      | MonoBehaviour | Vision cone, per-point linecasts, quality factors, conduct reading | stub | M1, M2 |
| `NpcPerceptionDebug.cs` | MonoBehaviour | Gizmos, floor cone, on-screen readout                     | stub   | M1        |
| `NpcSuspicion.cs`       | MonoBehaviour | Rise from abnormal conduct, decay, alert-state events     | stub   | M2        |
| `NpcHeadLook.cs`        | MonoBehaviour | Animation Rigging look-at toward what the NPC noticed     | stub   | M3        |
| `NpcHearing.cs`         | MonoBehaviour | `NoiseBus` subscriber, NavMesh path-length check          | stub   | M4        |

### NPC/Scripts/Guard/

| File                 | Type          | Purpose                                                         | Status   | Milestone |
| -------------------- | ------------- | --------------------------------------------------------------- | -------- | --------- |
| `GuardBehaviour.cs`  | MonoBehaviour | FSM, `NavMeshAgent`, last-known position                        | stub     | M3        |
| `GuardActivity.cs`   | enum          | Patrolling, Investigating, Pursuing, Reporting, ReturningToPost | declared | M3        |
| `PatrolRoute.cs`     | MonoBehaviour | Scene waypoints, drawn as gizmos                                | stub     | M3        |
| `SecurityMonitor.cs` | MonoBehaviour | Relays camera observations to the guard posted at it            | —        | M8        |

### NPC/Scripts/Civilian/

| File                   | Type             | Purpose                                                     | Status   | Milestone |
| ---------------------- | ---------------- | ----------------------------------------------------------- | -------- | --------- |
| `CivilianBehaviour.cs` | MonoBehaviour    | Idle routine, decision window, executing the outcome        | stub     | M6        |
| `CivilianActivity.cs`  | enum             | Roaming, Working, Deliberating, the reactions, PretendingNotToSee | declared | M6  |
| `CivilianRole.cs`      | enum             | Customer, Employee, Manager                                 | declared | M6        |
| `ReactionTree.cs`      | ScriptableObject | Root of a decision tree                                     | stub     | M6        |
| `ReactionNode.cs`      | ScriptableObject | Becomes abstract, with branch and leaf subclasses           | stub     | M6        |
| `PanicContagion.cs`    | MonoBehaviour    | Spreads panic with distance falloff and a depth cap         | stub     | M6        |

### NPC/Scripts/Witness/

| File                 | Type             | Purpose                                                  | Status   | Milestone |
| -------------------- | ---------------- | -------------------------------------------------------- | -------- | --------- |
| `NpcWitness.cs`      | MonoBehaviour    | Turns observations into beliefs; owns `f_duration` and memory decay | stub | M7 |
| `FeatureBelief.cs`   | class            | Log-odds belief over one feature slot                    | declared | M7        |
| `WitnessReport.cs`   | class            | One witness's statement at job end                       | declared | M7        |
| `ConfusionMatrix.cs` | ScriptableObject | Wrong-value table for one feature slot                   | stub     | M7        |

### Core/Scripts/

| File                     | Type          | Purpose                                                  | Status   | Milestone |
| ------------------------ | ------------- | -------------------------------------------------------- | -------- | --------- |
| `Noise/NoiseEvent.cs`    | struct        | Position, radius (loudness), source                      | declared | M4        |
| `Noise/NoiseBus.cs`      | static class  | Publish and subscribe                                    | declared | M4        |
| `Volumes/Zone.cs`        | MonoBehaviour | Trigger volume: zone id, public or staff. Reused by Phase 8 | —     | M2        |
| `Volumes/LightVolume.cs` | MonoBehaviour | Trigger volume tagged with a light level                 | —        | M4        |

### Player/Scripts/ (what NPCs read)

| File                               | Type          | Purpose                                            | Status  | Milestone |
| ---------------------------------- | ------------- | -------------------------------------------------- | ------- | --------- |
| `Perception/PerceptionTargets.cs`  | MonoBehaviour | Head, torso, feet transforms on the rig            | stub    | M1        |
| `Perception/PlayerConduct.cs`      | MonoBehaviour | What the player is doing right now                 | —       | M2        |
| `Locomotion/PlayerStance.cs`       | MonoBehaviour | Owns `Stance`; needs `WallFlatten`, loses `Sprinting` | working | seam   |
| `Noise/PlayerNoise.cs`             | MonoBehaviour | Movement noise; migrates to `NoiseBus`             | working | M4        |

### Prefab composition

| Prefab   | Components                                                                                                    |
| -------- | ------------------------------------------------------------------------------------------------------------- |
| Guard    | `NpcPerception`, `NpcSuspicion`, `NpcHearing`, `NpcHeadLook`, `NpcWitness`, `GuardBehaviour`, `NavMeshAgent` |
| Civilian | `NpcPerception`, `NpcSuspicion`, `NpcHearing`, `NpcWitness`, `CivilianBehaviour`, `NavMeshAgent`             |
| Clerk    | Same as Civilian with the `TiredClerk` archetype and no patrol                                               |
| Camera   | `NpcPerception`, `NpcWitness` — nothing else                                                                  |
| Monitor  | `SecurityMonitor`, linked to cameras; a guard is posted at it                                                 |

---

## Milestones

Ordered by dependency. **Perception comes before behaviour.** A guard state machine with no perception feeding it is driven by nothing and cannot be tested, however tempting it is to start there because it is "the AI".

**How this maps to `roadmap.md`.** The milestones interleave Phases 2 and 3: suspicion (M2, Phase 3) comes before hearing and light (M4, Phase 2), because it only needs M1 and it is what makes the guard in M3 testable. Phase 2 is not a hard gate, so this is allowed.

**Prototype jobs use a guard.** Phases 3 and 4 build and test the loop with a guard, but the first tier's only opposition is civilians, whose behaviour arrives in Phase 5 (M6). So the prototype job runs on the test bench with a guard, and tier 1 content is built after M6. This is a conscious choice, not an accident of ordering.

### M0 — Groundwork

- [x] Scaffolding committed (`8f8bd04`)
- [x] Git index restored after the accidental `git rm -r --cached .`
- [x] `NoiseBus` and `NoiseEvent` moved from `NPC/Scripts/Noise/` to `Core/Scripts/Noise/` (decision 4)
- [ ] Assembly definitions: Core, Player, Player.Editor, NPC, with the dependency direction from decision 4 (decision 12)
- [ ] Add `.DS_Store` to `.gitignore`, and untrack the 4 already committed with `git rm --cached` on those paths only
- [ ] Review `.gitattributes`: it has a single `filter=lfs` line for a repo that already holds FBX, `.blend`, and PNG files
- [ ] Confirm the Phase 1 gate with Marcus. No dither-fade occluder shader exists in the repo yet, and wall-flatten is not implemented, so "move along a wall" cannot pass. Perception work assumes the player can judge sightlines.
- [ ] Agree the `Stance` change with Marcus: add `WallFlatten`, move sprinting out to conduct (decision 3)
- [ ] Test bench in `Assets/Testing/TestingScene.unity`: a ProBuilder room, one waist-high counter, one stationary guard capsule. No NavMesh.
- [ ] Physics layers: `Occluder`, `Player`, and `NPC` already exist. Walls and counters go on `Occluder`; the player must not.

**Done when:** the test bench reproduces the Phase 2 scenario, even though nothing detects anything yet. Build the scenario before the code that passes it.

### M1 — Perception core (Phase 2 done-when)

1. `PerceptionTargets` — three transforms on the player rig. About fifteen lines, no dependencies, and it unblocks everything else.
2. `NpcArchetype` — the vision block only: view distance, FOV, eye height, distance and angle falloff curves. Data before logic, so step 4 is tunable from the inspector from the first frame.
3. `Observation` and `IPerceiver` — who perceived, when, per-point visibility, and each quality factor. **Reserve a field for the player's appearance snapshot now**, even though nothing fills it until M7: `roadmap.md` says `CharacterAppearance` is an interface to agree up front, and the observation is where it lands.
4. `NpcPerception` **and** `NpcPerceptionDebug`, in the same sitting rather than one after the other. A perception system you cannot see is one you cannot tune.

Implementation notes:

- Check cheap to expensive and bail at the first failure: range → FOV angle → `Physics.Linecast(eye, point, occluderMask)` per body point.
- Cast per target point, not once per NPC. Visibility is the weighted fraction of points with a clear line. That fraction *is* the partial detection Phase 2 asks for.
- Tick at about 10 Hz rather than every frame, staggered by `GetInstanceID()` so NPCs do not all spike on the same frame.
- Keep each quality factor (`f_dist`, `f_angle`, `f_light`, `f_stance`) as its own readable value rather than pre-multiplying, so the readout can print them individually. `f_light` stays at 1.0 until M4. `f_duration` is not here; it belongs to the witness (decision 7).
- The readout is on-screen text (`OnGUI` is fine for debug) showing per-point hit or miss and each factor, alongside gizmos for the cone.

**Done when:** crouching behind the half-height counter reliably breaks the ray to the head while the ray to the feet still lands, and the partial detection is visible in the debug readout.

### M2 — Conduct, suspicion, and the alert ladder (Phase 3)

- `PlayerConduct` with the conduct that exists so far (sprinting; loot and tools as Phase 3 adds them).
- `Zone` in Core, and a public/staff tag on the test bench.
- `NpcPerception` reads conduct and zone into each `Observation`, and computes abnormality using the archetype's conduct weights (decision 6).
- `NpcArchetype` suspicion block: conduct weights, rise rate, decay rate, one threshold per alert state.
- `NpcSuspicion` rises by roughly `visibility × abnormality`, decays when there is nothing abnormal in view, and raises an event on each `NpcAlertState` change.
- Use a lower threshold on the way down than on the way up, so an NPC hovering at a boundary does not flicker between states.
- Leave hooks for skills: rise and decay multipliers that something else can set later (concept 7.4 Ghost, "slower suspicion accumulation in shadow"; Face, "talk down a Curious NPC"). No skill logic here.
- A suspicion indicator above the NPC (concept 6.5, in-world telegraphing).

**Done when:** walking normally in view of a guard raises nothing; sprinting, or walking into a staff zone, fills suspicion over time rather than instantly; breaking line of sight lets it drain; and the current state is readable above the NPC. Per the Phase 3 gate check: if there is no tension, lengthen Curious before doing anything else.

### M3 — The guard (Phases 2–3)

- NavMeshSurface bake on the test bench.
- `PatrolRoute`: waypoints in the scene, drawn as gizmos.
- `GuardBehaviour` maps alert state to `GuardActivity`: Idle → Patrolling; Curious → stop and turn toward the stimulus; Investigating → walk to the last-known position; Alerted → Pursuing, and Reporting when there is someone to report to. Giving up leads to ReturningToPost.
- `NpcHeadLook` via Animation Rigging, so the guard visibly turns its head before its body. Turning toward noise follows in M4.
- Investigation must be interruptible. The near miss depends on it.
- Being caught ends the job (the Phase 3 basic fail state). Being *seen* does not (concept 9: detection must be recoverable).

**Done when:** a guard patrols, notices you doing something abnormal, pauses, walks to where you were, and gives up and returns to their route if you are gone — or catches you if you are not.

### M4 — Hearing and light (the rest of Phase 2)

- `NoiseEvent` and `NoiseBus` (in Core).
- `NpcHearing`: `NavMesh.CalculatePath` length compared against `radius × sensitivity` (decision 10). `NpcArchetype` gains a hearing-sensitivity value.
- `PlayerNoise` migrates from resizing a `SphereCollider` to publishing on `NoiseBus`.
- `NpcHeadLook` turns toward heard noise.
- `LightVolume` in Core: trigger volumes tagged with a light level. `f_light` reads the volume the player is standing in, and the debug readout shows the player's current light level. The component is ours; placing the volumes in levels is level design.

**Done when:** running behind a wall is heard around the corner but not through it, and stepping into shadow visibly lowers `f_light` in the readout.

### M5 — Verbs move NPCs (Phase 4, hard gate)

NPC-side support for the three Phase 4 verbs:

- **Thrown object** → a `NoiseEvent` → a guard leaves their patrol to investigate.
- **Breaker** → a light volume changes level → `f_light` drops for everyone looking into that zone.
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
- The per-point ray results from M1 decide which slots were actually seen: feet blocked means lower-body features go unobserved. This is what `f_stance` means in practice, and it is also why a ceiling camera sees headwear well and footwear badly.
- The **Behaviour** slot is filled from the conduct reading (decision 6), and only by observers whose mask includes it.
- `ConfusionMatrix` per feature slot. Below a quality threshold, the reported value is sampled from the confusion distribution, so a red jacket gets described as orange.
- `WitnessReport` at job end, with the log-odds merge across witnesses.
- Reactions feed evidence differently: a frozen civilian gets a long, clean look; a fleeing one gives a poor, error-prone description.
- A hook for a player-side confidence multiplier (concept 7.4 Face: "witnesses observe you at reduced confidence"). No skill logic here.
- Unit tests for the merge using the Unity Test Framework. It is pure logic and painful to debug through gameplay.

**Done when:** a player can predict who saw them well and who did not, purely from how the encounter played out.

### M8 — Cameras, trained guards, and security placement (Phases 8 and 10)

- The `SecurityCamera` prefab: `NpcPerception` and `NpcWitness` only. Visual-only feature mask, a mounted height, a fixed arc, no memory decay.
- `SecurityMonitor`: relays its cameras' observations to the guard posted at it, whose `NpcSuspicion` acts on them (decision 11).
- Guards register behavioural features that civilians never notice.
- Guard posts, patrol routes, camera mounts, and the monitor post expose which `Zone`s they cover, so the Phase 8 placement optimiser can treat them as candidates in the `SecurityPlan`.

**Done when:** cameras work without a single change to guard code, and tampering with a camera fools the guard at the monitor.

---

## Integration seams with Marcus

| Seam                        | NPC side needs                                                        | Marcus side provides                                                    |
| --------------------------- | --------------------------------------------------------------------- | ----------------------------------------------------------------------- |
| Phase 1 gate                | A camera and occlusion the player can judge sightlines through       | Dither-fade occluder, low interior walls                                |
| `Stance`                    | Posture only: Standing, Crouching, WallFlatten                        | Add `WallFlatten`; move sprinting out of `Stance` in `PlayerMotor` and `PlayerNoise` |
| Assembly definitions        | NPC → Player → Core, no cycles                                        | Agreement on the split, and the Player assembly's package references    |
| `PerceptionTargets`         | Head, torso, and feet transforms that follow the animated rig         | A rig where those bones exist on every outfit                           |
| `PlayerConduct`             | What the player is doing, readable each tick                          | Hooks from the controller and interactions (sprint, loot, tools)       |
| `CharacterAppearance`       | The player's feature vector at the moment of each observation         | Outfit state mapped to both meshes and features. **Agree this early.**  |
| Vision cone on the floor    | A debug cone during M1                                                | The final look, agreed together                                         |
| NPC animation               | Patrol, investigate, run; civilian phone reach, back away, freeze     | Mixamo retargeting onto the NPC rig                                     |
| Zones and light volumes     | The components and their debug readouts                               | Placement during level design and the lighting pass                     |
| Witness Report screen       | `WitnessReport` with per-feature confidence                           | The newspaper screen that presents it                                   |

---

## Risks

| Risk                                                        | Mitigation                                                                              |
| ----------------------------------------------------------- | --------------------------------------------------------------------------------------- |
| Building the guard FSM before perception exists             | M1 before M3. Nothing to drive an FSM means nothing to test it with.                     |
| Perception is invisible, so it cannot be tuned              | Build the debug readout alongside perception, not after it                              |
| Suspicion from mere visibility kills social stealth         | Decision 6: suspicion rises from abnormal conduct only. The M2 done-when checks it.     |
| Linecast cost grows with NPC count                          | Cheap-to-expensive checks, a 10 Hz tick, staggered per NPC                               |
| Runtime state written into a ScriptableObject               | Decision 1. State lives on components.                                                  |
| One enum trying to hold stance, alert, and activity         | Decision 3. Three fields.                                                               |
| Suspicion and evidence merged into one number               | Decision 7. Two accumulators, or calm witnesses stop being dangerous.                  |
| A dependency cycle once assembly definitions exist          | Decision 4. Shared types go in Core; Core references nothing.                           |
| The decision window buried inside tree evaluation           | Decision 9. `Deliberating` is a real, readable state.                                   |
| Panic contagion empties the building                        | Cap the cascade depth                                                                   |
| Cameras need guard code                                     | Perception has leaked into the guard. The M8 done-when catches it.                      |
| Curious is too short, so there is no tension                | The Phase 3 gate check: lengthen it before doing anything else                          |

---

## Open questions

- **Body-point weighting.** Are head, torso, and feet equal in the visibility fraction, or does the head count for more?
- **Tick rate.** 10 Hz is a starting point. Global, or per archetype?
- **Which conduct counts as abnormal, and where.** The first set of conduct flags and their weights per archetype and zone type.
- **Does `NpcStance` exist?** Only if civilians crouch or hide as part of a reaction.
- **Decision window length.** Likely 1.5–3 seconds, probably different per reaction. A playtest question.
- **Social proof.** Is the contagion broadcast enough for a civilian to know someone else is panicking, or do NPCs need to perceive each other?
- **Reading conduct through a feed.** Can the guard at a monitor react to abnormal conduct on screen? Probably yes for suspicion, while the camera's visual-only mask still keeps behaviour out of the evidence.
- **Cameras and light.** Does `f_light` apply to cameras? Probably yes, which would make the breaker a counter to them too.

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
