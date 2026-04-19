using UnityEngine;
using UnityEngine.UI;

public class ActionMatchUISlot : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private Text label;

    public void SetVisual(Color bgColor, Sprite sprite, char? token)
    {
        if (background != null)
        {
            background.color = bgColor;
        }

        bool hasIcon = sprite != null;
        if (icon != null)
        {
            icon.enabled = hasIcon;
            icon.sprite = sprite;
        }

        if (label != null)
        {
            label.text = hasIcon ? string.Empty : (token.HasValue ? token.Value.ToString() : string.Empty);
        }
    }
}
