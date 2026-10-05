# Suspect Unknown — instructions for AI assistants

These instructions apply to any AI assistant working in this repository: Claude Code, Antigravity (Gemini), Copilot, or anything else. Read this whole file before doing any work. If a rule here conflicts with your defaults, this file wins.

Suspect Unknown is a 3D stealth heist game built in Unity by a two-person university team. It is also an AI course deliverable, so the NPC and adaptive-security systems must be explainable, not just working.

## Read these first

| File | What it is |
| --- | --- |
| `Assets/heist-game-concept.md` | What the game is. Section numbers ("concept 6.2") are referenced everywhere |
| `Assets/roadmap.md` | Build order for the whole game, in phases, with gates |
| `Assets/roadmap_npc.md` | The NPC slice: numbered architecture decisions, file tables with status, milestones M0–M8 |

The numbered decisions in `roadmap_npc.md` ("decision 6") are binding. Do not work against one without raising it first. If a change alters a decision, update the roadmap in the same piece of work.

---

## 1. How to work with us: guide, don't write

This project is also a learning project. The default is **guidance**: the developer writes the code, and the assistant explains, plans, and reviews.

**Default behaviour:**

- **Explain the step before any code:** what it is for, where it lives, what it depends on, and what it deliberately does not do.
- **Give structure, not finished files:** class and method declarations, field lists with types and defaults, and short snippets for the tricky parts. Explain each snippet line by line when it introduces something new.
- **Break work into small numbered steps**, each ending in a **check**: what to do in play mode and what should happen. Do not move to the next step until the developer confirms the current one works.
- **Review on request:** when asked "is this correct?", read the file and report issues ranked by severity: bugs first, then missing pieces, then tidy-ups, then what is already correct. Quote line numbers. Do not rewrite the file.

**Only write or edit files when explicitly asked**, for example "fix these", "add the comment", "update the roadmap", "write the editor script". When asked, make exactly that change and nothing more.

**Plan before building:**

- Data before logic: the ScriptableObject fields exist before the code that reads them.
- Logic before presentation: behaviour works on a grey capsule before animation or UI goes on top.
- Debug tools alongside the system, not after. A system you cannot see is a system you cannot tune.
- Name the done-when test before writing the code that passes it.

**When something breaks,** investigate before answering: read the code, the scene or prefab YAML, and `Logs/Editor.log`. Give the cause, the evidence, and the fix. Do not guess.

---

## 2. Code standards

Aim for a practical industry standard: code another developer could pick up, extend, and trust.

### Every piece of code has a clear purpose

- Each file holds one type, and opens with a comment saying **what it is for and why it exists**, not what each line does.
- Comment decisions and traps, the "why". Do not comment the obvious.
- If a class needs "and" to describe it, it is probably two classes.

### Nothing irrelevant to the project

- **No speculative code.** No field, enum value, method, or parameter without something that produces it and something that uses it now. Reserve a slot only when the roadmap explicitly says so, and mark it as reserved.
- No generic frameworks, managers, or abstractions "for later". Build the version the current milestone needs.
- No template leftovers: delete Unity's empty `Start()` and `Update()` stubs.
- Delete dead code and unused fields during review; do not comment them out.

### Performance: do only the work that is needed, when it is needed

