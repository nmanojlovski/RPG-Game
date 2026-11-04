using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum MapCellKind
{
    Water,
    Sand,
    Grass,
    Covered,
    Blocked
}

[System.Serializable]
public class SpriteAnimation
{
    public Sprite[] frames;
}

[System.Serializable]
public class MapTheme
{
    public string name = "Meadow";
    public Color groundTint = Color.white;
    [Min(1)] public int beachWidth = 1;
    public float treesPer100Cells = 1.2f;

    [Tooltip("Solid props the player has to walk around.")]
    public Sprite[] obstacles = new Sprite[0];
    public float obstaclesPer100Cells = 0.5f;

    [Tooltip("Small props on the grass that can be walked over.")]
    public Sprite[] decorations = new Sprite[0];
    [Range(0f, 1f)] public float decorationChance = 0.05f;

    [Tooltip("Animated rocks out in the sea.")]
    public SpriteAnimation[] waterRocks = new SpriteAnimation[0];
    public float waterRocksPer100WaterCells;
}

// Builds a flat island map at load time: noise + cellular automata for the coastline,
// 4-neighbour autotiling for grass/sand edges, and themed obstacles that never cut off part of the island.
public class ProceduralMapGenerator : MonoBehaviour
{
    public static ProceduralMapGenerator Instance { get; private set; }

    [Header("Tilemaps")]
    [SerializeField] private Tilemap sandTilemap;
    [SerializeField] private Tilemap grassTilemap;
    [SerializeField] private Tilemap decorationTilemap;
    [SerializeField] private Tilemap treeTilemap;
    [SerializeField] private Tilemap collisionTilemap;

    [Header("Edge tiles, indexed by neighbour mask (N=1, E=2, S=4, W=8)")]
    [SerializeField] private TileBase[] sandTiles = new TileBase[16];
    [SerializeField] private TileBase[] grassTiles = new TileBase[16];
    [SerializeField] private TileBase[] grassDecorations;

    [Header("Tree (offsets are relative to the trunk cell)")]
    [SerializeField] private TileBase[] treeTiles;
    [SerializeField] private Vector2Int[] treeTileOffsets;

    [Header("Themes (map 1 uses the first one, then they repeat)")]
    [SerializeField] private MapTheme[] themes;

    [Header("Scene objects fitted to the generated map")]
    [SerializeField] private Transform player;
    [SerializeField] private SpriteRenderer water;
    [SerializeField] private BoxCollider2D cameraConfiner;
    [SerializeField] private BoxCollider2D enemySpawnArea;

    [Header("Map size (grows with every new map)")]
    [SerializeField] private Vector2Int baseSize = new Vector2Int(28, 18);
    [SerializeField] private Vector2Int sizeGrowthPerMap = new Vector2Int(4, 2);
    [SerializeField] private Vector2Int maxSize = new Vector2Int(48, 28);
    [SerializeField] private int waterMargin = 3;

    [Header("Island shape")]
    [SerializeField, Range(0f, 1f)] private float landFillPercent = 0.72f;
    [SerializeField] private int smoothingIterations = 5;
    [SerializeField] private int minLakeSize = 12;
    [SerializeField, Range(0f, 1f)] private float minIslandFraction = 0.4f;

    [Header("Obstacles")]
    [SerializeField] private int minObstacleSpacing = 3;
    [SerializeField] private int playerClearRadius = 3;

    [Header("Seed")]
    [SerializeField] private bool useFixedSeed;
    [SerializeField] private int fixedSeed = 12345;
    [SerializeField] private int levelsPerMap = 3;

    public int Seed { get; private set; }
    public int MapNumber { get; private set; }
    public MapTheme Theme { get; private set; }
    public Rect WorldBounds { get; private set; }

    public int Width
    {
        get { return width; }
    }

    public int Height
    {
        get { return height; }
    }

    public Vector2Int PlayerCell
    {
        get { return playerCell; }
    }

    // One placed obstacle or water rock.
    private class PlacedProp
    {
        public Vector2Int cell;
        public PropTile tile; // null for a tree
    }

