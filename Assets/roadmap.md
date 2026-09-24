# Suspect Unknown — Roadmap

Companion to `heist-game-concept.md`. That document is _what_ the game is; this one is _what order to build it in and what to do when you fall behind_.

**Emil Djurson** (emidj236) · **Marcus Skoglund** (marsk090)

> Rewritten for the 3D low-poly version with a fixed 65–70° camera. Supersedes the 2D roadmap.

---

## How to read this

Phases are ordered by **dependency and risk**, not by how interesting they are. Percentages are shares of your total available time, not weeks. Map them onto your real deadline once you know it.

Three phases are marked **GATE** (1, 4, and 9), and Phases 3 and 6 carry a lighter gate check. A gate means: do not start the next phase until it is honestly passed, even if the next phase is more fun to build. Each gate exists because failing it late is expensive and failing it early is free.

**What changed from the 2D plan:** Phase 1 is now the highest-risk phase rather than a formality, because camera occlusion can sink the whole project. Art and animation get their own phase. Content shrinks from five tiers to three, because 3D content costs roughly double.

---

## Phase 0 — Setup (3%)

- Unity 6 LTS, 3D URP project
- Git repo, Unity `.gitignore`, **Git LFS configured for models, textures, and audio before the first asset lands**. In 3D this matters far more than it did in 2D.
- Project Settings → Editor → Asset Serialization: **Force Text**; Version Control Mode: **Visible Meta Files**
- Install: Unity AI Navigation, Cinemachine, Input System, Newtonsoft JSON, PrimeTween, ProBuilder, Animation Rigging
- Folder structure and assembly definitions agreed before either of you writes gameplay code
- **Decide the palette and the poly budget now.** Changing art direction after 40 assets exist is painful.
- Pick and download a base asset pack (Kenney CC0 or Synty) to use as placeholder from day one. Never work against grey boxes longer than necessary.

**Done when:** anyone on the team can clone, open, and hit play without setup steps.

---

## Phase 1 — GATE: camera and readability (10%)

**This is now the riskiest phase in the project.** Everything downstream assumes the player can see themselves and judge sightlines. Prove it before building anything.

- Fixed-rotation Cinemachine camera at 65–70° pitch, with damping and a confiner
- **Build both orthographic and narrow-FOV perspective, and compare with real art in frame.** Pick one and commit.
- **Dither-fade occluder shader** in Shader Graph: sphere mask on the camera-to-player vector, applied to any geometry between the two
- A greyboxed room in ProBuilder using **low interior walls** (waist to chest) with full-height exterior only
- Third-person character controller with stance: stand, crouch. Wall-flatten is deferred: add it later only if playtests show crouching behind low walls is not enough cover.
- Mixamo animations retargeted onto a base character

**Done when:** you can walk through a full room, crouch behind a counter, and at no point lose sight of your character or misjudge where you are.

**Gate:** show it to someone who has not seen it. If they ever ask "where am I?", the occlusion solution is not done. Fix it here, not later.

---

## Phase 2 — Perception in three dimensions (9%)

- 3D vision cones with height, occluded by geometry
- **Multi-point body raycasts** (head, torso, feet) so partial cover yields partial detection. This is the mechanical payoff of going 3D and must work correctly.
- Cone projected onto the floor so it is visible from this camera angle
- NavMeshSurface bake, guard patrol via NavMesh, investigation of a last-known position
- **Light volumes** as triggers, tagged by level, with a debug readout of the player's current light level
- Hearing propagated through the navmesh rather than through walls
- Head look-at via Animation Rigging so NPCs visibly turn toward noise

**Done when:** crouching behind a half-height counter reliably breaks the ray to your head while the ray to your feet still lands, and the resulting partial detection is visible in the debug readout.

---

## Phase 3 — One complete job (11%)

- Suspicion accumulation and the `Idle → Curious → Investigating → Alerted` state machine
- Loot interaction with hold-to-take timers, carry weight, a bag
- Getaway vehicle, extraction, a results screen
- Basic fail state

**Done when:** you can enter, steal, and leave, and you can also get caught.

The prototype job uses a guard. Tier 1's only opposition is civilians, whose behaviour does not exist until Phase 5, so tier 1 content is built after that.

**Gate check:** play it ten times. Is there ever tension? If not, the Curious state is almost certainly too short. Lengthen it before doing anything else.

---

## Phase 4 — GATE: is it fun for thirty seconds? (11%)

**The critical phase. No persistence exists yet and none should.**

