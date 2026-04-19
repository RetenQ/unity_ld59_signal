using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class ActionMatchUIPrefabGenerator
{
    private const string PrefabPath = "Assets/Resources/ActionMatchUI.prefab";
    private const bool RewrtiePrefebflag = false;

    [InitializeOnLoadMethod]
    private static void EnsurePrefabExistsOnLoad()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            return;
        }

        EnsureResourcesFolder();
        CreatePrefab();
    }

    [MenuItem("Tools/ActionMatch/Create UI Prefab")]
    public static void CreatePrefab()
    {
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
        if (exists && !RewrtiePrefebflag)
        {
            Debug.LogWarning("[ActionMatchUIPrefabGenerator] Skip rewrite because RewrtiePrefebflag=false: " + PrefabPath);
            return;
        }

        var root = new GameObject("ActionMatchUI");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();

        var refs = root.AddComponent<ActionMatchUIRefs>();

        refs.row1Root = CreateRect("Row1Root", root.transform);
        refs.row2Root = CreateRect("Row2Root", root.transform);
        refs.row1Title = CreateText("Row1Title", root.transform, 14, TextAnchor.MiddleLeft, Color.black);
        refs.row2Title = CreateText("Row2Title", root.transform, 14, TextAnchor.MiddleLeft, Color.black);
        refs.skipHint = CreateText("SkipHint", root.transform, 16, TextAnchor.MiddleCenter, Color.black);
        refs.flashOverlay = CreateFlashOverlay(root.transform);
        refs.slotTemplate = CreateSlotTemplate(root.transform);

        LayoutStaticLabels(refs);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void EnsureResourcesFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    private static Text CreateText(string name, Transform parent, int size, TextAnchor anchor, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = size;
        text.alignment = anchor;
        text.color = color;
        return text;
    }

    private static Image CreateFlashOverlay(Transform parent)
    {
        var go = new GameObject("FlashOverlay");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var image = go.AddComponent<Image>();
        image.color = new Color(1f, 0.15f, 0.15f, 0.45f);
        go.SetActive(false);
        return image;
    }

    private static ActionMatchUISlot CreateSlotTemplate(Transform parent)
    {
        var go = new GameObject("SlotTemplate");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(56f, 56f);
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.9f, 0.9f, 0.9f, 1f);

        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(go.transform, false);
        var iconRt = iconGo.AddComponent<RectTransform>();
        iconRt.anchorMin = Vector2.zero;
        iconRt.anchorMax = Vector2.one;
        iconRt.offsetMin = new Vector2(3f, 3f);
        iconRt.offsetMax = new Vector2(-3f, -3f);
        var icon = iconGo.AddComponent<Image>();
        icon.preserveAspect = true;

        var label = CreateText("Label", go.transform, 18, TextAnchor.MiddleCenter, Color.black);
        var labelRt = label.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        var slot = go.AddComponent<ActionMatchUISlot>();
        var so = new SerializedObject(slot);
        so.FindProperty("background").objectReferenceValue = bg;
        so.FindProperty("icon").objectReferenceValue = icon;
        so.FindProperty("label").objectReferenceValue = label;
        so.ApplyModifiedPropertiesWithoutUndo();

        go.SetActive(false);
        return slot;
    }

    private static void LayoutStaticLabels(ActionMatchUIRefs refs)
    {
        var row1Rt = refs.row1Title.GetComponent<RectTransform>();
        row1Rt.anchorMin = new Vector2(0f, 0f);
        row1Rt.anchorMax = new Vector2(1f, 0f);
        row1Rt.pivot = new Vector2(0f, 0f);
        row1Rt.anchoredPosition = new Vector2(12f, 102f);
        row1Rt.sizeDelta = new Vector2(-24f, 20f);

        var row2Rt = refs.row2Title.GetComponent<RectTransform>();
        row2Rt.anchorMin = new Vector2(0f, 0f);
        row2Rt.anchorMax = new Vector2(1f, 0f);
        row2Rt.pivot = new Vector2(0f, 0f);
        row2Rt.anchoredPosition = new Vector2(12f, 52f);
        row2Rt.sizeDelta = new Vector2(-24f, 20f);

        var hintRt = refs.skipHint.GetComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0f, 0f);
        hintRt.anchorMax = new Vector2(1f, 0f);
        hintRt.pivot = new Vector2(0.5f, 0f);
        hintRt.anchoredPosition = new Vector2(0f, 12f);
        hintRt.sizeDelta = new Vector2(0f, 24f);
    }
}
