using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class AudioManager : MonoBehaviour
{
    private const int DefaultPlayerCount = 3;
    private const string FxFolderPath = "Assets/musicFx";
    private const string ProjectRootResourcesFxFolderPath = "Assets/Resources/musicFx";
    private const string ResourcesFxPath = "musicFx";

    public static AudioManager Instance { get; private set; }

    [Header("Pool")]
    [SerializeField] private int playerCount = DefaultPlayerCount;
    [SerializeField] private List<AudioClip> clipLibrary = new List<AudioClip>();
    [SerializeField] private bool enableTestHotkeys = true;

    private readonly List<AudioSource> players = new List<AudioSource>();
    private readonly Dictionary<string, AudioClip> clipByName = new Dictionary<string, AudioClip>();
    private readonly Dictionary<string, int> playerIndexByClipName = new Dictionary<string, int>();
    private readonly List<string> clipNameByPlayerIndex = new List<string>();
    private readonly List<int> lastUseTickByPlayerIndex = new List<int>();
    private int useTick;
    private string startupSourceLabel = "none";

    public static void playFx(string clipName)
    {
        if (Instance == null)
        {
            Debug.LogWarning("[AudioManager] playFx called before AudioManager exists.");
            return;
        }

        Instance.PlayFxInternal(clipName);
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

        playerCount = Mathf.Max(1, playerCount);
        EnsurePlayers();
        LoadLibraryByPriority(logSource: true);
    }

    private void OnValidate()
    {
        playerCount = Mathf.Max(1, playerCount);
        EnsurePlayers();
        LoadLibraryByPriority(logSource: false);
    }

    private void Reset()
    {
        playerCount = DefaultPlayerCount;
        EnsurePlayers();
        LoadLibraryByPriority(logSource: false);
    }

    private void Update()
    {
        if (!enableTestHotkeys)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            playFx("t1");
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            playFx("t2");
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            playFx("t3");
        }

        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            playFx("t4");
        }

        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            playFx("t5");
        }
    }

    private void EnsurePlayers()
    {
        players.Clear();
        GetComponents(players);

        while (players.Count < playerCount)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            ConfigurePlayer(source);
            players.Add(source);
        }

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i] == null)
            {
                continue;
            }

            ConfigurePlayer(players[i]);
        }

        SyncPlayerStateArrays();
    }

    private void ConfigurePlayer(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
    }

    private void SyncPlayerStateArrays()
    {
        while (clipNameByPlayerIndex.Count < players.Count)
        {
            clipNameByPlayerIndex.Add(string.Empty);
        }

        while (lastUseTickByPlayerIndex.Count < players.Count)
        {
            lastUseTickByPlayerIndex.Add(0);
        }

        if (clipNameByPlayerIndex.Count > players.Count)
        {
            clipNameByPlayerIndex.RemoveRange(players.Count, clipNameByPlayerIndex.Count - players.Count);
        }

        if (lastUseTickByPlayerIndex.Count > players.Count)
        {
            lastUseTickByPlayerIndex.RemoveRange(players.Count, lastUseTickByPlayerIndex.Count - players.Count);
        }
    }

    private void BuildClipCache()
    {
        clipByName.Clear();

        for (int i = 0; i < clipLibrary.Count; i++)
        {
            AudioClip clip = clipLibrary[i];
            if (clip == null)
            {
                continue;
            }

            if (!clipByName.ContainsKey(clip.name))
            {
                clipByName.Add(clip.name, clip);
            }
        }
    }

    private void LoadLibraryByPriority(bool logSource)
    {
        clipLibrary.Clear();
        startupSourceLabel = "none";

#if UNITY_EDITOR
        if (TryRefreshLibraryFromProjectRootResources())
        {
            startupSourceLabel = ProjectRootResourcesFxFolderPath + " (Editor AssetDatabase)";
        }
#endif

        if (clipLibrary.Count == 0 && RefreshLibraryFromResources())
        {
            startupSourceLabel = "Assets/**/Resources/" + ResourcesFxPath + " (Resources.LoadAll)";
        }

#if UNITY_EDITOR
        if (clipLibrary.Count == 0 && TryRefreshLibraryFromAssetsMusicFx())
        {
            startupSourceLabel = FxFolderPath + " (Editor fallback)";
        }
#endif

        BuildClipCache();

        if (logSource)
        {
            Debug.Log("[AudioManager] Startup source: " + startupSourceLabel + ", clips=" + clipLibrary.Count);
        }
    }

    private bool RefreshLibraryFromResources()
    {
        AudioClip[] resourcesClips = Resources.LoadAll<AudioClip>(ResourcesFxPath);
        if (resourcesClips == null || resourcesClips.Length == 0)
        {
            return false;
        }

        clipLibrary.Clear();
        for (int i = 0; i < resourcesClips.Length; i++)
        {
            AudioClip clip = resourcesClips[i];
            if (clip != null)
            {
                clipLibrary.Add(clip);
            }
        }

        return clipLibrary.Count > 0;
    }

    private void PlayFxInternal(string clipName)
    {
        if (string.IsNullOrWhiteSpace(clipName))
        {
            return;
        }

        AudioClip clip = ResolveClip(clipName);
        if (clip == null)
        {
            Debug.LogWarning("[AudioManager] Clip not found: " + clipName
                + " (checked Resources/musicFx, project root Resources/musicFx, Assets/musicFx)");
            return;
        }

        EnsurePlayers();

        int playerIndex;
        if (TryGetMountedPlayerIndex(clipName, out playerIndex))
        {
            PlayOnPlayer(playerIndex, clipName, clip);
            return;
        }

        playerIndex = GetOldestUnusedPlayerIndex();
        MountClipToPlayer(playerIndex, clipName, clip);
        PlayOnPlayer(playerIndex, clipName, clip);
    }

    private AudioClip ResolveClip(string clipName)
    {
        AudioClip clip;
        if (clipByName.TryGetValue(clipName, out clip))
        {
            return clip;
        }

        clip = Resources.Load<AudioClip>(ResourcesFxPath + "/" + clipName);
        if (clip != null)
        {
            clipByName[clipName] = clip;
            return clip;
        }

#if UNITY_EDITOR
        clip = TryFindClipInEditorFolder(clipName, ProjectRootResourcesFxFolderPath);
        if (clip != null)
        {
            clipByName[clipName] = clip;
            if (!clipLibrary.Contains(clip))
            {
                clipLibrary.Add(clip);
            }

            return clip;
        }

        string[] guids = AssetDatabase.FindAssets(clipName + " t:AudioClip", new[] { FxFolderPath });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            AudioClip editorClip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (editorClip != null && editorClip.name == clipName)
            {
                clipByName[clipName] = editorClip;
                if (!clipLibrary.Contains(editorClip))
                {
                    clipLibrary.Add(editorClip);
                }

                return editorClip;
            }
        }
