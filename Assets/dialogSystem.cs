using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class dialogSystem : MonoBehaviour
{
    public static dialogSystem Instance { get; private set; }
    private const string RuntimeDialogPrefabResourcePath = "DialogSystem";

    [Header("UI Binding")]
    [SerializeField] private GameObject dialogRoot;
    [SerializeField] private Text dialogText;
    [SerializeField] private GameObject dialogPrefab;

    [Header("Typewriter")]
    [SerializeField] private float wordInterval = 0.2f;
    [SerializeField] private float typingSfxVolume = 0.9f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugKeys = true;
    [SerializeField] private bool allowRuntimeFallbackUI = true;

    private Coroutine typingCoroutine;
    private string currentFullText = "type IT";
    private bool hasLoggedBindingError;
    private Text activeTypingTarget;
    private bool skipTypewriterRequested;
    private AudioSource typingAudioSource;
    private AudioClip[] dialogTypingClips;
    private bool dialogTypingLoaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureMountedOnSystemManager()
    {
        if (Instance != null)
        {
            return;
        }

        dialogSystem existing = FindObjectOfType<dialogSystem>();
        if (existing != null)
        {
            return;
        }

        GameObject manager = FindSystemManagerInActiveScene();
        if (manager == null)
        {
            manager = new GameObject("SystemManager");
            Debug.LogWarning("[dialogSystem] SystemManager not found in scene. Created a fallback 'SystemManager' object.");
        }

        if (manager.GetComponent<dialogSystem>() == null)
        {
            manager.AddComponent<dialogSystem>();
            Debug.Log("[dialogSystem] Mounted dialogSystem on SystemManager automatically.");
        }
    }

    private static GameObject FindSystemManagerInActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindChildByNameRecursive(roots[i].transform, "SytemManager");
            if (found != null)
            {
                return found.gameObject;
            }

            found = FindChildByNameRecursive(roots[i].transform, "SystemManager");
            if (found != null)
            {
                return found.gameObject;
            }
        }

        return null;
    }

    public static void Open(string text = null)
    {
        if (Instance == null)
        {
            Debug.LogError("[dialogSystem] Open failed: Instance is null.");
            return;
        }

        Instance.OpenDialog(text);
    }

    public static void Close()
    {
        if (Instance == null)
        {
            Debug.LogError("[dialogSystem] Close failed: Instance is null.");
            return;
        }

        Instance.CloseDialog();
    }

    public static void SetText(string text)
    {
        if (Instance == null)
        {
            Debug.LogError("[dialogSystem] SetText failed: Instance is null.");
            return;
        }

        Instance.UpdateDialogText(text);
    }

    public static void SetWordInterval(float seconds)
    {
        if (Instance == null)
        {
            Debug.LogError("[dialogSystem] SetWordInterval failed: Instance is null.");
            return;
        }

        Instance.SetWordIntervalInternal(seconds);
    }

    public static float EstimateTypewriterDuration(string text)
    {
        if (Instance == null)
        {
            return 0f;
        }

        return Instance.EstimateTypewriterDurationInternal(text);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureTypingAudioReady();
        TryAssignPrefabInEditor();
        EnsureRuntimeDialogPrefabSource();
        CachePrefabReferenceFromAssetBinding();
        EnsureBindings();
        EnsureDialogTransformSanity();

        bool recovered = false;
        if (!HasValidBinding())
        {
            if (TrySetupFromPrefab())
            {
                EnsureBindings();
                if (HasValidBinding())
                {
                    recovered = true;
                    Debug.Log("[dialogSystem] UI created from prefab: Assets/Scenes/Prefeb/DialogSystem.prefab");
                }
            }

            if (!HasValidBinding() && allowRuntimeFallbackUI)
            {
                BuildDefaultUI();
                EnsureBindings();
                if (HasValidBinding())
                {
                    recovered = true;
                    Debug.LogWarning("[dialogSystem] UI binding missing, runtime fallback UI was created.");
                }
            }
        }

        if (!HasValidBinding())
        {
            LogBindingError("Awake");
        }
        else if (recovered)
        {
            hasLoggedBindingError = false;
        }

        ApplyHiddenState();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            SkipTypewriterIfRunning();
        }

        if (!enableDebugKeys)
        {
            return;
        }

        if (IsOpenDebugPressed())
        {
            OpenDialog("Test It");
        }

        if (IsCloseDebugPressed())
        {
            CloseDialog();
        }

        if (IsUpdateDebugPressed())
        {
            UpdateDialogText("Hello , this is the test !");
        }
    }

    private static bool IsOpenDebugPressed()
    {
        if (Input.GetKeyDown(KeyCode.O))
        {
            return true;
        }

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.oKey.wasPressedThisFrame)
        {
            return true;
        }
