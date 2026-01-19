// StatRowUI.cs - Optional, for structured stat display
using TMPro;
using UnityEngine;

public class StatRowUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _statNameText;
    [SerializeField] private TextMeshProUGUI _statValueText;

    public void SetStat(string statName, string statValue)
    {
        if (_statNameText != null)
        {
            _statNameText.text = $"{statName}:";
        }

        if (_statValueText != null)
        {
            _statValueText.text = statValue;
        }
    }
}