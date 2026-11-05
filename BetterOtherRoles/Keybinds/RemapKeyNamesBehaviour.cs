using BetterOtherRoles.Utilities;
using Reactor.Utilities.Attributes;
using Rewired;
using Rewired.UI.ControlMapper;
using TMPro;
using UnityEngine;

namespace BetterOtherRoles.Keybinds;

/// <summary>
///     Added to Remap Controls (Rewired ControlMapper): its keyboard fields show the key name in the player's
///     keyboard layout (see <see cref="BorKeyNames"/>).
///     Rewired writes the US name in native code during the frame (no managed call to patch): the displayed text
///     is fixed in LateUpdate, after it. Each field has an InputFieldInfo giving its element map
///     (as MiraAPI does to color the conflicts).
/// </summary>
[RegisterInIl2Cpp]
public sealed class RemapKeyNamesBehaviour(nint cppPtr) : MonoBehaviour(cppPtr)
{
    private void LateUpdate()
    {
        foreach (var field in GetComponentsInChildren<InputFieldInfo>())
        {
            var elementMap = ReInput.mapping.GetActionElementMap(field.actionElementMapId);
            if (elementMap?.controllerMap?.controllerType != ControllerType.Keyboard) continue;

            var text = field.GetComponentInChildren<TMP_Text>();
            if (text == null) continue;

            var localized = BorKeyNames.Localize(text.text, elementMap.keyCode.ToString());
            if (localized != text.text)
                text.text = localized;
        }
    }
}
