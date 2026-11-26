using System.Globalization;
using MiraAPI.Utilities;
using UnityEngine;

namespace BetterOtherRoles.Modules.Debug;

/// <summary>
///     Debug menu: notes the position of the local player (map, room and coordinates, as given to RpcSnapTo), written in
///     the log and copied to the clipboard, to choose the spawn points of the rooms.
/// </summary>
public static class BorDebugPosition
{
    /// <summary>
    ///     Notes the position of the local player.
    /// </summary>
    public static void Note()
    {
        var player = PlayerControl.LocalPlayer;
        var ship = ShipStatus.Instance;
        if (!player || !ship) return;

        var position = (Vector2)player.transform.position;
        var room = RoomAt(ship, position);
        var roomText = room ? $"{room!.RoomId} ({TranslationController.Instance.GetString(room.RoomId)})" : "Outside";
        var text = string.Create(CultureInfo.InvariantCulture, $"{MapName()} | {roomText} | ({position.x:0.00}, {position.y:0.00})");

        GUIUtility.systemCopyBuffer = text;
        Info($"[Position] {text}");
        Helpers.CreateAndShowNotification($"<b>{text}</b>", Color.white, new Vector3(0f, 1f, -20f));
    }

    // The room whose area holds the point, a true room before a hallway.
    private static PlainShipRoom? RoomAt(ShipStatus ship, Vector2 point)
    {
        PlainShipRoom? hallway = null;
        foreach (var room in ship.AllRooms)
        {
            if (!room || !room.roomArea || !room.roomArea.OverlapPoint(point)) continue;

            if (room.RoomId != SystemTypes.Hallway) return room;

            hallway = room;
        }

        return hallway;
    }

    private static MapNames MapName()
    {
        return TutorialManager.InstanceExists
            ? (MapNames)AmongUsClient.Instance.TutorialMapId
            : (MapNames)GameOptionsManager.Instance.CurrentGameOptions.MapId;
    }
}
