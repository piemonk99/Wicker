using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public abstract class BaseSlotUI<T> : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    where T : ScriptableObject
{
    [Header("UI References")]
    [SerializeField] protected Image _iconImage;
    [SerializeField] protected Image _backgroundImage;
    [SerializeField] protected GameObject _selectedBorder;
    [SerializeField] protected GameObject _equippedIndicator;
    [SerializeField] protected TextMeshProUGUI _nameText;
    [SerializeField] protected TextMeshProUGUI _typeText; // Optional

    [Header("Colors")]
    [SerializeField] protected Color _normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [SerializeField] protected Color _selectedColor = new Color(0.4f, 0.4f, 0.8f, 1f);
    [SerializeField] protected Color _equippedColor = new Color(0.8f, 0.8f, 0.2f, 1f);
    [SerializeField] protected Color _hoverColor = new Color(0.3f, 0.3f, 0.6f, 1f);

    protected T _itemData;
    protected System.Action<T> _onSelected;
    protected bool _isSelected = false;
    protected bool _isEquipped = false;

    public T ItemData => _itemData;

    public virtual void Initialize(T itemData, System.Action<T> onSelected)
    {
        _itemData = itemData;
        _onSelected = onSelected;

        UpdateDisplay();
        SetSelected(false);
    }

    protected abstract void UpdateDisplay(); // Made abstract - each type implements

    public virtual void Select()
    {
        SetSelected(true);
        _onSelected?.Invoke(_itemData);
    }

    public virtual void SetSelected(bool selected)
    {
        _isSelected = selected;
        if (_selectedBorder != null)
        {
            _selectedBorder.SetActive(selected);
        }

        UpdateBackgroundColor();
    }

    public virtual void SetEquipped(bool equipped)
    {
        _isEquipped = equipped;
        if (_equippedIndicator != null)
        {
            _equippedIndicator.SetActive(equipped);
        }

        UpdateBackgroundColor();
    }

    protected virtual void UpdateBackgroundColor()
    {
        if (_backgroundImage == null) return;

        if (_isEquipped)
        {
            _backgroundImage.color = _equippedColor;
        }
        else if (_isSelected)
        {
            _backgroundImage.color = _selectedColor;
        }
        else
        {
            _backgroundImage.color = _normalColor;
        }
    }

    public virtual void OnPointerEnter(PointerEventData eventData)
    {
        if (_itemData != null && !_isSelected && _backgroundImage != null)
        {
            _backgroundImage.color = _hoverColor;
        }
    }

    public virtual void OnPointerClick(PointerEventData eventData)
    {
        if (_itemData != null)
        {
            Select();

            // Double-click to equip
            if (eventData.clickCount == 2)
            {
                OnDoubleClick();
            }
        }
    }

    protected virtual void OnDoubleClick()
    {
        // Override in derived classes to handle double-click equipping
    }
}