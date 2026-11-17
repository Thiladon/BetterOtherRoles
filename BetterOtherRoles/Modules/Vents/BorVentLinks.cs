using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BetterOtherRoles.Modules.Vents;

/// <summary>
///     Vents with more than 3 exits. The game only has Left, Right and Center, one arrow each (Vent.Buttons). Each exit
///     added here gets a copy of the first arrow and of its cleaning sign, added at the end of the arrays of the vent:
///     the controller (ConsoleJoystick) picks among all the arrows already. The arrows are then placed by
///     <see cref="SetButtonsPrefix"/> as the game does (Vent.SetButtons), which would read past its 3 exits.
///     The move is the one of the game (Vent.TryMoveToVent): snap and VentilationSystem update, seen by everyone.
///     The links are set by each client when the map starts, the same on all of them (shared options), and forgotten
///     with the map.
/// </summary>
[HarmonyPatch(typeof(Vent))]
public static class BorVentLinks
{
    private const int GameExits = 3;

    // Added exits of each vent, by its id.
    private static readonly Dictionary<int, List<Vent>> Added = [];

    /// <summary>
    ///     Adds an exit to a vent, after its Left, Right and Center (one way: call it again for the way back).
    /// </summary>
    /// <param name="from">The vent.</param>
    /// <param name="to">The vent it leads to.</param>
    public static void Add(Vent from, Vent to)
    {
        if (!from || !to || from == to) return;

        if (!Added.TryGetValue(from.Id, out var exits))
        {
            exits = [];
            Added[from.Id] = exits;
        }

        if (exits.Exists(x => x.Id == to.Id)) return;

        exits.Add(to);
        AddArrow(from, to);
    }

    /// <summary>
    ///     The map destroyed: the links are forgotten.
    /// </summary>
    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.OnDestroy))]
    [HarmonyPostfix]
    public static void ShipDestroyPostfix() => Added.Clear();

    /// <summary>
    ///     Arrows of a vent with added exits: all placed here, the game ones as the game does.
    /// </summary>
    [HarmonyPatch(nameof(Vent.SetButtons))]
    [HarmonyPrefix]
    public static bool SetButtonsPrefix(Vent __instance, bool enabled)
    {
        if (!Added.TryGetValue(__instance.Id, out var exits)) return true;

        var right = __instance.Right;
        var left = __instance.Left;
        var position = __instance.transform.position;
        var between = right && left ? (Vector2)((right!.transform.position + left!.transform.position) / 2f - position) : Vector2.zero;
        var ventilation = ShipStatus.Instance.Systems[SystemTypes.Ventilation].TryCast<VentilationSystem>();
        for (var i = 0; i < __instance.Buttons.Length; i++)
        {
            var button = __instance.Buttons[i];
            var vent = ExitAt(__instance, exits, i);
            if (!enabled || !vent)
            {
                button.gameObject.SetActive(false);
                continue;
            }

            button.gameObject.SetActive(true);
            ShowCleaning(__instance, i, ventilation != null && ventilation.IsVentCurrentlyBeingCleaned(vent!.Id));

            var toVent = vent!.transform.position - position;
            var place = toVent.normalized * (0.7f + __instance.spreadShift);
            place.x *= Mathf.Sign(ShipStatus.Instance.transform.localScale.x);
            place.y -= 0.08f;
            place.z = -10f;
            button.transform.localPosition = place;
            button.transform.LookAt2d(vent.transform);
            var spread = between.AngleSigned(toVent) > 0f ? __instance.spreadAmount : -__instance.spreadAmount;
            button.transform.localPosition = place.RotateZ(spread);
            button.transform.Rotate(0f, 0f, spread);
        }

        return false;
    }

    /// <summary>
    ///     Cleaning signs of the added exits (the game updates its 3).
    /// </summary>
    [HarmonyPatch(nameof(Vent.UpdateArrows))]
    [HarmonyPostfix]
    public static void UpdateArrowsPostfix(Vent __instance, VentilationSystem ventSystem)
    {
        if (__instance != Vent.currentVent || ventSystem == null || !Added.TryGetValue(__instance.Id, out var exits)) return;

        for (var i = 0; i < exits.Count; i++)
        {
            if (exits[i])
                ShowCleaning(__instance, GameExits + i, ventSystem.IsVentCurrentlyBeingCleaned(exits[i].Id));
        }
    }

    // Exit shown by an arrow: the game order (Right, Left, Center), then the added ones.
    private static Vent? ExitAt(Vent vent, List<Vent> exits, int index) => index switch
    {
        0 => vent.Right,
        1 => vent.Left,
        2 => vent.Center,
        _ => index - GameExits < exits.Count ? exits[index - GameExits] : null,
    };

    // As Vent.ToggleNeighborVentBeingCleaned: the arrow stops working, its sign shows.
    private static void ShowCleaning(Vent vent, int index, bool cleaned)
    {
        vent.Buttons[index].enabled = !cleaned;
        if (index < vent.CleaningIndicators.Length && vent.CleaningIndicators[index])
            vent.CleaningIndicators[index].SetActive(cleaned);
    }

    // Copy of the first arrow and of its cleaning sign, at the end of the arrays of the vent.
    private static void AddArrow(Vent from, Vent to)
    {
        var model = from.Buttons[0];
        var arrow = Object.Instantiate(model, model.transform.parent);
        arrow.name = $"BorExit{to.Id}";
        arrow.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        arrow.OnClick.AddListener((Action)(() => MoveTo(from, to)));
        arrow.gameObject.SetActive(false);
        from.Buttons = new Il2CppReferenceArray<ButtonBehavior>(from.Buttons.Append(arrow).ToArray());

        // The sign is copied with the arrow when it is its child, else beside the model sign.
        var signModel = from.CleaningIndicators.Length > 0 ? from.CleaningIndicators[0] : null;
        GameObject sign;
        if (!signModel)
            sign = new GameObject("BorCleaning");
        else if (signModel!.transform.IsChildOf(model.transform))
            sign = arrow.GetComponentsInChildren<Transform>(true).First(x => x.name == signModel.name).gameObject;
        else
            sign = Object.Instantiate(signModel, signModel.transform.parent);

        sign.SetActive(false);
        from.CleaningIndicators = new Il2CppReferenceArray<GameObject>(from.CleaningIndicators.Append(sign).ToArray());
    }

    private static void MoveTo(Vent from, Vent to)
    {
        if (Vent.currentVent != from) return;

        if (!from.TryMoveToVent(to, out var error))
            Warning($"Local player failed to move to vent {to.Id}: {error}");
    }
}
