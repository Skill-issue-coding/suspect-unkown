using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The room the player is in, for the job log (thriftshop-level.md 4.1). Rooms are Zone volumes
// with a room index; 0 is the street, used when the player is inside no zone. A new room counts
// only after the player has stayed in it for settleTime, so brushing a boundary (the register
// counter, the open stockroom front) never logs a transition. Triggers arrive through the
// CharacterController only, as in PlayerConduct.
[RequireComponent(typeof(CharacterController))]
public class PlayerRoom : MonoBehaviour
{
    [SerializeField, Tooltip("Seconds the player must stay in a new room before it counts")]
    private float settleTime = 0.5f;

    [SerializeField, Tooltip("Log every committed room change to the console")]
    private bool logChanges = true;

    public int Current { get; private set; }

    // from, to
    public event Action<int, int> RoomChanged;

    // Entry order matters: the newest zone still occupied is the raw room
    private readonly List<Zone> inside = new();
    private readonly Dictionary<Zone, int> colliderCounts = new();

    private WaitForSeconds settleWait;
    private Coroutine pending;
    private int pendingRoom;

    private void Awake() => settleWait = new WaitForSeconds(settleTime);

    private void OnTriggerEnter(Collider other)
    {
        Zone zone = other.GetComponentInParent<Zone>();
        if (zone == null) return;

        colliderCounts.TryGetValue(zone, out int count);
        colliderCounts[zone] = count + 1;
        if (count == 0) inside.Add(zone);
        Evaluate();
    }

    private void OnTriggerExit(Collider other)
    {
        Zone zone = other.GetComponentInParent<Zone>();
        if (zone == null || !colliderCounts.TryGetValue(zone, out int count)) return;

        if (count > 1) { colliderCounts[zone] = count - 1; return; }
        colliderCounts.Remove(zone);
        inside.Remove(zone);
        Evaluate();
    }

    private int RawRoom() => inside.Count > 0 ? inside[inside.Count - 1].Room : 0;

    private void Evaluate()
    {
        int raw = RawRoom();

        if (pending != null)
        {
            if (raw == pendingRoom) return;
            StopCoroutine(pending);
            pending = null;
        }

        if (raw == Current) return;
        pendingRoom = raw;
        pending = StartCoroutine(CommitAfterSettle(raw));
    }

    private IEnumerator CommitAfterSettle(int room)
    {
        yield return settleWait;
        pending = null;

        int from = Current;
        Current = room;
        if (logChanges) Debug.Log($"{name} room {from} -> {room}", this);
        RoomChanged?.Invoke(from, room);
    }
}
