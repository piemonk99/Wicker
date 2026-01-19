using UnityEngine;

public abstract class BaseEquipmentPage : MonoBehaviour
{
    public abstract void Initialize();
    public abstract void OnPageShown();
    public abstract void OnPageHidden();
    public abstract void RefreshData();
}