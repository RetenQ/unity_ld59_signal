using UnityEngine;

public class NoiseAreaTrigger : MonoBehaviour
{
    [SerializeField] private ActionMatchUIManager actionMatchUIManager;
    private int playerOverlapCount;

    private void Awake()
    {
        if (actionMatchUIManager == null)
        {
            actionMatchUIManager = FindObjectOfType<ActionMatchUIManager>();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayerCollider(other))
        {
            return;
        }

        if (actionMatchUIManager == null)
        {
            actionMatchUIManager = FindObjectOfType<ActionMatchUIManager>();
        }

        if (actionMatchUIManager != null)
        {
            playerOverlapCount++;
            actionMatchUIManager.SetIsmark(false);
            Debug.Log("[NoiseAreaTrigger] Player entered noiseArea: ismark set to false.");
        }
        else
        {
            Debug.LogWarning("[NoiseAreaTrigger] ActionMatchUIManager not found.");
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayerCollider(other))
        {
            return;
        }

        if (actionMatchUIManager == null)
        {
            actionMatchUIManager = FindObjectOfType<ActionMatchUIManager>();
        }

        if (actionMatchUIManager == null)
        {
            Debug.LogWarning("[NoiseAreaTrigger] ActionMatchUIManager not found.");
            return;
        }

        playerOverlapCount = Mathf.Max(0, playerOverlapCount - 1);
        if (playerOverlapCount == 0)
        {
            actionMatchUIManager.SetIsmark(true);
            Debug.Log("[NoiseAreaTrigger] Player exited noiseArea: ismark restored to true.");
        }
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
}
