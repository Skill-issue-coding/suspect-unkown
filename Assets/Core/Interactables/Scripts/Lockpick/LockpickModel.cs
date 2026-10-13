using System;
using UnityEngine; // Needed for Mathf

public class LockpickModel
{
    public float PickAngle { get; private set; }
    public float LockTurn { get; private set; }  // 0 = rest, 1 = open
    public bool IsBinding { get; private set; }
    public bool IsOpen => LockTurn >= 1f;

    // Read by the HUD to show what the player can do right now: whether tension is applied this tick,
    // whether a slip lockout is running, and how close a binding lock is to slipping (0 to 1)
    public bool HasTension { get; private set; }
    public bool IsLockedOut => currentLockout > 0f;
    public float Strain => level.slipTime > 0f ? Mathf.Clamp01(bindingTimer / level.slipTime) : 0f;

    public event Action Slipped;

    private readonly LockLevelTuning level;
    private readonly float returnSpeed;
    private readonly float slipLockoutDuration;
    private readonly float sweetSpotAngle;

    private float bindingTimer;
    private float currentLockout;

    public LockpickModel(LockLevelTuning level, float returnSpeed, float slipLockout, float sweetSpotAngle)
    {
        this.level = level;
        this.returnSpeed = returnSpeed;
        this.slipLockoutDuration = slipLockout;
        this.sweetSpotAngle = sweetSpotAngle;
        
        PickAngle = 0f;
        LockTurn = 0f;
    }

    public void Tick(float deltaTime, float pickDelta, bool tension)
    {
        // 1. During a slip lockout, count it down, let the lock spring back, ignore tension
        if (currentLockout > 0f)
        {
            currentLockout -= deltaTime;
            tension = false;
        }

        HasTension = tension;

        // 2. Without tension
        if (!tension)
        {
            // Move the pick
            PickAngle = Mathf.Clamp(PickAngle + pickDelta, -135f, 135f);
            
            // Spring the lock back to 0
            LockTurn = Mathf.MoveTowards(LockTurn, 0f, returnSpeed * deltaTime);
            
            IsBinding = false;
            bindingTimer = 0f;
            return;
        }

        // 3. With tension
        float off = Mathf.Abs(PickAngle - sweetSpotAngle);
        float maxTurn = off <= level.sweetSpot ? 1f : Mathf.Clamp01(1f - (off - level.sweetSpot) / level.margin);

        LockTurn = Mathf.MoveTowards(LockTurn, maxTurn, level.turnSpeed * deltaTime);

        // 4. Binding
        if (LockTurn >= maxTurn && maxTurn < 1f)
        {
            IsBinding = true;
            bindingTimer += deltaTime;

            if (bindingTimer >= level.slipTime)
            {
                LockTurn = 0f;
                currentLockout = slipLockoutDuration;
                IsBinding = false;
                bindingTimer = 0f;
                Slipped?.Invoke();
            }
        }
        else
        {
            IsBinding = false;
        }
    }
}