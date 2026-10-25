using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Fades something that moves between floors, such as an NPC, with the floor it stands on (decision 26):
// gone while the player is on another floor, fading in and out as the player walks the stairs. Its floor
// comes from its height, re-checked on a slow tick, since NPCs change floor only on stairs. Props that
// stay put go through BuildingFloors' prop roots instead. In a level without BuildingFloors it does
// nothing, so prefabs can carry it everywhere
public class FloorHideable : MonoBehaviour
{
    [SerializeField, Min(0.05f), Tooltip("Seconds between checks of which floor this stands on")]
    private float checkInterval = 0.5f;

    // 0 shown, 1 gone
    public float Hide { get; private set; }

    public event Action<float> HideChanged;

    private RendererFade[] fades;
    private BuildingFloors floors;
    private WaitForSeconds wait;
    private int floor;
    private bool started;

    private void Awake()
    {
        // Inactive renderers too: a civilian picks its outfit after this
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        var list = new List<RendererFade>(renderers.Length);
        foreach (Renderer r in renderers)
        {
            var fade = new RendererFade(r, false);
            if (fade.CanFade) list.Add(fade);
        }

        fades = list.ToArray();
        wait = new WaitForSeconds(checkInterval);
    }

    // BuildingFloors.Instance is set in its Awake, which may run after this object's OnEnable
    private void Start()
    {
        floors = BuildingFloors.Instance;
        if (floors == null)
        {
            enabled = false;
            return;
        }

        started = true;
        Attach();
    }

    private void OnEnable()
    {
        if (started) Attach();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (floors != null) floors.Unregister(this);
    }

    // Called by BuildingFloors while the floors fade, and here when the floor changes
    public void Refresh()
    {
        float hide = floors.HideOnFloor(floor);
        if (Mathf.Approximately(hide, Hide)) return;

        Hide = hide;
        foreach (RendererFade fade in fades)
            fade.Set(hide);
        HideChanged?.Invoke(hide);
    }

    private void Attach()
    {
        floors.Register(this);
        floor = floors.FloorAt(transform.position.y);
        Refresh();
        StartCoroutine(CheckFloor());
    }

    private IEnumerator CheckFloor()
    {
        // Staggered, so NPCs spawned together do not all check on the same frame
        yield return new WaitForSeconds(UnityEngine.Random.Range(0f, checkInterval));
        while (true)
        {
            int current = floors.FloorAt(transform.position.y);
            if (current != floor)
            {
                floor = current;
                Refresh();
            }
            yield return wait;
        }
    }
}
