// =====================================================
// ChallengeMode.cs
// 기존 챌린지 모드(모드1) 로직을 GridManager에서 분리한 구현체
//
// ⚠️  Phase 1 이행 전략:
//     GridManager의 직접 참조 코드는 아직 그대로 남아 있음.
//     이 클래스는 병렬로 호출되어 동작을 검증하는 용도.
//     검증 완료 후 GridManager에서 직접 참조를 단계적으로 제거.
// =====================================================

using UnityEngine;
using System.Collections.Generic;

public class ChallengeMode : MonoBehaviour, IGridEventListener
{
    [Header("References - Inspector 또는 Awake 자동 탐색")]
    [SerializeField] private GunSystem gunSystem;
    [SerializeField] private PlayerHPSystem playerHP;
    [SerializeField] private BossBattleSystem bossBattle;
    [SerializeField] private BossManager bossManager;
    [SerializeField] private UnlockManager unlockManager;
    [SerializeField] private GridManager gridManager;

    private void Awake()
    {
        // Inspector 미할당 시 같은 GameObject에서 자동 탐색
        if (gunSystem == null)    gunSystem    = GetComponent<GunSystem>();
        if (playerHP == null)     playerHP     = GetComponent<PlayerHPSystem>();
        if (bossBattle == null)   bossBattle   = GetComponent<BossBattleSystem>();
        if (bossManager == null)  bossManager  = FindAnyObjectByType<BossManager>();
        if (unlockManager == null) unlockManager = GetComponent<UnlockManager>();
        if (gridManager == null)  gridManager  = GetComponent<GridManager>();
    }

    // ─────────────────────────────────────────────
    // IGridEventListener 구현
    // ─────────────────────────────────────────────

    public void OnTileMerged(MergeInfo info)
    {
        // Phase 1에서는 GridManager가 직접 처리 중이므로 아직 비워둠.
        // Phase 1 완료 후 MoveCoroutine 내 머지별 처리 로직이 여기로 이동.
    }

    public void OnTurnMergesComplete(TurnMergeSummary summary)
    {
        // Phase 1에서는 GridManager가 직접 처리 중이므로 아직 비워둠.
        // Phase 1 완료 후 MoveCoroutine 후처리 로직이 여기로 이동:
        //   - 보스 데미지 계산
        //   - Heat 회복
        //   - 게임오버 판정
        //   - Freeze/Fever 처리
    }

    public void OnAfterMove()
    {
        // Phase 1에서는 GridManager가 직접 처리 중이므로 아직 비워둠.
        // Phase 1 완료 후 AfterMove 내 보스 턴 진행 로직이 여기로 이동.
    }

    public void OnBoardFull()
    {
        // 이동 불가 + Gun 없으면 게임오버
        bool hasGun = gunSystem.HasBullet || (gunSystem.IsFeverMode && !gunSystem.FeverBulletUsed);
        if (!hasGun)
        {
            bossBattle.GameOver();
        }
        else
        {
            gunSystem.SetEmergencyFlash(true);
        }
    }

    public void OnTileSpawned(Vector2Int pos, int value)
    {
        // Gun 모드면 타일 테두리 갱신
        if (gunSystem != null && gunSystem.IsGunMode)
            gridManager.UpdateTileBorders();
    }

    public TileColor? GetSpawnTileColor()
    {
        if (unlockManager != null)
            return unlockManager.GetTileColorForStage();
        return null; // GridManager 기본값 사용
    }

    public TileColor? GetMergeResultColor()
    {
        if (unlockManager != null)
            return unlockManager.GetMergeResultColorForStage();
        return null; // GridManager 기본값 사용
    }
}
