# Suspect Unknown — Concept & Technical Document

3D low-poly stealth heist game with a fixed steep isometric camera (65–70° pitch). Unity. Joint project across TDDD23 (game project) and an AI course.

**Emil Djurson** (emidj236) · **Marcus Skoglund** (marsk090)

> Working document. The handed-in project plan is the summary; this is the implementation detail behind it.
> Supersedes the earlier 2D top-down version of this document.

---

## 1. Pitch

You are a thief working your way up. You start by hitting convenience stores for pocket change and end by robbing a bank. You need to be stealthy because everyone who sees you might remember something, and the police are slowly assembling a description of you and your methods. Security learns your routes, your entrances, and your tools between jobs, and spends its budget specifically against you.

The opposition is not a difficulty slider. It is a system that watches what you do and reacts to it, both within a job and across your entire career. The interesting play is not avoiding that system but feeding it the wrong information.

**Non-lethal by design.** No guns, no killing. Getting caught means losing the score and burning your identity, not dying. This keeps tension high, keeps scope sane, and avoids building a violent game for a university project.

**Reference points:** Shadow Tactics and Desperados III for the camera, vision cones, and readability. Payday for the fantasy and the progression. Monaco for the heist loop.

---

## 2. The camera, and why it changes the design

A fixed camera pitched 65–70° down, low-poly models, no free orbit. The angle is a good choice and worth understanding why.

**What you gain over top-down 2D:**

- **Real occlusion in three dimensions.** Crouching behind a counter genuinely breaks line of sight. Cover becomes a mechanic instead of a wall-hugging abstraction.
- **Silhouette reads at a glance.** Clothing, headwear, body shape, and carried tools are visible on the model, which makes the witness system legible without UI. The player can *see* what a witness would describe.
- **Verticality.** Mezzanines, vents, second floors, rooftops. Each is a route the security model has to account for.
- **Cheap art.** Flat-shaded low-poly is fast to produce and reads as intentional. A two-person team can build a consistent-looking 3D world faster than a consistent-looking pixel-art one.

**What it costs, and the single biggest technical risk in the project:**

At 65–70° the camera is steep, which helps a lot, but walls between the camera and the player still occlude. This must be solved in Phase 1 or the entire game is unplayable. Options, in order of recommendation:

1. **Low interior walls** (waist to chest height) with full-height only on the building exterior. Solves most of it by construction and suits the art style.
2. **Dither-fade occluders.** Any geometry between camera and player fades to a stipple pattern via a shader with a sphere mask. Standard technique, one shader, works everywhere.
3. **Camera rotation in 90° snaps.** Lets the player look behind a wall deliberately. Adds a lot of level-design cost since every room must read from four angles.

Recommend 1 + 2 together, and skip 3 unless playtesting demands it.

**Projection:** orthographic, or perspective with a narrow FOV (~25–30°). Orthographic gives the cleaner tactical read and makes vision cones easier to judge. Try both in Phase 1 and decide with the actual art in frame.

**Fixed rotation.** Do not give the player free camera orbit. It destroys the ability to design readable sightlines, and it makes vision cones much harder to judge. A fixed angle is a constraint that pays for itself.

---

## 3. Art direction

- **Flat-shaded low poly**, no textures or minimal ones. Colour comes from vertex colours or per-material palettes. This is the cheapest path to a consistent look.
- **Limited palette**, chosen up front and never deviated from. Consistency reads as intentional style; mixed fidelity reads as unfinished. This matters more for the game course grade than polygon count does.
- **Readable silhouettes over detail.** A guard must be distinguishable from a civilian at a glance from this camera height. Use colour and silhouette, not facial detail.
- **Characters are modular**: a base body with attachment sockets for head, torso, legs, and a carried item. Clothing becomes mesh swaps plus material tints, which is what makes the wardrobe system cheap to build and makes the suspect lineup screen possible.
- **Lighting sells the mood.** Baked lightmaps for the static world plus a handful of realtime lights. Dark corners, pooled light under fixtures, harsh light at entrances.

---

## 4. Core loop

