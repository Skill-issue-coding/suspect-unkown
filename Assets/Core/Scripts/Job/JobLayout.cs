using System;
using UnityEngine;

// The rooms and entrances of one level, by index, for the job log and adaptive security
// (thriftshop-level.md 4.1). Room 0 is always the street, the implicit room outside every Zone;
// the listed zones are rooms 1 and up, and their object names are the room names. The one place
// that numbers rooms and entrances: it hands each Zone and Entrance its number in Awake, before
// the first physics step, so no trigger fires unnumbered. The job log sizes its counts from it and
// the saved security record is checked against it. Core, so the job log and the save can read it;
// guard spawns and candidate POIs live in SecurityLayout (NPC).
public class JobLayout : MonoBehaviour
{
    [SerializeField, Tooltip("The level's rooms in index order; the first is room 1. The street (0) is implicit")]
    private Zone[] rooms = { };

    [SerializeField, Tooltip("Doorways from the street, in index order; the first is entrance 0")]
    private Entrance[] entrances = { };

    public int RoomCount => rooms.Length + 1;
    public int EntranceCount => entrances.Length;

    public string RoomName(int room)
    {
        if (room == 0) return "Street";
        return room > 0 && room <= rooms.Length && rooms[room - 1] != null ? rooms[room - 1].name : $"Room {room}";
    }

    public string EntranceName(int entrance) =>
        entrance >= 0 && entrance < entrances.Length && entrances[entrance] != null ? entrances[entrance].name : $"Entrance {entrance}";

    // The room an entrance opens into, from list positions rather than the numbers Awake hands out,
    // so it works in edit mode. -1 when the entrance is missing or opens into an unlisted room
    public int EntranceRoom(int entrance)
    {
        if (entrance < 0 || entrance >= EntranceCount || entrances[entrance] == null) return -1;
        return RoomIndex(entrances[entrance].OpensInto);
    }

    // A room's number from its list position, so it works before Awake; -1 when unlisted.
    // The null check matters: IndexOf(null) would match an empty slot and return its number
    public int RoomIndex(Zone zone)
    {
        if (zone == null) return -1;
        int index = Array.IndexOf(rooms, zone);
        return index < 0 ? -1 : index + 1;
    }

    // An entrance's index from its list position; -1 when unlisted or null
    public int EntranceIndex(Entrance entrance) =>
        entrance == null ? -1 : Array.IndexOf(entrances, entrance);

    private void Awake()
    {
        for (int i = 0; i < rooms.Length; i++)
            if (rooms[i] != null) rooms[i].AssignRoom(i + 1);
        for (int i = 0; i < entrances.Length; i++)
            if (entrances[i] != null) entrances[i].AssignIndex(i);

        JobLog.Begin(this, RoomCount, EntranceCount);
    }

    private void Start() => CheckScene();

    // Own data only: during scene load OnValidate can run before the rest of the scene exists
    private void OnValidate()
    {
        WarnOnGaps(rooms, "room");
        WarnOnGaps(entrances, "entrance");
    }

    private void OnDestroy() => JobLog.End(this);

    private void WarnOnGaps<T>(T[] list, string label) where T : UnityEngine.Object
    {
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] == null)
                Debug.LogWarning($"{name}: {label} slot {i} is empty", this);
            else if (Array.IndexOf(list, list[i]) != i)
                Debug.LogWarning($"{name}: {list[i].name} is listed twice as a {label}", this);
        }
    }

    // Works in edit mode too: it checks list membership, not the numbers Awake hands out
    [ContextMenu("Check Scene")]
    private void CheckScene()
    {
        foreach (Zone zone in FindObjectsByType<Zone>(FindObjectsInactive.Exclude))
            if (Array.IndexOf(rooms, zone) < 0)
                Debug.LogWarning($"{zone.name} is not in {name}'s room list; the job log reads it as the street", zone);

        foreach (Entrance entrance in FindObjectsByType<Entrance>(FindObjectsInactive.Exclude))
        {
            if (Array.IndexOf(entrances, entrance) < 0)
                Debug.LogWarning($"{entrance.name} is not in {name}'s entrance list; its crossings are not counted", entrance);
            if (entrance.OpensInto != null && Array.IndexOf(rooms, entrance.OpensInto) < 0)
                Debug.LogWarning($"{entrance.name} opens into {entrance.OpensInto.name}, which is not in the room list", entrance);
        }
    }
}
