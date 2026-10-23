using System.Collections.Generic;
using UnityEngine;

// Vertical arcade menu: medium-font items, skull cursor, blinking selection.
public class MenuList
{
    private readonly List<PixelText> items = new List<PixelText>();
    private readonly SpriteRenderer cursorLeft, cursorRight;
    private readonly AudioManager audio;
    private readonly float top;
    private readonly float spacing;
    private readonly float x;
    private int tick;
    public int Selected { get; private set; }

    public MenuList(Transform parent, AudioManager audio, string[] labels, float top, float spacing = 20f, float x = 0f)
    {
        this.audio = audio;
        this.top = top;
        this.spacing = spacing;
        this.x = x;
        for (int i = 0; i < labels.Length; i++)
            items.Add(new PixelText(parent, "medium", labels[i], new Vector2(x, top - i * spacing), TextAlign.Center,
                Layers.Screen + 5));
        Sprite skull = Art.Sprite("UI/skull_on");
        cursorLeft = Nodes.Sprite(parent, "Cursor", skull, Vector2.zero, Layers.Screen + 5);
        cursorRight = Nodes.Sprite(parent, "Cursor", skull, Vector2.zero, Layers.Screen + 5);
        Refresh();
    }

    public void SetLabel(int index, string label)
    {
        items[index].Set(label);
        Refresh();
    }

    // Returns the confirmed index, or -1.
    public int Tick(InputFrame input)
    {
        tick++;
        if (input.upPressed || input.downPressed)
        {
            Selected = (Selected + (input.downPressed ? 1 : -1) + items.Count) % items.Count;
            audio?.Sfx("ui_move", 0.7f, 0f);
            Refresh();
        }
        for (int i = 0; i < items.Count; i++)
        {
            bool active = i == Selected;
            items[i].Color = active ? (tick / 8 % 2 == 0 ? Color.white : new Color(1f, 0.85f, 0.5f))
                : new Color(0.55f, 0.45f, 0.45f);
        }
        if (input.confirm)
        {
            audio?.Sfx("ui_select", 0.8f, 0f);
            return Selected;
        }
        return -1;
    }

    private void Refresh()
    {
        PixelText item = items[Selected];
        float half = item.Width / 2f + 12f;
        float y = top - Selected * spacing + 7f;
        cursorLeft.transform.localPosition = new Vector3(Mathf.Round(x - half), y, 0f);
        cursorRight.transform.localPosition = new Vector3(Mathf.Round(x + half), y, 0f);
    }
}
