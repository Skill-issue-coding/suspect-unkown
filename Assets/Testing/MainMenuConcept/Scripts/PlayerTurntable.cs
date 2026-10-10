using UnityEngine;
using UnityEngine.EventSystems;

// Drag the player on the wardrobe pedestal to spin them, so the back of the outfit can be
// checked too. Sits on a collider around the pedestal and is picked up by the camera's
// PhysicsRaycaster; works only through drag events, nothing per frame.
[RequireComponent(typeof(Collider))]
public class PlayerTurntable : MonoBehaviour, IDragHandler
{
    [Tooltip("Transform that is spun, normally the player root")]
    [SerializeField] private Transform target;
    [Tooltip("Degrees of yaw per pixel dragged")]
    [SerializeField] private float degreesPerPixel = 0.4f;

    private void Awake()
    {
        if (target == null)
        {
            Debug.LogWarning($"{name} has no target to spin", this);
            enabled = false;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!enabled) return;
        target.Rotate(0f, -eventData.delta.x * degreesPerPixel, 0f, Space.World);
    }
}