1. **Plan** — pick a target, review what the police already know about you, choose a disguise and loadout.
2. **Case** — enter the location, observe patrols, camera arcs, staff routines, and where the value is.
3. **Steal** — take as much value as possible while managing suspicion.
4. **Escape** — reach the getaway vehicle. Being seen is not failure; being *identified* is the cost.
5. **Aftermath** — witnesses give statements, your police profile updates, security at that location adapts.
6. **Spend** — launder the take, buy gear, skills, and new clothing. Repeat with a bigger target.

The tension in the loop is greed versus exposure. Every extra thirty seconds inside is more value and more observation.

---

## 5. Progression tiers

Three tiers, reduced from five because 3D locations cost roughly double (see `roadmap.md` Phase 10). Each tier introduces exactly one new opposition concept so the player learns them one at a time. Verticality lets each tier also add a spatial dimension.

| Tier | Target | New opposition | New space |
|---|---|---|---|
| 1 | Convenience store | Civilians: a tired clerk (poor memory, decays fast) and a few customers giving independent partial descriptions. | Single room, low shelving, stockroom door |
| 2 | Jewelry store | Patrolling guard. Trained observer, reads behaviour. | Mezzanine (first verticality), security office above |
| 3 | Bank branch | Cameras, watched from a security monitor. Perfect retention, fixed arcs, visual features only. Locked zones. | Two floors, vault stairwell |

Folded or cut from the five-tier plan: the pharmacy tier's civilians move into tier 1, and the vault tier's alarm timers and guards sharing information become stretch goals. Roof access, vents, and service shafts are no longer tied to a tier; add them where a level wants them, and note that they are third in the `roadmap.md` cut order.

Value scales, but the real difficulty curve is the number of independent observers and the number of routes security has to cover.

---

## 6. The AI systems, and playing against them

Three systems on three timescales. This layering is what makes the project defensible for the AI course.

**The central design principle:** the player must be able to attack these systems, not merely comply with them. A model the player only reacts to produces chores. A model the player can deceive produces a cat-and-mouse game. Sections 6.6 and 6.7 are where that happens, and they are not optional extras.

### 6.1 Perception and suspicion — seconds

Every NPC has:

- **Vision** — a 3D cone with a height component, occluded by geometry. Sphere-cast or raycast to several points on the player's body rather than one. This is what makes crouching behind a half-height counter work: the ray to your head is blocked, the ray to your feet is not, and partial visibility yields partial detection strength.
- **Hearing** — a radius, triggered by running, breaking glass, drilling, dropped items. Propagated through the navmesh rather than through walls, so sound goes around corners instead of through them.
- **Suspicion** — a float accumulating while you are perceived doing something abnormal, decaying when you are not.

State machine:

```
Idle → Curious → Investigating → Alerted
```

Reporting — a guard radioing in, a civilian making a call — is something an NPC *does* once alerted, not a further level of suspicion, so it lives with the reactions in 6.8 rather than on this ladder.

The **Curious** state is where the game lives. Detection must not be binary. A guard who pauses, turns, and walks toward where you were gives the player a chance to react, and that moment is the whole genre.

**Light from the lamps, not from the rendered image.** Do not read pixels back from the screen. Instead, compute the light level at the player from the scene's actual lights: distance falloff, spot cone, and a shadow ray per lamp, summed and normalised, with an ambient floor. Lamps that count are marked as gameplay lights. Because the number comes from the same lamps the player sees, a visible shadow always hides you, and a breaker darkens a zone simply by switching its lamps off. An earlier plan used hand-placed trigger volumes tagged with a light level; those drift from the real lighting every time a lamp moves. See decision 17 in `roadmap_npc.md`.

**Cover and stance.** Standing, crouching, and flattening against a wall change your detection profile and your movement noise. With a steep camera this reads clearly, and it gives the player a constant meaningful choice.

### 6.2 Witness model — per job

**The centerpiece mechanic.**

When an NPC perceives you, it does not record "the player." It records a **feature vector with per-feature confidence**:

