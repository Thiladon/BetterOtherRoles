using System.Runtime.InteropServices;
using UnityEngine;

namespace BetterOtherRoles.Utilities;

/// <summary>
///     Letter keys read with the keyboard layout. Unity reads a key by its place on a QWERTY keyboard (KeyCode.A is the
///     key left of S): on an AZERTY keyboard, the A key gives KeyCode.Q, so Ctrl+A was never seen. Windows virtual keys
///     follow the layout (VK 'A' is the key that writes "a"). Off Windows, the Unity key is used.
/// </summary>
public static class BorLetterKeys
{
    // Last state read for each letter, and the frame it was read on (a key read twice in a frame gives the same answer).
    private static readonly Dictionary<char, (bool Held, bool Down, int Frame)> States = [];

    private static bool IsWindows => Application.platform == RuntimePlatform.WindowsPlayer;

    /// <summary>
    ///     Gets whether the key writing this letter was pressed since the last frame it was read. Read it every frame
    ///     while it can be used: a key already held the first time it is read counts as pressed.
    /// </summary>
    /// <param name="letter">The letter, A to Z.</param>
    /// <returns>True on the frame the key is pressed.</returns>
    public static bool GetKeyDown(char letter)
    {
        letter = char.ToUpperInvariant(letter);
        if (!IsWindows) return Input.GetKeyDown(KeyCode.A + (letter - 'A'));

        var frame = Time.frameCount;
        States.TryGetValue(letter, out var state);
        if (state.Frame == frame) return state.Down;

        var held = Application.isFocused && (GetAsyncKeyState(letter) & 0x8000) != 0;
        var down = held && !state.Held;
        States[letter] = (held, down, frame);
        return down;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
