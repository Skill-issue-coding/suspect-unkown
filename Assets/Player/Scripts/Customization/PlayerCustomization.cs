using System;
using UnityEngine;

// The player's outfit: one active part per category (Head, Chest, Legs, Feet), switched by turning
// the part meshes on and off, and remembered between sessions in PlayerPrefs. The wardrobe's arrows call
// NextPart and PreviousPart. Raises Changed once per change, so readers (PlayerAppearance) rebuild only then
public class PlayerCustomization : MonoBehaviour
{
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

    void Start() => LoadAppearance();

    public void LoadAppearance()
    {
        for (int i = 0; i < categories.Length; i++)
        {
            // Save using the category name for accuracy
            categories[i].currentIndex = PlayerPrefs.GetInt("PlayerLook_" + categories[i].categoryName, 0);
            UpdateCategoryVisibility(i);
        }

        // Once, after every category is loaded, so nobody reads a half-loaded outfit
        Changed?.Invoke();
    }

    public void SaveAppearance()
    {
        for (int i = 0; i < categories.Length; i++)
        {
            PlayerPrefs.SetInt("PlayerLook_" + categories[i].categoryName, categories[i].currentIndex);
        }
        PlayerPrefs.Save();
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
