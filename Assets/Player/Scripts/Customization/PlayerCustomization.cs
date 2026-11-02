using System;
using UnityEngine;

// The player's outfit: one active part per category (Head, Chest, Legs, Feet), switched by turning
// the part meshes on and off, and remembered between sessions in PlayerPrefs by part name, so reordering
// or adding parts on the prefab never dresses a saved player in something else. Loaded in Awake, so the
// first frame and every reader's Start already see the saved outfit. The wardrobe's arrows call NextPart
// and PreviousPart. Raises Changed once per change, so readers (PlayerAppearance) rebuild only then.
// Parts with an OutfitPart price must be bought first: the arrows may show one as a preview, but only owned
// parts are saved or loaded, so a job is never played in something the player has not paid for
public class PlayerCustomization : MonoBehaviour
{
    // One key per category holding the part's name. The old keys held a position in the parts list;
    // they are read once if no name is saved yet, so outfits saved before the change carry over
    private const string NameKey = "PlayerLookPart_";
    private const string OldIndexKey = "PlayerLook_";

    [System.Serializable]
    public class CustomizationCategory
    {
        public string categoryName; // for example "Head"
        public GameObject[] parts;
        [HideInInspector] public int currentIndex = 0;
    }

    [Header("Add categories:")]
    public CustomizationCategory[] categories;

    public event Action Changed;

    private void Awake() => LoadAppearance();

    // Also called by the wardrobe when the player leaves it, to take off any part only being previewed
    public void LoadAppearance()
    {
        if (categories == null) return;

        ProgressSave save = ProgressStore.Load();
        for (int i = 0; i < categories.Length; i++)
        {
            categories[i].currentIndex = SavedIndex(categories[i], save);
            UpdateCategoryVisibility(i);
        }

        // Once, after every category is loaded, so nobody reads a half-loaded outfit
        Changed?.Invoke();
    }

    // Saves owned parts only. A category showing a part not yet bought keeps its last saved part
    public void SaveAppearance()
    {
        ProgressSave save = ProgressStore.Load();
        foreach (CustomizationCategory category in categories)
        {
            GameObject part = category.parts.Length > 0 ? category.parts[category.currentIndex] : null;
            if (part != null && IsOwned(part, save)) PlayerPrefs.SetString(NameKey + category.categoryName, part.name);
        }
        PlayerPrefs.Save();
    }

    // Clean money the part costs in the wardrobe; 0 for a part with no OutfitPart
    public static int PriceOf(GameObject part) =>
        part != null && part.TryGetComponent(out OutfitPart outfitPart) ? outfitPart.Price : 0;

    public static bool IsOwned(GameObject part, ProgressSave save) =>
        part != null && (PriceOf(part) == 0 || save.OwnsOutfitPart(part.name));

    // The saved part's position in this prefab's list: by name, else the old saved position, else the first
    // part. A saved part the player does not own (it gained a price since) falls back to the first owned one
    private int SavedIndex(CustomizationCategory category, ProgressSave save)
    {
        if (category.parts == null || category.parts.Length == 0) return 0;

        int index;
        string nameKey = NameKey + category.categoryName;
        if (PlayerPrefs.HasKey(nameKey))
        {
            string saved = PlayerPrefs.GetString(nameKey);
            index = -1;
            for (int j = 0; j < category.parts.Length; j++)
                if (category.parts[j] != null && category.parts[j].name == saved) { index = j; break; }
            if (index < 0)
            {
                Debug.LogWarning($"{name}: saved {category.categoryName} part {saved} no longer exists; wearing the first owned one", this);
                return FirstOwned(category, save);
            }
        }
        else
        {
            int old = PlayerPrefs.GetInt(OldIndexKey + category.categoryName, 0);
            index = old >= 0 && old < category.parts.Length ? old : 0;
        }

        return IsOwned(category.parts[index], save) ? index : FirstOwned(category, save);
    }

    private int FirstOwned(CustomizationCategory category, ProgressSave save)
    {
        for (int j = 0; j < category.parts.Length; j++)
            if (IsOwned(category.parts[j], save)) return j;

        Debug.LogWarning($"{name}: no {category.categoryName} part is free or owned; wearing the first one", this);
        return 0;
    }

    // A new player owns only the free parts, so every category needs at least one
    private void OnValidate()
    {
        if (categories == null) return;
        foreach (CustomizationCategory category in categories)
        {
            if (category.parts == null || category.parts.Length == 0) continue;

            bool anyFree = false;
            foreach (GameObject part in category.parts)
                if (part != null && PriceOf(part) == 0) { anyFree = true; break; }

            if (!anyFree)
                Debug.LogWarning($"{name}: every {category.categoryName} part has a price, so a new player has none to wear", this);
        }
    }

    public void NextPart(string categoryName)
    {
        if (categories == null || categories.Length == 0)
        {
            Debug.LogError("Categories array is empty or null!");
            return;
        }

        for (int i = 0; i < categories.Length; i++)
        {
            if (categories[i].categoryName == categoryName)
            {
                categories[i].currentIndex++;
                if (categories[i].currentIndex >= categories[i].parts.Length)
                    categories[i].currentIndex = 0;

                UpdateCategoryVisibility(i);
                SaveAppearance();
                Changed?.Invoke();
                return;
            }
        }
        Debug.LogWarning("Could not find category: " + categoryName);
    }

    public void PreviousPart(string categoryName)
    {
        if (categories == null || categories.Length == 0)
        {
            Debug.LogError("Categories array is empty or null!");
            return;
        }

        for (int i = 0; i < categories.Length; i++)
        {
            if (categories[i].categoryName == categoryName)
            {
                categories[i].currentIndex--;
                if (categories[i].currentIndex < 0)
                    categories[i].currentIndex = categories[i].parts.Length - 1;

                UpdateCategoryVisibility(i);
                SaveAppearance();
                Changed?.Invoke();
                return;
            }
        }
        Debug.LogWarning("Could not find category: " + categoryName);
    }

    // Shows only the category's current part
    private void UpdateCategoryVisibility(int index)
    {
        var cat = categories[index];
        for (int j = 0; j < cat.parts.Length; j++)
        {
            if (cat.parts[j] != null)
                cat.parts[j].SetActive(j == cat.currentIndex);
        }
    }
}
