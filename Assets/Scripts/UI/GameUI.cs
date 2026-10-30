using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Builds and drives all screens: menu, HUD, pause, level complete, game over, popups and the joystick.</summary>
public class GameUI : MonoBehaviour
{
    public event Action Clicked;
    public event Action PlayPressed, PausePressed, ResumePressed, RestartPressed, HomePressed, NextPressed;
    public event Action MusicToggled, SfxToggled;

    private class UIScreen
    {
        public RectTransform Rt;
        public CanvasGroup Group;
        public bool On;
    }

    private class Popup
    {
        public Text Text;
        public float Age;
        public float Life;
        public Vector2 Start;
        public bool Alive;
    }

    private static readonly Color PanelTop = new Color(0.24f, 0.14f, 0.45f, 0.94f);
    private static readonly Color PanelBot = new Color(0.07f, 0.04f, 0.18f, 0.94f);
    private static readonly Color PanelBorder = new Color(0.68f, 0.5f, 1f, 0.95f);
    private static readonly Color OrangeTop = new Color(1f, 0.62f, 0.25f);
    private static readonly Color OrangeBot = new Color(1f, 0.25f, 0.45f);
    private static readonly Color BlueTop = new Color(0.4f, 0.62f, 1f);
    private static readonly Color BlueBot = new Color(0.3f, 0.2f, 0.85f);
    private static readonly Color GreenTop = new Color(0.45f, 1f, 0.62f);
    private static readonly Color GreenBot = new Color(0.1f, 0.65f, 0.55f);
    private static readonly Color White90 = new Color(1f, 1f, 1f, 0.9f);

    private RectTransform canvasRt;
    private UIScreen menu, hud, pause, complete, over;
    private readonly List<UIScreen> screens = new List<UIScreen>();

    // HUD
    private Text scoreValue, levelLabel, timerValue, comboText, percentText, hintText;
    private Material barFill;
    private float barShown;
    private int lastScore = -1, lastSec = -1, lastCombo = -1;
    private float scorePunch, comboPunch;
    private bool timerWasLow;

    // menu
    private Text bestText, titleText;
    private RectTransform titleRt;
    private readonly List<Text> musicLabels = new List<Text>();
    private readonly List<Text> sfxLabels = new List<Text>();

    // complete / over
    private Image[] stars;
    private Text completeTitle, completeScore, completeBonus, overScore, overBest;
    private float starTimer;
    private int starsEarned;

    // overlays
    private Image joyBase, joyKnob, flash;
    private Text bannerText, bannerSub;
    private float bannerTimer;
    private readonly List<Popup> popups = new List<Popup>();

    public void Init(BlackHoleCatalog cat)
    {
        UIKit.Init(cat);
        EnsureEventSystem();

        var cgo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasRt = (RectTransform)cgo.transform;

        BuildJoystick();
        hud = NewScreen("HUD");
        BuildHud(hud.Rt);
        menu = NewScreen("Menu");
        BuildMenu(menu.Rt);
        pause = NewScreen("Pause");
        BuildPause(pause.Rt);
        complete = NewScreen("Complete");
        BuildComplete(complete.Rt);
        over = NewScreen("GameOver");
        BuildOver(over.Rt);
        BuildOverlays();

        foreach (var s in screens)
        {
            s.Group.alpha = 0f;
            s.Group.blocksRaycasts = false;
            s.Group.interactable = false;
        }
    }

    // ------------------------------------------------------------------ public API

    public void ShowMenu(int best)
    {
        bestText.text = best > 0 ? "BEST  " + best.ToString("N0") : "Swallow everything!";
        SetOn(menu, true);
        SetOn(hud, false);
        SetOn(pause, false);
        SetOn(complete, false);
        SetOn(over, false);
    }

    public void ShowHud()
    {
        SetOn(menu, false);
        SetOn(hud, true);
        SetOn(pause, false);
        SetOn(complete, false);
        SetOn(over, false);
        lastScore = -1;
        lastSec = -1;
        lastCombo = -1;
        barShown = 0f;
    }

    public void ShowPause(bool on) => SetOn(pause, on);

    public void ShowComplete(int level, int stars, int score, int bonus)
    {
        completeTitle.text = "LEVEL " + level + " CLEAR!";
        completeScore.text = score.ToString("N0");
        completeBonus.text = "Time bonus  +" + bonus.ToString("N0");
        starsEarned = stars;
        starTimer = 0f;
        for (int i = 0; i < this.stars.Length; i++) this.stars[i].transform.localScale = Vector3.zero;
        SetOn(complete, true);
    }

