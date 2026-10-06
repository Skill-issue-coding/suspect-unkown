using UnityEngine;

public class CameraPanner : MonoBehaviour
{
    [Header("Cameras")]
    public GameObject vcam_Main;
    public GameObject vcam_Store;
    public GameObject vcam_Customize;

    [Header("UI Panels")]
    public GameObject storeUI;
    public GameObject customizeUI;

    private void Start()
    {
        GoToMain();
    }

    public void GoToMain()
    {
        vcam_Main.SetActive(true);
        vcam_Store.SetActive(false);
        vcam_Customize.SetActive(false);

        // Hide overlay menus on the main poster
        storeUI.SetActive(false);
        customizeUI.SetActive(false);
    }

    public void GoToStore()
    {
        vcam_Main.SetActive(false);
        vcam_Store.SetActive(true);
        vcam_Customize.SetActive(false);

        // Show store menu
        storeUI.SetActive(true);
        customizeUI.SetActive(false);
    }

    public void GoToCustomize()
    {
        vcam_Main.SetActive(false);
        vcam_Store.SetActive(false);
        vcam_Customize.SetActive(true);

        // Show customize menu
        storeUI.SetActive(false);
        customizeUI.SetActive(true);
    }
}