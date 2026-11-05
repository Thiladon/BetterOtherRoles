using UnityEngine;

namespace BetterOtherRoles.Utilities;

/// <summary>
///     Sprite helpers.
/// </summary>
public static class BorSpriteUtils
{
    /// <summary>
    ///     Copies a sprite packed in an atlas (vanilla buttons, ...) into its own texture.
    ///     The cooldown shadow shader of the ability buttons fills the whole texture, so an atlas sprite
    ///     is only shaded once the shadow reaches its part of the atlas.
    /// </summary>
    /// <param name="sprite">The sprite to copy.</param>
    /// <returns>A sprite with its own texture, or the original sprite if the copy failed.</returns>
    public static Sprite ToStandalone(Sprite sprite)
    {
        try
        {
            var rect = sprite.textureRect;
            var source = sprite.texture;

            var renderTexture = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, renderTexture);

            var previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            var texture = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.ARGB32, false)
            {
                filterMode = source.filterMode,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.ReadPixels(rect, 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTexture);

            var pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
            // Rect of the texture: a sprite rect can be fractional (69.88 in a 69 px texture).
            var copy = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, sprite.pixelsPerUnit);
            copy.name = sprite.name;
            KeepLoaded(copy);
            return copy;
        }
        catch (Exception e)
        {
            Error($"Could not copy sprite {sprite.name}: {e}");
            return sprite;
        }
    }

    /// <summary>
    ///     The game unloads the unused assets between scenes (after a game): a sprite made by the mod and kept for later
    ///     would be destroyed (empty, zero bounds) while its C# reference stays. Kept loaded, with its texture.
    /// </summary>
    /// <param name="sprite">A sprite made by the mod.</param>
    public static void KeepLoaded(Sprite sprite)
    {
        sprite.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        sprite.texture.hideFlags |= HideFlags.DontUnloadUnusedAsset;
    }

    /// <summary>
    ///     White copy of a plain colored sprite (same shape, alpha, borders and pivot), to be tinted with any color.
    /// </summary>
    /// <param name="sprite">The sprite to copy.</param>
    /// <returns>The white sprite, or the original sprite if the copy failed.</returns>
    public static Sprite ToWhite(Sprite sprite)
    {
        var copy = ToStandalone(sprite);
        if (copy == sprite) return sprite;

        var texture = copy.texture;
        var pixels = texture.GetPixels32();
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color32(255, 255, 255, pixels[i].a);
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        var white = Sprite.Create(texture, copy.rect, copy.pivot / copy.rect.size, copy.pixelsPerUnit, 0, SpriteMeshType.FullRect, sprite.border);
        white.name = sprite.name + "_White";
        KeepLoaded(white);
        return white;
    }

    /// <summary>
    ///     Copy of a sprite with 9-slice borders, to stretch it (SpriteDrawMode.Sliced) without stretching its corners.
    /// </summary>
    /// <param name="sprite">The sprite to copy.</param>
    /// <param name="border">Borders in pixels: left, bottom, right, top.</param>
    /// <returns>The sliced sprite, or the original sprite if the copy failed.</returns>
    public static Sprite ToSliced(Sprite sprite, Vector4 border)
    {
        var copy = ToStandalone(sprite);
        if (copy == sprite) return sprite;

        var sliced = Sprite.Create(copy.texture, copy.rect, copy.pivot / copy.rect.size, copy.pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
        sliced.name = sprite.name + "_Sliced";

        // Kept for the next scenes: otherwise unloaded at the game end, as nothing of the new scene uses it yet.
        copy.texture.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        sliced.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        return sliced;
    }
}
