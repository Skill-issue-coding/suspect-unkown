using PrimeTween;
using TMPro;
using UnityEngine;

// Announces what the job's civilians just did, under the status tag: "CIVILIAN SCARED" fades in, holds and
// fades out, like the pickup toast. Hears JobAlert.Reacted, so it knows no NPC (dependencies point NPC to
// Core). The same reaction while the line is still showing counts up ("2 CIVILIANS SCARED"); another one
// replaces it. "CIVILIAN ABOUT TO CALL" is the orange warning while the player can still get out of sight,
// "CIVILIAN LOST SIGHT" says they did; the police call is red, stays up and overrides everything until the
// job ends. A copy of PickupToast's fade on purpose: extract a shared toast only when a third one appears
public class ReactionToast : MonoBehaviour
{
    [SerializeField, Tooltip("Fades the toast in and out")]
    private CanvasGroup group;

    [SerializeField, Tooltip("The announcement line")]
    private TMP_Text label;

    [SerializeField, Tooltip("Seconds to fade in, and again to fade out")]
    private float fadeDuration = 0.2f;

    [SerializeField, Tooltip("Seconds the toast stays fully shown")]
    private float holdSeconds = 2f;

    [SerializeField, Tooltip("Text color for a reaction, and for the lost-sight line")]
    private Color reactionColor = new Color(0.93f, 0.91f, 0.86f, 1f);

    [SerializeField, Tooltip("Text color for the warning that a civilian is about to call, as the HUD ring")]
    private Color warningColor = new Color(1f, 0.5f, 0.2f, 1f);

    [SerializeField, Tooltip("Text color once a call is under way")]
    private Color callColor = new Color(1f, 0.3f, 0.27f, 1f);

    private Sequence show;
    private JobReaction shown;
    private int count;
    private bool callShown;

    private void Awake()
    {
        if (group == null || label == null)
        {
            Debug.LogWarning($"{name} needs a canvas group and a label; assign it in JobHud.prefab", this);
            enabled = false;
            return;
        }

        group.alpha = 0f;
    }

    private void OnEnable() => JobAlert.Reacted += OnReacted;

    private void OnDisable() => JobAlert.Reacted -= OnReacted;

    private void OnReacted(JobReaction reaction)
    {
        // The call is the last thing the player needs to know; nothing replaces it before the job fails
        if (callShown) return;

        // Counts while the line is still on screen, fading out included, so a burst reads as one line
        count = show.isAlive && reaction == shown ? count + 1 : 1;
        shown = reaction;
        callShown = reaction == JobReaction.Calling;

        label.text = Text(reaction, count);
        label.color = callShown ? callColor : reaction == JobReaction.AboutToCall ? warningColor : reactionColor;

        show.Stop();
        show = callShown
            ? Sequence.Create().Chain(Tween.Alpha(group, 1f, fadeDuration, Ease.OutQuad))
            : Sequence.Create()
                .Chain(Tween.Alpha(group, 1f, fadeDuration, Ease.OutQuad))
                .ChainDelay(holdSeconds)
                .Chain(Tween.Alpha(group, 0f, fadeDuration, Ease.InQuad));
    }

    private static string Text(JobReaction reaction, int count) => reaction switch
    {
        JobReaction.Scared => count == 1 ? "CIVILIAN SCARED" : $"{count} CIVILIANS SCARED",
        JobReaction.CalmedDown => count == 1 ? "CIVILIAN CALMED DOWN" : $"{count} CIVILIANS CALMED DOWN",
        JobReaction.Avoiding => count == 1 ? "CIVILIAN WALKING AWAY" : $"{count} CIVILIANS WALKING AWAY",
        JobReaction.Calling => "POLICE CALL IN PROGRESS",
        JobReaction.AboutToCall => count == 1 ? "CIVILIAN ABOUT TO CALL" : $"{count} CIVILIANS ABOUT TO CALL",
        JobReaction.LostSight => count == 1 ? "CIVILIAN LOST SIGHT" : $"{count} CIVILIANS LOST SIGHT",
        _ => "",
    };
}
