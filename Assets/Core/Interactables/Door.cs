using System;
using PrimeTween;
using UnityEngine;
using UnityEngine.AI;

// A player-only door (decision 18). Closed, it is a wall to NPCs: the NavMeshObstacle carves the
// doorway, and the leaf (on the Occluder layer) blocks sight. Open, it is a route and evidence: the
// obstacle is off and its Anomaly is raised. The door knows nothing about the player or skills. It
// holds its lock level and sweet spot and opens when asked, so any opener (the lockpick now, a
// crowbar or keycard later) calls the same Open(). A double door is one Door with a second leaf:
// one lock, one anomaly, one doorway. It slams when an NPC resolves its anomaly, so
// NPCs never reference the door to shut it
[RequireComponent(typeof(Anomaly))]
[RequireComponent(typeof(NavMeshObstacle))]
public class Door : MonoBehaviour
{
    [Header("Swing")]
    [SerializeField, Tooltip("Rotated around the door's up axis to swing the leaf. The leaf's pivot must sit on the hinge")]
    private Transform hinge;

    [SerializeField, Tooltip("Degrees the leaf swings when open. Negative swings it the other way")]
    private float openAngle = 100f;

    [SerializeField, Tooltip("Second leaf of a double door (optional). Swings the mirrored angle, so both leaves open the same way")]
    private Transform secondHinge;

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

    [Header("Audio")]
    [SerializeField, Tooltip("3D source for the door slam sound.")]
    private AudioSource audioSource;

    [SerializeField, Tooltip("Played when a guard walks through a door")]
    private AudioClip closeClip;
    [SerializeField, Tooltip("Played when an NPC slams shut a door the player left open.")]
    private AudioClip slamClip;

    public int LockLevel => lockLevel;
    public bool IsOpen { get; private set; }
    public bool IsLocked { get; private set; }

    // Degrees, inside the pick's swing (LockpickModel clamps it to ±135). Random per door, fixed for
    // the heist, so a slip teaches the player something
    public float SweetSpot { get; private set; }
    public float SwingSeconds => swingDuration;

    private Anomaly anomaly;
    private NavMeshObstacle obstacle;
    private Interactable[] reaches;
    private Quaternion closedRotation;
    private Quaternion secondClosedRotation;
    private Tween swing;
    private Tween secondSwing;

    // World bounds of the reach triggers, taken in Awake while the door is shut and the triggers are on:
    // a disabled collider reports empty bounds, and an open door's reaches are off
    private Bounds[] reachBounds;

    private void Awake()
    {
        anomaly = GetComponent<Anomaly>();
        obstacle = GetComponent<NavMeshObstacle>();
        reaches = GetComponentsInChildren<Interactable>(true);
        reachBounds = new Bounds[reaches.Length];
        for (int i = 0; i < reaches.Length; i++)
            reachBounds[i] = reaches[i].GetComponent<Collider>().bounds;

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

        if (secondHinge != null)
            secondClosedRotation = secondHinge.localRotation;

        IsLocked = startsLocked;
        SweetSpot = UnityEngine.Random.Range(-130f, 130f);
        obstacle.enabled = true;
    }

    private void OnEnable() { if (anomaly != null) anomaly.Resolved += Slam; }
    private void OnDisable() { if (anomaly != null) anomaly.Resolved -= Slam; }

    [ContextMenu("Open")]
    public void Open()
    {
        if (!enabled || IsOpen) return;

        OpenForPassage();
        IsLocked = false;
        anomaly.Raise();
    }

    // Close at normal speed, as a guard does behind them. The sound plays only when the door was open,
    // so closing a shut door stays silent
    [ContextMenu("Close")]
    public void Close()
    {
        if (!enabled || !IsOpen) return;

        CloseOver(swingDuration);
        PlaySound(closeClip);
    }

    // Close fast and loud. Alarm 0: NPCs glance at a slam but don't grow suspicious of it
    [ContextMenu("Slam")]
    public void Slam()
    {
        if (!enabled || !IsOpen) return;

        CloseOver(slamDuration);
        NoiseBus.Publish(new NoiseEvent(transform.position, slamNoiseRadius, 0f));
        PlaySound(slamClip);
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

    // For a guard passing through opens without raising the anomaly and leaves the lock as it was, since guards carry keys.
    [ContextMenu("OpenForPassage")]
    public void OpenForPassage()
    {
        if (!enabled || IsOpen) return;

        IsOpen = true;
        obstacle.enabled = false;
        SetReachable(false);
        Swing(openAngle, swingDuration);
    }

    // The reach triggers exist only while the door is shut: an open door offers nothing to use
    private void SetReachable(bool reachable)
    {
        foreach (Interactable reach in reaches)
            reach.SetReachable(reachable);
    }

    // Rotates around the parent's up axis (pre-multiplied), so a leaf imported with a -90° X
    // correction from Blender still swings horizontally. A double door's second leaf is hinged on
    // the opposite side, so it takes the negated angle to swing the same way
    private void Swing(float angle, float duration)
    {
        if (hinge != null)
        {
            swing.Stop();
            swing = Tween.LocalRotation(hinge, Quaternion.Euler(0f, angle, 0f) * closedRotation, duration, Ease.OutCubic);
        }

        if (secondHinge != null)
        {
            secondSwing.Stop();
            secondSwing = Tween.LocalRotation(secondHinge, Quaternion.Euler(0f, -angle, 0f) * secondClosedRotation, duration, Ease.OutCubic);
        }
    }

    private void Reset()
    {
        NavMeshObstacle box = GetComponent<NavMeshObstacle>();
        if (box == null) return;

        box.shape = NavMeshObstacleShape.Box;
        box.carving = true;
        box.carveOnlyStationary = true;
    }

    // Whether a position stands in either reach. Height is ignored: the NPC's position is at its feet,
    // which can sit on the box's bottom edge
    public bool InReach(Vector3 position)
    {
        foreach (Bounds bounds in reachBounds)
        {
            Vector3 flat = new Vector3(position.x, bounds.center.y, position.z);
            if (bounds.Contains(flat)) return true;
        }
        return false;
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
    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}
