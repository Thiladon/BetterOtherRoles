using System.Globalization;
using HarmonyLib;
using MiraAPI.Translation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Patches.Misc;

/// <summary>
///     Credits of Better Other Roles in the credits screen of the game, on a page of their own: a small button in the
///     column of the social icons (under the last one) goes from the credits of Among Us to those of BOR and back.
///     The BOR page is made like the game makes its own (CreditsController.Start): a copy of the panel per entry, and
///     in it a copy of each text of its format ("title", "person": name and role), so it looks the same. The entries
///     of the game are never changed (making game structs from the mod crashed the game): going back runs the
///     game's own Start again. The scroll starts again from the top.
///     The credits do not scroll by themselves nor close at the end: they move with the mouse wheel, by dragging
///     them, and with the scroll keys and stick of the game (both pages).
///     Content: the port, BetterOtherRoles and TheOtherRoles, the ideas and resources listed by TheOtherRoles (README of
///     BetterOtherRoles), the inspiration (Town of Us Mira), the libraries, and the authors of the hats (from the
///     bundles).
/// </summary>
[HarmonyPatch]
public static class CreditsPatches
{
    // In the column of the social icons (x 2.85 from the screen, the last one at y 1.69), at their size.
    private const float ButtonX = 2.85f;
    private const float ButtonY = 1.05f;
    private const float ButtonScale = 0.6f;
    private const float ButtonWidth = 1.7f;

    // Scroll of one notch of the mouse wheel, in units of the credits canvas.
    private const float WheelStep = 60f;

    private static bool _borPage;

    // Credits screen with the button (made again with each main menu).
    private static int _controllerId;

    // Mouse position at the last frame of a drag, or null.
    private static Vector3? _dragFrom;

    // No scroll by itself, and no closing at the end (the game moves the credits up in FixedUpdate and closes the
    // screen when they are over): the credits move with the wheel, by dragging, and with the scroll keys of the game.
    [HarmonyPatch(typeof(CreditsController), nameof(CreditsController.FixedUpdate))]
    [HarmonyPrefix]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S3400:Methods should not return constants", Justification = "Harmony prefix skipping the method of the game.")]
    public static bool FixedUpdatePrefix() => false;

    [HarmonyPatch(typeof(CreditsController), nameof(CreditsController.Update))]
    [HarmonyPostfix]
    public static void UpdatePostfix(CreditsController __instance)
    {
        var panel = __instance.creditMainPanel.transform;
        var move = -Input.mouseScrollDelta.y * WheelStep;

        // Dragging: the credits follow the mouse (screen pixels to units of the canvas).
        if (Input.GetMouseButton(0))
        {
            var mouse = Input.mousePosition;
            var camera = Camera.main;
            if (_dragFrom is { } from && camera && panel.lossyScale.y > 0f)
                move += (mouse.y - from.y) * (2f * camera!.orthographicSize / Screen.height) / panel.lossyScale.y;
            _dragFrom = mouse;
        }
        else
        {
            _dragFrom = null;

            // Keys and stick of the game (its Update sets a speed other than the default one while they are held).
            if (__instance.creditScrollSpeed != __instance.defaultCreditsScrollSpeed)
                move += __instance.creditScrollSpeed * Time.deltaTime * 50f;
        }

        if (move == 0f) return;

        var height = __instance.creditsRect.rect.height;
        var y = Mathf.Clamp(panel.localPosition.y + move, 0f, height);
        panel.localPosition = new Vector3(panel.localPosition.x, y, panel.localPosition.z);
    }

    // The button, once per credits screen (Start runs again when going back to the credits of the game).
    [HarmonyPatch(typeof(CreditsController), nameof(CreditsController.Start))]
    [HarmonyPrefix]
    public static void StartPrefix(CreditsController __instance)
    {
        var id = __instance.GetInstanceID();
        if (_controllerId == id) return;

        _controllerId = id;
        _borPage = false;
        AddButton(__instance);
    }

    private static void AddButton(CreditsController controller)
    {
        if (!DestroyableSingleton<DisconnectPopup>.InstanceExists) return;

        var screen = controller.GetComponentInParent<CreditsScreenPopUp>();
        if (!screen) return;

        var model = DestroyableSingleton<DisconnectPopup>.Instance.transform.Find("OkayButton");
        if (!model) return;

        var copy = Object.Instantiate(model!.gameObject, screen!.transform);
        copy.name = "BorCreditsPage";
        copy.SetActive(true);
        copy.transform.localPosition = new Vector3(ButtonX, ButtonY, -1f);
        copy.transform.localScale = Vector3.one * ButtonScale;
        foreach (var sprite in copy.GetComponentsInChildren<SpriteRenderer>(true))
            sprite.size = new Vector2(ButtonWidth / 0.84f, sprite.size.y);
        var collider = copy.GetComponent<BoxCollider2D>();
        collider.size = new Vector2(ButtonWidth, collider.size.y);

        var text = copy.GetComponentInChildren<TextMeshPro>(true);
        var translator = text.GetComponent<TextTranslatorTMP>();
        if (translator)
            Object.DestroyImmediate(translator);
        text.transform.localPosition = new Vector3(0f, text.transform.localPosition.y, text.transform.localPosition.z);
        text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.sizeDelta = new Vector2(ButtonWidth - 0.2f, text.rectTransform.sizeDelta.y);
        text.enableAutoSizing = true;
        text.fontSizeMin = 1f;
        text.text = PageLabel();

        var button = copy.GetComponent<PassiveButton>();
        button.OnClick = new Button.ButtonClickedEvent();
        button.OnClick.AddListener((Action)(() =>
        {
            _borPage = !_borPage;
            ShowPage(controller);
            text.text = PageLabel();
        }));
    }

