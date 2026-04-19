using UnityEngine;
using UnityEngine.SceneManagement;

public class ActionMatchTestBootstrap : MonoBehaviour
{
    private const string TargetSceneName = "Test";
    private const string LevelSetterName = "levelSetter";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateInTestScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.name.Equals(TargetSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        GameObject levelSetterGo = GameObject.Find(LevelSetterName);
        if (levelSetterGo == null)
        {
            levelSetterGo = new GameObject(LevelSetterName);
        }

        LevelSetter setter = levelSetterGo.GetComponent<LevelSetter>();
        if (setter == null)
        {
            setter = levelSetterGo.AddComponent<LevelSetter>();
        }
    }
}
