using UnityEngine;

public static class SaveData
{
    private const string BestKey = "BH_Best";
    private const string MusicKey = "BH_Music";
    private const string SfxKey = "BH_Sfx";

    public static int Best
    {
        get => PlayerPrefs.GetInt(BestKey, 0);
        set { PlayerPrefs.SetInt(BestKey, value); PlayerPrefs.Save(); }
    }

    public static bool MusicOn
    {
        get => PlayerPrefs.GetInt(MusicKey, 1) == 1;
        set { PlayerPrefs.SetInt(MusicKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static bool SfxOn
    {
        get => PlayerPrefs.GetInt(SfxKey, 1) == 1;
        set { PlayerPrefs.SetInt(SfxKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }
}
