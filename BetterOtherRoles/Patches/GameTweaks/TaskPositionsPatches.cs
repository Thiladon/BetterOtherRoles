using BetterOtherRoles.Options;
using HarmonyLib;
using MiraAPI.GameOptions;
using UnityEngine;

namespace BetterOtherRoles.Patches.GameTweaks;

/// <summary>
///     Random task positions (BetterOtherRoles 1.7.1 TaskPositionsRandomizer, NormalPlayerTaskPatches, UploadData*Patches,
///     <see cref="GameTweaksOptions"/>): wiring panels shuffled, upload swapped with a random download.
///     As BOR, each client draws its own places when the map starts: two players may not find them at the same place.
/// </summary>
[HarmonyPatch]
public static class TaskPositionsPatches
{
    // The Skeld, terminal of the upload in Admin: much bigger than the downloads, which would show where it is.
    private const float SkeldUploadScale = 0.625f;

    // Polus, download of Life Support at the place of the upload: a bit lower (BOR).
    private const float PolusLifeSupportShift = -0.1f;

    // Room shown for the upload and the download that swapped places (the consoles keep their room: the game finds
    // them with it).
    private static readonly Dictionary<SystemTypes, SystemTypes> RelocatedRooms = new();

    private static GameTweaksOptions Options => OptionGroupSingleton<GameTweaksOptions>.Instance;

    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start))]
    [HarmonyPostfix]
    public static void ShipStartPostfix()
    {
        RelocatedRooms.Clear();
        if (Options.RandomWirePositions.Value || Options.RandomUploadPosition.Value) Apply();
    }

    /// <summary>
    ///     Task list: the upload and the download that swapped places show the room where they now are.
    /// </summary>
    [HarmonyPatch(typeof(UploadDataTask), nameof(UploadDataTask.AppendTaskText))]
    [HarmonyPrefix]
    public static void UploadTextPrefix(UploadDataTask __instance, out (SystemTypes Start, SystemTypes End)? __state)
    {
        __state = null;
        if (RelocatedRooms.Count == 0) return;

        __state = (__instance.StartAt, __instance.EndAt);
        __instance.StartAt = ShownRoom(__instance.StartAt);
        __instance.EndAt = ShownRoom(__instance.EndAt);
    }

    [HarmonyPatch(typeof(UploadDataTask), nameof(UploadDataTask.AppendTaskText))]
    [HarmonyPostfix]
    public static void UploadTextPostfix(UploadDataTask __instance, (SystemTypes Start, SystemTypes End)? __state)
    {
        if (__state == null) return;

        __instance.StartAt = __state.Value.Start;
        __instance.EndAt = __state.Value.End;
    }

    /// <summary>
    ///     Download window: the room it comes from is the one where the console now is.
    /// </summary>
    [HarmonyPatch(typeof(UploadDataGame), nameof(UploadDataGame.Begin))]
    [HarmonyPostfix]
    public static void UploadGameBeginPostfix(UploadDataGame __instance)
    {
        var task = __instance.MyNormTask;
        if (!task || task.taskStep != 0 || !RelocatedRooms.TryGetValue(task.StartAt, out var room)) return;

        __instance.SourceText.text = DestroyableSingleton<TranslationController>.Instance.GetString(room);
    }

    // After the swap of the images and scales, which it follows.
    private static void ShrinkSkeldUpload(ShipStatus ship, SpriteRenderer? renderer)
    {
        if (ship.Type != ShipStatus.MapType.Ship || !renderer) return;

        renderer!.transform.localScale = Vector3.Scale(renderer.transform.localScale, new Vector3(SkeldUploadScale, SkeldUploadScale, 1f));
    }

    // Image of a console: its own, or the one given to it.
    private static SpriteRenderer? Renderer(Console console)
    {
        var renderer = console.GetComponent<SpriteRenderer>();
        return renderer ? renderer : console.Image;
    }

    private static SystemTypes ShownRoom(SystemTypes room) => RelocatedRooms.TryGetValue(room, out var shown) ? shown : room;

    private static void Apply()
    {
        var ship = ShipStatus.Instance;
        var random = new System.Random();
        var consoles = ship.AllConsoles.ToList();
        try
        {
            if (Options.RandomWirePositions.Value) ShuffleWires(consoles.FindAll(console => console.TaskTypes.Contains(TaskTypes.FixWiring)), random);
            if (Options.RandomUploadPosition.Value) MoveUpload(ship, consoles, random);
        }
        catch (Exception e)
        {
            Error($"Random task positions: could not move the consoles: {e}");
        }

        // Tasks given before: their arrows and rooms follow the consoles.
        foreach (var task in PlayerControl.LocalPlayer.myTasks)
        {
            var normal = task ? task.TryCast<NormalPlayerTask>() : null;
            if (!normal) continue;

            normal!.LocationDirty = true;
            normal.UpdateArrowAndLocation();
        }
    }

    // Each panel takes the place of another one, with what belongs to the place: sprite, room (shown in the task list,
    // the wiring task finds its panels with their id), reach.
    private static void ShuffleWires(List<Console> wires, System.Random random)
    {
        if (wires.Count < 2) return;

        var places = wires.Select(wire => (wire.transform.position, Renderer(wire)?.sprite, scale: Renderer(wire)?.transform.localScale, wire.Room, wire.usableDistance, wire.onlySameRoom, wire.checkWalls)).ToList();
        var shuffled = wires.OrderBy(_ => random.Next()).ToList();
        for (var i = 0; i < shuffled.Count; i++)
        {
            var wire = shuffled[i];
            var place = places[i];
            wire.transform.position = place.position;
            var renderer = Renderer(wire);
            if (renderer && place.sprite) renderer!.sprite = place.sprite;
            if (renderer && place.scale != null) renderer!.transform.localScale = place.scale.Value;
            wire.Room = place.Room;
            wire.usableDistance = place.usableDistance;
            wire.onlySameRoom = place.onlySameRoom;
            wire.checkWalls = place.checkWalls;
        }
    }

    // The upload swaps places with one of the downloads, or stays (one chance more than the downloads, as BOR).
    private static void MoveUpload(ShipStatus ship, List<Console> consoles, System.Random random)
    {
        // Found with their task (the names changed since BOR): the upload is the console of the task, the downloads
        // the consoles of its first step.
        var upload = consoles.Find(console => console.TaskTypes.Contains(TaskTypes.UploadData));
        var downloads = consoles.FindAll(console => console != upload && console.ValidTasks.Any(set => set.taskType == TaskTypes.UploadData && set.taskStep.Contains(0)));
        if (!upload || downloads.Count == 0) return;

        // Terminal of Admin smaller, wherever the upload goes (the console at its place has its image).
        var index = random.Next(downloads.Count + 1);
        var adminRenderer = Renderer(index == downloads.Count ? upload! : downloads[index]);
        if (index == downloads.Count)
        {
            ShrinkSkeldUpload(ship, adminRenderer);
            return;
        }

        var download = downloads[index];
        var uploadPosition = upload!.transform.position;
        var downloadPosition = download.transform.position;
        upload.transform.position = downloadPosition;
        download.transform.position = uploadPosition;
        if (ship.Type == ShipStatus.MapType.Pb && download.Room == SystemTypes.LifeSupp)
            download.transform.position = uploadPosition + new Vector3(0f, PolusLifeSupportShift, 0f);

        var uploadRenderer = Renderer(upload);
        var downloadRenderer = Renderer(download);
        if (uploadRenderer && downloadRenderer)
        {
            // With its scale (0.84 for the upload of The Skeld, 1.2 for the downloads): each place keeps its look.
            (uploadRenderer!.sprite, downloadRenderer!.sprite) = (downloadRenderer.sprite, uploadRenderer.sprite);
            (uploadRenderer.transform.localScale, downloadRenderer.transform.localScale) = (downloadRenderer.transform.localScale, uploadRenderer.transform.localScale);
        }

        var uploadCollider = upload.GetComponent<BoxCollider2D>();
        var downloadCollider = download.GetComponent<BoxCollider2D>();
        if (uploadCollider && downloadCollider)
            (uploadCollider.size, downloadCollider.size) = (downloadCollider.size, uploadCollider.size);
        (upload.usableDistance, download.usableDistance) = (download.usableDistance, upload.usableDistance);

        // The room of a console is no longer the one around it: walls checked instead.
        if (upload.onlySameRoom) download.checkWalls = true;
        if (download.onlySameRoom) upload.checkWalls = true;
        upload.onlySameRoom = false;
        download.onlySameRoom = false;

        ShrinkSkeldUpload(ship, adminRenderer);

        RelocatedRooms[upload.Room] = download.Room;
        RelocatedRooms[download.Room] = upload.Room;
        Info($"Random task positions: upload moved to {download.Room}");
    }
}
