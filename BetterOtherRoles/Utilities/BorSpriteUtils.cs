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
            return copy;
        }
        catch (Exception e)
        {
            Error($"Could not copy sprite {sprite.name}: {e}");
            return sprite;
        }
    }

    /// <summary>
    ///     White copy of a plain colored sprite (same shape, alpha, borders and pivot), to be tinted with any color.
    /// </summary>
    /// <param name="sprite">The sprite to copy.</param>
    /// <returns>The white sprite, or the original sprite if the copy failed.</returns>
    public static Sprite ToWhite(Sprite sprite)
    {
        var copy = ToStandalone(sprite);
        if (copy == sprite)
        {
            return sprite;
        }

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
        if (copy == sprite)
        {
            return sprite;
        }

        var sliced = Sprite.Create(copy.texture, copy.rect, copy.pivot / copy.rect.size, copy.pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
        sliced.name = sprite.name + "_Sliced";

        // Kept for the next scenes: otherwise unloaded at the game end, as nothing of the new scene uses it yet.
        copy.texture.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        sliced.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        return sliced;
    }

    private static Sprite? _closeButton;
    private static Sprite? _closeButtonHover;

    /// <summary>
    ///     Draws a close button like the game settings one: dark rounded square, white frame and cross with rounded ends.
    /// </summary>
    /// <param name="hover">Turquoise frame and cross, as the game settings button under the mouse.</param>
    /// <returns>The sprite (created once).</returns>
    public static Sprite CreateCloseButtonSprite(bool hover)
    {
        var cached = hover ? _closeButtonHover : _closeButton;
        if (cached)
        {
            return cached!;
        }

        // Proportions measured on the game button (40 px): corners of 8 px, frame of 4 px, cross of 16 px.
        const int size = 128;
        const float half = size / 2f;
        const float radius = 24f;
        const float frameWidth = 13f;
        const float crossReach = 19f;
        const float crossHalfWidth = 7.5f;
        var background = new Color(16 / 255f, 16 / 255f, 16 / 255f);
        var foreground = hover ? new Color(44 / 255f, 235 / 255f, 198 / 255f) : Color.white;

        var pixels = new Color32[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var px = x + 0.5f - half;
                var py = y + 0.5f - half;

                // Signed distance to the rounded square (negative inside).
                var qx = Mathf.Abs(px) - (half - radius);
                var qy = Mathf.Abs(py) - (half - radius);
                var outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                var border = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;

                // Distance to the two diagonal segments of the cross.
                var cross = Mathf.Min(SegmentDistance(px, py, crossReach, crossReach), SegmentDistance(px, py, crossReach, -crossReach));

                var frameCoverage = Mathf.Clamp01(border + frameWidth + 0.5f);
                var crossCoverage = Mathf.Clamp01(crossHalfWidth + 0.5f - cross);
                var color = Color.Lerp(background, foreground, Mathf.Max(frameCoverage, crossCoverage));
                color.a = Mathf.Clamp01(0.5f - border);
                pixels[(y * size) + x] = color;
            }
        }

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels);
        texture.Apply();
        texture.hideFlags |= HideFlags.HideAndDontSave;

        var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags |= HideFlags.HideAndDontSave;
        if (hover)
        {
            _closeButtonHover = sprite;
        }
        else
        {
            _closeButton = sprite;
        }

        return sprite;
    }

    // Distance from (px, py) to the segment going from (-ex, -ey) to (ex, ey).
    private static float SegmentDistance(float px, float py, float ex, float ey)
    {
        var t = Mathf.Clamp(((px * ex) + (py * ey)) / ((ex * ex) + (ey * ey)), -1f, 1f);
        return new Vector2(px - (t * ex), py - (t * ey)).magnitude;
    }

    private static Sprite? _trash;
    private static Sprite? _pencil;

    /// <summary>
    ///     Draws a trash can icon (lid with a handle, body with three slots), in the color of the other preset icons.
    /// </summary>
    /// <param name="color">Color of the icon.</param>
    /// <returns>The sprite (created once).</returns>
    public static Sprite CreateTrashSprite(Color color)
    {
        if (_trash)
        {
            return _trash!;
        }

        const int size = 128;
        var pixels = new Color32[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var px = x + 0.5f;
                var py = y + 0.5f;

                // Body: narrower at the bottom, rounded corners, three slots.
                var bodyHalf = Mathf.Lerp(30f, 36f, Mathf.InverseLerp(14f, 86f, py));
                var body = py is >= 14f and <= 86f && Mathf.Abs(px - 64f) <= bodyHalf;
                var slot = py is >= 26f and <= 74f && (Mathf.Abs(px - 64f) <= 4f || Mathf.Abs(px - 44f) <= 4f || Mathf.Abs(px - 84f) <= 4f);

                // Lid and handle.
                var lid = py is >= 92f and <= 102f && Mathf.Abs(px - 64f) <= 44f;
                var handle = py is >= 102f and <= 114f && Mathf.Abs(px - 64f) <= 14f && !(py <= 108f && Mathf.Abs(px - 64f) <= 7f);

                var filled = (body && !slot) || lid || handle;
                pixels[(y * size) + x] = filled ? color : new Color(color.r, color.g, color.b, 0f);
            }
        }

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels);
        texture.Apply();
        texture.hideFlags |= HideFlags.HideAndDontSave;

        _trash = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        _trash.hideFlags |= HideFlags.HideAndDontSave;
        return _trash;
    }

    /// <summary>
    ///     Draws a pencil icon (point down left, body, eraser up right), in the color of the other preset icons.
    /// </summary>
    /// <param name="color">Color of the icon.</param>
    /// <returns>The sprite (created once).</returns>
    public static Sprite CreatePencilSprite(Color color)
    {
        if (_pencil)
        {
            return _pencil!;
        }

        const int size = 128;
        const float halfWidth = 13f;
        var pixels = new Color32[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                // Along the pencil (u, from the point to the eraser) and across it (v), on the diagonal.
                var dx = x + 0.5f - 64f;
                var dy = y + 0.5f - 64f;
                var u = (dx + dy) * 0.7071f;
                var v = Mathf.Abs(dy - dx) * 0.7071f;

                var point = u is >= -56f and < -30f && v <= halfWidth * (u + 56f) / 26f;
                var body = u is >= -26f and <= 34f && v <= halfWidth;
                var eraser = u is >= 39f and <= 54f && v <= halfWidth;

                var filled = point || body || eraser;
                pixels[(y * size) + x] = filled ? color : new Color(color.r, color.g, color.b, 0f);
            }
        }

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels);
        texture.Apply();
        texture.hideFlags |= HideFlags.HideAndDontSave;

        _pencil = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        _pencil.hideFlags |= HideFlags.HideAndDontSave;
        return _pencil;
    }

    /// <summary>
    ///     Average color of the opaque pixels of a sprite (atlas sprites included), or white.
    /// </summary>
    public static Color GetMainColor(Sprite sprite)
    {
        var copy = ToStandalone(sprite);
        if (copy == sprite)
        {
            return Color.white;
        }

        var sum = Vector4.zero;
        var count = 0;
        foreach (var pixel in copy.texture.GetPixels32())
        {
            if (pixel.a > 200)
            {
                sum += new Vector4(pixel.r, pixel.g, pixel.b, 255f);
                count++;
            }
        }

        return count == 0 ? Color.white : new Color(sum.x / count / 255f, sum.y / count / 255f, sum.z / count / 255f, 1f);
    }
}
