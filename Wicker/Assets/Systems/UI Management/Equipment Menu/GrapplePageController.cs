using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GrapplePageController : BaseEquipmentPage
{
    [Header("UI Container References")]
    [SerializeField] private Transform _grapplesListContent;
    [SerializeField] private GrappleSlotUI _grappleSlotPrefab;

    [Header("Display References")]
    [SerializeField] private Image _grappleSprite;
    [SerializeField] private TextMeshProUGUI _grappleNameText;
    [SerializeField] private GameObject _equippedIndicator;
    [SerializeField] private Button _equipButton;

    [Header("Stats Containers")]
    [SerializeField] private Transform _baseStatsContainer;
    [SerializeField] private Transform _propertiesContainer;

    [Header("Prefabs for Dynamic UI")]
    [SerializeField] private GameObject _statRowPrefab;

    private CharacterInventory _playerInventory;
    private CharacterEquipment _playerEquipment;

    private List<GrappleSlotUI> _grappleSlots = new List<GrappleSlotUI>();
    private GrappleConfig _selectedGrapple;

    public override void Initialize()
    {
        // Find player components
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var characterCore = player.GetComponent<CharacterCore>();
            if (characterCore != null)
            {
                _playerInventory = characterCore.GetCharacterComponent<CharacterInventory>();
                _playerEquipment = characterCore.GetCharacterComponent<CharacterEquipment>();
            }
        }

        if (_playerInventory == null)
        {
            Debug.LogError("GrapplePageController: Could not find CharacterInventory");
            return;
        }

        // Set up equip button
        if (_equipButton != null)
        {
            _equipButton.onClick.AddListener(OnEquipButtonClicked);
        }

        // Clear everything initially
        ClearAllUI();
    }

    public override void OnPageShown()
    {
        RefreshGrappleList();

        // Select first grapple if we have any
        if (_grappleSlots.Count > 0 && _grappleSlots[0].ItemData != null)
        {
            _grappleSlots[0].Select();
        }
        else
        {
            ClearGrappleDisplay();
        }
    }

    public override void OnPageHidden()
    {
        // Nothing to save
    }

    public override void RefreshData()
    {
        RefreshGrappleList();
    }

    private void RefreshGrappleList()
    {
        if (_playerInventory == null) return;

        // Clear existing slots
        foreach (var slot in _grappleSlots)
        {
            if (slot != null && slot.gameObject != null)
            {
                Destroy(slot.gameObject);
            }
        }
        _grappleSlots.Clear();

        // Create slots for each owned grapple
        foreach (var grapple in _playerInventory.OwnedGrappleHooks)
        {
            if (grapple == null) continue;

            var slot = Instantiate(_grappleSlotPrefab, _grapplesListContent);
            slot.Initialize(grapple, OnGrappleSelected);

            // Check if this grapple is equipped
            bool isEquipped = _playerEquipment != null && _playerEquipment.CurrentGrappleHook == grapple;
            slot.SetEquipped(isEquipped);

            _grappleSlots.Add(slot);
        }
    }

    private void OnGrappleSelected(GrappleConfig grapple)
    {
        _selectedGrapple = grapple;
        UpdateGrappleDisplay(grapple);

        // Update all slots' selected state
        foreach (var slot in _grappleSlots)
        {
            slot.SetSelected(slot.ItemData == grapple);
        }
    }

    private void UpdateGrappleDisplay(GrappleConfig grapple)
    {
        if (grapple == null)
        {
            ClearGrappleDisplay();
            return;
        }

        // Update basic display
        if (_grappleSprite != null)
        {
            _grappleSprite.color = Color.cyan; // Default grapple color
            // You could add an icon property to GrappleConfig later
        }

        if (_grappleNameText != null)
        {
            _grappleNameText.text = grapple.GrappleName;
        }

        // Update equipped indicator
        if (_equippedIndicator != null && _playerEquipment != null)
        {
            _equippedIndicator.SetActive(grapple == _playerEquipment.CurrentGrappleHook);
        }

        // Clear and rebuild stats
        ClearStatsUI();
        PopulateStatsUI(grapple);
    }

    private void PopulateStatsUI(GrappleConfig grapple)
    {
        if (grapple == null) return;

        // Base Stats
        if (_baseStatsContainer != null)
        {
            CreateStatRow("Max Range", $"{grapple.physicsConfig.maxDistance:F0}m", _baseStatsContainer);

            if (grapple.reelConfig != null)
            {
                CreateStatRow("Reel Speed", $"{grapple.reelConfig.reelSpeed:F0}m/s", _baseStatsContainer);
                CreateStatRow("Unreel Speed", $"{grapple.reelConfig.unreelSpeed:F0}m/s", _baseStatsContainer);
            }

            CreateStatRow("Rope Type", grapple.physicsConfig.enableStretch ? "Stretchable" : "Fixed", _baseStatsContainer);
        }

        // Properties/Abilities
        if (_propertiesContainer != null)
        {
            CreateStatRow("Gravity", $"{grapple.movementState.gravityMultiplier:F1}x", _propertiesContainer);

            if (grapple.movementState.allowMovement)
            {
                CreateStatRow("Movement", "Allowed", _propertiesContainer);
            }

            if (grapple.movementState.canJump)
            {
                CreateStatRow("Jumping", "Allowed", _propertiesContainer);
            }

            if (grapple.physicsConfig.enableStretch)
            {
                CreateStatRow("Stiffness", $"{grapple.physicsConfig.stretchStiffness:F0}", _propertiesContainer);
            }

            if (grapple.physicsConfig.friction > 0)
            {
                CreateStatRow("Friction", $"{grapple.physicsConfig.friction:F2}", _propertiesContainer);
            }
        }
    }

    private void CreateStatRow(string statName, string statValue, Transform parent)
    {
        // Same implementation as WeaponsPageController
        if (_statRowPrefab == null)
        {
            var statGO = new GameObject($"Stat_{statName}");
            statGO.transform.SetParent(parent, false);

            var layout = statGO.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            var nameText = new GameObject("Name");
            nameText.transform.SetParent(statGO.transform, false);
            var nameTMP = nameText.AddComponent<TextMeshProUGUI>();
            nameTMP.text = $"{statName}: ";
            nameTMP.fontSize = 16;
            nameTMP.color = new Color(0.8f, 0.8f, 0.8f, 1f);

            var valueText = new GameObject("Value");
            valueText.transform.SetParent(statGO.transform, false);
            var valueTMP = valueText.AddComponent<TextMeshProUGUI>();
            valueTMP.text = statValue;
            valueTMP.fontSize = 16;
            valueTMP.color = Color.white;
            valueTMP.alignment = TextAlignmentOptions.Right;
        }
        else
        {
            var statRow = Instantiate(_statRowPrefab, parent);
            var statUI = statRow.GetComponent<StatRowUI>();
            if (statUI != null)
            {
                statUI.SetStat(statName, statValue);
            }
        }
    }

    private void ClearStatsUI()
    {
        if (_baseStatsContainer != null)
        {
            foreach (Transform child in _baseStatsContainer)
            {
                Destroy(child.gameObject);
            }
        }

        if (_propertiesContainer != null)
        {
            foreach (Transform child in _propertiesContainer)
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void ClearGrappleDisplay()
    {
        _selectedGrapple = null;

        if (_grappleSprite != null)
        {
            _grappleSprite.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        }

        if (_grappleNameText != null)
        {
            _grappleNameText.text = "No grapple selected";
        }

        if (_equippedIndicator != null)
        {
            _equippedIndicator.SetActive(false);
        }

        ClearStatsUI();
    }

    private void ClearAllUI()
    {
        foreach (var slot in _grappleSlots)
        {
            if (slot != null && slot.gameObject != null)
            {
                Destroy(slot.gameObject);
            }
        }
        _grappleSlots.Clear();

        ClearGrappleDisplay();
    }

    private void OnEquipButtonClicked()
    {
        if (_selectedGrapple == null || _playerEquipment == null) return;

        // Equip the grapple
        bool success = _playerEquipment.EquipGrappleHook(_selectedGrapple);

        if (success)
        {
            // Update all slots
            foreach (var slot in _grappleSlots)
            {
                slot.SetEquipped(slot.ItemData == _selectedGrapple);
            }

            // Update equipped indicator
            if (_equippedIndicator != null)
            {
                _equippedIndicator.SetActive(true);
            }
        }
    }
}