    using UnityEngine;
    using UnityEngine.SceneManagement;

    public class MainMenuManager : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The exact name of the scene to load when Play is pressed.")]
        private string gameSceneName = "TestingScene";

        public void StartGame()
        {
            SceneManager.LoadScene(gameSceneName);
        }
    }