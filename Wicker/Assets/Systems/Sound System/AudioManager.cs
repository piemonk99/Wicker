using System;
using System.Collections.Generic;
using UnityEngine;

public enum SoundCategory { Master, BGM, SFX, Ambient, UI, Voice }

[System.Serializable]
public class GameSound
{
    public string name;
    public AudioClip clip;
    public SoundCategory category = SoundCategory.SFX;
    [Range(0f, 1f)] public float baseVolume = 1f;
    [Range(0.1f, 3f)] public float pitch = 1f;
    public bool loop = false;
}

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

    [Header("Volume Settings")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float ambientVolume = 1f;

    [Header("Sound Library")]
    [SerializeField] private List<GameSound> soundLibrary = new List<GameSound>();

    // Event for volume changes
    public event Action<SoundCategory> OnVolumeChanged;

    // AudioSource for BGM (music)
    private AudioSource bgmSource;

    // Pool for SFX
    private Queue<AudioSource> sfxPool = new Queue<AudioSource>();
    private List<AudioSource> activeSfxSources = new List<AudioSource>();
    private int poolSize = 10;

    // Dictionary for sound lookup
    private Dictionary<string, GameSound> soundDictionary = new Dictionary<string, GameSound>();

    private void Awake()
    {
        // Singleton setup
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeAudioSources();
        BuildSoundDictionary();
        LoadVolumeSettings();
    }

    private void InitializeAudioSources()
    {
        // Create BGM source
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;

        // Create SFX pool
        for (int i = 0; i < poolSize; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            sfxPool.Enqueue(source);
        }
    }

    private void BuildSoundDictionary()
    {
        soundDictionary.Clear();
        foreach (GameSound sound in soundLibrary)
        {
            if (!string.IsNullOrEmpty(sound.name) && sound.clip != null)
            {
                if (!soundDictionary.ContainsKey(sound.name))
                {
                    soundDictionary.Add(sound.name, sound);
                }
            }
        }
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

    private void UpdateAllVolumes()
    {
        // Update BGM source
        if (bgmSource != null && bgmSource.isPlaying)
        {
            bgmSource.volume = masterVolume * bgmVolume;
        }

        // Update all active SFX sources
        foreach (AudioSource source in activeSfxSources)
        {
            if (source != null)
            {
                source.volume = masterVolume * sfxVolume;
            }
        }
    }

    // ========== SOUND PLAYBACK ==========
    public AudioSource PlaySound(string soundName)
    {
        if (soundDictionary.TryGetValue(soundName, out GameSound sound))
        {
            return PlaySound(sound);
        }

        Debug.LogWarning($"Sound not found: {soundName}");
        return null;
    }

    public AudioSource PlaySound(GameSound sound)
    {
        if (sound == null || sound.clip == null) return null;

        if (sound.category == SoundCategory.BGM)
        {
            // Play as BGM
            bgmSource.clip = sound.clip;
            bgmSource.volume = masterVolume * bgmVolume * sound.baseVolume;
            bgmSource.pitch = sound.pitch;
            bgmSource.loop = sound.loop;
            bgmSource.Play();
            return bgmSource;
        }
        else
        {
            // Play as SFX (or other category)
            AudioSource source = GetAvailableSfxSource();
            if (source == null) return null;

            source.clip = sound.clip;
            source.volume = masterVolume * sfxVolume * sound.baseVolume;
            source.pitch = sound.pitch;
            source.loop = sound.loop;
            source.Play();

            if (!sound.loop)
            {
                StartCoroutine(ReturnToPoolWhenFinished(source));
            }

            return source;
        }
    }

    private AudioSource GetAvailableSfxSource()
    {
        if (sfxPool.Count > 0)
        {
            AudioSource source = sfxPool.Dequeue();
            activeSfxSources.Add(source);
            return source;
        }

        // Create new source if pool is empty
        AudioSource newSource = gameObject.AddComponent<AudioSource>();
        activeSfxSources.Add(newSource);
        return newSource;
    }

    private System.Collections.IEnumerator ReturnToPoolWhenFinished(AudioSource source)
    {
        yield return new WaitForSeconds(source.clip.length / source.pitch + 0.1f);

        if (source != null && activeSfxSources.Contains(source))
        {
            activeSfxSources.Remove(source);
            source.Stop();
            source.clip = null;
            sfxPool.Enqueue(source);
        }
    }

    // ========== BGM CONTROL ==========
    public void PlayBGM(string bgmName)
    {
        if (soundDictionary.TryGetValue(bgmName, out GameSound sound))
        {
            PlayBGM(sound);
        }
    }

    public void PlayBGM(GameSound bgmSound)
    {
        if (bgmSound == null) return;
        bgmSound.category = SoundCategory.BGM;
        bgmSound.loop = true;
        PlaySound(bgmSound);
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

    private void Start()
    {
        // Ensure volumes are applied
        UpdateAllVolumes();
    }
}