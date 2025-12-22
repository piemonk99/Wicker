using System.Collections;
using UnityEngine;

public class GrappleSoundManager
{
    private GrappleSoundConfig config;
    private MonoBehaviour coroutineHost;
    private Coroutine creakCoroutine;
    private bool isCreaking = false;
    private AudioSource borrowedCreakSource;
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

        AudioManager.Instance.Play(config.grappleSoundSet.GetChildNode("Launch"));
    }

    /// <summary>
    /// Start continuous creak sounds with 3D positioning.
    /// </summary>
    public void StartCreakSounds()
    {
        if (isCreaking || config.grappleSoundSet == null) return;

        isCreaking = true;

        // Borrow an AudioSource
        borrowedCreakSource = AudioManager.Instance.BorrowSource();
        if (borrowedCreakSource == null)
        {
            Debug.LogWarning("Could not borrow AudioSource for creak sounds");
            isCreaking = false;
            return;
        }

        // Configure for 3D sound
        borrowedCreakSource.spatialBlend = config.creakSpatialBlend;
        borrowedCreakSource.minDistance = 1f;
        borrowedCreakSource.maxDistance = 20f;
        borrowedCreakSource.rolloffMode = AudioRolloffMode.Linear;

        // Start playback coroutine
        if (creakCoroutine != null)
        {
            coroutineHost.StopCoroutine(creakCoroutine);
        }

        creakCoroutine = coroutineHost.StartCoroutine(ContinuousCreakRoutine());
    }

    /// <summary>
    /// Stop all creak sounds and return borrowed AudioSource.
    /// </summary>
    public void StopCreakSounds()
    {
        isCreaking = false;

        if (creakCoroutine != null)
        {
            coroutineHost.StopCoroutine(creakCoroutine);
            creakCoroutine = null;
        }

        // Return borrowed AudioSource
        if (borrowedCreakSource != null)
        {
            AudioManager.Instance.ReturnSource(borrowedCreakSource);
            borrowedCreakSource = null;
        }
    }

    /// <summary>
    /// Update creak volume based on restoring force.
    /// </summary>
    public void UpdateCreakVolume(float forceMagnitude, float minForce, float maxForce)
    {
        if (!isCreaking || borrowedCreakSource == null) return;

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
            currentNormalizedForce = (forceMagnitude - minForce) / forceRange;
        }

        // Calculate and set volume
        float targetVolume = Mathf.Lerp(config.creakMinVolume, config.creakMaxVolume, currentNormalizedForce);

        // Force volume to 0 when force is below threshold
        if (currentNormalizedForce < 0.01f)
            targetVolume = 0f;

        borrowedCreakSource.volume = Mathf.Lerp(borrowedCreakSource.volume, targetVolume, 0.8f);
    }

    /// <summary>
    /// Update the 3D position of the creak sound.
    /// </summary>
    public void UpdateCreakPosition(Vector3 grappleOrigin, Vector3 grapplePoint)
    {
        if (!isCreaking || borrowedCreakSource == null) return;

        Vector3 soundPosition = (grappleOrigin + grapplePoint) * 0.5f;
        borrowedCreakSource.transform.position = soundPosition;
    }

    /// <summary>
    /// Continuous creak playback using borrowed AudioSource.
    /// </summary>
    private IEnumerator ContinuousCreakRoutine()
    {
        var creakContainer = config.grappleSoundSet.GetChildNode("Creak");
        if (creakContainer == null) yield break;

        bool firstPlay = true;

        while (isCreaking && borrowedCreakSource != null)
        {
            // Get next creak sound from the container
            var soundNode = creakContainer.GetNextNode();
            if (soundNode?.nodeType != SoundNodeType.Sound || soundNode.clip == null)
            {
                yield return new WaitForSeconds(0.1f);
                continue;
            }

            // Configure AudioSource
            borrowedCreakSource.clip = soundNode.clip;
            borrowedCreakSource.pitch = soundNode.pitch;
            borrowedCreakSource.volume = Mathf.Lerp(config.creakMinVolume, config.creakMaxVolume, currentNormalizedForce);

            // Skip initial silence on first play
            if (firstPlay)
            {
                float clipLength = soundNode.clip.length / soundNode.pitch;
                borrowedCreakSource.time = Mathf.Min(0.5f, clipLength - 0.1f);
                firstPlay = false;
            }
            else
            {
                borrowedCreakSource.time = 0f;
            }

            borrowedCreakSource.Play();

            // Wait for sound to almost finish
            if (soundNode.clip != null)
            {
                float remainingTime = (soundNode.clip.length - borrowedCreakSource.time) / soundNode.pitch;
                yield return new WaitForSeconds(Mathf.Max(0.1f, remainingTime - 0.1f));
            }
            else
            {
                yield return new WaitForSeconds(1f);
            }
        }
    }

    /// <summary>
    /// Clean up all resources.
    /// </summary>
    public void Cleanup()
    {
        StopCreakSounds();
    }
}