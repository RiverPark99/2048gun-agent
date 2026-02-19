// =====================================================
// GunSystem.cs - v6.7
// Gun 모드, Freeze(게이지 0~40)
// Freeze: 40/40 진입, 20 이하 종료, 턴당 1.06배율
// Freeze Gun: 즉시 -20 → Freeze 종료
// Continue 횟수 제한 2회
// ATK+/Freeze Turn: 주황↔검정 색상 루프
// Combo: -2 합산 후 한 번만 표시
// =====================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using DG.Tweening;

public class GunSystem : MonoBehaviour
{
    [Header("Gun UI")]
    [SerializeField] private Button gunButton;
    [SerializeField] private TextMeshProUGUI bulletCountText;
    [SerializeField] private TextMeshProUGUI turnsUntilBulletText;
    [SerializeField] private TextMeshProUGUI attackPowerText;
    [SerializeField] private TextMeshProUGUI gunModeGuideText;
    [SerializeField] private Image gunButtonImage;
    [SerializeField] private RectTransform progressBarFill;

    [Header("Gauge Change Text")]
    [SerializeField] private GameObject damageTextPrefab;
    [SerializeField] private Transform damageTextParent;

    [Header("Freeze Effects")]
    [SerializeField] private Transform feverParticleSpawnPoint;
    [SerializeField] private Image feverBackgroundImage;
    [SerializeField] private Image freezeImage1;

    [Header("Gun Mode Visual")]
    [SerializeField] private Image gunModeOverlayImage;
    [SerializeField] private Image hpBarBackgroundImage;
    [SerializeField] private Image progressBarGlowOverlay;

    [Header("Freeze UI")]
    [SerializeField] private TextMeshProUGUI freezeTurnText;
    [SerializeField] private TextMeshProUGUI freezeTotalDamageText;

    [Header("Continue")]
    [SerializeField] private TextMeshProUGUI continueGuideText;

    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private PlayerHPSystem playerHP;
    [SerializeField] private BossBattleSystem bossBattle;
    [SerializeField] private BossManager bossManager;

    // 상수
    private const int GAUGE_MAX = 40;
    private const int GAUGE_FOR_BULLET = 20;
    private const int FREEZE_MOVE_COST = 2;
    private const int FREEZE_COMBO_BONUS = 2;
    private const int GUN_SHOT_COST = 20;
    private const float FREEZE_TURN_MULTIPLIER = 1.06f;
    private const int MAX_CONTINUES = 2;

    // Freeze 색상 루프 상수
    private static readonly Color FREEZE_ORANGE = new Color(1f, 0.6f, 0.1f, 1f);
    private static readonly Color FREEZE_BLACK  = new Color(0f, 0f, 0f, 1f);

    // Gauge & Fever 상태
    private int mergeGauge = 0;
    private bool hasBullet = false;
    private bool isFeverMode = false;
    private bool feverBulletUsed = false;

    // Freeze 턴 배율
    private int freezeTurnCount = 0;
    private long freezeTotalDamage = 0;

    // ATK 보너스
    private long feverMergeIncreaseAtk = 1;
    private long permanentAttackPower = 0;

    // Gun 모드
    private bool isGunMode = false;
    private Sequence hpBarGunModeAnim;
    private Color hpBarOriginalBgColor;
    private bool hpBarBgColorSaved = false;

    // Progress bar glow
    private Sequence progressBarGlowAnim;

    // UI 상태
    private Tweener gunButtonHeartbeat;
    private bool lastGunButtonAnimationState = false;
    private float turnsTextOriginalY = 0f;
    private bool turnsTextInitialized = false;
    private float attackTextOriginalY = 0f;
    private bool attackTextInitialized = false;
    private long lastPermanentAttackPower = 0;
    private int lastMergeGauge = -1;

    // Progress bar
    private Color progressBarOriginalColor;
    private bool progressBarColorSaved = false;

    // 파티클
    private GameObject activeFeverParticle;

    // 긴급 깜빡임
    private Sequence emergencyGunFlash;
    private bool isEmergencyFlashing = false;

    // ATK+ 시소
    private Sequence atkBobAnim;
    private float atkOriginalX = 0f;
    private bool atkOriginalXSaved = false;

    // ATK 색상
    private Color atkOriginalColor = Color.black;
    private bool atkColorSaved = false;
    private Sequence atkFreezeColorAnim;   // 주황↔검정
    private Sequence freezeTurnColorAnim;  // 주황↔검정
    private Sequence freezeTotalDmgColorAnim; // 주황↔검정
    private Sequence freezeTotalDmgBobAnim;   // bob 효과

    // Freeze UI 원래 위치 저장
    private Vector2 freezeTurnOriginalPos;
    private bool freezeTurnPosSaved = false;
    private Vector2 freezeTotalDmgOriginalPos;
    private bool freezeTotalDmgPosSaved = false;

    // Freeze total damage 원래 색상
    private Color freezeTotalDmgOriginalColor = Color.white;
    private bool freezeTotalDmgColorSaved = false;

    // Continue 횟수
    private static int continueCount = 0;

    // === 프로퍼티 ===
    public bool IsFeverMode => isFeverMode;
    public bool IsGunMode => isGunMode;
    public bool HasBullet => hasBullet;
    public bool FeverBulletUsed => feverBulletUsed;
    public int MergeGauge => mergeGauge;
    public long PermanentAttackPower => permanentAttackPower;
    public long FeverMergeIncreaseAtk => feverMergeIncreaseAtk;
    public int ContinuesRemaining => MAX_CONTINUES - continueCount;
    public float GetFreezeDamageMultiplier() { return Mathf.Pow(FREEZE_TURN_MULTIPLIER, freezeTurnCount); }