| Feature | Example values |
|---|---|
| Headwear | beanie, cap, hood, none |
| Upper body | jacket colour, hoodie, coat |
| Lower body | jeans, work trousers, colour |
| Face covering | mask, balaclava, scarf, none |
| Build / height | short, average, tall |
| Gait | limping, fast, casual |
| Behaviour | moved abnormally, avoided eye contact, entered staff area |
| Method | picked lock, drilled, smashed glass, roof entry |

3D helps here: every one of these visual features corresponds to a visible mesh or material on the character, so what the witness reports and what the player saw on screen are the same thing.

Observer types differ meaningfully:

- **Tired clerk** — low base confidence, fast decay, obvious visual features only.
- **Civilian** — moderate visual observation, no behavioural reading, observes poorly under stress.
- **Security guard** — trained. Registers behavioural features civilians never notice, retains longer.
- **Camera** — perfect retention, zero decay, visual features only, and only within its arc and at its mounted height. A camera has no suspicion of its own: in-job alarms come from a guard watching the feed at a security monitor. With nobody at the monitor it still records evidence, but raises no alarm.

#### Implementation: Bayesian evidence accumulation, not machine learning

There is no ground truth to train against, so this is a designed model. Each feature slot holds a belief distribution over values; each observation is a noisy report; reports combine in log-odds. Roughly 50 lines, calibrated numbers you can print onto the intel screen, sensible with two witnesses or twenty.

Observation quality is a product of independent factors:

```
q = f_dist · f_angle · f_light · f_duration · f_stance · observerSkill · featureVisibility
```

- `f_dist` — falloff, clamped, tuned by feel rather than realism
- `f_angle` — how centred the player was in the cone, and whether the NPC was facing them
- `f_light` — from the light level at the player at the time, sampled from the scene's lights (6.1)
- `f_duration` — accumulated across frames. A glance and a ten-second stare must not produce the same evidence. This factor matters most.
- `f_stance` — crouching behind cover occludes lower-body features specifically. Partial visibility should produce partial descriptions.
- `observerSkill` — per archetype: tired clerk ~0.4, civilian ~0.6, guard 1.0
- `featureVisibility` — per-observer, per-feature mask. A camera is 1.0 on visual, 0.0 on behaviour. A ceiling camera sees headwear well and footwear badly.

**Let witnesses be wrong.** Below a quality threshold, sample the reported value from a confusion distribution rather than reporting the truth, so a red jacket gets described as orange. Store confusion matrices per feature slot in a ScriptableObject. Cheap to build, and it buys a great deal: low-quality witnesses become actively misleading, corroboration becomes meaningful, the post-job report becomes fun to read, and section 6.6 becomes possible at all.

Mid-job outfit changes work automatically, since each report keys on the appearance at the moment of observation.

### 6.3 Police profile — career-long

Resolved features accumulate into a persistent profile. Once a feature is confirmed:

- Wearing that item again means being recognised faster, at greater distance, by anyone.
- The item is **burned** and must be discarded or replaced.
- Enough confirmed features and you cross a heat threshold that forces an expensive identity reset or triggers harder policing.

**Present it as a 3D suspect lineup.** Because characters are modular, the profile screen can render a mannequin assembled from confirmed features, with unknown slots left as featureless grey geometry that fills in as evidence accumulates. This is a strong visual hook, it is nearly free given the modular character system, and it communicates the entire mechanic without text.

### 6.4 Adaptive security — between jobs

Do not use a learned model here. Frequency counts with recency weighting are tunable, explainable, and being able to explain exactly why security did what it did is an asset when you present.

Log per job: entry point, room/zone transitions, loot order, tools used, time of day, detection events and by whom, and which vertical routes were used.

Between jobs each location spends a **security budget** against your logged tendencies:

- Always enter through the rear door → alarm contact or a better lock.
- Always take the same internal route → a patrol re-routed to cross it.
- Always drill the safe → a reinforced safe, or a guard posted nearby.
- Always come in through the roof → the roof hatch gets a sensor, and a camera covers the stairwell.
- Hit the same store repeatedly → more staff, fewer civilians (fewer bodies to hide among, more trained eyes).

Some budget goes to a global pool, so hitting many *different* jewelry stores raises awareness across the category.

#### The budget is fixed — the most important rule in the design