    public void ShowGameOver(int score, int best, bool newBest)
    {
        overScore.text = score.ToString("N0");
        overBest.text = newBest ? "NEW BEST!" : "BEST  " + best.ToString("N0");
        SetOn(over, true);
    }

    public void SetHintVisible(bool v) => hintText.gameObject.SetActive(v);

    public void RefreshToggles(bool music, bool sfx)
    {
        foreach (var t in musicLabels) t.text = music ? "MUSIC  ON" : "MUSIC  OFF";
        foreach (var t in sfxLabels) t.text = sfx ? "SOUND  ON" : "SOUND  OFF";
    }

    public void UpdateHud(GameSession s, bool timerActive)
    {
        if (s.Score != lastScore)
        {
            scoreValue.text = s.Score.ToString("N0");
            if (lastScore >= 0) scorePunch = 1f;
            lastScore = s.Score;
        }

        int sec = Mathf.CeilToInt(Mathf.Max(0f, s.TimeLeft));
        if (sec != lastSec)
        {
            timerValue.text = (sec / 60) + ":" + (sec % 60).ToString("00");
            lastSec = sec;
        }
        bool low = timerActive && s.TimeLeft < 10f;
        float flashK = low ? (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f)) : 0f;
        if (low || timerWasLow)
            timerValue.color = Color.Lerp(Color.white, new Color(1f, 0.3f, 0.35f), flashK);
        timerWasLow = low;

        levelLabel.text = "LEVEL " + s.Level;

        barShown = Mathf.Lerp(barShown, s.Progress, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
        barFill.SetFloat("_Fill", barShown);
        percentText.text = Mathf.RoundToInt(s.Progress * 100f) + "%";

        int combo = s.ComboTimer > 0f ? s.Combo : 0;
        if (combo != lastCombo)
        {
            if (combo >= 2)
            {
                comboText.text = "COMBO x" + combo + (s.Multiplier > 1 ? "   SCORE x" + s.Multiplier : "");
                comboPunch = 1f;
            }
            lastCombo = combo;
        }
        comboText.gameObject.SetActive(combo >= 2);
    }

    public void SetJoystick(bool show, Vector2 baseScreen, Vector2 knobScreen)
    {
        joyBase.gameObject.SetActive(show);
        joyKnob.gameObject.SetActive(show);
        if (!show) return;
        joyBase.rectTransform.anchoredPosition = ScreenToCanvas(baseScreen);
        joyKnob.rectTransform.anchoredPosition = ScreenToCanvas(knobScreen);
    }

    public void ShowBanner(string title, string sub)
    {
        bannerText.text = title;
        bannerSub.text = sub;
        bannerTimer = 2.1f;
    }

    public void Flash(Color c, float alpha)
    {
        flash.color = new Color(c.r, c.g, c.b, alpha);
    }

    public void SpawnPopup(Camera cam, Vector3 worldPos, string text, Color color, int size)
    {
        Vector3 sp = cam.WorldToScreenPoint(worldPos + Vector3.up * 0.6f);
        if (sp.z < 0f) return;
        Popup p = null;
        foreach (var q in popups) if (!q.Alive) { p = q; break; }
        if (p == null) return;

        p.Alive = true;
        p.Age = 0f;
        p.Life = 0.95f;
        p.Start = ScreenToCanvas(sp);
        p.Text.text = text;
        p.Text.fontSize = size;
        p.Text.material.SetColor("_ColTop", Color.Lerp(color, Color.white, 0.55f));
        p.Text.material.SetColor("_ColBot", color);
        p.Text.gameObject.SetActive(true);
    }

