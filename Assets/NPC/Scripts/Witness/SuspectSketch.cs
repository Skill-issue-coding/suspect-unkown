using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

// The newspaper's witness sketch: a mannequin of the player model, posed in the first frame of the idle clip,
// dressed in the police description and rendered once into a RenderTexture at job end. Lives in the scene's
// SketchBooth, out of sight on the Sketch layer. Each outfit slot shows the part the description names: as it
// is when the witness is sure, see-through when unsure, a flat silhouette when the witness cannot say
public class SuspectSketch : MonoBehaviour
{
    private static readonly FeatureSlot[] Slots = (FeatureSlot[])System.Enum.GetValues(typeof(FeatureSlot));

    [SerializeField, Tooltip("The mannequin's Animator: a copy of the player model with the player's avatar")]
    private Animator mannequin;

    [SerializeField, Tooltip("The pose the sketch holds: frame 0 of this clip")]
    private AnimationClip pose;

    [Header("Dressing")]
    [SerializeField, Tooltip("Swapped onto a part the witness is unsure of: see-through, so the paper shows through")]
    private Material unsureMaterial;

    [SerializeField, Tooltip("Swapped onto the default part of a slot the witness cannot describe: a flat silhouette")]
    private Material unknownMaterial;

    [SerializeField, Range(0f, 1f), Tooltip("Color confidence from which a part is drawn as it is")]
    private float clearAt = 0.35f;

    [SerializeField, Range(0f, 1f), Tooltip("Color confidence from which a part is drawn faded; below it, a silhouette")]
    private float vagueAt = 0.2f;

    [Header("Render")]
    [SerializeField, Tooltip("Sees only the Sketch layer and draws into the newspaper's RenderTexture. Kept disabled: it renders once, on demand")]
    private Camera sketchCamera;

    [Header("Caption")]
    [SerializeField, Range(0f, 1f), Tooltip("Color confidence from which the caption states a part without hedging")]
    private float certainAt = 0.6f;

    [SerializeField, Tooltip("What a head part is called, by style: index 0 is style 001. A style past the end uses the last word")]
    private string[] headWords = { "hair", "hat", "hat" };

    [SerializeField, Tooltip("What a chest part is called, by style")]
    private string[] chestWords = { "jacket", "jacket" };

    [SerializeField, Tooltip("What a legs part is called, by style")]
    private string[] legsWords = { "trousers" };

    [SerializeField, Tooltip("What a feet part is called, by style")]
    private string[] feetWords = { "shoes", "boots", "shoes" };

    // One wearable part on the mannequin, with the material it came with
    private struct Part
    {
        public Renderer renderer;
        public OutfitFeature feature;
        public Material original;
    }

    private List<Part>[] parts;

    private PlayableGraph graph;

    private void Awake()
    {
        if (mannequin == null || pose == null || unsureMaterial == null || unknownMaterial == null || sketchCamera == null)
        {
            Debug.LogWarning($"{name} needs the mannequin, a pose clip, unsureMaterial, unknownMaterial and sketchCamera; there will be no witness sketch", this);
            enabled = false;
            return;
        }
        Pose();
        CollectParts();
    }

    // In the editor, a shader variant is compiled in the background the first time it is drawn, and nothing
    // is drawn until it is ready: the one real render at job end could lose its faded parts. Drawing every
    // look once at load compiles them in good time. Builds compile shaders up front; this costs one render
    private void Start()
    {
        Material[] looks = { null, unsureMaterial, unknownMaterial };
        for (int i = 0; i < Slots.Length; i++)
        {
            List<Part> slotParts = parts[i];
            if (slotParts.Count == 0) continue;
            foreach (Part part in slotParts) part.renderer.gameObject.SetActive(false);
            Show(slotParts[0], looks[i % looks.Length]);
        }
        sketchCamera.Render();
    }

    private void OnEnable() => JobWitnesses.Described += OnDescribed;

    private void OnDisable() => JobWitnesses.Described -= OnDescribed;

    private void OnValidate()
    {
        if (certainAt < clearAt)
            Debug.LogWarning($"{name} certainAt is below clearAt: the caption would hedge parts the sketch draws as sure", this);
        if (headWords.Length == 0 || chestWords.Length == 0 || legsWords.Length == 0 || feetWords.Length == 0)
            Debug.LogWarning($"{name} has a slot with no words: its caption would throw", this);
    }

    private void OnDestroy()
    {
        if (graph.IsValid()) graph.Destroy();
    }

    // Puts the mannequin in frame 0 of the pose clip and holds it there: the graph never plays, so the
    // pose costs nothing after this
    private void Pose()
    {
        AnimationPlayableUtilities.PlayClip(mannequin, pose, out graph);
        graph.Stop();
        graph.Evaluate(0f);
    }