    private int width;
    private int height;
    private bool[,] land;
    private bool[,] grass;
    private bool[,] covered;
    private bool[,] blocked;
    private readonly List<PlacedProp> obstacles = new List<PlacedProp>();
    private readonly List<PlacedProp> waterRocks = new List<PlacedProp>();
    private readonly List<Vector2Int> spawnCells = new List<Vector2Int>();
    private readonly List<Vector2Int> spawnRing = new List<Vector2Int>();
    private readonly Dictionary<Sprite, PropTile> propTiles = new Dictionary<Sprite, PropTile>();
    private Vector2Int playerCell;
    private System.Random rng;
    private Tile collisionTile;

    private static readonly Vector2Int[] FourDirections =
    {
        Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
    };

    void Awake()
    {
        Instance = this;

        int level = 1;
        if (GameManager.Instance != null)
        {
            level = GameManager.Instance.level;
        }
        int mapNumber = level / levelsPerMap + 1;

        // The seed comes from the computer's clock, so every map is different.
        int seed = System.Environment.TickCount;
        if (useFixedSeed)
        {
            seed = fixedSeed;
        }

        Generate(mapNumber, seed);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Generate(int mapNumber, int seed)
    {
        MapNumber = Mathf.Max(1, mapNumber);

        // The themes take turns: map 1 uses the first one, map 2 the second, and so on.
        if (themes != null && themes.Length > 0)
        {
            Theme = themes[(MapNumber - 1) % themes.Length];
        }
        else
        {
            Theme = new MapTheme();
        }

        int growthSteps = MapNumber - 1;
        width = Mathf.Min(baseSize.x + sizeGrowthPerMap.x * growthSteps, maxSize.x);
        height = Mathf.Min(baseSize.y + sizeGrowthPerMap.y * growthSteps, maxSize.y);
        int interiorCells = (width - 2 * waterMargin) * (height - 2 * waterMargin);

        // A rare unlucky seed can produce a tiny island - just roll the next one.
        for (int attempt = 0; attempt < 10; attempt++)
        {
            Seed = seed + attempt;
            rng = new System.Random(Seed);
            if (GenerateIsland() >= interiorCells * minIslandFraction) break;
        }

        grass = land;
        for (int i = 0; i < Theme.beachWidth; i++)
        {
            grass = ErodeToInterior(grass);
        }

        covered = new bool[width, height];
        blocked = new bool[width, height];
        playerCell = FindPlayerCell();

        obstacles.Clear();
        PlaceObstacles(Theme.obstaclesPer100Cells, Theme.obstacles);
        PlaceObstacles(Theme.treesPer100Cells, null);
        PlaceWaterRocks();
        CollectSpawnCells();

        PaintTilemaps();
        FitSceneObjects();

        Debug.Log($"[MapGen] Map {MapNumber} '{Theme.name}': seed {Seed}, {width}x{height}, {obstacles.Count} obstacles, {waterRocks.Count} water rocks, {spawnCells.Count} spawnable cells");
    }

    public bool IsWalkable(Vector2 worldPosition)
    {
        Vector2Int c = WorldToMapCell(worldPosition);
        return InBounds(c.x, c.y) && land[c.x, c.y] && !blocked[c.x, c.y];
    }

    // Picks a cell in a ring around the player, so enemies show up nearby but not on top of them.
    public Vector2 GetRandomSpawnPoint(float minDistanceFromPlayer, float maxDistanceFromPlayer)
    {
        if (player == null) return CellCenter(spawnCells[Random.Range(0, spawnCells.Count)]);

        Vector2 playerPosition = player.position;
        spawnRing.Clear();
        Vector2Int closestFarEnough = spawnCells[0];
        float closestFarEnoughDistance = float.MaxValue;

        foreach (Vector2Int cell in spawnCells)
        {
            float distance = Vector2.Distance(CellCenter(cell), playerPosition);
            if (distance < minDistanceFromPlayer) continue;

            if (distance <= maxDistanceFromPlayer) spawnRing.Add(cell);
            if (distance < closestFarEnoughDistance)
            {
                closestFarEnoughDistance = distance;
                closestFarEnough = cell;
            }
        }

        // On a narrow strip of island the ring can be empty - fall back to the nearest cell that's still far enough.
        if (spawnRing.Count == 0)
        {
            return CellCenter(closestFarEnough);
        }

        Vector2Int chosen = spawnRing[Random.Range(0, spawnRing.Count)];
        return CellCenter(chosen);
    }

    public MapCellKind GetCellKind(int x, int y)
    {
        if (!land[x, y]) return MapCellKind.Water;
        if (blocked[x, y]) return MapCellKind.Blocked;
        if (covered[x, y]) return MapCellKind.Covered;
        if (grass[x, y]) return MapCellKind.Grass;
        return MapCellKind.Sand;
    }

    // ---------- Island shape ----------

    private int GenerateIsland()
    {
        land = new bool[width, height];
        Vector2 center = new Vector2((width - 1) / 2f, (height - 1) / 2f);
        Vector2 radius = new Vector2(width / 2f - waterMargin, height / 2f - waterMargin);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (IsInMargin(x, y)) continue;

                // 0 in the middle, 1 at the water margin: land gets less likely towards the edge.
                float dx = (x - center.x) / radius.x;
                float dy = (y - center.y) / radius.y;
                float edge = dx * dx + dy * dy;
                float chance = landFillPercent + 0.4f * (1f - edge) - 0.25f;

                land[x, y] = rng.NextDouble() < chance;
            }
        }

