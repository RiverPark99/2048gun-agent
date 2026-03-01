// =====================================================
// ClassicChocoMode.cs  v2.0
// 클래식 2048 + 콤보 점수 시스템
//
// ── 규칙 ──
//   • 타일 색상: Choco 고정
//   • HP 시스템: 없음 (보스 없음)
//   • 점수: totalMergedValueBase × comboScoreMultipliers[mergeCount]
//   • 게임오버: 이동 불가 시
//
// v2.0:
//   • PlayerPrefs 베스트 스코어 저장
//   • 스코어 플로팅 텍스트 (GameModeBase 공통)
//   • NewRecord 팝업 연출
//   • GameOver UI 연결
// =====================================================

using UnityEngine;
using TMPro;
using DG.Tweening;

public class ClassicChocoMode : GameModeBase
{
    [Header("점수 UI")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI bestScoreText;

    [Header("콤보 추가 점수 테이블 (배율 아님, 점수에 더해지는 고정 int)")]
    [Tooltip("index = 머지 횟수. [0]=미사용, [1]=1콤보(추가없음), [2]=2콤보, ...\n최종점수 = totalMergedValueBase + comboScoreBonus[mergeCount]")]
    [SerializeField] private int[] comboScoreBonus = { 0, 0, 50, 150, 350, 700 };

    [Header("GameOver UI")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI gameOverScoreText;
    [SerializeField] private TextMeshProUGUI gameOverBestText;

    // ─────────────────────────────────────────────
    // 베스트 스코어 Key
    // ─────────────────────────────────────────────
    protected override string BestScorePlayerPrefsKey => "BestScore_ClassicChoco";

    // ─────────────────────────────────────────────
    // 초기화
    // ─────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
    }

    public override void OnModeStart()
    {
        base.OnModeStart();   // currentScore=0, newRecord 초기화, bestScore 로드
        UpdateScoreUI();
    }

    // ─────────────────────────────────────────────
    // Template Method Hooks
    // ─────────────────────────────────────────────

    protected override long CalculateScore(TurnMergeSummary summary)
    {
        if (summary.mergeCount <= 0) return 0;
        int  bonus = GetComboBonus(summary.mergeCount);
        long score = summary.totalMergedValueBase + bonus;
        Debug.Log($"[ClassicChoco] score={score} (base={summary.totalMergedValueBase} + bonus={bonus})");
        return score;
    }

    // HP 없음 — 콤보 회복 불필요
    protected override void ApplyComboHeal(TurnMergeSummary summary) { }

    protected override void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.text = currentScore.ToString("N0");

        long best = LoadBestScore();
        if (bestScoreText != null)
            bestScoreText.text = best.ToString("N0");
    }

    protected override void OnGameOver()
    {
        Debug.Log($"[ClassicChoco] 이동 불가 — 게임오버. 최종 점수: {currentScore}");

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);

            if (gameOverScoreText != null)
                gameOverScoreText.text = currentScore.ToString("N0");

            if (gameOverBestText != null)
                gameOverBestText.text = LoadBestScore().ToString("N0");

            // 패널 페이드 인
            CanvasGroup cg = gameOverPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = gameOverPanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.DOFade(1f, 0.4f).SetEase(Ease.OutQuad);
        }
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

    int GetComboBonus(int mergeCount)
    {
        if (comboScoreBonus == null || comboScoreBonus.Length == 0) return 0;
        int idx = Mathf.Clamp(mergeCount, 0, comboScoreBonus.Length - 1);
        return comboScoreBonus[idx];
    }
}
