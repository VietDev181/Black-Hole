using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the game loop: builds levels, moves the hole, swallows items, scores, handles win / lose.
/// The menu is a live attract mode: the hole plays itself on a random theme behind the title.
/// </summary>
public class GameManager : MonoBehaviour
{
    private BlackHoleCatalog cat;
    private GameSession s;
    private HoleController hole;
    private CameraRig rig;
    private FXPlayer fx;
    private PostFX post;
    private AudioService audioSvc;
    private GameUI ui;
    private LevelBuilder builder;
    private InputReader input;

    private LevelBuilder.Result world;
    private List<WorldItem> items = new List<WorldItem>();
    private float spawnClock;
    private float completeTimer;
    private float menuTimer;
    private int menuTheme;
    private float nextGrowRadius;

    private Vector2 autoDir;
    private float autoRetarget;
    private WorldItem autoTarget;

    public void Init(BlackHoleCatalog catalog)
    {
        cat = catalog;
        s = new GameSession { Best = SaveData.Best };
        input = new InputReader();

        var cam = Camera.main;
        if (cam == null)
        {
            var cgo = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = cgo.AddComponent<Camera>();
            cgo.AddComponent<AudioListener>();
        }

        var holeMat = new Material(cat.holeShader);
        hole = new GameObject("Hole").AddComponent<HoleController>();
        hole.transform.SetParent(transform, false);
        hole.Init(holeMat);

        rig = cam.gameObject.GetComponent<CameraRig>();
        if (rig == null) rig = cam.gameObject.AddComponent<CameraRig>();
        rig.Init(cam, hole);

        post = new GameObject("PostFX").AddComponent<PostFX>();
        post.transform.SetParent(transform, false);
        post.Init();

        fx = new GameObject("FX").AddComponent<FXPlayer>();
        fx.transform.SetParent(transform, false);
        fx.Init(cat);

        audioSvc = new GameObject("Audio").AddComponent<AudioService>();
        audioSvc.transform.SetParent(transform, false);
        audioSvc.Init(cat);

        builder = new LevelBuilder(cat, transform);

        ui = new GameObject("UI").AddComponent<GameUI>();
        ui.transform.SetParent(transform, false);
        ui.Init(cat);
        ui.Clicked += audioSvc.Click;
        ui.PlayPressed += () => { s.ResetRun(); StartLevel(1); };
        ui.PausePressed += Pause;
        ui.ResumePressed += Resume;
        ui.RestartPressed += Restart;
        ui.HomePressed += GoHome;
        ui.NextPressed += () => StartLevel(s.Level + 1);
        ui.MusicToggled += () => { audioSvc.SetMusic(!audioSvc.MusicOn); ui.RefreshToggles(audioSvc.MusicOn, audioSvc.SfxOn); };
        ui.SfxToggled += () => { audioSvc.SetSfx(!audioSvc.SfxOn); ui.RefreshToggles(audioSvc.MusicOn, audioSvc.SfxOn); };
        ui.RefreshToggles(audioSvc.MusicOn, audioSvc.SfxOn);

        menuTheme = Random.Range(0, cat.themes.Length);
        GoHome();
    }

    // ------------------------------------------------------------------ flow

    private void GoHome()
    {
        Time.timeScale = 1f;
        s.State = GameState.Menu;
        s.Combo = 0;
        s.ComboTimer = 0f;
        menuTheme = (menuTheme + 1) % cat.themes.Length;
        LoadWorld(1, menuTheme, true);
        rig.Cinematic = true;
        ui.ShowMenu(SaveData.Best);
        ui.SetJoystick(false, Vector2.zero, Vector2.zero);
        ui.Flash(Color.black, 1f);
        audioSvc.PlayMenuMusic();
        menuTimer = 0f;
    }

    private void StartLevel(int level)
    {
        Time.timeScale = 1f;
        int themeIndex = (level - 1) % cat.themes.Length;
        LoadWorld(level, themeIndex, false);
        s.StartLevel(level, world.Time, world.GoalMass);
        s.State = GameState.Playing;
        rig.Cinematic = false;
        ui.ShowHud();
        ui.SetHintVisible(level == 1);
        ui.ShowBanner("LEVEL " + level, world.Theme.name.ToUpperInvariant());
        ui.Flash(Color.black, 1f);
        audioSvc.PlayGameMusic();
    }

    private void Restart()
    {
        s.Score = s.LevelStartScore;
        StartLevel(s.Level);
    }

    private void Pause()
    {
        if (s.State != GameState.Playing) return;
        s.State = GameState.Paused;
        Time.timeScale = 0f;
        ui.ShowPause(true);
    }

    private void Resume()
    {
        if (s.State != GameState.Paused) return;
        s.State = GameState.Playing;
        Time.timeScale = 1f;
        ui.ShowPause(false);
    }

