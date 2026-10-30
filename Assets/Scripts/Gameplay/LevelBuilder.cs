using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a level: the floating island (ground + cliff), the sky, all props and the bombs.
/// Difficulty grows with the level: wider map, more and bigger props, more bombs, less time per prop.
/// </summary>
public class LevelBuilder
{
    public class Result
    {
        public List<WorldItem> Items = new List<WorldItem>();
        public float MapHalf;
        public float StartRadius;
        public float Time;
        public float GoalMass;
        public BlackHoleCatalog.Theme Theme;
        public int ThemeIndex;
    }

    private struct Placed
    {
        public Vector2 Pos;
        public float R;
    }

    private const float Alpha = 0.55f; // keep in sync with HoleController
    private const float CityCell = 18f;   // distance between road centre lines (matches the Ground shader)
    private const float CityScale = 2.4f; // the city pack is tiny next to the hole, scale it up

    private readonly BlackHoleCatalog cat;
    private readonly Transform root;
    private readonly Transform itemsRoot;
    private readonly Material[] objectMats;
    private readonly Material[] groundMats;
    private readonly Material[] cliffMats;
    private readonly Material[] skyMats;
    private readonly Material bombMat;
    private readonly GameObject ground;
    private readonly GameObject cliff;
    private readonly GameObject bombPrefab;

