// WeaponSoundManager.cs
using UnityEngine;
using System.Collections;

/// <summary>
/// Manages weapon sounds using borrowed AudioSources from AudioManager.
/// Similar to GrappleSoundManager structure.
/// </summary>
public class WeaponSoundManager
{
    private WeaponSoundConfig config;
    private MonoBehaviour coroutineHost;
    private AudioSource borrowedAudioSource;
    private float lastSwingTime = 0f;

    public WeaponSoundManager(WeaponSoundConfig config, MonoBehaviour coroutineHost)
    {
        this.config = config;
        this.coroutineHost = coroutineHost;
    }

    /// <summary>
    /// Play the weapon swing sound.
    /// </summary>
    public void PlaySwingSound(float velocityMagnitude = 0f, bool isCritical = false)
    {
        if (config == null || config.weaponSoundSet == null) return;

        lastSwingTime = Time.time;

        // Determine sound node based on velocity
        SoundNode soundNode = null;

        // Check for crit in auto-attack weapons
        var autoSoundConfig = config as AutoAttackWeaponSoundConfig;
        if (autoSoundConfig != null && isCritical)
        {
            soundNode = config.weaponSoundSet.GetChildNode("Crit");

            if (soundNode != null)
                Debug.Log($"Playing CRIT sound");
        }

        // Fallback to swing sound
        if (soundNode == null)
        {
            soundNode = config.weaponSoundSet.GetChildNode("Swing");
            if (soundNode == null)
            {
                // If no Swing node, use the weapon sound set itself
                soundNode = config.weaponSoundSet;
            }

            Debug.Log($"Playing swing sound");
        }

        // Play the sound
        if (soundNode != null)
        {
            AudioManager.Instance.Play(soundNode);
        }
    }

    /// <summary>
    /// Play a specific sound by node name.
    /// </summary>
    public void PlaySound(string nodeName, float volumeMultiplier = 1.0f)
    {
        if (config == null || config.weaponSoundSet == null) return;

        var node = config.weaponSoundSet.GetChildNode(nodeName);
        if (node != null)
        {
            AudioManager.Instance.Play(node);
        }
    }

    /// <summary>
    /// Play swoosh sound for cursor weapons when moving fast.
    /// </summary>
    public void PlaySwooshSound(float velocityMagnitude)
    {
        var cursorSoundConfig = config as CursorWeaponSoundConfig;
        if (cursorSoundConfig == null || cursorSoundConfig.swooshSound == null) return;

        if (velocityMagnitude > cursorSoundConfig.swooshVelocityThreshold)
        {
            AudioManager.Instance.Play(cursorSoundConfig.swooshSound); // does not use volume
        }
    }

    /// <summary>
    /// Clean up all resources.
    /// </summary>
    public void Cleanup()
    {
        // Nothing, for now. Will need to clean up borrowed sources when used.
    }
}