using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class ProceduralLevelSetup
{
    private const string SourceScenePath = "Assets/Scenes/Levels/Level 1.unity";
    private const string ScenePath = "Assets/Scenes/Levels/ProceduralLevel.unity";
    private const string FlatTilesFolder = "Assets/Sprites/Tile Palettes";
    private const string TreeTilesFolder = "Assets/Sprites/Threes";
    private const string DecoFolder = "Assets/Sprites/Deco/Deco";
    private const string WaterRocksFolder = "Assets/Sprites/Water/Rocks";
    private const string EnemyPrefabFolder = "Assets/Sprites/Prefabs";
    private const int TreeSortingOrder = 10; // above the player (5), so the player walks behind canopies

    // Enemies appear 5-8 units away: just outside their 5-unit detection range, around the screen edge.
    private const float MinSpawnDistance = 5f;
    private const float MaxSpawnDistance = 8f;

    // Like the old hand-made levels: one spawner per enemy type, with new types joining on maps 3 and 5.
    private static readonly (string prefab, int firstMap)[] SpawnerSetup =
    {
        ("Enemy", 1), ("EnemyPawn", 3), ("LancerEnemy", 5)
    };

    // Hand-made pieces of Level 1 (elevation, borders, painted props) that the generator replaces.
    private static readonly string[] HandMadeObjects =
    {
        "Collision-High", "Collision-Low", "Non-Collision-Low", "Non-Collision-High",
        "MountainBoundary", "Animations", "Borders", "ForbidenArea1"
    };

    private static readonly string[] TunableSettings =
    {
        "baseSize", "sizeGrowthPerMap", "maxSize", "waterMargin", "landFillPercent", "smoothingIterations",
        "minLakeSize", "minIslandFraction", "minObstacleSpacing", "playerClearRadius"
    };

    // Tilemap_Flat sprite number for each neighbour mask (N=1, E=2, S=4, W=8).
    private static readonly int[] GrassSpriteByMask = { 29, 21, 26, 18, 3, 13, 0, 10, 28, 20, 27, 19, 2, 12, 1, 11 };
    private static readonly int[] SandSpriteByMask = { 33, 25, 30, 22, 8, 17, 5, 14, 32, 24, 31, 23, 7, 16, 6, 15 };
    private const int GrassDecorationSprite = 4;

    [MenuItem("Tools/Procedural Map/Create Procedural Level Scene")]
    public static void CreateProceduralLevelScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            Debug.LogError($"{ScenePath} already exists. Delete it first if you want to rebuild it.");
            return;
        }

        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (!AssetDatabase.CopyAsset(SourceScenePath, ScenePath))
        {
            throw new System.Exception($"Could not copy {SourceScenePath} to {ScenePath}");
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        foreach (string name in HandMadeObjects)
        {
            GameObject handMade = FindInScene(scene, name);
            if (handMade != null) Object.DestroyImmediate(handMade);
        }

        // Hand-placed enemies could land in the water on a random map; the spawner fills the map instead.
        foreach (EnemyMovement enemy in Object.FindObjectsByType<EnemyMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(enemy.gameObject);
            Object.DestroyImmediate(root != null ? root : enemy.gameObject);
        }

        Tilemap grass = FindInScene(scene, "Grass").GetComponent<Tilemap>();
        grass.ClearAllTiles();
        grass.transform.localPosition = Vector3.zero;
        TilemapRenderer grassRenderer = grass.GetComponent<TilemapRenderer>();
        Transform grid = grass.transform.parent;

        Tilemap sand = CreateTilemap(grid, "Sand", grassRenderer, grassRenderer.sortingOrder - 1);
        sand.transform.SetSiblingIndex(grass.transform.GetSiblingIndex());
        Tilemap decorations = CreateTilemap(grid, "Decorations", grassRenderer, grassRenderer.sortingOrder + 1);
        Tilemap trees = CreateTilemap(grid, "Trees", grassRenderer, TreeSortingOrder);
        Tilemap collision = CreateTilemap(grid, "Collision", null, 0);
        collision.gameObject.AddComponent<TilemapCollider2D>();

        EnemySpawner spawner = Object.FindAnyObjectByType<EnemySpawner>();
        var spawnerSettings = new SerializedObject(spawner);
        spawnerSettings.FindProperty("forbiddenAreas").arraySize = 0;
        spawnerSettings.ApplyModifiedPropertiesWithoutUndo();
        ConfigureSpawners(spawner);

        CinemachineConfiner2D confiner = Object.FindAnyObjectByType<CinemachineConfiner2D>();

        var generator = new GameObject("Map Generator").AddComponent<ProceduralMapGenerator>();
        var settings = new SerializedObject(generator);

        SetReference(settings, "sandTilemap", sand);
        SetReference(settings, "grassTilemap", grass);
        SetReference(settings, "decorationTilemap", decorations);
        SetReference(settings, "treeTilemap", trees);
        SetReference(settings, "collisionTilemap", collision);
        SetReference(settings, "player", FindInScene(scene, "Player").transform);
        SetReference(settings, "water", FindInScene(scene, "Water").GetComponent<SpriteRenderer>());
        SetReference(settings, "cameraConfiner", confiner != null ? confiner.BoundingShape2D as BoxCollider2D : null);
        SetReference(settings, "enemySpawnArea", spawner.spawnArea);

        Dictionary<string, TileBase> flatTiles = LoadTilesBySpriteName(FlatTilesFolder);
        SetArray(settings, "grassTiles", GrassSpriteByMask.Select(i => flatTiles["Tilemap_Flat_" + i]).ToArray());
        SetArray(settings, "sandTiles", SandSpriteByMask.Select(i => flatTiles["Tilemap_Flat_" + i]).ToArray());
        SetArray(settings, "grassDecorations", new[] { flatTiles["Tilemap_Flat_" + GrassDecorationSprite] });

        LoadTreeTiles(out TileBase[] treeTiles, out Vector2Int[] treeOffsets);
        SetArray(settings, "treeTiles", treeTiles);
        SerializedProperty offsets = settings.FindProperty("treeTileOffsets");
        offsets.arraySize = treeOffsets.Length;
        for (int i = 0; i < treeOffsets.Length; i++)
        {
            offsets.GetArrayElementAtIndex(i).vector2IntValue = treeOffsets[i];
        }

        SetDefaultThemes(settings.FindProperty("themes"));
        settings.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AddToBuildSettings(ScenePath);

        Debug.Log($"[MapGen] Created {ScenePath} ({treeTiles.Length} tree tiles: {string.Join(", ", treeOffsets)})");
    }

    // Brings the existing scene up to the current default map sizes, themes and spawners, keeping its scene references.
    [MenuItem("Tools/Procedural Map/Apply Default Settings")]
    public static void ApplyDefaultSettings()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ConfigureSpawners(FindInScene(scene, "EnemySpawner").GetComponent<EnemySpawner>());
        var settings = new SerializedObject(Object.FindAnyObjectByType<ProceduralMapGenerator>());

        var defaults = new GameObject("Generator Defaults").AddComponent<ProceduralMapGenerator>();
        var defaultSettings = new SerializedObject(defaults);
        foreach (string name in TunableSettings)
        {
            settings.CopyFromSerializedProperty(defaultSettings.FindProperty(name));
        }
        Object.DestroyImmediate(defaults.gameObject);

        SetDefaultThemes(settings.FindProperty("themes"));
        settings.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[MapGen] Applied default map sizes, themes and spawners to {ScenePath}");
    }

    private static void ConfigureSpawners(EnemySpawner template)
    {
        foreach (EnemySpawner other in Object.FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None))
        {
            if (other != template) Object.DestroyImmediate(other.gameObject);
        }

        for (int i = 0; i < SpawnerSetup.Length; i++)
        {
            (string prefabName, int firstMap) = SpawnerSetup[i];
            EnemySpawner spawner = template;
            if (i > 0)
            {
                spawner = new GameObject($"EnemySpawner ({prefabName})").AddComponent<EnemySpawner>();
                EditorUtility.CopySerialized(template, spawner);
            }

            var settings = new SerializedObject(spawner);
            SerializedProperty enemyTypes = settings.FindProperty("enemyTypes");
            enemyTypes.arraySize = 1;
            SerializedProperty entry = enemyTypes.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("prefab").objectReferenceValue = LoadEnemyPrefab(prefabName);
            entry.FindPropertyRelative("weight").floatValue = 1f;
            entry.FindPropertyRelative("unlockDifficulty").floatValue = 0f;
            settings.FindProperty("firstMap").intValue = firstMap;
            settings.FindProperty("minSpawnDistanceFromPlayer").floatValue = MinSpawnDistance;
            settings.FindProperty("maxSpawnDistanceFromPlayer").floatValue = MaxSpawnDistance;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static GameObject LoadEnemyPrefab(string name)
    {
        string path = $"{EnemyPrefabFolder}/{name}.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new System.Exception($"Enemy prefab not found at {path}");
        return prefab;
    }

    [MenuItem("Tools/Procedural Map/Export Map Previews")]
    public static void ExportMapPreviews()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string outputFolder = GetCommandLineArg("-previewOut") ?? Path.GetFullPath("Temp/MapPreviews");
        Directory.CreateDirectory(outputFolder);

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var generator = Object.FindAnyObjectByType<ProceduralMapGenerator>();

        for (int map = 1; map <= 4; map++)
        {
            generator.Generate(map, 1000 + map);
            File.WriteAllBytes(Path.Combine(outputFolder, $"map{map}_layout.png"), DrawLayout(generator));

            byte[] render = RenderMap(generator.WorldBounds);
            if (render != null) File.WriteAllBytes(Path.Combine(outputFolder, $"map{map}_render.png"), render);
        }

        // Reload from disk so the preview tiles never end up saved into the scene.
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log($"[MapGen] Previews written to {outputFolder}");
    }

    // Map 1 = Meadow, 2 = Rocky coast, 3 = Pumpkin farm, 4 = Bone fields, then they repeat.
    private static void SetDefaultThemes(SerializedProperty themes)
    {
        themes.arraySize = 4;

        SetTheme(themes.GetArrayElementAtIndex(0), "Meadow", Color.white, beachWidth: 1, trees: 1.4f,
            obstacles: Deco(9), obstacleDensity: 0.4f,
            decorations: Deco(1, 2, 3, 10, 11), decorationChance: 0.15f,
            waterRocks: new Sprite[0][], waterRockDensity: 0f);

        SetTheme(themes.GetArrayElementAtIndex(1), "Rocky coast", new Color(0.85f, 0.93f, 0.95f), beachWidth: 2, trees: 0.5f,
            obstacles: Deco(5, 6), obstacleDensity: 1.0f,
            decorations: Deco(4, 7), decorationChance: 0.12f,
            waterRocks: WaterRocks(), waterRockDensity: 2.5f);

        SetTheme(themes.GetArrayElementAtIndex(2), "Pumpkin farm", new Color(1f, 0.95f, 0.78f), beachWidth: 1, trees: 0.4f,
            obstacles: Deco(13, 18, 8), obstacleDensity: 0.8f,
            decorations: Deco(12, 10, 1), decorationChance: 0.14f,
            waterRocks: new Sprite[0][], waterRockDensity: 0f);

        SetTheme(themes.GetArrayElementAtIndex(3), "Bone fields", new Color(0.95f, 0.82f, 0.62f), beachWidth: 2, trees: 0.25f,
            obstacles: Deco(16, 17, 6), obstacleDensity: 0.6f,
            decorations: Deco(14, 15, 4), decorationChance: 0.15f,
            waterRocks: new Sprite[0][], waterRockDensity: 0f);
    }

    private static void SetTheme(SerializedProperty theme, string name, Color tint, int beachWidth, float trees,
        Sprite[] obstacles, float obstacleDensity, Sprite[] decorations, float decorationChance,
        Sprite[][] waterRocks, float waterRockDensity)
    {
        theme.FindPropertyRelative("name").stringValue = name;
        theme.FindPropertyRelative("groundTint").colorValue = tint;
        theme.FindPropertyRelative("beachWidth").intValue = beachWidth;
        theme.FindPropertyRelative("treesPer100Cells").floatValue = trees;
        SetSprites(theme.FindPropertyRelative("obstacles"), obstacles);
        theme.FindPropertyRelative("obstaclesPer100Cells").floatValue = obstacleDensity;
        SetSprites(theme.FindPropertyRelative("decorations"), decorations);
        theme.FindPropertyRelative("decorationChance").floatValue = decorationChance;

        SerializedProperty rocks = theme.FindPropertyRelative("waterRocks");
        rocks.arraySize = waterRocks.Length;
        for (int i = 0; i < waterRocks.Length; i++)
        {
            SetSprites(rocks.GetArrayElementAtIndex(i).FindPropertyRelative("frames"), waterRocks[i]);
        }
        theme.FindPropertyRelative("waterRocksPer100WaterCells").floatValue = waterRockDensity;
    }

    private static void SetSprites(SerializedProperty array, Sprite[] sprites)
    {
        array.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++)
        {
            array.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }
    }

    private static Sprite[] Deco(params int[] numbers)
    {
        return numbers.Select(n => LargestSprite($"{DecoFolder}/{n:D2}.png")).ToArray();
    }

    private static Sprite LargestSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .OrderByDescending(s => s.rect.width * s.rect.height)
            .FirstOrDefault();
        if (sprite == null) throw new System.Exception($"No sprite found in {path}");
        return sprite;
    }

    // Each Rocks_0X sheet is a strip of square frames, but auto-slicing split some frames into
    // small pieces - keep the biggest piece of every frame.
    private static Sprite[][] WaterRocks()
    {
        var rocks = new List<Sprite[]>();
        for (int n = 1; n <= 4; n++)
        {
            string path = $"{WaterRocksFolder}/Rocks_{n:D2}.png";
            Sprite[] slices = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            if (slices.Length == 0) continue;

            float frameSize = slices[0].texture.height;
            Sprite[] frames = slices
                .GroupBy(s => Mathf.FloorToInt(s.rect.center.x / frameSize))
                .OrderBy(g => g.Key)
                .Select(g => g.OrderByDescending(s => s.rect.width * s.rect.height).First())
                .ToArray();
            rocks.Add(frames);
        }
        return rocks.ToArray();
    }

    private static GameObject FindInScene(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t.gameObject;
            }
        }
        return null;
    }

    private static Tilemap CreateTilemap(Transform grid, string name, TilemapRenderer materialSource, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(grid, false);
        var tilemap = go.AddComponent<Tilemap>();

        if (materialSource != null)
        {
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sharedMaterial = materialSource.sharedMaterial;
            renderer.sortingLayerID = materialSource.sortingLayerID;
            renderer.sortingOrder = sortingOrder;
        }

        return tilemap;
    }

    private static Dictionary<string, TileBase> LoadTilesBySpriteName(string folder)
    {
        var tiles = new Dictionary<string, TileBase>();
        foreach (string guid in AssetDatabase.FindAssets("", new[] { folder }))
        {
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(AssetDatabase.GUIDToAssetPath(guid));
            if (tile != null && tile.sprite != null) tiles[tile.sprite.name] = tile;
        }
        return tiles;
    }

    private static void LoadTreeTiles(out TileBase[] tiles, out Vector2Int[] offsets)
    {
        var tileList = new List<TileBase>();
        var offsetList = new List<Vector2Int>();

        foreach (string guid in AssetDatabase.FindAssets("", new[] { TreeTilesFolder }))
        {
            var tile = AssetDatabase.LoadAssetAtPath<TileBase>(AssetDatabase.GUIDToAssetPath(guid));
            if (tile == null) continue;

            SerializedProperty frames = new SerializedObject(tile).FindProperty("m_AnimatedSprites");
            if (frames == null || frames.arraySize == 0) continue;

            // Tree.png is a sheet of 3x3-cell trees, so the first frame's rect tells which cell this tile is.
            var sprite = (Sprite)frames.GetArrayElementAtIndex(0).objectReferenceValue;
            float cellSize = sprite.rect.width;
            int column = Mathf.RoundToInt(sprite.rect.x / cellSize) % 3;
            int row = Mathf.RoundToInt(sprite.rect.y / cellSize) % 3;

            tileList.Add(tile);
            offsetList.Add(new Vector2Int(column - 1, row));
        }

        tiles = tileList.ToArray();
        offsets = offsetList.ToArray();
    }

    private static void SetReference(SerializedObject settings, string property, Object value)
    {
        settings.FindProperty(property).objectReferenceValue = value;
    }

    private static void SetArray(SerializedObject settings, string property, TileBase[] values)
    {
        SerializedProperty array = settings.FindProperty(property);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static void AddToBuildSettings(string path)
    {
        // Replace any old entry: a rebuilt scene gets a new GUID.
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        scenes.RemoveAll(s => s.path == path);
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static byte[] DrawLayout(ProceduralMapGenerator generator)
    {
        const int pixelsPerCell = 8;
        var texture = new Texture2D(generator.Width * pixelsPerCell, generator.Height * pixelsPerCell);
        var pixels = new Color32[texture.width * texture.height];

        for (int x = 0; x < generator.Width; x++)
        {
            for (int y = 0; y < generator.Height; y++)
            {
                Color32 color = generator.GetCellKind(x, y) switch
                {
                    MapCellKind.Water => new Color32(64, 150, 200, 255),
                    MapCellKind.Sand => new Color32(230, 205, 120, 255),
                    MapCellKind.Grass => new Color32(110, 175, 70, 255),
                    MapCellKind.Covered => new Color32(40, 100, 40, 255),
                    _ => new Color32(100, 60, 30, 255)
                };
                if (new Vector2Int(x, y) == generator.PlayerCell) color = new Color32(220, 30, 30, 255);

                for (int px = 0; px < pixelsPerCell; px++)
                {
                    for (int py = 0; py < pixelsPerCell; py++)
                    {
                        pixels[(y * pixelsPerCell + py) * texture.width + x * pixelsPerCell + px] = color;
                    }
                }
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        byte[] png = texture.EncodeToPNG();
        Object.DestroyImmediate(texture);
        return png;
    }

    private static byte[] RenderMap(Rect worldBounds)
    {
        const int pixelsPerUnit = 32;
        var cameraObject = new GameObject("Map Preview Camera");
        var renderTexture = new RenderTexture(
            Mathf.RoundToInt(worldBounds.width * pixelsPerUnit),
            Mathf.RoundToInt(worldBounds.height * pixelsPerUnit), 24);

        try
        {
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = worldBounds.height / 2f;
            camera.aspect = worldBounds.width / worldBounds.height;
            camera.transform.position = new Vector3(worldBounds.center.x, worldBounds.center.y, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture.active = renderTexture;
            var texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture.Apply();
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            return png;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MapGen] Rendered preview skipped: {e.Message}");
            return null;
        }
        finally
        {
            RenderTexture.active = null;
            Object.DestroyImmediate(cameraObject);
            renderTexture.Release();
            Object.DestroyImmediate(renderTexture);
        }
    }

    private static string GetCommandLineArg(string name)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        int index = System.Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
