using UnityEngine;
// using PrimeTween; // Uncomment when you are ready to animate the door swinging

public class Door : MonoBehaviour
{
    [Range(1, 3)] public int lockLevel = 1;
    public bool startsLocked = true;

    public bool IsOpen { get; private set; }
    public bool IsLocked { get; private set; }
    public float SweetSpot { get; private set; } 

    private void Awake()
    {
        IsLocked = startsLocked;
        // Generate a random sweet spot between -80 and 80 degrees for this specific door
        SweetSpot = Random.Range(-130f, 130f); 
    }

    [ContextMenu("Open Door")]
    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        IsLocked = false;
        
        Debug.Log("Door Opened successfully!");
        // Tween.LocalRotation(transform, Quaternion.Euler(0f, 100f, 0f), 0.35f, Ease.OutCubic);
        // anomaly.Raise();
    }
}