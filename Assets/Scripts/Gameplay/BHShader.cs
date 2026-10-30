using UnityEngine;

/// <summary>Pushes the global values every Black Hole shader reads.</summary>
public static class BHShader
{
    private static readonly int Hole = Shader.PropertyToID("_BH_Hole");
    private static readonly int LightDir = Shader.PropertyToID("_BH_LightDir");
    private static readonly int LightColor = Shader.PropertyToID("_BH_LightColor");
    private static readonly int AmbSky = Shader.PropertyToID("_BH_AmbSky");
    private static readonly int AmbGround = Shader.PropertyToID("_BH_AmbGround");
    private static readonly int Rim = Shader.PropertyToID("_BH_RimColor");
    private static readonly int HoleColor = Shader.PropertyToID("_BH_HoleColor");
    private static readonly int HoleColor2 = Shader.PropertyToID("_BH_HoleColor2");
    private static readonly int FogColor = Shader.PropertyToID("_BH_FogColor");
    private static readonly int Fog = Shader.PropertyToID("_BH_Fog");
    private static readonly int Map = Shader.PropertyToID("_BH_Map");
    private static readonly int Border = Shader.PropertyToID("_BH_Border");

    public static void SetHole(Vector3 pos, float radius)
    {
        Shader.SetGlobalVector(Hole, new Vector4(pos.x, 0f, pos.z, radius));
    }

    public static void SetMap(float half)
    {
        Shader.SetGlobalVector(Map, new Vector4(half, half, 0f, 0f));
    }

    public static void ApplyTheme(BlackHoleCatalog.Theme t)
    {
        Vector3 l = t.lightDir.sqrMagnitude > 0.001f ? t.lightDir.normalized : new Vector3(-0.35f, 0.85f, -0.4f).normalized;
        Shader.SetGlobalVector(LightDir, new Vector4(l.x, l.y, l.z, 0f));
        Shader.SetGlobalColor(LightColor, t.lightColor);
        Shader.SetGlobalColor(AmbSky, t.ambientSky);
        Shader.SetGlobalColor(AmbGround, t.ambientGround);
        Shader.SetGlobalColor(Rim, t.rim);
        Shader.SetGlobalColor(HoleColor, t.holeAccent);
        Shader.SetGlobalColor(HoleColor2, t.holeHot);
        Shader.SetGlobalColor(FogColor, t.fog);
        Shader.SetGlobalVector(Fog, new Vector4(28f, 75f, 0.85f, 0f));
        Shader.SetGlobalColor(Border, t.border);
    }
}
