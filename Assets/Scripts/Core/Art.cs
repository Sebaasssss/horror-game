using System.Collections.Generic;
using UnityEngine;

// Loads pixel-art textures from Resources and slices them into 1-pixel-per-unit sprites at runtime.
public static class Art
{
    private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
    private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, Sprite[]> sheets = new Dictionary<string, Sprite[]>();
    private static Material spriteMaterial;
    private static Shader spriteShader;

    public static Material SpriteMaterial
    {
        get
        {
            if (spriteMaterial == null)
            {
                spriteShader = Resources.Load<Shader>("Shaders/RetroSprite");
                spriteMaterial = new Material(spriteShader != null ? spriteShader : Shader.Find("Sprites/Default"))
                {
                    name = "Retro Sprite (runtime)",
                    hideFlags = HideFlags.DontSave
                };
            }
            return spriteMaterial;
        }
    }

    public static Texture2D Texture(string path)
    {
        if (textures.TryGetValue(path, out Texture2D cached) && cached != null)
            return cached;

        Texture2D texture = Resources.Load<Texture2D>(path);
        if (texture == null)
        {
            Debug.LogWarning("Missing texture: Resources/" + path);
            return null;
        }
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        textures[path] = texture;
        return texture;
    }

    public static Sprite Sprite(string path)
    {
        return Sprite(path, new Vector2(0.5f, 0.5f));
    }

    public static Sprite Sprite(string path, Vector2 pivot)
    {
        string key = path + "|" + pivot.x + "," + pivot.y;
        if (sprites.TryGetValue(key, out Sprite cached) && cached != null)
            return cached;

        Texture2D texture = Texture(path);
        if (texture == null)
            return null;

        Sprite sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, 1f,
            0, SpriteMeshType.FullRect);
        sprite.name = path;
        sprites[key] = sprite;
        return sprite;
    }

    public static Sprite Sliced(string path, int border)
    {
        string key = path + "|sliced" + border;
        if (sprites.TryGetValue(key, out Sprite cached) && cached != null)
            return cached;

        Texture2D texture = Texture(path);
        if (texture == null)
            return null;

        Sprite sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
            Vector2.zero, 1f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        sprites[key] = sprite;
        return sprite;
    }

    // Grid sheet, cells read left-to-right, top-to-bottom.
    public static Sprite[] Sheet(string path, int cellWidth, int cellHeight, Vector2 pivot)
    {
        string key = path + "|" + cellWidth + "x" + cellHeight + "|" + pivot.x + "," + pivot.y;
        if (sheets.TryGetValue(key, out Sprite[] cached))
            return cached;

        Texture2D texture = Texture(path);
        if (texture == null)
            return new Sprite[0];

        int columns = texture.width / cellWidth;
        int rows = texture.height / cellHeight;
        Sprite[] result = new Sprite[columns * rows];
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                Rect rect = new Rect(column * cellWidth, texture.height - (row + 1) * cellHeight, cellWidth, cellHeight);
                result[row * columns + column] = UnityEngine.Sprite.Create(texture, rect, pivot, 1f, 0,
                    SpriteMeshType.FullRect);
            }
        }
        sheets[key] = result;
        return result;
    }

    public static Sprite Pixel => Sprite("UI/pixel", Vector2.zero);
}
