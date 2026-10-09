using System;
using UnityEngine;

// Picks one outfit for the civilian's role at random when it spawns, so a room of the same prefab doesn't
// look like clones, and staff read as staff at a glance (concept 6.8: role is a tree input, and the
// uniform makes it visible). Outfits are skinned meshes on one shared skeleton, so switching is only
// turning objects on and off. Cosmetic only: civilians' clothes feed nothing in the witness model
[RequireComponent(typeof(CivilianBehaviour))]
public class CivilianRandomOutfit : MonoBehaviour
{
    [Header("Outfits per role")]
    [SerializeField, Tooltip("Outfits a customer can wear")]
    private GameObject[] customerOutfits;

    [SerializeField, Tooltip("Outfits an employee can wear. Should read as staff")]
    private GameObject[] employeeOutfits;

    [SerializeField, Tooltip("Outfits a manager can wear")]
    private GameObject[] managerOutfits;

    private void Awake()
    {
        CivilianRole role = GetComponent<CivilianBehaviour>().Role;
        GameObject[] pool = OutfitsFor(role);

        if (pool == null || pool.Length == 0)
        {
            Debug.LogWarning($"{name} has no outfits for role {role}; CivilianRandomOutfit disabled.", this);
            enabled = false;
            return;
        }

        HideAll(customerOutfits);
        HideAll(employeeOutfits);
        HideAll(managerOutfits);

        GameObject chosen = pool[UnityEngine.Random.Range(0, pool.Length)];
        if (chosen != null)
            chosen.SetActive(true);
    }

    private GameObject[] OutfitsFor(CivilianRole role) => role switch
    {
        CivilianRole.Customer => customerOutfits,
        CivilianRole.Employee => employeeOutfits,
        CivilianRole.Manager => managerOutfits,
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
