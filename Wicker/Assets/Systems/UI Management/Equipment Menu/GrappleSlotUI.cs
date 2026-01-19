using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class GrappleSlotUI : BaseSlotUI<GrappleConfig>
{
    [Header("Grapple-specific UI")]
    [SerializeField] private Image _rangeIndicator; // Visual range indicator
    [SerializeField] private TextMeshProUGUI _rangeText;

    protected override void UpdateDisplay()
    {
        if (_itemData != null)
        {
            // GrappleConfig doesn't have an icon by default, so we'll just show a colored square
            if (_iconImage != null)
            {
                _iconImage.color = Color.cyan; // Default grapple color
            }

            if (_nameText != null)
            {
                _nameText.text = _itemData.GrappleName;
            }

            if (_typeText != null)
            {
                _typeText.text = "Grapple";
            }

            // Show max distance from physics config
            if (_rangeText != null && _itemData.physicsConfig != null)
            {
                _rangeText.text = $"Range: {_itemData.physicsConfig.maxDistance:F0}m";
            }
        }
        else
        {
            _iconImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            _nameText.text = "Empty";
            _typeText.text = "";
            if (_rangeText != null) _rangeText.text = "";
        }
    }

    protected override void OnDoubleClick()
    {
        // Equip grapple on double-click - will be handled by page controller
        // This is just a fallback
    }
}