- **Three verbs**: throw an object to pull a patrol, trip a breaker to darken a zone, jam a door. Each must visibly move an NPC or change what an NPC perceives.
- **Vault and climb** via NavMeshLink, so cover is traversable rather than just an obstacle
- **The greed ratchet**: escalating value at escalating exposure. Register fast, back office safe 40s, display case loud. Leaving early with a partial take is always allowed.
- Audio on every interaction. Placeholder is fine; silence is not.
- Footstep audio by surface and stance.

**Done when:** someone outside the team plays three jobs and wants a fourth.

**Hard gate.** If the loop is flat here, adding persistence will not fix it, it will only delay the discovery. If you are behind schedule, cut from Phase 9 and 10 and spend the time here.

---

## Phase 5 — Civilian reactions (8%)

- Decision tree evaluation with the inputs from concept section 6.8
- **The decision window** with visible deliberation, carried primarily by body language: reaching for a phone, backing away, freezing
- Three outcomes first (call, flee, walk away), one player counter-action during the window
- Panic contagion with distance falloff and a **capped cascade depth**
- Two civilian archetypes with telegraphed idle behaviour

**Done when:** a player can look at a room and correctly guess which civilian is the dangerous one.

---

## Phase 6 — Modular characters and the witness model (12%)

The art side and the systems side of the same feature. Build them together.

- **Modular character rig**: base mesh with head/torso/legs/hands sockets, shared skeleton, animations retargeting across all outfits
- Three to five wardrobe pieces as swappable meshes with material tints
- Feature slots, per-observer per-feature visibility masks, the quality function including `f_stance`
- Confusion matrices so low-quality witnesses report wrong values
- Log-odds merge across witnesses at job end
- **The Witness Report screen** (the newspaper). Headline feature for the game course; budget real art time.

**Done when:** a player can predict who saw them well and who did not, purely from how the encounter played out.

**Gate check:** show the report to someone who has played one job. Do they conclude unprompted that they should change clothes? If not, the screen is not communicating.

---

## Phase 7 — Meta layer (9%)

- Save/load via JSON: police profile, wardrobe, money, per-location state
- Persistent profile with burn status per item
- **The 3D suspect lineup screen** — a mannequin assembled from confirmed features, unknown slots left as grey geometry. Nearly free given the modular character work in Phase 6, and it communicates the whole mechanic wordlessly.
- Laundering, shop, two to three tools
- Pre-job briefing board

**Done when:** burning an item forces a real purchasing decision.

---

## Phase 8 — Adaptive security (11%)

The AI course deliverable.

- Job logging: entry point, zone transitions, loot order, tools, detection events, vertical routes used
- Markov transition model over **zones** with recency decay and Laplace smoothing; Dirichlet over entry points
- Forward propagation to a predicted occupancy distribution
- Fixed-budget greedy max-coverage placement over **points of interest** and camera mounts, with softened selection and a solvability check. Guards patrol by serving POIs whose coverage drains over time, so the optimiser's output for guards is which POIs exist and how fast each one drains (its decay multiplier). Total drain is the budget: more than the guards can cover and every POI empties.
- **Intel change log** generated from the marginal-gain step, phrased in POI terms: "guards now check the back door more often, because you used it on your last three jobs"

**Done when:** you can hit the same store four times the same way and watch coverage visibly migrate onto your route, with the change log explaining why.

**For the report:** keep the Markov model as a baseline even if you later add a neural route predictor. A baseline-versus-model comparison with an ablation is most of what the AI course wants, and it costs almost nothing once logging exists.

---

## Phase 9 — GATE: poisoning pays off (6%)

- Decoy wardrobe pieces, visibly marked in the shop
- Verify by hand that three front-door jobs followed by a rear-door score is measurably easier
- Tune budget size and decay rate until it is. Poisoning works through POI weights: repeated use of a decoy entry raises that POI's decay multiplier and pulls guard time away from the real target. Recency decay in the model lets old habits fade, so the player can change strategy.

**Gate:** if a planned deception produces no noticeable advantage, the budget is too large or decay too slow. This is a tuning problem, not a design problem. Do not abandon the mechanic.

---

## Phase 10 — Content and tiers (8%)

**Reduced from five tiers to three.** 3D locations cost roughly double their 2D equivalents.

- Tier 2: jewelry store with a patrolling guard and a **mezzanine** (first verticality)
- Tier 3: bank branch with cameras, locked zones, two floors
- Guard archetype with behavioural feature observation
- Cameras with fixed arcs, mounted height, and visual-only features
- One skill line fully implemented beats three half-implemented

