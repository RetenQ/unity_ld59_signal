using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartMenuController : MonoBehaviour
{
    [SerializeField] private string continueBlockedSceneName = "Start";

    private LevelManager levelManager;
    private Button startButton;
    private Button continueButton;
    private Button exitButton;

    private void Awake()
    {
        levelManager = FindObjectOfType<LevelManager>();
        if (levelManager == null)
        {
            var go = new GameObject("LevelManager");
            levelManager = go.AddComponent<LevelManager>();
        }

        BindButtons();
    }

    private void BindButtons()
    {
        startButton = FindButton("Start Game");
        continueButton = FindButton("Continue Game");
        exitButton = FindButton("Exit");

        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(OnStartGameClicked);
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(OnContinueClicked);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(OnExitClicked);
        }
    }

    public void OnStartGameClicked()
    {
        Scene active = SceneManager.GetActiveScene();
        int nextIndex = active.buildIndex + 1;
        if (nextIndex < 0 || nextIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogWarning("[StartMenuController] Start Game failed: next scene index is out of Build Settings range.");
            return;
        }

        GameManager.LoadSceneByIndex(nextIndex);
    }

    public void OnContinueClicked()
    {
        if (!HasValidContinueData())
        {
            // No-op by requirement.
            return;
        }

        levelManager.LoadGame();
    }

    public void OnExitClicked()
    {
        levelManager.QuitGame();
    }

    private bool HasValidContinueData()
    {
        if (levelManager == null || !levelManager.HasSaveData())
        {
            return false;
        }

        string savedName = levelManager.GetSavedSceneName();
        int savedIndex = levelManager.GetSavedSceneIndex();

        if (!string.IsNullOrWhiteSpace(savedName)
            && savedName.Equals(continueBlockedSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(savedName) && Application.CanStreamedLevelBeLoaded(savedName))
        {
            return true;
        }

        if (savedIndex >= 0 && savedIndex < SceneManager.sceneCountInBuildSettings)
        {
            Scene active = SceneManager.GetActiveScene();
            if (savedIndex != active.buildIndex)
            {
                return true;
            }
        }

        return false;
    }

    private static Button FindButton(string labelText)
    {
        Text[] labels = FindObjectsOfType<Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            Text label = labels[i];
            if (label == null || label.text != labelText)
            {
                continue;
            }

            Button btn = label.GetComponentInParent<Button>();
            if (btn != null)
            {
                return btn;
            }
        }

        return null;
    }
}