The budget grows slowly with how often and how recently a location has been hit, but at any moment it is finite and must be fully allocated. Fortifying the front means the rear gets thinner. A guard watching the loading dock is not watching the showroom. Verticality makes this sharper: covering the roof costs coverage on the ground floor.

If coverage could simply grow everywhere, adaptation is pure punishment and deception pays nothing. Scarcity on their side is the entire reason 6.6 works. NPC attention is finite too, so a guard investigating a noise is genuinely not looking at anything else.

#### Implementation: predict, then optimize

Two separate stages, two different kinds of problem.

**Stage A — route prediction.** First-order Markov chain over **zones** rather than grid cells. 3D levels partition naturally into named rooms and floors, which makes the state space small, interpretable, and easy to display. Transition counts `C[prev][next]`, decayed ~0.8 per job so recent behaviour dominates, Laplace smoothing so unseen transitions are non-zero. A separate Dirichlet-multinomial over entry points. Seed at the entries and propagate forward T steps to get a predicted occupancy distribution over zones.

Build this even if a neural network replaces it later. A network outputs the same thing, drops into the same slot, and this gives you the baseline you need for the report.

**Stage B — placement.** A maximum coverage problem. Precompute per level which zones each candidate guard post, patrol route, or camera mount observes. Then:

```
Score(S) = Σ_zones  occupancy[zone] × detectionProb[zone | S]
```

Greedy selection: repeatedly add the candidate with the largest marginal gain until the budget runs out. The objective is submodular, so greedy carries the standard (1 − 1/e) approximation guarantee, worth a paragraph in the report.

Greedy has a second benefit: because it picks one candidate at a time for one identifiable reason, the intel change-log text generates itself from the marginal-gain step. *"Camera installed over the stairwell — used in 6 of your last 8 jobs."*

**Two guardrails:**

- **Do not let it play optimally.** Sample from the top few candidates rather than the argmax, and cap how many placements change per job. A perfectly countering security system is unreadable and miserable.
- **Verify solvability.** After placement, check that a path to loot and back still exists with a non-trivial success chance. If not, drop the lowest-gain placement and re-check. Optimizers will happily make your game impossible.

### 6.5 Making it visible

These systems are worthless to both courses if the player cannot perceive them.

- **Pre-job briefing board** — the 3D suspect lineup, plus confirmed items flagged as burned in your wardrobe.
- **Intel change log** — what changed at this location and why, generated from the placement step.
- **Post-job witness report** — the newspaper screen. Each witness, what they saw, how confident. This teaches the entire witness system without a tutorial and is a headline feature for the game course.
- **In-world telegraphing** — vision cones projected onto the floor, suspicion indicator above the NPC, audible cue on state change, a clear visual for how lit you are.

### 6.6 Poisoning the model — the core counterplay

Security learns from what it observes, so let the player feed it false data deliberately. This is what turns adaptation from an obstacle into the main strategic layer.

- **Feed false entry habits.** Three cheap jobs through the front door. Security invests in fronts. Then take the bank through the rear, which is thin because the budget is fixed.
- **Build a decoy identity.** Wear a distinctive red jacket on low-value jobs until the profile confirms it. That confirmation is an asset you paid for. Burn the jacket before the real score and witnesses describe someone who does not match the suspect on file.
- **Feed false method signatures.** Drill a safe loudly on a small job. They buy reinforced safes. Come back with a glass cutter and take the display case.
- **Plant wrong data.** Deliberately let a *low-quality* witness see you badly, so an incorrect value enters the profile with high confidence. The police are now looking for the wrong person.

Consequences:

- **Cheap jobs stop being grind.** A convenience store is how you shape the model before a score. A deliberate "burn job" becomes a real strategic action with a real cost.
- **The profile screen becomes the primary strategic interface.**
- **The player programs the AI rather than dodging it.** That is the cat-and-mouse loop.

### 6.7 Moment-to-moment verbs

The strategic layer does not save the second-to-second game. Removing guns leaves a hole that must be filled or the optimal play becomes standing still.

**The rule: every tool should either move an NPC or change what an NPC can perceive.** Tools that only make the player quieter are passive and forgettable.

