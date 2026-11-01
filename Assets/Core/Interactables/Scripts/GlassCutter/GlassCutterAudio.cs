using UnityEngine;

// The glass cutter minigame's sound: a looping scratch that plays only while the cut advances (tension
// held on the line, circle not finished). Reads a GlassCutterModel and decides nothing. GlassCutterView
// calls Begin and End, as it does the HUD. The "glass comes free" sound is not here: the glass case
// plays it from GlassCutterInteraction.OnCut
[RequireComponent(typeof(AudioSource))]
public class GlassCutterAudio : MonoBehaviour
{
    private AudioSource source;
    private GlassCutterModel model;

    // Last state, so the source is only touched when cutting starts or stops
    private bool playing;
    // Whether the loop has been started this session. Pause and UnPause then resume mid-clip, so short
    // taps do not restart the scratch from its first frame
    private bool started;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        // The scratch is held for as long as the player cuts, so it must loop
        source.loop = true;

        if (source.resource == null)
            Debug.LogWarning($"{name} has no scratch clip on its AudioSource; cutting will be silent", this);
    }

    public void Begin(GlassCutterModel cutterModel)
    {
        End();
        model = cutterModel;
    }

    public void End()
    {
        source.Stop();
        model = null;
        playing = false;
        started = false;
    }

    // Continuous check, but it only compares two bools; the source changes only on a state change
    private void LateUpdate()
    {
        if (model == null) return;

        bool cutting = model.HasTension && model.IsAligned && !model.IsComplete;
        if (cutting == playing) return;
        playing = cutting;

        if (!cutting)
            source.Pause();
        else if (started)
            source.UnPause();
        else
        {
            source.Play();
            started = true;
        }
    }
}
