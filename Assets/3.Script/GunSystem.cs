// =====================================================
// GunSystem.cs - v6.1
// Gun 모드, Freeze(Fever), 게이지, ATK 보너스 관리
// Freeze: 40/40 → 이동 -2, 콤보 +2, 20/40 도달시 종료
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
    [SerializeField] private Image freezeImage2;

    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private PlayerHPSystem playerHP;
    [SerializeField] private BossBattleSystem bossBattle;
    [SerializeField] private BossManager bossManager;

    // 상수
    private const int GAUGE_MAX = 40;
    private const int GAUGE_FOR_BULLET = 20;
    private const int FREEZE_START_GAUGE = 40;
    private const int FREEZE_END_GAUGE = 20;
    private const int FREEZE_MOVE_COST = 2;
    private const int FREEZE_COMBO_BONUS = 2;

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
    private int lastMergeGauge = 0;

    // Progress bar 원래 색상
    private Color progressBarOriginalColor;
    private bool progressBarColorSaved = false;

    // Fever 파티클
    private GameObject activeFeverParticle;

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
        // Freeze 이미지 자동 설정 및 초기화
        if (freezeImage1 == null)
        {
            GameObject freezeObj1 = GameObject.Find("infoFreeze");
            if (freezeObj1 != null)
            {
                freezeImage1 = freezeObj1.GetComponent<Image>();
                Debug.Log("✅ freezeImage1 자동 연결 완료: infoFreeze");
            }
        }

        if (freezeImage2 == null)
        {
            GameObject freezeObj2 = GameObject.Find("imageFreeze");
            if (freezeObj2 != null)
            {
                freezeImage2 = freezeObj2.GetComponent<Image>();
                Debug.Log("✅ freezeImage2 자동 연결 완료: imageFreeze");
            }
        }

        if (freezeImage1 != null)
        {
            float alphaValue = 70f / 255f;
            freezeImage1.color = new Color(1f, 1f, 1f, alphaValue);
            freezeImage1.gameObject.SetActive(false);
        }

        if (freezeImage2 != null)
        {
            float alphaValue = 70f / 255f;
            freezeImage2.color = new Color(1f, 1f, 1f, alphaValue);
            freezeImage2.gameObject.SetActive(false);
        }

        // Progress bar 원래 색상 저장
        if (progressBarFill != null && !progressBarColorSaved)
        {
            Image fillImg = progressBarFill.GetComponent<Image>();
            if (fillImg != null)
            {
                progressBarOriginalColor = fillImg.color;
                progressBarColorSaved = true;
            }
        }

        if (gunButton != null)
            gunButton.onClick.AddListener(ToggleGunMode);

        UpdateGunUI();
    }

    public void ResetState()
    {
        mergeGauge = 0;
        hasBullet = false;
        isFeverMode = false;
        feverAtkBonus = 0;
        feverMergeAtkBonus = 0;
        feverMergeIncreaseAtk = 1;
        permanentAttackPower = 0;
        feverBulletUsed = false;
        isGunMode = false;
        justEndedFeverWithoutShot = false;

        if (gunButtonHeartbeat != null)
        {
            gunButtonHeartbeat.Kill();
            gunButtonHeartbeat = null;
        }

        if (gunModeGuideText != null)
            gunModeGuideText.gameObject.SetActive(false);

        if (freezeImage1 != null) freezeImage1.gameObject.SetActive(false);
        if (freezeImage2 != null) freezeImage2.gameObject.SetActive(false);

        if (activeFeverParticle != null)
        {
            Destroy(activeFeverParticle);
            activeFeverParticle = null;
        }

        if (feverBackgroundImage != null)
        {
            feverBackgroundImage.DOKill();
            feverBackgroundImage.gameObject.SetActive(false);
        }

        RestoreProgressBarColor();
        UpdateGunUI();
    }

    // === 게이지 조작 ===
    public void AddMergeGauge(int amount)
    {
        mergeGauge += amount;
        if (mergeGauge > GAUGE_MAX)
            mergeGauge = GAUGE_MAX;
    }

    public void ClearFeverPaybackIfNeeded()
    {
        if (justEndedFeverWithoutShot && mergeGauge > 20)
            justEndedFeverWithoutShot = false;
    }

    // === Fever Merge ATK 증가 ===
    public void AddFeverMergeATK()
    {
        permanentAttackPower += feverMergeIncreaseAtk;
    }

    // === Freeze 턴 처리 (AfterMove에서 호출) ===
    // 1이동당 -2, 콤보시 +2*comboCount
    public void ProcessFreezeAfterMove(int comboCount)
    {
        if (!isFeverMode) return;

        int oldGauge = mergeGauge;

        // 콤보 보너스 먼저
        if (comboCount >= 2)
        {
            int bonus = FREEZE_COMBO_BONUS * comboCount;
            mergeGauge += bonus;
            if (mergeGauge > GAUGE_MAX)
                mergeGauge = GAUGE_MAX;
            ShowGaugeChangeText(bonus);
            Debug.Log($"❄️ Freeze 콤보 보너스! +{bonus} ({mergeGauge}/{GAUGE_MAX})");
        }

        // 이동 비용
        mergeGauge -= FREEZE_MOVE_COST;
        ShowGaugeChangeText(-FREEZE_MOVE_COST);
        Debug.Log($"❄️ Freeze 이동 비용 -{FREEZE_MOVE_COST} ({mergeGauge}/{GAUGE_MAX})");

        // 20/40 이하 도달시 종료
        if (mergeGauge <= FREEZE_END_GAUGE)
        {
            EndFever();
        }

        UpdateGunUI();
    }

    // === Gauge & Fever 체크 (AfterMove 마지막에서만 호출) ===
    public void CheckGaugeAndFever()
    {
        if (isFeverMode)
        {
            // Freeze 종료는 ProcessFreezeAfterMove에서 처리
            return;
        }

        if (mergeGauge >= GAUGE_MAX)
        {
            StartFever();
        }
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
            Color c = feverBackgroundImage.color;
            c.a = 1.0f;
            feverBackgroundImage.color = c;

            feverBackgroundImage.DOKill();
            feverBackgroundImage.DOFade(0.7f, 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        if (freezeImage1 != null) freezeImage1.gameObject.SetActive(true);
        if (freezeImage2 != null) freezeImage2.gameObject.SetActive(true);

        if (bossManager != null)
            bossManager.SetFrozen(true);

        FireFeverFreezeLaser();

        isFeverMode = true;
        feverBulletUsed = false;
        mergeGauge = FREEZE_START_GAUGE; // 항상 40/40에서 시작
        hasBullet = false;
        Debug.Log($"FREEZE MODE! Gauge: {mergeGauge}/{GAUGE_MAX}");
        UpdateGunButtonAnimation();

        // Progress bar 붉은색
        SetProgressBarFreezeColor();

        // ⭐ v6.0: Clear 모드에서는 ATK 증가 비활성
        if (!bossManager.IsClearMode())
        {
            feverAtkBonus++;
            Debug.Log($"🔥 FREEZE 진입! ATK Bonus +1 (Total: {feverAtkBonus})");

            feverMergeIncreaseAtk++;
            Debug.Log($"🔥 FREEZE 진입! 머지 증가량 +1 (Now: {feverMergeIncreaseAtk})");
        }
    }

    void EndFever()
    {
        if (activeFeverParticle != null)
        {
            Destroy(activeFeverParticle);
            activeFeverParticle = null;
        }

        if (feverBackgroundImage != null)
        {
            feverBackgroundImage.DOKill();
            feverBackgroundImage.gameObject.SetActive(false);
        }

        if (freezeImage1 != null) freezeImage1.gameObject.SetActive(false);
        if (freezeImage2 != null) freezeImage2.gameObject.SetActive(false);

        if (bossManager != null)
            bossManager.SetFrozen(false);

        isFeverMode = false;

        // Progress bar 원래 색상 복원
        RestoreProgressBarColor();

        if (feverBulletUsed)
        {
            mergeGauge = 0;
            hasBullet = false;
            justEndedFeverWithoutShot = false;
            Debug.Log("FREEZE END! Shot used, reset to 0/40");
        }
        else
        {
            mergeGauge = 20;
            hasBullet = true;
            justEndedFeverWithoutShot = true;
            Debug.Log("FREEZE END! No shot, keep 20/40 - PAYBACK!");
        }
        feverBulletUsed = false;
    }

    // === Progress bar 색상 ===
    void SetProgressBarFreezeColor()
    {
        if (progressBarFill == null) return;
        Image fillImg = progressBarFill.GetComponent<Image>();
        if (fillImg != null)
            fillImg.color = new Color(0.9f, 0.2f, 0.2f); // 붉은색
    }

    void RestoreProgressBarColor()
    {
        if (progressBarFill == null || !progressBarColorSaved) return;
        Image fillImg = progressBarFill.GetComponent<Image>();
        if (fillImg != null)
            fillImg.color = progressBarOriginalColor;
    }

    // === Continue → Fever 진입 ===
    public void ContinueIntoFever()
    {
        isFeverMode = true;
        mergeGauge = FREEZE_START_GAUGE; // 40/40
        feverBulletUsed = false;
        hasBullet = false;

        SpawnFeverParticle();

        if (feverBackgroundImage != null)
        {
            feverBackgroundImage.gameObject.SetActive(true);
            Color c = feverBackgroundImage.color;
            c.a = 1.0f;
            feverBackgroundImage.color = c;

            feverBackgroundImage.DOKill();
            feverBackgroundImage.DOFade(0.7f, 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        if (freezeImage1 != null) freezeImage1.gameObject.SetActive(true);
        if (freezeImage2 != null) freezeImage2.gameObject.SetActive(true);

        if (bossManager != null)
        {
            bossManager.SetFrozen(true);
            bossManager.ResetBonusTurns();
        }

        SetProgressBarFreezeColor();
        FireFeverFreezeLaser();
        UpdateGunUI();

        Debug.Log("🎮 CONTINUE! Freeze 40/40 진입!");
    }

    // === Fever Freeze 레이저 ===
    void FireFeverFreezeLaser()
    {
        ProjectileManager pm = bossBattle.GetProjectileManager();
        if (pm == null || gunButton == null || bossManager == null || bossManager.bossImageArea == null) return;

        Vector3 startPos = gunButton.transform.position;
        RectTransform monsterRect = bossManager.bossImageArea.GetComponent<RectTransform>();
        Vector3 targetPos = monsterRect.position;

        Color iceColor = new Color(0.5f, 0.85f, 1f, 0.9f);
        pm.FireFreezeLaser(startPos, targetPos, iceColor, null);
        Debug.Log("🧊 Freeze Laser 발사! GunButton → Enemy");
    }

    // === Fever 파티클 ===
    void SpawnFeverParticle()
    {
        if (feverParticleSpawnPoint == null)
        {
            Debug.LogWarning("Fever particle spawn point not set!");
            return;
        }

        if (activeFeverParticle != null)
            Destroy(activeFeverParticle);

        GameObject particleObj = new GameObject("FeverFlameParticle");
        particleObj.transform.SetParent(feverParticleSpawnPoint, false);
        particleObj.transform.localPosition = Vector3.zero;

        ParticleSystem ps = particleObj.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.startLifetime = 0.5f;
        main.startSpeed = 50f;
        main.startSize = 30f;
        main.startColor = new Color(1f, 0.5f, 0f);
        main.maxParticles = 50;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.playOnAwake = true;
        main.loop = true;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 20;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 15f;
        shape.radius = 10f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 1f, 0f), 0.0f),
                new GradientColorKey(new Color(1f, 0.5f, 0f), 0.5f),
                new GradientColorKey(new Color(1f, 0f, 0f), 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(0.8f, 0.5f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var velocityOverLifetime = ps.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(100f);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = new Material(Shader.Find("UI/Default"));
        renderer.sortingOrder = 1;

        var uiParticle = particleObj.AddComponent<Coffee.UIExtensions.UIParticle>();
        uiParticle.scale = 2f;

        activeFeverParticle = particleObj;
        Debug.Log("Freeze flame particle spawned!");
    }

    // === Freeze Sync (보스 리스폰 시) ===
    public IEnumerator SyncFreezeWithBossRespawn()
    {
        if (freezeImage1 != null)
            freezeImage1.DOFade(0f, 0.5f).SetEase(Ease.InQuad);
        if (freezeImage2 != null)
            freezeImage2.DOFade(0f, 0.5f).SetEase(Ease.InQuad);

        yield return new WaitForSeconds(1.5f);

        if (!isFeverMode)
        {
            Debug.Log("🧊 Freeze 모드가 종료되어 Freeze 이미지 복원 안함");
            yield break;
        }

        if (freezeImage1 != null)
        {
            float targetAlpha = 70f / 255f;
            freezeImage1.DOFade(targetAlpha, 0.5f).SetEase(Ease.OutQuad);
        }
        if (freezeImage2 != null)
        {
            float targetAlpha = 70f / 255f;
            freezeImage2.DOFade(targetAlpha, 0.5f).SetEase(Ease.OutQuad);
        }

        Debug.Log("🧊 Freeze 이미지 Boss와 함께 리스폰 완료!");
    }

    // === Gun 모드 토글 ===
    public void ToggleGunMode()
    {
        if (bossBattle.IsBossAttacking)
        {
            Debug.Log("보스 공격 중에는 Gun Mode 전환 불가!");
            return;
        }

        if (isGunMode)
        {
            ExitGunMode();
            return;
        }

        if (!hasBullet && (!isFeverMode || feverBulletUsed)) return;

        if (gridManager.ActiveTiles.Count <= 2)
        {
            Debug.Log("타일이 2개 이하일 때는 총을 쓸 수 없습니다!");
            return;
        }

        isGunMode = true;

        // Gun mode 진입 시 guide text는 "Cancel"
        if (gunModeGuideText != null)
        {
            gunModeGuideText.gameObject.SetActive(true);
            gunModeGuideText.text = "Cancel";
        }

        gridManager.UpdateTileBorders();
        UpdateGunUI();
    }

    void ExitGunMode()
    {
        isGunMode = false;

        if (gunModeGuideText != null)
        {
            gunModeGuideText.transform.localScale = Vector3.one;
            // 상태별 가이드 텍스트 복원
            UpdateGuideText();
        }

        gridManager.ClearAllTileBorders();
        UpdateGunUI();
    }

    // === 총 발사 ===
    public void ShootTile()
    {
        if (!hasBullet && (!isFeverMode || feverBulletUsed))
        {
            ExitGunMode();
            return;
        }

        var topTwoValues = gridManager.GetTopTwoTileValues();
        if (gridManager.ActiveTiles.Count <= 2 || (topTwoValues.Item1 == 0 && topTwoValues.Item2 == 0))
        {
            Debug.Log("타일이 2개 이하이거나 보호된 타일만 남았습니다!");
            ExitGunMode();
            return;
        }

        Canvas canvas = gridManager.GridContainer.GetComponentInParent<Canvas>();
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            gridManager.GridContainer,
            Input.mousePosition,
            cam,
            out localPoint
        );

        Tile targetTile = null;
        float minDistance = gridManager.CellSize / 2;

        foreach (var tile in gridManager.ActiveTiles)
        {
            if (tile == null) continue;
            RectTransform tileRect = tile.GetComponent<RectTransform>();
            float distance = Vector2.Distance(localPoint, tileRect.anchoredPosition);
            if (distance < minDistance)
            {
                minDistance = distance;
                targetTile = tile;
            }
        }

        if (targetTile != null)
        {
            var currentTopTwo = gridManager.GetTopTwoTileValues();

            if (targetTile.value == currentTopTwo.Item1 || targetTile.value == currentTopTwo.Item2)
            {
                Debug.Log($"❌ 가장 큰 값 타일({targetTile.value})은 부술 수 없습니다!");
                return;
            }

            // 체력 전부 회복
            int oldHeat = playerHP.CurrentHeat;
            playerHP.SetHeatToMax();
            playerHP.UpdateHeatUI(false);

            int recovery = playerHP.CurrentHeat - oldHeat;
            if (recovery > 0)
                playerHP.ShowHeatChangeText(recovery);

            Debug.Log("💚 총 발사! 체력 전부 회복!");

            Vector2Int pos = targetTile.gridPosition;
            targetTile.PlayGunDestroyEffect();

            gridManager.Tiles[pos.x, pos.y] = null;
            gridManager.ActiveTiles.Remove(targetTile);
            Destroy(targetTile.gameObject);

            if (isFeverMode)
            {
                feverBulletUsed = true;
                mergeGauge = 0;
                hasBullet = false;
                Debug.Log("FREEZE SHOT! Bullet used, gauge reset to 0/40");

                if (bossManager != null)
                {
                    bossManager.AddTurns(3);
                    Debug.Log("🔥 FREEZE SHOT! 보스 공격 턴 +3");
                }

                // ⭐ v6.0: Clear 모드에서는 ATK 증가 비활성
                if (!bossManager.IsClearMode())
                {
                    feverAtkBonus++;
                    Debug.Log($"🔥 FREEZE ATK BONUS +1! (Total: {feverAtkBonus})");

                    feverMergeIncreaseAtk++;
                    Debug.Log($"🔥 FREEZE GUN! 머지 증가량 +1 (Now: {feverMergeIncreaseAtk})");
                }

                // Freeze 즉시 종료
                EndFever();
            }
            else
            {
                mergeGauge = Mathf.Max(0, mergeGauge - GAUGE_FOR_BULLET);
                hasBullet = (mergeGauge >= GAUGE_FOR_BULLET);
                Debug.Log($"GUN SHOT! Remaining charge: {mergeGauge}/{GAUGE_MAX}");
            }

            ExitGunMode();

            if (!gridManager.CanMove() && !hasBullet && !isFeverMode)
                bossBattle.GameOver();
        }
    }

    // === 게이지 변화 Floating Text ===
    void ShowGaugeChangeText(int change)
    {
        if (damageTextPrefab == null || damageTextParent == null || turnsUntilBulletText == null) return;

        GameObject changeObj = Instantiate(damageTextPrefab, damageTextParent);
        TextMeshProUGUI changeText = changeObj.GetComponent<TextMeshProUGUI>();

        if (changeText != null)
        {
            if (change > 0)
            {
                changeText.text = $"+{change}";
                changeText.color = new Color(0.9f, 0.2f, 0.2f); // 붉은 글자
            }
            else
            {
                changeText.text = change.ToString();
                changeText.color = new Color(0.6f, 0.6f, 0.6f); // 회색 글자
            }

            changeText.fontSize = 36;

            RectTransform changeRect = changeObj.GetComponent<RectTransform>();
            RectTransform targetRect = turnsUntilBulletText.GetComponent<RectTransform>();
            changeRect.position = targetRect.position;

            CanvasGroup canvasGroup = changeObj.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = changeObj.AddComponent<CanvasGroup>();

            Sequence seq = DOTween.Sequence();
            seq.Append(changeRect.DOAnchorPosY(changeRect.anchoredPosition.y + 80f, 0.8f).SetEase(Ease.OutCubic));
            seq.Join(canvasGroup.DOFade(0f, 0.8f).SetEase(Ease.InCubic));
            seq.Insert(0f, changeRect.DOScale(1.2f, 0.1f).SetEase(Ease.OutQuad));
            seq.Insert(0.1f, changeRect.DOScale(1f, 0.1f).SetEase(Ease.InQuad));
            seq.OnComplete(() => {
                if (changeObj != null) Destroy(changeObj);
            });
        }
    }

    // === Gun UI 업데이트 ===
    public void UpdateGunUI()
    {
        // bulletCountText: CANCEL 없음
        if (bulletCountText != null)
        {
            if (isFeverMode)
                bulletCountText.text = "FREEZE";
            else if (hasBullet)
                bulletCountText.text = "CHARGE";
            else
                bulletCountText.text = "RELOAD";
        }

        // gunModeGuideText: 상태별 표시
        UpdateGuideText();

        // turnsUntilBulletText: 통합 게이지 표시
        if (turnsUntilBulletText != null)
        {
            if (!turnsTextInitialized)
            {
                RectTransform textRect = turnsUntilBulletText.GetComponent<RectTransform>();
                turnsTextOriginalY = textRect.anchoredPosition.y;
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
                if (justEndedFeverWithoutShot && mergeGauge == 20)
                    turnsUntilBulletText.text = $"20/{GAUGE_MAX} Payback!";
                else
                    turnsUntilBulletText.text = $"{mergeGauge}/{GAUGE_MAX}";
            }

            // 값 변경 시 바운스 애니메이션
            if (mergeGauge != lastMergeGauge)
            {
                lastMergeGauge = mergeGauge;

                RectTransform textRect = turnsUntilBulletText.GetComponent<RectTransform>();
                textRect.DOKill();

                Sequence seq = DOTween.Sequence();
                seq.Append(textRect.DOAnchorPosY(turnsTextOriginalY + 8f, 0.12f).SetEase(Ease.OutQuad));
                seq.Append(textRect.DOAnchorPosY(turnsTextOriginalY, 0.12f).SetEase(Ease.InQuad));
                seq.OnComplete(() => {
                    if (textRect != null)
                        textRect.anchoredPosition = new Vector2(textRect.anchoredPosition.x, turnsTextOriginalY);
                });
            }
        }

        // attackPowerText
        if (attackPowerText != null)
        {
            if (!attackTextInitialized)
            {
                RectTransform textRect = attackPowerText.GetComponent<RectTransform>();
                attackTextOriginalY = textRect.anchoredPosition.y;
                attackTextInitialized = true;
            }

            attackPowerText.text = $"+ATK: {permanentAttackPower}";

            if (permanentAttackPower != lastPermanentAttackPower)
            {
                lastPermanentAttackPower = permanentAttackPower;

                RectTransform textRect = attackPowerText.GetComponent<RectTransform>();
                textRect.DOKill();

                Sequence seq = DOTween.Sequence();
                seq.Append(textRect.DOAnchorPosY(attackTextOriginalY + 10f, 0.15f).SetEase(Ease.OutQuad));
                seq.Append(textRect.DOAnchorPosY(attackTextOriginalY, 0.15f).SetEase(Ease.InQuad));
                seq.OnComplete(() => {
                    if (textRect != null)
                        textRect.anchoredPosition = new Vector2(textRect.anchoredPosition.x, attackTextOriginalY);
                });
            }
        }

        // progressBarFill: 통합 게이지 바
        if (progressBarFill != null)
        {
            float progress = Mathf.Clamp01((float)mergeGauge / GAUGE_MAX);
            float targetWidth = progressBarFill.parent.GetComponent<RectTransform>().rect.width * progress;

            progressBarFill.DOKill();
            progressBarFill.DOSizeDelta(
                new Vector2(targetWidth, progressBarFill.sizeDelta.y),
                0.3f
            ).SetEase(Ease.OutQuad);
        }

        // Gun button 색상
        if (gunButtonImage != null)
        {
            if (isGunMode)
                gunButtonImage.color = Color.red;
            else if (isFeverMode)
                gunButtonImage.color = new Color(1f, 0.3f, 0f);
            else if (hasBullet)
                gunButtonImage.color = new Color(0.6f, 0.95f, 0.85f);
            else
                gunButtonImage.color = new Color(0.5f, 0.5f, 0.5f);
        }

        if (gunButton != null)
        {
            gunButton.interactable = !bossBattle.IsGameOver && !bossBattle.IsBossTransitioning
                && (hasBullet || (isFeverMode && !feverBulletUsed))
                && gridManager.ActiveTiles.Count > 1;
        }

        bool shouldAnimate = hasBullet || (isFeverMode && !feverBulletUsed);
        UpdateGunButtonAnimationIfNeeded(shouldAnimate);
    }

    // === Guide Text: 상태별 표시 ===
    public void UpdateGuideText()
    {
        if (gunModeGuideText == null) return;

        // Gun 모드 중이면 "Cancel" 유지
        if (isGunMode)
        {
            gunModeGuideText.gameObject.SetActive(true);
            gunModeGuideText.text = "Cancel";
            return;
        }

        gunModeGuideText.gameObject.SetActive(true);

        if (isFeverMode)
            gunModeGuideText.text = "Freeze\nGun!";
        else if (hasBullet)
            gunModeGuideText.text = "Gun\nReady";
        else
            gunModeGuideText.text = "Gun";
    }

    void UpdateGunButtonAnimationIfNeeded(bool shouldAnimate)
    {
        bool currentState = isGunMode || shouldAnimate;
        if (currentState == lastGunButtonAnimationState && gunButtonHeartbeat != null)
            return;

        lastGunButtonAnimationState = currentState;

        if (gunButton == null || gunButtonImage == null) return;

        if (gunButtonHeartbeat != null)
        {
            gunButtonHeartbeat.Kill();
            gunButtonHeartbeat = null;
        }

        if (gunButtonImage != null)
        {
            Color c = gunButtonImage.color;
            c.a = 1f;
            gunButtonImage.color = c;
        }

        gunButton.transform.localScale = Vector3.one;

        if (isGunMode)
        {
            gunButtonHeartbeat = gunButton.transform.DOScale(1.15f, 0.3f)
                .SetEase(Ease.InOutQuad)
                .SetLoops(-1, LoopType.Yoyo);
        }
        else if (shouldAnimate)
        {
            gunButtonHeartbeat = gunButton.transform.DOScale(1.1f, 0.6f)
                .SetEase(Ease.InOutQuad)
                .SetLoops(-1, LoopType.Yoyo);
        }
    }

    void UpdateGunButtonAnimation()
    {
        if (gunButton == null || gunButtonImage == null) return;

        if (gunButtonHeartbeat != null)
        {
            gunButtonHeartbeat.Kill();
            gunButtonHeartbeat = null;
        }

        if (gunButtonImage != null)
        {
            Color c = gunButtonImage.color;
            c.a = 1f;
            gunButtonImage.color = c;
        }

        gunButton.transform.localScale = Vector3.one;

        if (isGunMode)
        {
            gunButtonHeartbeat = gunButton.transform.DOScale(1.15f, 0.3f)
                .SetEase(Ease.InOutQuad)
                .SetLoops(-1, LoopType.Yoyo);
        }
        else if (hasBullet || (isFeverMode && !feverBulletUsed))
        {
            gunButtonHeartbeat = gunButton.transform.DOScale(1.1f, 0.6f)
                .SetEase(Ease.InOutQuad)
                .SetLoops(-1, LoopType.Yoyo);
        }
    }

    // === Fever 종료 시 클린업 (게임 오버용) ===
    public void CleanupFeverEffects()
    {
        if (activeFeverParticle != null)
        {
            Destroy(activeFeverParticle);
            activeFeverParticle = null;
        }

        if (feverBackgroundImage != null)
        {
            feverBackgroundImage.DOKill();
            feverBackgroundImage.gameObject.SetActive(false);
        }

        if (freezeImage1 != null) freezeImage1.gameObject.SetActive(false);
        if (freezeImage2 != null) freezeImage2.gameObject.SetActive(false);

        if (bossManager != null)
            bossManager.SetFrozen(false);

        RestoreProgressBarColor();
    }
}
