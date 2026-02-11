// =====================================================
// GameManager.cs - v6.0
// 게임 코디네이터: 입력 처리, 시스템 초기화, 상태 중계
// =====================================================

using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("System References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private GunSystem gunSystem;
    [SerializeField] private PlayerHPSystem playerHP;
    [SerializeField] private BossBattleSystem bossBattle;
    [SerializeField] private BossManager bossManager;

    void Start()
    {
        // 각 시스템 초기화
        gridManager.Initialize();
        playerHP.Initialize();
        gunSystem.Initialize();
        bossBattle.Initialize();

        // 게임 시작
        gridManager.StartNewGame();
        gunSystem.UpdateGunUI();
        gridManager.UpdateTurnUI();
    }

    void Update()
    {
        if (bossBattle.ShouldBlockInput()) return;

        if (!gunSystem.IsGunMode)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                gridManager.Move(Vector2Int.down);
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                gridManager.Move(Vector2Int.up);
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                gridManager.Move(Vector2Int.left);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                gridManager.Move(Vector2Int.right);
        }

        if (gunSystem.IsGunMode && Input.GetMouseButtonDown(0))
        {
            gunSystem.ShootTile();
        }
    }

    // === 다른 시스템에서 호출하는 중계 메서드 ===
    // BossManager가 기존에 GameManager를 참조하던 부분 호환용

    public void SetBossAttacking(bool attacking)
    {
        bossBattle.SetBossAttacking(attacking);
    }

    public bool IsBossAttacking()
    {
        return bossBattle.IsBossAttacking;
    }

    public void SetBossTransitioning(bool transitioning)
    {
        bossBattle.SetBossTransitioning(transitioning);
    }

    public void TakeBossAttack(int damage)
    {
        bossBattle.TakeBossAttack(damage);
    }

    public void OnBossDefeated()
    {
        bossBattle.OnBossDefeated();
    }

    public void UpdateTurnUI()
    {
        gridManager.UpdateTurnUI();
    }
}
