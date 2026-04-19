using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class StartSceneSetupEditor
{
    private const string ScenePath = "Assets/Scenes/Start.unity";
    private const bool RewrtiePrefebflag = false;
    private static bool ensureScheduled;

    [InitializeOnLoadMethod]
    private static void EnsureStartScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            return;
        }

        ScheduleEnsureStartScene();
    }

    private static void ScheduleEnsureStartScene()
    {
        if (ensureScheduled)
        {
            return;
        }

        ensureScheduled = true;
        EditorApplication.delayCall += TryEnsureStartSceneWhenEditorReady;
    }

    private static void TryEnsureStartSceneWhenEditorReady()
    {
        ensureScheduled = false;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            return;
        }

        // During domain reload/compilation/updating, scene creation APIs are not available yet.
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            ScheduleEnsureStartScene();
            return;
        }

        CreateOrUpdateStartScene();
    }

    [MenuItem("Tools/ActionMatch/Create Start Scene")]
    public static void CreateOrUpdateStartScene()
    {
        bool exists = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;
        if (exists && !RewrtiePrefebflag)
        {
            Debug.LogWarning("[StartSceneSetupEditor] Skip rewrite because RewrtiePrefebflag=false: " + ScenePath);
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.16f, 0.30f, 0.53f, 1f);
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0f, 0f, -10f);

        var eventSystemGo = new GameObject("EventSystem");
        eventSystemGo.AddComponent<EventSystem>();
        eventSystemGo.AddComponent<StandaloneInputModule>();

        var rootGo = new GameObject("StartMenuRoot");
        rootGo.AddComponent<StartMenuController>();

        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(rootGo.transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        var title = CreateText("Title", canvasGo.transform, "Signal", 86, TextAnchor.MiddleCenter);
        var titleRt = title.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 0.86f);
        titleRt.anchorMax = new Vector2(0.5f, 0.86f);
        titleRt.sizeDelta = new Vector2(900f, 120f);
        titleRt.anchoredPosition = Vector2.zero;

        var panelGo = new GameObject("MenuPanel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRt = panelGo.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.34f);
        panelRt.anchorMax = new Vector2(0.5f, 0.34f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(780f, 420f);
        var panelImg = panelGo.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.92f);

        CreateMenuButton(panelGo.transform, "Start Game", 78f);
        CreateMenuButton(panelGo.transform, "Continue Game", 8f);
        CreateMenuButton(panelGo.transform, "Exit", -132f);

        EnsureScenesFolder();
        EditorSceneManager.SaveScene(scene, ScenePath, true);
    }

    private static Text CreateText(string name, Transform parent, string textValue, int size, TextAnchor anchor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(760f, 72f);
        var text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.text = textValue;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = Color.white;
        return text;
    }

    private static void CreateMenuButton(Transform parent, string label, float y)
    {
        var go = new GameObject(label + "Button");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(360f, 70f);
        rt.anchoredPosition = new Vector2(0f, y);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.86f, 0.86f, 0.86f, 0.9f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var labelText = CreateText(label + "Label", go.transform, label, 48, TextAnchor.MiddleCenter);
        labelText.rectTransform.anchorMin = Vector2.zero;
        labelText.rectTransform.anchorMax = Vector2.one;
        labelText.rectTransform.offsetMin = Vector2.zero;
        labelText.rectTransform.offsetMax = Vector2.zero;
    }

    private static void EnsureScenesFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
        {
            AssetDatabase.CreateFolder("Assets", "Scenes");
        }
    }
}
