// =====================================================
// ClassicChocoMode.cs
// 클래식 2048 + 콤보 점수 시스템
//
// ── 규칙 ──
//   • 타일 색상: Choco 고정
//   • HP 시스템: 없음 (보스 없음)
//   • 점수: totalMergedValueBase × comboScoreMultipliers[mergeCount]
//   • 게임오버: 이동 불가 시
// =====================================================

using UnityEngine;
using TMPro;

public class ClassicChocoMode : GameModeBase
{
    [Header("점수 UI")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI bestScoreText;

    [Header("콤보 점수 배율 테이블")]
    [Tooltip("index = 머지 횟수. [0]=미사용, [1]=1콤보, [2]=2콤보, ...")]
    [SerializeField] private float[] comboScoreMultipliers = { 1f, 1f, 1.5f, 2.5f, 4f, 6f };

    private long bestScore = 0;
    private const string BestScoreKey = "BestScore_ClassicChoco";

    protected override void Awake()
    {
        base.Awake();
    }

    public override void OnModeStart()
    {
        base.OnModeStart();
        string saved = PlayerPrefs.GetString(BestScoreKey, "0");
        long.TryParse(saved, out bestScore);
        UpdateScoreUI();
    }

    // ─────────────────────────────────────────────
    // Template Method Hooks
    // ─────────────────────────────────────────────

    protected override long CalculateScore(TurnMergeSummary summary)
    {
        if (summary.mergeCount <= 0) return 0;
        float mult = GetComboMultiplier(summary.mergeCount);
        long score = (long)(summary.totalMergedValueBase * mult);
        Debug.Log($"[ClassicChoco] score={score} (base={summary.totalMergedValueBase} × {mult:F2})");
        return score;
    }

    // HP 시스템 없음 — 콤보 회복 불필요
    protected override void ApplyComboHeal(TurnMergeSummary summary) { }

    protected override void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.text = currentScore.ToString("N0");

        if (currentScore > bestScore)
        {
            bestScore = currentScore;
            PlayerPrefs.SetString(BestScoreKey, bestScore.ToString());
            PlayerPrefs.Save();
        }

        if (bestScoreText != null)
            bestScoreText.text = bestScore.ToString("N0");
    }

    protected override void OnGameOver()
    {
        Debug.Log($"[ClassicChoco] 이동 불가 — 게임오버. 최종 점수: {currentScore}");
        // TODO: GameOver UI 연결
    }

    // ─────────────────────────────────────────────
    // IGridEventListener
    // ─────────────────────────────────────────────

    public override void OnBoardFull() => OnGameOver();

    public override TileColor? GetSpawnTileColor()   => TileColor.Choco;
    public override TileColor? GetMergeResultColor() => TileColor.Choco;

    // ─────────────────────────────────────────────
    // 헬퍼
    // ─────────────────────────────────────────────

    float GetComboMultiplier(int mergeCount)
    {
        if (comboScoreMultipliers == null || comboScoreMultipliers.Length == 0) return 1f;
        int idx = Mathf.Clamp(mergeCount, 0, comboScoreMultipliers.Length - 1);
        return comboScoreMultipliers[idx];
    }
}
