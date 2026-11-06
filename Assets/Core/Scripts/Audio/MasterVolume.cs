using UnityEngine;

// The player's master volume setting, kept between launches. AudioListener.volume is the live
// gain, which scene transitions fade, so it cannot be read back as the setting. This is the setting
public static class MasterVolume
{
    private const string Key = "MasterVolume";

    public static float Value { get; private set; } = 1f;

    // Runs once when the game starts, before the first scene, so the saved volume applies everywhere
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Load()
    {
        Value = PlayerPrefs.GetFloat(Key, 1f);
        AudioListener.volume = Value;
    }

    public static void Set(float value)
    {
        Value = Mathf.Clamp01(value);
        AudioListener.volume = Value;
        PlayerPrefs.SetFloat(Key, Value);
        PlayerPrefs.Save();
    }
}