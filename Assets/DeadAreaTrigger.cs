using UnityEngine;

public class DeadAreaTrigger : MonoBehaviour
{
    [SerializeField] private float waitEND = 1f;
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

        if (actionMatchUIManager != null)
        {
            FreezePlayer(other);
            actionMatchUIManager.TriggerDeadAreaFailFlow(waitEND);
            return;
        }

        Debug.LogWarning("[DeadAreaTrigger] ActionMatchUIManager not found, fallback reload.");
        GameManager.ReloadCurrentScene();
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
}
