// =====================================================
// GameModeBase.cs
// ClassicChocoMode / BerryMode 공통 기반 (Template Method Pattern)
//
// ── 역할 ──
//   GridManager의 ChallengeMode 직접 참조 로직은 일절 건드리지 않는다.
//   이 클래스를 상속한 모드가 modeListener로 연결되면,
//   GridManager는 직접 처리 블록을 skip하고 OnTurnMergesComplete에 위임한다.
//
// ── Template Method 흐름 ──
//   OnTurnMergesComplete(summary)
//     ├─ ProcessMergeBonus(summary)   ← 머지별 bonus HP (override)
//     ├─ CalculateScore(summary)      ← 점수 계산 (abstract)
//     ├─ ApplyComboHeal(summary)      ← 콤보 회복 (virtual)
//     ├─ AddScore(score)              ← 점수 누적 (공통)
//     ├─ UpdateScoreUI()              ← UI 갱신 (abstract)
//     └─ OnTurnProcessed(summary)     ← 모드별 후처리 (virtual)
// =====================================================

using UnityEngine;

public abstract class GameModeBase : MonoBehaviour, IGridEventListener
{
    [Header("공통 참조")]
    [SerializeField] protected GridManager gridManager;
    [SerializeField] protected PlayerHPSystem playerHP;

    protected long currentScore = 0;

    // ─────────────────────────────────────────────
    // 초기화
    // ─────────────────────────────────────────────

    protected virtual void Awake()
    {
        if (gridManager == null) gridManager = GetComponent<GridManager>();
        if (playerHP   == null) playerHP   = GetComponent<PlayerHPSystem>();
    }

    /// <summary>게임 시작/리셋 시 모드 상태 초기화</summary>
    public virtual void OnModeStart()
    {
        currentScore = 0;
        UpdateScoreUI();
    }

    // ─────────────────────────────────────────────
    // IGridEventListener
    // ─────────────────────────────────────────────

    public virtual void OnTileMerged(MergeInfo info) { }

    /// <summary>
    /// Template Method: 한 턴 머지 완료 후 GridManager가 위임하는 진입점
    /// </summary>
    public void OnTurnMergesComplete(TurnMergeSummary summary)
    {
        // 1. 머지별 즉각 보너스 (Berry heal 등)
        ProcessMergeBonus(summary);

        // 2. 점수 계산
        long score = CalculateScore(summary);

        // 3. 콤보 회복
        ApplyComboHeal(summary);

        // 4. HP 반영 & UI
        if (playerHP != null)
        {
            playerHP.ClampHeat();
            int netChange = playerHP.CurrentHeat - summary.heatBefore;
            playerHP.UpdateHeatUI();
            if (netChange != 0) playerHP.ShowHeatChangeText(netChange);
            if (netChange > 0)  playerHP.FlashHealGreen();
        }

        // 5. 점수 반영
        AddScore(score);

        // 6. 게임오버 체크
        if (playerHP != null && playerHP.CurrentHeat <= 0)
        {
            OnGameOver();
            return;
        }

        // 7. 모드별 후처리
        OnTurnProcessed(summary);
    }

    public virtual void OnAfterMove() { }

    public virtual void OnBoardFull() => OnGameOver();

    public virtual void OnTileSpawned(Vector2Int pos, int value) { }

    public virtual TileColor? GetSpawnTileColor()   => null;
    public virtual TileColor? GetMergeResultColor() => null;

    // ─────────────────────────────────────────────
    // Template Method Hooks
    // ─────────────────────────────────────────────

    /// <summary>머지별 즉각 보너스 처리 (Berry heal, 이펙트 등)</summary>
    protected virtual void ProcessMergeBonus(TurnMergeSummary summary) { }

    /// <summary>이번 턴 획득 점수 계산 [필수 구현]</summary>
    protected abstract long CalculateScore(TurnMergeSummary summary);

    /// <summary>콤보 기반 HP 회복 적용</summary>
    protected virtual void ApplyComboHeal(TurnMergeSummary summary)
    {
        if (summary.mergeCount <= 0 || playerHP == null) return;

        int[] recover = playerHP.ComboHeatRecover;
        int idx = Mathf.Min(summary.mergeCount, recover.Length - 1);
        int heal = recover[idx];

        if (summary.hadBerryMerge) heal *= 2;
        if (heal > 0) playerHP.AddHeat(heal);
    }

    /// <summary>점수 누적 및 UI 갱신</summary>
    protected void AddScore(long score)
    {
        currentScore += score;
        UpdateScoreUI();
    }

    /// <summary>점수 UI 갱신 [필수 구현]</summary>
    protected abstract void UpdateScoreUI();

    /// <summary>턴 처리 완료 후 모드별 추가 동작</summary>
    protected virtual void OnTurnProcessed(TurnMergeSummary summary) { }

    /// <summary>게임오버 처리</summary>
    protected virtual void OnGameOver()
    {
        Debug.Log($"[{GetType().Name}] Game Over. Score: {currentScore}");
    }
}
