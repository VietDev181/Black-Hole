using UnityEngine;

/// <summary>Music with cross-fade + a small round-robin pool of SFX sources. Created in code.</summary>
public class AudioService : MonoBehaviour
{
    private const int SfxVoices = 8;
    private const float MusicVolume = 0.5f;
    private const float SfxVolume = 0.85f;

    private BlackHoleCatalog cat;
    private AudioSource music;
    private AudioSource[] voices;
    private int nextVoice;
    private AudioClip wantedClip;
    private float musicLevel;

    public bool MusicOn { get; private set; }
    public bool SfxOn { get; private set; }

    public void Init(BlackHoleCatalog catalog)
    {
        cat = catalog;
        music = gameObject.AddComponent<AudioSource>();
        music.loop = true;
        music.playOnAwake = false;
        music.volume = 0f;

        voices = new AudioSource[SfxVoices];
        for (int i = 0; i < SfxVoices; i++)
        {
            voices[i] = gameObject.AddComponent<AudioSource>();
            voices[i].playOnAwake = false;
        }

        MusicOn = SaveData.MusicOn;
        SfxOn = SaveData.SfxOn;
    }

    public void PlayMenuMusic() => wantedClip = cat.menuMusic;
    public void PlayGameMusic() => wantedClip = cat.gameMusic;
    public void PlayGameOverMusic() => wantedClip = cat.gameOverMusic != null ? cat.gameOverMusic : cat.menuMusic;

    public void SetMusic(bool on)
    {
        MusicOn = on;
        SaveData.MusicOn = on;
    }

    public void SetSfx(bool on)
    {
        SfxOn = on;
        SaveData.SfxOn = on;
    }

    public void Click() => Play(cat.clickSfx, 0.7f, 1f);
    public void Eat(float pitch) => Play(cat.eatSfx, 0.9f, pitch);
    public void Coin() => Play(cat.coinSfx, 0.9f, 1f);
    public void Grow() => Play(cat.growSfx, 1f, 0.9f);
    public void Boom() => Play(cat.eatSfx, 1f, 0.45f);

    public void Play(AudioClip clip, float volume, float pitch)
    {
        if (!SfxOn || clip == null) return;
        var v = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;
        v.pitch = pitch;
        v.PlayOneShot(clip, volume * SfxVolume);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float target = MusicOn ? MusicVolume : 0f;

        if (wantedClip != null && music.clip != wantedClip)
        {
            // fade out, swap, fade in
            musicLevel = Mathf.MoveTowards(musicLevel, 0f, dt * 1.6f);
            if (musicLevel <= 0.01f)
            {
                music.clip = wantedClip;
                music.Play();
            }
        }
        else
        {
            musicLevel = Mathf.MoveTowards(musicLevel, target, dt * 0.8f);
        }
        music.volume = musicLevel;
    }
}
