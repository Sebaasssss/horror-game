using UnityEngine;

// Draws a Fighter: body sprite (mirrored with scale), floor shadow and the white hit flash.
public class FighterView
{
    public readonly Fighter fighter;
    private readonly Transform root;
    private readonly SpriteRenderer body;
    private readonly SpriteRenderer shadow;
    private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
    private static readonly int FlashId = Shader.PropertyToID("_Flash");
    private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");

    public FighterView(Fighter fighter, Transform parent)
    {
        this.fighter = fighter;
        root = Nodes.Group(parent, fighter.def.Name + " P" + (fighter.index + 1));
        shadow = Nodes.Sprite(parent, "Shadow P" + (fighter.index + 1), Art.Sprite("UI/shadow"), Vector2.zero,
            Layers.Shadows);
        body = Nodes.Sprite(root, "Body", fighter.sprite, Vector2.zero, Layers.Fighter);
    }

    public void Sync(bool inFront)
    {
        Vector2 snapped = Nodes.Snap(fighter.pos);
        root.localPosition = snapped;
        body.sprite = fighter.sprite;
        body.enabled = !fighter.hidden;
        body.sortingOrder = inFront ? Layers.FighterFront : Layers.Fighter;
        body.transform.localScale = new Vector3(fighter.facing, 1f, 1f);
        body.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Round(fighter.rotation / 15f) * 15f);

        float flash = fighter.flashTicks > 0 ? (fighter.flashTicks % 2 == 1 ? 0.8f : 0.3f) : 0f;
        body.GetPropertyBlock(block);
        block.SetFloat(FlashId, flash);
        block.SetColor(FlashColorId, fighter.flashColor);
        body.SetPropertyBlock(block);

        shadow.enabled = !fighter.hidden;
        float height = Mathf.Clamp01(fighter.pos.y / 120f);
        shadow.transform.localPosition = new Vector3(snapped.x, 0f, 0f);
        shadow.transform.localScale = new Vector3(1f - height * 0.5f, 1f, 1f);
        shadow.color = new Color(1f, 1f, 1f, 1f - height * 0.6f);
    }
}
