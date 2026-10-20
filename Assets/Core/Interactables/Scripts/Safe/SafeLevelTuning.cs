using System;
using UnityEngine;

// How hard one safe level is to crack: how many numbers the combination has, how wide the zone around
// each number is where the dial catches and testing sets it, and how fast testing goes. Plain data read by
// SafeModel; the levels themselves live in SafeTuning. Widths are degrees of dial turn, either side of the
// number
[Serializable]
public struct SafeLevelTuning
{
    [Tooltip("Numbers in the combination. SafeHud needs one pip per number")]
    public int numbers;

    [Tooltip("Degrees either side of the number where the dial catches and testing sets it. Half the spacing between numbers matches the number shown")]
    public float sweetSpot;

    [Tooltip("Degrees beyond the sweet spot where testing gets partway before it binds")]
    public float margin;

    [Tooltip("Full tests per second while tension is held")]
    public float testSpeed;

    [Tooltip("Seconds a binding test can be held before the dial slips")]
    public float slipTime;
}
