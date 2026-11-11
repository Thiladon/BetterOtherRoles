using Reactor.Utilities.Attributes;
using TMPro;
using UnityEngine;

namespace BetterOtherRoles.Modules.Chat;

/// <summary>
///     Hover and click of the links of a chat text (<see cref="BorChatLinks"/>): italic under the mouse, a left click
///     opens the page. One per text: the chat bubbles are reused, the links of the new message are found at each frame.
/// </summary>
[RegisterInIl2Cpp]
public sealed class BorChatLinkBehaviour(nint cppPtr) : MonoBehaviour(cppPtr)
{
    private TextMeshPro? _text;
    private string? _hovered;

    /// <summary>
    ///     Adds the behaviour to a text, once.
    /// </summary>
    public static void AddTo(TextMeshPro text)
    {
        if (text && !text.GetComponent<BorChatLinkBehaviour>())
            text.gameObject.AddComponent<BorChatLinkBehaviour>();
    }

    public void Awake()
    {
        _text = GetComponent<TextMeshPro>();
    }

    public void Update()
    {
        var camera = Camera.main;
        if (!_text || !camera) return;

        var id = GetLinkUnderMouse(_text!, camera);
        if (id != _hovered)
        {
            if (_hovered != null) SetHovered(_hovered, false);
            if (id != null) SetHovered(id, true);
            _hovered = id;
        }

        if (id != null && Input.GetMouseButtonDown(0))
            BorChatLinks.Open(id);
    }

    private static string? GetLinkUnderMouse(TextMeshPro text, Camera camera)
    {
        if (!text.text.Contains("<link=", StringComparison.Ordinal)) return null;

        var index = TMP_TextUtilities.FindIntersectingLink(text, Input.mousePosition, camera);
        if (index < 0 || index >= text.textInfo.linkCount) return null;

        var id = text.textInfo.linkInfo[index].GetLinkID();
        return BorChatLinks.IsLink(id) ? id : null;
    }

    private void SetHovered(string id, bool hovered)
    {
        var value = BorChatLinks.SetHovered(_text!.text, id, hovered);
        if (value != _text.text)
            _text.text = value;
    }
}
