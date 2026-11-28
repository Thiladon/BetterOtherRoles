using System.Text;
using BetterOtherRoles.Assets;
using BetterOtherRoles.LocalSettings;
using BetterOtherRoles.Modules.Summary;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MiraAPI.LocalSettings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Summary of the game on the end game screen (TOU-Mira 1.7.9 EndGamePatches, from <see cref="BorGameHistory"/>):
///     one line per player (<see cref="BorSummaryText.GetLine"/>). Shown as a full list on the left (two thirds
///     of the screen), two short columns (without the killer) or hidden, from the local settings
///     (<see cref="BorLocalSettingsTab.EndSummary"/>), changed with a button right of the exit button; the three
///     buttons at 75 % of the game size.
///     Under each winner: the name and the last role, in its color.
/// </summary>
[HarmonyPatch]
public static class EndGameSummaryPatches
{
    private const float FontSize = 1.1f;

    // Space kept at the right edge of the screen and between the two columns, in world units.
    private const float Margin = 0.15f;

    // Size of the play again, exit and summary buttons, from the size of the game ones.
    private const float ButtonScale = 0.75f;

    // Space between the exit button and the summary button, in world units.
    private const float ButtonGap = 0.1f;

    // The summary sprite is lower than the exit one: raised to be centered with it, in world units.
    private const float ButtonRaise = 0.075f;

    private static GameObject? _summaryButton;

    private static BorLocalSettingsTab Settings => LocalSettingsTabSingleton<BorLocalSettingsTab>.Instance;

    [HarmonyPatch(typeof(EndGameManager), "SetEverythingUp")]
    [HarmonyPostfix]
    public static void SetEverythingUpPostfix(EndGameManager __instance)
    {
        if (TutorialManager.InstanceExists || BorGameHistory.Players.Count == 0) return;

        ShowWinnerRoles(__instance);
        BuildSummary(__instance, BorSummaryText.GetWinners());
    }

    private static string Get(string key, string fallback) => BorSummaryText.Get(key, fallback);

    // The name, then the last role in its color, under each winner (the game hides the names when the crew wins).
    // The players are made in the order of the winners, the local player first.
    private static void ShowWinnerRoles(EndGameManager manager)
    {
        var winners = EndGameResult.CachedWinners.ToArray().OrderBy(x => x.IsYou ? -1 : 0).ToList();
        var players = manager.transform.GetComponentsInChildren<PoolablePlayer>(true);
        for (var i = 0; i < players.Length && i < winners.Count; i++)
        {
            var winner = winners[i];
            var record = BorGameHistory.Players.FirstOrDefault(x => x.PlayerName == winner.PlayerName);
            var role = record?.Roles.LastOrDefault();
            if (role == null) continue;

            var player = players[i];
            var scale = player.transform.localScale;
            player.cosmetics.nameText.gameObject.SetActive(true);
            player.SetName(
                $"\n<size=85%>{winner.PlayerName}</size>\n<size=65%><color=#{ColorUtility.ToHtmlStringRGBA(role.Color)}>{role.Name}</color></size>",
                new Vector3(1f / scale.x, 1f / scale.y, 1f),
                Color.white,
                -15f);
            player.SetNamePosition(new Vector3(0f, -1.31f, -0.5f));
        }
    }

    private static void BuildSummary(EndGameManager manager, HashSet<string> winners)
    {
        var exitButton = manager.Navigation.ExitButton;
        var camera = Camera.main!;
        var corner = camera.ViewportToWorldPoint(new Vector3(0f, 1f, camera.nearClipPlane));
        var screenRight = camera.ViewportToWorldPoint(new Vector3(1f, 1f, camera.nearClipPlane)).x;

        // Title, then an empty line before the players.
        var title = Get("Title", "End Game Summary:") + "\n";
        var full = new StringBuilder().AppendLine(title);
        var left = new StringBuilder().AppendLine(title);

        // The right column starts on the line of the first player of the left one.
        var right = new StringBuilder().AppendLine().AppendLine();
        var index = 0;
        foreach (var record in BorGameHistory.Players.OrderBy(x => x.PlayerId))
        {
            var winner = winners.Contains(record.PlayerName);
            full.AppendLine(BorSummaryText.GetLine(record, winner, true));
            (index % 2 == 0 ? left : right).AppendLine(BorSummaryText.GetLine(record, winner, false));
            index++;
        }

        // Lines too long wrapped at the end of their room, at any resolution: two thirds of the screen for the list,
        // half of the room each for the columns.
        var listText = MakeText(manager, exitButton, corner, full.ToString());
        var listLeft = FitWidth(listText, null, corner.x + ((screenRight - corner.x) * 2f / 3f));
        var middle = (listLeft + screenRight - Margin) / 2f;
        var leftText = MakeText(manager, exitButton, corner, left.ToString());
        FitWidth(leftText, null, middle - Margin);
        var rightText = MakeText(manager, exitButton, corner, right.ToString());
        FitWidth(rightText, middle, screenRight - Margin);

        // Room for the summary above the winners while it is shown, as TOU-Mira does.
        var winText = manager.WinText;
        var winPosition = winText.transform.position;
        var winTitle = winText.text;
        var winScale = winText.transform.localScale;

        void Show()
        {
            var mode = Settings.EndSummary.Value;
            listText.SetActive(mode == EndSummaryDisplay.LeftSide);
            leftText.SetActive(mode == EndSummaryDisplay.Split);
            rightText.SetActive(mode == EndSummaryDisplay.Split);

            var shown = mode != EndSummaryDisplay.Hidden;
            winText.transform.position = shown ? winPosition + (Vector3.down * 0.8f) : winPosition;
            winText.text = shown ? $"\n{winTitle}" : winTitle;
            winText.transform.localScale = shown ? new Vector3(1.1f, 1.1f, 1f) : winScale;
        }

        Show();
        MakeButton(manager.Navigation, () =>
        {
            Settings.EndSummary.Value = Settings.EndSummary.Value switch
            {
                EndSummaryDisplay.Hidden => EndSummaryDisplay.Split,
                EndSummaryDisplay.Split => EndSummaryDisplay.LeftSide,
                _ => EndSummaryDisplay.Hidden,
            };
            Show();
        });
    }

