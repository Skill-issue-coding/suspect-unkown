using UnityEngine;

// Shows what PlayerLighting found on its last tick; computes nothing itself, so what you tune is
// what NPCs read. Values update at the lighting tick rate, so they step rather than glide.
[RequireComponent(typeof(PlayerLighting))]
public class PlayerLightingDebug : MonoBehaviour
{
    [Header("Readout")]
    [SerializeField] private bool readout = true;
    [SerializeField] private Rect readoutRect = new Rect(240f, 10f, 200f, 125f);

    [Header("Gizmo")]
    [SerializeField] private bool levelGizmo = true;
    [SerializeField] private Color darkColor = new Color(0.1f, 0.1f, 0.4f);
    [SerializeField] private Color brightColor = new Color(1f, 0.9f, 0.3f);
    [SerializeField, Range(0.05f, 0.5f)] private float gizmoRadius = 0.2f;

    private PlayerLighting lighting;

    private bool TryGetLighting()
    {
        if (lighting == null)
            lighting = GetComponent<PlayerLighting>();
        return lighting != null;
    }

    private void OnGUI()
    {
        if (!readout || !TryGetLighting()) return;
        GUILayout.BeginArea(readoutRect, GUI.skin.box);
        GUILayout.Label("Player light");
        GUI.contentColor = Color.Lerp(darkColor, brightColor, lighting.Level);
        GUILayout.Label($"level     {lighting.Level:F2}");
        GUI.contentColor = Color.white;
        GUILayout.Label($"raw       {lighting.RawIntensity:F2}");
        GUILayout.Label($"lit       {lighting.DebugLitCount}");
        GUILayout.Label($"shadowed  {lighting.DebugShadowedCount}");
        GUILayout.EndArea();
    }

    private void OnDrawGizmos()
    {
        if (!levelGizmo || !Application.isPlaying || !TryGetLighting()) return;
        if (lighting.SamplePoint == null) return;

        Gizmos.color = Color.Lerp(darkColor, brightColor, lighting.Level);
        Gizmos.DrawSphere(lighting.SamplePoint.position, gizmoRadius);
    }
}