    public void AddFreezeTotalDamage(long dmg)
    {
        freezeTotalDamage += dmg;
        if (freezeTotalDamageText != null && freezeTotalDamageText.gameObject.activeSelf)
        {
            freezeTotalDamageText.text = $"{freezeTotalDamage:N0}";
            RectTransform rt = freezeTotalDamageText.GetComponent<RectTransform>();
            rt.DOKill();
            rt.localScale = Vector3.one;
            if (freezeTotalDmgPosSaved)
            {
                rt.anchoredPosition = freezeTotalDmgOriginalPos;
                rt.DOAnchorPosY(freezeTotalDmgOriginalPos.y + 4f, 0.08f).SetEase(Ease.OutQuad)
                    .OnComplete(() => { if (rt != null) rt.DOAnchorPosY(freezeTotalDmgOriginalPos.y, 0.1f).SetEase(Ease.InQuad); });
            }
        }
    }

    public void Initialize()
    {
        if (freezeImage1 == null)
        {
            GameObject obj = GameObject.Find("infoFreeze");
            if (obj != null) freezeImage1 = obj.GetComponent<Image>();
        }
        if (freezeImage1 != null) { freezeImage1.color = new Color(1f, 1f, 1f, 70f / 255f); freezeImage1.gameObject.SetActive(false); }

        if (progressBarFill != null && !progressBarColorSaved)
        {
            Image fillImg = progressBarFill.GetComponent<Image>();
            if (fillImg != null) { progressBarOriginalColor = fillImg.color; progressBarColorSaved = true; }
        }

        if (gunButton != null) gunButton.onClick.AddListener(ToggleGunMode);
        if (gunModeOverlayImage != null) gunModeOverlayImage.gameObject.SetActive(false);

        if (hpBarBackgroundImage != null && !hpBarBgColorSaved)
        {
            hpBarOriginalBgColor = hpBarBackgroundImage.color;
            hpBarBgColorSaved = true;
        }

        if (progressBarGlowOverlay != null)
        {
            Color c = progressBarGlowOverlay.color; c.a = 0f; progressBarGlowOverlay.color = c;
            progressBarGlowOverlay.gameObject.SetActive(false);
        }

        if (freezeTurnText != null)
        {
            if (!freezeTurnPosSaved) { freezeTurnOriginalPos = freezeTurnText.GetComponent<RectTransform>().anchoredPosition; freezeTurnPosSaved = true; }
            freezeTurnText.gameObject.SetActive(false);
        }
        if (freezeTotalDamageText != null)
        {
            if (!freezeTotalDmgPosSaved) { freezeTotalDmgOriginalPos = freezeTotalDamageText.GetComponent<RectTransform>().anchoredPosition; freezeTotalDmgPosSaved = true; }
            if (!freezeTotalDmgColorSaved) { freezeTotalDmgOriginalColor = freezeTotalDamageText.color; freezeTotalDmgColorSaved = true; }
            freezeTotalDamageText.gameObject.SetActive(false);
        }

        if (attackPowerText != null && !atkColorSaved)
        {
            atkOriginalColor = attackPowerText.color;
            atkColorSaved = true;
        }

        continueCount = 0;
        UpdateContinueGuideText();
        UpdateGunUI();
    }

    public void ResetState()
    {
        mergeGauge = 0; hasBullet = false; isFeverMode = false;
        feverMergeIncreaseAtk = 1; permanentAttackPower = 0;
        feverBulletUsed = false; isGunMode = false;
        freezeTurnCount = 0; freezeTotalDamage = 0;
        lastPermanentAttackPower = 0; lastMergeGauge = -1;

        if (gunButtonHeartbeat != null) { gunButtonHeartbeat.Kill(); gunButtonHeartbeat = null; }
        if (atkBobAnim != null) { atkBobAnim.Kill(); atkBobAnim = null; }
        StopFreezeColorLoops();
        if (gunModeGuideText != null) gunModeGuideText.gameObject.SetActive(false);
        if (freezeImage1 != null) freezeImage1.gameObject.SetActive(false);
        if (activeFeverParticle != null) { Destroy(activeFeverParticle); activeFeverParticle = null; }
        if (feverBackgroundImage != null) { feverBackgroundImage.DOKill(); feverBackgroundImage.gameObject.SetActive(false); }
        if (gunModeOverlayImage != null) gunModeOverlayImage.gameObject.SetActive(false);
        ForceResetFreezeUITransforms();
        if (freezeTurnText != null) freezeTurnText.gameObject.SetActive(false);
        if (freezeTotalDamageText != null) freezeTotalDamageText.gameObject.SetActive(false);

        // ATK 색상 복원
        if (attackPowerText != null && atkColorSaved)
        {
            attackPowerText.DOKill();
            attackPowerText.color = atkOriginalColor;
        }

        StopProgressBarGlow();
        RestoreProgressBarColor();
        StopHPBarGunModeAnim();
        StopEmergencyFlash();
        UpdateGunUI();
    }

    // === 게이지 ===
    public void AddMergeGauge(int amount)
    {
        int before = mergeGauge;
        mergeGauge += amount;
        if (mergeGauge > GAUGE_MAX) mergeGauge = GAUGE_MAX;
    }

    public void UpdateGaugeUIOnly() { UpdateGunUI(); }
    public void AddFeverMergeATK() { permanentAttackPower += feverMergeIncreaseAtk; }
    public void ClearFeverPaybackIfNeeded() { }

    // === Freeze 턴 처리 ===
    // #1 수정: combo bonus + move cost 합산 후 1회만 표시
    public void ProcessFreezeAfterMove(int comboCount)
    {
        if (!isFeverMode) return;

        freezeTurnCount++;
        int gaugeBeforeAll = mergeGauge;

        // 콤보 보너스
        if (comboCount >= 2)
        {
            int bonus = FREEZE_COMBO_BONUS * comboCount;
            mergeGauge += bonus;
            if (mergeGauge > GAUGE_MAX) mergeGauge = GAUGE_MAX;
        }

        // 이동 비용
        mergeGauge -= FREEZE_MOVE_COST;

        // 합산 결과
        int netChange = mergeGauge - gaugeBeforeAll;
        bool isCombo = (comboCount >= 2);
        if (netChange != 0)
            ShowGaugeChangeText(netChange, isCombo);

        UpdateFreezeTurnUI();

        if (mergeGauge <= GAUGE_FOR_BULLET) EndFever();

        UpdateGunUI();
    }

