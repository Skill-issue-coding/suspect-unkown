using PrimeTween;
using UnityEngine;

// A combination safe in the world. It knows nothing about the player: it holds its lock level, a
// combination rolled once per heist, how many of its numbers have been set so far (so a player who backs
// off keeps them), and swings its door open when asked. PlayerSafeCracking reads and writes these
[RequireComponent(typeof(Interactable), typeof(Anomaly))]
public class Safe : MonoBehaviour
{
    [Header("Door")]
    [SerializeField, Tooltip("Swung open around Hinge Axis. Its pivot must sit on the hinge")]
    private Transform door;

    [SerializeField, Tooltip("Door's local axis it swings around")]
    private Vector3 hingeAxis = Vector3.forward;

    [SerializeField, Tooltip("Degrees the door swings when open. The sign picks the way it swings")]
    private float openAngle = 110f;

    [SerializeField, Tooltip("Seconds the door takes to swing open")]
    private float swingDuration = 0.6f;

    [Header("Lock")]
    [SerializeField, Range(1, 3), Tooltip("How hard the combination is to crack")]
    private int lockLevel = 1;

    [SerializeField, Tooltip("Positions either side of the previous number (and of 0, for the first) a number never lands on, so each number needs a real turn")]
    private int minimumTurn = 3;

    [SerializeField, Tooltip("Interact outline object, disables when the safe is done")]
    private GameObject interactOutline;

    [SerializeField, Tooltip("Component to enable after GlassCutting is done")]
    private Interactable enableOnDone;

    public int LockLevel => lockLevel;
    public bool IsOpen { get; private set; }

    // Cracked once, cracked for the heist: a safe a guard has closed reopens without the minigame
    public bool IsCracked { get; private set; }
    public int NumbersSet { get; private set; }

    // Raised on every opening, the first and any reopening. The office POI listens (step 5)
    public event System.Action Opened;

    private Interactable reach;
    private int[] combination;
    private Tween swing;

    private Anomaly anomaly;
    private Quaternion closedRotation;

    private void Awake()
    {
        reach = GetComponent<Interactable>();
        anomaly = GetComponent<Anomaly>();

        if (door == null)
            Debug.LogWarning($"{name} has no door; it will open without swinging", this);

        if (door != null) closedRotation = door.localRotation;

        if (enableOnDone != null)
        {
            enableOnDone.enabled = false;
            enableOnDone.SetReachable(false);
        }
    }

    private void OnEnable() => anomaly.Resolved += Close;
    private void OnDisable() => anomaly.Resolved -= Close;

    // Rolled on first use and fixed for the heist, so numbers set in one try still hold in the next
    public int[] CombinationFor(int length, int dialNumbers)
    {
        if (combination != null && combination.Length == length) return combination;

        combination = new int[length];
        int previous = 0;
        for (int i = 0; i < length; i++)
        {
            // An offset past the minimum turn either way, so the number is never right next to the last one
            int span = Mathf.Max(1, dialNumbers - 2 * minimumTurn - 1);
            int number = (previous + minimumTurn + 1 + Random.Range(0, span)) % dialNumbers;
            combination[i] = number;
            previous = number;
        }
        return combination;
    }

    public void RememberNumbers(int numbersSet) => NumbersSet = Mathf.Max(NumbersSet, numbersSet);

    [ContextMenu("Open")]
    public void Open()
    {
        if (IsOpen) return;

        if (interactOutline != null)
            interactOutline.SetActive(false);

        if (enableOnDone != null)
        {
            enableOnDone.enabled = true;
            enableOnDone.SetReachable(true);
        }

        IsOpen = true;
        IsCracked = true;
        reach.enabled = false;
        reach.SetReachable(false);
        anomaly.Raise();
        Opened?.Invoke();

        if (door == null) return;
        swing.Stop();
        swing = Tween.LocalRotation(door, closedRotation * Quaternion.AngleAxis(openAngle, hingeAxis), swingDuration, Ease.OutCubic);
    }

    [ContextMenu("Close")]
    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        anomaly.Clear();
        reach.enabled = true;
        reach.SetReachable(true);

        if (interactOutline != null)
            interactOutline.SetActive(true);

        if (enableOnDone != null)
        {
            enableOnDone.enabled = false;
            enableOnDone.SetReachable(false);
        }

        if (door == null) return;
        swing.Stop();
        swing = Tween.LocalRotation(door, closedRotation, swingDuration, Ease.OutCubic);
    }
}
