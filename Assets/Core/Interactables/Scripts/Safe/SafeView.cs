using PrimeTween;
using TMPro;
using UnityEngine;

// The safe-cracking minigame's own drawing: the 3D door with its dial and handle, the numbers on the dial,
// the catch on the right number (the dial jumps, the handle kicks), the bind shake, the slip rattle, the handle turning once it opens, and
// the HUD. The camera, blur, push-in and open punch are the MinigameScreen beside it. Reads a SafeModel
// and decides nothing. PlayerSafeCracking calls Begin and End. The dial face owns how many numbers the
// dial has, so PlayerSafeCracking reads DialNumbers from here when it builds the model
[RequireComponent(typeof(MinigameScreen))]
public class SafeView : MonoBehaviour
{
    [Header("Hierarchy")]
    [SerializeField, Tooltip("The door with the dial and handle, shaken when the dial slips")]
    private Transform safeRoot;

    [SerializeField, Tooltip("Turned by the dial angle. Pivot must sit on the dial's centre")]
    private Transform dial;

    [SerializeField, Tooltip("Twitches at the catch and turns once the safe opens. Pivot must sit on its hub")]
    private Transform handle;

    [Header("Dial")]
    [SerializeField, Tooltip("Positions around the dial, numbered from 0. Rebuild the dial face after changing it")]
    private int dialNumbers = 20;

    [SerializeField, Tooltip("Dial's local axis pointing out of the door toward the viewer; turning around it is clockwise as seen from the front")]
    private Vector3 dialAxis = Vector3.down;

    [SerializeField, Tooltip("Degrees the dial shakes at most while a test binds")]
    private float maxShakeAngle = 1.5f;

    [SerializeField, Tooltip("Degrees the handle kicks when the dial catches on the right number")]
    private float catchTwitch = 10f;

    [SerializeField, Tooltip("Metres the dial jumps toward the viewer when it catches on the right number, in the door's local space")]
    private float catchJump = 0.006f;

    [SerializeField, Tooltip("Scale punch on the dial when a number is set")]
    private float setPunch = 0.06f;

    [Header("Handle")]
    [SerializeField, Tooltip("Handle's local axis it turns around")]
    private Vector3 handleAxis = Vector3.up;

    [SerializeField, Tooltip("Degrees the handle turns when the safe opens. The sign picks the way it turns")]
    private float handleOpenAngle = -90f;

    [SerializeField, Tooltip("Seconds the handle takes to turn open")]
    private float handleTurnSeconds = 0.3f;

    [Header("Dial Face")]
    [SerializeField, Tooltip("Font for the numbers and ticks")]
    private TMP_FontAsset faceFont;

    [SerializeField, Tooltip("Colour of the numbers and ticks")]
    private Color faceColor = new Color(0.08f, 0.08f, 0.09f, 1f);

    [SerializeField, Tooltip("Metres from the dial's pivot to the face it is printed on, toward the viewer")]
    private float faceDepth = 0.003f;

    [SerializeField, Tooltip("Metres from the dial's centre to the middle of the numbers")]
    private float numberRadius = 0.06f;

    [SerializeField, Tooltip("Metres from the dial's centre to the middle of the ticks")]
    private float tickRadius = 0.0705f;

    [SerializeField, Tooltip("Font size of the numbers")]
    private float numberSize = 0.11f;

    [SerializeField, Tooltip("Font size of the ticks")]
    private float tickSize = 0.06f;

    [SerializeField, Tooltip("A number is printed on every this many positions; the rest get a tick only")]
    private int numberEvery = 5;

    [Header("HUD")]
    [SerializeField, Tooltip("The safe HUD beside this view in Minigames.prefab. Kept outside this object so it can fade out after the view hides")]
    private SafeHud hud;

    private const string FaceName = "DialFace";

    private SafeModel currentModel;
    private Quaternion initialDialRotation;
    private Quaternion initialHandleRotation;
    private Tween handleTween;
    private Tween catchJumpTween;
    private Tween catchKickTween;
    private float bindingTimer;
    private bool wasOnNumber;
    private bool handleTurned;

    public int DialNumbers => dialNumbers;

    private void Awake()
    {
        if (safeRoot == null || dial == null || handle == null)
        {
            Debug.LogWarning($"{name} is missing hierarchy references; the safe view is disabled", this);
            enabled = false;
            return;
        }

        initialDialRotation = dial.localRotation;
        initialHandleRotation = handle.localRotation;
    }

    public void Begin(SafeModel model)
    {
        if (!enabled) return;

        Detach();
        currentModel = model;
        currentModel.NumberSet += OnNumberSet;
        currentModel.Slipped += OnSlipped;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        bindingTimer = 0f;
        wasOnNumber = false;
        handleTurned = false;
        catchJumpTween.Complete();
        catchKickTween.Complete();
        handleTween.Stop();
        handle.localRotation = initialHandleRotation;

        if (hud != null) hud.Show(model);
    }

    public void End()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (currentModel == null) return;

