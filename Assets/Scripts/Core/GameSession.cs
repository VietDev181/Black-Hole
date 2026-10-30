using UnityEngine;

public enum GameState
{
    Menu,
    Playing,
    Paused,
    LevelComplete,
    GameOver
}

/// <summary>Plain data for the current run. No Unity objects, no behaviour besides small helpers.</summary>
public class GameSession
{
    public GameState State = GameState.Menu;

    public int Level = 1;
    public int Score;
    public int LevelStartScore;
    public int Best;

    public float TimeTotal;
    public float TimeLeft;

    public float GoalMass;
    public float CollectedMass;

    public int Combo;
    public float ComboTimer;

    public int Stars;
    public int TimeBonus;

    public const float ComboWindow = 1.6f;

    public float Progress => GoalMass <= 0f ? 0f : Mathf.Clamp01(CollectedMass / GoalMass);
    public int Multiplier => Mathf.Min(5, 1 + Combo / 4);

    public void StartLevel(int level, float time, float goalMass)
    {
        Level = level;
        LevelStartScore = Score;
        TimeTotal = time;
        TimeLeft = time;
        GoalMass = goalMass;
        CollectedMass = 0f;
        Combo = 0;
        ComboTimer = 0f;
        Stars = 0;
        TimeBonus = 0;
    }

    public void ResetRun()
    {
        Score = 0;
        LevelStartScore = 0;
    }

    public int RegisterEat()
    {
        Combo = ComboTimer > 0f ? Combo + 1 : 1;
        ComboTimer = ComboWindow;
        return Multiplier;
    }

    public void TickCombo(float dt)
    {
        if (ComboTimer <= 0f) return;
        ComboTimer -= dt;
        if (ComboTimer <= 0f) Combo = 0;
    }

    public int ComputeStars()
    {
        float ratio = TimeTotal <= 0f ? 0f : TimeLeft / TimeTotal;
        return ratio >= 0.5f ? 3 : ratio >= 0.25f ? 2 : 1;
    }
}
