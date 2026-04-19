using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;

public class LevelSetter : MonoBehaviour
{
    private static string lastLoadedSceneName = string.Empty;
    private static int consecutiveReloadCountForLastScene;

    [System.Serializable]
    private class IconPrefabEntry
    {
        public string letter = "A";
        public Sprite iconSprite;
        public GameObject prefab;
        public string audioFxName = string.Empty;
    }

    [Header("Level Config")]
    [SerializeField] private bool applyOnlyInSpecificScene = false;
    [SerializeField] private string specificSceneName = "Test";
    [SerializeField] private int gridNum = 10;
    [SerializeField] private string fixedScenePattern = "ABC";
    [SerializeField] private bool startWithIsmark = true;
    [SerializeField] private bool autoCreateActionSystem = true;
    [SerializeField] private float previewStartDelay = 0f;
    [SerializeField] private float previewAudioInterval = 0.2f;
    [SerializeField] private float waitEND = 1.5f;
    [SerializeField] private List<IconPrefabEntry> iconPrefabs = new List<IconPrefabEntry>();

    [Header("Opening Dialog")]
    [SerializeField] private bool playOpeningDialog = true;
    [TextArea(2, 4)]
    [SerializeField] private string openingDialogText = "Obey the SIGNAL!";
    [SerializeField] private float openingDialogWordInterval = 0.2f;
    [SerializeField] private float openingDialogEndDelay = 0.2f;

    [Header("Retry Opening Dialog")]
    [SerializeField] private int retryDialogTriggerReloadCount = -1;
    [TextArea(2, 4)]
    [SerializeField] private string retryOpeningDialogText = "Try another way.";

    // Runtime dictionary maintained from inspector list: letter -> icon source.
    private readonly Dictionary<char, Sprite> iconSpriteByLetter = new Dictionary<char, Sprite>();
    private readonly Dictionary<char, string> audioFxByLetter = new Dictionary<char, string>();
    private int currentSceneReloadCount;

