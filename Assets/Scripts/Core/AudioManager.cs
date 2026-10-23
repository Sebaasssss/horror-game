using System.Collections.Generic;
using UnityEngine;

// Pooled one-shot effects and a single music channel. Clips live in Resources/Audio/SFX and Resources/Audio/Music.
// A key may have variants named key_1, key_2, ... and one is picked at random.
public class AudioManager
{
    private readonly GameObject root;
    private readonly AudioSource[] voices;
    private readonly AudioSource music;
    private readonly Dictionary<string, AudioClip[]> cache = new Dictionary<string, AudioClip[]>();
    private readonly HashSet<string> missing = new HashSet<string>();
    private int nextVoice;
    private string currentMusic;

    public float SfxVolume = 0.9f;
    public float MusicVolume = 0.55f;

    public AudioManager(Transform parent)
    {
        root = new GameObject("Audio");
        root.transform.SetParent(parent, false);
        voices = new AudioSource[16];
        for (int i = 0; i < voices.Length; i++)
        {
            voices[i] = root.AddComponent<AudioSource>();
            voices[i].playOnAwake = false;
        }
        music = root.AddComponent<AudioSource>();
        music.playOnAwake = false;
        music.loop = true;
    }

    public void Sfx(string key, float volume = 1f, float pitchJitter = 0.06f)
    {
        if (string.IsNullOrEmpty(key))
            return;
        AudioClip clip = Pick("Audio/SFX/", key);
        if (clip == null)
            return;

        AudioSource voice = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;
        voice.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        voice.PlayOneShot(clip, volume * SfxVolume);
    }

    public void Music(string key)
    {
        if (currentMusic == key && music.isPlaying)
            return;
        currentMusic = key;
        // Screens without their own track fall back to the menu theme, then the fight theme.
        AudioClip clip = Pick("Audio/Music/", key) ?? Pick("Audio/Music/", "menu") ?? Pick("Audio/Music/", "fight");
        if (clip != null && clip == music.clip && music.isPlaying)
            return;
        music.Stop();
        if (clip == null)
            return;
        music.clip = clip;
        music.volume = MusicVolume;
        music.Play();
    }

    public void StopMusic()
    {
        currentMusic = null;
        music.Stop();
    }

    private AudioClip Pick(string folder, string key)
    {
        string path = folder + key;
        if (!cache.TryGetValue(path, out AudioClip[] clips))
        {
            List<AudioClip> found = new List<AudioClip>();
            AudioClip single = Resources.Load<AudioClip>(path);
            if (single != null)
                found.Add(single);
            for (int i = 1; i <= 8; i++)
            {
                AudioClip variant = Resources.Load<AudioClip>(path + "_" + i);
                if (variant == null)
                    break;
                found.Add(variant);
            }
            clips = found.ToArray();
            cache[path] = clips;
        }

        if (clips.Length == 0)
        {
            if (folder.EndsWith("SFX/") && missing.Add(path))
                Debug.Log("Audio not found yet: Resources/" + path);
            return null;
        }
        return clips[Random.Range(0, clips.Length)];
    }
}
