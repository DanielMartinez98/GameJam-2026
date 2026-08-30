using UnityEngine;
using TMPro;
using InterrogationRoom;

public class SceneNavigatorMenu : MonoBehaviour
{
    [SerializeField] private GameObject mainMenuUI;
    [SerializeField] private GameObject settingsMenuUI;
    [SerializeField] private GameObject creditsMenuUI;

    [Header("Opening cutscene")]
    //The picture and passage shown when the game is started, read the same way a memory's prologue is.
    //Left empty, the game starts straight into MainScene with no cutscene at all.
    [SerializeField] private Sprite introImage;
    [SerializeField, TextArea(3, 10)] private string introText;
    //The face the cutscene reads in. The menu scene does not otherwise load MTO, so the director cannot
    //find it by name the way it does in the interrogation room - it is handed in here instead.
    [SerializeField] private TMP_FontAsset cutsceneFont;
    //Wired if the scene authors its own overlay; otherwise one is added here and builds its own, exactly
    //as the interrogation room does.
    [SerializeField] private CutsceneDirector cutsceneDirector;

    public void StartGame()
    {
        //A cutscene only if there is one to play: a picture, some words, or both. With neither, the game
        //goes straight in rather than holding on a blank screen.
        if (introImage != null || !string.IsNullOrEmpty(introText))
        {
            if (cutsceneDirector == null)
            {
                cutsceneDirector = gameObject.AddComponent<CutsceneDirector>();
            }
            if (cutsceneFont != null)
            {
                cutsceneDirector.Font = cutsceneFont;
            }
            cutsceneDirector.Play(introImage, introText, LoadGameScene);
            return;
        }
        LoadGameScene();
    }

    private void LoadGameScene()
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
