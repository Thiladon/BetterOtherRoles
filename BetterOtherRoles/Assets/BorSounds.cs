using System.Reflection;
using UnityEngine;

namespace BetterOtherRoles.Assets;

/// <summary>
///     Sound effects embedded in the DLL (Resources/Sounds), taken from BetterOtherRoles 1.7.1 (SoundEffectsManager).
///     The files are raw (headerless) 2-channel signed 32 bit PCM (little endian), at 48 kHz.
/// </summary>
public static class BorSounds
{
    private const string SoundsPath = "BetterOtherRoles.Resources.Sounds.";
    private const int Channels = 2;
    private const int SampleRate = 48000;

    private static readonly Dictionary<string, AudioClip> Clips = [];

    public static void Play(string name, float volume = 0.8f)
    {
        if (!Constants.ShouldPlaySfx()) return;

        var clip = Get(name);
        if (clip)
        {
            SoundManager.Instance.StopSound(clip);
            SoundManager.Instance.PlaySound(clip, false, volume);
        }
    }

    private static AudioClip? Get(string name)
    {
        if (Clips.TryGetValue(name, out var clip) && clip) return clip;

        clip = Load(name);
        if (clip)
            Clips[name] = clip!;

        return clip;
    }

    private static AudioClip? Load(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(SoundsPath + name + ".raw");
        if (stream == null)
        {
            Error($"[Sounds] {name}.raw not found in the resources");
            return null;
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var bytes = memory.ToArray();

        var samples = new float[bytes.Length / 4];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)BitConverter.ToInt32(bytes, i * 4) / int.MaxValue;
        }

        var clip = AudioClip.Create(name, samples.Length / Channels, Channels, SampleRate, false);
        clip.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;
        clip.SetData(samples, 0);
        return clip;
    }
}
