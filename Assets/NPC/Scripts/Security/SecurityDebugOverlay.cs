using System.Text;
using UnityEngine;

// SecurityDebugOverlay is purely for demonstration purposes only. It shows a
// debug view in game of the security meassures taken by the SecurityDirector
// in a level.
[RequireComponent(typeof(SecurityDirector))]
public class SecurityDebugOverlay : MonoBehaviour
{
    [SerializeField, Tooltip("The player's input; its debug key toggles the panel")]
    private PlayerInputs input;

    [SerializeField, Tooltip("The players job hud; used to disable it while the debug view is active")]
    private Transform jobHud;

    private SecurityDirector director;
    private bool visible;
    private string panelText;
    private GUIStyle style;

    private float[] cellShade;
    private string[] cellText;
    private const float CellSize = 30f;

    private SecurityLayout securityLayout;
    private Camera cam;
    private GUIStyle markerStyle;
    private Vector3[] markerPoints;
    private string[] markerLabels;

    private void Awake()
    {
        director = GetComponent<SecurityDirector>();
        securityLayout = GetComponent<SecurityLayout>();
        cam = Camera.main;
        if (input == null)
        {
            Debug.LogWarning($"{name} needs the player's PlayerInputs", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (input != null) input.SecurityDebugPressed += Toggle;
    }

    private void OnDisable()
    {
        if (input != null) input.SecurityDebugPressed -= Toggle;
    }

    private void Toggle()
    {
        visible = !visible;
        if (jobHud != null) jobHud.gameObject.SetActive(!visible);
        if (visible && panelText == null)
        {
            panelText = BuildText();
            BuildHeatmap();
            BuildMarkers();
        }
    }

    private void OnGUI()
    {
        if (!visible || panelText == null) return;
        style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 13, richText = true };
        GUI.Box(new Rect(10, 10, 610, Screen.height - 20), panelText, style);
        DrawHeatmap();
        DrawMarkers();
    }

    private string BuildText()
    {
        ActionModel model = director.Model;
        SecurityPlan plan = director.Plan;
        if (model == null || plan == null) return "Security: director has no plan";

        StringBuilder text = new StringBuilder();
        text.AppendLine($"<b>Security</b> {director.Record.completedRuns} completed runs, {plan.Guards} guards, {plan.Picks.Count} adaptive POIs\n");

        text.AppendLine("<b>Entrances</b>");
        for (int e = 0; e < model.EntranceCount; e++)
            text.AppendLine($"{director.Record.entrances[e],-18} {Bar(model.EntranceProbability(e))} {model.EntranceProbability(e):P0}");

        text.AppendLine("\n<b>Occupancy</b> (index = heatmap axis)");
        text.AppendLine($"{0,2}  {director.Record.rooms[0],-18} street, not counted");
        for (int r = 1; r < model.RoomCount; r++)
            text.AppendLine($"{r,2}  {director.Record.rooms[r],-18} {Bar(model.Occupancy(r))} {model.Occupancy(r):P0}");

        text.AppendLine("\n<b>Candidates</b> (standalone gain)");
        for (int c = 0; c < director.Candidates.Count; c++)
            text.AppendLine($"#{c} {director.Candidates[c].name,-18} {plan.StandaloneGains[c]:0.000}{PickMark(plan, c)}");

        text.AppendLine("\n<b>Why</b>");
        foreach (SecurityPick pick in plan.Picks)
            text.AppendLine(pick.explanation);

        return text.ToString();
    }

    private static string Bar(float p) => new string('#', Mathf.RoundToInt(p * 20f));

    private static string PickMark(SecurityPlan plan, int candidate)
    {
        for (int i = 0; i < plan.Picks.Count; i++)
            if (plan.Picks[i].candidate == candidate)
                return $"  <color=orange>pick {i + 1}, drains {plan.Picks[i].decayMultiplier:0.0}x</color>";
        return "";
    }

    private void BuildHeatmap()
    {
        ActionModel model = director.Model;
        if (model == null) return;

        int n = model.RoomCount;
        cellShade = new float[n * n];
        cellText = new string[n * n];

        float max = 0f;
        for (int from = 0; from < n; from++)
            for (int to = 0; to < n; to++)
                if (from != to) max = Mathf.Max(max, model.Transition(from, to));

        for (int from = 0; from < n; from++)
            for (int to = 0; to < n; to++)
            {
                float p = model.Transition(from, to);
                cellShade[from * n + to] = max > 0f ? p / max : 0f;
                cellText[from * n + to] = p >= 0.005f ? Mathf.RoundToInt(p * 100f).ToString() : "";
            }
    }

    private void BuildMarkers()
    {
        SecurityPlan plan = director.Plan;
        if (plan == null) return;

        markerPoints = new Vector3[plan.Picks.Count];
        markerLabels = new string[plan.Picks.Count];

        for (int i = 0; i < plan.Picks.Count; i++)
        {
            SecurityPick pick = plan.Picks[i];
            PointOfInterest poi = securityLayout.Candidates[pick.candidate].poi;
            if (poi == null) continue; // label stays null, nothing to draw

            PoiCandidate candidate = director.Candidates[pick.candidate];
            markerPoints[i] = poi.transform.position + Vector3.up * 1.5f;
            markerLabels[i] = $"<b>pick {i + 1}: {poi.name}</b> {pick.decayMultiplier:0.0}x\n" +
                              $"Rooms: {Names(candidate.rooms, director.Record.rooms)}\n" +
                              $"Entrance: {Names(candidate.entrances, director.Record.entrances)}";
        }
    }

    private static string Names(int[] indices, string[] names)
    {
        if (indices.Length == 0) return "-";
        string[] result = new string[indices.Length];
        for (int i = 0; i < indices.Length; i++)
            result[i] = names[indices[i]];
        return string.Join(", ", result);
    }

    private void DrawHeatmap()
    {
        if (cellShade == null) return;

        int n = director.Model.RoomCount;
        Vector2 origin = new Vector2(630f, 40f);
        GUI.Label(new Rect(origin.x, 10f, 400f, 24f), "<b>Room transitions</b> (row = from, column = to, %)", style);

        for (int from = 0; from < n; from++)
            for (int to = 0; to < n; to++)
            {
                Rect cell = new Rect(origin.x + (to + 1) * CellSize, origin.y + (from + 1) * CellSize, CellSize - 2f, CellSize - 2f);
                GUI.color = Color.Lerp(new Color(0.12f, 0.12f, 0.15f), new Color(1f, 0.6f, 0f), cellShade[from * n + to]);
                GUI.DrawTexture(cell, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(cell, cellText[from * n + to], style);
            }

        for (int i = 0; i < n; i++)
        {
            GUI.Label(new Rect(origin.x + (i + 1) * CellSize, origin.y, CellSize, CellSize), i.ToString(), style);
            GUI.Label(new Rect(origin.x, origin.y + (i + 1) * CellSize, CellSize, CellSize), i.ToString(), style);
        }
    }

    private void DrawMarkers()
    {
        if (markerLabels == null || cam == null) return;

        markerStyle ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 12, richText = true };

        for (int i = 0; i < markerLabels.Length; i++)
        {
            if (markerLabels[i] == null) continue;

            Vector3 screen = cam.WorldToScreenPoint(markerPoints[i]);
            if (screen.z <= 0f) continue; // behind the camera

            // IMGUI's y runs down from the top, the camera's runs up from the bottom
            Rect box = new Rect(screen.x - 110f, Screen.height - screen.y - 24f, 220f, 48f);
            GUI.Box(box, markerLabels[i], markerStyle);
        }
    }
}
