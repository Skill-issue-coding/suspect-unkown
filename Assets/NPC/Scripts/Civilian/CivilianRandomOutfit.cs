using System;
using UnityEngine;

// Picks one outfit for the civilian's role at random when it spawns, so a room of the same prefab doesn't
// look like clones, and staff read as staff at a glance (concept 6.8: role is a tree input, and the
// uniform makes it visible). Outfits are skinned meshes on one shared skeleton, so switching is only
// turning objects on and off. Picks again when the civilian's role is assigned after spawning, since a
// spawner builds every role from one prefab. Cosmetic only: civilians' clothes feed nothing in the
// witness model
[RequireComponent(typeof(CivilianBehaviour))]
public class CivilianRandomOutfit : MonoBehaviour
{
    [Header("Outfits per role")]
    [SerializeField, Tooltip("Outfits a customer can wear")]
    private GameObject[] customerOutfits;

    [SerializeField, Tooltip("Outfits an employee can wear. Should read as staff")]
    private GameObject[] employeeOutfits;

    private CivilianBehaviour civilian;

    private void Awake()
    {
        civilian = GetComponent<CivilianBehaviour>();
        Pick(civilian.Role);
    }

    private void OnEnable()
    {
        if (civilian != null) civilian.RoleChanged += OnRoleChanged;
    }

    private void OnDisable()
    {
        if (civilian != null) civilian.RoleChanged -= OnRoleChanged;
    }

    private void OnRoleChanged(CivilianRole from, CivilianRole to) => Pick(to);

    // A role with no outfits only warns: the component stays on, or a later role change could not pick
    private void Pick(CivilianRole role)
    {
        GameObject[] pool = OutfitsFor(role);

        if (pool == null || pool.Length == 0)
        {
            Debug.LogWarning($"{name} has no outfits for role {role}; keeping the current one.", this);
            return;
        }

        HideAll(customerOutfits);
        HideAll(employeeOutfits);

        GameObject chosen = pool[UnityEngine.Random.Range(0, pool.Length)];
        if (chosen != null)
            chosen.SetActive(true);
    }

    private GameObject[] OutfitsFor(CivilianRole role) => role switch
    {
        CivilianRole.Customer => customerOutfits,
        CivilianRole.Employee => employeeOutfits,
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    private static void HideAll(GameObject[] outfits)
    {
        if (outfits == null) return;
        foreach (GameObject outfit in outfits)
            if (outfit != null)
                outfit.SetActive(false);
    }
}
