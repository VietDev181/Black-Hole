using UnityEngine;

/// <summary>Movement, growth and visual of the hole. The visual is a quad drawn by the Hole shader.</summary>
public class HoleController : MonoBehaviour
{
    // Area conservation: radius^2 grows by Alpha * (mass swallowed).
    private const float Alpha = 0.55f;

    public Vector3 Position { get; private set; }
    public Vector3 Velocity { get; private set; }
    public float Radius { get; private set; }
    public float TargetRadius => targetRadius;

    private float targetRadius;
    private float baseRadius;
    private float mapHalf;
    private float radiusVel;
    private float pulse;

    public void Init(Material holeMaterial)
    {
        var mf = gameObject.AddComponent<MeshFilter>();
        mf.sharedMesh = MeshUtil.FlatQuad();
        var mr = gameObject.AddComponent<MeshRenderer>();
        mr.sharedMaterial = holeMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    public void ResetHole(float startRadius, float mapHalfExtent)
    {
        baseRadius = startRadius;
        targetRadius = startRadius;
        Radius = startRadius;
        radiusVel = 0f;
        mapHalf = mapHalfExtent;
        Position = Vector3.zero;
        Velocity = Vector3.zero;
        pulse = 0f;
        Apply();
    }

    public void Tick(float dt, Vector2 move)
    {
        float speed = 5.5f + Radius * 2.6f;
        Vector3 want = new Vector3(move.x, 0f, move.y) * speed;
        Velocity = Vector3.MoveTowards(Velocity, want, speed * 7f * dt);
        Vector3 p = Position + Velocity * dt;

        float lim = mapHalf - Radius * 0.35f;
        p.x = Mathf.Clamp(p.x, -lim, lim);
        p.z = Mathf.Clamp(p.z, -lim, lim);
        Position = p;

        Radius = Mathf.SmoothDamp(Radius, targetRadius, ref radiusVel, 0.25f);
        pulse = Mathf.MoveTowards(pulse, 0f, dt * 2.2f);
        Apply();
    }

    public void Grow(float mass)
    {
        targetRadius = Mathf.Sqrt(targetRadius * targetRadius + Alpha * mass);
        pulse = Mathf.Max(pulse, 0.6f);
    }

    public void Shrink(float factor)
    {
        targetRadius = Mathf.Max(baseRadius * 0.85f, targetRadius * factor);
        pulse = 1f;
    }

    public void Pulse() => pulse = 1f;

    private void Apply()
    {
        float vr = Radius * (1f + 0.07f * Mathf.Sin(pulse * Mathf.PI));
        transform.position = new Vector3(Position.x, 0.004f, Position.z);
        transform.localScale = Vector3.one * (vr * 4.4f);
        BHShader.SetHole(Position, vr);
    }
}