- Throw an object to pull a guard off a patrol point
- Trip a breaker to darken a zone
- Jam or lock a door so an investigating guard takes the long way
- Tamper with a camera so it shows an empty aisle
- Knock and hide
- Non-lethal takedown, with somewhere to hide the body
- **Stance and cover** — crouch, vault a counter, flatten against a wall
- **Vertical routes** — vents, service stairs, roof hatches. These are verbs too, and they are the ones the security model most visibly reacts to.
- **Social stealth** — walking normally in a public area is safe; identical movement in a staff corridor is not

**The greed ratchet.** The most reliable tension source in the genre is the decision to stay one more room. Make value escalate visibly in risk: register is quick, back office safe costs 40 seconds of exposure, display case is loud. Let the player leave at any time with what they have.

**Design the near miss deliberately.** Guards should regularly almost catch the player and then turn away. That requires suspicion to decay, investigation to be interruptible, and the player to have a recovery move.

**Pacing rule:** introduce counters at the same rate as adaptations. Ideally each new security measure ships alongside the tool that beats it.

### 6.8 Civilian reactions — decision trees

When an NPC's suspicion crosses the threshold, a decision tree resolves what they do. Trees are the right tool: explainable, hand-tunable, and easy to justify alongside the route model since they solve a different problem.

#### Inputs

Personality alone reads as random because the player cannot see it. Branch primarily on things the player influences or can observe:

1. **Severity of what was witnessed** — the tree root. Seeing someone move oddly is not the same as seeing someone with a crowbar in a display case.
2. **Role** — an employee has a duty and a manager; a customer has nothing to gain. Uniform makes this visible.
3. **Do they think you saw them?** Being noticed noticing pushes hard toward flight or compliance.
4. **Social proof** — is anyone else already panicking? This is where contagion enters the tree rather than sitting beside it.
5. **Distance to the player, and distance to an exit** — nobody flees past the threat.
6. **Personality traits** as tiebreaker, not driver. Two axes suffice: self-preservation versus civic duty.

#### Outcomes, and the evidence each produces

Reactions must differ in what they feed the witness model, so the reaction system connects to 6.2 instead of running parallel to it.

| Reaction | Evidence produced |
|---|---|
| Freeze / comply | Long, calm, high-quality observation. The most dangerous outcome for the player. |
| Discreet call | Starts a police timer. Phone is visible, so it is interruptible. |
| Panic flee | Poor, error-prone description. Loud. Spreads. |
| Avoid / walk away | Almost nothing. |
| Pretend not to see | Keeps working, keeps observing secretly. |
| Film the player | Camera-quality visual evidence. Must be stopped. |
| Confront | Rare. A physical obstacle. |

**The resulting inversion: calm witnesses are the dangerous ones.** Scaring a civilian degrades their description but adds noise and alarm. Keeping them calm is quiet but gives them a long clean look at you. A real strategic decision rather than a preference.

#### The decision window

**The single most important mechanic in this section.** Do not resolve the tree instantly. Show a few seconds of visible deliberation, with an indicator above the NPC showing which way they are leaning. During that window the player can act: intimidate to force compliance, break line of sight, retreat, cover the loot, knock the phone away.

This turns being seen from binary failure into a playable moment, which is exactly what the loop needs without shooting. It also makes the tree legible, because the player watches the decision happen.

In 3D this is much stronger than it would be in 2D: body language carries it. A civilian reaching for a phone, backing away, or freezing is readable from animation alone, so the indicator becomes reinforcement rather than the whole signal.

#### Telegraph personality before it matters

The fidgety one, the one already on their phone, the bored employee, the one wearing headphones. If these read at a glance from idle animations, casing a store becomes about identifying which civilians are threats. Civilians become part of the puzzle rather than hazards.

#### Implementation notes

- Tree for coarse branching, weighted score at the leaves, then **sample softly** rather than taking the top result. Pure determinism becomes a lookup table; pure randomness feels unfair.
- **Panic contagion** as a value spreading to nearby NPCs with distance falloff and a cap, re-evaluating their tree at a threshold. Cap cascade depth or one trigger empties the building.
- Panic generates noise, which feeds guard perception. The path from one frightened civilian to an alerted guard should be traceable and visible.
- Author trees as ScriptableObjects so archetypes are data rather than code.

