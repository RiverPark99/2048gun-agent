// =====================================================
// GunSystem.cs - v6.5
// Gun 모드, Freeze(게이지 0~40)
// Freeze: 40/40 진입, 20 이하 종료, 턴당 1.14배율
// Freeze Gun: 즉시 -20 → Freeze 종료
// 페이백/쿨다운/턴증가 삭제
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

    [Header("Freeze UI")]
    [SerializeField] private TextMeshProUGUI freezeTurnText;
    [SerializeField] private TextMeshProUGUI freezeTotalDamageText;

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

    // Gauge & Fever 상태
    private int mergeGauge = 0;
    private bool hasBullet = false;
    private bool isFeverMode = false;
    private bool feverBulletUsed = false;

    // ⭐ v6.5: Freeze 턴 배율
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

    // ⭐ v6.5: Gun 모드 시 progress bar 빛남
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

    // ⭐ v6.5: 긴급 깜빡임
    private Sequence emergencyGunFlash;
    private bool isEmergencyFlashing = false;

    // ⭐ v6.5: ATK+ 시소 애니메이션
    private Sequence atkBobAnim;

    // Freeze UI 원래 위치 저장
    private Vector2 freezeTurnOriginalPos;
    private bool freezeTurnPosSaved = false;
    private Vector2 freezeTotalDmgOriginalPos;
    private bool freezeTotalDmgPosSaved = false;

    // === 프로퍼티 ===
    public bool IsFeverMode => isFeverMode;
    public bool IsGunMode => isGunMode;
    public bool HasBullet => hasBullet;
    public bool FeverBulletUsed => feverBulletUsed;
    public int MergeGauge => mergeGauge;
    public long PermanentAttackPower => permanentAttackPower;
    public long FeverMergeIncreaseAtk => feverMergeIncreaseAtk;
    // ⭐ v6.5: Freeze 턴 배율 (GridManager에서 사용)
    public float GetFreezeDamageMultiplier() { return Mathf.Pow(FREEZE_TURN_MULTIPLIER, freezeTurnCount); }
    public void AddFreezeTotalDamage(long dmg)
    {
        freezeTotalDamage += dmg;
        if (freezeTotalDamageText != null && freezeTotalDamageText.gameObject.activeSelf)
        {
            freezeTotalDamageText.text = $"Total: {freezeTotalDamage:N0}";
            // 위로 살짝 올랐다 내려오는 효과
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

        if (freezeTurnText != null)
        {
            if (!freezeTurnPosSaved) { freezeTurnOriginalPos = freezeTurnText.GetComponent<RectTransform>().anchoredPosition; freezeTurnPosSaved = true; }
            freezeTurnText.gameObject.SetActive(false);
        }
        if (freezeTotalDamageText != null)
        {
            if (!freezeTotalDmgPosSaved) { freezeTotalDmgOriginalPos = freezeTotalDamageText.GetComponent<RectTransform>().anchoredPosition; freezeTotalDmgPosSaved = true; }
            freezeTotalDamageText.gameObject.SetActive(false);
        }

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
        if (gunModeGuideText != null) gunModeGuideText.gameObject.SetActive(false);
        if (freezeImage1 != null) freezeImage1.gameObject.SetActive(false);
        if (activeFeverParticle != null) { Destroy(activeFeverParticle); activeFeverParticle = null; }
        if (feverBackgroundImage != null) { feverBackgroundImage.DOKill(); feverBackgroundImage.gameObject.SetActive(false); }
        if (gunModeOverlayImage != null) gunModeOverlayImage.gameObject.SetActive(false);
        ForceResetFreezeUITransforms();
        if (freezeTurnText != null) freezeTurnText.gameObject.SetActive(false);
        if (freezeTotalDamageText != null) freezeTotalDamageText.gameObject.SetActive(false);

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

    // ⭐ v6.5: 페이백 삭제 → 빈 메서드 (호출처 호환)
    public void ClearFeverPaybackIfNeeded() { }

    // === Freeze 턴 처리 ===
    public void ProcessFreezeAfterMove(int comboCount)
    {
        if (!isFeverMode) return;

        freezeTurnCount++;
        int netChange = 0;

        if (comboCount >= 2)
        {
            int bonus = FREEZE_COMBO_BONUS * comboCount;
            int before = mergeGauge;
            mergeGauge += bonus;
            if (mergeGauge > GAUGE_MAX) mergeGauge = GAUGE_MAX;
            netChange += (mergeGauge - before);
        }

        mergeGauge -= FREEZE_MOVE_COST;
        netChange -= FREEZE_MOVE_COST;

        if (netChange != 0) ShowGaugeChangeText(netChange);

        // Freeze 턴 UI 갱신
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

    // ⭐ v6.5: 보스 전환 완료 후 Freeze 진입 (버튼 투명도 복원 후)
    public IEnumerator DelayedFreezeCheck()
    {
        // Gun button alpha가 1로 완전히 복원될 때까지 대기
        if (gunButtonImage != null)
        {
            while (gunButtonImage.color.a < 0.99f)
                yield return null;
        }
        yield return null; // 1프레임 추가 대기
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

        // Freeze 턴/데미지 UI 활성화
        if (freezeTurnText != null) { freezeTurnText.gameObject.SetActive(true); freezeTurnText.text = "Turn 0"; }
        if (freezeTotalDamageText != null) { freezeTotalDamageText.gameObject.SetActive(true); freezeTotalDamageText.text = "Total: 0"; }

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

        // ⭐ v6.5: Freeze UI 강제 리셋 후 종료 애니메이션
        ForceResetFreezeUITransforms();
        AnimateAndHideFreezeUI();

        // ⭐ v6.5: 자연종료 → 현재 게이지 유지, bullet 체크
        hasBullet = (mergeGauge >= GAUGE_FOR_BULLET);
        freezeTurnCount = 0;

        UpdateGunUI();
    }

    // ⭐ v6.5: Freeze UI transform 강제 리셋 (DOTween이 중간에 멈춘 경우 대비)
    void ForceResetFreezeUITransforms()
    {
        if (freezeTurnText != null)
        {
            RectTransform rt = freezeTurnText.GetComponent<RectTransform>();
            rt.DOKill(); rt.localScale = Vector3.one;
            if (freezeTurnPosSaved) rt.anchoredPosition = freezeTurnOriginalPos;
        }
        if (freezeTotalDamageText != null)
        {
            RectTransform rt = freezeTotalDamageText.GetComponent<RectTransform>();
            rt.DOKill(); rt.localScale = Vector3.one;
            if (freezeTotalDmgPosSaved) rt.anchoredPosition = freezeTotalDmgOriginalPos;
        }
    }

    // ⭐ v6.5: Freeze UI 종료 애니메이션 (턴 + 총데미지)
    void AnimateAndHideFreezeUI()
    {
        // Freeze Turn: 페이드아웃
        if (freezeTurnText != null && freezeTurnText.gameObject.activeSelf)
        {
            RectTransform rt = freezeTurnText.GetComponent<RectTransform>();
            CanvasGroup cg = freezeTurnText.GetComponent<CanvasGroup>();
            if (cg == null) cg = freezeTurnText.gameObject.AddComponent<CanvasGroup>();
            rt.DOKill(); cg.DOKill();
            cg.alpha = 1f; rt.localScale = Vector3.one;

            DOTween.Sequence()
                .AppendInterval(1.0f)
                .Append(cg.DOFade(0f, 0.5f).SetEase(Ease.InQuad))
                .OnComplete(() => {
                    if (freezeTurnText == null) return;
                    freezeTurnText.gameObject.SetActive(false);
                    cg.alpha = 1f; rt.localScale = Vector3.one;
                    if (freezeTurnPosSaved) rt.anchoredPosition = freezeTurnOriginalPos;
                });
        }

        // Freeze Total Damage: 픽스 표시 후 페이드아웃
        if (freezeTotalDamageText != null && freezeTotalDamageText.gameObject.activeSelf)
        {
            freezeTotalDamageText.text = $"Freeze DMG: {freezeTotalDamage:N0}";
            RectTransform rt = freezeTotalDamageText.GetComponent<RectTransform>();
            CanvasGroup cg = freezeTotalDamageText.GetComponent<CanvasGroup>();
            if (cg == null) cg = freezeTotalDamageText.gameObject.AddComponent<CanvasGroup>();
            rt.DOKill(); cg.DOKill();
            cg.alpha = 1f; rt.localScale = Vector3.one;
            if (freezeTotalDmgPosSaved) rt.anchoredPosition = freezeTotalDmgOriginalPos;

            DOTween.Sequence()
                .AppendInterval(2.0f)
                .Append(cg.DOFade(0f, 0.6f).SetEase(Ease.InQuad))
                .OnComplete(() => {
                    if (freezeTotalDamageText == null) return;
                    freezeTotalDamageText.gameObject.SetActive(false);
                    cg.alpha = 1f; rt.localScale = Vector3.one;
                    if (freezeTotalDmgPosSaved) rt.anchoredPosition = freezeTotalDmgOriginalPos;
                });
        }
    }

    // ⭐ v6.5: Freeze 턴 UI 갱신
    void UpdateFreezeTurnUI()
    {
        if (freezeTurnText != null && isFeverMode)
        {
            float mult = GetFreezeDamageMultiplier();
            freezeTurnText.text = $"Turn {freezeTurnCount} (x{mult:F2})";
            // 턴 변경 시 스케일 펌스 (1→1.06→1)
            RectTransform rt = freezeTurnText.GetComponent<RectTransform>();
            rt.DOKill();
            rt.localScale = Vector3.one;
            rt.DOScale(1.06f, 0.08f).SetEase(Ease.OutQuad)
                .OnComplete(() => { if (rt != null) rt.DOScale(1f, 0.1f).SetEase(Ease.InQuad); });
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

    // === Continue → Fever ===
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
        FireFeverFreezeLaser();

        if (freezeTurnText != null) { freezeTurnText.gameObject.SetActive(true); freezeTurnText.text = "Turn 0"; }
        if (freezeTotalDamageText != null) { freezeTotalDamageText.gameObject.SetActive(true); freezeTotalDamageText.text = "Total: 0"; }
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

    // === ATK Floating Text ===
    void ShowATKChangeText(long increase)
    {
        if (damageTextPrefab == null || damageTextParent == null || attackPowerText == null) return;
        GameObject obj = Instantiate(damageTextPrefab, damageTextParent);
        TextMeshProUGUI txt = obj.GetComponent<TextMeshProUGUI>();
        if (txt != null)
        {
            txt.text = $"+{increase}"; txt.color = new Color(1f, 0.7f, 0.2f); txt.fontSize = 32;
            RectTransform r = obj.GetComponent<RectTransform>();
            r.position = attackPowerText.GetComponent<RectTransform>().position;
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

        // HP 회복
        int oldHP = playerHP.CurrentHeat;
        playerHP.SetHeatToMax();
        playerHP.UpdateHeatUI(false);
        int recovery = playerHP.CurrentHeat - oldHP;
        if (recovery > 0) playerHP.ShowHeatChangeText(recovery);

        // 타일 파괴
        Vector2Int pos = targetTile.gridPosition;
        targetTile.PlayGunDestroyEffect();
        gridManager.Tiles[pos.x, pos.y] = null;
        gridManager.ActiveTiles.Remove(targetTile);
        Destroy(targetTile.gameObject);

        if (isFeverMode)
        {
            // ⭐ v6.5: Freeze Gun → 즉시 -20 → Freeze 종료
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
    void ShowGaugeChangeText(int change)
    {
        if (damageTextPrefab == null || damageTextParent == null || turnsUntilBulletText == null) return;
        GameObject obj = Instantiate(damageTextPrefab, damageTextParent);
        TextMeshProUGUI txt = obj.GetComponent<TextMeshProUGUI>();
        if (txt != null)
        {
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

        // ⭐ v6.5: ATK+ 텍스트 (□/■ 알파 구분)
        if (attackPowerText != null)
        {
            if (!attackTextInitialized)
            {
                attackTextOriginalY = attackPowerText.GetComponent<RectTransform>().anchoredPosition.y;
                attackTextInitialized = true;
            }

            bool bulletReady = hasBullet || (isFeverMode && !feverBulletUsed);
            string bulletIcon = bulletReady ? "■" : "□";
            attackPowerText.text = $"{bulletIcon} ATK+{permanentAttackPower}";

            // 알파값: □=투명하게, ■=최대
            Color atkColor = attackPowerText.color;
            atkColor.a = bulletReady ? 1f : 0.5f;
            attackPowerText.color = atkColor;

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

        // 버튼 색상
        if (gunButtonImage != null && !isEmergencyFlashing)
        {
            if (isGunMode) gunButtonImage.color = Color.red;
            else if (isFeverMode) gunButtonImage.color = new Color(1f, 0.3f, 0f);
            else if (hasBullet) gunButtonImage.color = new Color(0.6f, 0.95f, 0.85f); // 민트
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
        if (isFeverMode) gunModeGuideText.text = "Freeze\nGun!";
        else if (hasBullet) gunModeGuideText.text = "Gun\nReady";
        else gunModeGuideText.text = "";
    }

    // === ATK+ 시소 애니메이션 (좌우 미세 이동) ===
    private float atkOriginalX = 0f;
    private bool atkOriginalXSaved = false;

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

        // 좌우 ±3px 미세 이동 (0.6초 주기)
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
            if (!isEmergencyFlashing)
            {
                isEmergencyFlashing = true;
                StartEmergencyFlashLoop();
            }
        }
        else
        {
            StopEmergencyFlash();
        }
    }

    void StartEmergencyFlashLoop()
    {
        if (gunButtonImage == null) return;
        if (emergencyGunFlash != null) { emergencyGunFlash.Kill(); emergencyGunFlash = null; }

        // ⭐ v6.5: Freeze gun도 민트 (기본 gun 장전 색상)
        Color colorA = new Color(0.6f, 0.95f, 0.85f); // 민트
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
        if (gunButtonImage != null)
        {
            Color c = gunButtonImage.color; c.a = 1f; gunButtonImage.color = c;
        }
    }

    // === Progress bar 빛남 (Gun mode) ===
    void StartProgressBarGlow()
    {
        StopProgressBarGlow();
        if (progressBarFill == null) return;
        Image fillImg = progressBarFill.GetComponent<Image>();
        if (fillImg == null) return;

        Color baseColor = fillImg.color;
        Color glowColor = new Color(Mathf.Min(1f, baseColor.r + 0.3f), Mathf.Min(1f, baseColor.g + 0.3f), Mathf.Min(1f, baseColor.b + 0.3f));

        progressBarGlowAnim = DOTween.Sequence();
        progressBarGlowAnim.Append(fillImg.DOColor(glowColor, 0.4f).SetEase(Ease.InOutSine));
        progressBarGlowAnim.Append(fillImg.DOColor(baseColor, 0.4f).SetEase(Ease.InOutSine));
        progressBarGlowAnim.SetLoops(-1, LoopType.Restart);
    }

    void StopProgressBarGlow()
    {
        if (progressBarGlowAnim != null) { progressBarGlowAnim.Kill(); progressBarGlowAnim = null; }
    }

    // === HP bar 배경 애니메이션 (Gun mode) ===
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
        StopEmergencyFlash();
    }
}
