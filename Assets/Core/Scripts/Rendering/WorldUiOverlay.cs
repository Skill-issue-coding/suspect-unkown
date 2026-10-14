using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Draws world-space UI (interaction prompts, NPC indicators: everything on the given layers) sharp,
// after post-processing. World UI writes no depth, so depth of field blurs each pixel of a prompt as
// much as whatever lies behind it: sharp over a wall top, blurry over the far floor. This camera stops
// drawing those layers and stacks an overlay camera without post-processing that draws only them.
// The overlay clears depth (URP only allows setting that in the Inspector), so world UI draws over
// walls. Put it on each scene's main camera
[RequireComponent(typeof(Camera))]
public class WorldUiOverlay : MonoBehaviour
{
    [SerializeField, Tooltip("Layers drawn by the overlay instead of this camera: sharp, after post-processing, on top")]
    private LayerMask layers = 1 << 5;

    private Camera baseCamera;
    private UniversalAdditionalCameraData baseData;
    private Camera overlay;
    private int baseMask;

    private void Awake()
    {
        baseCamera = GetComponent<Camera>();
        baseData = baseCamera.GetUniversalAdditionalCameraData();

        if (baseData.renderType != CameraRenderType.Base)
        {
            Debug.LogWarning($"{name} is not a Base camera, so world UI cannot be stacked on it", this);
            enabled = false;
            return;
        }

        var holder = new GameObject("WorldUiOverlay");
        holder.transform.SetParent(transform, false);

        overlay = holder.AddComponent<Camera>();
        overlay.cullingMask = layers;

        UniversalAdditionalCameraData overlayData = overlay.GetUniversalAdditionalCameraData();
        overlayData.renderType = CameraRenderType.Overlay;
        overlayData.renderPostProcessing = false;
        overlayData.renderShadows = false;
    }

    private void OnEnable()
    {
        if (overlay == null) return;

        baseMask = baseCamera.cullingMask;
        baseCamera.cullingMask &= ~layers;
        baseData.cameraStack.Add(overlay);
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        if (overlay == null) return;   // destroyed with the scene: nothing left to restore

        baseCamera.cullingMask = baseMask;
        baseData.cameraStack.Remove(overlay);
    }

    // Cinemachine sets the base lens in LateUpdate. The overlay copies it just before the base renders,
    // so both project the same way and the UI lands on the world it belongs to
    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera rendering)
    {
        if (rendering != baseCamera) return;

        overlay.fieldOfView = baseCamera.fieldOfView;
        overlay.nearClipPlane = baseCamera.nearClipPlane;
        overlay.farClipPlane = baseCamera.farClipPlane;
        overlay.orthographic = baseCamera.orthographic;
        overlay.orthographicSize = baseCamera.orthographicSize;
    }
}
