// AbilityItemUI.cs - Optional, for structured ability display
using TMPro;
using UnityEngine;

public class AbilityItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _abilityText;

    public void SetAbility(string ability)
    {
        if (_abilityText != null)
        {
            _abilityText.text = $"• {ability}";
        }
    }
}