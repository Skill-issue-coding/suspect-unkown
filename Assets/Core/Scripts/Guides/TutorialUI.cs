using UnityEngine;

public class TutorialUI : MonoBehaviour
{
    [Tooltip("The UI element to hide")]
    [SerializeField] private GameObject tutorialUIElement;

    [Tooltip("Reference to the player's input script.")]
    [SerializeField] private PlayerInputs playerInputs;

    [Tooltip("How long to wait after the player starts moving before hiding the UI.")]
    [SerializeField] private float hideDelay = 5f;

    private bool hasStartedMoving = false;
    private float moveTimer = 0f;

    private void Update()
    {
        // has the player started moving or are they moving right now
        if (hasStartedMoving || playerInputs.move != Vector2.zero)
        {
            hasStartedMoving = true; 
            moveTimer += Time.deltaTime;

            if (moveTimer >= hideDelay)
            {
                tutorialUIElement.SetActive(false); 
                this.enabled = false; 
            }
        }
    }
}