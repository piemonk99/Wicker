using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SoundCategory { Master, BGM, SFX, Ambient }

public class AudioManager : MonoBehaviour
{
    private static AudioManager _instance;
    public static AudioManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<AudioManager>();
                if (_instance == null)
                {
                    GameObject audioManagerObject = new GameObject("AudioManager");
                    _instance = audioManagerObject.AddComponent<AudioManager>();
                    DontDestroyOnLoad(audioManagerObject);
                }
            }
            return _instance;
        }
    }

    [Header("Sound System")]
    [SerializeField] private SoundNode rootSoundNode;

    [Header("Volume Settings")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float ambientVolume = 1f;

    public event Action<SoundCategory> OnVolumeChanged;

    // Single unified audio source pool
    private Queue<AudioSource> audioSourcePool = new Queue<AudioSource>();
    private List<ActiveAudioSource> activeSources = new List<ActiveAudioSource>();
    private int poolSize = 15; // Increased for BGM + SFX needs

    private Dictionary<string, SoundNode> nodePathCache = new Dictionary<string, SoundNode>();

    private struct ActiveAudioSource
    {
        public AudioSource source;
        public SoundCategory category;
        public float baseVolume; // For volume updates

        public ActiveAudioSource(AudioSource source, SoundCategory category, float baseVolume)
        {
            this.source = source;
            this.category = category;
            this.baseVolume = baseVolume;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeAudioPool();

        if (rootSoundNode != null)
        {
            rootSoundNode.Initialize();
            BuildPathCache(rootSoundNode, "");
        }

        LoadVolumeSettings();
    }

    // ========== NODE LOOKUP ==========

    public SoundNode GetNode(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        if (nodePathCache.TryGetValue(path, out var cachedNode))
        {
            return cachedNode;
        }

        return FindNodeByPath(rootSoundNode, path);
    }

    private void BuildPathCache(SoundNode node, string currentPath)
    {
        if (node == null) return;

        string nodePath = string.IsNullOrEmpty(currentPath) ? node.nodeID : currentPath + "/" + node.nodeID;

        if (!string.IsNullOrEmpty(node.nodeID))
        {
            nodePathCache[nodePath] = node;
        }

        if (node.nodeType == SoundNodeType.Container)
        {
            foreach (var child in node.childNodes)
            {
                if (child != null)
                {
                    BuildPathCache(child, nodePath);
                }
            }
        }
    }

    private SoundNode FindNodeByPath(SoundNode currentNode, string path)
    {
        if (currentNode == null || string.IsNullOrEmpty(path)) return null;

        string[] pathSegments = path.Split('/');
        return FindNodeRecursive(currentNode, pathSegments, 0);
    }

    private SoundNode FindNodeRecursive(SoundNode currentNode, string[] pathSegments, int currentIndex)
    {
        if (currentNode == null || currentIndex >= pathSegments.Length) return null;

        string targetID = pathSegments[currentIndex];

        if (currentNode.nodeID == targetID)
        {
            if (currentIndex == pathSegments.Length - 1)
            {
                return currentNode;
            }

            if (currentNode.nodeType == SoundNodeType.Container)
            {
                foreach (var child in currentNode.childNodes)
                {
                    var found = FindNodeRecursive(child, pathSegments, currentIndex + 1);
                    if (found != null) return found;
                }
            }
        }

        return null;
    }

    // ========== UNIFIED PLAYBACK API ==========

    /// <summary>
    /// Play a sound by path with optional parameters
    /// </summary>
    public AudioSource Play(string path,
        float volumeMultiplier = 1f,
        Vector3 position = default,
        SoundCategory category = SoundCategory.SFX,
        bool loop = false,
        float pitchMultiplier = 1f,
        bool allowMultiple = true)
    {
        var node = GetNode(path);
        if (node == null)
        {
            Debug.LogWarning($"Sound node not found at path: {path}");
            return null;
        }
        return Play(node, volumeMultiplier, position, category, loop, pitchMultiplier, allowMultiple);
    }

    /// <summary>
    /// Play a SoundNode with optional parameters
    /// </summary>
    public AudioSource Play(SoundNode node,
        float volumeMultiplier = 1f,
        Vector3 position = default,
        SoundCategory category = SoundCategory.SFX,
        bool loop = false,
        float pitchMultiplier = 1f,
        bool allowMultiple = true)
    {
        if (node == null)
        {
            Debug.LogWarning("Attempted to play null SoundNode");
            return null;
        }

        // If it's a container, get a sound from it
        if (node.nodeType == SoundNodeType.Container)
        {
            var soundNode = node.GetNextNode();
            if (soundNode != null)
            {
                return PlaySound(soundNode, volumeMultiplier, position, category, loop, pitchMultiplier, allowMultiple);
            }
            return null;
        }

        // It's a sound node - play it
        return PlaySound(node, volumeMultiplier, position, category, loop, pitchMultiplier, allowMultiple);
    }

    private AudioSource PlaySound(SoundNode soundNode,
        float volumeMultiplier,
        Vector3 position,
        SoundCategory category,
        bool loop,
        float pitchMultiplier,
        bool allowMultiple)
    {
        if (soundNode == null || soundNode.clip == null) return null;

        // Check if we should allow multiple instances
        if (!allowMultiple)
        {
            // Check if this sound is already playing
            foreach (var activeSource in activeSources)
            {
                if (activeSource.source != null &&
                    activeSource.source.clip == soundNode.clip &&
                    activeSource.source.isPlaying)
                {
                    return activeSource.source; // Return existing source
                }
            }
        }

        // Get an AudioSource from the pool
        AudioSource source = GetAvailableAudioSource();
        if (source == null)
        {
            Debug.LogWarning("No available AudioSource in pool");
            return null;
        }

        // Calculate final volume and pitch
        float finalVolume = soundNode.baseVolume * volumeMultiplier * GetEffectiveVolume(category);
        float finalPitch = soundNode.pitch * pitchMultiplier;

        // Apply variations
        if (soundNode.volumeVariation > 0)
        {
            finalVolume *= (1f + UnityEngine.Random.Range(-soundNode.volumeVariation, soundNode.volumeVariation));
        }

        if (soundNode.pitchVariation > 0)
        {
            finalPitch += UnityEngine.Random.Range(-soundNode.pitchVariation, soundNode.pitchVariation);
        }

        // Configure AudioSource
        source.clip = soundNode.clip;
        source.volume = finalVolume;
        source.pitch = finalPitch;
        source.loop = loop;

        // 3D positioning
        if (position != Vector3.zero)
        {
            source.transform.position = position;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 1f;
            source.maxDistance = 50f;
        }
        else
        {
            source.spatialBlend = 0f;
        }

        // Play
        source.Play();

        // Track for volume updates and cleanup
        activeSources.Add(new ActiveAudioSource(source, category, finalVolume));

        // Auto-return non-looping sounds to pool
        if (!loop)
        {
            StartCoroutine(ReturnToPoolWhenFinished(source));
        }

        return source;
    }

    // ========== AUDIO SOURCE MANAGEMENT ==========

    private void InitializeAudioPool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            audioSourcePool.Enqueue(source);
        }
    }

    private AudioSource GetAvailableAudioSource()
    {
        if (audioSourcePool.Count > 0)
        {
            return audioSourcePool.Dequeue();
        }

        // Expand pool dynamically if needed
        AudioSource newSource = gameObject.AddComponent<AudioSource>();
        newSource.playOnAwake = false;
        return newSource;
    }

    private IEnumerator ReturnToPoolWhenFinished(AudioSource source)
    {
        if (source == null || source.clip == null) yield break;

        float duration = source.clip.length / source.pitch;
        yield return new WaitForSeconds(duration + 0.1f);

        ReturnSourceToPool(source);
    }

    private void ReturnSourceToPool(AudioSource source)
    {
        if (source == null) return;

        // Reset to default state
        source.Stop();
        source.clip = null;
        source.volume = 1f;
        source.pitch = 1f;
        source.loop = false;
        source.spatialBlend = 0f;
        source.transform.localPosition = Vector3.zero;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = 1f;
        source.maxDistance = 100f;

        // Remove from active list
        for (int i = activeSources.Count - 1; i >= 0; i--)
        {
            if (activeSources[i].source == source)
            {
                activeSources.RemoveAt(i);
                break;
            }
        }

        // Return to pool
        if (!audioSourcePool.Contains(source))
        {
            audioSourcePool.Enqueue(source);
        }
    }

    // ========== BORROWING SYSTEM ==========

    /// <summary>
    /// Borrow an AudioSource for manual control
    /// </summary>
    public AudioSource BorrowSource()
    {
        AudioSource source = GetAvailableAudioSource();
        if (source == null) return null;

        // Remove from auto-management
        for (int i = activeSources.Count - 1; i >= 0; i--)
        {
            if (activeSources[i].source == source)
            {
                activeSources.RemoveAt(i);
                break;
            }
        }

        return source;
    }

    /// <summary>
    /// Return a borrowed AudioSource to the pool
    /// </summary>
    public void ReturnSource(AudioSource source)
    {
        ReturnSourceToPool(source);
    }

    // ========== VOLUME CONTROL ==========

    public float GetVolume(SoundCategory category)
    {
        switch (category)
        {
            case SoundCategory.Master: return masterVolume;
            case SoundCategory.BGM: return bgmVolume;
            case SoundCategory.SFX: return sfxVolume;
            case SoundCategory.Ambient: return ambientVolume;
            default: return 1f;
        }
    }

    public void SetVolume(SoundCategory category, float volume)
    {
        volume = Mathf.Clamp01(volume);

        switch (category)
        {
            case SoundCategory.Master: masterVolume = volume; break;
            case SoundCategory.BGM: bgmVolume = volume; break;
            case SoundCategory.SFX: sfxVolume = volume; break;
            case SoundCategory.Ambient: ambientVolume = volume; break;
        }

        UpdateAllVolumes();
        OnVolumeChanged?.Invoke(category);
        SaveVolumeSettings();
    }

    private float GetEffectiveVolume(SoundCategory category)
    {
        float master = GetVolume(SoundCategory.Master);

        switch (category)
        {
            case SoundCategory.BGM: return master * GetVolume(SoundCategory.BGM);
            case SoundCategory.SFX: return master * GetVolume(SoundCategory.SFX);
            case SoundCategory.Ambient: return master * GetVolume(SoundCategory.Ambient);
            default: return master;
        }
    }

    private void UpdateAllVolumes()
    {
        CleanupFinishedSources();

        // Update all active sources
        for (int i = activeSources.Count - 1; i >= 0; i--)
        {
            var activeSource = activeSources[i];
            if (activeSource.source == null)
            {
                activeSources.RemoveAt(i);
                continue;
            }

            if (activeSource.source.isPlaying)
            {
                activeSource.source.volume = activeSource.baseVolume * GetEffectiveVolume(activeSource.category);
            }
            else if (!activeSource.source.loop)
            {
                // Mark non-looping finished sources for cleanup
                ReturnSourceToPool(activeSource.source);
            }
        }
    }

    private void CleanupFinishedSources()
    {
        // Clean up any sources that might have finished outside our tracking
        for (int i = activeSources.Count - 1; i >= 0; i--)
        {
            var activeSource = activeSources[i];
            if (activeSource.source != null &&
                !activeSource.source.isPlaying &&
                !activeSource.source.loop)
            {
                ReturnSourceToPool(activeSource.source);
            }
        }
    }

    // ========== CONTROL METHODS ==========

    /// <summary>
    /// Stop all sounds of a specific category
    /// </summary>
    public void StopAll(SoundCategory? category = null)
    {
        for (int i = activeSources.Count - 1; i >= 0; i--)
        {
            var activeSource = activeSources[i];
            if (activeSource.source != null &&
                (category == null || activeSource.category == category))
            {
                ReturnSourceToPool(activeSource.source);
            }
        }
    }

    /// <summary>
    /// Stop a specific AudioSource
    /// </summary>
    public void Stop(AudioSource source)
    {
        if (source != null)
        {
            ReturnSourceToPool(source);
        }
    }

    /// <summary>
    /// Pause all sounds of a specific category
    /// </summary>
    public void PauseAll(SoundCategory? category = null)
    {
        foreach (var activeSource in activeSources)
        {
            if (activeSource.source != null &&
                activeSource.source.isPlaying &&
                (category == null || activeSource.category == category))
            {
                activeSource.source.Pause();
            }
        }
    }

    /// <summary>
    /// Resume all paused sounds of a specific category
    /// </summary>
    public void ResumeAll(SoundCategory? category = null)
    {
        foreach (var activeSource in activeSources)
        {
            if (activeSource.source != null &&
                !activeSource.source.isPlaying &&
                (category == null || activeSource.category == category))
            {
                activeSource.source.UnPause();
            }
        }
    }

    // ========== SAVE/LOAD ==========

    private void SaveVolumeSettings()
    {
        PlayerPrefs.SetFloat("MasterVolume", masterVolume);
        PlayerPrefs.SetFloat("BGMVolume", bgmVolume);
        PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
        PlayerPrefs.SetFloat("AmbientVolume", ambientVolume);
        PlayerPrefs.Save();
    }

    private void LoadVolumeSettings()
    {
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        bgmVolume = PlayerPrefs.GetFloat("BGMVolume", 1f);
        sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
        ambientVolume = PlayerPrefs.GetFloat("AmbientVolume", 1f);

        UpdateAllVolumes();
    }

    // ========== UPDATE LOOP ==========

    private void Start() => UpdateAllVolumes();

    private void Update()
    {
        // Periodic cleanup
        if (Time.frameCount % 60 == 0)
        {
            UpdateAllVolumes();
        }
    }

#if UNITY_EDITOR
    private void OnValidate() => UpdateAllVolumes();
    
    public void PrintSoundTree()
    {
        if (rootSoundNode != null) rootSoundNode.PrintBasicTree();
    }

    public void DebugPoolStatus()
    {
        Debug.Log($"Audio Pool Status: {activeSources.Count} active, {audioSourcePool.Count} available");
    }
#endif
}