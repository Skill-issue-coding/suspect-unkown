using System;
using UnityEngine;

// The combination dial's rules, with no scene objects. The player turns the dial either way until it
// catches (OnNumber), which happens only with the next number of the combination under the marker, then
// holds tension to test it. At the number the test goes through and sets it; near it the test gets partway
// and binds, and held too long it slips. PlayerSafeCracking feeds it input each frame; SafeView and
// SafeHud only read it
public class SafeModel : IMinigameModel
{
    // Degrees turned clockwise from 0 under the marker. Unwrapped, so the dial can turn round and round
    public float DialAngle { get; private set; }

    // The number under the marker, 0 to DialNumbers - 1
    public int Number => Wrap(Mathf.RoundToInt(DialAngle / step));

    public int DialNumbers { get; }
    public int CombinationLength => combination.Length;
    public int NumbersSet { get; private set; }
    public bool IsOpen => NumbersSet >= combination.Length;

    // The next number of the combination is under the marker: the dial catches here
    public bool OnNumber { get; private set; }

    // How far the current test has got, 0 to 1. Reaching 1 sets the number
    public float TestProgress { get; private set; }

    public bool HasTension { get; private set; }
    public bool IsBinding { get; private set; }
    public bool IsLockedOut => lockout > 0f;
    public float Strain => level.slipTime > 0f ? Mathf.Clamp01(bindingTimer / level.slipTime) : 0f;

    float IMinigameModel.Progress => Mathf.Clamp01((NumbersSet + TestProgress) / combination.Length);
    bool IMinigameModel.IsComplete => IsOpen;

    public event Action NumberSet;
    public event Action Slipped;

    private readonly SafeLevelTuning level;
    private readonly int[] combination;
    private readonly float step;
    private readonly float returnSpeed;
    private readonly float slipLockout;

    private float bindingTimer;
    private float lockout;

    // Set after a number is set, so tension still held from that test does not start testing the next one
    private bool awaitingRelease;

    // numbersSet carries over numbers the player set in an earlier session at the same safe
    public SafeModel(SafeLevelTuning level, int[] combination, int dialNumbers, int numbersSet, float returnSpeed, float slipLockout)
    {
        this.level = level;
        this.combination = combination;
        this.returnSpeed = returnSpeed;
        this.slipLockout = slipLockout;

        DialNumbers = Mathf.Max(1, dialNumbers);
        step = 360f / DialNumbers;
        NumbersSet = Mathf.Clamp(numbersSet, 0, combination.Length);
    }

    public void Tick(float deltaTime, float dialDelta, bool tension)
    {
        if (IsOpen)
        {
            HasTension = false;
            IsBinding = false;
            OnNumber = false;
            return;
        }

        // 1. During a slip lockout, count it down and ignore tension
        if (lockout > 0f)
        {
            lockout -= deltaTime;
            tension = false;
        }

        if (!tension) awaitingRelease = false;
        if (awaitingRelease) tension = false;

        HasTension = tension;

        // 2. Without tension the dial turns and the test falls back
        if (!tension)
        {
            DialAngle += dialDelta;
            TestProgress = Mathf.MoveTowards(TestProgress, 0f, returnSpeed * deltaTime);
            IsBinding = false;
            bindingTimer = 0f;
            OnNumber = AtNumber(OffNumber());
            return;
        }

        // 3. With tension the dial is held and tested
        float off = OffNumber();
        // Off the number the test always binds short of 1, even at the sweet spot's very edge
        float maxTest = AtNumber(off) ? 1f : Mathf.Clamp(1f - (off - level.sweetSpot) / level.margin, 0f, 0.95f);

        TestProgress = Mathf.MoveTowards(TestProgress, maxTest, level.testSpeed * deltaTime);

        if (TestProgress >= 1f)
        {
            SetNumber();
            return;
        }

        // 4. Binding: the test has gone as far as it can
        if (TestProgress >= maxTest)
        {
            IsBinding = true;
            bindingTimer += deltaTime;

            if (bindingTimer >= level.slipTime)
            {
                TestProgress = 0f;
                lockout = slipLockout;
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

    private void SetNumber()
    {
        NumbersSet++;
        TestProgress = 0f;
        IsBinding = false;
        bindingTimer = 0f;
        OnNumber = false;
        awaitingRelease = true;
        NumberSet?.Invoke();
    }

    // Degrees between the dial and the next number, 0 to 180
    private float OffNumber()
    {
        float target = combination[NumbersSet] * step;
        return Mathf.Abs(Mathf.DeltaAngle(DialAngle, target));
    }

    // Inside the sweet spot and showing the number. The second check matters when the sweet spot is half the
    // spacing: exactly between two numbers the readout can show the neighbour, and the catch must agree with it
    private bool AtNumber(float off) => off <= level.sweetSpot && Number == combination[NumbersSet];

    private int Wrap(int number) => ((number % DialNumbers) + DialNumbers) % DialNumbers;
}
