using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum MapCellKind
{
    Water,
    Sand,
    Grass,
    TreeCanopy,
    TreeTrunk
}

// Builds a flat island map at load time: noise + cellular automata for the coastline,
// 4-neighbour autotiling for grass/sand edges, and trees that never cut off part of the island.
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

    [Header("Scene objects fitted to the generated map")]
    [SerializeField] private Transform player;
    [SerializeField] private SpriteRenderer water;
    [SerializeField] private BoxCollider2D cameraConfiner;
    [SerializeField] private BoxCollider2D enemySpawnArea;

    [Header("Map size (grows with every new map)")]
    [SerializeField] private Vector2Int baseSize = new Vector2Int(40, 26);
    [SerializeField] private Vector2Int sizeGrowthPerMap = new Vector2Int(6, 4);
    [SerializeField] private Vector2Int maxSize = new Vector2Int(70, 46);
    [SerializeField] private int waterMargin = 3;

    [Header("Island shape")]
    [SerializeField, Range(0f, 1f)] private float landFillPercent = 0.72f;
    [SerializeField] private int smoothingIterations = 5;
    [SerializeField] private int minLakeSize = 12;
    [SerializeField] private int minIslandSize = 300;

    [Header("Trees")]
    [SerializeField] private float treesPer100Cells = 1.2f;
    [SerializeField] private float extraTreesPer100CellsPerMap = 0.3f;
    [SerializeField] private int minTreeSpacing = 3;
    [SerializeField] private int playerClearRadius = 5;

    [Header("Decorations")]
    [SerializeField, Range(0f, 1f)] private float decorationChance = 0.05f;

    [Header("Seed")]
    [SerializeField] private bool useFixedSeed;
    [SerializeField] private int fixedSeed = 12345;
    [SerializeField] private int levelsPerMap = 3;

    public int Seed { get; private set; }
    public int MapNumber { get; private set; }
    public int Width => width;
    public int Height => height;
    public Vector2Int PlayerCell => playerCell;
    public Rect WorldBounds { get; private set; }

    private int width;
    private int height;
    private bool[,] land;
    private bool[,] grass;
    private bool[,] canopy;
    private bool[,] blocked;
    private readonly List<Vector2Int> trunkCells = new List<Vector2Int>();
    private readonly List<Vector2Int> spawnCells = new List<Vector2Int>();
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

        int level = GameManager.Instance != null ? GameManager.Instance.level : 1;
        int mapNumber = level / levelsPerMap + 1;
        int seed = useFixedSeed ? fixedSeed : System.Environment.TickCount;

        Generate(mapNumber, seed);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Generate(int mapNumber, int seed)
    {
        MapNumber = Mathf.Max(1, mapNumber);
        int growthSteps = MapNumber - 1;
        width = Mathf.Min(baseSize.x + sizeGrowthPerMap.x * growthSteps, maxSize.x);
        height = Mathf.Min(baseSize.y + sizeGrowthPerMap.y * growthSteps, maxSize.y);

        // A rare unlucky seed can produce a tiny island - just roll the next one.
        for (int attempt = 0; attempt < 10; attempt++)
        {
            Seed = seed + attempt;
            rng = new System.Random(Seed);
            if (GenerateIsland() >= minIslandSize) break;
        }

        grass = ErodeToInterior(land);
        canopy = new bool[width, height];
        blocked = new bool[width, height];
        playerCell = FindPlayerCell();
        PlaceTrees();
        CollectSpawnCells();

        PaintTilemaps();
        FitSceneObjects();

        Debug.Log($"[MapGen] Map {MapNumber}: seed {Seed}, {width}x{height}, {trunkCells.Count} trees, {spawnCells.Count} spawnable cells");
    }

    public bool IsWalkable(Vector2 worldPosition)
    {
        Vector2Int c = WorldToMapCell(worldPosition);
        return InBounds(c.x, c.y) && land[c.x, c.y] && !blocked[c.x, c.y];
    }

    public Vector2 GetRandomSpawnPoint(float minDistanceFromPlayer)
    {
        Vector2 position = CellCenter(spawnCells[Random.Range(0, spawnCells.Count)]);

        for (int i = 0; i < 30; i++)
        {
            if (player == null || Vector2.Distance(position, player.position) >= minDistanceFromPlayer)
            {
                return position;
            }
            position = CellCenter(spawnCells[Random.Range(0, spawnCells.Count)]);
        }

        return position;
    }

    public MapCellKind GetCellKind(int x, int y)
    {
        if (!land[x, y]) return MapCellKind.Water;
        if (blocked[x, y]) return MapCellKind.TreeTrunk;
        if (canopy[x, y]) return MapCellKind.TreeCanopy;
        return grass[x, y] ? MapCellKind.Grass : MapCellKind.Sand;
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
                int nx = x + dx, ny = y + dy;
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
        foreach (var island in islands)
        {
            if (island.Count > largest.Count) largest = island;
        }

        foreach (var island in islands)
        {
            if (island == largest) continue;
            foreach (var c in island) land[c.x, c.y] = false;
        }

        return largest.Count;
    }

    private void FillSmallLakes()
    {
        foreach (var region in FindRegions(false))
        {
            bool isSea = region.Exists(c => c.x == 0 || c.y == 0 || c.x == width - 1 || c.y == height - 1);
            if (isSea || region.Count >= minLakeSize) continue;

            foreach (var c in region) land[c.x, c.y] = true;
        }
    }

    private List<List<Vector2Int>> FindRegions(bool isLand)
    {
        var regions = new List<List<Vector2Int>>();
        bool[,] visited = new bool[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (visited[x, y] || land[x, y] != isLand) continue;

                var region = new List<Vector2Int>();
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(new Vector2Int(x, y));
                visited[x, y] = true;

                while (queue.Count > 0)
                {
                    Vector2Int c = queue.Dequeue();
                    region.Add(c);

                    foreach (var dir in FourDirections)
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

    // Grass sits one tile inside the sand so the coast always shows a beach ring.
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

    // ---------- Player, trees, spawn points ----------

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

    private void PlaceTrees()
    {
        trunkCells.Clear();
        if (treeTiles == null || treeTiles.Length == 0) return;

        float density = treesPer100Cells + extraTreesPer100CellsPerMap * (MapNumber - 1);
        int targetTrees = Mathf.RoundToInt(width * height / 100f * density);
        int reachable = CountReachableCells();

        for (int attempt = 0; attempt < targetTrees * 25 && trunkCells.Count < targetTrees; attempt++)
        {
            Vector2Int trunk = new Vector2Int(rng.Next(width), rng.Next(height));
            if (!CanPlaceTree(trunk)) continue;

            // Only keep the tree if blocking its trunk doesn't cut any part of the island off.
            blocked[trunk.x, trunk.y] = true;
            int reachableAfter = CountReachableCells();
            if (reachableAfter != reachable - 1)
            {
                blocked[trunk.x, trunk.y] = false;
                continue;
            }

            reachable = reachableAfter;
            trunkCells.Add(trunk);
            foreach (var offset in treeTileOffsets)
            {
                Vector2Int c = trunk + offset;
                canopy[c.x, c.y] = true;
            }
        }
    }

    private bool CanPlaceTree(Vector2Int trunk)
    {
        if (Chebyshev(trunk, playerCell) <= playerClearRadius) return false;

        foreach (var offset in treeTileOffsets)
        {
            Vector2Int c = trunk + offset;
            if (!InBounds(c.x, c.y) || !grass[c.x, c.y]) return false;
        }

        foreach (var other in trunkCells)
        {
            if (Chebyshev(trunk, other) < minTreeSpacing) return false;
        }

        return true;
    }

    private int CountReachableCells()
    {
        bool[,] visited = new bool[width, height];
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(playerCell);
        visited[playerCell.x, playerCell.y] = true;
        int count = 0;

        while (queue.Count > 0)
        {
            Vector2Int c = queue.Dequeue();
            count++;

            foreach (var dir in FourDirections)
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
                if (land[x, y] && !blocked[x, y] && !canopy[x, y]) spawnCells.Add(new Vector2Int(x, y));
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

                bool canDecorate = grassMask == 15 && !canopy[x, y] && new Vector2Int(x, y) != playerCell;
                if (canDecorate && grassDecorations.Length > 0 && rng.NextDouble() < decorationChance)
                {
                    decorationTilemap.SetTile(cell, grassDecorations[rng.Next(grassDecorations.Length)]);
                }
            }
        }

        foreach (var trunk in trunkCells)
        {
            collisionTilemap.SetTile(ToTilemapCell(trunk.x, trunk.y), collisionTile);
            for (int i = 0; i < treeTiles.Length; i++)
            {
                Vector2Int c = trunk + treeTileOffsets[i];
                treeTilemap.SetTile(ToTilemapCell(c.x, c.y), treeTiles[i]);
            }
        }
    }

    private int NeighbourMask(bool[,] map, int x, int y)
    {
        int mask = 0;
        if (InBounds(x, y + 1) && map[x, y + 1]) mask |= 1;
        if (InBounds(x + 1, y) && map[x + 1, y]) mask |= 2;
        if (InBounds(x, y - 1) && map[x, y - 1]) mask |= 4;
        if (InBounds(x - 1, y) && map[x - 1, y]) mask |= 8;
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
            Vector3 parentScale = water.transform.parent != null ? water.transform.parent.lossyScale : Vector3.one;
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
        var confiner = FindAnyObjectByType<CinemachineConfiner2D>();
        if (confiner != null) confiner.InvalidateBoundingShapeCache();

        foreach (var cam in FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
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

    private static int Chebyshev(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }
}
