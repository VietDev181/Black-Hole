using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Scans the three model packs in Assets/Black Hole, measures every prop and writes
/// Assets/Resources/BlackHoleCatalog.asset together with references to shaders and audio.
/// Runs automatically when the catalog is missing; also available from Tools > Black Hole.
/// </summary>
[InitializeOnLoad]
public static class CatalogBuilder
{
    private const string CatalogPath = "Assets/Resources/BlackHoleCatalog.asset";

    static CatalogBuilder()
    {
        EditorApplication.delayCall += EnsureCatalog;
    }

    private static void EnsureCatalog()
    {
        var existing = AssetDatabase.LoadAssetAtPath<BlackHoleCatalog>(CatalogPath);
        bool broken = existing == null || existing.objectShader == null || existing.themes == null || existing.themes.Length == 0;
        if (broken) Build();
    }

    [MenuItem("Tools/Black Hole/Rebuild Catalog")]
    public static void Build()
    {
        var cat = ScriptableObject.CreateInstance<BlackHoleCatalog>();

        cat.objectShader = Shader.Find("BlackHole/Object");
        cat.groundShader = Shader.Find("BlackHole/Ground");
        cat.holeShader = Shader.Find("BlackHole/Hole");
        cat.skyShader = Shader.Find("BlackHole/Sky");
        cat.particleShader = Shader.Find("BlackHole/Particle");
        cat.uiPanelShader = Shader.Find("BlackHole/UIPanel");
        cat.uiTextShader = Shader.Find("BlackHole/UIText");

        cat.menuMusic = Load<AudioClip>("Assets/Audio/colorful-candy-321734.mp3");
        cat.gameMusic = Load<AudioClip>("Assets/Audio/spring-day-318971.mp3");
        cat.gameOverMusic = Load<AudioClip>("Assets/Audio/game-over-284367.mp3");
        cat.clickSfx = Load<AudioClip>("Assets/Audio/8-bit-game-sfx-sound-8-269967.mp3");
        cat.eatSfx = Load<AudioClip>("Assets/Audio/SFX/pop.mp3");
        cat.coinSfx = Load<AudioClip>("Assets/Audio/SFX/pop + coins.mp3");
        cat.growSfx = Load<AudioClip>("Assets/Audio/SFX/fruit_10_merge.mp3");

        cat.themes = new[]
        {
            City(),
            Halloween(),
            Dungeon()
        };

        Directory.CreateDirectory("Assets/Resources");
        AssetDatabase.DeleteAsset(CatalogPath);
        AssetDatabase.CreateAsset(cat, CatalogPath);
        AssetDatabase.SaveAssets();
        Debug.Log("Black Hole: catalog rebuilt (" + cat.themes[0].items.Length + " / " + cat.themes[1].items.Length + " / " + cat.themes[2].items.Length + " props).");
    }

    // ------------------------------------------------------------------ themes

    private static BlackHoleCatalog.Theme City()
    {
        var t = new BlackHoleCatalog.Theme
        {
            name = "City",
            atlas = Load<Texture2D>("Assets/Black Hole/Build/citybits_texture.png"),
            groundStyle = 0,
            colorA = new Color(0.22f, 0.23f, 0.29f),
            colorB = new Color(0.55f, 0.57f, 0.62f),
            colorC = new Color(1f, 0.82f, 0.3f),
            lightColor = new Color(1f, 0.92f, 0.8f),
            lightDir = new Vector3(-0.35f, 0.85f, -0.4f),
            ambientSky = new Color(0.5f, 0.55f, 0.8f) * 0.7f,
            ambientGround = new Color(0.3f, 0.22f, 0.4f) * 0.6f,
            rim = new Color(0.55f, 0.7f, 1f),
            fog = new Color(0.12f, 0.08f, 0.25f),
            border = new Color(0.35f, 0.85f, 1f),
            holeAccent = new Color(0.55f, 0.3f, 1f),
            holeHot = new Color(1f, 0.55f, 0.2f),
            space = new Color(0.02f, 0.01f, 0.06f),
            nebulaA = new Color(0.22f, 0.1f, 0.5f),
            nebulaB = new Color(0.9f, 0.3f, 0.6f)
        };
        t.items = Collect("Assets/Black Hole/Build", name =>
            !(name.Contains("withoutbase") || name.StartsWith("road_") || name == "base"));
        return t;
    }

