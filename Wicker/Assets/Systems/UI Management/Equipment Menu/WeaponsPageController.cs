using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WeaponsPageController : BaseEquipmentPage
{
    [Header("UI Container References")]
    [SerializeField] private Transform _weaponsListContent; // The Content inside ScrollRect
    [SerializeField] private WeaponSlotUI _weaponSlotPrefab;

    [Header("Display References")]
    [SerializeField] private Image _weaponSprite;
    [SerializeField] private TextMeshProUGUI _weaponNameText;
    [SerializeField] private GameObject _equippedIndicator;
    [SerializeField] private Button _equipButton;

    [Header("Stats Containers")]
    [SerializeField] private Transform _baseStatsContainer; // Empty container for base stats
    [SerializeField] private Transform _specialAbilitiesContainer; // Empty container for abilities

    [Header("Prefabs for Dynamic UI")]
    [SerializeField] private GameObject _statRowPrefab; // Prefab for "Damage: 10" type rows
    [SerializeField] private GameObject _abilityPrefab; // Prefab for ability items

    [Header("Data")]
    private CharacterInventory _playerInventory;
    private CharacterEquipment _playerEquipment;

    private List<WeaponSlotUI> _weaponSlots = new List<WeaponSlotUI>();
    private WeaponConfig _selectedWeapon;

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
            Debug.LogError("WeaponsPageController: Could not find CharacterInventory");
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
        Debug.Log("shown");
        RefreshWeaponList();

        // Select first weapon if we have any
        if (_weaponSlots.Count > 0 && _weaponSlots[0].ItemData != null)
        {
            _weaponSlots[0].Select();
        }
        else
        {
            ClearWeaponDisplay();
        }
    }

    public override void OnPageHidden()
    {
        // Nothing to save
    }

    public override void RefreshData()
    {
        RefreshWeaponList();
    }

    private void RefreshWeaponList()
    {
        Debug.Log("got here1");
        if (_playerInventory == null) return;
        Debug.Log("got here2");

        // Clear existing slots
        foreach (var slot in _weaponSlots)
        {
            if (slot != null && slot.gameObject != null)
            {
                Destroy(slot.gameObject);
            }
        }
        _weaponSlots.Clear();

        // Create slots for each owned weapon
        foreach (var weapon in _playerInventory.OwnedWeapons)
        {
            if (weapon == null) continue;

            var slot = Instantiate(_weaponSlotPrefab, _weaponsListContent);
            slot.Initialize(weapon, OnWeaponSelected);

            // Check if this weapon is equipped
            bool isEquipped = _playerEquipment != null && _playerEquipment.CurrentWeapon == weapon;
            slot.SetEquipped(isEquipped);

            _weaponSlots.Add(slot);
        }
    }

    private void OnWeaponSelected(WeaponConfig weapon)
    {
        _selectedWeapon = weapon;
        UpdateWeaponDisplay(weapon);

        // Update all slots' selected state
        foreach (var slot in _weaponSlots)
        {
            slot.SetSelected(slot.ItemData == weapon);
        }
    }

    private void UpdateWeaponDisplay(WeaponConfig weapon)
    {
        if (weapon == null)
        {
            ClearWeaponDisplay();
            return;
        }

        // Update basic display
        if (_weaponSprite != null && weapon.VisualConfig?.icon != null)
        {
            _weaponSprite.sprite = weapon.VisualConfig.icon;
            _weaponSprite.color = Color.white;
        }

        if (_weaponNameText != null)
        {
            _weaponNameText.text = weapon.weaponName;
        }

        // Update equipped indicator
        if (_equippedIndicator != null && _playerEquipment != null)
        {
            _equippedIndicator.SetActive(weapon == _playerEquipment.CurrentWeapon);
        }

        // Clear and rebuild stats
        ClearStatsUI();
        PopulateStatsUI(weapon);
    }

    private void PopulateStatsUI(WeaponConfig weapon)
    {
        if (weapon == null) return;

        var mechanics = weapon.MechanicsConfig;

        // 1. Base Stats
        if (_baseStatsContainer != null)
        {
            // Damage
            if (mechanics != null)
            {
                CreateStatRow("Damage", $"{mechanics.baseDamage:F0}", _baseStatsContainer);

                if (mechanics.scalesWithVelocity)
                {
                    CreateStatRow("Max Damage", $"{mechanics.baseDamage * mechanics.maxVelocityMultiplier:F0}", _baseStatsContainer);
                }
            }

            // Weapon-specific stats
            if (weapon.typeSpecificConfig is HitboxWeaponConfig hitboxConfig)
            {
                CreateStatRow("Attack Speed", $"{1f / hitboxConfig.mechanics.attackCooldown:F1}/s", _baseStatsContainer);
                CreateStatRow("Range", $"{hitboxConfig.mechanics.hitboxSize.x:F1}m", _baseStatsContainer);
                CreateStatRow("Knockback", $"{hitboxConfig.mechanics.knockbackForce:F0}", _baseStatsContainer);
            }
            else if (weapon.typeSpecificConfig is CursorWeaponConfig cursorConfig)
            {
                CreateStatRow("Orbit Range", $"{cursorConfig.mechanics.minOrbitRadius:F1}-{cursorConfig.mechanics.maxOrbitRadius:F1}m", _baseStatsContainer);
                CreateStatRow("Control Speed", $"{cursorConfig.mechanics.cursorFollowSpeed:F0}", _baseStatsContainer);
                CreateStatRow("Mode", cursorConfig.mechanics.movementMode.ToString(), _baseStatsContainer);
            }
            else if (weapon.typeSpecificConfig is AutoAttackWeaponConfig autoConfig)
            {
                CreateStatRow("Attack Rate", $"{1f / autoConfig.mechanics.attackInterval:F1}/s", _baseStatsContainer);
                CreateStatRow("Auto Range", $"{autoConfig.mechanics.detectionRadius:F1}m", _baseStatsContainer);
                CreateStatRow("Trigger Speed", $"{autoConfig.mechanics.velocityThreshold:F0}m/s", _baseStatsContainer);
            }
        }

        // 2. Special Abilities
        if (_specialAbilitiesContainer != null)
        {
            // Weapon Type
            CreateAbilityItem($"Type: {weapon.weaponType}", _specialAbilitiesContainer);

            // Velocity Scaling
            if (mechanics != null && mechanics.scalesWithVelocity)
            {
                CreateAbilityItem($"Velocity Scaling: Up to {mechanics.maxVelocityMultiplier:F1}x damage", _specialAbilitiesContainer);
            }

            // Weapon-specific abilities
            if (weapon.typeSpecificConfig is HitboxWeaponConfig)
            {
                CreateAbilityItem("Sweeping Attack", _specialAbilitiesContainer);
                CreateAbilityItem("Can attack while moving", _specialAbilitiesContainer);
            }
            else if (weapon.typeSpecificConfig is CursorWeaponConfig)
            {
                CreateAbilityItem("Orbital Control", _specialAbilitiesContainer);
                CreateAbilityItem("Cursor-following", _specialAbilitiesContainer);
            }
            else if (weapon.typeSpecificConfig is AutoAttackWeaponConfig autoConfig)
            {
                CreateAbilityItem("Automatic Targeting", _specialAbilitiesContainer);
                if (autoConfig.mechanics.onlyActiveDuringGrapple)
                {
                    CreateAbilityItem($"Grapple Bonus: {autoConfig.mechanics.grappleDamageMultiplier:F1}x damage", _specialAbilitiesContainer);
                }
            }

            // Category
            CreateAbilityItem($"Category: {weapon.category}", _specialAbilitiesContainer);
        }
    }

    private void CreateStatRow(string statName, string statValue, Transform parent)
    {
        if (_statRowPrefab == null)
        {
            // Create simple stat row if no prefab
            var statGO = new GameObject($"Stat_{statName}");
            statGO.transform.SetParent(parent, false);

            var layout = statGO.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            // Stat name
            var nameText = new GameObject("Name");
            nameText.transform.SetParent(statGO.transform, false);
            var nameTMP = nameText.AddComponent<TextMeshProUGUI>();
            nameTMP.text = $"{statName}: ";
            nameTMP.fontSize = 16;
            nameTMP.color = new Color(0.8f, 0.8f, 0.8f, 1f);

            // Stat value
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
            // Use prefab if available
            var statRow = Instantiate(_statRowPrefab, parent);
            var statUI = statRow.GetComponent<StatRowUI>();
            if (statUI != null)
            {
                statUI.SetStat(statName, statValue);
            }
        }
    }

    private void CreateAbilityItem(string abilityText, Transform parent)
    {
        if (_abilityPrefab == null)
        {
            // Create simple ability item if no prefab
            var abilityGO = new GameObject($"Ability");
            abilityGO.transform.SetParent(parent, false);

            var abilityTMP = abilityGO.AddComponent<TextMeshProUGUI>();
            abilityTMP.text = $"• {abilityText}";
            abilityTMP.fontSize = 14;
            abilityTMP.color = new Color(0.7f, 0.9f, 1f, 1f);
        }
        else
        {
            // Use prefab if available
            var abilityItem = Instantiate(_abilityPrefab, parent);
            var abilityUI = abilityItem.GetComponent<AbilityItemUI>();
            if (abilityUI != null)
            {
                abilityUI.SetAbility(abilityText);
            }
        }
    }

    private void ClearStatsUI()
    {
        // Clear base stats
        if (_baseStatsContainer != null)
        {
            foreach (Transform child in _baseStatsContainer)
            {
                Destroy(child.gameObject);
            }
        }

        // Clear abilities
        if (_specialAbilitiesContainer != null)
        {
            foreach (Transform child in _specialAbilitiesContainer)
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void ClearWeaponDisplay()
    {
        _selectedWeapon = null;

        if (_weaponSprite != null)
        {
            _weaponSprite.sprite = null;
            _weaponSprite.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        }

        if (_weaponNameText != null)
        {
            _weaponNameText.text = "No weapon selected";
        }

        if (_equippedIndicator != null)
        {
            _equippedIndicator.SetActive(false);
        }

        ClearStatsUI();
    }

    private void ClearAllUI()
    {
        // Clear weapon slots
        foreach (var slot in _weaponSlots)
        {
            if (slot != null && slot.gameObject != null)
            {
                Destroy(slot.gameObject);
            }
        }
        _weaponSlots.Clear();

        // Clear weapon display
        ClearWeaponDisplay();
    }

    private void OnEquipButtonClicked()
    {
        if (_selectedWeapon == null || _playerEquipment == null) return;

        // Equip the weapon
        bool success = _playerEquipment.EquipWeapon(_selectedWeapon);

        if (success)
        {
            // Update all slots
            foreach (var slot in _weaponSlots)
            {
                slot.SetEquipped(slot.ItemData == _selectedWeapon);
            }

            // Update equipped indicator
            if (_equippedIndicator != null)
            {
                _equippedIndicator.SetActive(true);
            }
        }
    }
}