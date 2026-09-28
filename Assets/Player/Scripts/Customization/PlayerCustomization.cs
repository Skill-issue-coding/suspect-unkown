    using UnityEngine;

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

        void Start()
        {
            // Load saved customization
            LoadAppearance();
        }

        public void LoadAppearance()
        {
            for (int i = 0; i < categories.Length; i++)
            {
                // Save using the category name for accuracy
                categories[i].currentIndex = PlayerPrefs.GetInt("PlayerLook_" + categories[i].categoryName, 0);
                UpdateCategoryVisibility(i);
            }
        }

        public void SaveAppearance()
        {
            for (int i = 0; i < categories.Length; i++)
            {
                PlayerPrefs.SetInt("PlayerLook_" + categories[i].categoryName, categories[i].currentIndex);
            }
            PlayerPrefs.Save();
        }

        // --- BUTTONS ---

        // Pass the name of the category ("Head")
        public void NextPart(string categoryName)
        {
            Debug.Log("NextPart called for category: " + categoryName);
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
                        categories[i].currentIndex = 0; // wrap around

                    UpdateCategoryVisibility(i);
                    SaveAppearance(); // Save instantly
                    return;
                }
            }
            Debug.LogWarning("Could not find category: " + categoryName);
        }

        public void PreviousPart(string categoryName)
        {
            Debug.Log("PreviousPart called for category: " + categoryName);
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
                        categories[i].currentIndex = categories[i].parts.Length - 1; //wrap around

                    UpdateCategoryVisibility(i);
                    SaveAppearance(); // Save instantly
                    return;
                }
            }
            Debug.LogWarning("Could not find category: " + categoryName);
        }

        private void UpdateCategoryVisibility(int index)
        {
            var cat = categories[index];
            int validParts = 0;
            int nullParts = 0;
            for (int j = 0; j < cat.parts.Length; j++)
            {
                if (cat.parts[j] != null)
                {
                    // Activate the mesh if it matches the current index
                    cat.parts[j].SetActive(j == cat.currentIndex);
                    validParts++;
                }
                else
                {
                    nullParts++;
                }
            }
        }
    }