using UnityEngine;

// Small factory for the sprite GameObjects every screen is made of.
public static class Nodes
{
    public static Transform Group(Transform parent, string name, Vector2 position = default)
    {
        GameObject group = new GameObject(name);
        group.transform.SetParent(parent, false);
        group.transform.localPosition = position;
        return group.transform;
    }

    public static SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, Vector2 position, int order,
        Color? color = null)
    {
        GameObject node = new GameObject(name);
        node.transform.SetParent(parent, false);
        node.transform.localPosition = position;
        SpriteRenderer renderer = node.AddComponent<SpriteRenderer>();
        renderer.sharedMaterial = Art.SpriteMaterial;
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        renderer.color = color ?? Color.white;
        return renderer;
    }

    // Solid rectangle from the 1x1 pixel sprite; position is the bottom-left corner.
    public static SpriteRenderer Rect(Transform parent, string name, Vector2 position, Vector2 size, Color color,
        int order)
    {
        SpriteRenderer renderer = Sprite(parent, name, Art.Pixel, position, order, color);
        renderer.transform.localScale = new Vector3(size.x, size.y, 1f);
        return renderer;
    }

    public static void SetRect(SpriteRenderer renderer, Vector2 position, Vector2 size)
    {
        renderer.transform.localPosition = position;
        renderer.transform.localScale = new Vector3(Mathf.Max(0f, size.x), Mathf.Max(0f, size.y), 1f);
    }

    public static SpriteRenderer Panel(Transform parent, string name, Vector2 position, Vector2 size, int order)
    {
        SpriteRenderer renderer = Sprite(parent, name, Art.Sliced("UI/panel", 8), position, order);
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = size;
        return renderer;
    }

    public static void Destroy(Object target)
    {
        if (target == null)
            return;
        if (Application.isPlaying)
            Object.Destroy(target);
        else
            Object.DestroyImmediate(target);
    }

    public static Vector2 Snap(Vector2 value)
    {
        return new Vector2(Mathf.Round(value.x), Mathf.Round(value.y));
    }
}