    private void Awake()
    {
        if (applyOnlyInSpecificScene
            && !SceneManager.GetActiveScene().name.Equals(specificSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        currentSceneReloadCount = RegisterCurrentSceneReloadCount();
        StartCoroutine(EnterSceneSequence());
    }

    public void ApplyLevelConfig()
    {
        ActionMatchUIManager manager = ResolveActionMatchManager();
        if (manager == null)
        {
            return;
        }

        ApplyConfigToManager(manager, true);
    }

    private ActionMatchUIManager ResolveActionMatchManager()
    {
        ActionMatchUIManager manager = FindObjectOfType<ActionMatchUIManager>();
        if (manager == null && autoCreateActionSystem)
        {
            var go = new GameObject("ActionMatchSystem");
            manager = go.AddComponent<ActionMatchUIManager>();
        }

        if (manager == null)
        {
            Debug.LogError("[LevelSetter] ActionMatchUIManager not found.");
            return null;
        }

        return manager;
    }

    private void ApplyConfigToManager(ActionMatchUIManager manager, bool startPreview)
    {
        manager.SetSlotCount(gridNum);
        manager.SetScenePattern(fixedScenePattern);
        manager.SetIsmark(startWithIsmark);
        manager.SetIconSprites(BuildIconSpriteMap());
        manager.SetAudioFxMap(BuildAudioFxMap());
        manager.SetNoMatchWaitEnd(waitEND);
        manager.ClearPlayerRecord();
        if (startPreview)
        {
            manager.PlaySceneRowPreviewAudio(previewAudioInterval, previewStartDelay);
        }
    }

    private IEnumerator EnterSceneSequence()
    {
        // Give auto-mounted singleton systems one frame to be ready.
        yield return null;

        ActionMatchUIManager manager = ResolveActionMatchManager();
        if (manager == null)
        {
            yield break;
        }

        // Apply icon/audio/grid mapping before opening dialog so UI is correct from the first frame.
        ApplyConfigToManager(manager, false);

        float previousTimeScale = Time.timeScale;
        bool frozeForOpeningDialog = false;
        bool skipToGameplayRequested = false;

        bool retryDialogDisabled = retryDialogTriggerReloadCount < 0;
        bool useRetryDialog = !retryDialogDisabled
            && currentSceneReloadCount > retryDialogTriggerReloadCount
            && !string.IsNullOrWhiteSpace(retryOpeningDialogText);
        bool shouldPlayOpeningDialog = playOpeningDialog || useRetryDialog;
        string effectiveDialogText = useRetryDialog ? retryOpeningDialogText : openingDialogText;

        if (!retryDialogDisabled
            && currentSceneReloadCount > retryDialogTriggerReloadCount
            && string.IsNullOrWhiteSpace(retryOpeningDialogText))
        {
            Debug.LogWarning("[LevelSetter] Retry dialog triggered but retryOpeningDialogText is empty.");
        }

        if (shouldPlayOpeningDialog && !string.IsNullOrWhiteSpace(effectiveDialogText))
        {
            Time.timeScale = 0f;
            frozeForOpeningDialog = true;
            dialogSystem.SetWordInterval(openingDialogWordInterval);
            dialogSystem.Open(effectiveDialogText);

            float waitSeconds = EstimateDialogDuration(effectiveDialogText, openingDialogWordInterval, openingDialogEndDelay);
            float elapsed = 0f;
            while (elapsed < waitSeconds)
            {
                if (Input.GetKeyDown(KeyCode.E))
                {
                    skipToGameplayRequested = true;
                    break;
                }

                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            dialogSystem.Close();
        }

        if (frozeForOpeningDialog)
        {
            Time.timeScale = previousTimeScale;
        }

        if (skipToGameplayRequested)
        {
            yield break;
        }

        manager.PlaySceneRowPreviewAudio(previewAudioInterval, previewStartDelay);
    }

    private static int RegisterCurrentSceneReloadCount()
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(lastLoadedSceneName)
            && currentSceneName.Equals(lastLoadedSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            consecutiveReloadCountForLastScene++;
        }
        else
        {
            consecutiveReloadCountForLastScene = 0;
        }

        lastLoadedSceneName = currentSceneName;
        Debug.Log("[LevelSetter] Reload count for scene '" + currentSceneName + "': " + consecutiveReloadCountForLastScene);
        return consecutiveReloadCountForLastScene;
    }

    private static float EstimateDialogDuration(string text, float perWordSeconds, float endDelay)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Mathf.Max(0f, endDelay);
        }

        string[] words = text.Split(' ');
        int wordCount = 0;
        for (int i = 0; i < words.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(words[i]))
            {
                wordCount++;
            }
        }

        float typingSeconds = Mathf.Max(0f, perWordSeconds) * wordCount;
        return typingSeconds + Mathf.Max(0f, endDelay);
    }

    private Dictionary<char, Sprite> BuildIconSpriteMap()
    {
        iconSpriteByLetter.Clear();
        var spriteMap = new Dictionary<char, Sprite>();

        for (int i = 0; i < iconPrefabs.Count; i++)
        {
            IconPrefabEntry entry = iconPrefabs[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.letter))
            {
                continue;
            }

            char key = char.ToUpperInvariant(entry.letter[0]);
            Sprite resolved = entry.iconSprite;
            if (resolved == null && entry.prefab != null)
            {
                SpriteRenderer renderer = entry.prefab.GetComponentInChildren<SpriteRenderer>(true);
                if (renderer != null)
                {
                    resolved = renderer.sprite;
                }
            }

            if (resolved != null)
            {
                iconSpriteByLetter[key] = resolved;
                spriteMap[key] = resolved;
            }
        }

        return spriteMap;
    }

    private Dictionary<char, string> BuildAudioFxMap()
    {
        audioFxByLetter.Clear();

        for (int i = 0; i < iconPrefabs.Count; i++)
        {
            IconPrefabEntry entry = iconPrefabs[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.letter) || string.IsNullOrWhiteSpace(entry.audioFxName))
            {
                continue;
            }

            char key = char.ToUpperInvariant(entry.letter[0]);
            audioFxByLetter[key] = entry.audioFxName.Trim();
        }

        return new Dictionary<char, string>(audioFxByLetter);
    }
}