- **Do not put work in `Update()` that does not change every frame.** Use, in order of preference:
  - **Events** for things that happen rarely (a state change, a stance change, a door opening).
  - **Ticks** for things that are sampled (perception runs at the archetype's tick rate, about 10 Hz, staggered across NPCs).
  - **Caching** for things that never change after start.
- `Update()` is for genuinely continuous work: smoothing a value, moving something, reading continuous input.
- `LateUpdate()` for anything that must follow the camera or the animated pose (billboards, procedural dangling, head look).
- `FixedUpdate()` only for physics.
- **Never in a per-frame path:** `GetComponent`, `Find*`, `Camera.main`, LINQ, `new` arrays or lists, string building, `GetComponentsInChildren`, `NavMesh.CalculatePath`. Cache in `Awake` or `Start`, reuse buffers, compute paths only when choosing.
- Cheap checks before expensive ones, and bail early: range, then angle, then raycasts.
- Compare distances with `sqrMagnitude`.
- Profile before optimising anything else. Readable first, then fast where it measurably matters.

### Robustness patterns used throughout the codebase

- **Guard dependencies** in `Awake` or `Start`: if a required reference is missing, `Debug.LogWarning($"{name} …", this)` and set `enabled = false`. Never let a missing reference throw every frame.
- **Subscribe in `OnEnable`, unsubscribe in `OnDisable`**, null-guarding both.
- **Raise events with `?.Invoke`.**
- **No runtime state in ScriptableObjects** (decision 1). Archetypes hold tuning only. Expose curves through methods (`EvaluateDistance`), not by handing out the mutable `AnimationCurve`.
- **Validate data in `OnValidate`** (for example threshold order, all-zero weights, empty curves).
- **Smooth what is displayed, never the underlying value.** Suspicion stays exact; the indicator eases toward it.

---

## 3. Conventions

- **Serialized fields:** `[SerializeField] private` with a public getter or method. Every serialized field gets a `[Tooltip]` stating its unit or meaning. Trailing full stops follow the file: the NPC scripts leave them off, the player locomotion scripts use them. Group fields with `[Header]`.
- **Naming:** public members PascalCase. Private fields: match the file. `Player/Scripts/Locomotion` and `Animation` use `_camelCase`; everything else uses `camelCase`. Public fields on plain data structs (`Observation`) are `camelCase`.
- **Events** are past tense (`Perceived`, `AlertStateChanged`, `ActivityChanged`). State-change events carry `from` and `to`.
- **Enums:** one per file. If code relies on the declaration order (`NpcAlertState`, `BodyRegion`), say so in a comment. `[Flags]` enums use `1 << n` values.
- **Dependencies between components:** use `[RequireComponent]`, then `GetComponent` in `Awake`.
- **Editor tools:** static classes in an `Editor` folder, menu paths from `MenuPaths` (`Assets/Editor/MenuPaths.cs`). Tools that edit prefabs use `PrefabUtility.LoadPrefabContents`, never a scene instance.
- **No namespaces** are used in gameplay code today; stay consistent.
- **Folders:** `Assets/Core` (shared, owns nothing), `Assets/Player`, `Assets/NPC`, each with `Scripts/<Area>/`. Test-only scripts live in `Assets/Testing`.
- **Git:** commit messages are `Area: Summary` (`NPC: Suspicion base + indicator`). Commit only when asked.

### Architecture in brief (full detail in `roadmap_npc.md`)

- Dependencies point one way: **NPC → Player → Core**. Core references nothing. Player never references NPC types.
- Composition over inheritance: an NPC is shared perception components plus one role behaviour. No `Npc` base class.
- Perception is stateless per tick and exposes results only (`IPerceiver.Perceived`, `TryGetLatest`). Consumers never trigger raycasts.
- `Observation` is a struct with no arrays or other reference-type collections.
- Suspicion comes from abnormal conduct, never from being seen.
- Guards patrol by serving points of interest (decision 15), not fixed routes.

### Physics layers

| Layer | Index | Used for |
| --- | --- | --- |
| Occluder | 10 | Walls and cover. The only layer perception rays test against |
| Player | 14 | The player and its bone perception colliders |
| NPC | 15 | NPC bodies |
| Volume | 16 | `Zone` volumes. Collides with Player only |
| UI | 5 | HUD canvases |

---

## 4. Unity specifics for this project

Assume nothing from older Unity tutorials. If you are not certain an API exists in this version, say so and check the documentation for this version instead of guessing.

### Versions

| Component | Version | Notes |
| --- | --- | --- |
| Unity Editor | **6000.6.0f1** (Unity 6) | |
| Render pipeline | **URP 17.7** | Shaders must be URP-compatible. No Built-in pipeline shaders or `OnRenderImage` |
| Input | **Input System 1.20 only** | The legacy `Input` class is disabled. Never use `Input.GetKey` or `Input.GetAxis`. Input arrives through `PlayerInputs` |
| Camera | **Cinemachine 3** (package 6.6.0) | Namespace `Unity.Cinemachine`, component `CinemachineCamera`. Not `CinemachineVirtualCamera` or the `Cinemachine` namespace |
| Navigation | **AI Navigation 2.0.14** | Bake with a `NavMeshSurface` component. The legacy Navigation bake window is gone |
| Rigging | **Animation Rigging** (package 6.6.0) | `RigBuilder`, `Rig`, `MultiAimConstraint` for head look |
| Level geometry | ProBuilder 6.1.2 | |
| UI | uGUI 2.6 with TextMeshPro | `TMPro.TMP_Text`. No UI Toolkit in gameplay UI so far |
| Tweening | PrimeTween (in `Assets/Plugins`) | |
| Scripting | .NET Standard 2.1 | C# 9. No `Array.Clear(array)` one-argument overload |

### Deprecated or changed APIs in this Unity version

| Do not use | Use instead |
| --- | --- |
| `FindObjectOfType`, `FindFirstObjectByType` | `FindAnyObjectByType` (or a static accessor, preferred) |
| `GetInstanceID()` converted to `int` | Avoid. Use a static counter for staggering |
| `Rigidbody.velocity`, `.drag`, `.angularDrag` | `linearVelocity`, `linearDamping`, `angularDamping` |
| `Input.*` | Input System actions |
| `CinemachineVirtualCamera` | `CinemachineCamera` |

### Engine behaviour that trips people up

- **Execution order:** all `Awake`, then all `OnEnable`, then all `Start`, then per frame `Update` → animation → `LateUpdate`. `Awake` is not called on inactive GameObjects. `OnEnable` runs right after `Awake`, before `Start`.
- **Unity null:** a destroyed object compares `== null` as true. `?.` and `??` bypass this check. Use explicit `!= null` on anything derived from `UnityEngine.Object`.
- **Serialization:** Unity does not serialize dictionaries, properties, or static fields. Use named fields or a `[Serializable]` struct. A new field on an existing asset keeps its field initializer value.
- **Editor-only code:** `using UnityEditor;` must be inside `#if UNITY_EDITOR`, or the build fails even when the editor compiles. Editor scripts go in folders named `Editor`.
- **Gizmos run in edit mode,** where `Awake` and `Start` have not run. Fetch references lazily in gizmo methods and null-guard them.
- **Prefabs:** add components in prefab mode, not on a scene instance, or they become scene overrides.
- **Assets:** move and rename assets inside Unity, never in the file explorer, so the `.meta` file and its GUID move with them. A lost GUID breaks every reference to the asset.
- **Enums in C#** support arithmetic (`state + 1`, `state++`) and ordering comparisons. Code relying on this depends on declaration order.

### Animation

- All characters are **Humanoid**. Clips retarget through avatars, so clips and models do not need to share a skeleton or a scale.
- `PlayerMotor` and the `NavMeshAgent` move characters, and code turns them. **Root motion is off**, so clips keep their root movement in the pose: on each clip's Animation tab, Bake Into Pose for Root Rotation (Body Orientation), Position Y (Feet), and Position XZ (Center of Mass).
  - Exception: **jump clips** use Position Y based on **Original**.
  - Exception: **turn-in-place clips** leave Root Rotation **unbaked**, so code does the turning and the clip supplies only the footwork.
- Animator controllers are generated by editor builders (`PlayerLocomotionBuilder`, `NPCLocomotionBuilder`), not edited by hand. Change the builder and rebuild. Builders hard-code asset paths: update them when assets move.
- Check each avatar's **Configure** screen: the Hips slot must hold the actual hips bone, not a `Root` bone above it.

### Models from Blender

- Scene units: metric, unit scale 1.0, metres. Apply all transforms (Ctrl+A) so armature and meshes are at scale 1.
- FBX export: Selected Objects, Armature and Mesh only, **Apply Scalings: FBX Units Scale**, **Apply Transform** on, **Add Leaf Bones** off, Only Deform Bones on, Bake Animation off.
- In Unity the imported `Armature` must be at scale 1, and `Hips` at a local height of about 0.9. A scale of 100 anywhere means the export settings were wrong.
- Keep `.blend` source files outside `Assets/`. Unity re-imports them through Blender on every save.

---

## 5. Keeping the docs current

- When a milestone step is done and tested, update its status in `roadmap_npc.md` (file table status, milestone checkbox, changelog entry with the date).
- When a design changes, update the relevant decision in the same piece of work, and add a changelog line.
- Roadmaps describe **what** needs to be done. They do not assign work to people.