    private static BlackHoleCatalog.Theme Halloween()
    {
        var t = new BlackHoleCatalog.Theme
        {
            name = "Haunted Graveyard",
            atlas = Load<Texture2D>("Assets/Black Hole/Halloween/halloweenbits_texture.png"),
            groundStyle = 1,
            colorA = new Color(0.2f, 0.17f, 0.24f),
            colorB = new Color(0.36f, 0.31f, 0.36f),
            colorC = new Color(0.95f, 0.45f, 0.1f),
            lightColor = new Color(0.6f, 0.7f, 1f) * 0.95f,
            lightDir = new Vector3(0.3f, 0.8f, -0.5f),
            ambientSky = new Color(0.4f, 0.4f, 0.8f) * 0.6f,
            ambientGround = new Color(0.22f, 0.12f, 0.28f) * 0.55f,
            rim = new Color(0.4f, 1f, 0.7f),
            fog = new Color(0.07f, 0.06f, 0.16f),
            border = new Color(1f, 0.5f, 0.1f),
            holeAccent = new Color(0.3f, 1f, 0.55f),
            holeHot = new Color(1f, 0.5f, 0.1f),
            space = new Color(0.01f, 0.02f, 0.04f),
            nebulaA = new Color(0.05f, 0.28f, 0.3f),
            nebulaB = new Color(0.55f, 0.18f, 0.6f)
        };
        t.items = Collect("Assets/Black Hole/Halloween", name =>
            !(name.StartsWith("floor_") || name.StartsWith("path_")));
        return t;
    }

    private static BlackHoleCatalog.Theme Dungeon()
    {
        var t = new BlackHoleCatalog.Theme
        {
            name = "Dungeon",
            atlas = Load<Texture2D>("Assets/Black Hole/Dungeon/dungeon_texture.png"),
            groundStyle = 2,
            colorA = new Color(0.2f, 0.18f, 0.2f),
            colorB = new Color(0.34f, 0.3f, 0.31f),
            colorC = new Color(0.95f, 0.6f, 0.25f),
            lightColor = new Color(1f, 0.72f, 0.42f),
            lightDir = new Vector3(0.25f, 0.85f, 0.4f),
            ambientSky = new Color(0.5f, 0.4f, 0.5f) * 0.55f,
            ambientGround = new Color(0.28f, 0.18f, 0.2f) * 0.55f,
            rim = new Color(1f, 0.7f, 0.4f),
            fog = new Color(0.1f, 0.06f, 0.1f),
            border = new Color(0.3f, 0.8f, 1f),
            holeAccent = new Color(0.35f, 0.6f, 1f),
            holeHot = new Color(1f, 0.8f, 0.3f),
            space = new Color(0.03f, 0.01f, 0.02f),
            nebulaA = new Color(0.45f, 0.12f, 0.08f),
            nebulaB = new Color(0.9f, 0.55f, 0.15f)
        };
        string[] prefixes =
        {
            "barrel", "box", "bottle", "candle", "chair", "chest", "coin", "crates", "key", "keg", "pillar",
            "plate", "rubble", "shelf", "shelves", "stool", "sword", "table", "torch", "trunk", "bed", "barrier"
        };
        t.items = Collect("Assets/Black Hole/Dungeon", name =>
        {
            foreach (var p in prefixes) if (name.StartsWith(p)) return true;
            return name == "wall" || name == "wall_arched" || name == "wall_half" || name == "wall_broken" || name == "wall_pillar";
        });
        return t;
    }

    // ------------------------------------------------------------------ measuring

    private static BlackHoleCatalog.Item[] Collect(string folder, System.Func<string, bool> include)
    {
        var list = new List<BlackHoleCatalog.Item>();
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (!include(name)) continue;
            var item = Measure(path);
            if (item != null) list.Add(item);
        }
        list.Sort((a, b) => a.radius.CompareTo(b.radius));
        return list.ToArray();
    }

    private static BlackHoleCatalog.Item Measure(string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return null;

        var inst = Object.Instantiate(prefab);
        inst.transform.position = Vector3.zero;
        inst.transform.rotation = Quaternion.identity;

        var renderers = inst.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Object.DestroyImmediate(inst);
            return null;
        }

        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);

        float localH = 0f;
        foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
            if (mf.sharedMesh != null) localH = Mathf.Max(localH, mf.sharedMesh.bounds.max.y);
        if (localH <= 0.0001f) localH = Mathf.Max(b.size.y, 0.01f);

        Object.DestroyImmediate(inst);

        float big = Mathf.Max(b.size.x, b.size.z);
        float small = Mathf.Min(b.size.x, b.size.z);
        float radius = Mathf.Max(0.05f, 0.5f * (0.65f * big + 0.35f * small));

        return new BlackHoleCatalog.Item
        {
            id = Path.GetFileNameWithoutExtension(path),
            prefab = prefab,
            radius = radius,
            height = b.size.y,
            localHeight = localH,
            lift = -b.min.y,
            score = Mathf.Max(1, Mathf.RoundToInt(radius * radius * 25f))
        };
    }

    private static T Load<T>(string path) where T : Object
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (a == null) Debug.LogWarning("Black Hole: missing asset " + path);
        return a;
    }
}
