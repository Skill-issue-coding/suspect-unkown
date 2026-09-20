using UnityEditor;
using UnityEngine;

// Draws the foot IK numbers where they act, in the scene view, with drag handles on each one.
// The settings are distances against real ground: tuning them from the inspector alone means
// guessing, playing, and guessing again, while the same numbers drawn on the rig read at a glance.
//
// Everything here works both in edit mode, against the rig's current pose, and in play mode,
// where it also shows the live blend weights and hip drop the solver produced this frame.
[CustomEditor(typeof(PlayerFootIk))]
public class PlayerFootIkEditor : Editor
{
    private static readonly Color ProbeColor = new Color(0.4f, 0.8f, 1.0f);
    private static readonly Color HitColor = new Color(0.4f, 1.0f, 0.4f);
    private static readonly Color MissColor = new Color(1.0f, 0.45f, 0.35f);
    private static readonly Color AnkleColor = new Color(1.0f, 0.85f, 0.2f);
    private static readonly Color HipColor = new Color(1.0f, 0.4f, 0.9f);

    private SerializedProperty _probeHeight;
    private SerializedProperty _probeDistance;
    private SerializedProperty _ankleHeight;
    private SerializedProperty _groundLayers;

    private GUIStyle _label;

    private void OnEnable()
    {
        _probeHeight = serializedObject.FindProperty("_probeHeight");
        _probeDistance = serializedObject.FindProperty("_probeDistance");
        _ankleHeight = serializedObject.FindProperty("_ankleHeight");
        _groundLayers = serializedObject.FindProperty("_groundLayers");
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Scene view:\n" +
            "Blue  - the probe ray. It starts Probe Height above the animated foot and reaches " +
            "Probe Distance below it. Ground outside that span is never found.\n" +
            "Green - where the probe hit, and the slope the foot tilts to.\n" +
            "Yellow - the IK target, Ankle Height above the hit. Raise it until the soles stop sinking.\n" +
            "Pink  - the hip drop the solver applied this frame (play mode).\n" +
            "Drag the cone handles to edit the values in place.",
            MessageType.None);
    }

    private void OnSceneGUI()
    {
        var footIk = (PlayerFootIk)target;
        var animator = footIk.GetComponent<Animator>();
        if (animator == null || !animator.isHuman) return;

        serializedObject.Update();

        DrawFoot(animator, HumanBodyBones.LeftFoot, "L", footIk.LeftFootWeight, true);
        DrawFoot(animator, HumanBodyBones.RightFoot, "R", footIk.RightFootWeight, false);
        DrawHips(animator, footIk.HipOffset);

        serializedObject.ApplyModifiedProperties();
    }

    // handles: only the left foot carries the drag handles, so the two feet cannot fight over
    // the same serialized value inside one frame.
    private void DrawFoot(Animator animator, HumanBodyBones bone, string side, float weight, bool handles)
    {
        Transform foot = animator.GetBoneTransform(bone);
        if (foot == null) return;

        float probeHeight = _probeHeight.floatValue;
        float probeDistance = _probeDistance.floatValue;
        float ankleHeight = _ankleHeight.floatValue;

        Vector3 footPosition = foot.position;
        Vector3 origin = footPosition + Vector3.up * probeHeight;
        Vector3 end = footPosition - Vector3.up * probeDistance;

        Handles.color = ProbeColor;
        Handles.DrawLine(origin, end, 2.0f);
        Handles.DrawWireDisc(origin, Vector3.up, 0.04f);
        Handles.DrawWireDisc(end, Vector3.up, 0.04f);
        Handles.DrawWireCube(footPosition, Vector3.one * 0.03f);

        bool hit = Physics.Raycast(origin, Vector3.down, out RaycastHit ground, probeHeight + probeDistance,
            _groundLayers.intValue, QueryTriggerInteraction.Ignore);

        if (hit)
        {
            Vector3 target = ground.point + Vector3.up * ankleHeight;

            Handles.color = HitColor;
            Handles.DrawWireDisc(ground.point, ground.normal, 0.09f);
            Handles.DrawLine(ground.point, ground.point + ground.normal * 0.15f);

            Handles.color = AnkleColor;
            Handles.DrawLine(ground.point, target);
            Handles.DrawWireDisc(target, Vector3.up, 0.05f);
            Handles.DrawDottedLine(target, footPosition, 3.0f);
        }

        Handles.color = hit ? HitColor : MissColor;
        Handles.Label(footPosition + Vector3.right * 0.08f, BuildLabel(side, hit, weight, footPosition, ground), Style());

        if (!handles) return;

        DragHandle(ref probeHeight, footPosition, Vector3.up, ProbeColor);
        DragHandle(ref probeDistance, footPosition, Vector3.down, ProbeColor);
        if (hit) DragHandle(ref ankleHeight, ground.point, Vector3.up, AnkleColor);

        _probeHeight.floatValue = probeHeight;
        _probeDistance.floatValue = probeDistance;
        _ankleHeight.floatValue = ankleHeight;
    }

    private void DrawHips(Animator animator, float hipOffset)
    {
        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        if (hips == null || Mathf.Abs(hipOffset) < 0.0005f) return;

        Vector3 applied = hips.position;
        Vector3 unshifted = applied - Vector3.up * hipOffset;

        Handles.color = HipColor;
        Handles.DrawLine(unshifted, applied, 3.0f);
        Handles.DrawWireDisc(unshifted, Vector3.up, 0.06f);
        Handles.Label(applied + Vector3.right * 0.08f, $"hip drop {hipOffset:F3} m", Style());
    }

    // Distance handles rather than a float field: the value is a length along an axis, and that
    // is what the cone drags out.
    private void DragHandle(ref float value, Vector3 anchor, Vector3 direction, Color color)
    {
        Vector3 position = anchor + direction * value;
        float size = HandleUtility.GetHandleSize(position) * 0.09f;

        Handles.color = color;
        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.Slider(position, direction, size, Handles.ConeHandleCap, 0.01f);
        if (EditorGUI.EndChangeCheck()) value = Mathf.Max(0.0f, Vector3.Dot(moved - anchor, direction));
    }

    private string BuildLabel(string side, bool hit, float weight, Vector3 foot, RaycastHit ground)
    {
        if (!hit) return $"{side}  no ground in probe span";

        float lift = ground.point.y + _ankleHeight.floatValue - foot.y;
        string weightText = Application.isPlaying ? $"\nIK weight {weight:F2}" : string.Empty;
        return $"{side}  hit {foot.y - ground.point.y:F3} m below foot\nlift {lift:+0.000;-0.000} m{weightText}";
    }

    private GUIStyle Style()
    {
        if (_label == null)
        {
            _label = new GUIStyle(EditorStyles.label);
            _label.normal.textColor = Color.white;
            _label.fontSize = 11;
        }

        return _label;
    }
}
