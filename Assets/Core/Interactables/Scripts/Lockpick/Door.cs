using System;
using PrimeTween;
using UnityEngine;
using UnityEngine.AI;

// A player-only door (decision 18). Closed, it is a wall to NPCs: the NavMeshObstacle carves the
// doorway, and the leaf (on the Occluder layer) blocks sight. Open, it is a route and evidence: the
// obstacle is off and its Anomaly is raised. The door knows nothing about the player or skills. It
// holds its lock level and sweet spot and opens when asked, so any opener (the lockpick now, a
// crowbar or keycard later) calls the same Open()
[RequireComponent(typeof(Anomaly))]
[RequireComponent(typeof(NavMeshObstacle))]
public class Door : MonoBehaviour
{
    [Header("Swing")]
    [SerializeField, Tooltip("Rotated around the door's up axis to swing the leaf. The leaf's pivot must sit on the hinge")]
    private Transform hinge;

    [SerializeField, Tooltip("Degrees the leaf swings when open. Negative swings it the other way")]
    private float openAngle = 100f;

    [SerializeField, Tooltip("Seconds to open or close normally")]
    private float swingDuration = 0.35f;

    [SerializeField, Tooltip("Seconds to close when slammed")]
    private float slamDuration = 0.12f;

    [Header("Lock")]
    [SerializeField, Range(1, 3), Tooltip("Lockpicking skill needed, and how hard the lock is")]
    private int lockLevel = 1;

    [SerializeField, Tooltip("False for a door that is shut but not locked")]
    private bool startsLocked = true;

    [Header("Noise")]
    [SerializeField, Tooltip("Metres a slam carries along the NavMesh")]
    private float slamNoiseRadius = 8f;

    public int LockLevel => lockLevel;
    public bool IsOpen { get; private set; }
    public bool IsLocked { get; private set; }

    // Degrees, inside the pick's swing (LockpickModel clamps it to ±135). Random per door, fixed for
    // the heist, so a slip teaches the player something
    public float SweetSpot { get; private set; }

    // The middle of the doorway, already kept in open air for the anomaly's sight line
    public Vector3 PromptPoint => anomaly.SightPoint;

    public event Action<Door> Opened;

    private Anomaly anomaly;
    private NavMeshObstacle obstacle;
    private DoorReach[] reaches;
    private Quaternion closedRotation;
    private Tween swing;

    private void Awake()
    {
        anomaly = GetComponent<Anomaly>();
        obstacle = GetComponent<NavMeshObstacle>();
        reaches = GetComponentsInChildren<DoorReach>(true);

        // RequireComponent only adds components when Door itself is added, not to existing objects
        if (anomaly == null || obstacle == null)
        {
            Debug.LogWarning($"{name} needs an Anomaly and a NavMeshObstacle; the door is disabled", this);
            enabled = false;
            return;
        }

        if (hinge == null)
            Debug.LogWarning($"{name} has no hinge; it will open and close without swinging", this);
        else
            closedRotation = hinge.localRotation;

        IsLocked = startsLocked;
        SweetSpot = UnityEngine.Random.Range(-130f, 130f);
        obstacle.enabled = true;
    }

    [ContextMenu("Open")]
    public void Open()
    {
        if (!enabled || IsOpen) return;

        IsOpen = true;
        IsLocked = false;
        obstacle.enabled = false;
        SetReachable(false);
        Swing(openAngle, swingDuration);
        anomaly.Raise();
        Opened?.Invoke(this);
    }

    [ContextMenu("Close")]
    public void Close() => CloseOver(swingDuration);

    // Close fast and loud. Alarm 0: NPCs glance at a slam but don't grow suspicious of it
    [ContextMenu("Slam")]
    public void Slam()
    {
        if (!enabled || !IsOpen) return;

        CloseOver(slamDuration);
        NoiseBus.Publish(new NoiseEvent(transform.position, slamNoiseRadius, 0f));
    }

    private void CloseOver(float duration)
    {
        if (!enabled || !IsOpen) return;

        IsOpen = false;
        obstacle.enabled = true;
        SetReachable(true);
        Swing(0f, duration);
        anomaly.Clear();
    }

    // The reach triggers exist only while the door is shut: an open door offers nothing to use
    private void SetReachable(bool reachable)
    {
        foreach (DoorReach reach in reaches)
            reach.SetReachable(reachable);
    }

    // Rotates around the parent's up axis (pre-multiplied), so a leaf imported with a -90° X
    // correction from Blender still swings horizontally
    private void Swing(float angle, float duration)
    {
        if (hinge == null) return;

        swing.Stop();
        swing = Tween.LocalRotation(hinge, Quaternion.Euler(0f, angle, 0f) * closedRotation, duration, Ease.OutCubic);
    }

    private void Reset()
    {
        NavMeshObstacle box = GetComponent<NavMeshObstacle>();
        if (box == null) return;

        box.shape = NavMeshObstacleShape.Box;
        box.carving = true;
        box.carveOnlyStationary = true;
    }

    // The doorway as NPCs see it: red while it blocks, green while it is a route
    private void OnDrawGizmos()
    {
        NavMeshObstacle box = obstacle != null ? obstacle : GetComponent<NavMeshObstacle>();
        if (box == null) return;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = IsOpen ? Color.green : Color.red;
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