        for (int i = 0; i < smoothingIterations; i++)
        {
            land = Smooth(land);
        }

        int islandSize = KeepLargestIsland();
        FillSmallLakes();
        return islandSize;
    }

    private bool[,] Smooth(bool[,] source)
    {
        bool[,] result = new bool[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (IsInMargin(x, y)) continue;

                int neighbours = CountLandNeighbours(source, x, y);
                if (neighbours > 4) result[x, y] = true;
                else if (neighbours < 4) result[x, y] = false;
                else result[x, y] = source[x, y];
            }
        }

        return result;
    }

    private int CountLandNeighbours(bool[,] map, int x, int y)
    {
        int count = 0;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx;
                int ny = y + dy;
                if (InBounds(nx, ny) && map[nx, ny]) count++;
            }
        }
        return count;
    }

    private int KeepLargestIsland()
    {
        List<List<Vector2Int>> islands = FindRegions(true);
        if (islands.Count == 0) return 0;

        List<Vector2Int> largest = islands[0];
        foreach (List<Vector2Int> island in islands)
        {
            if (island.Count > largest.Count) largest = island;
        }

        foreach (List<Vector2Int> island in islands)
        {
            if (island == largest) continue;
            foreach (Vector2Int c in island)
            {
                land[c.x, c.y] = false;
            }
        }

        return largest.Count;
    }

    private void FillSmallLakes()
    {
        foreach (List<Vector2Int> region in FindRegions(false))
        {
            // Water that reaches the edge of the map is the sea, not a lake.
            bool isSea = false;
            foreach (Vector2Int c in region)
            {
                if (c.x == 0 || c.y == 0 || c.x == width - 1 || c.y == height - 1)
                {
                    isSea = true;
                }
            }

            if (isSea || region.Count >= minLakeSize) continue;

            foreach (Vector2Int c in region)
            {
                land[c.x, c.y] = true;
            }
        }
    }

    private List<List<Vector2Int>> FindRegions(bool isLand)
    {
        List<List<Vector2Int>> regions = new List<List<Vector2Int>>();
        bool[,] visited = new bool[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (visited[x, y] || land[x, y] != isLand) continue;

                List<Vector2Int> region = new List<Vector2Int>();
                Queue<Vector2Int> queue = new Queue<Vector2Int>();
                queue.Enqueue(new Vector2Int(x, y));
                visited[x, y] = true;

                while (queue.Count > 0)
                {
                    Vector2Int c = queue.Dequeue();
                    region.Add(c);

                    foreach (Vector2Int dir in FourDirections)
                    {
                        Vector2Int n = c + dir;
                        if (!InBounds(n.x, n.y) || visited[n.x, n.y] || land[n.x, n.y] != isLand) continue;
                        visited[n.x, n.y] = true;
                        queue.Enqueue(n);
                    }
                }

                regions.Add(region);
            }
        }

        return regions;
    }

    // Each erosion pass pulls the grass one tile further in, widening the sand beach.
    private bool[,] ErodeToInterior(bool[,] source)
    {
        bool[,] result = new bool[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                result[x, y] = source[x, y] && CountLandNeighbours(source, x, y) == 8;
            }
        }
        return result;
    }

    // ---------- Player, obstacles, spawn points ----------

    private Vector2Int FindPlayerCell()
    {
        Vector2 center = new Vector2(width / 2f, height / 2f);
        Vector2Int best = new Vector2Int(-1, -1);
        float bestDistance = float.MaxValue;
        bool bestIsOpenGrass = false;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!land[x, y]) continue;

                bool openGrass = grass[x, y] && CountLandNeighbours(grass, x, y) == 8;
                float distance = Vector2.Distance(center, new Vector2(x, y));

                if ((openGrass && !bestIsOpenGrass) || (openGrass == bestIsOpenGrass && distance < bestDistance))
                {
                    best = new Vector2Int(x, y);
                    bestDistance = distance;
                    bestIsOpenGrass = openGrass;
                }
            }
        }

        return best;
    }

    // Pass null for props to place trees instead.
    private void PlaceObstacles(float per100Cells, Sprite[] props)
    {
        bool isTree = props == null;
        if (isTree && (treeTiles == null || treeTiles.Length == 0)) return;
        if (!isTree && props.Length == 0) return;

        int target = Mathf.RoundToInt(width * height / 100f * per100Cells);
        int reachable = CountReachableCells();
        int placed = 0;

        // Trying every land cell (in random order) fills small islands as reliably as big ones.
        foreach (Vector2Int cell in ShuffledLandCells())
        {
            if (placed >= target) break;

            // A tree always covers the same cells; a prop covers as many cells as its sprite needs.
            PropTile prop = null;
            Vector2Int[] footprint = treeTileOffsets;
            if (!isTree)
            {
                Sprite sprite = props[rng.Next(props.Length)];
                prop = GetPropTile(new Sprite[] { sprite });
                footprint = Footprint(prop);
            }

            if (!CanPlaceObstacle(cell, footprint, isTree)) continue;

            // Only keep it if blocking this cell doesn't cut any part of the island off.
            blocked[cell.x, cell.y] = true;
            int reachableAfter = CountReachableCells();
            if (reachableAfter != reachable - 1)
            {
                blocked[cell.x, cell.y] = false;
                continue;
            }

            reachable = reachableAfter;
            foreach (Vector2Int offset in footprint)
            {
                covered[cell.x + offset.x, cell.y + offset.y] = true;
            }

            PlacedProp obstacle = new PlacedProp();
            obstacle.cell = cell;
            obstacle.tile = prop;
            obstacles.Add(obstacle);
            placed++;
        }
    }

    private List<Vector2Int> ShuffledLandCells()
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (land[x, y]) cells.Add(new Vector2Int(x, y));
            }
        }

        // Fisher-Yates shuffle: swap every cell with a random cell before it.
        for (int i = cells.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            Vector2Int temp = cells[i];
            cells[i] = cells[j];
            cells[j] = temp;
        }
        return cells;
    }

    private bool CanPlaceObstacle(Vector2Int cell, Vector2Int[] footprint, bool needsGrass)
    {
        if (Chebyshev(cell, playerCell) <= playerClearRadius) return false;

        foreach (Vector2Int offset in footprint)
        {
            Vector2Int c = cell + offset;
            if (!InBounds(c.x, c.y) || !land[c.x, c.y] || covered[c.x, c.y]) return false;
            if (needsGrass && !grass[c.x, c.y]) return false;
        }

        foreach (PlacedProp other in obstacles)
        {
            if (Chebyshev(cell, other.cell) < minObstacleSpacing) return false;
        }

        return true;
    }

    // Cells a prop's sprite covers, so nothing spawns underneath it.
    private static Vector2Int[] Footprint(PropTile prop)
    {
        Vector2 size = prop.WorldSize;
        int minX = Mathf.FloorToInt(0.5f - size.x / 2f);
        int maxX = Mathf.CeilToInt(size.x / 2f - 0.5f);
        int maxY = Mathf.Max(0, Mathf.CeilToInt(PropTile.GroundLevel + size.y - 0.5f));

        List<Vector2Int> cells = new List<Vector2Int>();
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = 0; y <= maxY; y++)
            {
                cells.Add(new Vector2Int(x, y));
            }
        }
        return cells.ToArray();
    }

    private void PlaceWaterRocks()
    {
        waterRocks.Clear();
        SpriteAnimation[] rocks = Theme.waterRocks;
        if (rocks == null || rocks.Length == 0) return;

        int waterCells = 0;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!land[x, y]) waterCells++;
            }
        }
        int target = Mathf.RoundToInt(waterCells / 100f * Theme.waterRocksPer100WaterCells);

        for (int attempt = 0; attempt < target * 25 && waterRocks.Count < target; attempt++)
        {
            Vector2Int cell = new Vector2Int(rng.Next(width), rng.Next(height));
            if (HasLandWithin(cell, 2) || IsNearAnotherRock(cell)) continue;

            PlacedProp rock = new PlacedProp();
            rock.cell = cell;
            rock.tile = GetPropTile(rocks[rng.Next(rocks.Length)].frames);
            waterRocks.Add(rock);
        }
    }

    private bool IsNearAnotherRock(Vector2Int cell)
    {
        foreach (PlacedProp rock in waterRocks)
        {
            if (Chebyshev(rock.cell, cell) < 3) return true;
        }
        return false;
    }

    private bool HasLandWithin(Vector2Int cell, int distance)
    {
        for (int x = cell.x - distance; x <= cell.x + distance; x++)
        {
            for (int y = cell.y - distance; y <= cell.y + distance; y++)
            {
                if (InBounds(x, y) && land[x, y]) return true;
            }
        }
        return false;
    }

    private int CountReachableCells()
    {
        bool[,] visited = new bool[width, height];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(playerCell);
        visited[playerCell.x, playerCell.y] = true;
        int count = 0;

        while (queue.Count > 0)
        {
            Vector2Int c = queue.Dequeue();
            count++;

            foreach (Vector2Int dir in FourDirections)
            {
                Vector2Int n = c + dir;
                if (!InBounds(n.x, n.y) || visited[n.x, n.y] || !land[n.x, n.y] || blocked[n.x, n.y]) continue;
                visited[n.x, n.y] = true;
                queue.Enqueue(n);
            }
        }

        return count;
    }

    private void CollectSpawnCells()
    {
        spawnCells.Clear();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (land[x, y] && !covered[x, y]) spawnCells.Add(new Vector2Int(x, y));
            }
        }
    }

    // ---------- Painting ----------

    private void PaintTilemaps()
    {
        if (collisionTile == null)
        {
            collisionTile = ScriptableObject.CreateInstance<Tile>();
            collisionTile.colliderType = Tile.ColliderType.Grid;
        }

        sandTilemap.ClearAllTiles();
        grassTilemap.ClearAllTiles();
        decorationTilemap.ClearAllTiles();
        treeTilemap.ClearAllTiles();
        collisionTilemap.ClearAllTiles();

        sandTilemap.color = Theme.groundTint;
        grassTilemap.color = Theme.groundTint;

        int decorationOptions = grassDecorations.Length + Theme.decorations.Length;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3Int cell = ToTilemapCell(x, y);

                if (!land[x, y])
                {
                    if (CountLandNeighbours(land, x, y) > 0) collisionTilemap.SetTile(cell, collisionTile);
                    continue;
                }

                sandTilemap.SetTile(cell, sandTiles[NeighbourMask(land, x, y)]);

                if (!grass[x, y]) continue;

                int grassMask = NeighbourMask(grass, x, y);
                grassTilemap.SetTile(cell, grassTiles[grassMask]);

                bool canDecorate = grassMask == 15 && !covered[x, y] && new Vector2Int(x, y) != playerCell;
                if (canDecorate && decorationOptions > 0 && rng.NextDouble() < Theme.decorationChance)
                {
                    // Pick from the grass tufts (every map) and the theme's own decorations together.
                    int pick = rng.Next(decorationOptions);
                    TileBase decoration;
                    if (pick < grassDecorations.Length)
                    {
                        decoration = grassDecorations[pick];
                    }
                    else
                    {
                        Sprite sprite = Theme.decorations[pick - grassDecorations.Length];
                        decoration = GetPropTile(new Sprite[] { sprite });
                    }
                    decorationTilemap.SetTile(cell, decoration);
                }
            }
        }

        foreach (PlacedProp obstacle in obstacles)
        {
            Vector2Int c = obstacle.cell;
            collisionTilemap.SetTile(ToTilemapCell(c.x, c.y), collisionTile);

            if (obstacle.tile == null)
            {
                for (int i = 0; i < treeTiles.Length; i++)
                {
                    Vector2Int t = c + treeTileOffsets[i];
                    treeTilemap.SetTile(ToTilemapCell(t.x, t.y), treeTiles[i]);
                }
            }
            else if (obstacle.tile.WorldSize.y > 1f)
            {
                // Tall props draw over the player, so the player can walk behind them.
                treeTilemap.SetTile(ToTilemapCell(c.x, c.y), obstacle.tile);
            }
            else
            {
                decorationTilemap.SetTile(ToTilemapCell(c.x, c.y), obstacle.tile);
            }
        }

        foreach (PlacedProp rock in waterRocks)
        {
            decorationTilemap.SetTile(ToTilemapCell(rock.cell.x, rock.cell.y), rock.tile);
        }
    }

    private PropTile GetPropTile(Sprite[] frames)
    {
        // Each sprite only needs to be turned into a tile once.
        if (propTiles.ContainsKey(frames[0]))
        {
            return propTiles[frames[0]];
        }

        PropTile tile = PropTile.Create(frames);
        propTiles.Add(frames[0], tile);
        return tile;
    }

    // Each side adds its own number (up=1, right=2, down=4, left=8), so every combination
    // of neighbours gives a different number from 0 to 15 - one for each edge tile.
    private int NeighbourMask(bool[,] map, int x, int y)
    {
        int mask = 0;
        if (InBounds(x, y + 1) && map[x, y + 1]) mask += 1;
        if (InBounds(x + 1, y) && map[x + 1, y]) mask += 2;
        if (InBounds(x, y - 1) && map[x, y - 1]) mask += 4;
        if (InBounds(x - 1, y) && map[x - 1, y]) mask += 8;
        return mask;
    }

    // ---------- Fitting scene objects ----------

    private void FitSceneObjects()
    {
        Vector2 min = grassTilemap.CellToWorld(ToTilemapCell(0, 0));
        Vector2 max = grassTilemap.CellToWorld(ToTilemapCell(width, height));
        Vector2 center = (min + max) / 2f;
        Vector2 size = max - min;
        WorldBounds = new Rect(min, size);

        FitBox(cameraConfiner, center, size);
        FitBox(enemySpawnArea, center, size);

        if (water != null && water.sprite != null)
        {
            Vector2 spriteSize = water.sprite.bounds.size;
            Vector3 parentScale = Vector3.one;
            if (water.transform.parent != null)
            {
                parentScale = water.transform.parent.lossyScale;
            }
            water.transform.position = new Vector3(center.x, center.y, water.transform.position.z);
            water.transform.localScale = new Vector3(
                (size.x + 4f) / spriteSize.x / parentScale.x,
                (size.y + 4f) / spriteSize.y / parentScale.y,
                1f);
        }

        if (player != null)
        {
            Vector2 spawn = CellCenter(playerCell);
            player.position = new Vector3(spawn.x, spawn.y, player.position.z);
        }

        // The confiner caches its shape and the camera would otherwise pan over from the old player position.
        CinemachineConfiner2D confiner = FindAnyObjectByType<CinemachineConfiner2D>();
        if (confiner != null) confiner.InvalidateBoundingShapeCache();

        foreach (CinemachineCamera cam in FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
        {
            cam.PreviousStateIsValid = false;
        }
    }

    private static void FitBox(BoxCollider2D box, Vector2 worldCenter, Vector2 worldSize)
    {
        if (box == null) return;

        Vector3 scale = box.transform.lossyScale;
        box.offset = box.transform.InverseTransformPoint(worldCenter);
        box.size = new Vector2(worldSize.x / Mathf.Abs(scale.x), worldSize.y / Mathf.Abs(scale.y));
    }

    // ---------- Coordinates ----------

    private Vector3Int ToTilemapCell(int x, int y)
    {
        return new Vector3Int(x - width / 2, y - height / 2, 0);
    }

    private Vector2Int WorldToMapCell(Vector2 worldPosition)
    {
        Vector3Int cell = grassTilemap.WorldToCell(worldPosition);
        return new Vector2Int(cell.x + width / 2, cell.y + height / 2);
    }

    private Vector2 CellCenter(Vector2Int c)
    {
        return grassTilemap.GetCellCenterWorld(ToTilemapCell(c.x, c.y));
    }

    private bool InBounds(int x, int y)
    {
        return x >= 0 && y >= 0 && x < width && y < height;
    }

    private bool IsInMargin(int x, int y)
    {
        return x < waterMargin || y < waterMargin || x >= width - waterMargin || y >= height - waterMargin;
    }

    // Distance in cells when a diagonal step also counts as 1 (how many "rings" apart two cells are).
    private static int Chebyshev(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }
}