    // The page to go to.
    private static string PageLabel() => _borPage ? "Among Us" : "BOR";

    // The scroll made again with the credits of the page, from the start.
    private static void ShowPage(CreditsController controller)
    {
        var panel = controller.creditMainPanel.transform;
        for (var i = panel.childCount - 1; i >= 0; i--)
            Object.Destroy(panel.GetChild(i).gameObject);

        if (_borPage)
            BuildPanels(controller, MakeBorCredits());
        else
            controller.Start();

        panel.localPosition = new Vector3(panel.localPosition.x, controller.startPos, panel.localPosition.z);
        controller.remainingDelay = controller.initialDelay;
    }

    // As CreditsController.Start for the text formats: a panel per entry, a text per column of its format.
    private static void BuildPanels(CreditsController controller, List<(string Format, string First, string Second)> entries)
    {
        var prefabs = new Dictionary<string, List<GameObject>>();
        foreach (var format in controller.formats)
            prefabs[format.format] = [.. format.formatPrefabs.ToArray()];

        foreach (var (format, first, second) in entries)
        {
            if (!prefabs.TryGetValue(format, out var parts)) continue;

            var panel = Object.Instantiate(controller.creditPanelPrefab, controller.creditMainPanel.transform);
            var columns = new[] { first, second };
            for (var j = 0; j < parts.Count; j++)
            {
                var part = Object.Instantiate(parts[j], panel.transform);
                var text = part.GetComponent<TextMeshProUGUI>();
                if (!text) continue;

                var value = j < columns.Length ? columns[j].Trim() : string.Empty;
                text.text = value.Length == 0 ? string.Empty : text.text + value;
            }
        }
    }

    private static List<(string Format, string First, string Second)> MakeBorCredits()
    {
        var credits = new List<(string Format, string First, string Second)>();
        void Title(string text) => credits.Add(("title", text, string.Empty));
        void Person(string name, string role) => credits.Add(("person", name, role));

        Title(string.Empty);
        Title("Better Other Roles");
        Person("Thilladon", Get("Port", "Port to MiraAPI"));

        Title(Get("BasedOn", "Based on BetterOtherRoles"));
        Person("Brybry", Get("Developer", "Developer"));
        Person("EnoPM", Get("Developer", "Developer"));
        Person("Thilladon", Get("Developer", "Developer"));

        Title(Get("ItselfBasedOn", "Itself based on TheOtherRoles"));
        Person("TheOtherRoles", Get("OriginalMod", "Original mod"));

        Title(Get("Ideas", "Ideas and resources (TheOtherRoles)"));
        Person("DorCoMaNdO", Get("Idea.DorCoMaNdO", "Essentials: custom game options"));
        Person("Dhalucard", Get("Idea.Dhalucard", "Idea of the Jackal and the Sidekick"));
        Person("Woodi-dev", Get("Idea.Woodi", "Idea of the Lovers and the Sheriff"));
        Person("Maartii", Get("Idea.Maartii", "Idea of the Jester"));
        Person("NotHunter101", Get("Idea.NotHunter101", "Idea of the Engineer and the Medic, code"));
        Person("Hardel-DW", Get("Idea.Hardel", "Idea of the Detective and the Time Master, code"));
        Person("Slushiegoose", Get("Idea.Slushiegoose", "Idea of the Swapper, Shifter, Arsonist, Mayor and Vulture"));
        Person("Ottomated", Get("Idea.Ottomated", "Idea of the Morphling, Snitch and Camouflager"));
        Person("Crowded Mod Team", Get("Idea.Crowded", "Lobbies of more than 10 players"));
        Person("LaicosVK, DasMonschta, Nova", Get("Idea.EpicRoles", "First kill shield and tabbed options (TheEpicRoles)"));
        Person("K3ndo", Get("Idea.K3ndo", "Idea of the Ninja, Thief, Lawyer, Pursuer, Deputy, Portalmaker and Guesser"));
        Person("Gendelo, Mallöris", Get("Idea.Gendelo", "Development of these roles"));

        Title(Get("Inspiration", "Inspiration"));
        Person("AU-Avengers", "Town of Us Mira");

        Title(Get("Libraries", "Libraries"));
        Person("MiraAPI", "All Of Us");
        Person("Reactor", "NuclearPowered");
        Person("BepInEx", Get("ModLoader", "Mod loader"));
        Person("CorsacCosmetics", Get("Cosmetics", "Cosmetics loader"));

        var authors = HatNamePatches.GetAuthors();
        if (authors.Count > 0)
        {
            Title(Get("Hats", "Hats (TheOtherHats)"));
            foreach (var (author, hats) in authors)
                Person(author, string.Format(CultureInfo.InvariantCulture, Get(hats > 1 ? "HatCount" : "HatCountOne", hats > 1 ? "{0} hats" : "{0} hat"), hats));
        }

        return credits;
    }

    private static string Get(string key, string fallback) => MiraLocaleManager.Get("BetterOtherRoles.Credits." + key, fallback);
}
