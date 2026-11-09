using BetterOtherRoles.LocalSettings;
using MiraAPI.LocalSettings;
using Reactor.Utilities.Attributes;
using UnityEngine;

namespace BetterOtherRoles.Modules.Display;

/// <summary>
///     FPS counter in a corner of the screen (TOU-Mira 1.7.9 FpsCounter), shown with
///     <see cref="BorLocalSettingsTab.ShowFps"/>, in the corner of <see cref="BorLocalSettingsTab.FpsPlace"/>.
///     Small and green, against the edge of the screen (above the task bar at the top left).
///     Its size follows the height of the window: about 12 pixels in 800×600.
///     Added to the plugin object when it loads, so it works everywhere (menus, lobby, game).
/// </summary>
[RegisterInIl2Cpp]
public sealed class BorFpsCounter(nint cppPtr) : MonoBehaviour(cppPtr)
{
    // Sizes in parts of the window height.
    private const float FontRatio = 0.02f;
    private const float EdgeRatio = 0.004f;
    private const float SideRatio = 0.017f;
    private const float LineRatio = FontRatio * 1.3f;

    /// <summary>
    ///     Part of the window height kept free for the counter at the top of the screen, shown or not: the texts
    ///     placed at the top left stay below it (<see cref="Patches.Misc.LobbyRoleListPatches"/>).
    /// </summary>
    public const float ReservedRatio = EdgeRatio + LineRatio;

    private static readonly Color TextColor = new(0f, 1f, 0f, 1f);

    private float _deltaTime;
    private GUIStyle? _style;
    private GUIStyle? _shadow;

    private static BorLocalSettingsTab Settings => LocalSettingsTabSingleton<BorLocalSettingsTab>.Instance;

    private void Update()
    {
        // Smoothed, so the value does not flicker every frame.
        _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
    }

    private void OnGUI()
    {
        var settings = Settings;
        if (settings == null || !settings.ShowFps.Value || _deltaTime <= 0f) return;

        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label) { padding = new RectOffset() };
            _style.normal.textColor = TextColor;
            _shadow = new GUIStyle(_style);
            _shadow.normal.textColor = new Color(0f, 0f, 0f, 0.65f);
        }

        float height = Screen.height;
        _style.fontSize = Mathf.Max(8, Mathf.RoundToInt(height * FontRatio));
        _shadow!.fontSize = _style.fontSize;

        var place = settings.FpsPlace.Value;
        var right = place is FpsPlace.TopRight or FpsPlace.BottomRight;
        var bottom = place is FpsPlace.BottomLeft or FpsPlace.BottomRight;
        _style.alignment = GetAlignment(right, bottom);
        _shadow.alignment = _style.alignment;

        var width = height * 0.3f;
        var line = height * LineRatio;
        var side = height * SideRatio;
        var edge = height * EdgeRatio;
        var x = right ? Screen.width - width - side : side;
        var y = bottom ? height - line - edge : edge;
        var text = $"{1f / _deltaTime:F1} FPS";

        GUI.Label(new Rect(x + 1f, y + 1f, width, line), text, _shadow);
        GUI.Label(new Rect(x, y, width, line), text, _style);
    }

    private static TextAnchor GetAlignment(bool right, bool bottom)
    {
        if (bottom) return right ? TextAnchor.LowerRight : TextAnchor.LowerLeft;

        return right ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
    }
}
