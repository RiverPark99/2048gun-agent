// =====================================================
// GunSystem.cs - v6.3
// Gun 모드, Freeze, 게이지(0~40), ATK 보너스
// Freeze: 40/40 → 이동 -2, 콤보 +2*n, 20/40 도달시 종료
// Freeze Gun: 즉시 종료, gauge→0, ■ 채워야 재공격
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

    [Header("Freeze(Fever) Effects")]
    [SerializeField] private Transform feverParticleSpawnPoint;
    [SerializeField] private Image feverBackgroundImage;
    [SerializeField] private Image freezeImage1;

    [Header("Gun Mode Visual")]
    [SerializeField] private Image gunModeOverlayImage;

    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private PlayerHPSystem playerHP;
    [SerializeField] private BossBattleSystem bossBattle;
    [SerializeField] private BossManager bossManager;

    // 상수 (게이지 0~40)
    private const int GAUGE_MAX = 40;
    private const int GAUGE_FOR_BULLET = 20;
    private const int FREEZE_START_GAUGE = 40;
    private const int FREEZE_END_GAUGE = 20;
    private const int FREEZE_MOVE_COST = 2;
    private const int FREEZE_COMBO_BONUS = 2;
    private const int GUN_SHOT_COST = 20;

    // Gauge & Fever 상태
    private int mergeGauge = 0;
    private bool hasBullet = false;
    private bool isFeverMode = false;
    private bool feverBulletUsed = false;
    private bool justEndedFeverWithoutShot = false;

    // ATK 보너스
    private int feverAtkBonus = 0;
    private int feverMergeAtkBonus = 0;
    private long feverMergeIncreaseAtk = 1;
    private long permanentAttackPower = 0;

    // Gun 모드
    private bool isGunMode = false;

    // UI 애니메이션 상태
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

    // Fever 파티클
    private GameObject activeFeverParticle;
    // Gun 연기 파티클
    private GameObject activeGunSmoke;
    // ⭐ v6.4: 긴급 Gun 버튼 색상 깜빡임
    private Sequence emergencyGunFlash;
    private bool isEmergencyFlashing = false;

    // === 프로퍼티 ===
    public bool IsFeverMode => isFeverMode;
    public bool IsGunMode => isGunMode;
    public bool HasBullet => hasBullet;
    public bool FeverBulletUsed => feverBulletUsed;
    public int MergeGauge => mergeGauge;
    public int FeverAtkBonus => feverAtkBonus;
    public int FeverMergeAtkBonus => feverMergeAtkBonus;
    public long FeverMergeIncreaseAtk => feverMergeIncreaseAtk;
    public long PermanentAttackPower => permanentAttackPower;

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
        UpdateGunUI();
    }

    public void ResetState()
    {
        mergeGauge = 0; hasBullet = false; isFeverMode = false;
        feverAtkBonus = 0; feverMergeAtkBonus = 0; feverMergeIncreaseAtk = 1; permanentAttackPower = 0;
        feverBulletUsed = false; isGunMode = false; justEndedFeverWithoutShot = false;
        lastPermanentAttackPower = 0; lastMergeGauge = -1;

        if (gunButtonHeartbeat != null) { gunButtonHeartbeat.Kill(); gunButtonHeartbeat = null; }
        if (gunModeGuideText != null) gunModeGuideText.gameObject.SetActive(false);
        if (freezeImage1 != null) freezeImage1.gameObject.SetActive(false);
        if (activeFeverParticle != null) { Destroy(activeFeverParticle); activeFeverParticle = null; }
        if (activeGunSmoke != null) { Destroy(activeGunSmoke); activeGunSmoke = null; }
        if (feverBackgroundImage != null) { feverBackgroundImage.DOKill(); feverBackgroundImage.gameObject.SetActive(false); }
        if (gunModeOverlayImage != null) gunModeOverlayImage.gameObject.SetActive(false);

        RestoreProgressBarColor();
        UpdateGunUI();
    }

    // === 게이지 조작 ===
    public void AddMergeGauge(int amount)
    {
        int before = mergeGauge;
        mergeGauge += amount;
        if (mergeGauge > GAUGE_MAX) mergeGauge = GAUGE_MAX;
        Debug.Log($"💠 AddMergeGauge({amount}): {before} → {mergeGauge}/{GAUGE_MAX} (Fever:{isFeverMode}, Bullet:{hasBullet}, Payback:{justEndedFeverWithoutShot})");
    }

    public void UpdateGaugeUIOnly() { UpdateGunUI(); }

    public void ClearFeverPaybackIfNeeded()
    {
        if (justEndedFeverWithoutShot && mergeGauge > GAUGE_FOR_BULLET) justEndedFeverWithoutShot = false;
    }

    public void AddFeverMergeATK() { permanentAttackPower += feverMergeIncreaseAtk; }

    // === Freeze 턴 처리 ===
    public void ProcessFreezeAfterMove(int comboCount)
    {
        if (!isFeverMode) return;

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

        Debug.Log($"❄️ Freeze: gauge={mergeGauge}/{GAUGE_MAX} (net:{netChange:+#;-#;0})");

        if (mergeGauge <= FREEZE_END_GAUGE) EndFever();

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
            // ■ 채워졌으므로 연기 파티클 정리
            if (activeGunSmoke != null) { Destroy(activeGunSmoke); activeGunSmoke = null; }
            Debug.Log($"Bullet ready! ({mergeGauge}/{GAUGE_MAX})");
            UpdateGunButtonAnimation();
        }

        UpdateGunUI();
    }

    void StartFever()
    {
        // 연기 파티클 정리
        if (activeGunSmoke != null) { Destroy(activeGunSmoke); activeGunSmoke = null; }

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
        mergeGauge = FREEZE_START_GAUGE;
        hasBullet = false;
        UpdateGunButtonAnimation();
        SetProgressBarFreezeColor();

        if (!bossManager.IsClearMode()) { feverAtkBonus++; feverMergeIncreaseAtk++; }
        Debug.Log($"FREEZE MODE! Gauge: {mergeGauge}/{GAUGE_MAX}");
    }

    void EndFever()
    {
        if (activeFeverParticle != null) { Destroy(activeFeverParticle); activeFeverParticle = null; }
        if (feverBackgroundImage != null) { feverBackgroundImage.DOKill(); feverBackgroundImage.gameObject.SetActive(false); }
        if (freezeImage1 != null) freezeImage1.gameObject.SetActive(false);

        if (bossManager != null) bossManager.SetFrozen(false);
        isFeverMode = false;
        RestoreProgressBarColor();

        // ⭐ v6.3: feverBulletUsed 로컬 변수로 캐시 후 즉시 리셋 (보스 리스폰 중 재진입 방지)
        bool wasGunUsed = feverBulletUsed;
        feverBulletUsed = false;

        Debug.Log($"❄️ EndFever: wasGunUsed={wasGunUsed}");

        if (wasGunUsed)
        {
            // Gun 사용 → 환급 없음, 즉시 0/40
            mergeGauge = 0;
            hasBullet = false;
            justEndedFeverWithoutShot = false;
            UpdateGunUI();

            Debug.Log("FREEZE END! Gun used → 0/40, 환급 없음");
        }
        else
        {
            mergeGauge = GAUGE_FOR_BULLET;
            hasBullet = true;
            justEndedFeverWithoutShot = true;
            UpdateGunUI();
            Debug.Log($"FREEZE END! No shot → {GAUGE_FOR_BULLET}/{GAUGE_MAX} Gun Payback!");
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
        isFeverMode = true; mergeGauge = FREEZE_START_GAUGE; feverBulletUsed = false; hasBullet = false;

        if (activeGunSmoke != null) { Destroy(activeGunSmoke); activeGunSmoke = null; }

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
        FireFeverFreezeLaser();
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
        main.startLifetime = 0.5f;
        main.startSpeed = 50f / canvasCorr;
        main.startSize = 30f / canvasCorr;
        main.startColor = new Color(1f, 0.5f, 0f); main.maxParticles = 50;
        main.simulationSpace = ParticleSystemSimulationSpace.Local; main.playOnAwake = true; main.loop = true;

        var emission = ps.emission; emission.enabled = true; emission.rateOverTime = 20;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 15f;
        shape.radius = 10f / canvasCorr;

        var col = ps.colorOverLifetime; col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(1f, 1f, 0f), 0f), new GradientColorKey(new Color(1f, 0.5f, 0f), 0.5f), new GradientColorKey(new Color(1f, 0f, 0f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = new ParticleSystem.MinMaxGradient(g);

        var vel = ps.velocityOverLifetime; vel.enabled = true;
        vel.y = new ParticleSystem.MinMaxCurve(100f / canvasCorr);
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = new Material(Shader.Find("UI/Default")); renderer.sortingOrder = 1;
        var uiP = particleObj.AddComponent<Coffee.UIExtensions.UIParticle>();
        uiP.scale = 3f;

        activeFeverParticle = particleObj;
    }

    // === Freeze Sync (Boss 리스폰 시) ===
    public IEnumerator SyncFreezeWithBossRespawn()
    {
        // ⭐ v6.4: 보스 쓰러짐 즉시 - Freeze 이미지 + 레이저 정리
        if (freezeImage1 != null) { freezeImage1.DOKill(); freezeImage1.gameObject.SetActive(false); }
        CleanupFreezeLasers();

        // 보스 스폰 완료 대기 (transitioning=false = 보스 등장 애니메이션 + UI 완료)
        while (bossBattle.IsBossTransitioning)
            yield return null;

        yield return new WaitForSeconds(2.3f);

        if (!isFeverMode) yield break;

        // ⭐ transitioning 해제됨 = Gun button 색상 복원 시점 → 레이저 발사 + 이미지 복원
        FireFeverFreezeLaser();

        if (freezeImage1 != null)
        {
            freezeImage1.gameObject.SetActive(true);
            freezeImage1.color = new Color(1f, 1f, 1f, 0f);
            freezeImage1.DOFade(70f / 255f, 0.5f).SetEase(Ease.OutQuad);
        }
    }

    // ⭐ v6.4: 화면에 남아있는 Freeze 레이저 오브젝트 정리
    void CleanupFreezeLasers()
    {
        var projectiles = GameObject.FindObjectsByType<Projectile>(FindObjectsSortMode.None);
        foreach (var p in projectiles)
        {
            if (p != null && p.gameObject.name.Contains("Freeze"))
                Destroy(p.gameObject);
        }
    }

    // v6.3: Canvas Expand 모드 보정
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

    // === Gun 연기 파티클 ===
    void SpawnGunSmokeParticle()
    {
        if (gunButton == null) return;
        if (activeGunSmoke != null) Destroy(activeGunSmoke);

        RectTransform btnRect = gunButton.GetComponent<RectTransform>();
        float btnSize = Mathf.Max(btnRect.rect.width, btnRect.rect.height);
        float canvasCorr = GetCanvasScaleCorrection();

        GameObject smokeObj = new GameObject("GunSmoke");
        smokeObj.transform.SetParent(gunButton.transform, false);
        smokeObj.transform.localPosition = Vector3.zero;

        ParticleSystem ps = smokeObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 1.5f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(btnSize * 0.25f / canvasCorr, btnSize * 0.5f / canvasCorr);
        main.startSize = new ParticleSystem.MinMaxCurve(btnSize * 0.1f / canvasCorr, btnSize * 0.22f / canvasCorr);
        main.startColor = new Color(0.6f, 0.6f, 0.6f, 0.45f);
        main.maxParticles = 50;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.playOnAwake = true;
        main.loop = true;
        main.gravityModifier = -0.15f;

        var emission = ps.emission; emission.enabled = true; emission.rateOverTime = 8;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 15f; shape.radius = btnSize * 0.08f / canvasCorr;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var sizeOL = ps.sizeOverLifetime; sizeOL.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 0.5f); curve.AddKey(0.4f, 1.0f); curve.AddKey(1f, 2.0f);
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var colOL = ps.colorOverLifetime; colOL.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(0.5f, 0.5f, 0.5f), 0f), new GradientColorKey(new Color(0.65f, 0.65f, 0.65f), 0.3f), new GradientColorKey(new Color(0.8f, 0.8f, 0.8f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.45f, 0f), new GradientAlphaKey(0.3f, 0.4f), new GradientAlphaKey(0f, 1f) }
        );
        colOL.color = new ParticleSystem.MinMaxGradient(g);

        var noise = ps.noise; noise.enabled = true;
        noise.strength = btnSize * 0.04f / canvasCorr; noise.frequency = 0.4f;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = new Material(Shader.Find("UI/Default"));

        var uiP = smokeObj.AddComponent<Coffee.UIExtensions.UIParticle>();
        uiP.scale = 3f;

        ps.Play();
        activeGunSmoke = smokeObj;
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
            Sequence s = DOTween.Sequence();
            s.Append(r.DOAnchorPosY(r.anchoredPosition.y + 60f, 0.7f).SetEase(Ease.OutCubic));
            s.Join(cg.DOFade(0f, 0.7f).SetEase(Ease.InCubic));
            s.Insert(0f, r.DOScale(1.3f, 0.1f).SetEase(Ease.OutQuad));
            s.Insert(0.1f, r.DOScale(1f, 0.15f).SetEase(Ease.InQuad));
            s.OnComplete(() => { if (obj != null) Destroy(obj); });
        }
    }

    // === Gun 모드 토글 ===
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
        // ⭐ v6.4: 큰 타일 2개 어둡게 투명하게
        gridManager.DimProtectedTiles(true);
        UpdateGunUI();
    }

    void ExitGunMode()
    {
        isGunMode = false;
        // ⭐ v6.4: cancel 후에도 이동 불가면 깜빡임 유지 (이동 가능이면 AfterMove에서 꺼짐)
        if (gunModeOverlayImage != null) gunModeOverlayImage.gameObject.SetActive(false);
        gridManager.ClearAllTileBorders();
        gridManager.DimProtectedTiles(false);
        UpdateGuideText();
        UpdateGunUI();
    }

    // === 총 발사 ===
    public void ShootTile()
    {
        if (!hasBullet && (!isFeverMode || feverBulletUsed)) { ExitGunMode(); return; }

        var topTwo = gridManager.GetTopTwoTileValues();
        if (gridManager.ActiveTiles.Count <= 2 || (topTwo.Item1 == 0 && topTwo.Item2 == 0)) { ExitGunMode(); return; }

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

        // HP 전부 회복
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
            // ⭐ v6.3: Freeze Gun → Freeze 유지, 자연 종료 시 환급 없음
            feverBulletUsed = true;
            hasBullet = false;

            // Freeze Gun 전용 연기 파티클
            SpawnGunSmokeParticle();

            if (bossManager != null)
            {
                bossManager.AddTurns(3);
                // □ 추가 시 시각적 강조 애니메이션
                bossManager.PlayBonusTurnEffect();
            }
            if (!bossManager.IsClearMode()) { feverAtkBonus++; feverMergeIncreaseAtk++; }

            Debug.Log("🔫 FREEZE GUN! Freeze 유지, 자연 종료 시 0/40 (환급 없음)");
        }
        else
        {
            // 일반 Gun: 연기 파티클 없음
            mergeGauge = Mathf.Max(0, mergeGauge - GUN_SHOT_COST);
            hasBullet = (mergeGauge >= GAUGE_FOR_BULLET);
        }

        ExitGunMode();
        if (!gridManager.CanMove() && !hasBullet && !isFeverMode) bossBattle.GameOver();
    }

    // === 게이지 변화 Floating Text ===
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
            Sequence s = DOTween.Sequence();
            s.Append(r.DOAnchorPosY(r.anchoredPosition.y + 80f, 0.8f).SetEase(Ease.OutCubic));
            s.Join(cg.DOFade(0f, 0.8f).SetEase(Ease.InCubic));
            s.Insert(0f, r.DOScale(1.2f, 0.1f).SetEase(Ease.OutQuad));
            s.Insert(0.1f, r.DOScale(1f, 0.1f).SetEase(Ease.InQuad));
            s.OnComplete(() => { if (obj != null) Destroy(obj); });
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

            if (isFeverMode)
            {
                if (gridManager != null && gridManager.ComboCount >= 2)
                    turnsUntilBulletText.text = $"{mergeGauge}/{GAUGE_MAX} Combo!";
                else
                    turnsUntilBulletText.text = $"{mergeGauge}/{GAUGE_MAX}";
            }
            else
            {
                if (justEndedFeverWithoutShot && mergeGauge == GAUGE_FOR_BULLET)
                    turnsUntilBulletText.text = $"{GAUGE_FOR_BULLET}/{GAUGE_MAX} Gun Payback!";
                else
                    turnsUntilBulletText.text = $"{mergeGauge}/{GAUGE_MAX}";
            }

            if (mergeGauge != lastMergeGauge)
            {
                lastMergeGauge = mergeGauge;
                RectTransform tr = turnsUntilBulletText.GetComponent<RectTransform>();
                tr.DOKill();
                Sequence seq = DOTween.Sequence();
                seq.Append(tr.DOAnchorPosY(turnsTextOriginalY + 8f, 0.12f).SetEase(Ease.OutQuad));
                seq.Append(tr.DOAnchorPosY(turnsTextOriginalY, 0.12f).SetEase(Ease.InQuad));
                seq.OnComplete(() => { if (tr != null) tr.anchoredPosition = new Vector2(tr.anchoredPosition.x, turnsTextOriginalY); });
            }
        }

        if (attackPowerText != null)
        {
            if (!attackTextInitialized)
            {
                attackTextOriginalY = attackPowerText.GetComponent<RectTransform>().anchoredPosition.y;
                attackTextInitialized = true;
            }

            string bulletIcon = isFeverMode ? "■" : "□";
            attackPowerText.text = $"{bulletIcon} ATK+{permanentAttackPower}";

            if (permanentAttackPower != lastPermanentAttackPower)
            {
                long increase = permanentAttackPower - lastPermanentAttackPower;
                lastPermanentAttackPower = permanentAttackPower;
                RectTransform tr = attackPowerText.GetComponent<RectTransform>();
                tr.DOKill();
                Sequence seq = DOTween.Sequence();
                seq.Append(tr.DOAnchorPosY(attackTextOriginalY + 10f, 0.15f).SetEase(Ease.OutQuad));
                seq.Append(tr.DOAnchorPosY(attackTextOriginalY, 0.15f).SetEase(Ease.InQuad));
                seq.OnComplete(() => { if (tr != null) tr.anchoredPosition = new Vector2(tr.anchoredPosition.x, attackTextOriginalY); });
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

    // === Guide Text ===
    public void UpdateGuideText()
    {
        if (gunModeGuideText == null) return;
        if (isGunMode) { gunModeGuideText.gameObject.SetActive(true); gunModeGuideText.text = "Cancel"; return; }
        gunModeGuideText.gameObject.SetActive(true);
        if (isFeverMode)
        {
            if (feverBulletUsed)
                gunModeGuideText.text = "Cool\nDown";
            else
                gunModeGuideText.text = "Freeze\nGun!";
        }
        else if (hasBullet) gunModeGuideText.text = "Gun\nReady";
        else gunModeGuideText.text = "";
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

    // === ⭐ v6.4: 이동 불가 + Gun 있을 때 긴급 깜빡임 ===
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

        // Freeze gun이면 하늘색↔붉은색, 일반 gun이면 민트↔붉은색
        Color colorA = isFeverMode ? new Color(0.4f, 0.85f, 1f) : new Color(0.6f, 0.95f, 0.85f);
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
    }

    // === Cleanup ===
    public void CleanupFeverEffects()
    {
        if (activeFeverParticle != null) { Destroy(activeFeverParticle); activeFeverParticle = null; }
        if (feverBackgroundImage != null) { feverBackgroundImage.DOKill(); feverBackgroundImage.gameObject.SetActive(false); }
        if (freezeImage1 != null) freezeImage1.gameObject.SetActive(false);
        if (activeGunSmoke != null) { Destroy(activeGunSmoke); activeGunSmoke = null; }
        if (bossManager != null) bossManager.SetFrozen(false);
        RestoreProgressBarColor();
    }
}
