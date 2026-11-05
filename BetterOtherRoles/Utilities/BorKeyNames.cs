using System.Runtime.InteropServices;

namespace BetterOtherRoles.Utilities;

/// <summary>
///     Names of keyboard keys in the player's keyboard layout (AZERTY, QWERTZ...).
///     Rewired keys are physical positions named after the US QWERTY layout: the key "Q" is the A key in AZERTY.
///     Only the layout dependent keys (letters, punctuation) are renamed, through the Windows layout.
/// </summary>
public static class BorKeyNames
{
    private const uint MapVirtualScanCodeToKey = 1;
    private const uint ToUnicodeKeepKeyboardState = 4;

    // US QWERTY scan codes (set 1) of the layout dependent keys, by Rewired / Unity key name.
    private static readonly Dictionary<string, uint> ScanCodes = new()
    {
        ["Q"] = 0x10, ["W"] = 0x11, ["E"] = 0x12, ["R"] = 0x13, ["T"] = 0x14, ["Y"] = 0x15, ["U"] = 0x16,
        ["I"] = 0x17, ["O"] = 0x18, ["P"] = 0x19, ["LeftBracket"] = 0x1A, ["RightBracket"] = 0x1B,
        ["A"] = 0x1E, ["S"] = 0x1F, ["D"] = 0x20, ["F"] = 0x21, ["G"] = 0x22, ["H"] = 0x23, ["J"] = 0x24,
        ["K"] = 0x25, ["L"] = 0x26, ["Semicolon"] = 0x27, ["Quote"] = 0x28, ["BackQuote"] = 0x29,
        ["Backslash"] = 0x2B, ["Z"] = 0x2C, ["X"] = 0x2D, ["C"] = 0x2E, ["V"] = 0x2F, ["B"] = 0x30,
        ["N"] = 0x31, ["M"] = 0x32, ["Comma"] = 0x33, ["Period"] = 0x34, ["Slash"] = 0x35,
        ["Minus"] = 0x0C, ["Equals"] = 0x0D,
    };

    private static readonly Dictionary<(IntPtr Layout, string Key), string?> Cache = [];

    /// <summary>
    ///     Gets the name of a key in the current keyboard layout, or <c>null</c> when it doesn't depend on the layout
    ///     (or can't be found): the caller then keeps its own name.
    /// </summary>
    /// <param name="keyName">The Rewired <c>KeyboardKeyCode</c> or Unity <c>KeyCode</c> name (e.g. "Q", "Alpha1").</param>
    public static string? GetLayoutName(string keyName)
    {
        // The digit row keeps its digits (written on the keys of every layout, even when shift is needed).
        if (keyName.Length == 6 && keyName.StartsWith("Alpha", StringComparison.Ordinal)) return keyName[5..];

        if (!ScanCodes.TryGetValue(keyName, out var scanCode)) return null;

        try
        {
            var layout = GetKeyboardLayout(0);
            if (!Cache.TryGetValue((layout, keyName), out var name))
            {
                name = ReadKey(layout, scanCode);
                Cache[(layout, keyName)] = name;
            }

            return name;
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Gets the name of a key in the current keyboard layout, or its own name.
    /// </summary>
    public static string Get(string keyName)
    {
        return GetLayoutName(keyName) ?? keyName;
    }

    /// <summary>
    ///     Replaces the US name of a key at the end of a label (e.g. "Shift + Q") by its name in the current layout.
    /// </summary>
    /// <param name="label">The label written by Rewired.</param>
    /// <param name="keyName">The key name (Rewired <c>KeyboardKeyCode</c> or Unity <c>KeyCode</c>).</param>
    public static string Localize(string label, string keyName)
    {
        if (string.IsNullOrEmpty(label) || GetLayoutName(keyName) is not { } layoutName) return label;

        // Rewired writes either the key character (US layout) or the key name.
        foreach (var usName in new[] { UsCharacter(keyName), keyName })
        {
            if (usName != null && label.EndsWith(usName, StringComparison.OrdinalIgnoreCase))
                return label[..^usName.Length] + layoutName;
        }

        return label;
    }

    private static string? UsCharacter(string keyName)
    {
        return keyName switch
        {
            { Length: 1 } => keyName,
            "LeftBracket" => "[",
            "RightBracket" => "]",
            "Semicolon" => ";",
            "Quote" => "'",
            "BackQuote" => "`",
            "Backslash" => "\\",
            "Comma" => ",",
            "Period" => ".",
            "Slash" => "/",
            "Minus" => "-",
            "Equals" => "=",
            _ => null,
        };
    }

    private static string? ReadKey(IntPtr layout, uint scanCode)
    {
        var virtualKey = MapVirtualKeyEx(scanCode, MapVirtualScanCodeToKey, layout);
        if (virtualKey == 0) return null;

        // Dead keys (^ in AZERTY) return -1 with their character.
        var buffer = new char[8];
        var length = ToUnicodeEx(virtualKey, scanCode, new byte[256], buffer, buffer.Length, ToUnicodeKeepKeyboardState, layout);
        if (length == 0 || buffer[0] == 0) return null;

        var character = buffer[0].ToString();
        return string.IsNullOrWhiteSpace(character) ? null : character.ToUpperInvariant();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKeyEx(uint code, uint mapType, IntPtr layout);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ToUnicodeEx(uint virtualKey, uint scanCode, byte[] keyState, [Out] char[] buffer, int bufferSize, uint flags, IntPtr layout);
}
