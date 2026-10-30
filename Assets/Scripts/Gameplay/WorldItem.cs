using UnityEngine;

/// <summary>
/// One swallowable prop (or bomb). Has no Update: GameManager ticks it, so hundreds of
/// items cost nothing while idle. The swallow deformation itself is done by the Object shader.
/// </summary>
public class WorldItem : MonoBehaviour
{
    public enum ItemState { Idle, Falling }

    private static readonly int FallId = Shader.PropertyToID("_Fall");
    private static readonly int WarnId = Shader.PropertyToID("_Warn");
    private static readonly int HeightId = Shader.PropertyToID("_ObjHeight");
    private static readonly int PivotId = Shader.PropertyToID("_FallPivot");
    private static MaterialPropertyBlock block;

    public ItemState State { get; private set; }
    public float Radius { get; private set; }
    public float Mass => Radius * Radius;
    public int Score { get; private set; }
    public bool IsBomb { get; private set; }

    private Renderer[] renderers;
    private float height;
    private float localHeight;
    private Vector3 baseScale;
    private Vector3 startPos;
    private Quaternion startRot;
    private Vector3 tiltAxis;
    private float fall;
    private float duration;
    private float warn;

    public void Init(float radius, float worldHeight, float meshHeight, int score, bool bomb)
    {
        Radius = radius;
        height = worldHeight;
        localHeight = Mathf.Max(meshHeight, 0.001f);
        Score = score;
        IsBomb = bomb;
        baseScale = transform.localScale;
        renderers = GetComponentsInChildren<Renderer>();
        duration = Mathf.Clamp(0.5f + radius * 0.12f, 0.5f, 1f);
        State = ItemState.Idle;
        block ??= new MaterialPropertyBlock();
    }

    public void SetSpawnScale(float k)
    {
        transform.localScale = baseScale * k;
    }

    public void SetWarn(float w)
    {
        if (Mathf.Approximately(w, warn)) return;
        warn = w;
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].GetPropertyBlock(block);
            block.SetFloat(WarnId, warn);
            renderers[i].SetPropertyBlock(block);
        }
    }

    public void BeginFall()
    {
        State = ItemState.Falling;
        fall = 0f;
        startPos = transform.position;
        startRot = transform.rotation;
        transform.localScale = baseScale;
        Vector2 r = Random.insideUnitCircle.normalized;
        tiltAxis = new Vector3(r.x, 0f, r.y);
        warn = 0f;
        ApplyFall(0f);
    }

    /// <summary>Returns true once the item has fully disappeared (and destroys it).</summary>
    public bool Tick(float dt, Vector3 holeCenter)
    {
        fall += dt / duration;
        float t = Mathf.Clamp01(fall);
        float e = t * t * (3f - 2f * t);

        Vector3 p = startPos;
        p.x = Mathf.Lerp(startPos.x, holeCenter.x, e);
        p.z = Mathf.Lerp(startPos.z, holeCenter.z, e);
        p.y = startPos.y - (height * 1.05f + Radius * 0.6f + 0.4f) * t * t;
        transform.position = p;
        transform.rotation = Quaternion.AngleAxis(t * 55f, tiltAxis) * Quaternion.AngleAxis(t * 160f, Vector3.up) * startRot;
        transform.localScale = baseScale * (1f - 0.45f * t);
        ApplyFall(t);

        if (fall >= 1f)
        {
            Destroy(gameObject);
            return true;
        }
        return false;
    }

    private void ApplyFall(float t)
    {
        Vector3 pv = transform.position;
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].GetPropertyBlock(block);
            block.SetFloat(FallId, t);
            block.SetFloat(HeightId, localHeight);
            block.SetFloat(WarnId, 0f);
            block.SetVector(PivotId, new Vector4(pv.x, pv.y, pv.z, 0f));
            renderers[i].SetPropertyBlock(block);
        }
    }
}
