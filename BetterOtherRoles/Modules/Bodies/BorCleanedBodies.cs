using System.Collections;
using BetterOtherRoles.Options;
using BetterOtherRoles.Utilities;
using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.GameOptions;
using MiraAPI.Utilities.Assets;
using Reactor.Utilities;
using UnityEngine;

namespace BetterOtherRoles.Modules.Bodies;

/// <summary>
///     Bodies cleaned by a role (Cleaner, Vulture... and the Debug menu), as in TOU-Mira 1.7.9 CoCleanCustom and
///     VitalsBodyPatches. The body fades out, then shows on the vitals as set by
///     <see cref="GameplayOptions.CleanedBodies"/>:
///     <list type="bullet">
///         <item>missing: the body stays hidden (not reportable) and the panel gets the "MIS" background;</item>
///         <item>dead: the body stays hidden, the panel shows a dead player as in the game;</item>
///         <item>disconnected: the body is destroyed, so the game shows a disconnected player.</item>
///     </list>
///     A hidden body is kept: the game decides from the bodies left whether a dead player shows as disconnected.
///     The players cleaned are forgotten at each round start, when the game removes the bodies, and when revived.
/// </summary>
public static class BorCleanedBodies
{
    private const float FadeDuration = 1f;

    private static readonly LoadableAsset<Sprite> MissingAsset =
        new LoadableResourceAsset("BetterOtherRoles.Resources.Vitals.VitalBgMissing.png");

    private static readonly Dictionary<byte, CleanedBody> Cleaned = [];

    private static Sprite? _missing;

    /// <summary>
    ///     Gets the "MIS" background of the vitals.
    /// </summary>
    public static Sprite MissingSprite
    {
        get
        {
            if (_missing) return _missing!;

            _missing = MissingAsset.LoadAsset();
            BorSpriteUtils.KeepLoaded(_missing);
            return _missing;
        }
    }

    /// <summary>
    ///     Gets how a cleaned player shows on the vitals.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="mode">How the player shows, when cleaned.</param>
    /// <returns>Whether the player was cleaned.</returns>
    public static bool TryGet(byte playerId, out CleanedBody mode) => Cleaned.TryGetValue(playerId, out mode);

    /// <summary>
    ///     Cleans a body, on every client (a role's RPC calls it on each): it fades out, then is hidden or destroyed.
    /// </summary>
    /// <param name="body">The body.</param>
    public static void Clean(DeadBody body)
    {
        if (!body || body.Reported) return;

        // Not reportable from now on, even while it fades out.
        body.Reported = true;
        Coroutines.Start(CoClean(body));
    }

    /// <summary>
    ///     Forgets a player cleaned, who comes back to life (revive).
    /// </summary>
    /// <param name="playerId">The player.</param>
    public static void Forget(byte playerId) => Cleaned.Remove(playerId);

    /// <summary>
    ///     Forgets the players cleaned at each round start: the game removes the bodies after a meeting.
    /// </summary>
    /// <param name="event">The event.</param>
    [RegisterEvent]
    public static void RoundStartHandler(RoundStartEvent @event) => Cleaned.Clear();

    private static IEnumerator CoClean(DeadBody body)
    {
        var parentId = body.ParentId;
        var renderers = body.GetComponentsInChildren<SpriteRenderer>(true);
        var alphas = renderers.Select(x => x.color.a).ToArray();
        for (var time = 0f; time < FadeDuration; time += Time.deltaTime)
        {
            if (!body) yield break;

            for (var i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i]) continue;

                var color = renderers[i].color;
                color.a = alphas[i] * (1f - time / FadeDuration);
                renderers[i].color = color;
            }

            yield return null;
        }

        if (!body) yield break;

        var mode = (CleanedBody)OptionGroupSingleton<GameplayOptions>.Instance.CleanedBodies.Value;
        Cleaned[parentId] = mode;
        if (mode == CleanedBody.Disconnected)
        {
            UnityEngine.Object.Destroy(body.gameObject);
            yield break;
        }

        body.myCollider.enabled = false;
        foreach (var renderer in renderers.Where(x => x))
        {
            renderer.enabled = false;
        }
    }
}
