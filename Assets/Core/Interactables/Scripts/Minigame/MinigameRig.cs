using UnityEngine;

// The root of Minigames.prefab: every minigame view, the blur volume and the HUDs they share, in one
// prefab nested in PlayerHud, so a scene needs no minigame wiring of its own. Registers itself so the
// player's minigames can find their view: Player.prefab cannot reference objects inside another prefab's
// instance. Set in Awake and cleared on destroy, so a reloaded scene never keeps the old one
public class MinigameRig : MonoBehaviour
{
    public static MinigameRig Current { get; private set; }

    private void Awake()
    {
        Current = this;
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
    }
}
