using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Handles trigger collisions for the weapon and reports them to the weapon system
/// </summary>
public class WeaponCollisionHandler : MonoBehaviour
{
    private CursorWeaponSystem weaponSystem;
    private HashSet<Collider2D> currentCollisions = new HashSet<Collider2D>();

    /// <summary>
    /// Initialize with reference to the parent weapon system
    /// </summary>
    public void Initialize(CursorWeaponSystem system)
    {
        weaponSystem = system;

        // Ensure this GameObject has a Collider2D
        if (GetComponent<Collider2D>() == null)
        {
            Debug.LogWarning("Weapon prefab should have a Collider2D for normal collision detection");
        }
    }

    /// <summary>
    /// Report trigger enter events to weapon system
    /// </summary>
    void OnTriggerEnter2D(Collider2D other)
    {
        if (weaponSystem != null)
        {
            currentCollisions.Add(other);
            weaponSystem.OnWeaponTriggerEnter(other);
        }
    }

    /// <summary>
    /// Report trigger stay events to weapon system
    /// </summary>
    void OnTriggerStay2D(Collider2D other)
    {
        if (weaponSystem != null)
        {
            currentCollisions.Add(other);
            weaponSystem.OnWeaponTriggerStay(other);
        }
    }

    /// <summary>
    /// Report trigger exit events to weapon system
    /// </summary>
    void OnTriggerExit2D(Collider2D other)
    {
        currentCollisions.Remove(other);
    }

    /// <summary>
    /// Clear all current collisions (called when weapon stops swinging)
    /// </summary>
    public void ClearCollisions()
    {
        currentCollisions.Clear();
    }

    /// <summary>
    /// Get all colliders currently in contact with the weapon
    /// </summary>
    public HashSet<Collider2D> GetCurrentCollisions()
    {
        return new HashSet<Collider2D>(currentCollisions);
    }
}