    private void LoadWorld(int level, int themeIndex, bool menu)
    {
        // old items are destroyed by the builder; drop our references first
        items.Clear();
        world = builder.Build(level, themeIndex, menu);
        items = world.Items;
        hole.ResetHole(world.StartRadius, world.MapHalf);
        nextGrowRadius = world.StartRadius * 1.3f;
        rig.Snap();
        fx.Clear();
        fx.SetTheme(world.Theme, world.MapHalf);
        post.SetVignetteColor(Color.Lerp(Color.black, world.Theme.holeAccent, 0.18f));
        spawnClock = 0f;
        autoTarget = null;
        autoRetarget = 0f;
        completeTimer = 0f;
        for (int i = 0; i < items.Count; i++) items[i].SetSpawnScale(0f);
    }

    // ------------------------------------------------------------------ loop

    private void Update()
    {
        float dt = Time.deltaTime;

        if (s.State == GameState.Paused)
        {
            ui.UpdateHud(s, false);
            return;
        }

        input.Tick(s.State == GameState.Playing);

        Vector2 move;
        switch (s.State)
        {
            case GameState.Playing: move = input.Move; break;
            case GameState.Menu:
            case GameState.LevelComplete: move = AutoPilot(dt); break;
            default: move = Vector2.zero; break;
        }

        hole.Tick(dt, move);
        PopInItems(dt);
        UpdateItems(dt);

        float speed01 = Mathf.Clamp01(hole.Velocity.magnitude / (5.5f + hole.Radius * 2.6f));
        fx.TickDust(dt, hole.Position, hole.Radius, speed01, world.Theme.holeAccent);

        switch (s.State)
        {
            case GameState.Playing: TickPlaying(dt); break;
            case GameState.Menu: TickMenu(dt); break;
            case GameState.LevelComplete: completeTimer += dt; break;
        }

        ui.SetJoystick(s.State == GameState.Playing && input.Dragging, input.BaseScreen, input.KnobScreen);
        ui.UpdateHud(s, s.State == GameState.Playing);
    }

    private void TickPlaying(float dt)
    {
        s.TickCombo(dt);
        s.TimeLeft -= dt;
        if (input.Dragging) ui.SetHintVisible(false);

        if (s.TimeLeft <= 0f)
        {
            s.TimeLeft = 0f;
            EndGame();
        }
    }

    private void TickMenu(float dt)
    {
        menuTimer += dt;
        // refill the attract-mode world once it is nearly eaten or after a while
        if (items.Count < 12 || menuTimer > 50f)
        {
            ui.Flash(Color.black, 1f);
            menuTheme = (menuTheme + 1) % cat.themes.Length;
            LoadWorld(1, menuTheme, true);
            menuTimer = 0f;
        }
    }