---

## Phase 11 — Polish (10%)

Requirement 1 is graded here. Reserve this time and do not let it get eaten.

- Main menu, pause, options (audio sliders, resolution, rebinding), save slots
- Tutorial that teaches by design rather than text
- Lighting pass: bake lightmaps, place fixtures, pool light and shadow deliberately
- Camera feel: damping, a push on alert, a freeze frame on being spotted
- Adaptive music tied to alert state
- Consistent art pass, no placeholder assets in the final build
- Controller support (a fixed-camera 3D game suits it well)
- Playtest with at least five people who have not seen it, and fix what they trip on

---

## Phase 12 — Report and presentation (2%)

- Markov baseline vs any learned model: comparison, learning curves, ablation
- Written justification for decision trees vs learned models, different problems and different tools
- The (1 − 1/e) guarantee on greedy placement
- Recorded demo footage as a fallback for a live demo failing

---

## System boundaries

Split the work by system boundary rather than by file, and agree the interfaces between systems before building either side.

**The two sides:**

| Systems                                   | World and experience                           |
| ----------------------------------------- | ---------------------------------------------- |
| Perception, cones, multi-point rays       | Camera, occlusion shader, character controller |
| Suspicion, state machines, decision trees | Modular character rig, Mixamo retargeting      |
| Witness model and evidence merge          | Witness Report and suspect lineup screens      |
| Markov model, placement optimizer         | Level design in ProBuilder, lighting           |
| Save/load, job logging                    | Shop, wardrobe, skill UI                       |
| AI course report                          | Audio, feel, polish pass                       |

Interfaces to agree up front:

- `IPerceiver` → what an observation event (`Observation`) contains. Named to avoid colliding with `System.IObserver<T>`.
- `JobLog` → what gets recorded per job
- `SecurityPlan` → what the optimiser hands back to the level loader
- `CharacterAppearance` → how outfit state maps to both the rendered mesh set and the witness feature vector. **Get this one right early**; it is the seam between art and systems.

Making a system legible is part of building it, not a later step. The most common failure here is an invisible system that the rest of the game does not know exists.

---

## Weekly cadence

- **Start of week:** agree in writing what this week's gate looks like. One sentence per system area.
- **Midweek:** integrate. Branches should not live longer than about three days. Unity scene merges get worse the longer you wait, and 3D scenes are worse than 2D ones.
- **End of week:** make a build, and have one person outside the team play it for ten minutes. Write down what confused them and explain nothing while they play.

The team will be blind to its own UI by week three, and the witness and adaptive-security systems are exactly the kind of thing that feels obvious to whoever wrote it and invisible to everyone else.

---

## Risk register

| Risk                                              | Mitigation                                                                                  |
| ------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| Camera occlusion makes the game unreadable        | Phase 1 gate. Low walls plus dither fade, prototyped before content exists.                 |
| 3D asset workload overruns the schedule           | Asset packs and Mixamo from day one; greybox-first; three tiers instead of five             |
| Phase 4 is not fun                                | Find out in month one, not month three                                                      |
| Modular character system fights the animation rig | Build outfits and retargeting together in Phase 6; one shared skeleton, no exceptions       |
| Adaptive security makes levels unwinnable         | Solvability check after placement; cap changes per job                                      |
| Coverage-driven patrols look random to the player  | Deterministic POI scoring, commitment until covered, POIs visible in debug. See decision 15 in `roadmap_npc.md`. |
| Poisoning turns out to be pointless               | Phase 9 gate, tuned against one level before content exists                                 |
| Witness system is invisible to players            | Report and lineup screens are presentation work, not programming work. Budget them as such. |
| Verticality doubles level cost                    | One floor until tier 2                                                                      |
| Art inconsistency reads as unfinished             | Fixed palette, one asset pack as the visual baseline, no mixing sources                     |

---

## Cut order

If you run out of time, cut in this order. Cut content before depth. One location where security visibly adapts beats three where it does not.

1. Controller support
2. Skill lines (keep gear)
3. Second floors and vertical routes (keep the mezzanine, cut the rest)
4. Identity reset
5. Neural route predictor (the Markov chain is sufficient and defensible)
6. Tier 3 bank level
7. Panic contagion (keep individual reactions)
8. Third civilian archetype

**Never cut:** the occlusion solution, the Witness Report screen, the suspect lineup, the intel change log, the decision window, or the polish phase. Those are what the two courses are actually grading.
