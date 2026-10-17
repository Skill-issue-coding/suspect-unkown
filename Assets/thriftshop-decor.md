# Suspect Unknown — Thrift Shop Decoration Plan

Working notes for dressing `Assets/Thriftshop/Thriftshop.unity`. The gameplay design is in `Assets/thriftshop-level.md`; this file covers only what goes where and why, so decoration can continue in any session or tool (Unity, Blender, Poly Pizza).

> Started 2026-10-16. First pass placed with Synty assets only (126 props), under `Root/MainLevel/Decor`.

---

## 1. The space, in world coordinates

The building is `Root/MainLevel/Meshes/Houses` (`SM_Bld_Shop_03`). The floor is at y = 0. North (+z) is the back of the shop; the street and the front windows are at the south (−z).

| Area                           | x                         | z           | Notes                                                           |
| ------------------------------ | ------------------------- | ----------- | --------------------------------------------------------------- |
| Inner walls                    | −7.0 … 22.2               | 5.5 … 29.8  | Wall faces: left −6.98, right 22.18, back 29.8, front about 5.5 |
| Front doors                    | −3.6 … −1.6 and 6.5 … 8.5 | 5.6         | Keep a 2 m aisle north of each door                             |
| Register (existing mesh)       | 20.1 … 22.2               | 7.7 … 11.6  | The clerk's side is the right wall side; keep it clear          |
| Trash room (red)               | −7.0 … 2.6                | 23.5 … 29.8 | Front wall face z ≈ 23.05; right wall x ≈ 2.9                   |
| Stockroom (green, middle)      | 3.3 … 12.4                | 23.0 … 29.8 | No front wall yet: open to the shop floor                       |
| Office (green, right)          | 13.0 … 22.2               | 19.3 … 29.8 | Front wall face z ≈ 18.67; left wall face x ≈ 12.6              |
| Shop front (pink)              | −7.0 … 22.2               | 5.5 … ~15   | Displays, gondolas, register                                    |
| Shop back (pink)               | −7.0 … 22.2               | ~15 … 23    | Bric-a-brac, furniture, dimmer                                  |
| Side opening in the left wall  | x −7.2                    | 14.6 … 17.3 | Keep clear (see 4.1)                                            |
| Side opening in the right wall | x 22.4                    | 25.9 … 27.8 | Into the office (see 4.1)                                       |

Prop facing: Synty props face Unity's forward (+z) at rotation 0. Against the back wall use 180, against the left wall 90, against the right wall 270, facing the street 180.

---

## 2. What is placed (first pass)

All under `Root/MainLevel/Decor`, one child per area:

| Group             | Contents                                                                                                                                                                  | Why                                                                                                                                                 |
| ----------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| `FrontWindow`     | Display pedestals with a lantern, wine, a book, a basket, a shield on top; potted plants either side of both doors                                                        | The street-facing shop window: reads as a shop from outside at dusk                                                                                 |
| `LeftWall`        | Four tall wall shelves and one low one, stopping before the side opening                                                                                                  | Wall stock                                                                                                                                          |
| `FurnitureCorner` | Couch, coffee table, two chairs, a lantern, a book, a plant, a chest, against the trash room wall                                                                         | A second-hand living room set: low cover in the shop back                                                                                           |
| `BackShelves`     | Wall shelves under the trash room wall, along the office front wall (tall and low alternating), and along the right wall                                                  | Fills the walls; the low ones on the office wall leave sightlines over them                                                                         |
| `Gondolas`        | Four rows of four double gondolas (x 0.8, 3.6, 10.4, 13.2; z 11.0–15.6)                                                                                                   | Tall full cover. The central aisle (x 4.2–9.8) is kept open from the right front door to the stockroom. Row x 10.4 is turned to face the main aisle |
| `Clothes`         | Four hanging-cloth frames (Polygon Adventure) as stand-in clothes racks                                                                                                   | Placeholder only: replace with real clothes racks (section 3)                                                                                       |
| `VintageTables`   | Two market tables in the shop back with baskets, pots, books and wine; two round tables and two pedestals in the right section with books, lanterns, potions and a shield | The "fading into bric-a-brac" back of the shop                                                                                                      |
| `Register`        | A low impulse shelf in front of the counter, a plant                                                                                                                      | Kept off the clerk's side                                                                                                                           |
| `Stockroom`       | Two pallets with box stacks, two storage gondolas on the back wall, barrels, crates, loose boxes, sacks                                                                   | Back-of-house clutter; good spots for bonus loot slots                                                                                              |
| `TrashRoom`       | Trash bags, two wheelie bins, a trash can, a pallet with boxes                                                                                                            | The clerk's bin run destination                                                                                                                     |
| `Office`          | A round table under the existing cash, a desk with chair, two wall shelves, a plant, boxes, a framed poster                                                               | Where the safe will go (section 3)                                                                                                                  |

