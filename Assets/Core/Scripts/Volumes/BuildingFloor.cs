using System;
using UnityEngine;

// One floor of a building for BuildingFloors (decision 26): what fades with it, where it stands, and how
// far it fades while the player is below it
[Serializable]
public struct BuildingFloor
{
    [Tooltip("Everything under this fades with the floor. Leave empty for a floor that never fades, as the lowest")]
    public Transform root;

    [Tooltip("The tops of this floor's walls, under the root. They fade out while the player is on this floor, so the camera sees over them")]
    public Transform wallTops;

    [Tooltip("World Y of the floor's walking surface, in metres. Places NPCs, props and the player on a floor")]
    public float height;

    [Range(0f, 1f), Tooltip("How far the floor fades while the player is a floor or more below it. 1 is gone, 0.5 half dithered")]
    public float belowHide;

    [Tooltip("World box around the part straight over the floor below. Renderers centred in it fade fully, whatever belowHide says. Size 0 for none")]
    public Bounds fullHideArea;
}
