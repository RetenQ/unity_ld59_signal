using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum PlayerActionType
{
    Jump,
    Land,
    Dash
}

public class ActionMatchUIManager : MonoBehaviour
{
    private const int MaxAllowedSlots = 30;
    private const int MinAllowedSlots = 1;
    private const float SlotSize = 56f;
    private const float FixedSlotGap = 8f;
    private const float BottomPadding = 20f;
    private const float RowGap = 8f;

    [Header("Scene Pattern (Row 1)")]
    [SerializeField] private string scenePattern = "ABC";

    [Header("Player Record (Row 2)")]
    [SerializeField] private bool ismark = true;
    [SerializeField] private int slotCount = 10;
    [SerializeField] private GameObject uiPrefab;

    [Header("Demo Keys In Test Scene")]
    [SerializeField] private bool enableDemoInput = true;
    [SerializeField] private KeyCode jumpSuccessKey = KeyCode.J;
    [SerializeField] private KeyCode jumpFailKey = KeyCode.K;
    [SerializeField] private KeyCode landSuccessKey = KeyCode.L;
    [SerializeField] private KeyCode dashSuccessKey = KeyCode.Semicolon;
    [SerializeField] private KeyCode toggleIsmarkKey = KeyCode.M;

    private readonly List<char> playerRecord = new List<char>(10);
    private char[] sceneRow = new char[0];
    private readonly Dictionary<char, Sprite> iconSpriteByToken = new Dictionary<char, Sprite>();
    private readonly Dictionary<char, string> audioFxByToken = new Dictionary<char, string>();
    // Legacy OnGUI fallback styles (used when prefab UI is unavailable).
    private GUIStyle centeredLabelStyle;
    private GUIStyle skipHintStyle;
    private GUIStyle borderBoxStyle;
    private readonly List<ActionMatchUISlot> row1Slots = new List<ActionMatchUISlot>();
    private readonly List<ActionMatchUISlot> row2Slots = new List<ActionMatchUISlot>();
    private ActionMatchUIRefs uiRefs;
    private Coroutine sceneRowPreviewCoroutine;
    private Coroutine previewStartCoroutine;
    private Coroutine noMatchSequenceCoroutine;
    private Coroutine deadAreaSequenceCoroutine;
    private int previewHighlightIndex = -1;
    private bool isPreviewPlaying;
    private bool hasFrozenSceneForPreview;
    private float previousTimeScale = 1f;
    private bool isNoMatchSequenceRunning;
    private float noMatchWaitEndSeconds = 1.5f;
    private float noMatchFlashUntilRealtime;
    private const float NoMatchFlashDurationSeconds = 0.18f;
    private bool uiDirty;
    private bool hasInvalidXPlacement;
    private string invalidXPlacementReason = string.Empty;
    private int firstXColumnIndex = -1;
    private static Texture2D fallbackSolidFillTexture;

