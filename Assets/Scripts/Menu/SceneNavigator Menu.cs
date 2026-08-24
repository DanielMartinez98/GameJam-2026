using UnityEngine;

public class SceneNavigatorMenu : MonoBehaviour
{
    [SerializeField] private GameObject mainMenuUI;
    [SerializeField] private GameObject settingsMenuUI;
    [SerializeField] private GameObject creditsMenuUI;
    public void StartGame()
    {
        // Load the main game scene
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainScene");
    }
    public void OpenSettings()
    {
        // Load the settings menu scene
        mainMenuUI.SetActive(false);
        settingsMenuUI.SetActive(true);
    }
    public void OpenCredits()
    {
        // Load the credits menu scene
        mainMenuUI.SetActive(false);
        creditsMenuUI.SetActive(true);
    }
    public void BackToMainMenu()
    {
        // Load the main menu scene
        settingsMenuUI.SetActive(false);
        creditsMenuUI.SetActive(false);
        mainMenuUI.SetActive(true);
    }
}
