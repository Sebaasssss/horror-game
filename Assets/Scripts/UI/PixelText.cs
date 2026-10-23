using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PixelFontData
{
    public string name, chars;
    public int cellW, cellH, columns, advance, lineHeight, originX, originY, glyphW, glyphH;
}

[Serializable]
public class PixelFontList
{
    public PixelFontData[] fonts;
}

// Bitmap font baked by Tools/pixelart/ui.py (outline and shadow are part of each glyph cell).
public class PixelFont
{
    private static Dictionary<string, PixelFont> fonts;
    public readonly PixelFontData data;
    private readonly Dictionary<char, Sprite> glyphs = new Dictionary<char, Sprite>();

    private PixelFont(PixelFontData data)
    {
        this.data = data;
        Vector2 pivot = new Vector2(data.originX / (float)data.cellW, data.originY / (float)data.cellH);
        Sprite[] cells = Art.Sheet("UI/font_" + data.name, data.cellW, data.cellH, pivot);
        for (int i = 0; i < data.chars.Length && i < cells.Length; i++)
            glyphs[data.chars[i]] = cells[i];
    }

    public static PixelFont Get(string name)
    {
        if (fonts == null)
        {
            fonts = new Dictionary<string, PixelFont>();
            TextAsset json = Resources.Load<TextAsset>("UI/fonts");
            PixelFontList list = JsonUtility.FromJson<PixelFontList>(json.text);
            foreach (PixelFontData data in list.fonts)
                fonts[data.name] = new PixelFont(data);
        }
        return fonts.TryGetValue(name, out PixelFont font) ? font : fonts["small"];
    }

    public Sprite Glyph(char c)
    {
        c = char.ToUpperInvariant(c);
        c = c switch
        {
            'Á' => 'A', 'É' => 'E', 'Í' => 'I', 'Ó' => 'O', 'Ú' => 'U', 'Ñ' => 'N', _ => c
        };
        return glyphs.TryGetValue(c, out Sprite sprite) ? sprite : null;
    }

    public int Width(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        return (text.Length - 1) * data.advance + data.glyphW;
    }

    public int Height => data.glyphH;
}

public enum TextAlign { Left, Center, Right }

// A line of bitmap text built from pooled SpriteRenderers. Position is the baseline anchor.
public class PixelText
{
    public readonly Transform root;
    private readonly List<SpriteRenderer> glyphs = new List<SpriteRenderer>();
    private PixelFont font;
    private string text = "";
    private TextAlign align;
    private Color color = Color.white;
    private int order;
    private float wave;
    private int waveTick;

    public PixelText(Transform parent, string fontName, string value, Vector2 position, TextAlign align, int order,
        Color? color = null)
    {
        root = Nodes.Group(parent, "Text", position);
        font = PixelFont.Get(fontName);
        this.align = align;
        this.order = order;
        this.color = color ?? Color.white;
        Set(value);
    }

    public string Text => text;
    public int Width => font.Width(text);

    public Vector2 Position
    {
        get => root.localPosition;
        set => root.localPosition = new Vector3(Mathf.Round(value.x), Mathf.Round(value.y), 0f);
    }

    public bool Visible
    {
        get => root.gameObject.activeSelf;
        set => root.gameObject.SetActive(value);
    }

    public Color Color
    {
        get => color;
        set
        {
            color = value;
            foreach (SpriteRenderer glyph in glyphs)
                glyph.color = value;
        }
    }

    // Vertical sine wobble per letter (used by banners and the title).
    public float Wave
    {
        get => wave;
        set => wave = value;
    }

    public void Set(string value)
    {
        value ??= "";
        if (value == text && glyphs.Count >= value.Length)
            return;
        text = value;
        while (glyphs.Count < text.Length)
            glyphs.Add(Nodes.Sprite(root, "g", null, Vector2.zero, order, color));

        int width = font.Width(text);
        int start = align == TextAlign.Left ? 0 : align == TextAlign.Center ? -Mathf.RoundToInt(width / 2f) : -width;
        for (int i = 0; i < glyphs.Count; i++)
        {
            SpriteRenderer glyph = glyphs[i];
            if (i >= text.Length)
            {
                glyph.enabled = false;
                continue;
            }
            glyph.enabled = true;
            glyph.sprite = font.Glyph(text[i]);
            glyph.color = color;
            glyph.sortingOrder = order;
            glyph.transform.localPosition = new Vector3(start + i * font.data.advance, 0f, 0f);
        }
    }

    public void Tick()
    {
        if (wave <= 0f)
            return;
        waveTick++;
        int width = font.Width(text);
        int start = align == TextAlign.Left ? 0 : align == TextAlign.Center ? -Mathf.RoundToInt(width / 2f) : -width;
        for (int i = 0; i < text.Length && i < glyphs.Count; i++)
        {
            float y = Mathf.Round(Mathf.Sin((waveTick * 0.12f) + i * 0.6f) * wave);
            glyphs[i].transform.localPosition = new Vector3(start + i * font.data.advance, y, 0f);
        }
    }

    public void SetOrder(int value)
    {
        order = value;
        foreach (SpriteRenderer glyph in glyphs)
            glyph.sortingOrder = value;
    }

    public void Destroy()
    {
        Nodes.Destroy(root.gameObject);
    }
}
