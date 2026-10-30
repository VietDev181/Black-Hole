using UnityEngine;

/// <summary>Builds the whole game at runtime, so any scene can host it (SampleScene only needs the camera).</summary>
public static class Bootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Run()
    {
        var cat = BlackHoleCatalog.Load();
        if (cat == null || cat.themes == null || cat.themes.Length == 0 || cat.objectShader == null)
        {
            Debug.LogError("Black Hole: catalog missing or incomplete. Run  Tools > Black Hole > Rebuild Catalog.");
            return;
        }

        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        // props are drawn by custom shaders that ignore shadows; don't pay for an empty shadow pass
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) l.shadows = LightShadows.None;

        var go = new GameObject("BlackHoleGame");
        go.AddComponent<GameManager>().Init(cat);
    }
}