#endif
        return false;
    }

    private static bool IsCloseDebugPressed()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            return true;
        }

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
        {
            return true;
        }
#endif
        return false;
    }

    private static bool IsUpdateDebugPressed()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            return true;
        }

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
        {
            return true;
        }
#endif
        return false;
    }

    public void OpenDialog(string text = null)
    {
        EnsureRuntimeDialogPrefabSource();
        EnsureBindings();
        if (!HasValidBinding())
        {
            if (!TrySetupFromPrefab() && allowRuntimeFallbackUI)
            {
                BuildDefaultUI();
            }

            EnsureBindings();
            EnsureDialogTransformSanity();
            if (!HasValidBinding())
            {
                LogBindingError("OpenDialog");
                return;
            }
        }

        if (!string.IsNullOrEmpty(text))
        {
            currentFullText = text;
        }

        dialogRoot.SetActive(true);
        RestartTypewriter(currentFullText);
    }

    public void CloseDialog()
    {
        StopTypingSafely();

        if (dialogRoot != null)
        {
            dialogRoot.SetActive(false);
        }
    }

    public void UpdateDialogText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        currentFullText = text;
        if (dialogRoot != null && dialogRoot.activeSelf)
        {
            RestartTypewriter(currentFullText);
        }
    }

    private void SetWordIntervalInternal(float seconds)
    {
        wordInterval = Mathf.Max(0f, seconds);
    }

    private float EstimateTypewriterDurationInternal(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0f;
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

        return Mathf.Max(0f, wordInterval) * wordCount;
    }

    private void RestartTypewriter(string text)
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        if (dialogText == null)
        {
            LogBindingError("RestartTypewriter");
            typingCoroutine = null;
            return;
        }

        activeTypingTarget = dialogText;
        typingCoroutine = StartCoroutine(TypeWords(text));
    }

    private IEnumerator TypeWords(string text)
    {
        skipTypewriterRequested = false;
        Text target = activeTypingTarget != null ? activeTypingTarget : dialogText;
        if (target == null)
        {
            typingCoroutine = null;
            yield break;
        }

        target.text = string.Empty;
        string[] words = text.Split(' ');
        var builder = new StringBuilder();

        for (int i = 0; i < words.Length; i++)
        {
            if (target == null || target != activeTypingTarget)
            {
                typingCoroutine = null;
                yield break;
            }

            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append(words[i]);
            target.text = builder.ToString();

            if (skipTypewriterRequested)
            {
                target.text = text;
                typingCoroutine = null;
                skipTypewriterRequested = false;
                yield break;
            }

            PlayRandomDialogTypingSfx();
            yield return new WaitForSecondsRealtime(wordInterval);
        }

        if (target != null)
        {
            target.text = builder.ToString();
        }

        typingCoroutine = null;
    }

    private void SkipTypewriterIfRunning()
    {
        if (typingCoroutine == null)
        {
            return;
        }

        skipTypewriterRequested = true;
        if (dialogText != null)
        {
            dialogText.text = currentFullText;
        }
    }

    private void EnsureBindings()
    {
        // If inspector mistakenly references a prefab asset object, clear it and bind scene instance.
        if (dialogRoot != null && !dialogRoot.scene.IsValid())
        {
            dialogRoot = null;
        }

        if (dialogRoot == null)
        {
            Transform root = FindChildRecursive(transform, "DialogPanel");
            if (root != null)
            {
                dialogRoot = root.gameObject;
            }
        }

        if (dialogText == null && dialogRoot != null)
        {
            dialogText = dialogRoot.GetComponentInChildren<Text>(true);
        }
    }

    private void CachePrefabReferenceFromAssetBinding()
    {
        if (dialogPrefab == null && dialogRoot != null && !dialogRoot.scene.IsValid())
        {
            dialogPrefab = dialogRoot.transform.root.gameObject;
        }
    }

    private bool TrySetupFromPrefab()
    {
        EnsureRuntimeDialogPrefabSource();
        if (dialogPrefab == null && dialogRoot != null && !dialogRoot.scene.IsValid())
        {
            dialogPrefab = dialogRoot.transform.root.gameObject;
        }

        if (dialogPrefab == null)
        {
            return false;
        }

        Transform existingPanel = FindChildRecursive(transform, "DialogPanel");
        if (existingPanel == null)
        {
            GameObject spawnSource = dialogPrefab;
            dialogSystem prefabDialogSystem = dialogPrefab.GetComponent<dialogSystem>();
            if (prefabDialogSystem != null)
            {
                Transform canvasInPrefab = dialogPrefab.transform.Find("DialogCanvas");
                if (canvasInPrefab != null)
                {
                    spawnSource = canvasInPrefab.gameObject;
                }
                else
                {
                    Debug.LogWarning(
                        "[dialogSystem] dialogPrefab contains dialogSystem component and has no DialogCanvas child. "
                        + "Falling back to instantiate whole prefab.",
                        this);
                }
            }

            Instantiate(spawnSource, transform);
        }

        EnsureBindings();
        EnsureDialogTransformSanity();
        return HasValidBinding();
    }

    private void EnsureRuntimeDialogPrefabSource()
    {
        if (dialogPrefab != null)
        {
            return;
        }

        GameObject loaded = Resources.Load<GameObject>(RuntimeDialogPrefabResourcePath);
        if (loaded != null)
        {
            dialogPrefab = loaded;
        }
    }

    private void EnsureDialogTransformSanity()
    {
        if (dialogRoot == null)
        {
            return;
        }

        Transform current = dialogRoot.transform;
        while (current != null)
        {
            if (current == transform.parent)
            {
                break;
            }

            if (current.localScale == Vector3.zero)
            {
                current.localScale = Vector3.one;
            }

            if (current == transform)
            {
                break;
            }

            current = current.parent;
        }
    }

    private static Transform FindChildRecursive(Transform parent, string targetName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == targetName)
            {
                return child;
            }

            Transform found = FindChildRecursive(child, targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Transform FindChildByNameRecursive(Transform parent, string targetName)
    {
        if (parent.name == targetName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildByNameRecursive(parent.GetChild(i), targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private bool HasValidBinding()
    {
        return dialogRoot != null && dialogText != null;
    }

    private void LogBindingError(string context)
    {
        if (hasLoggedBindingError)
        {
            return;
        }

        hasLoggedBindingError = true;
        Debug.LogError(
            "[dialogSystem] " + context + " failed: missing UI binding. "
            + "Please bind dialogRoot and dialogText from prefab.",
            this);
    }

    private void BuildDefaultUI()
    {
        StopTypingSafely();

        GameObject canvasGo = new GameObject("DialogCanvas");
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject panelGo = new GameObject("DialogPanel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        RectTransform panelRect = panelGo.AddComponent<RectTransform>();
        Image panelImage = panelGo.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.75f);
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.sizeDelta = new Vector2(1200f, 260f);
        panelRect.anchoredPosition = new Vector2(0f, -28f);

        GameObject textGo = new GameObject("DialogText");
        textGo.transform.SetParent(panelGo.transform, false);
        RectTransform textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(48f, 32f);
        textRect.offsetMax = new Vector2(-48f, -32f);

        Text text = textGo.AddComponent<Text>();
        text.text = "type IT";
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 44;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;

        dialogRoot = panelGo;
        dialogText = text;
    }

    private void StopTypingSafely()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        activeTypingTarget = null;
    }

    private void EnsureTypingAudioReady()
    {
        if (typingAudioSource == null)
        {
            typingAudioSource = GetComponent<AudioSource>();
            if (typingAudioSource == null)
            {
                typingAudioSource = gameObject.AddComponent<AudioSource>();
            }

            typingAudioSource.playOnAwake = false;
            typingAudioSource.loop = false;
            typingAudioSource.spatialBlend = 0f;
        }

        if (!dialogTypingLoaded)
        {
            dialogTypingClips = Resources.LoadAll<AudioClip>("musicFx/dialogTyping");
            dialogTypingLoaded = true;
        }
    }

    private void PlayRandomDialogTypingSfx()
    {
        EnsureTypingAudioReady();
        if (typingAudioSource == null || dialogTypingClips == null || dialogTypingClips.Length == 0)
        {
            return;
        }

        int idx = Random.Range(0, dialogTypingClips.Length);
        AudioClip clip = dialogTypingClips[idx];
        if (clip != null)
        {
            typingAudioSource.PlayOneShot(clip, Mathf.Clamp01(typingSfxVolume));
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        TryAssignPrefabInEditor();
    }

    private void OnValidate()
    {
        TryAssignPrefabInEditor();
    }

    private void TryAssignPrefabInEditor()
    {
        if (dialogPrefab != null)
        {
            return;
        }

        const string prefabPath = "Assets/Scenes/Prefeb/DialogSystem.prefab";
        dialogPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }
#else
    private void TryAssignPrefabInEditor()
    {
    }
#endif

    private void ApplyHiddenState()
    {
        StopTypingSafely();
        if (dialogRoot != null)
        {
            dialogRoot.SetActive(false);
        }
    }
}
