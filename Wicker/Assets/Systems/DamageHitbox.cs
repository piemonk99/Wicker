using UnityEngine;

public class DamageHitbox : MonoBehaviour
{
    private int damage = 1;
    private GameObject owner;

    public void SetDamage(int amount) => damage = amount;
    public void SetOwner(GameObject ownerObject) => owner = ownerObject;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Don't hit owner
        if (other.gameObject == owner) return;

        // Try to damage the target
        var targetCore = other.GetComponent<CharacterCore>();
        if (targetCore != null)
        {
            targetCore.RaiseEvent("damage_taken", damage);
        }

        // Destroy self after hitting something
        Destroy(gameObject);
    }
}