    // ------------------------------------------------------------------ build

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        go.transform.SetParent(transform, false);
        go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    private UIScreen NewScreen(string name)
    {
        var rt = UIKit.Rect(name, canvasRt, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        UIKit.Stretch(rt);
        rt.gameObject.AddComponent<Canvas>(); // own canvas: changes here don't rebuild the other screens
        rt.gameObject.AddComponent<GraphicRaycaster>();
        var s = new UIScreen { Rt = rt, Group = rt.gameObject.AddComponent<CanvasGroup>() };
        screens.Add(s);
        return s;
    }

    private void Dim(Transform parent, float alpha)
    {
        var d = UIKit.Panel(parent, "Dim", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
            new Color(0.02f, 0f, 0.08f, alpha), new Color(0.02f, 0f, 0.08f, alpha), 0f, Color.clear, 0f, 0f, 0, true);
        UIKit.Stretch(d.rectTransform);
        d.material.SetVector("_Size", new Vector4(4000f, 4000f, 0f, 0f));
    }

    private void BuildJoystick()
    {
        joyBase = UIKit.Panel(canvasRt, "JoyBase", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 200f),
            new Color(1f, 1f, 1f, 0.08f), new Color(1f, 1f, 1f, 0.16f), 100f, new Color(1f, 1f, 1f, 0.55f), 4f);
        joyKnob = UIKit.Panel(canvasRt, "JoyKnob", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 84f),
            new Color(0.85f, 0.75f, 1f, 0.95f), new Color(0.5f, 0.3f, 1f, 0.95f), 42f, new Color(1f, 1f, 1f, 0.9f), 3f);
        joyBase.gameObject.SetActive(false);
        joyKnob.gameObject.SetActive(false);
    }

    private void BuildHud(Transform p)
    {
        var top = new Vector2(0.5f, 1f);

        // score
        UIKit.Panel(p, "ScoreCard", new Vector2(0f, 1f), new Vector2(190f, -68f), new Vector2(340f, 96f), PanelTop, PanelBot, 30f, PanelBorder, 3f, 0.6f);
        UIKit.Label(p, "ScoreCap", "SCORE", 22, new Vector2(0f, 1f), new Vector2(190f, -34f), new Vector2(300f, 30f), UIKit.Lavender, UIKit.Lavender, TextAnchor.MiddleCenter, 0f, false);
        scoreValue = UIKit.Label(p, "ScoreVal", "0", 50, new Vector2(0f, 1f), new Vector2(190f, -78f), new Vector2(320f, 60f), Color.white, UIKit.Gold);

        // level + timer
        UIKit.Panel(p, "TimerCard", top, new Vector2(0f, -68f), new Vector2(250f, 96f), PanelTop, PanelBot, 30f, PanelBorder, 3f, 0.6f);
        levelLabel = UIKit.Label(p, "LevelCap", "LEVEL 1", 22, top, new Vector2(0f, -34f), new Vector2(240f, 30f), UIKit.Lavender, UIKit.Lavender, TextAnchor.MiddleCenter, 0f, false);
        timerValue = UIKit.Label(p, "TimerVal", "0:45", 50, top, new Vector2(0f, -78f), new Vector2(240f, 60f), Color.white, Color.white);

        // pause button
        UIKit.MakeButton(p, "PauseBtn", "II", new Vector2(1f, 1f), new Vector2(-70f, -68f), new Vector2(92f, 92f), BlueTop, BlueBot, Color.white, 44, () => Fire(PausePressed));

        // progress bar
        UIKit.Panel(p, "BarBg", top, new Vector2(0f, -156f), new Vector2(640f, 36f), new Color(0.05f, 0.02f, 0.14f, 0.85f), new Color(0.12f, 0.07f, 0.28f, 0.85f), 18f, PanelBorder, 3f);
        var fill = UIKit.Panel(p, "BarFill", top, new Vector2(0f, -156f), new Vector2(640f, 36f), GreenTop, GreenBot, 18f, new Color(1f, 1f, 1f, 0.35f), 2f, 1f);
        barFill = fill.material;
        barFill.SetFloat("_Fill", 0f);
        percentText = UIKit.Label(p, "Percent", "0%", 22, top, new Vector2(0f, -156f), new Vector2(200f, 30f), Color.white, Color.white);
        UIKit.Label(p, "GoalCap", "GOAL", 20, top, new Vector2(-388f, -156f), new Vector2(90f, 30f), UIKit.Lavender, UIKit.Lavender, TextAnchor.MiddleRight, 0f, false);

        comboText = UIKit.Label(p, "Combo", "", 40, top, new Vector2(0f, -222f), new Vector2(700f, 54f), Color.white, new Color(1f, 0.45f, 0.35f), TextAnchor.MiddleCenter, 1f);
        comboText.gameObject.SetActive(false);

        hintText = UIKit.Label(p, "Hint", "DRAG TO MOVE   -   EAT SMALLER THINGS FIRST", 26, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(900f, 40f),
            Color.white, UIKit.Lavender, TextAnchor.MiddleCenter, 1f);
    }

    private void BuildMenu(Transform p)
    {
        var c = new Vector2(0.5f, 0.5f);
        titleText = UIKit.Label(p, "Title", "BLACK HOLE", 150, c, new Vector2(0f, 270f), new Vector2(1500f, 180f), Color.white, new Color(0.7f, 0.5f, 1f), TextAnchor.MiddleCenter, 1f);
        titleRt = titleText.rectTransform;
        UIKit.Label(p, "Sub", "SWALLOW THE WORLD", 38, c, new Vector2(0f, 175f), new Vector2(900f, 50f), UIKit.Gold, new Color(1f, 0.5f, 0.35f), TextAnchor.MiddleCenter, 1f);

        UIKit.MakeButton(p, "PlayBtn", "PLAY", c, new Vector2(0f, -250f), new Vector2(420f, 130f), OrangeTop, OrangeBot, Color.white, 74, () => Fire(PlayPressed));
        bestText = UIKit.Label(p, "Best", "", 36, c, new Vector2(0f, -352f), new Vector2(700f, 50f), Color.white, UIKit.Lavender, TextAnchor.MiddleCenter, 0f);

        var mb = UIKit.MakeButton(p, "MusicBtn", "MUSIC  ON", new Vector2(0f, 1f), new Vector2(160f, -60f), new Vector2(250f, 70f), BlueTop, BlueBot, Color.white, 26, () => Fire(MusicToggled));
        var sb = UIKit.MakeButton(p, "SfxBtn", "SOUND  ON", new Vector2(1f, 1f), new Vector2(-160f, -60f), new Vector2(250f, 70f), BlueTop, BlueBot, Color.white, 26, () => Fire(SfxToggled));
        musicLabels.Add(mb.GetComponentInChildren<Text>());
        sfxLabels.Add(sb.GetComponentInChildren<Text>());
    }

    private void BuildPause(Transform p)
    {
        Dim(p, 0.55f);
        var c = new Vector2(0.5f, 0.5f);
        UIKit.Panel(p, "Card", c, Vector2.zero, new Vector2(560f, 640f), PanelTop, PanelBot, 48f, PanelBorder, 4f, 0.5f);
        UIKit.Label(p, "T", "PAUSED", 76, c, new Vector2(0f, 245f), new Vector2(520f, 90f), Color.white, UIKit.Lavender, TextAnchor.MiddleCenter, 1f);
        UIKit.MakeButton(p, "Resume", "RESUME", c, new Vector2(0f, 120f), new Vector2(400f, 100f), GreenTop, GreenBot, Color.white, 46, () => Fire(ResumePressed));
        UIKit.MakeButton(p, "Restart", "RESTART", c, new Vector2(0f, 0f), new Vector2(400f, 90f), OrangeTop, OrangeBot, Color.white, 40, () => Fire(RestartPressed));
        UIKit.MakeButton(p, "Home", "HOME", c, new Vector2(0f, -100f), new Vector2(400f, 90f), BlueTop, BlueBot, Color.white, 40, () => Fire(HomePressed));
        var mb = UIKit.MakeButton(p, "Music", "MUSIC  ON", c, new Vector2(-105f, -215f), new Vector2(200f, 70f), BlueTop, BlueBot, Color.white, 22, () => Fire(MusicToggled));
        var sb = UIKit.MakeButton(p, "Sfx", "SOUND  ON", c, new Vector2(105f, -215f), new Vector2(200f, 70f), BlueTop, BlueBot, Color.white, 22, () => Fire(SfxToggled));
        musicLabels.Add(mb.GetComponentInChildren<Text>());
        sfxLabels.Add(sb.GetComponentInChildren<Text>());
    }

    private void BuildComplete(Transform p)
    {
        Dim(p, 0.5f);
        var c = new Vector2(0.5f, 0.5f);
        UIKit.Panel(p, "Card", c, Vector2.zero, new Vector2(640f, 730f), PanelTop, PanelBot, 52f, PanelBorder, 4f, 0.6f);
        completeTitle = UIKit.Label(p, "T", "LEVEL CLEAR!", 64, c, new Vector2(0f, 245f), new Vector2(600f, 80f), UIKit.Gold, new Color(1f, 0.5f, 0.35f), TextAnchor.MiddleCenter, 1f);

        stars = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            float y = i == 1 ? 130f : 112f;
            stars[i] = UIKit.Panel(p, "Star" + i, c, new Vector2((i - 1) * 150f, y), new Vector2(140f, 140f), UIKit.Gold, new Color(1f, 0.5f, 0.2f), 0f, Color.white, 4f, 1f, 1);
        }

        UIKit.Label(p, "ScoreCap", "SCORE", 26, c, new Vector2(0f, 10f), new Vector2(300f, 34f), UIKit.Lavender, UIKit.Lavender, TextAnchor.MiddleCenter, 0f, false);
        completeScore = UIKit.Label(p, "Score", "0", 76, c, new Vector2(0f, -45f), new Vector2(500f, 90f), Color.white, UIKit.Gold, TextAnchor.MiddleCenter, 1f);
        completeBonus = UIKit.Label(p, "Bonus", "", 28, c, new Vector2(0f, -110f), new Vector2(500f, 40f), GreenTop, GreenBot, TextAnchor.MiddleCenter, 0f);
        UIKit.MakeButton(p, "Next", "NEXT LEVEL", c, new Vector2(0f, -195f), new Vector2(440f, 110f), OrangeTop, OrangeBot, Color.white, 52, () => Fire(NextPressed));
        UIKit.MakeButton(p, "Home", "HOME", c, new Vector2(0f, -295f), new Vector2(220f, 64f), BlueTop, BlueBot, Color.white, 24, () => Fire(HomePressed));
    }

    private void BuildOver(Transform p)
    {
        Dim(p, 0.6f);
        var c = new Vector2(0.5f, 0.5f);
        UIKit.Panel(p, "Card", c, Vector2.zero, new Vector2(620f, 600f), new Color(0.35f, 0.1f, 0.3f, 0.95f), new Color(0.1f, 0.03f, 0.14f, 0.95f), 52f, new Color(1f, 0.45f, 0.55f, 0.95f), 4f, 0.5f);
        UIKit.Label(p, "T", "TIME'S UP!", 80, c, new Vector2(0f, 220f), new Vector2(580f, 100f), Color.white, new Color(1f, 0.45f, 0.5f), TextAnchor.MiddleCenter, 1f);
        UIKit.Label(p, "ScoreCap", "SCORE", 26, c, new Vector2(0f, 120f), new Vector2(300f, 34f), UIKit.Lavender, UIKit.Lavender, TextAnchor.MiddleCenter, 0f, false);
        overScore = UIKit.Label(p, "Score", "0", 96, c, new Vector2(0f, 45f), new Vector2(560f, 110f), Color.white, UIKit.Gold, TextAnchor.MiddleCenter, 1f);
        overBest = UIKit.Label(p, "Best", "", 34, c, new Vector2(0f, -35f), new Vector2(560f, 44f), UIKit.Gold, new Color(1f, 0.6f, 0.3f), TextAnchor.MiddleCenter, 0f);
        UIKit.MakeButton(p, "Retry", "TRY AGAIN", c, new Vector2(0f, -135f), new Vector2(440f, 110f), OrangeTop, OrangeBot, Color.white, 52, () => Fire(RestartPressed));
        UIKit.MakeButton(p, "Home", "HOME", c, new Vector2(0f, -240f), new Vector2(300f, 80f), BlueTop, BlueBot, Color.white, 36, () => Fire(HomePressed));
    }

    private void BuildOverlays()
    {
        var c = new Vector2(0.5f, 0.5f);
        bannerText = UIKit.Label(canvasRt, "Banner", "", 130, c, new Vector2(0f, 120f), new Vector2(1400f, 150f), Color.white, new Color(0.7f, 0.5f, 1f), TextAnchor.MiddleCenter, 1f);
        bannerSub = UIKit.Label(canvasRt, "BannerSub", "", 40, c, new Vector2(0f, 20f), new Vector2(1200f, 60f), UIKit.Gold, new Color(1f, 0.5f, 0.35f), TextAnchor.MiddleCenter, 0f);
        bannerText.gameObject.SetActive(false);
        bannerSub.gameObject.SetActive(false);

        for (int i = 0; i < 20; i++)
        {
            var t = UIKit.Label(canvasRt, "Popup", "", 40, c, Vector2.zero, new Vector2(300f, 60f), Color.white, Color.white);
            t.gameObject.SetActive(false);
            popups.Add(new Popup { Text = t });
        }

        flash = UIKit.Panel(canvasRt, "Flash", c, Vector2.zero, Vector2.zero, Color.white, Color.white, 0f, Color.clear);
        UIKit.Stretch(flash.rectTransform);
        flash.material.SetVector("_Size", new Vector4(4000f, 4000f, 0f, 0f));
        flash.color = new Color(0f, 0f, 0f, 0f);
    }

    // ------------------------------------------------------------------ runtime

    private void Fire(Action a)
    {
        Clicked?.Invoke();
        a?.Invoke();
    }

    private static void SetOn(UIScreen s, bool on)
    {
        s.On = on;
        s.Group.blocksRaycasts = on;
        s.Group.interactable = on;
    }

    private Vector2 ScreenToCanvas(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, null, out Vector2 local);
        return local;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        foreach (var s in screens)
        {
            float goal = s.On ? 1f : 0f;
            if (Mathf.Approximately(s.Group.alpha, goal)) continue;
            s.Group.alpha = Mathf.MoveTowards(s.Group.alpha, goal, dt * 5f);
            float k = Mathf.SmoothStep(0.93f, 1f, s.Group.alpha);
            s.Rt.localScale = new Vector3(k, k, 1f);
        }

        // punches
        if (scorePunch > 0f)
        {
            scorePunch = Mathf.MoveTowards(scorePunch, 0f, dt * 4f);
            scoreValue.rectTransform.localScale = Vector3.one * (1f + 0.22f * scorePunch);
        }
        if (comboPunch > 0f)
        {
            comboPunch = Mathf.MoveTowards(comboPunch, 0f, dt * 3.5f);
            comboText.rectTransform.localScale = Vector3.one * (1f + 0.3f * comboPunch);
        }

        // title bob
        if (menu.On && titleRt != null)
        {
            float bob = Mathf.Sin(Time.unscaledTime * 1.6f);
            titleRt.anchoredPosition = new Vector2(0f, 270f + bob * 8f);
            titleRt.localScale = Vector3.one * (1f + 0.025f * Mathf.Sin(Time.unscaledTime * 2.3f));
        }

        // stars pop in one after another
        if (complete.On)
        {
            starTimer += dt;
            for (int i = 0; i < stars.Length; i++)
            {
                float t = Mathf.Clamp01((starTimer - 0.25f - i * 0.28f) / 0.35f);
                float e = t < 1f ? 1f + 2.2f * Mathf.Pow(t - 1f, 3f) + 1.2f * Mathf.Pow(t - 1f, 2f) : 1f; // ease out back
                bool earned = i < starsEarned;
                stars[i].transform.localScale = Vector3.one * (earned ? e : Mathf.Min(e, 1f) * 0.8f);
                stars[i].material.SetColor("_ColTop", earned ? UIKit.Gold : new Color(0.35f, 0.32f, 0.45f));
                stars[i].material.SetColor("_ColBot", earned ? new Color(1f, 0.5f, 0.2f) : new Color(0.2f, 0.18f, 0.3f));
            }
        }

        // banner
        if (bannerTimer > 0f)
        {
            bannerTimer -= dt;
            float age = 2.1f - bannerTimer;
            float inK = Mathf.Clamp01(age / 0.35f);
            float outK = Mathf.Clamp01(bannerTimer / 0.4f);
            float a = Mathf.Min(inK, outK);
            float sc = Mathf.Lerp(1.5f, 1f, 1f - Mathf.Pow(1f - inK, 3f));
            bannerText.gameObject.SetActive(true);
            bannerSub.gameObject.SetActive(true);
            bannerText.color = new Color(1f, 1f, 1f, a);
            bannerSub.color = new Color(1f, 1f, 1f, a);
            bannerText.rectTransform.localScale = Vector3.one * sc;
            if (bannerTimer <= 0f)
            {
                bannerText.gameObject.SetActive(false);
                bannerSub.gameObject.SetActive(false);
            }
        }

        // popups
        foreach (var p in popups)
        {
            if (!p.Alive) continue;
            p.Age += dt;
            float t = p.Age / p.Life;
            if (t >= 1f)
            {
                p.Alive = false;
                p.Text.gameObject.SetActive(false);
                continue;
            }
            p.Text.rectTransform.anchoredPosition = p.Start + new Vector2(0f, 90f * (1f - Mathf.Pow(1f - t, 2f)));
            float pop = t < 0.15f ? Mathf.Lerp(0.4f, 1.2f, t / 0.15f) : Mathf.Lerp(1.2f, 1f, Mathf.Clamp01((t - 0.15f) / 0.2f));
            p.Text.rectTransform.localScale = Vector3.one * pop;
            p.Text.color = new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(0.55f, 1f, t));
        }

        // screen flash decays
        if (flash.color.a > 0.001f)
        {
            var fc = flash.color;
            fc.a = Mathf.MoveTowards(fc.a, 0f, dt * 1.6f);
            flash.color = fc;
        }
    }
}