---

## 7. Money, customization, and skills

### 7.1 Dirty money and laundering

Raw take is **dirty** and cannot be spent directly. A fence converts at a loss (60–70%), and safehouse upgrades improve the rate. This is a money sink, a pacing lever, and the reason stealing jewelry differs from stealing cash.

### 7.2 Wardrobe

Clothing is functional, not cosmetic. Each piece occupies a feature slot the witness system reads from, and each is a mesh swap on the modular character.

- Cheap pieces are disposable: buy, burn, discard.
- Expensive pieces carry a functional benefit. A work uniform lets you stand in staff areas without generating suspicion; a long coat conceals tools; quiet shoes reduce noise radius.
- **Layering** — carry a second layer and change appearance mid-job in a bathroom or stairwell, so witnesses from the first half describe someone different from the second half.
- **Decoy pieces** — cheap, loud, distinctive items that exist to be confirmed into the profile on purpose. Mark them as such in the shop so the strategy is discoverable.
- A wardrobe screen shows each piece's burn status and whether the burn was intentional.

### 7.3 Tools and gear

| Tool | Effect |
|---|---|
| Lockpicks (tiers) | Silent, slow, skill-gated |
| Drill | Fast, loud, distinctive method signature |
| Glass cutter | Silent entry through windows, limited uses |
| Crowbar | Fast, very loud, cheap |
| Signal jammer | Delays alarm transmission |
| Camera looper | Blinds one camera temporarily |
| Grappling hook / rope | Opens vertical routes: roofs, mezzanines |
| Bag upgrades | Carry capacity, directly converting to value per trip |
| Distraction items | Pull an NPC off a patrol point |

### 7.4 Skills

Three lines, kept short (4–5 nodes each). Depth over breadth.

**Ghost** — reduced noise while moving; faster lockpicking; slower suspicion accumulation in shadow; move at full speed with a full bag.

**Face** — blend in (standing still in a public area decays nearby suspicion); talk down a Curious NPC once per job; pass as staff while in uniform; witnesses observe you at reduced confidence.

**Hands** — faster looting; appraisal (see values before taking); larger capacity; partial value from a display without an obvious theft.

### 7.5 Safehouse upgrades

Better fence contacts; intel purchases revealing patrol routes or camera arcs pre-job; wardrobe storage; identity reset (expensive, gated).

---

## 8. Recommended tools and packages

### Engine and rendering
- **Unity 6 LTS**, 3D URP.
- **Baked lightmaps** for static geometry plus a few realtime lights. Light Probes for characters.
- **Dither-fade occluder shader** (Shader Graph, sphere mask on camera-to-player vector). Build this in Phase 1.
- **ProBuilder** for greyboxing levels directly in Unity before any art exists. This is how you iterate on level design without waiting for models.

### Navigation
- **Unity AI Navigation** (`com.unity.ai.navigation`) — NavMeshSurface with runtime rebaking, NavMeshObstacle with carving for doors you lock, NavMeshLink for vaulting, vents, and stairs. In 3D this is the built-in answer and the A\* Pathfinding Project is no longer needed.
- NavMeshLink is also how vertical routes become traversable for both the player's AI-facing state and for guards pursuing you.

### Characters and animation
- **Mixamo** — free rigged animations. Walk, run, crouch-walk, idle variants, interact, panic, phone call. This is the single biggest time saver in the project; hand-animating humanoids is not realistic at this scale.
- **Modular character setup** in Blender: one base mesh, sockets for head/torso/legs/hands. Export as FBX with a shared rig so animations retarget across all outfits.
- **Animation Rigging package** if you want look-at behaviour for NPC heads. Worth it: an NPC visibly turning its head toward a noise is enormous for readability.

### Art
- **Blender** for modelling. Flat shading, no UV unwrapping needed if you use vertex colours or palette atlases.
- **Kenney** (CC0) and **Synty** low-poly asset packs as a base or placeholder. Use them without embarrassment; a consistent bought set beats an inconsistent handmade one.

