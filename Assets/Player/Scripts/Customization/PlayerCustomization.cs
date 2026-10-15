using System;
using UnityEngine;

// The player's outfit: one active part per category (Head, Chest, Legs, Feet), switched by turning
// the part meshes on and off, and remembered between sessions in PlayerPrefs by part name, so reordering
// or adding parts on the prefab never dresses a saved player in something else. Loaded in Awake, so the
// first frame and every reader's Start already see the saved outfit. The wardrobe's arrows call NextPart
// and PreviousPart. Raises Changed once per change, so readers (PlayerAppearance) rebuild only then
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

    public void LoadAppearance()
    {
        if (categories == null) return;
        for (int i = 0; i < categories.Length; i++)
        {
            categories[i].currentIndex = SavedIndex(categories[i]);
            UpdateCategoryVisibility(i);
        }

        // Once, after every category is loaded, so nobody reads a half-loaded outfit
        Changed?.Invoke();
    }

    public void SaveAppearance()
    {
        foreach (CustomizationCategory category in categories)
        {
            GameObject part = category.parts.Length > 0 ? category.parts[category.currentIndex] : null;
            if (part != null) PlayerPrefs.SetString(NameKey + category.categoryName, part.name);
        }
        PlayerPrefs.Save();
    }

    // The saved part's position in this prefab's list: by name, else the old saved position, else the first part
    private int SavedIndex(CustomizationCategory category)
    {
        string nameKey = NameKey + category.categoryName;
        if (PlayerPrefs.HasKey(nameKey))
        {
            string saved = PlayerPrefs.GetString(nameKey);
            for (int j = 0; j < category.parts.Length; j++)
                if (category.parts[j] != null && category.parts[j].name == saved) return j;
            Debug.LogWarning($"{name}: saved {category.categoryName} part {saved} no longer exists; wearing the first one", this);
            return 0;
        }

        int old = PlayerPrefs.GetInt(OldIndexKey + category.categoryName, 0);
        return old >= 0 && old < category.parts.Length ? old : 0;
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