    private static readonly Color RowBackgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);
    private static readonly Color RowXColor = new Color(1f, 0.82f, 0.82f, 1f);
    private static readonly Color RowHighlightColor = new Color(0.78f, 0.93f, 0.78f, 1f);
    private static readonly Color RowMatchedColor = new Color(0.78f, 0.93f, 0.78f, 1f);
    private static readonly Color NoMatchFlashColor = new Color(1f, 0.15f, 0.15f, 0.45f);

    private void Awake()
    {
        EnsureUiPrefab();
        NormalizeSlotCount();
        BuildSceneRow();
        MarkUiDirty();
    }

    private void OnValidate()
    {
        NormalizeSlotCount();
        BuildSceneRow();
        MarkUiDirty();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            ReloadCurrentSceneNow();
            return;
        }

        if (hasFrozenSceneForPreview && Input.GetKeyDown(KeyCode.E))
        {
            FinishPreviewEarly();
            return;
        }

        if (enableDemoInput)
        {
            if (Input.GetKeyDown(toggleIsmarkKey))
            {
                ismark = !ismark;
            }

            if (Input.GetKeyDown(jumpSuccessKey))
            {
                TryRecordAction(PlayerActionType.Jump, true);
            }

            if (Input.GetKeyDown(jumpFailKey))
            {
                TryRecordAction(PlayerActionType.Jump, false);
            }

            if (Input.GetKeyDown(landSuccessKey))
            {
                TryRecordAction(PlayerActionType.Land, true);
            }

            if (Input.GetKeyDown(dashSuccessKey))
            {
                TryRecordAction(PlayerActionType.Dash, true);
            }
        }

        RefreshUiIfNeeded();
        UpdateFlashOverlay();
    }

    public void SetIsmark(bool value)
    {
        ismark = value;
    }

    public void ClearPlayerRecord()
    {
        playerRecord.Clear();
        MarkUiDirty();
    }

    public void SetSlotCount(int x)
    {
        slotCount = Mathf.Clamp(x, MinAllowedSlots, MaxAllowedSlots);

        // Trim existing record to keep the second row valid under new X.
        if (playerRecord.Count > slotCount)
        {
            playerRecord.RemoveRange(slotCount, playerRecord.Count - slotCount);
        }

        BuildSceneRow();
        MarkUiDirty();
    }

    public void SetScenePattern(string pattern)
    {
        scenePattern = pattern ?? string.Empty;
        BuildSceneRow();
        MarkUiDirty();
    }

    public void SetIconSprites(Dictionary<char, Sprite> iconMap)
    {
        iconSpriteByToken.Clear();
        if (iconMap == null)
        {
            return;
        }

        foreach (KeyValuePair<char, Sprite> kv in iconMap)
        {
            if (kv.Value == null)
            {
                continue;
            }

            iconSpriteByToken[char.ToUpperInvariant(kv.Key)] = kv.Value;
        }

        MarkUiDirty();
    }

    public void SetAudioFxMap(Dictionary<char, string> audioMap)
    {
        audioFxByToken.Clear();
        if (audioMap == null)
        {
            return;
        }

        foreach (KeyValuePair<char, string> kv in audioMap)
        {
            if (string.IsNullOrWhiteSpace(kv.Value))
            {
                continue;
            }

            audioFxByToken[char.ToUpperInvariant(kv.Key)] = kv.Value.Trim();
        }
    }

    public void SetNoMatchWaitEnd(float seconds)
    {
        noMatchWaitEndSeconds = Mathf.Max(0f, seconds);
    }

    public void PlaySceneRowPreviewAudio(float intervalSeconds = 0.2f, float startDelaySeconds = 0f)
    {
        if (sceneRowPreviewCoroutine != null)
        {
            StopCoroutine(sceneRowPreviewCoroutine);
            sceneRowPreviewCoroutine = null;
        }

        if (previewStartCoroutine != null)
        {
            StopCoroutine(previewStartCoroutine);
            previewStartCoroutine = null;
        }

        EndPreviewPhase();
        BeginPreviewPhase();
        previewStartCoroutine = StartCoroutine(BeginPreviewWithDelayCoroutine(
            Mathf.Max(0f, startDelaySeconds),
            Mathf.Max(0f, intervalSeconds)));
    }

    // Call this from any gameplay script when an action may happen.
    // Only successful actions with ismark=true are recorded.
    public void TryRecordAction(PlayerActionType action, bool actionSucceeded)
    {
        if (isNoMatchSequenceRunning)
        {
            return;
        }

        if (!ismark || !actionSucceeded)
        {
            return;
        }

        if (playerRecord.Count >= slotCount)
        {
            return;
        }

        char token = MapActionToToken(action);
        playerRecord.Add(token);
        MarkUiDirty();

        bool mismatch = !IsRecordStillMatchable();
        if (mismatch)
        {
            TriggerNoMatchFlow(BuildInputMismatchReason(token));
        }
    }

    public void RecordJump(bool actionSucceeded)
    {
        TryRecordAction(PlayerActionType.Jump, actionSucceeded);
    }

    public void RecordLand(bool actionSucceeded)
    {
        TryRecordAction(PlayerActionType.Land, actionSucceeded);
    }

    public void RecordDash(bool actionSucceeded)
    {
        TryRecordAction(PlayerActionType.Dash, actionSucceeded);
    }

    public bool AreRowsFullyMatched()
    {
        if (sceneRow == null)
        {
            return false;
        }

        return IsRecordFullyMatched();
    }

    public void TriggerNoMatchFlow()
    {
        TriggerNoMatchFlow(GetFirstMismatchReason());
    }

    public void TriggerNoMatchFlow(string reason)
    {
        if (isNoMatchSequenceRunning || deadAreaSequenceCoroutine != null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(reason))
        {
            Debug.LogWarning("[ActionMatchUIManager] NoMatch原因: " + reason);
        }

        GameManager.noMatch();
        StartNoMatchSequence();
    }

    public string GetFirstMismatchReason()
    {
        if (hasInvalidXPlacement)
        {
            return invalidXPlacementReason;
        }

        if (IsInputAtOrAfterX(playerRecord.Count))
        {
            return string.Format(
                "第{0}格是[X]空动作终止位：在该格及其后出现了玩家输入，判定失败。",
                firstXColumnIndex + 1);
        }

        if (!IsRecordStillMatchable())
        {
            return "当前输入序列在考虑X/F可跳过后仍无法匹配场景信号。";
        }

        return "尚未完成匹配：仍有未完成的非X/F信号。";
    }

    public void TriggerDeadAreaFailFlow(float waitEndSeconds)
    {
        if (isNoMatchSequenceRunning || deadAreaSequenceCoroutine != null)
        {
            return;
        }

        deadAreaSequenceCoroutine = StartCoroutine(DeadAreaFailSequenceCoroutine(Mathf.Max(0f, waitEndSeconds)));
    }

    private void BuildSceneRow()
    {
        hasInvalidXPlacement = false;
        invalidXPlacementReason = string.Empty;
        firstXColumnIndex = -1;

        if (string.IsNullOrEmpty(scenePattern))
        {
            sceneRow = new char[0];
            return;
        }

        string upper = scenePattern.ToUpperInvariant();
        var list = new List<char>(slotCount);
        for (int i = 0; i < upper.Length && list.Count < slotCount; i++)
        {
            char c = upper[i];
            if (c == 'A' || c == 'B' || c == 'C' || c == 'F' || c == 'X')
            {
                list.Add(c);
            }
        }

        sceneRow = list.ToArray();
        ValidateXPlacement();
    }

    private void EnsureUiPrefab()
    {
        if (uiRefs != null)
        {
            return;
        }

        if (uiPrefab == null)
        {
            uiPrefab = Resources.Load<GameObject>("ActionMatchUI");
        }

        if (uiPrefab == null)
        {
            return;
        }

        GameObject instance = Instantiate(uiPrefab);
        uiRefs = instance.GetComponent<ActionMatchUIRefs>();
        if (uiRefs == null)
        {
            Debug.LogWarning("[ActionMatchUIManager] UI prefab missing ActionMatchUIRefs component.");
        }
    }

    private void MarkUiDirty()
    {
        uiDirty = true;
    }

    private void RefreshUiIfNeeded()
    {
        if (uiRefs == null)
        {
            return;
        }

        if (!uiDirty && !isPreviewPlaying)
        {
            return;
        }

        int x = Mathf.Max(MinAllowedSlots, slotCount);
        EnsureSlotPool(row1Slots, uiRefs.row1Root, x);
        EnsureSlotPool(row2Slots, uiRefs.row2Root, x);

        if (uiRefs.row1Title != null)
        {
            uiRefs.row1Title.gameObject.SetActive(false);
        }

        if (uiRefs.row2Title != null)
        {
            uiRefs.row2Title.gameObject.SetActive(false);
        }

        if (uiRefs.skipHint != null)
        {
            // Skip hint content/style are now fully defined by the UI prefab.
        }

        for (int i = 0; i < x; i++)
        {
            bool matched = IsColumnMatched(i);
            bool highlight = i == previewHighlightIndex;
            bool isXColumn = i < sceneRow.Length && sceneRow[i] == 'X';

            Color bg = isXColumn ? RowXColor : RowBackgroundColor;
            if (!isXColumn && matched)
            {
                bg = RowMatchedColor;
            }
            else if (!isXColumn && highlight)
            {
                bg = RowHighlightColor;
            }

            char? row1Token = i < sceneRow.Length ? sceneRow[i] : (char?)null;
            char? row2Token = i < playerRecord.Count ? playerRecord[i] : (char?)null;
            Sprite row1Sprite = row1Token.HasValue ? GetMappedSprite(row1Token.Value) : null;
            Sprite row2Sprite = row2Token.HasValue ? GetMappedSprite(row2Token.Value) : null;
            row1Slots[i].SetVisual(bg, row1Sprite, row1Token);
            row2Slots[i].SetVisual(bg, row2Sprite, row2Token);
        }

        LayoutRows(x);
        uiDirty = false;
    }

    private Sprite GetMappedSprite(char token)
    {
        Sprite sprite;
        if (iconSpriteByToken.TryGetValue(char.ToUpperInvariant(token), out sprite))
        {
            return sprite;
        }

        return null;
    }

    private void EnsureSlotPool(List<ActionMatchUISlot> pool, RectTransform root, int count)
    {
        if (root == null || uiRefs.slotTemplate == null)
        {
            return;
        }

        while (pool.Count < count)
        {
            ActionMatchUISlot slot = Instantiate(uiRefs.slotTemplate, root);
            slot.gameObject.SetActive(true);
            pool.Add(slot);
        }

        for (int i = 0; i < pool.Count; i++)
        {
            pool[i].gameObject.SetActive(i < count);
        }
    }

    private void LayoutRows(int count)
    {
        if (uiRefs.row1Root == null || uiRefs.row2Root == null)
        {
            return;
        }

        uiRefs.row2Root.anchorMin = new Vector2(0.5f, 0f);
        uiRefs.row2Root.anchorMax = new Vector2(0.5f, 0f);
        uiRefs.row2Root.pivot = new Vector2(0.5f, 0.5f);
        uiRefs.row2Root.anchoredPosition = new Vector2(0f, BottomPadding + SlotSize * 0.5f);

        uiRefs.row1Root.anchorMin = new Vector2(0.5f, 0f);
        uiRefs.row1Root.anchorMax = new Vector2(0.5f, 0f);
        uiRefs.row1Root.pivot = new Vector2(0.5f, 0.5f);
        uiRefs.row1Root.anchoredPosition = new Vector2(0f, BottomPadding + SlotSize * 1.5f + RowGap);

        float rowWidth = count * SlotSize + (count - 1) * FixedSlotGap;
        float firstCenterX = -rowWidth * 0.5f + SlotSize * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float x = firstCenterX + i * (SlotSize + FixedSlotGap);
            PositionSlot(row1Slots, i, x);
            PositionSlot(row2Slots, i, x);
        }
    }

    private static void PositionSlot(List<ActionMatchUISlot> pool, int index, float x)
    {
        if (index >= pool.Count || pool[index] == null)
        {
            return;
        }

        RectTransform rt = pool[index].transform as RectTransform;
        if (rt == null)
        {
            return;
        }

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(SlotSize, SlotSize);
        rt.anchoredPosition = new Vector2(x, 0f);
    }

    private void UpdateFlashOverlay()
    {
        if (uiRefs == null || uiRefs.flashOverlay == null)
        {
            return;
        }

        bool active = Time.realtimeSinceStartup < noMatchFlashUntilRealtime;
        uiRefs.flashOverlay.gameObject.SetActive(active);
        if (active)
        {
            uiRefs.flashOverlay.color = NoMatchFlashColor;
        }
    }

    private static char MapActionToToken(PlayerActionType action)
    {
        switch (action)
        {
            case PlayerActionType.Jump:
                return 'A';
            case PlayerActionType.Land:
                return 'B';
            case PlayerActionType.Dash:
                return 'C';
            default:
                return '?';
        }
    }

    private string BuildInputMismatchReason(char actualToken)
    {
        if (IsInputAtOrAfterX(playerRecord.Count))
        {
            return string.Format(
                "输入[{0}]后失败：第{1}格是[X]空动作终止位，X位及其后不允许再有玩家输入。",
                actualToken,
                firstXColumnIndex + 1);
        }

        return string.Format(
            "输入[{0}]后无法继续匹配：在考虑X/F可跳过后，序列仍无有效匹配路径。",
            actualToken);
    }

    private void OnGUI()
    {
        if (uiRefs != null)
        {
            return;
        }

        const float horizontalPadding = 12f;
        float row2Y = Screen.height - BottomPadding - SlotSize;
        float row1Y = row2Y - SlotSize - 8f;

        DrawRow(sceneRow, row1Y);
        DrawRow(playerRecord, row2Y);

        // Always visible hint line under the two rows.
        GUI.Label(
            new Rect(0f, row2Y + SlotSize + 6f, Screen.width, 20f),
            "Press E to Skip . Press R to Restart.",
            GetSkipHintStyle());

        if (enableDemoInput)
        {
            GUI.Label(
                new Rect(horizontalPadding, row1Y - 40f, 800f, 18f),
                "Demo keys: J=Jump ok, K=Jump fail, L=Land ok, ;=Dash ok, M=Toggle ismark");
        }

        if (Time.realtimeSinceStartup < noMatchFlashUntilRealtime)
        {
            DrawFullScreenOverlay(NoMatchFlashColor);
        }
    }

    private void DrawRow(IReadOnlyList<char> values, float y)
    {
        int x = Mathf.Max(MinAllowedSlots, slotCount);
        float rowWidth = x * SlotSize + (x - 1) * FixedSlotGap;
        float startX = Mathf.Max(12f, (Screen.width - rowWidth) * 0.5f);

        for (int i = 0; i < x; i++)
        {
            float boxX = startX + i * (SlotSize + FixedSlotGap);
            Rect boxRect = new Rect(boxX, y, SlotSize, SlotSize);
            bool highlight = ReferenceEquals(values, sceneRow) && i == previewHighlightIndex;
            bool matched = IsColumnMatched(i);
            bool isXColumn = i < sceneRow.Length && sceneRow[i] == 'X';
            Color bg = isXColumn ? RowXColor : RowBackgroundColor;
            if (!isXColumn && matched)
            {
                bg = RowMatchedColor;
            }
            else if (!isXColumn && highlight)
            {
                bg = RowHighlightColor;
            }

            DrawSlotBackground(boxRect, bg);

            if (i >= values.Count)
            {
                continue;
            }

            char token = values[i];
            if (TryDrawIcon(token, boxRect))
            {
                continue;
            }

            GUI.Label(boxRect, token.ToString(), GetCenteredLabelStyle());
        }
    }

    private bool IsColumnMatched(int index)
    {
        if (index < 0 || index >= sceneRow.Length)
        {
            return false;
        }

        if (sceneRow[index] == 'X' || sceneRow[index] == 'F')
        {
            return true;
        }

        if (index >= playerRecord.Count)
        {
            return false;
        }

        return !IsTokenMismatchAt(index, playerRecord[index]);
    }

    private bool IsTokenMismatchAt(int index, char actualToken)
    {
        if (sceneRow == null || index < 0 || index >= sceneRow.Length)
        {
            return true;
        }

        char expected = sceneRow[index];
        if (expected == 'F')
        {
            return false;
        }

        if (expected == 'X')
        {
            return true;
        }

        return expected != actualToken;
    }

    private bool IsRecordStillMatchable()
    {
        return EvaluateRecordMatch(false);
    }

    private bool IsRecordFullyMatched()
    {
        return EvaluateRecordMatch(true);
    }

    private bool EvaluateRecordMatch(bool requireFullMatch)
    {
        if (sceneRow == null || hasInvalidXPlacement)
        {
            return false;
        }

        if (IsInputAtOrAfterX(playerRecord.Count))
        {
            return false;
        }

        int sceneLen = requireFullMatch
            ? (sceneRow != null ? sceneRow.Length : 0)
            : GetMatchColumnCount();
        bool[] states = new bool[sceneLen + 1];
        states[0] = true;
        ApplyWildcardSkipClosure(states, sceneLen);

        int recordCountToEvaluate = requireFullMatch
            ? Mathf.Min(playerRecord.Count, sceneLen)
            : playerRecord.Count;

        for (int r = 0; r < recordCountToEvaluate; r++)
        {
            char actual = playerRecord[r];
            bool[] next = new bool[sceneLen + 1];

            for (int i = 0; i < sceneLen; i++)
            {
                if (!states[i])
                {
                    continue;
                }

                char expected = GetExpectedTokenAt(i);
                if (expected == 'F' || expected == 'X' || expected == '\0')
                {
                    // F/X/empty slot can consume one arbitrary player input.
                    next[i + 1] = true;
                }
                else if (expected == actual)
                {
                    next[i + 1] = true;
                }
            }

            states = next;
            ApplyWildcardSkipClosure(states, sceneLen);
            if (!HasAnyState(states))
            {
                return false;
            }
        }

        if (!requireFullMatch)
        {
            return HasAnyState(states);
        }

        return states[sceneLen];
    }

    private void ApplyWildcardSkipClosure(bool[] states, int sceneLen)
    {
        if (states == null || sceneRow == null)
        {
            return;
        }

        for (int i = 0; i < sceneLen; i++)
        {
            if (!states[i])
            {
                continue;
            }

            char expected = GetExpectedTokenAt(i);
            if (expected == 'F' || expected == 'X' || expected == '\0')
            {
                states[i + 1] = true;
            }
        }
    }

    private int GetMatchColumnCount()
    {
        return Mathf.Max(sceneRow != null ? sceneRow.Length : 0, Mathf.Max(MinAllowedSlots, slotCount));
    }

    private char GetExpectedTokenAt(int index)
    {
        if (sceneRow != null && index >= 0 && index < sceneRow.Length)
        {
            return sceneRow[index];
        }

        // Empty slot in row-1 (no character configured).
        return '\0';
    }

    private void ValidateXPlacement()
    {
        if (sceneRow == null || sceneRow.Length == 0)
        {
            return;
        }

        bool seenX = false;
        int firstXIndex = -1;
        for (int i = 0; i < sceneRow.Length; i++)
        {
            char c = sceneRow[i];
            if (c == 'X')
            {
                seenX = true;
                if (firstXIndex < 0)
                {
                    firstXIndex = i;
                    firstXColumnIndex = i;
                }
                continue;
            }

            if (seenX)
            {
                hasInvalidXPlacement = true;
                invalidXPlacementReason = string.Format(
                    "配置错误：第{0}格是[X]空动作，但其后第{1}格仍有动作[{2}]。X后不能再有动作（包括X这一格判失败）。",
                    firstXIndex + 1,
                    i + 1,
                    c);
                Debug.LogWarning("[ActionMatchUIManager] " + invalidXPlacementReason);
                return;
            }
        }
    }

    private bool IsInputAtOrAfterX(int inputCount)
    {
        if (firstXColumnIndex < 0)
        {
            return false;
        }

        // firstXColumnIndex is 0-based. Inputs are 1..N, so inputCount > firstXIndex means at X or after.
        return inputCount > firstXColumnIndex;
    }

    private static bool HasAnyState(bool[] states)
    {
        if (states == null)
        {
            return false;
        }

        for (int i = 0; i < states.Length; i++)
        {
            if (states[i])
            {
                return true;
            }
        }

        return false;
    }

    private void NormalizeSlotCount()
    {
        slotCount = Mathf.Clamp(slotCount, MinAllowedSlots, MaxAllowedSlots);
    }

    private bool TryDrawIcon(char token, Rect boxRect)
    {
        Sprite sprite;
        if (!iconSpriteByToken.TryGetValue(char.ToUpperInvariant(token), out sprite) || sprite == null || sprite.texture == null)
        {
            return false;
        }

        float padding = 3f;
        Rect drawRect = new Rect(
            boxRect.x + padding,
            boxRect.y + padding,
            Mathf.Max(1f, boxRect.width - padding * 2f),
            Mathf.Max(1f, boxRect.height - padding * 2f));

        Rect spriteRect = sprite.rect;
        Texture2D tex = sprite.texture;
        Rect uv = new Rect(
            spriteRect.x / tex.width,
            spriteRect.y / tex.height,
            spriteRect.width / tex.width,
            spriteRect.height / tex.height);

        GUI.DrawTextureWithTexCoords(drawRect, tex, uv, true);
        return true;
    }

    private GUIStyle GetCenteredLabelStyle()
    {
        if (centeredLabelStyle == null)
        {
            centeredLabelStyle = new GUIStyle(GUI.skin.label);
            centeredLabelStyle.alignment = TextAnchor.MiddleCenter;
            centeredLabelStyle.fontStyle = FontStyle.Bold;
        }

        return centeredLabelStyle;
    }

    private GUIStyle GetSkipHintStyle()
    {
        if (skipHintStyle == null)
        {
            skipHintStyle = new GUIStyle(GUI.skin.label);
            skipHintStyle.alignment = TextAnchor.MiddleCenter;
            skipHintStyle.fontStyle = FontStyle.Bold;
            skipHintStyle.normal.textColor = Color.black;
        }

        return skipHintStyle;
    }

    private static void DrawSlotBackground(Rect rect, Color color)
    {
        // Filled background
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, GetSolidFillTexture(), ScaleMode.StretchToFill);
        GUI.color = old;

        // Border for readability
        GUI.Box(rect, string.Empty, GetBorderBoxStyle());
    }

    private static void DrawFullScreenOverlay(Color color)
    {
        Rect full = new Rect(0f, 0f, Screen.width, Screen.height);
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(full, Texture2D.whiteTexture, ScaleMode.StretchToFill);
        GUI.color = old;
    }

    private IEnumerator PlaySceneRowPreviewAudioCoroutine(float intervalSeconds)
    {
        if (sceneRow == null || sceneRow.Length == 0)
        {
            EndPreviewPhase();
            yield break;
        }

        for (int i = 0; i < sceneRow.Length; i++)
        {
            previewHighlightIndex = i;
            PlayMappedAudio(sceneRow[i]);

            if (intervalSeconds <= 0f)
            {
                yield return null;
                continue;
            }

            float elapsed = 0f;
            while (elapsed < intervalSeconds)
            {
                if (!isPreviewPlaying)
                {
                    yield break;
                }

                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
        }

        EndPreviewPhase();
    }

    private IEnumerator BeginPreviewWithDelayCoroutine(float delaySeconds, float intervalSeconds)
    {
        if (delaySeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(delaySeconds);
        }

        previewStartCoroutine = null;
        previewHighlightIndex = -1;
        sceneRowPreviewCoroutine = StartCoroutine(PlaySceneRowPreviewAudioCoroutine(intervalSeconds));
    }

    private static GUIStyle GetBorderBoxStyle()
    {
        ActionMatchUIManager any = FindObjectOfType<ActionMatchUIManager>();
        if (any == null)
        {
            return GUI.skin.box;
        }

        if (any.borderBoxStyle == null)
        {
            any.borderBoxStyle = new GUIStyle(GUI.skin.box);
            any.borderBoxStyle.normal.background = null;
            any.borderBoxStyle.hover.background = null;
            any.borderBoxStyle.active.background = null;
            any.borderBoxStyle.focused.background = null;
        }

        return any.borderBoxStyle;
    }

    private static Texture2D GetSolidFillTexture()
    {
        if (fallbackSolidFillTexture == null)
        {
            fallbackSolidFillTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            fallbackSolidFillTexture.SetPixel(0, 0, Color.white);
            fallbackSolidFillTexture.Apply();
        }

        return fallbackSolidFillTexture;
    }

    private void PlayMappedAudio(char token)
    {
        string clipName;
        if (!audioFxByToken.TryGetValue(char.ToUpperInvariant(token), out clipName) || string.IsNullOrWhiteSpace(clipName))
        {
            return;
        }

        AudioManager.playFx(clipName);
    }

    private void BeginPreviewPhase()
    {
        if (isPreviewPlaying)
        {
            return;
        }

        isPreviewPlaying = true;
        if (!hasFrozenSceneForPreview)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            hasFrozenSceneForPreview = true;
        }
    }

    private void EndPreviewPhase()
    {
        isPreviewPlaying = false;
        previewHighlightIndex = -1;
        previewStartCoroutine = null;
        sceneRowPreviewCoroutine = null;

        if (hasFrozenSceneForPreview)
        {
            Time.timeScale = previousTimeScale;
            hasFrozenSceneForPreview = false;
        }
    }

    private void FinishPreviewEarly()
    {
        if (previewStartCoroutine != null)
        {
            StopCoroutine(previewStartCoroutine);
            previewStartCoroutine = null;
        }

        if (sceneRowPreviewCoroutine != null)
        {
            StopCoroutine(sceneRowPreviewCoroutine);
        }

        EndPreviewPhase();
    }

    private void ReloadCurrentSceneNow()
    {
        dialogSystem.Close();

        // Ensure no freeze leaks into reloaded scene.
        Time.timeScale = 1f;
        hasFrozenSceneForPreview = false;
        isPreviewPlaying = false;
        previewHighlightIndex = -1;

        if (previewStartCoroutine != null)
        {
            StopCoroutine(previewStartCoroutine);
            previewStartCoroutine = null;
        }

        if (sceneRowPreviewCoroutine != null)
        {
            StopCoroutine(sceneRowPreviewCoroutine);
            sceneRowPreviewCoroutine = null;
        }

        Scene active = SceneManager.GetActiveScene();
        GameManager.ReloadCurrentScene();
    }

    private void StartNoMatchSequence()
    {
        if (noMatchSequenceCoroutine != null)
        {
            return;
        }

        noMatchSequenceCoroutine = StartCoroutine(NoMatchSequenceCoroutine());
    }

    private IEnumerator NoMatchSequenceCoroutine()
    {
        isNoMatchSequenceRunning = true;

        // 1) Flash red.
        noMatchFlashUntilRealtime = Time.realtimeSinceStartup + NoMatchFlashDurationSeconds;

        // 2) Show dialog.
        const string noMatchDialog = "Obey the SIGNAL!";
        dialogSystem.Open(noMatchDialog);

        // Wait until dialog typewriter fully shows all words.
        float dialogTypingSeconds = dialogSystem.EstimateTypewriterDuration(noMatchDialog);
        if (dialogTypingSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(dialogTypingSeconds);
        }

        // 3) After full dialog shown, wait waitEND then restart.
        if (noMatchWaitEndSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(noMatchWaitEndSeconds);
        }

        dialogSystem.Close();
        isNoMatchSequenceRunning = false;
        noMatchSequenceCoroutine = null;
        ReloadCurrentSceneNow();
    }

    private IEnumerator DeadAreaFailSequenceCoroutine(float waitEndSeconds)
    {
        // 1) Flash red.
        noMatchFlashUntilRealtime = Time.realtimeSinceStartup + NoMatchFlashDurationSeconds;

        // 2) Wait waitEND then restart.
        if (waitEndSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(waitEndSeconds);
        }

        deadAreaSequenceCoroutine = null;
        ReloadCurrentSceneNow();
    }
}
