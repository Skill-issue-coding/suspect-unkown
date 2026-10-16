# Props — sources and licences

Shared props, one folder per model (`<Name>/<Name>.fbx`, `<Name>.prefab`). Every model is UV-mapped onto the Synty `PolygonCity_Texture_01_A` atlas and uses `Assets/Core/Materials/BaseMaterial.mat`, so all props share one GPU-instanced material. The only exception is glass panels, which use the shared transparent `Props/Glass.mat`. Each face's UVs collapse onto the centre of the atlas swatch nearest its colour; to recolour a face, move its UVs to another swatch. Models come from [Poly Pizza](https://poly.pizza), were normalised in Blender (real-world scale, pivot at bottom centre, one mesh per model) and exported as FBX. CC-BY models must be credited in the game's credits.

## CC-BY 3.0 (credit required)

| Prop | Original | Author | Source |
| --- | --- | --- | --- |
| Typewriter | Typewriter | jeremy | https://poly.pizza/m/0lfEqzVkp8a |
| VaseBlack | Vase | Poly by Google | https://poly.pizza/m/bq4CLpUUqta |
| VaseTall | Tall Vase | Jarlan Perez | https://poly.pizza/m/52xZtzRG244 |
| Teapot | Teapot | Poly by Google | https://poly.pizza/m/3pxNWuQ-iii |
| WallClock | Analog clock | Poly by Google | https://poly.pizza/m/5gAoMR2YHs3 |
| Boombox | Booombox | Poly by Google | https://poly.pizza/m/56XYDxnVVM3 |
| Shoes | Pair of shoes | Poly by Google | https://poly.pizza/m/3MskwjG-x4L |
| Briefcase | Briefcase | jeremy | https://poly.pizza/m/8j8-b-RqJ76 |
| BookStack | Book Stack | Danni Bittman | https://poly.pizza/m/1WggoIFq8tx |
| MilkCrate | Storage Crate | Dave Edwards | https://poly.pizza/m/2Ipd1K9479q |
| FoldedShirts | T-Shirts - folded, 2 piles | sirkitree | https://poly.pizza/m/25zQhboZIxa |
| Suitcase | Suitcase | J-Toastie | https://poly.pizza/m/041xs8FnZZ |

Licence: https://creativecommons.org/licenses/by/3.0

## CC0 1.0 (no credit required)

| Prop | Original | Author | Source |
| --- | --- | --- | --- |
| TrestleTable | Table Big (height scaled to 0.76 m) | Quaternius | https://poly.pizza/m/KWaAZIDK9F |
| TableLamp | Table Lamp | Quaternius | https://poly.pizza/m/9Mo3JruPHY |
| Radio | Radio | Quaternius | https://poly.pizza/m/TPqvwkyWdV |
| Kettle | Kettle | MilkAndBanana | https://poly.pizza/m/XggUrd5f03 |
| CardboardBoxes | Cardboard Boxes | Quaternius | https://poly.pizza/m/V9KbWC8Vd6 |

## Made for this project

| Prop | Notes |
| --- | --- |
| ClothingRack | Rolling garment rack with hanging shirts |
| Safe | Floor safe for the thrift shop office (`thriftshop-level.md` 2.3). Two children: `Safe_Body` (hollow, one shelf) and `Safe_Door`, whose pivot sits on the hinge axis so rotating it about local Y opens the door. The front faces +Z |
| DisplayTable | Glass-topped oak display table with angled cubbies of folded clothes and caps |
| WallDisplayUnit | 2.4 m oak and black wall unit: a hanging bay and a shelf bay |
| ShoeWall | 2.4 m wall unit with angled shoe shelves |
| ShoeBench | Upholstered fitting bench |
| RugRectangle, RugRound, RugRunner | Floor rugs, 8 mm thick, no colliders |
| LootDisplay | Glass vitrine on a pedestal. Every high-value loot item sits on its velvet pad, at 1.04 m |
| LootPocketWatch, LootPearlNecklace, LootVintageCamera, LootDiamondRing, LootJeweledEgg | High-value loot models, made to fit `LootDisplay`. Their pickups are in `Assets/Core/Interactables/Prefabs` |
