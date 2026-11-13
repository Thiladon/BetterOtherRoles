using Reactor.Utilities.Attributes;
using UnityEngine;

namespace BetterOtherRoles.Modules.Display;

/// <summary>
///     Measure of the FPS (TOU-Mira 1.7.9 FpsCounter), shown next to the ping with
///     <see cref="LocalSettings.BorLocalSettingsTab.ShowFps"/> (<see cref="Patches.Misc.PingTrackerPatches"/>).
///     Added to the plugin object when it loads.
/// </summary>
[RegisterInIl2Cpp]
public sealed class BorFpsCounter(nint cppPtr) : MonoBehaviour(cppPtr)
{
    private float _deltaTime;

    /// <summary>
    ///     Gets the frames per second, smoothed so the value does not flicker every frame (0 before the first frame).
    /// </summary>
    public static float Fps { get; private set; }

    private void Update()
    {
        _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
        if (_deltaTime > 0f)
            Fps = 1f / _deltaTime;
    }
}
