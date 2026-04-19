using UnityEditor;
using UnityEngine;

public static class EndFlagPrefabGenerator
{
    private const string PrefabFolder = "Assets/Scenes/Prefeb";
    private const string PrefabPath = "Assets/Scenes/Prefeb/endFlag.prefab";
    private const string SpritePath = "Assets/art/endFlag.png";
    private const bool RewrtiePrefebflag = false;

    [InitializeOnLoadMethod]
    private static void EnsureEndFlagPrefabOnLoad()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            return;
        }

        CreateOrUpdatePrefab();
    }

    [MenuItem("Tools/ActionMatch/Create EndFlag Prefab")]
    public static void CreateOrUpdatePrefab()
    {
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
        if (exists && !RewrtiePrefebflag)
        {
            Debug.LogWarning("[EndFlagPrefabGenerator] Skip rewrite because RewrtiePrefebflag=false: " + PrefabPath);
            return;
        }

        EnsureFolder();

        var root = new GameObject("endFlag");
        var sr = root.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        sr.sortingOrder = 100;

        var col = root.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1f, 2f);

        root.AddComponent<EndFlagTrigger>();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
        {
            AssetDatabase.CreateFolder("Assets", "Scenes");
        }

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
        {
            AssetDatabase.CreateFolder("Assets/Scenes", "Prefeb");
        }
    }
}
