using UnityEngine;

// Puts the AudioListener at the player's head but turned like the camera. The camera sits far from the
// player (CameraDistance 20), so a listener on it hears every 3D sound from too far away; turned with the
// player instead, left and right would flip whenever the character turns. Lives on its own object at the
// scene root, and replaces the AudioListener on the main camera
[RequireComponent(typeof(AudioListener))]
public class ListenerAnchor : MonoBehaviour
{
    [SerializeField, Tooltip("What the listener stands at: the player")]
    private Transform target;

    [SerializeField, Tooltip("Offset from the target in metres; about head height")]
    private Vector3 offset = new Vector3(0f, 1.6f, 0f);

    private Transform cameraTransform;

    private void Start()
    {
        // Cached once: Camera.main does not belong in a per-frame path
        Camera mainCamera = Camera.main;
        cameraTransform = mainCamera != null ? mainCamera.transform : null;

        if (target == null || cameraTransform == null)
        {
            Debug.LogWarning($"{name} needs a target and a camera tagged MainCamera; 3D sounds will be heard from here", this);
            enabled = false;
        }
    }

    // Late, so it follows the camera after Cinemachine has moved it this frame
    private void LateUpdate()
    {
        transform.SetPositionAndRotation(target.position + offset, cameraTransform.rotation);
    }
}
