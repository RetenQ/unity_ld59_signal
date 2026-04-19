using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    private static GameManager instance;

    [Header("Scene Transition")]
    [SerializeField] private float wipeDuration = 0.6f;
    [SerializeField] private float congratulationDuration = 1.0f;
    [SerializeField] private float blackFadeOutDuration = 0.5f;
    [SerializeField] private string congratulationText = "Congratulation!";

    private Canvas overlayCanvas;
    private CanvasGroup overlayCanvasGroup;
    private RectTransform wipeRect;
    private Text centerText;
    private bool isTransitioning;

    public static GameManager Instance
    {
        get
        {
            EnsureInstance();
            return instance;
        }
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        instance = FindObjectOfType<GameManager>();
        if (instance != null)
        {
            return;
        }

        var go = new GameObject("GameManager");
        instance = go.AddComponent<GameManager>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureOverlay();
        SetOverlayVisible(false);
    }

    // Can be called from any runtime script when action sequence mismatches.
    public static void noMatch()
    {
        Debug.Log("NoMatch!");
    }

    public static void ReloadCurrentScene()
    {
        Scene active = SceneManager.GetActiveScene();
        SceneManager.LoadScene(active.name);
    }

    public static void LoadSceneByName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[GameManager] LoadSceneByName failed: empty sceneName.");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    public static void LoadSceneByIndex(int buildIndex)
    {
        if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogWarning("[GameManager] LoadSceneByIndex failed: invalid buildIndex " + buildIndex);
            return;
        }

        SceneManager.LoadScene(buildIndex);
    }

    public static void TransitionToSceneWithCongratulation(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning("[GameManager] TransitionToSceneWithCongratulation failed: scene not loadable.");
            return;
        }

        Instance.StartCongratulationTransitionByName(sceneName);
    }

    public static void TransitionToBuildIndexWithCongratulation(int buildIndex)
    {
        if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogWarning("[GameManager] TransitionToBuildIndexWithCongratulation failed: invalid buildIndex " + buildIndex);
            return;
        }

        Instance.StartCongratulationTransitionByIndex(buildIndex);
    }

    private void StartCongratulationTransitionByName(string sceneName)
    {
        if (isTransitioning)
        {
            return;
        }

        StartCoroutine(CongratulationTransitionCoroutineByName(sceneName));
    }

    private void StartCongratulationTransitionByIndex(int buildIndex)
    {
        if (isTransitioning)
        {
            return;
        }

        StartCoroutine(CongratulationTransitionCoroutineByIndex(buildIndex));
    }

    private IEnumerator CongratulationTransitionCoroutineByName(string sceneName)
    {
        yield return PlayCongratulationPreloadSequence();
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (op != null && !op.isDone)
        {
            yield return null;
        }

        yield return FadeOutBlackOverlay();
        isTransitioning = false;
    }

    private IEnumerator CongratulationTransitionCoroutineByIndex(int buildIndex)
    {
        yield return PlayCongratulationPreloadSequence();
        AsyncOperation op = SceneManager.LoadSceneAsync(buildIndex);
        while (op != null && !op.isDone)
        {
            yield return null;
        }

        yield return FadeOutBlackOverlay();
        isTransitioning = false;
    }

    private IEnumerator PlayCongratulationPreloadSequence()
    {
        isTransitioning = true;
        EnsureOverlay();
        SetOverlayVisible(true);
        SetOverlayAlpha(1f);
        centerText.gameObject.SetActive(false);
        SetWipeProgress(0f);

        float duration = Mathf.Max(0.01f, wipeDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetWipeProgress(Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        SetWipeProgress(1f);
        centerText.text = congratulationText;
        centerText.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, congratulationDuration));
        centerText.gameObject.SetActive(false);
        SetWipeProgress(1f);
    }

    private IEnumerator FadeOutBlackOverlay()
    {
        float duration = Mathf.Max(0.01f, blackFadeOutDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetOverlayAlpha(1f - t);
            yield return null;
        }

        SetOverlayAlpha(0f);
        SetOverlayVisible(false);
    }

    private void EnsureOverlay()
    {
        if (overlayCanvas != null && wipeRect != null && centerText != null)
        {
            if (overlayCanvasGroup == null)
            {
                overlayCanvasGroup = overlayCanvas.GetComponent<CanvasGroup>();
                if (overlayCanvasGroup == null)
                {
                    overlayCanvasGroup = overlayCanvas.gameObject.AddComponent<CanvasGroup>();
                }
            }
            return;
        }

        var canvasGo = new GameObject("SceneTransitionOverlay");
        canvasGo.transform.SetParent(transform, false);
        overlayCanvas = canvasGo.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 5000;
        overlayCanvasGroup = canvasGo.AddComponent<CanvasGroup>();
        overlayCanvasGroup.alpha = 1f;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        var wipeGo = new GameObject("WipeTopDown");
        wipeGo.transform.SetParent(canvasGo.transform, false);
        wipeRect = wipeGo.AddComponent<RectTransform>();
        wipeRect.anchorMin = new Vector2(0f, 1f);
        wipeRect.anchorMax = new Vector2(1f, 1f);
        wipeRect.pivot = new Vector2(0.5f, 1f);
        wipeRect.sizeDelta = new Vector2(0f, 0f);
        var wipeImg = wipeGo.AddComponent<Image>();
        wipeImg.color = Color.black;

        var textGo = new GameObject("CongratulationText");
        textGo.transform.SetParent(canvasGo.transform, false);
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0.5f, 0.5f);
        textRt.anchorMax = new Vector2(0.5f, 0.5f);
        textRt.pivot = new Vector2(0.5f, 0.5f);
        textRt.sizeDelta = new Vector2(800f, 120f);
        centerText = textGo.AddComponent<Text>();
        centerText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        centerText.fontSize = 48;
        centerText.alignment = TextAnchor.MiddleCenter;
        centerText.color = Color.white;
        centerText.text = congratulationText;
        centerText.gameObject.SetActive(false);
    }

    private void SetOverlayVisible(bool visible)
    {
        if (overlayCanvas != null)
        {
            overlayCanvas.gameObject.SetActive(visible);
        }
    }

    private void SetOverlayAlpha(float alpha)
    {
        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.alpha = Mathf.Clamp01(alpha);
        }
    }

    private void SetWipeProgress(float t)
    {
        if (wipeRect == null)
        {
            return;
        }

        t = Mathf.Clamp01(t);
        wipeRect.anchorMin = new Vector2(0f, 1f - t);
        wipeRect.anchorMax = new Vector2(1f, 1f);
        wipeRect.offsetMin = Vector2.zero;
        wipeRect.offsetMax = Vector2.zero;
    }
}
