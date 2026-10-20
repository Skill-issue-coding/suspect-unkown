using System;
using UnityEngine;

// The glass cutter's rules, with no scene objects: the cut advances only while tension is held and the
// aim follows the dotted line, and straining out of line is reported once. PlayerGlassCutting feeds it
// input each frame; GlassCutterView only reads it
public class GlassCutterModel : IMinigameModel
{
    private const float FullCircle = 360f;

    public float CutAngle { get; private set; }
    public float Progress => Mathf.Clamp01(CutAngle / FullCircle);
    public bool IsComplete => CutAngle >= FullCircle;
    public bool IsAligned { get; private set; }
    public bool IsStraining { get; private set; }

    // Read by the HUD to show what the player is doing: whether tension is held this tick
    public bool HasTension { get; private set; }

    // Raised on the tick the player starts holding tension out of line, not every tick after
    public event Action Misaligned;

    private readonly float cutSpeed;

    public GlassCutterModel(float cutSpeed)
    {
        this.cutSpeed = cutSpeed;
    }

    public void Tick(float deltaTime, bool aligned, bool tension)
    {
        IsAligned = aligned;
        HasTension = tension;

        if (IsComplete)
        {
            IsStraining = false;
            return;
        }

        if (tension && aligned)
        {
            IsStraining = false;
            CutAngle = Mathf.Min(FullCircle, CutAngle + cutSpeed * deltaTime);
        }
        else if (tension)
        {
            if (!IsStraining)
            {
                IsStraining = true;
                Misaligned?.Invoke();
            }
        }
        else
        {
            IsStraining = false;
        }
    }
}
