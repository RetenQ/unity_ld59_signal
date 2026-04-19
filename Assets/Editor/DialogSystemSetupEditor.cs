using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class DialogSystemSetupEditor
{
    private const string PrefebFolder = "Assets/Scenes/Prefeb";
    private const string PrefabPath = "Assets/Scenes/Prefeb/DialogSystem.prefab";
    private const string ScenePath = "Assets/Scenes/lev0.unity";
    private const bool RewrtiePrefebflag = false;

    [InitializeOnLoadMethod]
    private static void AutoSetupOnEditorLoad()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null
            && AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            return;
        }

        SetupLev0DialogUI();
    }

    [MenuItem("Tools/DialogSystem/Setup Lev0 Dialog UI")]
    public static void SetupLev0DialogUI()
    {
        EnsureFolders();
        CreateOrUpdatePrefab();
        CreateOrUpdateLev0Scene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[DialogSystemSetupEditor] Setup done: " + PrefabPath + " + " + ScenePath);
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
        {
            AssetDatabase.CreateFolder("Assets", "Scenes");
        }

        if (!AssetDatabase.IsValidFolder(PrefebFolder))
        {
            AssetDatabase.CreateFolder("Assets/Scenes", "Prefeb");
        }
    }

    private static void CreateOrUpdatePrefab()
    {
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
        if (exists && !RewrtiePrefebflag)
        {
            Debug.LogWarning("[DialogSystemSetupEditor] Skip prefab rewrite because RewrtiePrefebflag=false: " + PrefabPath);
            return;
        }

        var root = new GameObject("DialogSystemRoot");
        var dialog = root.AddComponent<dialogSystem>();

        var canvasGo = new GameObject("DialogCanvas");
        canvasGo.transform.SetParent(root.transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        var panelGo = new GameObject("DialogPanel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRect = panelGo.AddComponent<RectTransform>();
        var panelImage = panelGo.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.75f);
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.sizeDelta = new Vector2(320f, 320f);
        panelRect.anchoredPosition = new Vector2(0f, -20f);

        var textGo = new GameObject("DialogText");
        textGo.transform.SetParent(panelGo.transform, false);
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(16f, 16f);
        textRect.offsetMax = new Vector2(-16f, -16f);

        var text = textGo.AddComponent<Text>();
        text.text = "type IT";
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 28;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        var so = new SerializedObject(dialog);
        so.FindProperty("dialogRoot").objectReferenceValue = panelGo;
        so.FindProperty("dialogText").objectReferenceValue = text;
        so.FindProperty("wordInterval").floatValue = 0.2f;
        so.FindProperty("enableDebugKeys").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
    }

    private static void CreateOrUpdateLev0Scene()
    {
        bool exists = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;
        if (exists && !RewrtiePrefebflag)
        {
            Debug.LogWarning("[DialogSystemSetupEditor] Skip scene rewrite because RewrtiePrefebflag=false: " + ScenePath);
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null)
        {
            PrefabUtility.InstantiatePrefab(prefab);
        }

        EditorSceneManager.SaveScene(scene, ScenePath, true);
    }
}