    private void PopInItems(float dt)
    {
        if (spawnClock > 1.6f) return;
        spawnClock += dt;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it == null || it.State != WorldItem.ItemState.Idle) continue;
            float delay = (i % 40) * 0.018f;
            float t = Mathf.Clamp01((spawnClock - 0.15f - delay) / 0.45f);
            float k = t < 1f ? 1f + 2.2f * Mathf.Pow(t - 1f, 3f) + 1.2f * Mathf.Pow(t - 1f, 2f) : 1f;
            it.SetSpawnScale(k);
        }
    }

    private void UpdateItems(float dt)
    {
        float R = hole.Radius;
        Vector3 hp = hole.Position;

        for (int i = items.Count - 1; i >= 0; i--)
        {
            var it = items[i];
            if (it == null) { items.RemoveAt(i); continue; }

            if (it.State == WorldItem.ItemState.Falling)
            {
                if (it.Tick(dt, hp)) items.RemoveAt(i);
                continue;
            }

            Vector3 pos = it.transform.position;
            Vector3 d = pos - hp;
            d.y = 0f;
            float dist = d.magnitude;

            bool eatable = it.IsBomb || it.Radius <= R;
            if (!eatable)
            {
                it.SetWarn(dist < R + it.Radius + 1.2f ? 1f : 0f);
                continue;
            }
            it.SetWarn(0f);

            // gentle gravity pull so the hole feels magnetic
            float pullRange = R * 1.5f + it.Radius * 0.3f;
            if (!it.IsBomb && dist < pullRange && dist > 0.001f)
            {
                float k = 1f - dist / pullRange;
                pos -= d / dist * (k * k * (1.4f + R * 0.9f) * dt);
                it.transform.position = pos;
            }

            float trigger = it.IsBomb ? R * 0.65f : Mathf.Max(R * 0.55f, R * 0.85f - it.Radius * 0.3f);
            if (dist < trigger)
            {
                it.BeginFall();
                OnSwallowed(it, pos);
            }
        }
    }

    private void OnSwallowed(WorldItem it, Vector3 pos)
    {
        var theme = world.Theme;

        if (it.IsBomb)
        {
            fx.Burst(pos, new Color(1f, 0.3f, 0.25f), 36, 0.45f);
            fx.Ring(pos, new Color(1f, 0.3f, 0.25f), hole.Radius * 5f, 0.6f);
            audioSvc.Boom();
            rig.Shake(0.7f);
            rig.PunchFov(5f);
            post.Punch(0.9f, 0.8f);
            ui.Flash(new Color(1f, 0.1f, 0.15f), 0.45f);
            if (s.State == GameState.Playing)
            {
                s.TimeLeft = Mathf.Max(1f, s.TimeLeft - 5f);
                s.Combo = 0;
                s.ComboTimer = 0f;
                hole.Shrink(0.92f);
                ui.SpawnPopup(rig.Cam, pos, "-5s", new Color(1f, 0.35f, 0.35f), 56);
            }
            return;
        }

        int mult = 1;
        int gain = 0;
        if (s.State == GameState.Playing)
        {
            mult = s.RegisterEat();
            gain = it.Score * mult;
            s.Score += gain;
            s.CollectedMass += it.Mass;
        }

        hole.Grow(it.Mass);

        float size = Mathf.Clamp(it.Radius, 0.3f, 3f);
        fx.Burst(pos, theme.holeAccent, Mathf.RoundToInt(8 + size * 6f), 0.18f + size * 0.05f);
        audioSvc.Eat(Mathf.Min(1.6f, 0.85f + s.Combo * 0.04f - size * 0.04f));
        rig.Shake(Mathf.Min(0.25f, 0.03f + size * 0.04f));

        if (gain > 0)
        {
            Color c = mult > 1 ? UIKit.Gold : Color.white;
            ui.SpawnPopup(rig.Cam, pos, "+" + gain, c, 34 + Mathf.RoundToInt(size * 5f) + mult * 3);
        }

        // growth milestone: shockwave + sound
        if (hole.TargetRadius >= nextGrowRadius)
        {
            nextGrowRadius = hole.TargetRadius * 1.3f;
            fx.Ring(hole.Position, theme.holeHot, hole.TargetRadius * 4.2f, 0.9f);
            audioSvc.Grow();
            post.Punch(0.25f, 0.45f);
            rig.PunchFov(2.5f);
            hole.Pulse();
        }

        if (s.State == GameState.Playing && s.CollectedMass >= s.GoalMass)
            CompleteLevel();
    }

    private void CompleteLevel()
    {
        s.State = GameState.LevelComplete;
        s.Stars = s.ComputeStars();
        s.TimeBonus = Mathf.CeilToInt(s.TimeLeft) * 10;
        s.Score += s.TimeBonus;
        if (s.Score > SaveData.Best) SaveData.Best = s.Score;
        s.Best = SaveData.Best;

        fx.Ring(hole.Position, UIKit.Gold, hole.Radius * 6f, 1.2f);
        fx.Burst(hole.Position, UIKit.Gold, 60, 0.3f);
        audioSvc.Coin();
        post.Punch(0.3f, 0.8f);
        ui.Flash(Color.white, 0.35f);
        ui.ShowComplete(s.Level, s.Stars, s.Score, s.TimeBonus);
    }

    private void EndGame()
    {
        s.State = GameState.GameOver;
        bool newBest = s.Score > SaveData.Best;
        if (newBest) SaveData.Best = s.Score;
        s.Best = SaveData.Best;
        audioSvc.PlayGameOverMusic();
        ui.ShowGameOver(s.Score, s.Best, newBest);
        ui.Flash(new Color(0.3f, 0f, 0.2f), 0.5f);
    }

    // ------------------------------------------------------------------ attract mode

    private Vector2 AutoPilot(float dt)
    {
        autoRetarget -= dt;
        if (autoTarget == null || autoTarget.State != WorldItem.ItemState.Idle || autoRetarget <= 0f)
        {
            autoRetarget = 1.2f;
            autoTarget = null;
            float best = float.MaxValue;
            Vector3 hp = hole.Position;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it == null || it.IsBomb || it.State != WorldItem.ItemState.Idle || it.Radius > hole.Radius) continue;
                float d = (it.transform.position - hp).sqrMagnitude;
                if (d < best) { best = d; autoTarget = it; }
            }
        }

        if (autoTarget == null) return Vector2.zero;
        Vector3 to = autoTarget.transform.position - hole.Position;
        Vector2 dir = new Vector2(to.x, to.z);
        if (dir.sqrMagnitude < 0.0004f) return Vector2.zero;
        return Vector2.ClampMagnitude(dir.normalized * 0.85f, 1f);
    }
}
