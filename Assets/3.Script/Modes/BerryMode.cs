// =====================================================
// BerryMode.cs
// Berry 색상 고정 + HP drain 생존 모드
//
// ── 규칙 ──
//   • 타일 색상: Berry 고정
//   • HP: 내부 1000, 표시 ÷10 → 100
//   • HP drain: 1초마다 레벨 기반 감소
//     level = floor(log2(최대 타일 값)) - 1  (최소 1)
//     drainPerLevel[level] SerializeField
//   • Berry merge 회복: berryHealAmount (int, SerializeField)
//   • 콤보 회복: ComboHeatRecover × 10 배
//   • 점수: totalMergedValueBase × comboScoreMultipliers[mergeCount]
//   • Continue: HP 최대 회복 + Gun count 20 지급 (1회)
//   • 게임오버: HP drain → 0
// =====================================================

using UnityEngine;
using TMPro;
using System.Collections;

public class BerryMode : GameModeBase
{
    [Header("점수 UI")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI bestScoreText;

    [Header("콤보 점수 배율 테이블")]
    [Tooltip("index = 머지 횟수. [0]=미사용, [1]=1콤보, [2]=2콤보, ...")]
    [SerializeField] private float[] comboScoreMultipliers = { 1f, 1f, 1.5f, 2.5f, 4f, 6f };

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

    [Header("Continue UI")]
    [SerializeField] private GameObject continuePanel;

    private long bestScore = 0;
    private const string BestScoreKey = "BestScore_Berry";

    private Coroutine drainCoroutine;
    private bool isContinueUsed = false;
    private const int ContinueGunCount = 20;

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

        string saved = PlayerPrefs.GetString(BestScoreKey, "0");
        long.TryParse(saved, out bestScore);

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
            Debug.Log($"[BerryMode] Berry merge ×{summary.berryMergeCount} → +{heal} HP");
        }
    }

    protected override long CalculateScore(TurnMergeSummary summary)
    {
        if (summary.mergeCount <= 0) return 0;
        float mult = GetComboMultiplier(summary.mergeCount);
        long score = (long)(summary.totalMergedValueBase * mult);
        Debug.Log($"[BerryMode] score={score} (base={summary.totalMergedValueBase} × {mult:F2})");
        return score;
    }

    // 콤보 회복 × 10배
    protected override void ApplyComboHeal(TurnMergeSummary summary)
    {
        if (summary.mergeCount <= 0 || playerHP == null) return;

        int[] recover = playerHP.ComboHeatRecover;
        int idx = Mathf.Min(summary.mergeCount, recover.Length - 1);
        int heal = recover[idx] * 10;

        if (summary.hadBerryMerge) heal *= 2;
        if (heal > 0) playerHP.AddHeat(heal);
    }

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
        StopDrain();
        Debug.Log($"[BerryMode] HP 소진 — 게임오버. 최종 점수: {currentScore}");
        // TODO: GameOver UI 연결
    }

    // ─────────────────────────────────────────────
    // IGridEventListener
    // ─────────────────────────────────────────────

    public override void OnBoardFull()
    {
        // Continue 미사용 + Gun 있으면 Continue, 아니면 게임오버
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
            SetGunBulletCount(ContinueGunCount);

        if (continuePanel != null)
            continuePanel.SetActive(true);

        Debug.Log($"[BerryMode] Continue! HP 최대 회복 + Gun ×{ContinueGunCount}");
        StartDrain();
    }

    // GunSystem에 SetBulletCount 공개 메서드가 없을 경우를 대비한 래퍼
    // GunSystem에 해당 메서드가 추가되면 직접 호출로 교체
    void SetGunBulletCount(int count)
    {
        // TODO: gunSystem.SetBulletCount(count) — GunSystem에 메서드 추가 후 교체
        Debug.Log($"[BerryMode] Gun count {count} 지급 예정 (GunSystem.SetBulletCount 미구현)");
    }

    // ─────────────────────────────────────────────
    // 유틸
    // ─────────────────────────────────────────────

    public static int ToDisplayHP(int internalHP) => internalHP / 10;

    float GetComboMultiplier(int mergeCount)
    {
        if (comboScoreMultipliers == null || comboScoreMultipliers.Length == 0) return 1f;
        int idx = Mathf.Clamp(mergeCount, 0, comboScoreMultipliers.Length - 1);
        return comboScoreMultipliers[idx];
    }
}
