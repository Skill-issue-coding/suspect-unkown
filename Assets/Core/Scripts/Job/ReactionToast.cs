using PrimeTween;
using TMPro;
using UnityEngine;

// Announces what the job's civilians and guards just did, under the status tag: "CIVILIAN SCARED" fades in, holds and
// fades out, like the pickup toast. Hears JobAlert.Reacted, so it knows no NPC (dependencies point NPC to
// Core). The same reaction while the line is still showing counts up ("2 CIVILIANS SCARED"); another one
// replaces it. "CIVILIAN ABOUT TO CALL" is the orange warning while the player can still get out of sight,
// "CIVILIAN LOST SIGHT" says they did; guards announce noticing, investigating, chasing ("GUARD CHASING
// YOU" in red), losing the player and giving up; the police call is red, stays up and overrides everything
// until the job ends. A copy of PickupToast's fade on purpose: extract a shared toast only when a third one appears
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

    [SerializeField, Tooltip("Text color for the lines that are no threat yet or any more: scared, calmed down, lost sight, a guard curious, losing you or giving up")]
    private Color reactionColor = new Color(0.93f, 0.91f, 0.86f, 1f);

    [SerializeField, Tooltip("Text color for the warnings the player can still act on: a civilian about to call, a guard investigating")]
    private Color warningColor = new Color(1f, 0.5f, 0.2f, 1f);

    [SerializeField, Tooltip("Text color while the job is at stake: a call under way, a guard chasing")]
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
        label.color = ColorFor(reaction);

        show.Stop();
        show = Sequence.Create();
        if (group.alpha < 1f)
            show.Chain(Tween.Alpha(group, 1f, fadeDuration, Ease.OutQuad));
        if (!callShown)
            show.ChainDelay(holdSeconds)
                .Chain(Tween.Alpha(group, 0f, fadeDuration, Ease.InQuad));
    }

    // Red for the two moments the job is at stake (a call, a chase), orange for the warnings that can still
    // be acted on, off-white for the rest
    private Color ColorFor(JobReaction reaction) => reaction switch
    {
        JobReaction.Calling or JobReaction.GuardChasing => callColor,
        JobReaction.AboutToCall or JobReaction.GuardInvestigating => warningColor,
        _ => reactionColor,
    };

    private static string Text(JobReaction reaction, int count) => reaction switch
    {
        JobReaction.Scared => Civilians(count, "SCARED"),
        JobReaction.CalmedDown => Civilians(count, "CALMED DOWN"),
        JobReaction.Avoiding => Civilians(count, "WALKING AWAY"),
        JobReaction.Calling => "POLICE CALL IN PROGRESS",
        JobReaction.AboutToCall => Civilians(count, "ABOUT TO CALL"),
        JobReaction.LostSight => Civilians(count, "LOST SIGHT"),
        JobReaction.GuardCurious => Guards(count, "CURIOUS"),
        JobReaction.GuardInvestigating => Guards(count, "INVESTIGATING"),
        JobReaction.GuardChasing => Guards(count, "CHASING YOU"),
        JobReaction.GuardLostYou => Guards(count, "LOST YOU"),
        JobReaction.GuardGaveUp => Guards(count, "GAVE UP"),
        _ => "",
    };

    private static string Civilians(int count, string what) => count == 1 ? $"CIVILIAN {what}" : $"{count} CIVILIANS {what}";

    private static string Guards(int count, string what) => count == 1 ? $"GUARD {what}" : $"{count} GUARDS {what}";
}