**Conventions used, keep them:**

- **Layers.** Furniture and anything large enough to hide the player (shelves, gondolas, tables, couch, pallets, bins, racks) is on **Occluder (10)**, so perception rays and the vision cone treat it as cover. Small dressing (books, wine, plants, sacks) stays on **Default (0)**, so it never blocks sight.
- **Static.** Every decor prop is static (Contribute GI, batching, occlusion, reflection probe) for the lighting bake.
- **Prefab instances.** Props are prefab instances of the Synty assets, never unpacked, so the source assets can be swapped later.
- **Colliders.** The Synty prefabs come with colliders. They block the player and are collected by the NavMesh bake.

---

## 3. To do: models from Poly Pizza or Blender

Synty's city and fantasy packs have no clothing or real second-hand goods. These are the gaps, in priority order:

| #   | Model                                                                                                     | Where                                                                                                         | Replaces                 | Gameplay note                                                                     |
| --- | --------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------- | ------------------------ | --------------------------------------------------------------------------------- |
| 1   | **Clothes racks** (rolling rail with hanging garments), 3–4 variants                                      | `Clothes`, plus 4–6 more in the shop front left (x −6 … −4.5, z 8 … 13) and between the gondolas and the back | The hanging-cloth frames | About 1.5 m tall: hides a crouched player, not a standing one. Occluder           |
| 2   | **Safe** (floor safe, about 0.8 m)                                                                        | Office, against a wall away from the door, for example (21.4, 23.5), rotation 270                             | —                        | Needs a door that can swing open; pivot at the hinge (CLAUDE.md, Blender section) |
| 3   | **Mannequins**, 2–3                                                                                       | Front window (replace two pedestals) and the clothes section                                                  | Two window pedestals     | Decor only, no collider needed on the arms                                        |
| 4   | **Bonus loot models**: vintage watch, guitar, record player, fur coat, typewriter, camera, vase, painting | Loot slots (thriftshop-level.md 3.2)                                                                          | —                        | Each becomes a `LootItem` pickup prefab, not decor                                |
| 5   | **Hat and shoe displays**, a shoe rack                                                                    | Shop front right (x 14 … 18, z 8 … 11)                                                                        | —                        | Low cover                                                                         |
| 6   | **Rugs**                                                                                                  | Furniture corner, under the front window, the office                                                          | —                        | No collider                                                                       |
| 7   | **Floor and table lamps, a chandelier**                                                                   | Furniture corner, vintage tables                                                                              | —                        | Lamps that should light the player get `GameplayLight` (decision 17)              |
| 8   | **Signs**: "SALE", price boards, a staff-only sign on the stockroom opening                               | Walls, the stockroom opening                                                                                  | —                        | The staff-only sign tells the player where the `Staff` zone starts                |
| 9   | **Stockroom shelving** (metal racking with boxes)                                                         | Stockroom back wall                                                                                           | The two storage gondolas | Occluder                                                                          |
| 10  | **Office details**: filing cabinet, CCTV monitor, notice board                                            | Office                                                                                                        | —                        | A monitor should be decor only, or it reads as the M8 security monitor            |

When importing: metric, scale 1, apply transforms, FBX settings from CLAUDE.md. Put new models in `Assets/Thriftshop/Meshes` (or `Assets/Thriftshop/Props/` if they get prefabs), make each a prefab, and set the layer on the prefab, not the scene instance.

