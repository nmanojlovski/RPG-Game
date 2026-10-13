using UnityEngine;
using UnityEngine.Tilemaps;

// A tile built at runtime from loose sprites (deco, rocks) so they line up with the 64px tile grid,
// regardless of the sprites' own import scale and pivot. More than one frame makes it animate.
public class PropTile : TileBase
{
    private const float ArtPixelsPerCell = 64f;
    private const float FramesPerSecond = 6f;

    private Sprite[] frames;
    private Matrix4x4 placement;

    // Size of the sprite on the map, in cells.
    public Vector2 WorldSize { get; private set; }

    // Where the sprite's bottom sits inside its cell (cell centre is 0, cell bottom is -0.5).
    public const float GroundLevel = -0.4f;

    public static PropTile Create(Sprite[] frames)
    {
        var tile = CreateInstance<PropTile>();
        tile.frames = frames;

        Sprite first = frames[0];
        float scale = first.pixelsPerUnit / ArtPixelsPerCell;
        Bounds bounds = first.bounds;

        tile.WorldSize = bounds.size * scale;
        tile.placement = Matrix4x4.TRS(
            new Vector3(-bounds.center.x * scale, GroundLevel - bounds.min.y * scale, 0f),
            Quaternion.identity,
            Vector3.one * scale);

        return tile;
    }

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        tileData.sprite = frames[0];
        tileData.transform = placement;
        tileData.color = Color.white;
        tileData.flags = TileFlags.LockTransform | TileFlags.LockColor;
        tileData.colliderType = Tile.ColliderType.None;
    }

    public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData tileAnimationData)
    {
        if (frames.Length < 2) return false;

        tileAnimationData.animatedSprites = frames;
        tileAnimationData.animationSpeed = FramesPerSecond;
        // Offset each rock so they don't all bob in sync.
        tileAnimationData.animationStartTime = Mathf.Abs(position.x * 7 + position.y * 13) % frames.Length / FramesPerSecond;
        return true;
    }
}
