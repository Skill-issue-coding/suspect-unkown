// Where the player carries a taken item. Bag loot is what has to reach the van; pocket change (cash)
// rides along for free. The HUD shows each in its own place on the carry panel. Bag first, so an
// item asset that predates the field reads as Bag
public enum LootCarry
{
    Bag,
    Pocket,
}