---

## 4. Problems found in the level geometry (not decor, fix in Blender or Unity)

### 4.1 Things that disagree with `thriftshop-level.md`

> **Status (2026-10-17):** the walls now have colliders on Occluder (1); the trash room has an open doorway into the shop back, and the office is reached through the `StaffRoom Door`, which guards may now open (2, decision 19 changed); the stockroom keeps its open front, so there is no staff wall, and the shop back is its own room instead (3); the side doors match the plan, the left one into the trash room and the right one into the emergency exit pocket (4). `StaffArea` and `SnackPoint` are removed (5). Rooms are listed in `thriftshop-level.md` 1.1; the zones are checked to cover the floor up to the walls and counter with no overlaps.

1. **The building has no colliders.** `Houses` and every child (`SM_Bld_Shop_03`, `Floor`, the doors, the register) have no collider and are on layer Default. The player walks through the walls, perception rays pass through them, and the NavMesh bake sees no walls. Fix: mesh colliders (or box colliders per wall) on the walls, with the walls on **Occluder**, and the floor collidable.
2. **The trash room and the office are fully enclosed.** Neither has a doorway into the shop. The clerk's bin run and the guard's office POI need an **open doorway** in each (NPCs never use doors, decision 19). Suggested: a 1.4 m gap in the trash room's right wall facing the stockroom (x 2.9, z 25–26.5), and one in the office's left wall facing the stockroom (x 12.6, z 24–25.5). That keeps both staff rooms reachable only through the staff area.
3. **There is no staff wall.** The stockroom is open to the shop floor along its whole front (z ≈ 23, x 3.3–12.4). The plan wants the shop to fade into staff rooms, with a staff wall the right side entrance sits below. Suggested: a wall at z ≈ 23 from x 2.9 to 12.6 with one 2 m opening centred on the main aisle (x 6–8). The `Staff` zone starts at that opening.
4. **The side openings are mirrored compared with the plan.** The plan says the left side door leads into the trash room and the right one into the shop just below the staff wall. In the mesh, the left wall's opening is at z 14.6–17.3 (into the shop floor) and the right wall's at z 25.9–27.8 (into the office). Decide which is right, then move the openings or update `thriftshop-level.md` 1.1.
5. **Leftovers from the apartment:** `StaffArea` (one zone) and `SnackPoint` are still in the scene; remove them in step T1.

### 4.2 Things to keep in mind while decorating

- **Aisles.** At least 1.5 m wide everywhere an NPC walks. The central aisle (x 4.2–9.8) and both front door aisles stay open.
- **The register.** Nothing on the clerk's side of the counter. The till must be visible from the shop floor, so the player can watch the clerk leave.
- **Cover is a design tool.** Tall props (gondolas, wall shelves, racks) are full cover; tables, the couch and low shelves are crouch cover. Put crouch cover along the routes to the till and the office, and keep the guard's lines across the shop floor readable. Check new cover with the debug rays, not by eye (decision 21: perception is the truth).
- **Camera occlusion.** The camera is pitched 60° down. Tall props near the front can hide the player behind them; nothing over about 2 m in the shop front. The Synty materials do not dither-fade (concept 2); if a prop hides the player, lower or move it.
- **Light budget.** At most 32 realtime shadow maps (`apartment-level.md` 5). Decorative lamps cast no shadows unless they count for gameplay.
- **Loot slots.** Leave free surfaces for `LootSlot`s: the vintage tables, the stockroom pallets and shelves, the office desk and shelves.

---

## 5. How the first pass was placed

Through the Unity CLI against the open editor (`unity command eval_file`): one C# script that finds each Synty prefab by name, instantiates it as a prefab instance under `Decor/<Group>`, sets position, rotation, layer and static flags, then saves the scene. To redo or extend it, follow the same pattern: one line per prop with the group, prefab name, x, z, rotation, height and whether it is cover. Check each pass with a top-down orthographic render (camera at y = 4, near clip 0.1, far 4.5, so the roof is cut away) and a render from the game camera's 60° angle.
