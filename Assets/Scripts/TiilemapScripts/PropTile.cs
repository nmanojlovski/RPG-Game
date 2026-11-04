using UnityEngine;
using UnityEngine.Tilemaps;

// A tile made in code from a normal sprite (decorations, rocks).
// The deco sprites are imported at 100 pixels per unit but the map tiles use 64, so the sprite
// is scaled up to match and moved so it stands on the bottom of its cell.
// If it gets more than one sprite, it plays them as an animation (the rocks in the water).
public class PropTile : TileBase
{
    // The map's tiles are 64 pixels per cell.
    private const float PixelsPerCell = 64f;
    private const float FramesPerSecond = 6f;

    // Where the bottom of the sprite sits in its cell (the cell's centre is 0, its bottom edge is -0.5).
    public const float GroundLevel = -0.4f;

    private Sprite[] frames;
    private Vector3 offset;
    private float scale;

    // Size of the sprite on the map, measured in cells.
    public Vector2 WorldSize { get; private set; }

    public static PropTile Create(Sprite[] frames)
    {
        PropTile tile = CreateInstance<PropTile>();
        tile.frames = frames;

        Sprite sprite = frames[0];
        tile.scale = sprite.pixelsPerUnit / PixelsPerCell;
        tile.WorldSize = sprite.bounds.size * tile.scale;

        // Centre the sprite left-right, and move it up or down so its bottom is on the ground line.
        float moveX = -sprite.bounds.center.x * tile.scale;
        float moveY = GroundLevel - sprite.bounds.min.y * tile.scale;
        tile.offset = new Vector3(moveX, moveY, 0f);

        return tile;
    }

    // Unity calls this to ask what to draw in a cell.
    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        tileData.sprite = frames[0];
        tileData.color = Color.white;
        tileData.colliderType = Tile.ColliderType.None;

        // The transform moves and scales the sprite inside its cell.
        tileData.transform = Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one * scale);

        // Lock the transform and colour so nothing else on the tilemap changes them.
        tileData.flags = TileFlags.LockTransform | TileFlags.LockColor;
    }

    // Unity calls this to ask whether the cell is animated.
    public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData tileAnimationData)
    {
        if (frames.Length < 2) return false;

        tileAnimationData.animatedSprites = frames;
        tileAnimationData.animationSpeed = FramesPerSecond;

        // Start each rock on a different frame so they don't all move at the same time.
        int startFrame = Mathf.Abs(position.x * 7 + position.y * 13) % frames.Length;
        tileAnimationData.animationStartTime = startFrame / FramesPerSecond;
        return true;
    }
}
