using UnityEngine;

/// <summary>Particles for eating, shockwaves, the dust orbiting the hole and ambient motes. All use the Particle shader.</summary>
public class FXPlayer : MonoBehaviour
{
    private ParticleSystem burst;
    private ParticleSystem ring;
    private ParticleSystem dust;
    private ParticleSystem motes;
    private float dustAcc;

    public void Init(BlackHoleCatalog cat)
    {
        var glow = MakeMat(cat, 0f, 1.6f);
        var rng = MakeMat(cat, 1f, 1.8f);
        var spark = MakeMat(cat, 2f, 1.6f);

        burst = Make("Burst", spark, 1200, 0.35f, FadeGradient(), ShrinkCurve());
        ring = Make("Ring", rng, 64, 0f, FadeGradient(), GrowCurve());
        dust = Make("Dust", glow, 800, 0f, FadeGradient(), ShrinkCurve());
        motes = Make("Motes", glow, 150, 0f, SoftGradient(), FlatCurve());

        var em = motes.emission;
        em.enabled = true;
        em.rateOverTime = 7f;
        var main = motes.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.22f);
        var sh = motes.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.position = new Vector3(0f, 3f, 0f);
        sh.scale = new Vector3(40f, 6f, 40f);
        var vel = motes.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = 0f;
        vel.y = 0.45f;
        vel.z = 0f;
    }

    public void SetTheme(BlackHoleCatalog.Theme t, float mapHalf)
    {
        var main = motes.main;
        Color c = Color.Lerp(t.holeAccent, Color.white, 0.25f);
        c.a = 0.55f;
        main.startColor = c;
        var sh = motes.shape;
        sh.scale = new Vector3(mapHalf * 2f, 6f, mapHalf * 2f);
    }

    public void Clear()
    {
        burst.Clear();
        ring.Clear();
        dust.Clear();
    }

    public void Burst(Vector3 pos, Color color, int count, float size)
    {
        var ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            Vector3 dir = Random.onUnitSphere;
            dir.y = Mathf.Abs(dir.y) * 0.8f + 0.25f;
            ep.position = pos + Vector3.up * 0.2f;
            ep.velocity = dir * Random.Range(2f, 6.5f);
            ep.startLifetime = Random.Range(0.45f, 0.9f);
            ep.startSize = size * Random.Range(0.5f, 1.4f);
            ep.startColor = Color.Lerp(color, Color.white, Random.Range(0f, 0.5f));
            burst.Emit(ep, 1);
        }
    }

    public void Ring(Vector3 pos, Color color, float size, float life = 0.7f)
    {
        var ep = new ParticleSystem.EmitParams
        {
            position = new Vector3(pos.x, 0.06f, pos.z),
            velocity = Vector3.zero,
            startLifetime = life,
            startSize = size,
            startColor = color
        };
        ring.Emit(ep, 1);
    }

    /// <summary>Dust spiralling around the lip of the hole; denser while moving fast.</summary>
    public void TickDust(float dt, Vector3 holePos, float radius, float speed01, Color color)
    {
        dustAcc += (8f + 28f * speed01) * dt;
        var ep = new ParticleSystem.EmitParams();
        while (dustAcc >= 1f)
        {
            dustAcc -= 1f;
            float a = Random.value * Mathf.PI * 2f;
            Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
            ep.position = holePos + radial * radius * Random.Range(1.1f, 1.9f) + Vector3.up * 0.08f;
            ep.velocity = tangent * (1.5f + radius) - radial * Random.Range(0.8f, 1.8f);
            ep.startLifetime = Random.Range(0.5f, 0.9f);
            ep.startSize = Random.Range(0.08f, 0.22f) * (0.7f + radius * 0.3f);
            ep.startColor = Color.Lerp(color, Color.white, Random.Range(0f, 0.4f));
            dust.Emit(ep, 1);
        }
    }

    private static Material MakeMat(BlackHoleCatalog cat, float shape, float intensity)
    {
        var m = new Material(cat.particleShader);
        m.SetFloat("_Shape", shape);
        m.SetFloat("_Intensity", intensity);
        return m;
    }

    private ParticleSystem Make(string n, Material mat, int max, float gravity, Gradient colorOverLife, AnimationCurve sizeCurve)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = max;
        main.startLifetime = 1f;
        main.startSpeed = 0f;
        main.startSize = 0.2f;
        main.gravityModifier = gravity;

        var em = ps.emission;
        em.enabled = false;
        var sh = ps.shape;
        sh.enabled = false;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(colorOverLife);

        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        ps.Play();
        return ps;
    }

    private static Gradient FadeGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    private static Gradient SoftGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    private static AnimationCurve ShrinkCurve() => new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.1f));
    private static AnimationCurve GrowCurve() => new AnimationCurve(new Keyframe(0f, 0.12f), new Keyframe(0.35f, 0.7f), new Keyframe(1f, 1f));
    private static AnimationCurve FlatCurve() => new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 1f));
}
