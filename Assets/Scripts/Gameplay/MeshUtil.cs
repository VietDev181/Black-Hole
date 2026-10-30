using UnityEngine;

public static class MeshUtil
{
    private static Mesh flatQuad;

    /// <summary>1x1 quad lying in the XZ plane, centred on the origin, facing up.</summary>
    public static Mesh FlatQuad()
    {
        if (flatQuad != null) return flatQuad;
        flatQuad = new Mesh { name = "FlatQuad" };
        flatQuad.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3(-0.5f, 0f, 0.5f),
            new Vector3(0.5f, 0f, 0.5f),
            new Vector3(0.5f, 0f, -0.5f)
        };
        flatQuad.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
        flatQuad.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        flatQuad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        flatQuad.bounds = new Bounds(Vector3.zero, new Vector3(1f, 0.1f, 1f));
        return flatQuad;
    }
}
