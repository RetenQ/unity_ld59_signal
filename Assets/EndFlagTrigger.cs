using UnityEngine;
using UnityEngine.SceneManagement;

public class EndFlagTrigger : MonoBehaviour
{
    [Header("Scene Flow")]
    [SerializeField] private string nextSceneName = string.Empty;
    [SerializeField] private bool autoUseNextBuildIndex = true;

    [Header("Refs")]
    [SerializeField] private ActionMatchUIManager actionMatchUIManager;

    private bool consumed;
    private BasicPlatformerController2D frozenController;
    private Rigidbody2D frozenRigidbody;
    private bool frozenControllerPrevEnabled;
    private bool hasFrozenPlayer;

    private void Awake()
    {
        if (actionMatchUIManager == null)
        {
            actionMatchUIManager = FindObjectOfType<ActionMatchUIManager>();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed || !IsPlayerCollider(other))
        {
            return;
        }

        consumed = true;

        if (actionMatchUIManager == null)
        {
            actionMatchUIManager = FindObjectOfType<ActionMatchUIManager>();
        }

        if (actionMatchUIManager == null)
        {
            Debug.LogError("[EndFlagTrigger] ActionMatchUIManager not found.");
            consumed = false;
            return;
        }

        if (!actionMatchUIManager.AreRowsFullyMatched())
        {
            string reason = actionMatchUIManager.GetFirstMismatchReason();
            Debug.LogWarning("[EndFlagTrigger] 失败: " + reason);
            actionMatchUIManager.TriggerNoMatchFlow(reason);
            return;
        }

        Debug.Log("[EndFlagTrigger] 成功");
        FreezePlayer(other);
        if (!LoadNextScene())
        {
            UnfreezePlayer();
            consumed = false;
        }
    }

    private bool LoadNextScene()
    {
        if (!string.IsNullOrWhiteSpace(nextSceneName) && Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            GameManager.TransitionToSceneWithCongratulation(nextSceneName);
            return true;
        }

        if (autoUseNextBuildIndex)
        {
            int next = SceneManager.GetActiveScene().buildIndex + 1;
            if (next >= 0 && next < SceneManager.sceneCountInBuildSettings)
            {
                GameManager.TransitionToBuildIndexWithCongratulation(next);
                return true;
            }
        }

        Debug.LogWarning("[EndFlagTrigger] Next scene is not configured or not available.");
        return false;
    }

    private static bool IsPlayerCollider(Collider2D col)
    {
        if (col == null)
        {
            return false;
        }

        if (col.CompareTag("Player"))
        {
            return true;
        }

        if (col.GetComponentInParent<BasicPlatformerController2D>() != null)
        {
            return true;
        }

        if (col.attachedRigidbody != null
            && col.attachedRigidbody.GetComponent<BasicPlatformerController2D>() != null)
        {
            return true;
        }

        return false;
    }

    private void FreezePlayer(Collider2D col)
    {
        if (hasFrozenPlayer || col == null)
        {
            return;
        }

        frozenController = col.GetComponentInParent<BasicPlatformerController2D>();
        if (frozenController != null)
        {
            frozenControllerPrevEnabled = frozenController.enabled;
            frozenController.enabled = false;
        }

        frozenRigidbody = col.attachedRigidbody != null
            ? col.attachedRigidbody
            : col.GetComponentInParent<Rigidbody2D>();

        if (frozenRigidbody != null)
        {
            frozenRigidbody.velocity = Vector2.zero;
            frozenRigidbody.angularVelocity = 0f;
            frozenRigidbody.simulated = false;
        }

        hasFrozenPlayer = true;
    }

    private void UnfreezePlayer()
    {
        if (!hasFrozenPlayer)
        {
            return;
        }

        if (frozenRigidbody != null)
        {
            frozenRigidbody.simulated = true;
        }

        if (frozenController != null)
        {
            frozenController.enabled = frozenControllerPrevEnabled;
        }

        frozenController = null;
        frozenRigidbody = null;
        hasFrozenPlayer = false;
    }
}
