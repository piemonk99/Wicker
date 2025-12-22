// WeaponSoundManager.cs
using UnityEngine;
using System.Collections.Generic;

public class WeaponSoundManager : MonoBehaviour
{
    // Sound configuration
    private SoundNode weaponSoundSet;
    private float swingVolume = 1.0f;
    private float critVolume = 1.2f;
    private float critVelocityThreshold = 20f;
    private float swingCooldown = 0.1f;

    // Cached nodes for quick access
    private SoundNode swingNode;
    private SoundNode critNode;

    // State
    private bool isInitialized = false;
    private float lastSwingTime = 0f;

    /// <summary>
    /// Initialize with direct SoundNode reference.
    /// </summary>
    public void InitializeWithSoundNode(SoundNode soundNode, float swingVol = 1.0f, float critVol = 1.2f, float critThreshold = 20f)
    {
        if (soundNode == null)
        {
            Debug.LogWarning("WeaponSoundManager: No sound node provided");
            return;
        }

        weaponSoundSet = soundNode;
        swingVolume = swingVol;
        critVolume = critVol;
        critVelocityThreshold = critThreshold;

        // Cache sound nodes
        CacheSoundNodes();

        isInitialized = true;

        Debug.Log($"WeaponSoundManager initialized with sound node: {weaponSoundSet.nodeID}");
    }

    /// <summary>
    /// Initialize from WeaponSoundConfig.
    /// </summary>
    public void InitializeFromConfig(WeaponSoundConfig soundConfig)
    {
        if (soundConfig == null || soundConfig.weaponSoundSet == null)
        {
            Debug.LogWarning("WeaponSoundManager: No sound config or sound set provided");
            return;
        }

        weaponSoundSet = soundConfig.weaponSoundSet;
        swingVolume = soundConfig.swingVolume;
        critVolume = soundConfig.critVolume;
        swingCooldown = soundConfig.swingCooldown;

        CacheSoundNodes();
        isInitialized = true;
    }

    private void CacheSoundNodes()
    {
        if (weaponSoundSet == null) return;

        // Try to find Swing node
        swingNode = FindSoundNode("Swing");
        if (swingNode == null)
        {
            // Fallback: use the weapon sound set itself if it's a container with sounds
            if (weaponSoundSet.nodeType == SoundNodeType.Container && weaponSoundSet.childNodes.Count > 0)
            {
                swingNode = weaponSoundSet;
                Debug.Log($"No 'Swing' node found, using root node: {swingNode.nodeID}");
            }
            else
            {
                Debug.LogWarning($"No valid swing node found in {weaponSoundSet.nodeID}");
            }
        }

        // Try to find Crit node
        critNode = FindSoundNode("Crit");
        if (critNode == null)
        {
            Debug.Log($"No 'Crit' node found in {weaponSoundSet.nodeID}");
        }

        if (swingNode != null)
        {
            Debug.Log($"Cached swing node: {swingNode.nodeID} (type: {swingNode.nodeType})");
        }
        if (critNode != null)
        {
            Debug.Log($"Cached crit node: {critNode.nodeID} (type: {critNode.nodeType})");
        }
    }

    private SoundNode FindSoundNode(string nodeName)
    {
        if (weaponSoundSet == null) return null;

        // First, try to get it directly if it's a child
        var node = weaponSoundSet.GetChildNode(nodeName);
        if (node != null) return node;

        // If not found, search recursively in the subtree
        return weaponSoundSet.FindNodeInSubtree(nodeName);
    }

    /// <summary>
    /// Play swing sound based on current velocity.
    /// </summary>
    public void PlaySwingSound(float velocityMagnitude = 0f)
    {
        if (!isInitialized) return;

        // Check cooldown
        if (Time.time - lastSwingTime < swingCooldown)
            return;

        lastSwingTime = Time.time;

        // Determine if this is a crit based on velocity
        bool isCrit = velocityMagnitude > critVelocityThreshold;

        // Choose which node to play
        SoundNode nodeToPlay = null;
        float volume = swingVolume;

        if (isCrit && critNode != null)
        {
            nodeToPlay = critNode;
            volume = critVolume;
            Debug.Log($"Playing CRIT sound (velocity: {velocityMagnitude:F1} > {critVelocityThreshold})");
        }
        else if (swingNode != null)
        {
            nodeToPlay = swingNode;
            Debug.Log($"Playing swing sound (velocity: {velocityMagnitude:F1})");
        }

        // Play the sound
        if (nodeToPlay != null)
        {
            PlaySoundNode(nodeToPlay, volume);
        }
        else
        {
            Debug.LogWarning("No sound node available to play");
        }
    }

    /// <summary>
    /// Play a specific sound by node name.
    /// </summary>
    public void PlaySound(string nodeName, float volumeMultiplier = 1.0f)
    {
        if (!isInitialized || weaponSoundSet == null) return;

        var node = FindSoundNode(nodeName);
        if (node != null)
        {
            PlaySoundNode(node, volumeMultiplier);
        }
        else
        {
            Debug.LogWarning($"Sound node '{nodeName}' not found in {weaponSoundSet.nodeID}");
        }
    }

    /// <summary>
    /// Play a specific SoundNode.
    /// </summary>
    private void PlaySoundNode(SoundNode node, float volumeMultiplier = 1.0f)
    {
        if (node == null) return;

        // Get the actual sound node to play (could be a container that selects a child)
        SoundNode soundToPlay = node;
        if (node.nodeType == SoundNodeType.Container)
        {
            soundToPlay = node.GetNextNode();
            if (soundToPlay == null)
            {
                Debug.LogWarning($"Container node {node.nodeID} returned null child");
                return;
            }
        }

        // Play the sound using AudioManager
        if (AudioManager.Instance != null)
        {
            // Adjust volume based on node's base volume and our multiplier
            float finalVolume = Mathf.Clamp(soundToPlay.baseVolume * volumeMultiplier, 0f, 1f);
            AudioManager.Instance.PlaySoundNode(soundToPlay);
        }
        else
        {
            Debug.LogWarning("AudioManager.Instance is null - cannot play sound");

            // Fallback: log what would have been played
            Debug.Log($"Would play: {soundToPlay.nodeID} (clip: {soundToPlay.clip?.name}, volume: {soundToPlay.baseVolume * volumeMultiplier:F2})");
        }
    }

    /// <summary>
    /// Check if ready to play sounds.
    /// </summary>
    public bool IsReady()
    {
        return isInitialized && weaponSoundSet != null;
    }

    /// <summary>
    /// Get debug info about the sound setup.
    /// </summary>
    public string GetDebugInfo()
    {
        if (!isInitialized) return "Not initialized";

        string swingInfo = swingNode != null ?
            $"{swingNode.nodeID} ({swingNode.nodeType})" : "Not found";
        string critInfo = critNode != null ?
            $"{critNode.nodeID} ({critNode.nodeType})" : "Not found";

        return $"Weapon Sound Manager:\n" +
               $"Sound Set: {weaponSoundSet.nodeID}\n" +
               $"Swing Node: {swingInfo}\n" +
               $"Crit Node: {critInfo}\n" +
               $"Swing Volume: {swingVolume}\n" +
               $"Crit Volume: {critVolume}\n" +
               $"Crit Threshold: {critVelocityThreshold}";
    }
}