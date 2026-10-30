using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Tilted top-down camera that follows the hole and zooms out as it grows.</summary>
public class CameraRig : MonoBehaviour
{
    public Camera Cam { get; private set; }
    public bool Cinematic;

    private HoleController hole;
    private Vector3 focus;
    private float dist = 14f;
    private float shake;
    private float fovPunch;

    public void Init(Camera cam, HoleController h)
    {
        Cam = cam;
        hole = h;
        Cam.nearClipPlane = 0.3f;
        Cam.farClipPlane = 300f;
        Cam.fieldOfView = 42f;
        Cam.clearFlags = CameraClearFlags.Skybox;
        Cam.allowHDR = true;
        var data = Cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.None;
    }

    public void Snap()
    {
        focus = hole.Position;
        dist = TargetDist();
        Place(0f);
    }

    public void Shake(float amount) => shake = Mathf.Max(shake, amount);
    public void PunchFov(float amount) => fovPunch = Mathf.Max(fovPunch, amount);

    private float TargetDist() => 8.5f + hole.Radius * 5.2f;

    private void LateUpdate()
    {
        if (hole == null) return;
        float dt = Time.unscaledDeltaTime;
        Vector3 target = hole.Position + hole.Velocity * 0.14f;
        focus = Vector3.Lerp(focus, target, 1f - Mathf.Exp(-6f * dt));
        dist = Mathf.Lerp(dist, TargetDist(), 1f - Mathf.Exp(-2.5f * dt));
        Place(dt);
    }

    private void Place(float dt)
    {
        float pitch = 56f;
        float yaw = Cinematic ? Mathf.Sin(Time.unscaledTime * 0.18f) * 14f : 0f;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pos = focus - rot * Vector3.forward * dist;

        Vector3 off = shake > 0.001f ? Random.insideUnitSphere * shake : Vector3.zero;
        shake = Mathf.MoveTowards(shake, 0f, dt * 2.4f);
        fovPunch = Mathf.MoveTowards(fovPunch, 0f, dt * 18f);

        transform.SetPositionAndRotation(pos + off, rot);
        Cam.fieldOfView = 42f + fovPunch;
    }
}
