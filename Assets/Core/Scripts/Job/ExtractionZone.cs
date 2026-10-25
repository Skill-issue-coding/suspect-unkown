using System;
using UnityEngine;

// The getaway van's extraction zone: a trigger at its back doors, locked until the level's objectives
// are complete. Inside the unlocked zone, progress rises to 1 over secondsToLeave; outside, it drains
// back at the same speed, so a step out costs a little, not everything. At 1 the job is won if a bag
// item or story item was taken; with only pocket cash, ExtractionWarning asks first, since leaving then
// saves nothing. Cancel empties the bar, and the zone stays shut until the player steps out and back in,
// so the question never repeats on the spot. Only the timer runs per frame, and it bails at once while nothing is moving
[RequireComponent(typeof(Collider))]
public class ExtractionZone : MonoBehaviour
{
    [SerializeField, Tooltip("The level's objectives. The zone unlocks once they are completed")]
    private JobObjectives objectives;

    [SerializeField, Min(0.1f), Tooltip("Seconds of standing in the unlocked zone to leave")]
    private float secondsToLeave = 2.5f;

    private static readonly Color LockedColor = new Color(0.6f, 0.6f, 0.6f, 0.8f);
    private static readonly Color UnlockedColor = new Color(1f, 0.71f, 0.28f, 0.8f);

    private JobRewards rewards;
    private bool playerInside;
    private bool asking;     // the warning is open, waiting for an answer
    private bool declined;   // the player cancelled; the zone stays shut until they step out

    public bool IsUnlocked => objectives.IsComplete;
    public bool IsPlayerInside => playerInside;
    public float Progress { get; private set; }

    public event Action StateChanged;

    private void Awake()
    {
        if (objectives == null)
        {
            Debug.LogWarning($"{name} needs the level's objectives", this);
            enabled = false;
            return;
        }

        // JobRewards sits beside JobObjectives on the Job object. Without it every exit counts as a success
        rewards = objectives.GetComponent<JobRewards>();
        if (rewards == null)
            Debug.LogWarning($"{name}: no JobRewards beside {objectives.name}; leaving always completes the job", this);

        if (!GetComponent<Collider>().isTrigger)
            Debug.LogWarning($"{name} collider isn't a trigger; the player will bump into it instead", this);
    }

    private void Update()
    {
        if (JobOutcome.HasEnded)
        {
            enabled = false;
            return;
        }

        float target = IsUnlocked && IsPlayerInside && !declined ? 1f : 0f;
        if (asking || Progress == target) return;

        Progress = Mathf.MoveTowards(Progress, target, Time.deltaTime / secondsToLeave);
        if (Progress >= 1f) Leave();
    }

    private void Leave()
    {
        if (rewards == null || rewards.HasLootToKeep)
        {
            JobOutcome.Complete();
            return;
        }

        if (ExtractionWarning.Active == null)
        {
            Debug.LogWarning($"{name}: no ExtractionWarning in the scene; leaving empty-handed without asking", this);
            JobOutcome.Exit();
            return;
        }

        asking = true;
        ExtractionWarning.Active.Show(JobOutcome.Exit, OnCancelled);
    }

    private void OnCancelled()
    {
        asking = false;
        declined = true;
        Progress = 0f;
    }

    private void OnEnable()
    {
        if (objectives != null) objectives.Changed += OnObjectivesChanged;
    }

    private void OnDisable()
    {
        if (objectives != null) objectives.Changed -= OnObjectivesChanged;
    }

    private void OnObjectivesChanged() => StateChanged?.Invoke();

    private void OnTriggerEnter(Collider other)
    {
        if (!enabled || !other.TryGetComponent(out CharacterController _)) return;
        playerInside = true;
        StateChanged?.Invoke();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!enabled || !other.TryGetComponent(out CharacterController _)) return;
        playerInside = false;
        declined = false;
        StateChanged?.Invoke();
    }

    // Scene view: the zone grey while locked, orange once unlocked, filled bottom-up with Progress in play
    private void OnDrawGizmos()
    {
        if (!TryGetComponent(out BoxCollider box)) return;

        bool unlocked = objectives != null && objectives.IsComplete;
        Gizmos.color = unlocked ? UnlockedColor : LockedColor;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(box.center, box.size);

        if (Progress <= 0f) return;
        Vector3 size = new Vector3(box.size.x, box.size.y * Progress, box.size.z);
        Vector3 centre = box.center + Vector3.down * ((box.size.y - size.y) * 0.5f);
        Gizmos.DrawCube(centre, size);
    }
}
