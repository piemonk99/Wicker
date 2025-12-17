using System.Collections;
using UnityEngine;

/// <summary>
/// Minimal manager for grapple sounds with continuous creaking.
/// </summary>
public class GrappleSoundManager
{
    private GrappleSoundConfig config;
    private MonoBehaviour coroutineHost;
    private AudioSource creakSource;
    private Coroutine creakCoroutine;
    private bool isCreaking = false;
    private float currentNormalizedForce = 0f;

    public GrappleSoundManager(GrappleSoundConfig config, MonoBehaviour coroutineHost)
    {
        this.config = config;
        this.coroutineHost = coroutineHost;
    }

    /// <summary>
    /// Play the grapple launch sound.
    /// </summary>
    public void PlayLaunchSound()
    {
        if (config.grappleSoundSet == null) return;

        var launchNode = config.grappleSoundSet.GetChildNode("Launch");
        if (launchNode != null)
        {
            var soundNode = launchNode.GetNextNode();
            if (soundNode != null && soundNode.nodeType == SoundNodeType.Sound)
            {
                AudioManager.Instance.PlaySoundNode(soundNode);
            }
        }
    }

    /// <summary>
    /// Start continuous creak sounds.
    /// </summary>
    public void StartCreakSounds()
    {
        if (isCreaking || config.grappleSoundSet == null) return;

        isCreaking = true;

        // Create AudioSource if needed
        if (creakSource == null)
        {
            GameObject sourceObj = new GameObject("CreakSound");
            sourceObj.transform.SetParent(coroutineHost.transform);
            creakSource = sourceObj.AddComponent<AudioSource>();
        }

        // Start continuous creak playback
        if (creakCoroutine != null)
        {
            coroutineHost.StopCoroutine(creakCoroutine);
        }

        creakCoroutine = coroutineHost.StartCoroutine(ContinuousCreakRoutine());
    }

    /// <summary>
    /// Stop all creak sounds.
    /// </summary>
    public void StopCreakSounds()
    {
        isCreaking = false;

        if (creakCoroutine != null)
        {
            coroutineHost.StopCoroutine(creakCoroutine);
            creakCoroutine = null;
        }

        if (creakSource != null && creakSource.isPlaying)
        {
            creakSource.Stop();
        }
    }

    /// <summary>
    /// Update creak volume based on force magnitude.
    /// </summary>
    public void UpdateCreakVolume(float forceMagnitude, float minForce, float maxForce)
    {
        if (!isCreaking) return;

        // Calculate normalized force (0-1)
        if (forceMagnitude <= minForce)
        {
            currentNormalizedForce = 0f;
        }
        else if (forceMagnitude >= maxForce)
        {
            currentNormalizedForce = 1f;
        }
        else
        {
            float forceRange = maxForce - minForce;
            float forceWithinRange = forceMagnitude - minForce;
            currentNormalizedForce = forceWithinRange / forceRange;
        }

        // Update volume if we have a source
        if (creakSource != null)
        {
            float targetVolume = Mathf.Lerp(config.creakMinVolume, config.creakMaxVolume, currentNormalizedForce);
            creakSource.volume = Mathf.Lerp(creakSource.volume, targetVolume, 0.2f);
        }
    }

    /// <summary>
    /// Continuous creak playback - plays one after another.
    /// </summary>
    private IEnumerator ContinuousCreakRoutine()
    {
        // Skip to 0.5 seconds into the first sound
        bool firstPlay = true;

        while (isCreaking)
        {
            // Get next creak sound
            var creakNode = config.grappleSoundSet.GetChildNode("Creak");
            if (creakNode == null) yield break;

            var soundNode = creakNode.GetNextNode();
            if (soundNode == null || soundNode.nodeType != SoundNodeType.Sound || soundNode.clip == null)
                yield break;

            // Configure AudioSource
            creakSource.clip = soundNode.clip;
            creakSource.volume = config.creakMinVolume; // Start at minimum
            creakSource.pitch = soundNode.pitch;
            creakSource.loop = false; // We'll handle looping manually

            // Calculate clip length with pitch adjustment
            float clipLength = soundNode.clip.length / soundNode.pitch;

            if (firstPlay)
            {
                // Start 0.5 seconds into the first sound
                creakSource.time = Mathf.Min(0.5f, clipLength - 0.1f);
                firstPlay = false;
            }
            else
            {
                // Start from beginning for subsequent sounds
                creakSource.time = 0f;
            }

            // Play the sound
            creakSource.Play();

            // Wait until the sound is almost done (leave 0.1s overlap for smooth transition)
            float waitTime = clipLength - creakSource.time - 0.1f;
            if (waitTime > 0)
            {
                yield return new WaitForSeconds(waitTime);
            }
            else
            {
                // If wait time is negative or zero, wait a tiny bit
                yield return new WaitForSeconds(0.1f);
            }
        }
    }

    /// <summary>
    /// Clean up resources.
    /// </summary>
    public void Cleanup()
    {
        StopCreakSounds();

        if (creakSource != null)
        {
            Object.Destroy(creakSource.gameObject);
            creakSource = null;
        }
    }
}