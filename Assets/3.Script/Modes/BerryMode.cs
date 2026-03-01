// =====================================================
// BerryMode.cs  v2.0
// Berry 색상 고정 + HP drain 생존 모드
//
// v2.0:
//   • GameModeBase 공통 베스트 스코어 / 플로팅 텍스트 / NewRecord 연출 적용
// =====================================================

using UnityEngine;
using TMPro;
using System.Collections;
using DG.Tweening;

public class BerryMode : GameModeBase
{
    [Header("점수 UI")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI bestScoreText;

    [Header("콤보 추가 점수 테이블 (배율 아님, 점수에 더해지는 고정 int)")]
    [Tooltip("index = 머지 횟수. [0]=미사용, [1]=1콤보(추가없음), [2]=2콤보, ...\n최종점수 = totalMergedValueBase + comboScoreBonus[mergeCount]")]
    [SerializeField] private int[] comboScoreBonus = { 0, 0, 50, 150, 350, 700 };

    [Header("HP 설정")]
    [Tooltip("내부 최대 HP (표시는 ÷10)")]
    [SerializeField] private int internalMaxHP = 1000;

    [Header("HP Drain (레벨별 초당 감소량, 내부값)")]
    [Tooltip("index = level (1~11). level = floor(log2(최대타일값)) - 1")]
    [SerializeField] private int[] drainPerLevel = { 0, 2, 3, 5, 7, 10, 14, 19, 25, 33, 43, 55 };

    [Header("Berry Merge 회복량 (내부값, int)")]
    [SerializeField] private int berryHealAmount = 50;

    [Header("Gun 참조 (Continue용)")]
    [SerializeField] private GunSystem gunSystem;

    [Header("Continue / GameOver UI")]
    [SerializeField] private GameObject continuePanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI gameOverScoreText;
    [SerializeField] private TextMeshProUGUI gameOverBestText;

    private Coroutine drainCoroutine;
    private bool isContinueUsed = false;
    private const int ContinueGunCount = 20;

    protected override string BestScorePlayerPrefsKey => "BestScore_Berry";

    // ─────────────────────────────────────────────
    // 초기화
    // ─────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        if (gunSystem == null) gunSystem = GetComponent<GunSystem>();
    }

    public override void OnModeStart()
    {
        base.OnModeStart();

        if (playerHP != null)
        {
            playerHP.SetMaxHeatDirect(internalMaxHP);
            playerHP.SetHeatToMax();
            playerHP.UpdateHeatUI(true);
        }

        isContinueUsed = false;
        UpdateScoreUI();
        StartDrain();
    }

    void OnDestroy() => StopDrain();

    // ─────────────────────────────────────────────
    // HP Drain
    // ─────────────────────────────────────────────

    void StartDrain()
    {
        StopDrain();
        drainCoroutine = StartCoroutine(DrainCoroutine());
    }

    void StopDrain()
    {
        if (drainCoroutine != null) { StopCoroutine(drainCoroutine); drainCoroutine = null; }
    }

    IEnumerator DrainCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            if (playerHP == null) yield break;

            int level = GetCurrentLevel();
            int drain = GetDrainForLevel(level);

            playerHP.AddHeat(-drain);
            playerHP.ClampHeat();
            playerHP.UpdateHeatUI();

            if (playerHP.CurrentHeat <= 0)
            {
                HandleDeath();
                yield break;
            }
        }
    }

    void HandleDeath()
    {
        if (!isContinueUsed && gunSystem != null && gunSystem.HasBullet)
            TryContinue();
        else
            OnGameOver();
    }

    int GetCurrentLevel()
    {
        if (gridManager == null) return 1;
        int maxVal = gridManager.GetMaxTileValue();
        if (maxVal <= 1) return 1;
        int level = Mathf.FloorToInt(Mathf.Log(maxVal, 2)) - 1;
        return Mathf.Clamp(level, 1, drainPerLevel.Length - 1);
    }

    int GetDrainForLevel(int level)
    {
        if (drainPerLevel == null || drainPerLevel.Length == 0) return 2;
        int idx = Mathf.Clamp(level, 0, drainPerLevel.Length - 1);
        return drainPerLevel[idx];
    }

    // ─────────────────────────────────────────────
    // Template Method Hooks
    // ─────────────────────────────────────────────

    protected override void ProcessMergeBonus(TurnMergeSummary summary)
    {
        if (summary.berryMergeCount > 0 && playerHP != null)
        {
            int heal = berryHealAmount * summary.berryMergeCount;
            playerHP.AddHeat(heal);
        }
    }

    protected override long CalculateScore(TurnMergeSummary summary)
    {
        if (summary.mergeCount <= 0) return 0;
        int  bonus = GetComboBonus(summary.mergeCount);
        long score = summary.totalMergedValueBase + bonus;
        Debug.Log($"[BerryMode] score={score} (base={summary.totalMergedValueBase} + bonus={bonus})");
        return score;
    }

    protected override void ApplyComboHeal(TurnMergeSummary summary)
    {
        if (summary.mergeCount <= 0 || playerHP == null) return;
        int[] recover = playerHP.ComboHeatRecover;
        int idx  = Mathf.Min(summary.mergeCount, recover.Length - 1);
        int heal = recover[idx] * 10;
        if (heal > 0) playerHP.AddHeat(heal);
    }

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
        StopDrain();
        Debug.Log($"[BerryMode] Game Over. Score: {currentScore}");

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            if (gameOverScoreText != null) gameOverScoreText.text = currentScore.ToString("N0");
            if (gameOverBestText  != null) gameOverBestText.text  = LoadBestScore().ToString("N0");

            CanvasGroup cg = gameOverPanel.GetComponent<CanvasGroup>()
                          ?? gameOverPanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.DOFade(1f, 0.4f).SetEase(Ease.OutQuad);
        }
    }

    // ─────────────────────────────────────────────
    // IGridEventListener
    // ─────────────────────────────────────────────

    public override void OnBoardFull()
    {
        if (!isContinueUsed && gunSystem != null && gunSystem.HasBullet)
            TryContinue();
        else
            OnGameOver();
    }

    public override TileColor? GetSpawnTileColor()   => TileColor.Berry;
    public override TileColor? GetMergeResultColor() => TileColor.Berry;

    // ─────────────────────────────────────────────
    // Continue
    // ─────────────────────────────────────────────

    void TryContinue()
    {
        isContinueUsed = true;
        StopDrain();

        if (playerHP != null)
        {
            playerHP.SetHeatToMax();
            playerHP.UpdateHeatUI();
        }

        if (gunSystem != null)
            gunSystem.SetBulletCount(ContinueGunCount);

        if (continuePanel != null)
            continuePanel.SetActive(true);

        Debug.Log($"[BerryMode] Continue! HP max + Gun x{ContinueGunCount}");
        StartDrain();
    }

    // ─────────────────────────────────────────────
    // 유틸
    // ─────────────────────────────────────────────

    public static int ToDisplayHP(int internalHP) => internalHP / 10;

    int GetComboBonus(int mergeCount)
    {
        if (comboScoreBonus == null || comboScoreBonus.Length == 0) return 0;
        int idx = Mathf.Clamp(mergeCount, 0, comboScoreBonus.Length - 1);
        return comboScoreBonus[idx];
    }
}