    // Every child named Slot_Style_Color is a part; anything else (the base body, the armature) is left alone
    private void CollectParts()
    {
        parts = new List<Part>[Slots.Length];
        for (int i = 0; i < Slots.Length; i++) parts[i] = new List<Part>();

        foreach (Renderer renderer in mannequin.GetComponentsInChildren<Renderer>(true))
        {
            if (!OutfitFeature.TryParse(renderer.name, out FeatureSlot slot, out OutfitFeature feature)) continue;
            parts[(int)slot].Add(new Part { renderer = renderer, feature = feature, original = renderer.sharedMaterial });
        }
    }

    // Shows one part per slot as the report describes it: drawn as it is when sure, faded when unsure,
    // and the slot's first part as a silhouette when the witness cannot say
    public void Dress(WitnessReport report)
    {
        for (int i = 0; i < Slots.Length; i++)
        {
            List<Part> slotParts = parts[i];
            if (slotParts.Count == 0) continue;
            foreach (Part part in slotParts) part.renderer.gameObject.SetActive(false);

            bool known = report.TryDescribe(Slots[i], out OutfitFeature feature, out _, out float colorConfidence);
            if (!known || colorConfidence < vagueAt)
            {
                Show(slotParts[0], unknownMaterial);
                continue;
            }
            Show(Closest(slotParts, feature), colorConfidence >= clearAt ? null : unsureMaterial);
        }
    }

    private static void Show(Part part, Material swap)
    {
        part.renderer.sharedMaterial = swap != null ? swap : part.original;
        part.renderer.gameObject.SetActive(true);
    }

    // The part that matches the description best: exact, then the same style, then the same color, then any.
    // Confusion and merging can describe combinations the model does not have (no Chest_001_Purple), and
    // the sketch is grayscale, so the shape matters more than the exact color
    private static Part Closest(List<Part> slotParts, OutfitFeature wanted)
    {
        Part sameStyle = default, sameColor = default;
        bool hasStyle = false, hasColor = false;
        foreach (Part part in slotParts)
        {
            if (part.feature.style == wanted.style && part.feature.color == wanted.color) return part;
            if (!hasStyle && part.feature.style == wanted.style) { sameStyle = part; hasStyle = true; }
            if (!hasColor && part.feature.color == wanted.color) { sameColor = part; hasColor = true; }
        }
        return hasStyle ? sameStyle : hasColor ? sameColor : slotParts[0];
    }

    // The job ended and someone saw the player: dress, render once into the newspaper's RenderTexture, and hand
    // the caption to Core. Nobody saw anything: nothing is sent, and the paper keeps its "?"
    private void OnDescribed(WitnessReport report)
    {
        if (!report.SawAnything) return;

        Dress(report);
        sketchCamera.Render();

        string caption = Caption(report);
        Debug.Log($"Witness sketch: {caption}", this);
        JobNews.ReportWitness(caption);
    }

    // "Witness description: brown hair, red jacket, possibly grey trousers." Colors the witness is unsure of are
    // hedged, colors it cannot name are left out
    private string Caption(WitnessReport report)
    {
        List<string> items = new List<string>();
        foreach (FeatureSlot slot in Slots)
        {
            if (!report.TryDescribe(slot, out OutfitFeature feature, out _, out float colorConfidence)) continue;
            if (colorConfidence < vagueAt) continue;

            string hedge = colorConfidence >= certainAt ? "" : colorConfidence >= clearAt ? "probably " : "possibly ";
            items.Add($"{hedge}{feature.color.ToString().ToLowerInvariant()} {Word(slot, feature.style)}");
        }
        return items.Count == 0
            ? "Witness description: too vague to describe."
            : $"Witness description: {string.Join(", ", items)}.";
    }

    private string Word(FeatureSlot slot, int style)
    {
        string[] words = slot switch
        {
            FeatureSlot.Head => headWords,
            FeatureSlot.Chest => chestWords,
            FeatureSlot.Legs => legsWords,
            _ => feetWords,
        };
        return words[Mathf.Clamp(style - 1, 0, words.Length - 1)];
    }

    // Test: dresses the mannequin as the player is now, sure of the head, less of the chest, unsure of the
    // legs, blind to the feet. Play mode, then enable SketchCamera and look at the RenderTexture
    [ContextMenu("Test/Dress as the player, fading downward")]
    private void TestDressAsPlayer()
    {
        PlayerAppearance player = FindAnyObjectByType<PlayerAppearance>();
        if (player == null || parts == null) return;

        WitnessReport report = new WitnessReport();
        report.AddEvidence(FeatureSlot.Head, player.Current[FeatureSlot.Head], 5f);      // ~94 %: as it is
        report.AddEvidence(FeatureSlot.Chest, player.Current[FeatureSlot.Chest], 2f);    // ~45 %: as it is
        report.AddEvidence(FeatureSlot.Legs, player.Current[FeatureSlot.Legs], 1.2f);    // ~27 %: faded
        Dress(report);                                                                    // feet: silhouette
    }
}
