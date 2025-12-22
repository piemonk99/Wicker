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
    public void PlaySwingSound(float velocityMagnitude = 0f)
    {
        if (config == null || config.weaponSoundSet == null) return;

        // Check cooldown
        if (Time.time - lastSwingTime < config.swingCooldown)
            return;

        lastSwingTime = Time.time;

        // Determine sound node based on velocity
        SoundNode soundNode = null;
        float volume = config.swingVolume;

        // Check for crit in auto-attack weapons
        var autoSoundConfig = config as AutoAttackWeaponSoundConfig;
        if (autoSoundConfig != null && velocityMagnitude > autoSoundConfig.critVelocityThreshold)
        {
            soundNode = config.weaponSoundSet.GetChildNode("Crit");
            volume = config.critVolume;

            if (soundNode != null)
                Debug.Log($"Playing CRIT sound (velocity: {velocityMagnitude:F1})");
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

            Debug.Log($"Playing swing sound (velocity: {velocityMagnitude:F1})");
        }

        // Play the sound
        if (soundNode != null)
        {
            AudioManager.Instance.PlaySoundNode(soundNode); // does not use volume
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
            AudioManager.Instance.PlaySoundNode(node); // does not use volume
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
            AudioManager.Instance.PlaySoundNode(cursorSoundConfig.swooshSound); // does not use volume
        }
    }

    /// <summary>
    /// Borrow an AudioSource for continuous sounds (if needed).
    /// </summary>
    public AudioSource BorrowAudioSource()
    {
        if (borrowedAudioSource != null) return borrowedAudioSource;

        borrowedAudioSource = AudioManager.Instance.BorrowAudioSource();
        return borrowedAudioSource;
    }

    /// <summary>
    /// Return borrowed AudioSource.
    /// </summary>
    public void ReturnAudioSource()
    {
        if (borrowedAudioSource != null)
        {
            AudioManager.Instance.ReturnAudioSource(borrowedAudioSource);
            borrowedAudioSource = null;
        }
    }

    /// <summary>
    /// Clean up all resources.
    /// </summary>
    public void Cleanup()
    {
        ReturnAudioSource();
    }
}