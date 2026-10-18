// One candidate POI as the planner sees it (thriftshop-level.md 5.2): a name for the change log and the
// rooms and entrances it covers. Plain data, so the planner is tested without a scene; S5 builds these
// from the candidate POIs in SecurityLayout
public class PoiCandidate
{
    public readonly string name;
    public readonly int[] rooms;
    public readonly int[] entrances;

    public PoiCandidate(string name, int[] rooms, int[] entrances)
    {
        this.name = name;
        this.rooms = rooms;
        this.entrances = entrances;
    }
}
