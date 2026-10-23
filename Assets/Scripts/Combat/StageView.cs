using System.Collections.Generic;
using UnityEngine;

// Parallax background: each layer's bottom edge sits groundY pixels below the floor line.
public class StageView
{
    private readonly List<(SpriteRenderer renderer, float parallax)> layers = new List<(SpriteRenderer, float)>();
    private readonly float groundY;

    public StageView(Transform parent, string stageId)
    {
        StageData data = StageData.Load(stageId) ?? StageData.Load("fallback");
        Transform root = Nodes.Group(parent, "Stage");
        groundY = data != null ? data.groundY : GameConfig.GroundScreenOffset;
        if (data == null)
            return;

        for (int i = 0; i < data.layers.Length; i++)
        {
            StageLayer layer = data.layers[i];
            Sprite sprite = Art.Sprite("Stages/" + data.id + "/" + layer.file, new Vector2(0.5f, 0f));
            if (sprite == null)
                continue;
            SpriteRenderer renderer = Nodes.Sprite(root, layer.file, sprite, new Vector2(0f, -groundY),
                Layers.StageBack + i);
            layers.Add((renderer, layer.parallax));
        }
    }

    public void Sync(float cameraX)
    {
        foreach ((SpriteRenderer renderer, float parallax) in layers)
            renderer.transform.localPosition = new Vector3(Mathf.Round(cameraX * (1f - parallax)), -groundY, 0f);
    }

    public void SetTint(Color color)
    {
        foreach ((SpriteRenderer renderer, float _) in layers)
            renderer.color = color;
    }
}
