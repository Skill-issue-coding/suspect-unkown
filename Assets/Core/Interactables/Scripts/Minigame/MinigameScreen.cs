using PrimeTween;
using UnityEngine;
using UnityEngine.Rendering;

// The part every minigame view shares: the camera that films the minigame into its RenderTexture, the
// background blur, the camera push-in as the minigame progresses, and the punch when it completes. Lives on
// the view's root beside the component that draws the minigame itself (LockpickView, GlassCutterView), so
// each new minigame only draws what is its own. PlayerMinigame calls Open and Close. Reads an
// IMinigameModel and decides nothing
public class MinigameScreen : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField, Tooltip("Camera that films the minigame into its RenderTexture; its field of view narrows as the minigame progresses")]
    private Camera screenCamera;

    [SerializeField, Tooltip("Degrees the field of view narrows at full progress")]
    private float pushInFov = 8f;

    [SerializeField, Tooltip("How quickly the camera follows the progress. Higher is snappier")]
    private float pushInSharpness = 8f;

    [Header("Completion")]
    [SerializeField, Tooltip("Optional; scale-punched when the minigame completes")]
    private Transform punchRoot;

    [SerializeField, Tooltip("Scale punch on the punch root when the minigame completes")]
    private float completePunch = 0.08f;

    [Header("Background Blur")]
    [SerializeField, Tooltip("Global Volume with Depth of Field, weight 0. Kept outside this object so it can fade out after the view hides")]
    private Volume blurVolume;

    [SerializeField, Tooltip("Seconds to blur in or out")]
    private float blurDuration = 0.25f;

    private IMinigameModel model;
    private Tween blurFade;
    private float baseFov;
    private bool celebrated;

    // Read by views that map the pointer into the minigame (GlassCutterView)
    public Camera Camera => screenCamera;

    private void Awake()
    {
        if (screenCamera == null)
            Debug.LogWarning($"{name} has no screen camera; the push-in is off", this);
        else
            baseFov = screenCamera.fieldOfView;
    }

    // The view starts inactive, so activating it here runs Awake on this and the drawing view before
    // either is used
    public void Open(IMinigameModel minigameModel)
    {
        gameObject.SetActive(true);

        model = minigameModel;
        celebrated = false;
        if (screenCamera != null) screenCamera.fieldOfView = baseFov;
        FadeBlur(1f);
    }

    public void Close()
    {
        if (model == null) return;

        model = null;
        FadeBlur(0f);
        gameObject.SetActive(false);
    }

    // Disabled from outside (scene unload, a parent switched off) without Close: still clear the blur
    private void OnDisable()
    {
        if (model == null) return;

        model = null;
        FadeBlur(0f);
    }

    private void LateUpdate()
    {
        if (model == null) return;

        // Smooths only what is displayed: the camera eases toward the progress, the model stays exact.
        // Exponential smoothing, so the feel does not depend on frame rate
        if (screenCamera != null)
        {
            float targetFov = baseFov - pushInFov * model.Progress;
            float blend = 1f - Mathf.Exp(-pushInSharpness * Time.deltaTime);
            screenCamera.fieldOfView = Mathf.Lerp(screenCamera.fieldOfView, targetFov, blend);
        }

        if (!celebrated && model.IsComplete)
        {
            celebrated = true;
            if (punchRoot != null) Tween.PunchScale(punchRoot, Vector3.one * completePunch, duration: 0.35f, frequency: 6);
        }
    }

    private void FadeBlur(float to)
    {
        if (blurVolume == null) return;

        blurFade.Stop();
        blurFade = Tween.Custom(blurVolume, blurVolume.weight, to, blurDuration,
            (volume, weight) => volume.weight = weight, Ease.OutQuad);
    }
}
