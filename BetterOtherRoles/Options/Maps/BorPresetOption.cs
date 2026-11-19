using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Translation;
using System.Text;
using UnityEngine;

namespace BetterOtherRoles.Options.Maps;

/// <summary>
///     Option holding the name of a preset of the host ("None" first, then its presets): the list of choices follows the
///     preset files (<see cref="SetChoices"/>). Left out of the presets.
///     A player gets the name chosen by the host even if they have no preset of that name: it is added to their list.
/// </summary>
public sealed class BorPresetOption : ModdedStringOption
{
    /// <summary>
    ///     Value when no preset is chosen (its translation is shown).
    /// </summary>
    public const string None = "BetterOtherRoles.Option.RandomMap.NoPreset";

    /// <summary>
    ///     Initializes a new instance of the <see cref="BorPresetOption"/> class, with "None" as the only choice.
    /// </summary>
    /// <param name="title">Locale key of the title.</param>
    public BorPresetOption(string title) : base(title, None, [None], false)
    {
    }

    /// <summary>
    ///     Gets the chosen preset, or null for "None".
    /// </summary>
    public string? Preset => Value == None ? null : Value;

    /// <summary>
    ///     Replaces the choices with "None" then these presets; the current value is kept in the list (null: no other
    ///     choice to keep).
    /// </summary>
    /// <param name="presets">Names of the presets, in the order to show.</param>
    /// <param name="keepValue">Whether the current value stays a choice when it is not one of the presets.</param>
    public void SetChoices(IEnumerable<string> presets, bool keepValue)
    {
        var values = new List<string> { None };
        values.AddRange(presets.Where(x => x != None));
        if (keepValue && !values.Contains(Value))
            values.Add(Value);

        Values = values.ToArray();
        StringNameValues = Values.Select(MiraLocaleManager.GetOrCreateLocaleString).ToArray();
        var index = Math.Max(0, Array.IndexOf(Values, Value));

        // Destroyed by Unity in the lobby (unused asset), but still read for the change notification: given the
        // choices too.
        if (Data is not null && Data.TryCast<StringGameSetting>() is { } setting)
        {
            setting.Values = new Il2CppStructArray<StringNames>(StringNameValues);
            setting.Index = index;
        }

        if (!OptionBehaviour || OptionBehaviour!.TryCast<StringOption>() is not { } option) return;

        option.Values = new Il2CppStructArray<StringNames>(StringNameValues);
        option.Value = index;

        // The game only writes the text again when the index changes.
        option.ValueText.text = TranslationController.Instance.GetString(StringNameValues[index]);
    }

    /// <inheritdoc />
    public override OptionBehaviour CreateOption(ToggleOption toggleOpt, NumberOption numberOpt, StringOption stringOpt,
        PlayerOption playerOpt, Transform container)
    {
        var behaviour = base.CreateOption(toggleOpt, numberOpt, stringOpt, playerOpt, container);

        // The line gets the choices of now (the StringGameSetting it is made from may be older).
        SetChoices(Values.Skip(1).ToList(), true);
        return behaviour;
    }

    /// <inheritdoc />
    public override void HandleNetData(byte[] data)
    {
        // Name of a preset of the host: added to the choices first (unknown name: no valid index for the game).
        var value = Encoding.Unicode.GetString(data);
        if (!Values.Contains(value))
            SetChoices(Values.Append(value), true);

        base.HandleNetData(data);
    }
}
