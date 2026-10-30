using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Global post-processing volume built in code: bloom, tonemapping, vignette, grade, aberration.</summary>
public class PostFX : MonoBehaviour
{
    private Bloom bloom;
    private ChromaticAberration aberration;
    private Vignette vignette;
    private float aberrationValue;
    private float bloomPunch;

    public void Init()
    {
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();

        bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.85f);
        bloom.intensity.Override(0.9f);
        bloom.scatter.Override(0.7f);
        bloom.maxIterations.Override(4);
        bloom.highQualityFiltering.Override(false);

        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.ACES);

        vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.34f);
        vignette.smoothness.Override(0.55f);

        aberration = profile.Add<ChromaticAberration>(true);
        aberration.intensity.Override(0f);
        aberration.active = false;

        var grade = profile.Add<ColorAdjustments>(true);
        grade.saturation.Override(24f);
        grade.contrast.Override(14f);

        var vol = gameObject.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 100f;
        vol.sharedProfile = profile;
    }

    public void Punch(float aberrationAmount, float bloomAmount)
    {
        aberrationValue = Mathf.Max(aberrationValue, aberrationAmount);
        bloomPunch = Mathf.Max(bloomPunch, bloomAmount);
    }

    public void SetVignetteColor(Color c)
    {
        vignette.color.Override(c);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        aberrationValue = Mathf.MoveTowards(aberrationValue, 0f, dt * 2.2f);
        bloomPunch = Mathf.MoveTowards(bloomPunch, 0f, dt * 2.0f);
        bool ab = aberrationValue > 0.001f;
        if (aberration.active != ab) aberration.active = ab;
        if (ab) aberration.intensity.value = aberrationValue;
        bloom.intensity.value = 0.9f + bloomPunch;
    }
}
