using UnityEngine;
using UnityEngine.Splines;

// A camera path between two menu stations, so the switch travels around furniture instead of
// blending through it in a straight line. Only the spline's shape matters: its ends need not sit
// exactly on the two cameras, because the path is shifted so it starts and ends at their positions.
// Either direction plays the same path. MainMenuConcept moves its travel camera along it
[RequireComponent(typeof(SplineContainer))]
public class CameraRoute : MonoBehaviour
{
    [SerializeField, Tooltip("Station at the start of the spline")]
    private MenuStation from = MenuStation.Main;

    [SerializeField, Tooltip("Station at the end of the spline")]
    private MenuStation to = MenuStation.Shop;

    [SerializeField, Tooltip("Seconds the trip takes")]
    private float duration = 1.3f;

    private SplineContainer path;
    private Vector3 pathStart, pathEnd;

    public float Duration => duration;

    private void OnValidate()
    {
        if (from == to) Debug.LogWarning($"{name}: a route needs two different stations", this);
        if (duration <= 0f) Debug.LogWarning($"{name}: duration must be above 0", this);
    }

    private void Awake()
    {
        path = GetComponent<SplineContainer>();
        // The spline does not move at runtime, so its ends are read once
        pathStart = path.EvaluatePosition(0f);
        pathEnd = path.EvaluatePosition(1f);
    }

    // True when this route joins the two stations; reversed when it runs from its end to its start
    public bool Connects(MenuStation source, MenuStation destination, out bool reversed)
    {
        reversed = source == to && destination == from;
        return reversed || (source == from && destination == to);
    }

    // Position at progress 0 to 1 of the trip from sourcePosition to destinationPosition
    public Vector3 Evaluate(float progress, bool reversed, Vector3 sourcePosition, Vector3 destinationPosition)
    {
        float u = reversed ? 1f - progress : progress;
        Vector3 atStart = reversed ? destinationPosition : sourcePosition;
        Vector3 atEnd = reversed ? sourcePosition : destinationPosition;

        // Shift the path so its ends land on the cameras, fading from one correction to the other
        Vector3 shift = Vector3.Lerp(atStart - pathStart, atEnd - pathEnd, u);
        return (Vector3)path.EvaluatePosition(u) + shift;
    }
}
