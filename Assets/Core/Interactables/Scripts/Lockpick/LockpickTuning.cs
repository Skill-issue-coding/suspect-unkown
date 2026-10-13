using System;
using UnityEngine;

[Serializable]
public struct LockLevelTuning
{
    [Tooltip("Half-width of the zone where the lock turns all the way")]
    public float sweetSpot;
    
    [Tooltip("Half-width beyond the sweet spot where it turns partway")]
    public float margin;
    
    [Tooltip("Full turns per second while turning freely")]
    public float turnSpeed;
    
    [Tooltip("How long the lock can bind before the pick slips")]
    public float slipTime;
}

[CreateAssetMenu(menuName = "Data/Lockpick Tuning")]
public class LockpickTuning : ScriptableObject
{
    public LockLevelTuning[] levels = new LockLevelTuning[3]
    {
        new LockLevelTuning { sweetSpot = 12f, margin = 40f, turnSpeed = 1.5f, slipTime = 1.0f },
        new LockLevelTuning { sweetSpot = 7f,  margin = 30f, turnSpeed = 1.2f, slipTime = 0.7f },
        new LockLevelTuning { sweetSpot = 3.5f, margin = 20f, turnSpeed = 1.0f, slipTime = 0.5f }
    };

    public float pickSpeed = 0.25f;
    public float stickSpeed = 180f;
    public float returnSpeed = 3f;
    public float slipLockout = 0.4f;

    private void OnValidate()
    {
        for (int i = 0; i < levels.Length; i++)
        {
            if (levels[i].sweetSpot > levels[i].margin)
                Debug.LogWarning($"Lock level {i + 1}: sweetSpot ({levels[i].sweetSpot}) exceeds margin ({levels[i].margin}).");
            
            if (levels[i].sweetSpot <= 0 || levels[i].margin <= 0 || levels[i].turnSpeed <= 0 || levels[i].slipTime <= 0)
                Debug.LogWarning($"Lock level {i + 1}: Values must be greater than zero.");
        }
    }
}