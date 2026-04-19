using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    private const string SaveKeySceneIndex = "save_last_scene_index";
    private const string SaveKeySceneName = "save_last_scene_name";
    private const string StartSceneName = "Start";

    public static LevelManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void Start()
    {
        // Ensure there is always a valid save payload after entering play mode.
        SaveGame();
    }

    private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        // Auto-save whenever scene switches, including switches triggered by other systems.
        if (IsSavableScene(newScene))
        {
            SaveSceneData(newScene.buildIndex, newScene.name);
        }
    }

    // 1) Quit game API.
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // 2) Enter next scene API.
    // If targetSceneName is provided, load it by name.
    // Otherwise load current buildIndex + 1.
    public void EnterNextScene(string targetSceneName = null)
    {
        SaveGame();

        if (!string.IsNullOrWhiteSpace(targetSceneName))
        {
            GameManager.LoadSceneByName(targetSceneName);
            return;
        }

        int currentIndex = SceneManager.GetActiveScene().buildIndex;
        int nextIndex = currentIndex + 1;
        if (nextIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogWarning("[LevelManager] Next scene index is out of range: " + nextIndex);
            return;
        }

        GameManager.LoadSceneByIndex(nextIndex);
    }

    // 3) Manual save API.
    public void SaveGame()
    {
        Scene current = SceneManager.GetActiveScene();
        if (IsSavableScene(current))
        {
            SaveSceneData(current.buildIndex, current.name);
        }
    }

    // 3) Manual load API.
    // Loads scene by saved name first; if invalid, tries saved index.
    public void LoadGame()
    {
        if (!HasSaveData())
        {
            Debug.LogWarning("[LevelManager] No save data found.");
            return;
        }

        string savedName = PlayerPrefs.GetString(SaveKeySceneName, string.Empty);
        int savedIndex = PlayerPrefs.GetInt(SaveKeySceneIndex, -1);

        if (!string.IsNullOrWhiteSpace(savedName) && Application.CanStreamedLevelBeLoaded(savedName))
        {
            GameManager.LoadSceneByName(savedName);
            return;
        }

        if (savedIndex >= 0 && savedIndex < SceneManager.sceneCountInBuildSettings)
        {
            GameManager.LoadSceneByIndex(savedIndex);
            return;
        }

        Debug.LogWarning("[LevelManager] Save data exists but target scene is invalid.");
    }

    public bool HasSaveData()
    {
        return PlayerPrefs.HasKey(SaveKeySceneIndex) || PlayerPrefs.HasKey(SaveKeySceneName);
    }

    public int GetSavedSceneIndex()
    {
        return PlayerPrefs.GetInt(SaveKeySceneIndex, -1);
    }

    public string GetSavedSceneName()
    {
        return PlayerPrefs.GetString(SaveKeySceneName, string.Empty);
    }

    private void SaveSceneData(int sceneIndex, string sceneName)
    {
        PlayerPrefs.SetInt(SaveKeySceneIndex, sceneIndex);
        PlayerPrefs.SetString(SaveKeySceneName, sceneName ?? string.Empty);
        PlayerPrefs.Save();
    }

    private static bool IsSavableScene(Scene scene)
    {
        if (!scene.IsValid())
        {
            return false;
        }

        return !scene.name.Equals(StartSceneName, System.StringComparison.OrdinalIgnoreCase);
    }
}
