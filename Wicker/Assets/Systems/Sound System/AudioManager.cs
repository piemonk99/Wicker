using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

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
                _instance = FindObjectOfType<AudioManager>();
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

    private AudioSource bgmSource;
    private Queue<AudioSource> sfxPool = new Queue<AudioSource>();
    private List<ActiveAudioSource> activeSfxSources = new List<ActiveAudioSource>();
    private int poolSize = 10;
    private Dictionary<string, SoundNode> nodePathCache = new Dictionary<string, SoundNode>();

    private List<AudioSource> sourcesToRemove = new List<AudioSource>();

    // Helper struct to track audio source info
    private struct ActiveAudioSource
    {
        public AudioSource source;
        public SoundCategory category;

        public ActiveAudioSource(AudioSource source, SoundCategory category)
        {
            this.source = source;
            this.category = category;
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

        InitializeAudioSources();

        if (rootSoundNode != null)
        {
            rootSoundNode.Initialize();
            BuildPathCache(rootSoundNode, "");
        }

        LoadVolumeSettings();
    }

    // ========== PATH CACHE SYSTEM ==========

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

    // ========== NODE LOOKUP METHODS ==========

    public SoundNode GetNode(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        // Check path cache first
        if (nodePathCache.TryGetValue(path, out var cachedNode))
        {
            return cachedNode;
        }

        // Try to find by traversing the tree
        return FindNodeByPath(rootSoundNode, path);
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

        // Check if current node matches
        if (currentNode.nodeID == targetID)
        {
            // If this is the last segment, we found it!
            if (currentIndex == pathSegments.Length - 1)
            {
                return currentNode;
            }

            // Otherwise, search children
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

    // ========== SOUND PLAYBACK METHODS ==========

    public AudioSource PlaySoundByPath(string path)
    {
        var node = GetNode(path);
        if (node != null)
        {
            return PlaySoundNode(node);
        }

        Debug.LogWarning($"Sound node not found at path: {path}");
        return null;
    }

    public AudioSource PlaySoundNode(SoundNode node)
    {
        if (node == null) return null;

        // If it's a container, get the next sound from it
        if (node.nodeType == SoundNodeType.Container)
        {
            var soundNode = node.GetNextNode();
            if (soundNode != null)
            {
                return PlaySoundNode(soundNode);
            }
            return null;
        }

        // It's a sound node - play it
        return PlaySoundData(node);
    }

    public AudioSource PlaySoundData(SoundNode soundNode)
    {
        return PlaySoundData(soundNode, Vector3.zero);
    }

    public AudioSource PlaySoundData(SoundNode soundNode, Vector3 position)
    {
        if (soundNode == null || soundNode.clip == null) return null;

        float finalVolume = soundNode.baseVolume * GetEffectiveVolume(soundNode.category);
        float finalPitch = soundNode.pitch;

        // Apply variations
        if (soundNode.volumeVariation > 0)
        {
            finalVolume *= (1f + UnityEngine.Random.Range(-soundNode.volumeVariation, soundNode.volumeVariation));
        }

        if (soundNode.pitchVariation > 0)
        {
            finalPitch += UnityEngine.Random.Range(-soundNode.pitchVariation, soundNode.pitchVariation);
        }

        if (soundNode.category == SoundCategory.BGM)
        {
            return PlayBGMInternal(soundNode, finalVolume, finalPitch);
        }
        else
        {
            return PlaySFXInternal(soundNode, finalVolume, finalPitch, position);
        }
    }

    private AudioSource PlayBGMInternal(SoundNode soundNode, float volume, float pitch)
    {
        bgmSource.clip = soundNode.clip;
        bgmSource.volume = volume;
        bgmSource.pitch = pitch;
        bgmSource.loop = soundNode.loop;
        bgmSource.Play();
        return bgmSource;
    }

    private AudioSource PlaySFXInternal(SoundNode soundNode, float volume, float pitch, Vector3 position)
    {
        AudioSource source = GetAvailableSfxSource();
        if (source == null) return null;

        source.clip = soundNode.clip;
        source.volume = volume;
        source.pitch = pitch;
        source.loop = soundNode.loop;

        if (position != Vector3.zero)
        {
            source.transform.position = position;
            source.spatialBlend = 1f; // 3D sound
        }
        else
        {
            source.spatialBlend = 0f; // 2D sound
        }

        source.Play();

        // Track this source with its category
        activeSfxSources.Add(new ActiveAudioSource(source, soundNode.category));

        if (!soundNode.loop)
        {
            StartCoroutine(ReturnToPoolWhenFinished(source));
        }

        return source;
    }

    // ========== BGM CONTROL METHODS ==========

    public void PlayBGM(string bgmPath)
    {
        var node = GetNode(bgmPath);
        if (node != null && node.nodeType == SoundNodeType.Sound)
        {
            PlaySoundNode(node);
        }
    }

    public void StopBGM()
    {
        if (bgmSource != null) bgmSource.Stop();
    }

    public void PauseBGM()
    {
        if (bgmSource != null) bgmSource.Pause();
    }

    public void ResumeBGM()
    {
        if (bgmSource != null) bgmSource.UnPause();
    }

    public void SetBGMVolume(float volume)
    {
        if (bgmSource != null)
        {
            bgmSource.volume = volume * bgmVolume * masterVolume;
        }
    }

    // ========== VOLUME CONTROL METHODS ==========

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
        // Clear any pending removals
        if (sourcesToRemove.Count > 0)
        {
            foreach (var source in sourcesToRemove)
            {
                ReturnSourceToPool(source);
            }
            sourcesToRemove.Clear();
        }

        // Update BGM source
        if (bgmSource != null && bgmSource.isPlaying)
        {
            bgmSource.volume = GetEffectiveVolume(SoundCategory.BGM);
        }

        // Update all active SFX sources
        for (int i = activeSfxSources.Count - 1; i >= 0; i--)
        {
            var activeSource = activeSfxSources[i];
            if (activeSource.source == null || !activeSource.source.isPlaying)
            {
                // Mark for removal instead of removing immediately
                if (activeSource.source != null)
                {
                    sourcesToRemove.Add(activeSource.source);
                }
                // Remove from active list now
                activeSfxSources.RemoveAt(i);
                continue;
            }

            activeSource.source.volume = GetEffectiveVolume(activeSource.category);
        }
    }

    // ========== AUDIO SOURCE MANAGEMENT ==========

    private void InitializeAudioSources()
    {
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.spatialBlend = 0f; // 2D sound

        for (int i = 0; i < poolSize; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            sfxPool.Enqueue(source);
        }
    }

    private AudioSource GetAvailableSfxSource()
    {
        if (sfxPool.Count > 0)
        {
            return sfxPool.Dequeue();
        }

        // Create new source if pool is empty
        AudioSource newSource = gameObject.AddComponent<AudioSource>();
        newSource.playOnAwake = false;
        return newSource;
    }

    private void ReturnSourceToPool(AudioSource source)
    {
        if (source == null) return;

        source.Stop();
        source.clip = null;
        source.spatialBlend = 0f;
        source.transform.localPosition = Vector3.zero;

        // Don't add back if already in pool
        if (!sfxPool.Contains(source))
        {
            sfxPool.Enqueue(source);
        }
    }

    private IEnumerator ReturnToPoolWhenFinished(AudioSource source)
    {
        if (source == null || source.clip == null) yield break;

        float duration = source.clip.length / source.pitch;
        yield return new WaitForSeconds(duration + 0.1f);

        if (source != null && source.isPlaying == false)
        {
            ReturnSourceToPool(source);
        }
    }

    // ========== SAVE/LOAD SYSTEM ==========

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

    // ========== PUBLIC UTILITY METHODS ==========

    public void StopAllSFX()
    {
        for (int i = activeSfxSources.Count - 1; i >= 0; i--)
        {
            var activeSource = activeSfxSources[i];
            if (activeSource.source != null)
            {
                ReturnSourceToPool(activeSource.source);
            }
        }
        activeSfxSources.Clear();
    }

    public void StopSFXByCategory(SoundCategory category)
    {
        for (int i = activeSfxSources.Count - 1; i >= 0; i--)
        {
            var activeSource = activeSfxSources[i];
            if (activeSource.category == category && activeSource.source != null)
            {
                ReturnSourceToPool(activeSource.source);
                activeSfxSources.RemoveAt(i);
            }
        }
    }

    public void FadeOutBGM(float duration)
    {
        if (bgmSource != null && bgmSource.isPlaying)
        {
            StartCoroutine(FadeOutBGMCoroutine(duration));
        }
    }

    public void FadeInBGM(string bgmPath, float duration)
    {
        StopAllCoroutines();
        StartCoroutine(FadeInBGMCoroutine(bgmPath, duration));
    }

    private IEnumerator FadeOutBGMCoroutine(float duration)
    {
        float startVolume = bgmSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }

        bgmSource.Stop();
        bgmSource.volume = startVolume;
    }

    private IEnumerator FadeInBGMCoroutine(string bgmPath, float duration)
    {
        PlayBGM(bgmPath);
        float targetVolume = GetEffectiveVolume(SoundCategory.BGM);

        bgmSource.volume = 0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(0f, targetVolume, elapsed / duration);
            yield return null;
        }

        bgmSource.volume = targetVolume;
    }

    // ========== DEBUG/EDITOR METHODS ==========

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Update volumes in editor when changed
        UpdateAllVolumes();
    }
    
    public void PrintSoundTree()
    {
        if (rootSoundNode == null)
        {
            Debug.Log("No root sound node assigned.");
            return;
        }
        
        Debug.Log("=== Sound Tree Structure ===");
        PrintNodeRecursive(rootSoundNode, 0);
    }
    
    private void PrintNodeRecursive(SoundNode node, int indent)
    {
        string indentStr = new string(' ', indent * 2);
        string nodeType = node.nodeType == SoundNodeType.Container ? "[Container]" : "[Sound]";
        
        Debug.Log($"{indentStr}{nodeType} {node.nodeID} {(node.nodeType == SoundNodeType.Sound ? $"(Clip: {node.clip?.name})" : "")}");
        
        if (node.nodeType == SoundNodeType.Container)
        {
            foreach (var child in node.childNodes)
            {
                if (child != null)
                {
                    PrintNodeRecursive(child, indent + 1);
                }
            }
        }
    }
#endif

    private void Start()
    {
        // Ensure volumes are applied on start
        UpdateAllVolumes();
    }

    private void Update()
    {
        // Periodically clean up finished audio sources
        if (Time.frameCount % 60 == 0) // Every 60 frames
        {
            UpdateAllVolumes();
        }
    }
}