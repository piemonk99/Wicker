using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class WeaponSlotUI : BaseSlotUI<WeaponConfig>
{
    [Header("Weapon-specific UI")]
    [SerializeField] private Image _rarityBorder; // Optional for visual flair
    [SerializeField] private GameObject _lockedIndicator; // If you have locked weapons

    protected override void UpdateDisplay()
    {
        if (_itemData != null)
        {
            if (_iconImage != null && _itemData.VisualConfig?.icon != null)
            {
                _iconImage.sprite = _itemData.VisualConfig.icon;
                _iconImage.color = Color.white;
            }

            if (_nameText != null)
            {
                _nameText.text = _itemData.weaponName;
            }

            if (_typeText != null)
            {
                _typeText.text = _itemData.weaponType.ToString();
            }
        }
        else
        {
            _iconImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            _nameText.text = "Empty";
            _typeText.text = "";
        }
    }

    protected override void OnDoubleClick()
    {
        // Equip weapon on double-click - will be handled by page controller
        // This is just a fallback
    }
}