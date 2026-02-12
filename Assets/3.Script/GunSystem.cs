// =====================================================
// GunSystem.cs - v6.2
// Gun 모드, Freeze, 게이지(0~32), ATK 보너스
// Freeze: 32/32 → 이동 -2, 콤보 +2*n, 16/32 도달시 종료
// Freeze Gun: 즉시 종료 아님, 자연 소진 후 0/32 (쏜경우) or 16/32 Payback
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
    [SerializeField] private Image freezeImage1; // infoFreeze (불꽃 효과)
    [SerializeField] private Image freezeImage2; // imageFreeze (얼음 이미지)

    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private PlayerHPSystem playerHP;
    [SerializeField] private BossBattleSystem bossBattle;
    [SerializeField] private BossManager bossManager;

    // 상수 (게이지 0~32)
    private const int GAUGE_MAX = 32;
    private const int GAUGE_FOR_BULLET = 16;
    private const int FREEZE_START_GAUGE = 32;
    private const int FREEZE_END_GAUGE = 16;
    private const int FREEZE_MOVE_COST = 2;
    private const int FREEZE_COMBO_BONUS = 2;
    private const int GUN_SHOT_COST = 16;

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

    // Progress bar 원래 색상
    private Color progressBarOriginalColor;
    private bool progressBarColorSaved = false;

    // Fever 파티클
    private GameObject activeFeverParticle;

    // Gun 연기 파티클 (Freeze중 사용시 유지)
    private GameObject activeGunSmoke;

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
        // Freeze 이미지 자동 설정
        if (freezeImage1 == null)
        {
            GameObject obj = GameObject.Find("infoFreeze");
            if (obj != null) { freezeImage1 = obj.GetComponent<Image>(); Debug.Log("✅ freezeImage1 자동 연결: infoFreeze"); }
        }
        if (freezeImage2 == null)
        {
            GameObject obj = GameObject.Find("imageFreeze");
            if (obj != null) { freezeImage2 = obj.GetComponent<Image>(); Debug.Log("✅ freezeImage2 자동 연결: imageFreeze"); }
        }
        if (freezeImage1 != null) { freezeImage1.color = new Color(1f, 1f, 1f, 70f / 255f); freezeImage1.gameObject.SetActive(false); }
        if (freezeImage2 != null) { freezeImage2.color = new Color(1f, 1f, 1f, 70f / 255f); freezeImage2.gameObject.SetActive(false); }

        if (progressBarFill != null && !progressBarColorSaved)
        {
            Image fillImg = progressBarFill.GetComponent<Image>();
            if (fillImg != null) { progressBarOriginalColor = fillImg.color; progressBarColorSaved = true; }
        }

        if (gunButton != null) gunButton.onClick.AddListener(ToggleGunMode);
        UpdateGunUI();
    }

    public void ResetState()
    {
        mergeGauge = 0; hasBullet = false; isFeverMode = false;
        feverAtkBonus = 0; feverMergeAtkBonus = 0; feverMergeIncreaseAtk = 1; permanentAttackPower = 0;
        feverBulletUsed = false; isGunMode = false; justEndedFeverWithoutShot = false;

        if (gunButtonHeartbeat != null) { gunButtonHeartbeat.Kill(); gunButtonHeartbeat = null; }
        if (gunModeGuideText != null) gunModeGuideText.gameObject.SetActive(false);
        if (freezeImage1 != null) freezeImage1.gameObject.SetActive(false);
        if (freezeImage2 != null) freezeImage2.gameObject.SetActive(false);
        if (activeFeverParticle != null) { Destroy(activeFeverParticle); activeFeverParticle = null; }
        if (activeGunSmoke != null) { Destroy(activeGunSmoke); activeGunSmoke = null; }
        if (feverBackgroundImage != null) { feverBackgroundImage.DOKill(); feverBackgroundImage.gameObject.SetActive(false); }

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

    // === Freeze 턴 처리 (AfterMove에서 호출) ===
    public void ProcessFreezeAfterMove(int comboCount)
    {
        if (!isFeverMode) return;

        int netChange = 0;

        // 콤보 보너스 먼저 (연장 우선)
        if (comboCount >= 2)
        {
            int bonus = FREEZE_COMBO_BONUS * comboCount;
            int before = mergeGauge;
            mergeGauge += bonus;
            if (mergeGauge > GAUGE_MAX) mergeGauge = GAUGE_MAX;
            netChange += (mergeGauge - before);
        }

        // 이동 비용
        mergeGauge -= FREEZE_MOVE_COST;
        netChange -= FREEZE_MOVE_COST;

        // 합산된 변화량 1개만 표시
        if (netChange != 0) ShowGaugeChangeText(netChange);

        Debug.Log($"❄️ Freeze: gauge={mergeGauge}/{GAUGE_MAX} (net:{netChange:+#;-#;0})");

        // 16/32 이하 도달시 종료
        if (mergeGauge <= FREEZE_END_GAUGE) EndFever();

        UpdateGunUI();
    }

    // === Gauge & Fever 체크 (AfterMove 마지막에서만) ===
    public void CheckGaugeAndFever()
    {
        if (isFeverMode) return;

        if (mergeGauge >= GAUGE_MAX)
            StartFever();
        else if (mergeGauge >= GAUGE_FOR_BULLET && !hasBullet)
        {
            hasBullet = true;
            Debug.Log($"Bullet ready! ({mergeGauge}/{GAUGE_MAX})");
            UpdateGunButtonAnimation();
        }

        UpdateGunUI();
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
        if (freezeImage2 != null) freezeImage2.gameObject.SetActive(true);

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
        if (freezeImage2 != null) freezeImage2.gameObject.SetActive(false);

        if (bossManager != null) bossManager.SetFrozen(false);
        isFeverMode = false;
        RestoreProgressBarColor();

        if (feverBulletUsed)
        {
            // Gun 사용했으면 → 0/32 (Payback 없음)
            mergeGauge = 0;
            hasBullet = false;
            justEndedFeverWithoutShot = false;
            Debug.Log("FREEZE END! Gun used → 0/32");
        }
        else
        {
            // Gun 안 쏘면 → 16/32 Gun Payback
            mergeGauge = GAUGE_FOR_BULLET;
            hasBullet = true;
            justEndedFeverWithoutShot = true;
            Debug.Log($"FREEZE END! No shot → {GAUGE_FOR_BULLET}/{GAUGE_MAX} Gun Payback!");
        }
        feverBulletUsed = false;
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

        SpawnFeverParticle();
        if (feverBackgroundImage != null)
        {
            feverBackgroundImage.gameObject.SetActive(true);
            Color c = feverBackgroundImage.color; c.a = 1.0f; feverBackgroundImage.color = c;
            feverBackgroundImage.DOKill();
            feverBackgroundImage.DOFade(0.7f, 0.5f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
        }
        if (freezeImage1 != null) freezeImage1.gameObject.SetActive(true);
        if (freezeImage2 != null) freezeImage2.gameObject.SetActive(true);

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

        GameObject particleObj = new GameObject("FeverFlameParticle");
        particleObj.transform.SetParent(feverParticleSpawnPoint, false);
        particleObj.transform.localPosition = Vector3.zero;

        ParticleSystem ps = particleObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 0.5f; main.startSpeed = 50f; main.startSize = 30f;
        main.startColor = new Color(1f, 0.5f, 0f); main.maxParticles = 50;
        main.simulationSpace = ParticleSystemSimulationSpace.Local; main.playOnAwake = true; main.loop = true;

        var emission = ps.emission; emission.enabled = true; emission.rateOverTime = 20;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 15f; shape.radius = 10f;

        var col = ps.colorOverLifetime; col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(1f, 1f, 0f), 0f), new GradientColorKey(new Color(1f, 0.5f, 0f), 0.5f), new GradientColorKey(new Color(1f, 0f, 0f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = new ParticleSystem.MinMaxGradient(g);

        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.y = new ParticleSystem.MinMaxCurve(100f);
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = new Material(Shader.Find("UI/Default")); renderer.sortingOrder = 1;
        var uiP = particleObj.AddComponent<Coffee.UIExtensions.UIParticle>();
        Canvas fCvs = feverParticleSpawnPoint.GetComponentInParent<Canvas>();
        float fCanvasScale = (fCvs != null && fCvs.rootCanvas != null) ? fCvs.rootCanvas.scaleFactor : 1f;
        uiP.scale = 2f / fCanvasScale;
        uiP.autoScalingMode = Coffee.UIExtensions.UIParticle.AutoScalingMode.None;

        activeFeverParticle = particleObj;
    }

    // === Freeze Sync (Boss 리스폰 시 DOTween 페이드) ===
    public IEnumerator SyncFreezeWithBossRespawn()
    {
        if (freezeImage1 != null) freezeImage1.DOFade(0f, 0.5f).SetEase(Ease.InQuad);
        if (freezeImage2 != null) freezeImage2.DOFade(0f, 0.5f).SetEase(Ease.InQuad);

        yield return new WaitForSeconds(1.5f);

        if (!isFeverMode) { Debug.Log("🧊 Freeze 종료됨, 이미지 복원 안함"); yield break; }

        float targetAlpha = 70f / 255f;
        if (freezeImage1 != null) freezeImage1.DOFade(targetAlpha, 0.5f).SetEase(Ease.OutQuad);
        if (freezeImage2 != null) freezeImage2.DOFade(targetAlpha, 0.5f).SetEase(Ease.OutQuad);
        Debug.Log("🧊 Freeze 이미지 Boss 리스폰 싱크 완료!");
    }

    // === Gun 발사 시 연기 파티클 (위로 피어오르는 재, loop) ===
    void SpawnGunSmokeParticle()
    {
        if (gunButton == null) return;

        // 기존 연기 정리
        if (activeGunSmoke != null) Destroy(activeGunSmoke);

        GameObject smokeObj = new GameObject("GunSmoke");
        smokeObj.transform.SetParent(gunButton.transform, false);
        smokeObj.transform.localPosition = Vector3.zero;

        ParticleSystem ps = smokeObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 1.2f;
        // 버튼 크기 기준 적응형 파티클
        RectTransform btnRect = gunButton.GetComponent<RectTransform>();
        float btnSize = Mathf.Max(btnRect.rect.width, btnRect.rect.height);

        main.startLifetime = 1.5f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(btnSize * 0.25f, btnSize * 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(btnSize * 0.1f, btnSize * 0.22f);
        main.startColor = new Color(0.6f, 0.6f, 0.6f, 0.45f);
        main.maxParticles = 50;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.playOnAwake = true;
        main.loop = true;
        main.gravityModifier = -0.15f;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 8;

        // Cone 위쪽으로
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 15f;
        shape.radius = btnSize * 0.08f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        // 커지면서 흐려짐
        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 0.5f);
        curve.AddKey(0.4f, 1.0f);
        curve.AddKey(1f, 2.0f);
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, curve);

        // 재 색상: 회색 → 연회색 → 투명
        var colOL = ps.colorOverLifetime;
        colOL.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.5f, 0.5f, 0.5f), 0f),
                new GradientColorKey(new Color(0.65f, 0.65f, 0.65f), 0.3f),
                new GradientColorKey(new Color(0.8f, 0.8f, 0.8f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.45f, 0f),
                new GradientAlphaKey(0.3f, 0.4f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colOL.color = new ParticleSystem.MinMaxGradient(g);

        // 소용돌이 느낌
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = btnSize * 0.04f;
        noise.frequency = 0.4f;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = new Material(Shader.Find("UI/Default"));

        var uiP = smokeObj.AddComponent<Coffee.UIExtensions.UIParticle>();
        Canvas cvs = gunButton.GetComponentInParent<Canvas>();
        float canvasScale = (cvs != null && cvs.rootCanvas != null) ? cvs.rootCanvas.scaleFactor : 1f;
        uiP.scale = 2.5f / canvasScale;
        uiP.autoScalingMode = Coffee.UIExtensions.UIParticle.AutoScalingMode.None;

        ps.Play();
        activeGunSmoke = smokeObj;
        Debug.Log($"💨 Gun 연기 파티클 생성! (btnSize={btnSize}, canvasScale={canvasScale})");
    }

    // === ATK 증가 Floating Text ===
    void ShowATKChangeText(long increase)
    {
        if (damageTextPrefab == null || damageTextParent == null || attackPowerText == null) return;

        GameObject obj = Instantiate(damageTextPrefab, damageTextParent);
        TextMeshProUGUI txt = obj.GetComponent<TextMeshProUGUI>();
        if (txt != null)
        {
            txt.text = $"+{increase}";
            txt.color = new Color(1f, 0.7f, 0.2f); // 금색
            txt.fontSize = 32;

            RectTransform r = obj.GetComponent<RectTransform>();
            r.position = attackPowerText.GetComponent<RectTransform>().position;

            CanvasGroup cg = obj.GetComponent<CanvasGroup>();
            if (cg == null) cg = obj.AddComponent<CanvasGroup>();

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
        gridManager.UpdateTileBorders();
        UpdateGunUI();
    }

    void ExitGunMode()
    {
        isGunMode = false;
        gridManager.ClearAllTileBorders();
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

        // 연기 파티클
        SpawnGunSmokeParticle();

        if (isFeverMode)
        {
            // Freeze 중 Gun: 즉시 종료 아님! feverBulletUsed만 세팅
            feverBulletUsed = true;
            hasBullet = false;

            if (bossManager != null) bossManager.AddTurns(3);

            if (!bossManager.IsClearMode()) { feverAtkBonus++; feverMergeIncreaseAtk++; }

            Debug.Log("🔫 FREEZE GUN! 연기 파티클 + Freeze 계속 진행 (종료 시 0/32)");
        }
        else
        {
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

            CanvasGroup cg = obj.GetComponent<CanvasGroup>();
            if (cg == null) cg = obj.AddComponent<CanvasGroup>();

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

                // DOTween 바운스 + floating text 둘 다
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

        if (gunButtonImage != null)
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
        if (isFeverMode) gunModeGuideText.text = "Freeze\nGun!";
        else if (hasBullet) gunModeGuideText.text = "Gun\nReady";
        else gunModeGuideText.text = "Gun";
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

    // === Cleanup ===
    public void CleanupFeverEffects()
    {
        if (activeFeverParticle != null) { Destroy(activeFeverParticle); activeFeverParticle = null; }
        if (feverBackgroundImage != null) { feverBackgroundImage.DOKill(); feverBackgroundImage.gameObject.SetActive(false); }
        if (freezeImage1 != null) freezeImage1.gameObject.SetActive(false);
        if (freezeImage2 != null) freezeImage2.gameObject.SetActive(false);
        if (activeGunSmoke != null) { Destroy(activeGunSmoke); activeGunSmoke = null; }
        if (bossManager != null) bossManager.SetFrozen(false);
        RestoreProgressBarColor();
    }
}