#endif

        return null;
    }

    private bool TryGetMountedPlayerIndex(string clipName, out int playerIndex)
    {
        playerIndex = -1;
        int idx;
        if (!playerIndexByClipName.TryGetValue(clipName, out idx))
        {
            return false;
        }

        if (idx < 0 || idx >= players.Count || players[idx] == null)
        {
            playerIndexByClipName.Remove(clipName);
            return false;
        }

        playerIndex = idx;
        return true;
    }

    private int GetOldestUnusedPlayerIndex()
    {
        int selected = -1;
        int oldestTick = int.MaxValue;

        for (int i = 0; i < players.Count; i++)
        {
            AudioSource source = players[i];
            if (source == null || source.isPlaying)
            {
                continue;
            }

            if (lastUseTickByPlayerIndex[i] < oldestTick)
            {
                oldestTick = lastUseTickByPlayerIndex[i];
                selected = i;
            }
        }

        if (selected >= 0)
        {
            return selected;
        }

        // If all are busy, reuse the least recently used one.
        selected = 0;
        oldestTick = lastUseTickByPlayerIndex[0];
        for (int i = 1; i < players.Count; i++)
        {
            if (lastUseTickByPlayerIndex[i] < oldestTick)
            {
                oldestTick = lastUseTickByPlayerIndex[i];
                selected = i;
            }
        }

        return selected;
    }

    private void MountClipToPlayer(int playerIndex, string clipName, AudioClip clip)
    {
        string oldClipName = clipNameByPlayerIndex[playerIndex];
        if (!string.IsNullOrEmpty(oldClipName))
        {
            playerIndexByClipName.Remove(oldClipName);
        }

        clipNameByPlayerIndex[playerIndex] = clipName;
        playerIndexByClipName[clipName] = playerIndex;
        players[playerIndex].clip = clip;
    }

    private void PlayOnPlayer(int playerIndex, string clipName, AudioClip clip)
    {
        AudioSource source = players[playerIndex];
        if (source.clip != clip)
        {
            source.clip = clip;
        }

        if (source.isPlaying)
        {
            source.Stop();
        }

        source.Play();
        lastUseTickByPlayerIndex[playerIndex] = ++useTick;
        clipNameByPlayerIndex[playerIndex] = clipName;
        playerIndexByClipName[clipName] = playerIndex;
    }

#if UNITY_EDITOR
    [ContextMenu("Refresh Clip Library From Project Root Resources/musicFx")]
    private void RefreshLibraryFromProjectRootResources()
    {
        TryRefreshLibraryFromProjectRootResources();
        BuildClipCache();
    }

    [ContextMenu("Refresh Clip Library From Assets/musicFx")]
    private void RefreshLibraryFromFolder()
    {
        TryRefreshLibraryFromAssetsMusicFx();
        BuildClipCache();
    }

    private bool TryRefreshLibraryFromProjectRootResources()
    {
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { ProjectRootResourcesFxFolderPath });
        clipLibrary.Clear();

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null)
            {
                clipLibrary.Add(clip);
            }
        }

        return clipLibrary.Count > 0;
    }

    private bool TryRefreshLibraryFromAssetsMusicFx()
    {
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { FxFolderPath });
        clipLibrary.Clear();

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null)
            {
                clipLibrary.Add(clip);
            }
        }

        return clipLibrary.Count > 0;
    }

    private AudioClip TryFindClipInEditorFolder(string clipName, string folderPath)
    {
        string[] guids = AssetDatabase.FindAssets(clipName + " t:AudioClip", new[] { folderPath });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null && clip.name == clipName)
            {
                return clip;
            }
        }

        return null;
    }
#endif
}