    // === Gauge & Fever 체크 ===
    public void CheckGaugeAndFever()
    {
        if (isFeverMode) return;

        if (mergeGauge >= GAUGE_MAX)
            StartFever();
        else if (mergeGauge >= GAUGE_FOR_BULLET && !hasBullet)
        {
            hasBullet = true;
            UpdateGunButtonAnimation();
        }

        UpdateGunUI();
    }

    public IEnumerator DelayedFreezeCheck()
    {
        if (gunButtonImage != null)
        {
            while (gunButtonImage.color.a < 0.99f)
                yield return null;
        }
        yield return null;
        CheckGaugeAndFever();
    }

    void StartFever()
    {
        SpawnFeverParticle();

        if (feverBackgroundImage != null)
        {
            feverBackgroundImage.gameObject.SetActive(true);
            Color c = feverBackgroundImage.color; c.a = 1.0f; feverBackgroundImage.color = c;
            feverBackgroundImage.DOKill();
            feverBackgroundImage.DOFade(0.7f, 0.5f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
        }

        if (freezeImage1 != null) freezeImage1.gameObject.SetActive(true);
        if (bossManager != null) bossManager.SetFrozen(true);
        FireFeverFreezeLaser();

        isFeverMode = true;
        feverBulletUsed = false;
        mergeGauge = GAUGE_MAX;
        hasBullet = false;
        freezeTurnCount = 0;
        freezeTotalDamage = 0;

        UpdateGunButtonAnimation();
        SetProgressBarFreezeColor();
        StartAtkBobAnimation();
        StartFreezeColorLoops();

        // #4: "Freeze Turn" 명시
        if (freezeTurnText != null) { freezeTurnText.gameObject.SetActive(true); freezeTurnText.text = "Freeze Turn 0"; }
        if (freezeTotalDamageText != null) { freezeTotalDamageText.gameObject.SetActive(true); freezeTotalDamageText.text = "0"; }

        if (!bossManager.IsClearMode()) feverMergeIncreaseAtk++;
    }

    void EndFever()
    {
        if (activeFeverParticle != null) { Destroy(activeFeverParticle); activeFeverParticle = null; }
        if (feverBackgroundImage != null) { feverBackgroundImage.DOKill(); feverBackgroundImage.gameObject.SetActive(false); }
        if (freezeImage1 != null) freezeImage1.gameObject.SetActive(false);
        if (bossManager != null) bossManager.SetFrozen(false);

        isFeverMode = false;
        feverBulletUsed = false;
        RestoreProgressBarColor();
        StopAtkBobAnimation();
        StopFreezeColorLoops();

        ForceResetFreezeUITransforms();
        AnimateAndHideFreezeUI();

        hasBullet = (mergeGauge >= GAUGE_FOR_BULLET);
        freezeTurnCount = 0;

        UpdateGunUI();
    }

    void ForceResetFreezeUITransforms()
    {
        if (freezeTurnText != null)
        {
            freezeTurnText.DOKill();
            RectTransform rt = freezeTurnText.GetComponent<RectTransform>();
            rt.DOKill(); rt.localScale = Vector3.one;
            if (freezeTurnPosSaved) rt.anchoredPosition = freezeTurnOriginalPos;
            CanvasGroup cg = freezeTurnText.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;
        }
        if (freezeTotalDamageText != null)
        {
            freezeTotalDamageText.DOKill();
            RectTransform rt = freezeTotalDamageText.GetComponent<RectTransform>();
            rt.DOKill(); rt.localScale = Vector3.one;
            if (freezeTotalDmgPosSaved) rt.anchoredPosition = freezeTotalDmgOriginalPos;
            if (freezeTotalDmgColorSaved) freezeTotalDamageText.color = freezeTotalDmgOriginalColor;
            CanvasGroup cg = freezeTotalDamageText.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;
        }
        // ATK 색상 복원
        if (attackPowerText != null)
        {
            attackPowerText.DOKill();
            if (atkColorSaved)
            {
                Color c = atkOriginalColor;
                c.a = 0.35f;
                attackPowerText.color = c;
            }
        }
    }

    void AnimateAndHideFreezeUI()
    {
        float fadeDelay = 1.5f;
        float fadeDuration = 0.6f;

        if (freezeTurnText != null && freezeTurnText.gameObject.activeSelf)
        {
            RectTransform rt = freezeTurnText.GetComponent<RectTransform>();
            CanvasGroup cg = freezeTurnText.GetComponent<CanvasGroup>();
            if (cg == null) cg = freezeTurnText.gameObject.AddComponent<CanvasGroup>();
            rt.DOKill(); cg.DOKill(); freezeTurnText.DOKill();
            cg.alpha = 1f; rt.localScale = Vector3.one;
            // 색상 복원 (검정으로)
            freezeTurnText.color = FREEZE_BLACK;

            DOTween.Sequence()
                .AppendInterval(fadeDelay)
                .Append(cg.DOFade(0f, fadeDuration).SetEase(Ease.InQuad))
                .OnComplete(() => {
                    if (freezeTurnText == null) return;
                    freezeTurnText.gameObject.SetActive(false);
                    cg.alpha = 1f; rt.localScale = Vector3.one;
                    if (freezeTurnPosSaved) rt.anchoredPosition = freezeTurnOriginalPos;
                });
        }

        if (freezeTotalDamageText != null && freezeTotalDamageText.gameObject.activeSelf)
        {
            freezeTotalDamageText.text = $"{freezeTotalDamage:N0}";
            RectTransform rt = freezeTotalDamageText.GetComponent<RectTransform>();
            CanvasGroup cg = freezeTotalDamageText.GetComponent<CanvasGroup>();
            if (cg == null) cg = freezeTotalDamageText.gameObject.AddComponent<CanvasGroup>();
            rt.DOKill(); cg.DOKill(); freezeTotalDamageText.DOKill();
            cg.alpha = 1f; rt.localScale = Vector3.one;
            if (freezeTotalDmgPosSaved) rt.anchoredPosition = freezeTotalDmgOriginalPos;

            // 검정색으로 고정 + LevelUpMaxHP 스타일 팝 효과
            freezeTotalDamageText.color = FREEZE_BLACK;
            rt.localScale = Vector3.one * 1.4f;
            rt.DOScale(1f, 0.25f).SetEase(Ease.OutBack);

            DOTween.Sequence()
                .AppendInterval(fadeDelay)
                .Append(cg.DOFade(0f, fadeDuration).SetEase(Ease.InQuad))
                .OnComplete(() => {
                    if (freezeTotalDamageText == null) return;
                    freezeTotalDamageText.gameObject.SetActive(false);
                    cg.alpha = 1f; rt.localScale = Vector3.one;
                    if (freezeTotalDmgPosSaved) rt.anchoredPosition = freezeTotalDmgOriginalPos;
                    if (freezeTotalDmgColorSaved) freezeTotalDamageText.color = freezeTotalDmgOriginalColor;
                });
        }
    }

    void UpdateFreezeTurnUI()
    {
        if (freezeTurnText != null && isFeverMode)
        {
            float mult = GetFreezeDamageMultiplier();
            freezeTurnText.text = $"Freeze Turn {freezeTurnCount} (x{mult:F2})";
            RectTransform rt = freezeTurnText.GetComponent<RectTransform>();
            rt.DOKill();
            rt.localScale = Vector3.one;
            rt.DOScale(1.03f, 0.06f).SetEase(Ease.OutQuad)
                .OnComplete(() => { if (rt != null) rt.DOScale(1f, 0.08f).SetEase(Ease.InQuad); });
        }
    }

    // === #3 + #4: 주황↔검정 색상 루프 (ATK + Freeze Turn 동기화) ===
    void StartFreezeColorLoops()
    {
        StopFreezeColorLoops();

        // ATK+ 색상 루프
        if (attackPowerText != null)
        {
            attackPowerText.DOKill();
            attackPowerText.color = FREEZE_ORANGE;
            atkFreezeColorAnim = DOTween.Sequence();
            atkFreezeColorAnim.Append(attackPowerText.DOColor(FREEZE_BLACK, 1.2f).SetEase(Ease.InOutSine));
            atkFreezeColorAnim.Append(attackPowerText.DOColor(FREEZE_ORANGE, 1.2f).SetEase(Ease.InOutSine));
            atkFreezeColorAnim.SetLoops(-1, LoopType.Restart);
        }

        // Freeze Turn 색상 루프 (동일 타이밍)
        if (freezeTurnText != null)
        {
            freezeTurnText.DOKill();
            freezeTurnText.color = FREEZE_ORANGE;
            freezeTurnColorAnim = DOTween.Sequence();
            freezeTurnColorAnim.Append(freezeTurnText.DOColor(FREEZE_BLACK, 1.2f).SetEase(Ease.InOutSine));
            freezeTurnColorAnim.Append(freezeTurnText.DOColor(FREEZE_ORANGE, 1.2f).SetEase(Ease.InOutSine));
            freezeTurnColorAnim.SetLoops(-1, LoopType.Restart);
        }

        // Total Damage 색상 루프 + bob
        if (freezeTotalDamageText != null)
        {
            freezeTotalDamageText.DOKill();
            freezeTotalDamageText.color = FREEZE_ORANGE;
            freezeTotalDmgColorAnim = DOTween.Sequence();
            freezeTotalDmgColorAnim.Append(freezeTotalDamageText.DOColor(FREEZE_BLACK, 1.2f).SetEase(Ease.InOutSine));
            freezeTotalDmgColorAnim.Append(freezeTotalDamageText.DOColor(FREEZE_ORANGE, 1.2f).SetEase(Ease.InOutSine));
            freezeTotalDmgColorAnim.SetLoops(-1, LoopType.Restart);

            // bob (좌우 미세 이동) - ATK+와 동일
            RectTransform dmgRT = freezeTotalDamageText.GetComponent<RectTransform>();
            float origX = freezeTotalDmgPosSaved ? freezeTotalDmgOriginalPos.x : dmgRT.anchoredPosition.x;
            freezeTotalDmgBobAnim = DOTween.Sequence();
            freezeTotalDmgBobAnim.Append(dmgRT.DOAnchorPosX(origX + 3f, 0.3f).SetEase(Ease.InOutSine));
            freezeTotalDmgBobAnim.Append(dmgRT.DOAnchorPosX(origX - 3f, 0.3f).SetEase(Ease.InOutSine));
            freezeTotalDmgBobAnim.SetLoops(-1, LoopType.Yoyo);
        }
    }

    void StopFreezeColorLoops()
    {
        if (atkFreezeColorAnim != null) { atkFreezeColorAnim.Kill(); atkFreezeColorAnim = null; }
        if (freezeTurnColorAnim != null) { freezeTurnColorAnim.Kill(); freezeTurnColorAnim = null; }
        if (freezeTotalDmgColorAnim != null) { freezeTotalDmgColorAnim.Kill(); freezeTotalDmgColorAnim = null; }
        if (freezeTotalDmgBobAnim != null) { freezeTotalDmgBobAnim.Kill(); freezeTotalDmgBobAnim = null; }

        // ATK 색상 복원
        if (attackPowerText != null)
        {
            attackPowerText.DOKill();
            if (atkColorSaved)
            {
                Color c = atkOriginalColor;
                c.a = 0.35f;
                attackPowerText.color = c;
            }
        }
        // Total Damage bob 위치 복원
        if (freezeTotalDamageText != null)
        {
            RectTransform rt = freezeTotalDamageText.GetComponent<RectTransform>();
            rt.DOKill();
            if (freezeTotalDmgPosSaved) rt.anchoredPosition = freezeTotalDmgOriginalPos;
        }
    }

    void SetProgressBarFreezeColor()
    {
        if (progressBarFill == null) return;
        Image fillImg = progressBarFill.GetComponent<Image>();
        if (fillImg != null) fillImg.color = new Color(0.9f, 0.2f, 0.2f);
    }

    void RestoreProgressBarColor()
    {
        if (progressBarFill == null || !progressBarColorSaved) return;
        Image fillImg = progressBarFill.GetComponent<Image>();
        if (fillImg != null) fillImg.color = progressBarOriginalColor;
    }

    // === Continue ===
    public bool CanContinue() { return continueCount < MAX_CONTINUES; }

    public void UseContinue()
    {
        continueCount++;
        UpdateContinueGuideText();
    }

    void UpdateContinueGuideText()
    {
        if (continueGuideText != null)
            continueGuideText.text = $"{MAX_CONTINUES - continueCount}/{MAX_CONTINUES}";
    }

    public void ContinueIntoFever()
    {
        isFeverMode = true; mergeGauge = GAUGE_MAX; feverBulletUsed = false; hasBullet = false;
        freezeTurnCount = 0; freezeTotalDamage = 0;

        SpawnFeverParticle();
        if (feverBackgroundImage != null)
        {
            feverBackgroundImage.gameObject.SetActive(true);
            Color c = feverBackgroundImage.color; c.a = 1.0f; feverBackgroundImage.color = c;
            feverBackgroundImage.DOKill();
            feverBackgroundImage.DOFade(0.7f, 0.5f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
        }
        if (freezeImage1 != null) freezeImage1.gameObject.SetActive(true);
        if (bossManager != null) { bossManager.SetFrozen(true); bossManager.ResetBonusTurns(); }
        SetProgressBarFreezeColor();
        StartAtkBobAnimation();
        StartFreezeColorLoops();
        FireFeverFreezeLaser();

        if (freezeTurnText != null) { freezeTurnText.gameObject.SetActive(true); freezeTurnText.text = "Freeze Turn 0"; }
        if (freezeTotalDamageText != null) { freezeTotalDamageText.gameObject.SetActive(true); freezeTotalDamageText.text = "0"; }
        UpdateGunUI();
    }

    // === Freeze 레이저 ===
    void FireFeverFreezeLaser()
    {
        ProjectileManager pm = bossBattle.GetProjectileManager();
        if (pm == null || gunButton == null || bossManager == null || bossManager.bossImageArea == null) return;
        RectTransform monsterRect = bossManager.bossImageArea.GetComponent<RectTransform>();
        pm.FireFreezeLaser(gunButton.transform.position, monsterRect.position, new Color(0.5f, 0.85f, 1f, 0.9f), null);
    }

    // === Fever 파티클 ===
    void SpawnFeverParticle()
    {
        if (feverParticleSpawnPoint == null) return;
        if (activeFeverParticle != null) Destroy(activeFeverParticle);

        float canvasCorr = GetCanvasScaleCorrection();

        GameObject particleObj = new GameObject("FeverFlameParticle");
        particleObj.transform.SetParent(feverParticleSpawnPoint, false);
        particleObj.transform.localPosition = Vector3.zero;

        ParticleSystem ps = particleObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 0.5f; main.startSpeed = 50f / canvasCorr; main.startSize = 30f / canvasCorr;
        main.startColor = new Color(1f, 0.5f, 0f); main.maxParticles = 50;
        main.simulationSpace = ParticleSystemSimulationSpace.Local; main.playOnAwake = true; main.loop = true;

        var emission = ps.emission; emission.enabled = true; emission.rateOverTime = 20;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 15f; shape.radius = 10f / canvasCorr;

        var col = ps.colorOverLifetime; col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(1f, 1f, 0f), 0f), new GradientColorKey(new Color(1f, 0.5f, 0f), 0.5f), new GradientColorKey(new Color(1f, 0f, 0f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = new ParticleSystem.MinMaxGradient(g);

        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.y = new ParticleSystem.MinMaxCurve(100f / canvasCorr);
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = new Material(Shader.Find("UI/Default")); renderer.sortingOrder = 1;
        var uiP = particleObj.AddComponent<Coffee.UIExtensions.UIParticle>(); uiP.scale = 3f;

        activeFeverParticle = particleObj;
    }

    // === Freeze Sync ===
    public IEnumerator SyncFreezeWithBossRespawn()
    {
        if (freezeImage1 != null) { freezeImage1.DOKill(); freezeImage1.gameObject.SetActive(false); }
        CleanupFreezeLasers();

        while (bossBattle.IsBossTransitioning)
            yield return null;

        yield return new WaitForSeconds(5.4f);

        if (!isFeverMode) yield break;

        FireFeverFreezeLaser();
        if (freezeImage1 != null)
        {
            freezeImage1.gameObject.SetActive(true);
            freezeImage1.color = new Color(1f, 1f, 1f, 0f);
            freezeImage1.DOFade(70f / 255f, 0.5f).SetEase(Ease.OutQuad);
        }
    }

    void CleanupFreezeLasers()
    {
        var projectiles = GameObject.FindObjectsByType<Projectile>(FindObjectsSortMode.None);
        foreach (var p in projectiles)
        {
            if (p != null && p.gameObject.name.Contains("Freeze"))
                Destroy(p.gameObject);
        }
    }

    float GetCanvasScaleCorrection()
    {
        Canvas canvas = gunButton.GetComponentInParent<Canvas>();
        if (canvas == null) return 1f;
        Canvas root = canvas.rootCanvas;
        if (root == null) return 1f;
        RectTransform canvasRect = root.GetComponent<RectTransform>();
        if (canvasRect == null) return 1f;
        return canvasRect.rect.width / 1290f;
    }

    // === ATK Floating Text (우측 끝에서 생성) ===
    void ShowATKChangeText(long increase)
    {
        if (damageTextPrefab == null || damageTextParent == null || attackPowerText == null) return;
        GameObject obj = Instantiate(damageTextPrefab, damageTextParent);
        TextMeshProUGUI txt = obj.GetComponent<TextMeshProUGUI>();
        if (txt != null)
        {
            txt.text = $"+{increase}"; txt.color = new Color(1f, 0.7f, 0.2f); txt.fontSize = 32;
            RectTransform r = obj.GetComponent<RectTransform>();
            RectTransform atkRect = attackPowerText.GetComponent<RectTransform>();

            // ATK 텍스트의 우측 끝 월드 좌표 계산
            Vector3[] corners = new Vector3[4];
            atkRect.GetWorldCorners(corners); // [0]=BL, [1]=TL, [2]=TR, [3]=BR
            Vector3 rightEdgeWorld = (corners[2] + corners[3]) * 0.5f; // 우측 중앙
            r.position = rightEdgeWorld;

            CanvasGroup cg = obj.GetComponent<CanvasGroup>(); if (cg == null) cg = obj.AddComponent<CanvasGroup>();
            DOTween.Sequence()
                .Append(r.DOAnchorPosY(r.anchoredPosition.y + 60f, 0.7f).SetEase(Ease.OutCubic))
                .Join(cg.DOFade(0f, 0.7f).SetEase(Ease.InCubic))
                .Insert(0f, r.DOScale(1.3f, 0.1f).SetEase(Ease.OutQuad))
                .Insert(0.1f, r.DOScale(1f, 0.15f).SetEase(Ease.InQuad))
                .OnComplete(() => { if (obj != null) Destroy(obj); });
        }
    }

    // === Gun 모드 ===
    public void ToggleGunMode()
    {
        if (bossBattle.IsBossAttacking) return;
        if (isGunMode) { ExitGunMode(); return; }
        if (!hasBullet && (!isFeverMode || feverBulletUsed)) return;
        if (gridManager.ActiveTiles.Count <= 2) return;

        isGunMode = true;
        if (gunModeGuideText != null) { gunModeGuideText.gameObject.SetActive(true); gunModeGuideText.text = "Cancel"; }
        if (gunModeOverlayImage != null) gunModeOverlayImage.gameObject.SetActive(true);
        gridManager.UpdateTileBorders();
        gridManager.DimProtectedTiles(true);
        StartHPBarGunModeAnim();
        StartProgressBarGlow();
        UpdateGunUI();
    }

    void ExitGunMode()
    {
        isGunMode = false;
        if (gunModeOverlayImage != null) gunModeOverlayImage.gameObject.SetActive(false);
        gridManager.ClearAllTileBorders();
        gridManager.DimProtectedTiles(false);
        StopHPBarGunModeAnim();
        StopProgressBarGlow();
        UpdateGuideText();
        UpdateGunUI();
    }

    // === 총 발사 ===
    public void ShootTile()
    {
        if (!hasBullet && (!isFeverMode || feverBulletUsed)) { ExitGunMode(); return; }

        var topTwo = gridManager.GetTopTwoTileValues();
        if (gridManager.ActiveTiles.Count <= 2) { ExitGunMode(); return; }

        Canvas canvas = gridManager.GridContainer.GetComponentInParent<Canvas>();
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main;
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(gridManager.GridContainer, Input.mousePosition, cam, out localPoint);

        Tile targetTile = null;
        float minDist = gridManager.CellSize / 2;
        foreach (var tile in gridManager.ActiveTiles)
        {
            if (tile == null) continue;
            float d = Vector2.Distance(localPoint, tile.GetComponent<RectTransform>().anchoredPosition);
            if (d < minDist) { minDist = d; targetTile = tile; }
        }

        if (targetTile == null) return;
        var curTop = gridManager.GetTopTwoTileValues();
        if (targetTile.value == curTop.Item1 || targetTile.value == curTop.Item2) return;

        int oldHP = playerHP.CurrentHeat;
        playerHP.SetHeatToMax();
        playerHP.UpdateHeatUI(false);
        int recovery = playerHP.CurrentHeat - oldHP;
        if (recovery > 0) playerHP.ShowHeatChangeText(recovery);

        Vector2Int pos = targetTile.gridPosition;
        targetTile.PlayGunDestroyEffect();
        gridManager.Tiles[pos.x, pos.y] = null;
        gridManager.ActiveTiles.Remove(targetTile);
        Destroy(targetTile.gameObject);

        if (isFeverMode)
        {
            feverBulletUsed = true;
            hasBullet = false;
            mergeGauge -= GUN_SHOT_COST;
            if (mergeGauge < 0) mergeGauge = 0;
            EndFever();
        }
        else
        {
            mergeGauge = Mathf.Max(0, mergeGauge - GUN_SHOT_COST);
            hasBullet = (mergeGauge >= GAUGE_FOR_BULLET);
        }

        StopEmergencyFlash();
        ExitGunMode();
        if (!gridManager.CanMove() && !hasBullet && !isFeverMode) bossBattle.GameOver();
    }

    // === Gauge Change Text ===
    void ShowGaugeChangeText(int change, bool isCombo = false)
    {
        if (damageTextPrefab == null || damageTextParent == null || turnsUntilBulletText == null) return;
        GameObject obj = Instantiate(damageTextPrefab, damageTextParent);
        TextMeshProUGUI txt = obj.GetComponent<TextMeshProUGUI>();
        if (txt != null)
        {
            // #1: Combo!는 netChange가 양수/음수 상관없이 combo이면 표시
            if (isCombo)
                txt.text = change > 0 ? $"Combo! +{change}" : $"Combo! {change}";
            else
                txt.text = change > 0 ? $"+{change}" : change.ToString();

            txt.color = change > 0 ? new Color(0.9f, 0.2f, 0.2f) : new Color(0.6f, 0.6f, 0.6f);
            txt.fontSize = 36;
            RectTransform r = obj.GetComponent<RectTransform>();
            r.position = turnsUntilBulletText.GetComponent<RectTransform>().position;
            CanvasGroup cg = obj.GetComponent<CanvasGroup>(); if (cg == null) cg = obj.AddComponent<CanvasGroup>();
            DOTween.Sequence()
                .Append(r.DOAnchorPosY(r.anchoredPosition.y + 80f, 0.8f).SetEase(Ease.OutCubic))
                .Join(cg.DOFade(0f, 0.8f).SetEase(Ease.InCubic))
                .Insert(0f, r.DOScale(1.2f, 0.1f).SetEase(Ease.OutQuad))
                .Insert(0.1f, r.DOScale(1f, 0.1f).SetEase(Ease.InQuad))
                .OnComplete(() => { if (obj != null) Destroy(obj); });
        }
    }

    // === Gun UI ===
    public void UpdateGunUI()
    {
        if (bulletCountText != null)
        {
            if (isFeverMode) bulletCountText.text = "FREEZE!";
            else if (hasBullet) bulletCountText.text = "CHARGE";
            else bulletCountText.text = "RELOAD";
        }

        UpdateGuideText();

        if (turnsUntilBulletText != null)
        {
            if (!turnsTextInitialized)
            {
                turnsTextOriginalY = turnsUntilBulletText.GetComponent<RectTransform>().anchoredPosition.y;
                turnsTextInitialized = true;
            }

            turnsUntilBulletText.text = $"{mergeGauge}/{GAUGE_MAX}";

            if (mergeGauge != lastMergeGauge)
            {
                lastMergeGauge = mergeGauge;
                RectTransform tr = turnsUntilBulletText.GetComponent<RectTransform>();
                tr.DOKill();
                DOTween.Sequence()
                    .Append(tr.DOAnchorPosY(turnsTextOriginalY + 8f, 0.12f).SetEase(Ease.OutQuad))
                    .Append(tr.DOAnchorPosY(turnsTextOriginalY, 0.12f).SetEase(Ease.InQuad))
                    .OnComplete(() => { if (tr != null) tr.anchoredPosition = new Vector2(tr.anchoredPosition.x, turnsTextOriginalY); });
            }
        }

        // ATK+ 텍스트 (색상은 색상 루프가 관리, 여기서는 텍스트만)
        if (attackPowerText != null)
        {
            if (!attackTextInitialized)
            {
                attackTextOriginalY = attackPowerText.GetComponent<RectTransform>().anchoredPosition.y;
                attackTextInitialized = true;
            }

            bool bulletReady = hasBullet || (isFeverMode && !feverBulletUsed);
            string bulletIcon = bulletReady ? "\u25A0" : "\u25A1";
            attackPowerText.text = $"{bulletIcon} ATK+{permanentAttackPower:N0}";

            // 색상: Freeze 중에는 색상 루프가 관리, 아닐 때만 직접 설정
            if (!isFeverMode)
            {
                Color c = atkColorSaved ? atkOriginalColor : Color.black;
                c.a = bulletReady ? 0.7f : 0.35f;
                // 색상 루프 돌고있지 않을 때만 직접 설정
                if (atkFreezeColorAnim == null)
                    attackPowerText.color = c;
            }

            if (permanentAttackPower != lastPermanentAttackPower)
            {
                long increase = permanentAttackPower - lastPermanentAttackPower;
                lastPermanentAttackPower = permanentAttackPower;
                RectTransform tr = attackPowerText.GetComponent<RectTransform>();
                tr.DOKill();
                DOTween.Sequence()
                    .Append(tr.DOAnchorPosY(attackTextOriginalY + 10f, 0.15f).SetEase(Ease.OutQuad))
                    .Append(tr.DOAnchorPosY(attackTextOriginalY, 0.15f).SetEase(Ease.InQuad))
                    .OnComplete(() => { if (tr != null) tr.anchoredPosition = new Vector2(tr.anchoredPosition.x, attackTextOriginalY); });
                ShowATKChangeText(increase);
            }
        }

        if (progressBarFill != null)
        {
            float progress = Mathf.Clamp01((float)mergeGauge / GAUGE_MAX);
            float targetW = progressBarFill.parent.GetComponent<RectTransform>().rect.width * progress;
            progressBarFill.DOKill();
            progressBarFill.DOSizeDelta(new Vector2(targetW, progressBarFill.sizeDelta.y), 0.3f).SetEase(Ease.OutQuad);
        }

        if (gunButtonImage != null && !isEmergencyFlashing)
        {
            if (isGunMode) gunButtonImage.color = Color.red;
            else if (isFeverMode) gunButtonImage.color = new Color(1f, 0.3f, 0f);
            else if (hasBullet) gunButtonImage.color = new Color(0.6f, 0.95f, 0.85f);
            else gunButtonImage.color = new Color(0.5f, 0.5f, 0.5f);
        }

        if (gunButton != null)
        {
            gunButton.interactable = !bossBattle.IsGameOver && !bossBattle.IsBossTransitioning
                && (hasBullet || (isFeverMode && !feverBulletUsed))
                && gridManager.ActiveTiles.Count > 1;
        }

        UpdateGunButtonAnimationIfNeeded(hasBullet || (isFeverMode && !feverBulletUsed));
    }

    public void UpdateGuideText()
    {
        if (gunModeGuideText == null) return;
        if (isGunMode) { gunModeGuideText.gameObject.SetActive(true); gunModeGuideText.text = "Cancel"; return; }
        gunModeGuideText.gameObject.SetActive(true);
        if (isFeverMode) gunModeGuideText.text = "Gun\nReady";
        else if (hasBullet) gunModeGuideText.text = "Gun\nReady";
        else gunModeGuideText.text = "";
    }

    // === ATK+ 시소 ===
    void StartAtkBobAnimation()
    {
        StopAtkBobAnimation();
        if (attackPowerText == null) return;
        RectTransform tr = attackPowerText.GetComponent<RectTransform>();

        if (!atkOriginalXSaved)
        {
            atkOriginalX = tr.anchoredPosition.x;
            atkOriginalXSaved = true;
        }

        atkBobAnim = DOTween.Sequence();
        atkBobAnim.Append(tr.DOAnchorPosX(atkOriginalX + 3f, 0.3f).SetEase(Ease.InOutSine));
        atkBobAnim.Append(tr.DOAnchorPosX(atkOriginalX - 3f, 0.3f).SetEase(Ease.InOutSine));
        atkBobAnim.SetLoops(-1, LoopType.Yoyo);
    }

    void StopAtkBobAnimation()
    {
        if (atkBobAnim != null) { atkBobAnim.Kill(); atkBobAnim = null; }
        if (attackPowerText != null && atkOriginalXSaved)
        {
            RectTransform tr = attackPowerText.GetComponent<RectTransform>();
            tr.DOKill();
            tr.anchoredPosition = new Vector2(atkOriginalX, tr.anchoredPosition.y);
        }
    }

    // === Gun Button 애니메이션 ===
    void UpdateGunButtonAnimationIfNeeded(bool shouldAnimate)
    {
        bool currentState = isGunMode || shouldAnimate;
        if (currentState == lastGunButtonAnimationState && gunButtonHeartbeat != null) return;
        lastGunButtonAnimationState = currentState;

        if (gunButton == null || gunButtonImage == null) return;
        if (gunButtonHeartbeat != null) { gunButtonHeartbeat.Kill(); gunButtonHeartbeat = null; }

        Color c = gunButtonImage.color; c.a = 1f; gunButtonImage.color = c;
        gunButton.transform.localScale = Vector3.one;

        if (isGunMode)
            gunButtonHeartbeat = gunButton.transform.DOScale(1.15f, 0.3f).SetEase(Ease.InOutQuad).SetLoops(-1, LoopType.Yoyo);
        else if (shouldAnimate)
            gunButtonHeartbeat = gunButton.transform.DOScale(1.1f, 0.6f).SetEase(Ease.InOutQuad).SetLoops(-1, LoopType.Yoyo);
    }

    void UpdateGunButtonAnimation()
    {
        lastGunButtonAnimationState = false;
        UpdateGunButtonAnimationIfNeeded(hasBullet || (isFeverMode && !feverBulletUsed));
    }

    // === 긴급 깜빡임 ===
    public void SetEmergencyFlash(bool shouldFlash)
    {
        if (shouldFlash && gunButtonImage != null)
        {
            if (!isEmergencyFlashing) { isEmergencyFlashing = true; StartEmergencyFlashLoop(); }
        }
        else { StopEmergencyFlash(); }
    }

    void StartEmergencyFlashLoop()
    {
        if (gunButtonImage == null) return;
        if (emergencyGunFlash != null) { emergencyGunFlash.Kill(); emergencyGunFlash = null; }
        Color colorA = new Color(0.6f, 0.95f, 0.85f);
        Color colorB = new Color(1f, 0.25f, 0.25f);
        gunButtonImage.color = colorA;
        emergencyGunFlash = DOTween.Sequence();
        emergencyGunFlash.Append(gunButtonImage.DOColor(colorB, 0.35f).SetEase(Ease.InOutSine));
        emergencyGunFlash.Append(gunButtonImage.DOColor(colorA, 0.35f).SetEase(Ease.InOutSine));
        emergencyGunFlash.SetLoops(-1, LoopType.Restart);
    }

    void StopEmergencyFlash()
    {
        if (emergencyGunFlash != null) { emergencyGunFlash.Kill(); emergencyGunFlash = null; }
        isEmergencyFlashing = false;
        if (gunButtonImage != null) { Color c = gunButtonImage.color; c.a = 1f; gunButtonImage.color = c; }
    }

    // === Progress bar glow ===
    void StartProgressBarGlow()
    {
        StopProgressBarGlow();
        if (progressBarGlowOverlay == null) return;

        progressBarGlowOverlay.gameObject.SetActive(true);
        Color c = progressBarGlowOverlay.color; c.a = 0f; progressBarGlowOverlay.color = c;

        progressBarGlowAnim = DOTween.Sequence();
        progressBarGlowAnim.Append(progressBarGlowOverlay.DOFade(0.5f, 0.5f).SetEase(Ease.InOutSine));
        progressBarGlowAnim.Append(progressBarGlowOverlay.DOFade(0f, 0.5f).SetEase(Ease.InOutSine));
        progressBarGlowAnim.SetLoops(-1, LoopType.Restart);
    }

    void StopProgressBarGlow()
    {
        if (progressBarGlowAnim != null) { progressBarGlowAnim.Kill(); progressBarGlowAnim = null; }
        if (progressBarGlowOverlay != null)
        {
            progressBarGlowOverlay.DOKill();
            Color c = progressBarGlowOverlay.color; c.a = 0f; progressBarGlowOverlay.color = c;
            progressBarGlowOverlay.gameObject.SetActive(false);
        }
    }

    // === HP bar 배경 ===
    void StartHPBarGunModeAnim()
    {
        StopHPBarGunModeAnim();
        if (hpBarBackgroundImage == null) return;
        hpBarOriginalBgColor = hpBarBackgroundImage.color;
        Color greenColor = new Color(0.3f, 0.8f, 0.4f);

        hpBarGunModeAnim = DOTween.Sequence();
        hpBarGunModeAnim.Append(hpBarBackgroundImage.DOColor(greenColor, 0.5f).SetEase(Ease.InOutSine));
        hpBarGunModeAnim.Append(hpBarBackgroundImage.DOColor(hpBarOriginalBgColor, 0.5f).SetEase(Ease.InOutSine));
        hpBarGunModeAnim.SetLoops(-1, LoopType.Restart);
    }

    void StopHPBarGunModeAnim()
    {
        if (hpBarGunModeAnim != null) { hpBarGunModeAnim.Kill(); hpBarGunModeAnim = null; }
        if (hpBarBackgroundImage != null && hpBarBgColorSaved)
            hpBarBackgroundImage.color = hpBarOriginalBgColor;
    }

    // === Cleanup ===
    public void CleanupFeverEffects()
    {
        if (activeFeverParticle != null) { Destroy(activeFeverParticle); activeFeverParticle = null; }
        if (feverBackgroundImage != null) { feverBackgroundImage.DOKill(); feverBackgroundImage.gameObject.SetActive(false); }
        if (freezeImage1 != null) freezeImage1.gameObject.SetActive(false);
        if (bossManager != null) bossManager.SetFrozen(false);
        RestoreProgressBarColor();
        StopHPBarGunModeAnim();
        StopProgressBarGlow();
        StopAtkBobAnimation();
        StopFreezeColorLoops();
        StopEmergencyFlash();
    }
}