    public LevelBuilder(BlackHoleCatalog catalog, Transform parent)
    {
        cat = catalog;
        root = new GameObject("World").transform;
        root.SetParent(parent, false);
        itemsRoot = new GameObject("Items").transform;
        itemsRoot.SetParent(root, false);

        int n = cat.themes.Length;
        objectMats = new Material[n];
        groundMats = new Material[n];
        cliffMats = new Material[n];
        skyMats = new Material[n];

        for (int i = 0; i < n; i++)
        {
            var t = cat.themes[i];

            var om = new Material(cat.objectShader) { name = "Object_" + t.name };
            om.SetTexture("_BaseMap", t.atlas);
            objectMats[i] = om;

            var gm = new Material(cat.groundShader) { name = "Ground_" + t.name };
            gm.SetColor("_ColA", t.colorA);
            gm.SetColor("_ColB", t.colorB);
            gm.SetColor("_ColC", t.colorC);
            gm.SetFloat("_Style", t.groundStyle);
            groundMats[i] = gm;

            var cm = new Material(cat.objectShader) { name = "Cliff_" + t.name };
            cm.SetFloat("_UseAtlas", 0f);
            cm.SetColor("_BaseColor", Color.Lerp(t.colorA, t.colorB, 0.4f) * 0.35f);
            cm.SetFloat("_AOScale", 0.12f);
            cm.SetFloat("_AOBias", 0.5f);
            cm.SetFloat("_AOMin", 0.05f);
            cliffMats[i] = cm;

            var sm = new Material(cat.skyShader) { name = "Sky_" + t.name };
            sm.SetColor("_SpaceCol", t.space);
            sm.SetColor("_NebulaA", t.nebulaA);
            sm.SetColor("_NebulaB", t.nebulaB);
            skyMats[i] = sm;
        }

        bombMat = new Material(cat.objectShader) { name = "Bomb" };
        bombMat.SetFloat("_UseAtlas", 0f);
        bombMat.SetColor("_BaseColor", new Color(0.05f, 0.04f, 0.06f));
        bombMat.SetColor("_Emissive", new Color(1.1f, 0.05f, 0.08f));
        bombMat.SetFloat("_Pulse", 1f);

        ground = new GameObject("Ground");
        ground.transform.SetParent(root, false);
        ground.AddComponent<MeshFilter>().sharedMesh = MeshUtil.FlatQuad();
        var gr = ground.AddComponent<MeshRenderer>();
        gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        gr.receiveShadows = false;

        cliff = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cliff.name = "Cliff";
        Object.Destroy(cliff.GetComponent<Collider>());
        cliff.transform.SetParent(root, false);
        var cr = cliff.GetComponent<MeshRenderer>();
        cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        cr.receiveShadows = false;

        bombPrefab = new GameObject("BombPrefab");
        bombPrefab.SetActive(false);
        bombPrefab.transform.SetParent(root, false);
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(sphere.GetComponent<Collider>());
        sphere.transform.SetParent(bombPrefab.transform, false);
        sphere.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        sphere.GetComponent<MeshRenderer>().sharedMaterial = bombMat;
        sphere.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    public Result Build(int level, int themeIndex, bool menu)
    {
        Clear();

        var theme = cat.themes[themeIndex];
        var res = new Result { Theme = theme, ThemeIndex = themeIndex };

        bool city = theme.groundStyle == 0;
        res.MapHalf = city ? (level <= 3 ? CityCell : CityCell * 2f) : menu ? 16f : Mathf.Min(14f + 2.5f * level, 30f);
        BHShader.ApplyTheme(theme);
        BHShader.SetMap(res.MapHalf);
        ApplyIsland(themeIndex, res.MapHalf);
        RenderSettings.skybox = skyMats[themeIndex];

        // ---- pick the pool of props allowed on this level ----
        var all = new List<BlackHoleCatalog.Item>(theme.items);
        all.Sort((a, b) => a.radius.CompareTo(b.radius));
        float sc = city ? CityScale : 1f;
        float r0 = Mathf.Clamp(all[Mathf.Min(all.Count - 1, Mathf.FloorToInt(all.Count * 0.1f))].radius * sc * 1.3f, 0.45f, 1.3f);
        res.StartRadius = r0;

        if (city)
        {
            var cp = new List<Placed>(400);
            var cr = new List<float>(400);
            BuildCity(level, menu, res, r0, themeIndex, all, cp, cr);
            FinishLevel(level, res, r0, cr);
            return res;
        }
        float rMax = r0 * (2.4f + 0.8f * (level - 1));

        var pool = all.FindAll(i => i.radius <= rMax);
        if (pool.Count == 0) pool.Add(all[0]);
        float rMin = pool[0].radius;

        int count = menu ? 80 : Mathf.Min(70 + 22 * level, 300);
        var weights = new float[pool.Count];
        float wSum = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            weights[i] = 1f / Mathf.Pow(Mathf.Max(pool[i].radius, 0.05f) / Mathf.Max(rMin, 0.05f), 1.2f);
            wSum += weights[i];
        }

        var placed = new List<Placed>(count + 16);
        var chosenRadii = new List<float>(count);

        // ---- starter ring: a few small props right next to the hole so the first seconds feel good ----
        var small = pool.FindAll(i => i.radius <= r0 * 0.95f);
        if (small.Count > 0)
        {
            int starters = 6;
            for (int s = 0; s < starters; s++)
            {
                float ang = (s / (float)starters) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
                float d = Random.Range(2.2f, 4.2f) + r0;
                var item = small[Random.Range(0, small.Count)];
                TryPlace(item, new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * d, themeIndex, res, placed, chosenRadii);
            }
        }

        // ---- the rest: big first so they find room, then fill with small ones ----
        var picks = new List<BlackHoleCatalog.Item>(count);
        for (int i = 0; i < count; i++) picks.Add(WeightedPick(pool, weights, wSum));
        picks.Sort((a, b) => b.radius.CompareTo(a.radius));

        float lim = res.MapHalf - 1.2f;
        foreach (var item in picks)
        {
            for (int attempt = 0; attempt < 24; attempt++)
            {
                Vector2 pos = new Vector2(Random.Range(-lim, lim), Random.Range(-lim, lim));
                // keep big props away from the spawn so the player is not walled in
                if (item.radius > r0 && pos.magnitude < 4f * r0 + item.radius) continue;
                if (TryPlace(item, pos, themeIndex, res, placed, chosenRadii)) break;
            }
        }

        // ---- bombs ----
        int bombs = menu ? 0 : Mathf.Min(level * 2, 14);
        for (int b = 0; b < bombs; b++)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                Vector2 pos = new Vector2(Random.Range(-lim, lim), Random.Range(-lim, lim));
                if (pos.magnitude < 5f + r0) continue;
                if (Overlaps(placed, pos, 0.7f)) continue;
                PlaceBomb(pos, placed, res);
                break;
            }
        }

        FinishLevel(level, res, r0, chosenRadii);
        return res;
    }


    private void FinishLevel(int level, Result res, float r0, List<float> chosenRadii)
    {
        // ---- goal + time from what is actually reachable ----
        chosenRadii.Sort();
        float rr = r0 * r0;
        float reach = 0f;
        foreach (float r in chosenRadii)
        {
            if (r > Mathf.Sqrt(rr)) break;
            reach += r * r;
            rr += Alpha * r * r;
        }
        float frac = 0.62f + 0.03f * Mathf.Min(level, 6);
        if (res.Theme.groundStyle == 0) frac *= 0.8f;
        res.GoalMass = Mathf.Max(reach * frac, 1f);
        res.Time = 45f + 6f * level + (res.Theme.groundStyle == 0 ? 15f : 0f);

    }

    private void Clear()
    {
        for (int i = itemsRoot.childCount - 1; i >= 0; i--)
        {
            var child = itemsRoot.GetChild(i).gameObject;
            child.transform.SetParent(null);
            Object.Destroy(child);
        }
    }

    private void ApplyIsland(int themeIndex, float half)
    {
        ground.GetComponent<MeshRenderer>().sharedMaterial = groundMats[themeIndex];
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(half * 2f + 2f, 1f, half * 2f + 2f);

        cliff.GetComponent<MeshRenderer>().sharedMaterial = cliffMats[themeIndex];
        cliff.transform.position = new Vector3(0f, -4.03f, 0f);
        cliff.transform.localScale = new Vector3(half * 2f, 8f, half * 2f);
    }

    private static BlackHoleCatalog.Item WeightedPick(List<BlackHoleCatalog.Item> pool, float[] w, float sum)
    {
        float r = Random.value * sum;
        for (int i = 0; i < pool.Count; i++)
        {
            r -= w[i];
            if (r <= 0f) return pool[i];
        }
        return pool[pool.Count - 1];
    }

    private static bool Overlaps(List<Placed> placed, Vector2 pos, float r)
    {
        for (int i = 0; i < placed.Count; i++)
        {
            float min = (placed[i].R + r) * 0.85f + 0.15f;
            if ((placed[i].Pos - pos).sqrMagnitude < min * min) return true;
        }
        return false;
    }

    private bool TryPlace(BlackHoleCatalog.Item item, Vector2 pos, int themeIndex, Result res, List<Placed> placed, List<float> radii)
    {
        return PlaceItem(item, pos, Random.Range(0f, 360f), Random.Range(0.92f, 1.1f), themeIndex, res, placed, radii);
    }

    private bool PlaceItem(BlackHoleCatalog.Item item, Vector2 pos, float yaw, float jitter, int themeIndex, Result res, List<Placed> placed, List<float> radii)
    {
        float r = item.radius * jitter;
        float lim = res.MapHalf - r - 0.3f;
        if (Mathf.Abs(pos.x) > lim || Mathf.Abs(pos.y) > lim) return false;
        if (Overlaps(placed, pos, r)) return false;

        var go = Object.Instantiate(item.prefab, itemsRoot);
        go.name = item.id;
        go.transform.position = new Vector3(pos.x, item.lift * jitter, pos.y);
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = go.transform.localScale * jitter;

        foreach (var rend in go.GetComponentsInChildren<Renderer>())
        {
            var mats = new Material[rend.sharedMaterials.Length];
            for (int m = 0; m < mats.Length; m++) mats[m] = objectMats[themeIndex];
            rend.sharedMaterials = mats;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        var wi = go.AddComponent<WorldItem>();
        wi.Init(r, item.height * jitter, item.localHeight, item.score, false);

        res.Items.Add(wi);
        placed.Add(new Placed { Pos = pos, R = r });
        radii.Add(r);
        return true;
    }

    private void PlaceBomb(Vector2 pos, List<Placed> placed, Result res)
    {
        var go = Object.Instantiate(bombPrefab, itemsRoot);
        go.SetActive(true);
        go.name = "Bomb";
        go.transform.position = new Vector3(pos.x, 0f, pos.y);
        var wi = go.AddComponent<WorldItem>();
        wi.Init(0.5f, 1f, 1f, 0, true);
        res.Items.Add(wi);
        placed.Add(new Placed { Pos = pos, R = 0.5f });
    }

    // ------------------------------------------------------------------ city layout

    private static float FaceYaw(Vector2 dir) => Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

    /// <summary>
    /// Real street grid: roads every 18 units (same as the Ground shader), 2x2 building lots in every block
    /// facing the street, lamps / hydrants / benches on the sidewalks, traffic lights on the corners
    /// and cars driving in their lanes.
    /// </summary>
    private void BuildCity(int level, bool menu, Result res, float r0, int themeIndex,
        List<BlackHoleCatalog.Item> all, List<Placed> placed, List<float> radii)
    {
        var buildings = all.FindAll(i => i.id.StartsWith("building_"));
        var cars = all.FindAll(i => i.id.StartsWith("car_"));
        var lights = all.FindAll(i => i.id.StartsWith("trafficlight"));
        var litter = all.FindAll(i => i.id.StartsWith("trash") || i.id.StartsWith("box") || i.id == "firehydrant");
        var curbProps = all.FindAll(i => i.id == "firehydrant" || i.id == "bench" || i.id.StartsWith("trash") ||
                                         i.id == "dumpster" || i.id == "bush" || i.id.StartsWith("box"));
        var lamp = all.Find(i => i.id == "streetlight");
        var tower = all.Find(i => i.id == "watertower");
        var bushes = all.FindAll(i => i.id == "bush" || i.id == "bench");

        float half = res.MapHalf;
        int n = Mathf.RoundToInt(half / CityCell);              // roads at -n..n
        float S = CityScale;

        // ---- blocks: 2x2 lots, front facing a street ----
        for (int bi = -n; bi < n; bi++)
        {
            for (int bj = -n; bj < n; bj++)
            {
                Vector2 bc = new Vector2((bi + 0.5f) * CityCell, (bj + 0.5f) * CityCell);

                if (tower != null && buildings.Count > 0 && Random.value < 0.08f && !(bi == -1 || bi == 0) && !(bj == -1 || bj == 0))
                {
                    PlaceItem(tower, bc, Random.Range(0, 4) * 90f, S * 1.5f, themeIndex, res, placed, radii);
                    continue;
                }

                for (int sx = -1; sx <= 1; sx += 2)
                {
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector2 lot = bc + new Vector2(sx, sz) * 2.95f;
                        if (buildings.Count > 0 && Random.value > 0.08f)
                        {
                            var b = buildings[Random.Range(0, buildings.Count)];
                            Vector2 dir = Random.value < 0.5f ? new Vector2(sx, 0f) : new Vector2(0f, sz);
                            PlaceItem(b, lot, FaceYaw(dir), S * 0.88f, themeIndex, res, placed, radii);
                        }
                        else if (bushes.Count > 0)
                        {
                            for (int k = 0; k < 3; k++)
                            {
                                var p = bushes[Random.Range(0, bushes.Count)];
                                Vector2 q = lot + Random.insideUnitCircle * 1.7f;
                                PlaceItem(p, q, Random.Range(0f, 360f), S, themeIndex, res, placed, radii);
                            }
                        }
                    }
                }
            }
        }

        // ---- sidewalks along every road ----
        for (int ri = -n; ri <= n; ri++)
        {
            float rc = ri * CityCell;
            for (int axis = 0; axis < 2; axis++)
            {
                for (float t = -half + 2f; t <= half - 2f; t += 4.8f)
                {
                    if (Mathf.Abs(Mathf.Repeat(t + CityCell * 0.5f, CityCell) - CityCell * 0.5f) < 4.6f) continue; // intersections
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector2 toRoad = axis == 0 ? new Vector2(-side, 0f) : new Vector2(0f, -side);
                        Vector2 pos = axis == 0 ? new Vector2(rc + side * 3.45f, t) : new Vector2(t, rc + side * 3.45f);
                        bool lampSlot = Mathf.Abs(Mathf.Repeat(t, 9.6f)) < 4.8f;
                        if (lampSlot && lamp != null)
                            PlaceItem(lamp, pos, FaceYaw(toRoad), S, themeIndex, res, placed, radii);
                        else if (curbProps.Count > 0 && Random.value < 0.3f)
                        {
                            var p = curbProps[Random.Range(0, curbProps.Count)];
                            PlaceItem(p, pos, FaceYaw(toRoad), S, themeIndex, res, placed, radii);
                        }
                    }
                }
            }
        }

        // ---- traffic lights on the corners of every crossing ----
        if (lights.Count > 0)
        {
            for (int ix = -n; ix <= n; ix++)
            {
                for (int iz = -n; iz <= n; iz++)
                {
                    for (int cx = -1; cx <= 1; cx += 2)
                    {
                        for (int cz = -1; cz <= 1; cz += 2)
                        {
                            if (Random.value > 0.5f) continue;
                            Vector2 pos = new Vector2(ix * CityCell + cx * 3.6f, iz * CityCell + cz * 3.6f);
                            var l = lights[Random.Range(0, lights.Count)];
                            PlaceItem(l, pos, FaceYaw(new Vector2(-cx, -cz)), S, themeIndex, res, placed, radii);
                        }
                    }
                }
            }
        }

        // ---- litter in the starting crossing so the first seconds always feel good ----
        if (litter.Count > 0)
        {
            for (int k = 0; k < 8; k++)
            {
                float ang = k / 8f * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
                float d = Random.Range(2.2f, 4.2f);
                var it = litter[Random.Range(0, litter.Count)];
                PlaceItem(it, new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * d, Random.Range(0f, 360f), S, themeIndex, res, placed, radii);
            }
        }

        // ---- cars in their lanes ----
        if (cars.Count > 0)
        {
            float fill = Mathf.Min(0.45f + 0.04f * level, 0.7f);
            for (int ri = -n; ri <= n; ri++)
            {
                float rc = ri * CityCell;
                for (int axis = 0; axis < 2; axis++)
                {
                    for (float t = -half + 3f; t <= half - 3f; t += 5.5f)
                    {
                        if (Mathf.Abs(Mathf.Repeat(t + CityCell * 0.5f, CityCell) - CityCell * 0.5f) < 3.6f) continue;
                        for (int lane = -1; lane <= 1; lane += 2)
                        {
                            if (Random.value > fill) continue;
                            float tt = t + Random.Range(-1.2f, 1.2f);
                            Vector2 pos = axis == 0 ? new Vector2(rc + lane * 1.45f, tt) : new Vector2(tt, rc + lane * 1.45f);
                            if (pos.magnitude < 6f) continue;
                            Vector2 dir = axis == 0 ? new Vector2(0f, lane) : new Vector2(-lane, 0f);
                            var car = cars[Random.Range(0, cars.Count)];
                            PlaceItem(car, pos, FaceYaw(dir) + Random.Range(-3f, 3f), S, themeIndex, res, placed, radii);
                        }
                    }
                }
            }
        }

        // ---- bombs on the roads ----
        int bombs = menu ? 0 : Mathf.Min(level * 2, 14);
        float lim = half - 2f;
        for (int b = 0; b < bombs; b++)
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                int axis = Random.Range(0, 2);
                float rc = Random.Range(-n, n + 1) * CityCell + Random.Range(-1.2f, 1.2f);
                float t = Random.Range(-lim, lim);
                Vector2 pos = axis == 0 ? new Vector2(rc, t) : new Vector2(t, rc);
                if (pos.magnitude < 6f + r0) continue;
                if (Overlaps(placed, pos, 0.7f)) continue;
                PlaceBomb(pos, placed, res);
                break;
            }
        }
    }
}
