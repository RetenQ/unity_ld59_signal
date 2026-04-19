using UnityEngine;
using UnityEngine.SceneManagement;

public class EndSceneQuitOnQ : MonoBehaviour
{
    private const string EndSceneName = "END";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRunner()
    {
        if (FindObjectOfType<EndSceneQuitOnQ>() != null)
        {
            return;
        }

        var go = new GameObject("EndSceneQuitOnQ");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;
        go.AddComponent<EndSceneQuitOnQ>();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Q))
        {
            return;
        }

        if (!SceneManager.GetActiveScene().name.Equals(EndSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.QuitGame();
            return;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