### Architecture
- **ScriptableObjects** for NPC archetypes, clothing, tools, skills, loot tables, reaction trees, and level configs. The highest-value architectural decision in the project.
- **Newtonsoft JSON** (`com.unity.nuget.newtonsoft-json`) for saves: police profile, wardrobe, per-location security state, skills.
- **Unity Input System.**
- **Assembly definitions** from the start (Core, Player, NPC). They keep compile times sane, and they turn dependency direction (NPC → Player → Core) into a compiler error rather than a convention.

### Feel and polish
- **Cinemachine** with a fixed-rotation virtual camera, a confiner, and a small amount of follow damping. Dolly slightly on alert state changes.
- **PrimeTween** (free, allocation-free) or DOTween.
- **UI Toolkit** for the briefing board, wardrobe, and skill screens.
- **FMOD** if you want occlusion and adaptive music tied to alert state; Unity audio is acceptable, but audio is a large fraction of feeling finished either way.

### Workflow
- **Git + Git LFS.** Set Asset Serialization to Force Text and Version Control Mode to Visible Meta Files before the first commit. In 3D you will have far more binary assets than in 2D, so LFS configuration matters more.
- **Prefab-heavy structure** so two people rarely touch the same scene file.
- **Unity Test Framework** for the witness confidence merge and the security budget allocator. Pure logic, easy to test, painful to debug through gameplay.

---

## 9. Design risks

**Camera occlusion is the top risk and must be solved first.** If the player cannot see themselves reliably, nothing else matters. Prototype low walls plus dither fade in week one, before any content exists.

**3D costs more than 2D.** Models, rigs, animations, navmesh, lighting. The mitigation is aggressive: asset packs, Mixamo, greybox-first level design, and fewer locations than the 2D plan called for. Cut content, never cut the systems that the courses grade.

**Hand-author the levels.** Stealth depends on readable sightlines and learnable layouts. Procedural generation produces spaces that are legal but not interesting. Three hand-built locations, replayable because the *security* changes rather than the geometry, beats infinite generated stores.

**Detection must be recoverable.** Being seen should start a timer and a chase, not end the run. The loss condition is losing the take and burning the disguise.

**Verticality is a trap if overused.** Every floor doubles level-design cost and complicates the camera. Tier 2's mezzanine is the first verticality; a full second floor waits for tier 3.

**Feature-slot readability.** If the player cannot tell which wardrobe items are burned, the disguise economy becomes guesswork. That screen deserves as much care as the gameplay.

---

## 10. Polish checklist (for the game course)

- Main menu, pause menu, options (audio sliders, resolution, rebinding)
- Save and load with multiple slots
- A first job that teaches by design rather than by text
- Camera feel: damping, a slight push on detection, a freeze frame on being spotted
- Footstep audio varying by surface; adaptive music tied to alert state
- Readable UI for suspicion, carry weight, alarm timers, light level
- Results screen with value breakdown and witness statements
- Consistent art direction and a fixed palette
- Controller support (a fixed-camera 3D game is well suited to it)
- No placeholder assets in the final build

---

## 11. Open decisions

- **Orthographic or narrow-FOV perspective?** Decide in Phase 1 with real art in frame.
- **Camera rotation in 90° snaps?** Adds player agency, multiplies level-design cost. Default to no.
- **How many floors per location?** One at tier 1, a mezzanine at tier 2, two floors at tier 3.
- **Time limit per job?** Consider starting the police timer only on first detection.
- **Solo or crew?** Solo, for scope.
- **How many feature slots?** Six to eight. More becomes bookkeeping.
- **Does the identity reset exist?** It competes with poisoning; if wiping the profile is cheap, deceiving it is pointless.
- **How discoverable is poisoning?** Self-discovery is more satisfying and much riskier.
- **Can security be poisoned too far?** A floor on minimum coverage per zone is the fix, at some cost to expressiveness.
- **How long is the decision window?** Likely 1.5–3 seconds, probably different per reaction type. Playtest question.
- **Can a witness be silenced after the fact?** Dramatic, but risks becoming dominant and pulls toward violence.