    // The summary button, shown and hidden with the play again and exit buttons.
    [HarmonyPatch(typeof(EndGameNavigation), nameof(EndGameNavigation.ShowDefaultNavigation))]
    [HarmonyPostfix]
    public static void ShowDefaultNavigationPostfix()
    {
        if (_summaryButton)
            _summaryButton!.SetActive(true);
    }

    [HarmonyPatch(typeof(EndGameNavigation), nameof(EndGameNavigation.NextGame))]
    [HarmonyPatch(typeof(EndGameNavigation), nameof(EndGameNavigation.Exit))]
    [HarmonyPostfix]
    public static void HideButtonsPostfix()
    {
        if (_summaryButton)
            _summaryButton!.SetActive(false);
    }

    // The play again and exit buttons made smaller, and a copy of the exit button on its right, with the summary
    // sprite and its text on two lines.
    private static void MakeButton(EndGameNavigation navigation, Action onClick)
    {
        var exitButton = navigation.ExitButton;
        navigation.PlayAgainButton.transform.localScale *= ButtonScale;
        exitButton.transform.localScale *= ButtonScale;

        var button = Object.Instantiate(exitButton, exitButton.transform.parent);
        button.gameObject.SetActive(exitButton.gameObject.activeSelf);
        button.sprite = BorAssets.GameSummaryButton.LoadAsset();

        // From the sprites: the buttons are hidden until the end of the animation, and a hidden renderer has no bounds.
        var exitPosition = exitButton.transform.position;
        var exitRight = exitPosition.x + (exitButton.sprite.bounds.max.x * exitButton.transform.lossyScale.x);
        var summaryLeft = -button.sprite.bounds.min.x * button.transform.lossyScale.x;
        var offset = exitRight + ButtonGap + summaryLeft - exitPosition.x;
        button.transform.position = new Vector3(exitPosition.x + offset, exitPosition.y + ButtonRaise, exitPosition.z);

        // The exit button is kept at a distance from the edge of the screen (AspectPosition, again when shown and when
        // the resolution changes): the copy keeps it, further from the left edge (closer to the right one) and from
        // the bottom one (closer to the top one).
        var aspect = button.GetComponent<AspectPosition>();
        if (aspect)
        {
            var scale = button.transform.parent.lossyScale;
            var alignment = (int)aspect.Alignment;
            var fromRight = (alignment & (int)AspectPosition.EdgeAlignments.Right) != 0;
            var fromTop = (alignment & (int)AspectPosition.EdgeAlignments.Top) != 0;
            var x = offset / scale.x;
            var y = ButtonRaise / scale.y;
            aspect.DistanceFromEdge += new Vector3(fromRight ? -x : x, fromTop ? -y : y, 0f);
        }

        var translator = button.GetComponentInChildren<TextTranslatorTMP>(true);
        if (translator)
        {
            translator.defaultStr = $"<size=70%>{Get("ButtonTop", "End Game")}</size>\n<size=55%>{Get("ButtonBottom", "Summary")}</size>";
            translator.TargetText = StringNames.None;
            translator.ResetText();
        }

        var passive = button.GetComponent<PassiveButton>();
        passive.OnClick = new Button.ButtonClickedEvent();
        passive.OnClick.AddListener(onClick);
        _summaryButton = button.gameObject;
    }

    // Puts the left edge of the text box at a world x (or keeps it), keeps its top, and makes it end at a world x,
    // with word wrapping. Returns the world x of its left edge.
    private static float FitWidth(GameObject text, float? left, float right)
    {
        var rect = text.GetComponent<RectTransform>();
        var corners = new Il2CppStructArray<Vector3>(4);
        rect.GetWorldCorners(corners);
        var topLeft = corners[1];
        if (left != null)
            topLeft.x = left.Value;

        var scale = rect.lossyScale.x == 0f ? 1f : rect.lossyScale.x;
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(Mathf.Max(right - topLeft.x, 1f) / scale, rect.sizeDelta.y);
        rect.position = topLeft;

        var tmp = text.GetComponent<TMP_Text>();
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;        return topLeft.x;
    }

    // A copy of the win text at the top left of the screen (TOU-Mira 1.7.9 place).
    private static GameObject MakeText(EndGameManager manager, SpriteRenderer exitButton, Vector3 corner, string text)
    {
        var copy = Object.Instantiate(manager.WinText.gameObject);
        copy.transform.position = new Vector3(exitButton.transform.position.x + 0.1f, corner.y - 0.1f, -14f);
        copy.transform.localScale = Vector3.one;

        var tmp = copy.GetComponent<TMP_Text>();
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.color = Color.white;
        tmp.fontSizeMin = tmp.fontSizeMax = tmp.fontSize = FontSize;
        tmp.GetComponent<RectTransform>().anchoredPosition = new Vector2(corner.x + 3.5f, corner.y - 0.1f);
        tmp.text = text;
        return copy;
    }
}
