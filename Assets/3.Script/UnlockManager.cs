// =====================================================
// UnlockManager.cs - v7.0
// 단계적 UI/기능 해금 (Player 학습용 튜토리얼)
// Stage 진행에 따라 기능을 점진적으로 해금
// =====================================================
// 1. 시작: Enemy 공격UI + Gun UI 숨김, 적 공격 안함, Gun 로직 비활성
// 2. ~2 stage: Player만 공격 + Choco 타일만
// 3. 3 stage~: Enemy 공격 시작 + 공격 UI 활성화 + Berry만
// 4. 5 stage~: Choco+Berry 혼합 (기존 로직)
// 5. 7 stage~: Gun UI 반절 표시 (0/20), gauge 20 cap, freeze 불가
// 6. 9 stage~: Gun UI 전체 (0/40), gauge 40 cap, 가림막 비활성화
// 7. 새 UI는 DOTween으로 등장

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class UnlockManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BossManager bossManager;
    [SerializeField] private GunSystem gunSystem;

    [Header("Enemy Attack UI (3 stage에서 활성화)")]
    [SerializeField] private GameObject enemyAttackUIObj;

    [Header("Gun UI (7 stage에서 활성화)")]
    [SerializeField] private GameObject gunUIObj;

    [Header("Gun Gauge Cover (9 stage에서 비활성화)")]
    [SerializeField] private GameObject gaugeCoverObj;

    // 해금 상태
    private bool enemyAttackUnlocked = false;
    private bool gunUIUnlocked = false;
    private bool fullGaugeUnlocked = false;

    // 속성
    public bool IsEnemyAttackUnlocked => enemyAttackUnlocked;
    public bool IsGunUnlocked => gunUIUnlocked;
    public bool IsFullGaugeUnlocked => fullGaugeUnlocked;

    public void Initialize()
    {
        enemyAttackUnlocked = false;
        gunUIUnlocked = false;
        fullGaugeUnlocked = false;

        // 초기: Enemy 공격 UI + Gun UI 숨김
        if (enemyAttackUIObj != null) enemyAttackUIObj.SetActive(false);
        if (gunUIObj != null) gunUIObj.SetActive(false);
        if (gaugeCoverObj != null) gaugeCoverObj.SetActive(true);
    }

    // 보스 레벨 변경 시 호출 (BossManager에서 OnBossDefeated 후)
    public void OnStageChanged(int newStage)
    {
        // 3 stage: Enemy 공격 시작 + UI 활성화
        if (newStage >= 3 && !enemyAttackUnlocked)
        {
            enemyAttackUnlocked = true;
            if (enemyAttackUIObj != null)
            {
                enemyAttackUIObj.SetActive(true);
                AnimateUIAppear(enemyAttackUIObj);
            }
            Debug.Log("🔓 Unlock: Enemy Attack!");
        }

        // 7 stage: Gun UI 반절 표시
        if (newStage >= 7 && !gunUIUnlocked)
        {
            gunUIUnlocked = true;
            if (gunUIObj != null)
            {
                gunUIObj.SetActive(true);
                AnimateUIAppear(gunUIObj);
            }
            // 가림막 활성 상태 유지 (반절만 보이도록)
            if (gaugeCoverObj != null) gaugeCoverObj.SetActive(true);
            Debug.Log("🔓 Unlock: Gun UI (half gauge)!");
        }

        // 9 stage: 가림막 제거 → 전체 게이지
        if (newStage >= 9 && !fullGaugeUnlocked)
        {
            fullGaugeUnlocked = true;
            if (gaugeCoverObj != null)
            {
                // 가림막 페이드아웃 후 비활성화
                CanvasGroup cg = gaugeCoverObj.GetComponent<CanvasGroup>();
                if (cg == null) cg = gaugeCoverObj.AddComponent<CanvasGroup>();
                cg.DOFade(0f, 0.5f).SetEase(Ease.OutQuad)
                    .OnComplete(() => { if (gaugeCoverObj != null) gaugeCoverObj.SetActive(false); });
            }
            Debug.Log("🔓 Unlock: Full Gauge (40)!");
        }
    }

    // 타일 색상 결정 (stage에 따라)
    public TileColor GetTileColorForStage()
    {
        int stage = bossManager != null ? bossManager.GetBossLevel() : 1;

        if (stage <= 2)
            return TileColor.Choco;  // Choco만
        else if (stage <= 4)
            return TileColor.Berry;  // Berry만
        else
            return Random.value < 0.5f ? TileColor.Choco : TileColor.Berry;  // 혼합
    }

    // 머지 후 새 타일 색상 (stage에 따라)
    public TileColor GetMergeResultColorForStage()
    {
        return GetTileColorForStage();
    }

    // Enemy 공격 허용 여부
    public bool CanEnemyAttack()
    {
        return enemyAttackUnlocked;
    }

    // Gun 게이지 cap
    public int GetGaugeCap()
    {
        if (!gunUIUnlocked) return 0;        // Gun 미해금: 게이지 0 유지
        if (!fullGaugeUnlocked) return 20;   // 반절: 20 cap
        return 40;                            // 전체: 40 cap
    }

    // Freeze 가능 여부
    public bool CanFreeze()
    {
        return fullGaugeUnlocked;
    }

    // UI 등장 DOTween 효과
    void AnimateUIAppear(GameObject obj)
    {
        if (obj == null) return;

        // CanvasGroup 페이드인 + 스케일
        CanvasGroup cg = obj.GetComponent<CanvasGroup>();
        if (cg == null) cg = obj.AddComponent<CanvasGroup>();

        RectTransform rt = obj.GetComponent<RectTransform>();

        cg.alpha = 0f;
        if (rt != null) rt.localScale = Vector3.one * 0.8f;

        DOTween.Sequence()
            .Append(cg.DOFade(1f, 0.4f).SetEase(Ease.OutQuad))
            .Join(rt != null ? rt.DOScale(1f, 0.4f).SetEase(Ease.OutBack) : cg.DOFade(1f, 0.01f));
    }
}