        if (hud != null) hud.Hide();
        Detach();
    }

    private void OnDisable()
    {
        End();
    }

    private void Detach()
    {
        if (currentModel == null) return;

        currentModel.NumberSet -= OnNumberSet;
        currentModel.Slipped -= OnSlipped;
        currentModel = null;
    }

    private void LateUpdate()
    {
        if (currentModel == null) return;

        float shake = 0f;
        if (currentModel.IsBinding)
        {
            bindingTimer += Time.deltaTime;
            shake = Mathf.Sin(Time.time * 60f) * Mathf.Lerp(0f, maxShakeAngle, bindingTimer);
        }
        else
        {
            bindingTimer = 0f;
        }

        dial.localRotation = initialDialRotation * Quaternion.AngleAxis(currentModel.DialAngle + shake, dialAxis);

        // The catch: the dial jumps and the handle kicks as the right number comes under the marker, once
        // on the way in, not every frame it stays there
        if (currentModel.OnNumber && !wasOnNumber && !handleTurned)
        {
            // A punch started mid-punch would take the offset as its rest and leave the part out of place
            catchJumpTween.Complete();
            catchKickTween.Complete();
            catchJumpTween = Tween.PunchLocalPosition(dial, initialDialRotation * dialAxis.normalized * catchJump, duration: 0.25f, frequency: 6);
            catchKickTween = Tween.PunchLocalRotation(handle, handleAxis.normalized * catchTwitch, duration: 0.25f, frequency: 8);
        }
        wasOnNumber = currentModel.OnNumber;

        if (!handleTurned && currentModel.IsOpen)
        {
            handleTurned = true;
            catchKickTween.Complete();
            handleTween.Stop();
            handleTween = Tween.LocalRotation(handle, initialHandleRotation * Quaternion.AngleAxis(handleOpenAngle, handleAxis),
                handleTurnSeconds, Ease.OutBack);
        }
    }

    private void OnNumberSet()
    {
        Tween.PunchScale(dial, Vector3.one * setPunch, duration: 0.25f, frequency: 6);
    }

    private void OnSlipped()
    {
        Tween.PunchLocalPosition(safeRoot, new Vector3(0.01f, 0f, 0f), duration: 0.3f, frequency: 12);
    }

    // Prints the numbers and ticks on the dial as children, so they turn with it. Number n sits n
    // positions counter-clockwise from the top, so turning the dial clockwise by n positions brings it
    // under the marker. Run in prefab mode after changing the dial count or the face settings
    [ContextMenu("Rebuild Dial Face")]
    private void RebuildDialFace()
    {
        if (dial == null || faceFont == null)
        {
            Debug.LogWarning($"{name} needs a dial and a face font to rebuild the dial face", this);
            return;
        }

        Transform old = dial.Find(FaceName);
        if (old != null) DestroyImmediate(old.gameObject);

        Transform face = new GameObject(FaceName).transform;
        face.SetParent(dial, false);
        face.gameObject.layer = dial.gameObject.layer;

        Vector3 toViewer = dialAxis.normalized;

        // "Up" on the face is the dial's local axis that points to the marker at rest: the one of the
        // dial's axes, other than the turning axis, that points most upward in the world
        Vector3 up = FaceUp(toViewer);

        for (int n = 0; n < dialNumbers; n++)
        {
            bool numbered = numberEvery > 0 && n % numberEvery == 0;

            // Clockwise as seen from the front is around toViewer, so counter-clockwise is the negative angle
            Vector3 radial = Quaternion.AngleAxis(-n * 360f / dialNumbers, toViewer) * up;

            AddMark(face, n.ToString(), numberRadius, numberSize, radial, toViewer, numbered);
            AddMark(face, "|", tickRadius, tickSize, radial, toViewer, true);
        }
    }

    private void AddMark(Transform face, string text, float radius, float size, Vector3 radial, Vector3 toViewer, bool show)
    {
        if (!show) return;

        GameObject mark = new GameObject(text == "|" ? "Tick" : $"Number {text}");
        mark.layer = face.gameObject.layer;
        mark.transform.SetParent(face, false);
        mark.transform.localPosition = toViewer * faceDepth + radial * radius;

        // TMP text reads from its local -Z side, and the tops of the marks point outward
        mark.transform.localRotation = Quaternion.LookRotation(-toViewer, radial);

        TextMeshPro label = mark.AddComponent<TextMeshPro>();
        label.font = faceFont;
        label.text = text;
        label.fontSize = size;
        label.color = faceColor;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.rectTransform.sizeDelta = new Vector2(radius, radius);
    }

    private Vector3 FaceUp(Vector3 toViewer)
    {
        Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        Vector3 best = Vector3.up;
        float bestDot = float.MinValue;
        foreach (Vector3 axis in axes)
        {
            if (Mathf.Abs(Vector3.Dot(axis, toViewer)) > 0.5f) continue;

            float dot = Vector3.Dot(dial.TransformDirection(axis), Vector3.up);
            if (dot > bestDot)
            {
                bestDot = dot;
                best = axis;
            }
        }
        return best;
    }
}
