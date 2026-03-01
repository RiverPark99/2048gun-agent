// =====================================================
// UnlockManager.cs - v7.1
// 단계적 UI/기능 해금 (Player 학습용 튜토리얼)
// Stage 진행에 따라 기능을 점진적으로 해금
// =====================================================
// 1. 시작: Enemy 공격UI + Gun UI 숨김, 적 공격 안함, Gun 로직 비활성
// 2. ~2 stage: Player만 공격 + Choco 타일만
// 3. 3 stage~: Enemy 공격 시작 + 공격 UI 활성화 + Berry만
// 4. 5 stage~: Choco+Berry 혼합 (기존 로직)
// 5. 5 stage~: Gun UI 반절 표시 (0/20), gauge 20 cap, freeze 불가
// 6. 7 stage~: Gun UI 전체 (0/40), gauge 40 cap, 가림막 비활성화
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

    [Header("Gun UI (5 stage에서 활성화)")]
    [SerializeField] private GameObject gunUIObj;

    [Header("Gun Gauge Cover (7 stage에서 비활성화)")]
    [SerializeField] private GameObject gaugeCoverObj;

    [Header("튜토리얼 손가락 가이드 (Gun 버튼 안내)")]
    [SerializeField] private Image fingerGuideImage;
    [SerializeField] private Button gunButtonRef; // Gun 버튼 위치 참조

    [Header("해금 연출 암전 오버레이 (살짝 어두운 판)")]
    [SerializeField] private Image unlockDimOverlay;



    // 해금 상태
    private bool enemyAttackUnlocked = false;
    private bool gunUIUnlocked = false;
    private bool fullGaugeUnlocked = false;

    // 속성
    public bool IsEnemyAttackUnlocked => enemyAttackUnlocked;
    public bool IsGunUnlocked => gunUIUnlocked;
    public bool IsFullGaugeUnlocked => fullGaugeUnlocked;

    // 손가락 튜토리얼 상태
    private bool fingerGuideShown = false;
    private bool fingerGuideDismissed = false;
    private Sequence fingerGuideAnim;

    // UI 등장 애니메이션 중 입력 차단
    private bool isUnlockAnimating = false;
    public bool IsUnlockAnimating => isUnlockAnimating;

    public void Initialize()
    {
        enemyAttackUnlocked = false;
        gunUIUnlocked = false;
        fullGaugeUnlocked = false;
        fingerGuideShown = false;
        fingerGuideDismissed = false;
        isUnlockAnimating = false;

        if (enemyAttackUIObj != null) enemyAttackUIObj.SetActive(false);
        if (gunUIObj != null) gunUIObj.SetActive(false);
        if (gaugeCoverObj != null) gaugeCoverObj.SetActive(true);
        if (fingerGuideImage != null) fingerGuideImage.gameObject.SetActive(false);
        if (unlockDimOverlay != null) { unlockDimOverlay.color = new Color(unlockDimOverlay.color.r, unlockDimOverlay.color.g, unlockDimOverlay.color.b, 0f); unlockDimOverlay.gameObject.SetActive(false); }

    }

    // 보스 레벨 변경 시 호출 (BossManager에서 OnBossDefeated 후)
    public void OnStageChanged(int newStage)
    {
        // 3 stage: Enemy 공격 시작 + UI 활성화 + 회복력 UI 등장
        if (newStage >= 3 && !enemyAttackUnlocked)
        {
            enemyAttackUnlocked = true;
            if (enemyAttackUIObj != null)
            {
                enemyAttackUIObj.SetActive(true);
                AnimateUIAppear(enemyAttackUIObj, true); // 암전 사용
            }
            Debug.Log("🔓 Unlock: Enemy Attack UI!");
        }

        // 5 stage: Gun UI 반절 표시
        if (newStage >= 5 && !gunUIUnlocked)
        {
            gunUIUnlocked = true;
            // 해금 직후 0/20 표시 보장: GunSystem의 UpdateGunUI보다 먼저 실행
            if (gunSystem != null) gunSystem.ForceGaugeDisplayCap(20);
            if (gunUIObj != null)
            {
                gunUIObj.SetActive(true);
                AnimateUIAppear(gunUIObj, false); // 암전 없이
            }
            if (gaugeCoverObj != null) gaugeCoverObj.SetActive(true);
            Debug.Log("🔓 Unlock: Gun UI (half gauge)!");
        }

        // 7 stage: 20 UI → 40 UI 전환 + 가림막 제거
        if (newStage >= 7 && !fullGaugeUnlocked)
        {
            fullGaugeUnlocked = true;
            // 20 UI → 40 UI 전환
            if (gunSystem != null) gunSystem.SwitchToGunUI40();
            if (gaugeCoverObj != null)
            {
                CanvasGroup cg = gaugeCoverObj.GetComponent<CanvasGroup>();
                if (cg == null) cg = gaugeCoverObj.AddComponent<CanvasGroup>();
                // 깜빡깜빡 (2초) 후 사라지기
                Sequence coverSeq = DOTween.Sequence();
                for (int i = 0; i < 6; i++)
                {
                    coverSeq.Append(cg.DOFade(0.15f, 0.12f).SetEase(Ease.InOutSine));
                    coverSeq.Append(cg.DOFade(1f, 0.12f).SetEase(Ease.InOutSine));
                }
                coverSeq.Append(cg.DOFade(0f, 0.6f).SetEase(Ease.InQuad));
                coverSeq.OnComplete(() => { if (gaugeCoverObj != null) gaugeCoverObj.SetActive(false); });
            }
            Debug.Log("🔓 Unlock: Full Gauge (40) + UI Switch!");
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

    // === 손가락 튜토리얼 가이드 ===
    // 게이지 20 이상이면 나타남 (1회성)
    public void CheckFingerGuide(int gauge)
    {
        if (!gunUIUnlocked || fingerGuideDismissed || fingerGuideShown) return;
        if (gauge >= 20)
        {
            fingerGuideShown = true;
            ShowFingerGuide();
        }
    }

    // Gun 모드 진입 또는 총 발사 시 손가락 숨기기
    public void DismissFingerGuide()
    {
        if (!fingerGuideShown || fingerGuideDismissed) return;
        fingerGuideDismissed = true;
        StopFingerGuideAnim();
        if (fingerGuideImage != null)
        {
            fingerGuideImage.DOKill();
            fingerGuideImage.DOFade(0f, 0.3f).OnComplete(() => {
                if (fingerGuideImage != null) fingerGuideImage.gameObject.SetActive(false);
            });
        }
    }

    void ShowFingerGuide()
    {
        if (fingerGuideImage == null || gunButtonRef == null) return;
        fingerGuideImage.gameObject.SetActive(true);
        fingerGuideImage.color = new Color(1f, 1f, 1f, 0.9f);

        // ⭐ 루트 Canvas를 찾아 거기의 최상위 자식으로 이동 → 다른 UI에 가릴 일 없음
        Canvas rootCanvas = fingerGuideImage.canvas;
        if (rootCanvas != null)
        {
            Canvas[] parentCanvases = fingerGuideImage.GetComponentsInParent<Canvas>(true);
            foreach (var c in parentCanvases)
                if (c.isRootCanvas) { rootCanvas = c; break; }
            fingerGuideImage.transform.SetParent(rootCanvas.transform, true);
            fingerGuideImage.transform.SetAsLastSibling();
        }

        RectTransform fingerRT = fingerGuideImage.GetComponent<RectTransform>();
        RectTransform gunBtnRT = gunButtonRef.GetComponent<RectTransform>();

        Vector3 startPos = fingerRT.position;
        Vector3 endPos = gunBtnRT.position;

        StopFingerGuideAnim();
        fingerGuideAnim = DOTween.Sequence();
        fingerGuideAnim.Append(fingerRT.DOMove(endPos, 0.6f).SetEase(Ease.InOutSine));
        fingerGuideAnim.AppendInterval(0.15f);
        fingerGuideAnim.Append(fingerRT.DOMove(startPos, 0.6f).SetEase(Ease.InOutSine));
        fingerGuideAnim.AppendInterval(0.15f);
        fingerGuideAnim.SetLoops(-1, LoopType.Restart);
    }

    void StopFingerGuideAnim()
    {
        if (fingerGuideAnim != null) { fingerGuideAnim.Kill(); fingerGuideAnim = null; }
    }

    // UI 등장: 1.1초 대기 → 크게 시작 → 축소 + 깜빡깜빡 (6회, 느림→빠름) + 입력차단
    void AnimateUIAppear(GameObject obj, bool useDim = false)
    {
        if (obj == null) return;

        CanvasGroup cg = obj.GetComponent<CanvasGroup>();
        if (cg == null) cg = obj.AddComponent<CanvasGroup>();

        RectTransform rt = obj.GetComponent<RectTransform>();

        cg.alpha = 0f;
        if (rt != null) rt.localScale = Vector3.one * 1.8f;

        isUnlockAnimating = true;

        // 암전 오버레이 (3 stage 전용)
        if (useDim && unlockDimOverlay != null)
        {
            unlockDimOverlay.gameObject.SetActive(true);
            Color oc = unlockDimOverlay.color; oc.a = 0f; unlockDimOverlay.color = oc;
            unlockDimOverlay.DOKill();
            unlockDimOverlay.DOFade(1f, 0.5f).SetEase(Ease.OutQuad).SetDelay(0.8f);
        }

        Sequence seq = DOTween.Sequence();
        // 1.1초 대기
        seq.AppendInterval(1.1f);
        seq.AppendCallback(() => { if (cg != null) cg.alpha = 1f; });
        // 크게 시작 → 원래 사이즈로
        if (rt != null)
            seq.Append(rt.DOScale(1f, 0.5f).SetEase(Ease.OutBack));
        // 깜빡깜빡 6회 (느리게 → 빠르게 가속)
        seq.Append(cg.DOFade(0.15f, 0.14f).SetEase(Ease.InOutSine));
        seq.Append(cg.DOFade(1f, 0.14f).SetEase(Ease.InOutSine));
        seq.Append(cg.DOFade(0.15f, 0.13f).SetEase(Ease.InOutSine));
        seq.Append(cg.DOFade(1f, 0.13f).SetEase(Ease.InOutSine));
        seq.Append(cg.DOFade(0.15f, 0.11f).SetEase(Ease.InOutSine));
        seq.Append(cg.DOFade(1f, 0.11f).SetEase(Ease.InOutSine));
        seq.Append(cg.DOFade(0.15f, 0.09f).SetEase(Ease.InOutSine));
        seq.Append(cg.DOFade(1f, 0.09f).SetEase(Ease.InOutSine));
        seq.Append(cg.DOFade(0.15f, 0.07f).SetEase(Ease.InOutSine));
        seq.Append(cg.DOFade(1f, 0.07f).SetEase(Ease.InOutSine));
        seq.Append(cg.DOFade(0.15f, 0.06f).SetEase(Ease.InOutSine));
        seq.Append(cg.DOFade(1f, 0.06f).SetEase(Ease.InOutSine));
        // 완료 → 암전 페이드아웃 + 입력 차단 해제
        bool dimActive = useDim;
        seq.OnComplete(() =>
        {
            if (dimActive && unlockDimOverlay != null)
            {
                unlockDimOverlay.DOKill();
                unlockDimOverlay.DOFade(0f, 0.4f).SetEase(Ease.InQuad)
                    .OnComplete(() => {
                        if (unlockDimOverlay != null) unlockDimOverlay.gameObject.SetActive(false);
                        isUnlockAnimating = false;
                    });
            }
            else
            {
                isUnlockAnimating = false;
            }
        });
    }

}
