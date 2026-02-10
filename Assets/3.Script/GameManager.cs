// =====================================================
// GameManager.cs - UPDATED VERSION v5.0
// Date: 2026-02-10
// 
// 수정사항 v5.0:
// 1. Gun Mode: 초음파 효과 (안쪽→테두리 지속 웨이브)
// 2. 공격력 44부터 시작, 5 stage마다 1씩 증가
// 3. 피버 불길 레이어 → 가이드 아래로
// 4. 레이저 공격: monsterImage transform 확실히 가져오기
// 5. 21억 HP 표시 한 stage 앞당기기 (stage 39)
// 6. 피버 공격턴 반영 버그 수정
// 7. 피버때 텍스트 표시 (damage*1.8!\nmerge and get atk!)
// 8. Stage 40 infinite + Enemy bar 밝은붉은색 + 20회마다 공격력증가
// 9. Stage 39 clear시 최대체력 증가량 2
// 10. Fever때 GunButton→Enemy 얼음색 레이저
// 11. Gun 20이상일때 파스텔 민트색
// 12. Gun Mode 해제/총쏘면 효과 OFF
// =====================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;

public class GameManager : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int gridSize = 4;
    [SerializeField] private RectTransform gridContainer;
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private GameObject cellPrefab;
    [SerializeField] private float cellSpacing = 20f;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI bestScoreText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button continueButton;

    [Header("Gun System")]
    [SerializeField] private Button gunButton;
    [SerializeField] private TextMeshProUGUI bulletCountText;
    [SerializeField] private TextMeshProUGUI turnsUntilBulletText;
    [SerializeField] private TextMeshProUGUI attackPowerText;
    [SerializeField] private TextMeshProUGUI gunModeGuideText;

    [SerializeField] private Image gunButtonImage;
    [SerializeField] private RectTransform progressBarFill;

    private Tweener gunGuideAnimation;
    private bool isBossAttacking = false;
    private GameObject activeFeverParticle;

    [Header("Fever Effects")]
    [SerializeField] private Transform feverParticleSpawnPoint;
    [SerializeField] private GameObject feverParticlePrefab;
    [SerializeField] private Image feverBackgroundImage;
    [SerializeField] private Image freezeImage1;
    [SerializeField] private Image freezeImage2;

    [Header("Boss System")]
    [SerializeField] private BossManager bossManager;
    [SerializeField] private TextMeshProUGUI hpText;

    [Header("Damage Text")]
    [SerializeField] private GameObject damageTextPrefab;
    [SerializeField] private Transform damageTextParent;

    [Header("Heat System")]
    [SerializeField] private TextMeshProUGUI heatText;
    [SerializeField] private Slider heatSlider;
    [SerializeField] private Image heatBarImage;
    [SerializeField] private int maxHeat = 100;
    [SerializeField] private int[] comboHeatRecover = { 0, 0, 4, 10, 18, 30 };
    private const int BOSS_DEFEAT_MAX_HEAT_INCREASE = 1;
    [SerializeField] private float heatAnimationDuration = 0.3f;

    [Header("색상 조합 보너스")]
    [SerializeField] private int chocoMergeDamageMultiplier = 4;
    [SerializeField] private int berryMergeHealMultiplier = 4;
    [SerializeField] private int berryMergeBaseHeal = 5;
    [SerializeField] private float feverDamageMultiplier = 1.5f;

    [Header("Low Health Effect")]
    [SerializeField] private LowHealthVignette lowHealthVignette;

    [Header("피격 플래시 효과")]
    [SerializeField] private Image damageFlashImage;

    [Header("Turn & Stage UI")]
    [SerializeField] private TextMeshProUGUI turnText;
    [SerializeField] private TextMeshProUGUI stageText;

    private Tile[,] tiles;
    private List<Tile> activeTiles = new List<Tile>();
    private long score = 0;
    private long bestScore = 0;
    private float cellSize;
    private bool isProcessing = false;
    private bool isBossTransitioning = false;
    private bool isGameOver = false;

    private const int GAUGE_FOR_BULLET = 20;
    private const int GAUGE_FOR_FEVER = 40;
    private const int FEVER_BASE_TURNS = 10;
    private const int MAX_FEVER_TURNS = 10;

    private int mergeGauge = 0;
    private bool hasBullet = false;
    private bool isFeverMode = false;
    private int feverTurnsRemaining = 0;
    private int feverAtkBonus = 0;
    private int feverMergeAtkBonus = 0;
    private long FeverMergeIncreaseAtk = 1;
    private long permanentAttackPower = 0;
    private bool isGunMode = false;
    private bool feverBulletUsed = false;

    private float turnsTextOriginalY = 0f;
    private bool turnsTextInitialized = false;
    private float attackTextOriginalY = 0f;
    private bool attackTextInitialized = false;

    private long lastPermanentAttackPower = 0;
    private int lastMergeGauge = 0;
    private int lastFeverTurnsRemaining = 0;

    private Tweener gunButtonHeartbeat;

    private int currentHeat = 100;
    private const float COMBO_MULTIPLIER_BASE = 1.4f;
    private int comboCount = 0;

    private ProjectileManager projectileManager;
    private Vector3 lastMergedTilePosition;

    private int currentTurn = 0;

    private float heatTextOriginalY = 0f;
    private bool heatTextInitialized = false;
    private int lastCurrentHeat = 0;
    
    private bool justEndedFeverWithoutShot = false;

    // ⭐ v5.0: 무한대 보스 전용 변수
    private int infiniteBossMoveCount = 0;

    // ⭐ v5.1: 가이드 텍스트 상태 추적
    private bool isShowingFeverGuide = false;
    private bool isShowingLowHPGuide = false;

    void Start()
    {
        string bestScoreStr = PlayerPrefs.GetString("BestScore", "0");
        if (long.TryParse(bestScoreStr, out long parsedScore))
        {
            bestScore = parsedScore;
        }
        else
        {
            bestScore = 0;
        }

        projectileManager = FindAnyObjectByType<ProjectileManager>();

        if (heatSlider != null)
        {
            heatSlider.minValue = 0;
            heatSlider.maxValue = maxHeat;
            heatSlider.value = maxHeat;
        }

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

        if (damageFlashImage != null)
        {
            damageFlashImage.color = new Color(damageFlashImage.color.r, damageFlashImage.color.g, damageFlashImage.color.b, 0f);
            damageFlashImage.gameObject.SetActive(false);
        }

        InitializeGrid();
        StartGame();

        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);

        if (continueButton != null)
            continueButton.onClick.AddListener(ContinueGame);

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);

        if (gunButton != null)
            gunButton.onClick.AddListener(ToggleGunMode);

        UpdateGunUI();
        UpdateTurnUI();
    }

    void Update()
    {
        if (isGameOver || isProcessing || isBossTransitioning || isBossAttacking) return;

        if (!isGunMode)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                Move(Vector2Int.down);
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                Move(Vector2Int.up);
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                Move(Vector2Int.left);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                Move(Vector2Int.right);
        }

        if (isGunMode && Input.GetMouseButtonDown(0))
        {
            ShootTile();
        }
    }

    void InitializeGrid()
    {
        tiles = new Tile[gridSize, gridSize];

        float gridWidth = gridContainer.rect.width;
        cellSize = (gridWidth - cellSpacing * (gridSize + 1)) / gridSize;

        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                GameObject cell = Instantiate(cellPrefab, gridContainer);
                RectTransform cellRect = cell.GetComponent<RectTransform>();
                cellRect.sizeDelta = new Vector2(cellSize, cellSize);
                cellRect.anchoredPosition = GetCellPosition(x, y);
            }
        }
    }

    void StartGame()
    {
        score = 0;
        mergeGauge = 0;
        hasBullet = false;
        isFeverMode = false;
        feverTurnsRemaining = 0;
        feverAtkBonus = 0;
        feverMergeAtkBonus = 0;
        FeverMergeIncreaseAtk = 1;
        permanentAttackPower = 0;
        feverBulletUsed = false;
        currentHeat = maxHeat;
        isGunMode = false;
        isBossTransitioning = false;
        isGameOver = false;
        currentTurn = 0;
        infiniteBossMoveCount = 0; // ⭐ v5.0

        if (gunButtonHeartbeat != null)
        {
            gunButtonHeartbeat.Kill();
            gunButtonHeartbeat = null;
        }

        if (gunGuideAnimation != null)
        {
            gunGuideAnimation.Kill();
            gunGuideAnimation = null;
        }
        if (gunModeGuideText != null)
        {
            gunModeGuideText.gameObject.SetActive(false);
        }

        if (freezeImage1 != null) freezeImage1.gameObject.SetActive(false);
        if (freezeImage2 != null) freezeImage2.gameObject.SetActive(false);

        UpdateScoreUI();
        UpdateGunUI();
        UpdateHeatUI(true);
        UpdateTurnUI();
        SpawnTile();
        SpawnTile();
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    public void RestartGame()
    {
        isGameOver = false;
        isProcessing = false;
        isBossTransitioning = false;

        if (bossManager != null)
            bossManager.ResetBoss();

        foreach (var tile in activeTiles)
        {
            if (tile != null)
                Destroy(tile.gameObject);
        }
        activeTiles.Clear();
        tiles = new Tile[gridSize, gridSize];

        maxHeat = 100;
        permanentAttackPower = 0;
        feverAtkBonus = 0;
        feverMergeAtkBonus = 0;
        FeverMergeIncreaseAtk = 1;
        infiniteBossMoveCount = 0;

        // ⭐ v5.1: 비네트 보너스 리셋
        if (lowHealthVignette != null)
        {
            lowHealthVignette.ResetInfiniteBossBonus();
        }
        isShowingFeverGuide = false;
        isShowingLowHPGuide = false;

        StartGame();
    }

    void ContinueGame()
    {
        if (!isGameOver) return;

        isGameOver = false;
        isProcessing = false;

        currentHeat = maxHeat;
        UpdateHeatUI(true);

        isFeverMode = true;
        feverTurnsRemaining = 10;
        feverBulletUsed = false;
        mergeGauge = 0;
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
        }

        // ⭐ v5.0: Fever 시작 시 얼음 레이저 연출
        FireFeverFreezeLaser();

        UpdateGunUI();

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        // ⭐ v5.1: Continue로 Fever 진입 시 가이드 표시
        UpdateGuideText();

        Debug.Log("🎮 CONTINUE! 체력 전부 회복 + 피버 10턴 진입!");
    }

    void QuitGame()
    {
        Debug.Log("게임 종료");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ⭐ v5.0: Fever 시작 시 GunButton → Enemy로 얼음색 레이저 발사
    void FireFeverFreezeLaser()
    {
        if (projectileManager == null || gunButton == null || bossManager == null || bossManager.bossImageArea == null) return;

        // ⭐ v5.0: monsterImage의 RectTransform에서 world position 확실히 가져오기
        Vector3 startPos = gunButton.transform.position;
        RectTransform monsterRect = bossManager.bossImageArea.GetComponent<RectTransform>();
        Vector3 targetPos = monsterRect.position; // world position

        Color iceColor = new Color(0.5f, 0.85f, 1f, 0.9f); // 얼음색
        projectileManager.FireFreezeLaser(startPos, targetPos, iceColor, null);
        Debug.Log("🧊 Fever Freeze Laser 발사! GunButton → Enemy");
    }

    void CheckGaugeAndFever()
    {
        if (isFeverMode)
        {
            if (feverTurnsRemaining <= 0)
            {
                // Fever 종료
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
                {
                    bossManager.SetFrozen(false);
                }

                isFeverMode = false;

                if (feverBulletUsed)
                {
                    mergeGauge = 0;
                    hasBullet = false;
                    justEndedFeverWithoutShot = false;
                    Debug.Log("FEVER END! Shot used, reset to 0/40");
                }
                else
                {
                    mergeGauge = 20;
                    hasBullet = true;
                    justEndedFeverWithoutShot = true;
                    Debug.Log("FEVER END! No shot, keep 20/40 - PAYBACK!");
                }
                feverBulletUsed = false;

                // ⭐ v5.1: Fever 종료 후 가이드 텍스트 업데이트 (LowHP 표시 등)
                isShowingFeverGuide = false;
                UpdateGuideText();
            }
        }
        else
        {
            if (mergeGauge >= GAUGE_FOR_FEVER)
            {
                // Fever 시작
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
                }

                // ⭐ v5.0: Fever 시작 시 얼음 레이저 연출
                FireFeverFreezeLaser();

                isFeverMode = true;
                feverBulletUsed = false;
                feverTurnsRemaining = FEVER_BASE_TURNS;
                hasBullet = false;
                Debug.Log($"FEVER MODE! {FEVER_BASE_TURNS} turns granted!");
                UpdateGunButtonAnimation();

                feverAtkBonus++;
                Debug.Log($"🔥 FEVER 진입! Fever ATK Bonus +1 (Total: {feverAtkBonus})");

                FeverMergeIncreaseAtk++;
                Debug.Log($"🔥 FEVER 진입! Fever 머지 증가량 +1 (Now: {FeverMergeIncreaseAtk})");

                // ⭐ v5.1: Fever 시작 시 가이드 텍스트 표시
                UpdateGuideText();
            }
            else if (mergeGauge >= GAUGE_FOR_BULLET && !hasBullet)
            {
                hasBullet = true;
                Debug.Log($"Bullet ready! ({mergeGauge}/40)");
                UpdateGunButtonAnimation();
            }
        }
        UpdateGunUI();
    }

    void SpawnFeverParticle()
    {
        if (feverParticleSpawnPoint == null)
        {
            Debug.LogWarning("Fever particle spawn point not set!");
            return;
        }

        if (activeFeverParticle != null)
        {
            Destroy(activeFeverParticle);
        }

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
        renderer.sortingOrder = 1; // ⭐ v5.0: 레이어 낮추기 (5→1), 가이드 아래로

        var uiParticle = particleObj.AddComponent<Coffee.UIExtensions.UIParticle>();
        uiParticle.scale = 2f;

        activeFeverParticle = particleObj;

        Debug.Log("Fever flame particle spawned!");
    }

    void ToggleGunMode()
    {
        if (isBossAttacking)
        {
            Debug.Log("보스 공격 중에는 Gun Mode 전환 불가!");
            return;
        }

        if (isGunMode)
        {
            isGunMode = false;

            if (gunGuideAnimation != null)
            {
                gunGuideAnimation.Kill();
                gunGuideAnimation = null;
            }
            if (gunModeGuideText != null)
            {
                gunModeGuideText.transform.localScale = Vector3.one;
                gunModeGuideText.gameObject.SetActive(false);
            }

            // ⭐ v5.0: Gun 모드 종료 시 모든 타일 테두리 + 초음파 효과 제거
            foreach (var tile in activeTiles)
            {
                if (tile != null)
                {
                    tile.SetProtected(false, false);
                }
            }

            // ⭐ v5.1: Gun 모드 종료 후 Fever/LowHP 가이드 복원
            UpdateGuideText();
            UpdateGunUI();
            return;
        }

        if (!hasBullet && (!isFeverMode || feverBulletUsed)) return;

        if (activeTiles.Count <= 2)
        {
            Debug.Log("타일이 2개 이하일 때는 총을 쓸 수 없습니다!");
            return;
        }

        isGunMode = true;

        if (gunModeGuideText != null)
        {
            gunModeGuideText.gameObject.SetActive(true);
            
            if (isFeverMode)
            {
                // ⭐ v5.1: Fever Gun Mode 텍스트
                gunModeGuideText.text = "Tap Glowing Tile\nto Blast & Heal!\nFever bonus\n3 Turn Delay!";
            }
            else
            {
                gunModeGuideText.text = "Tap Glowing Tile\nto Blast & Heal!";
            }
            isShowingFeverGuide = false; // gun mode에서는 fever guide 상태 해제
            isShowingLowHPGuide = false;

            if (gunGuideAnimation != null)
            {
                gunGuideAnimation.Kill();
            }
            gunModeGuideText.transform.localScale = Vector3.one;

            gunGuideAnimation = gunModeGuideText.transform.DOScale(1.1f, 0.6f)
                .SetEase(Ease.InOutQuad)
                .SetLoops(-1, LoopType.Yoyo);
        }

        // Gun 모드 진입 시 타일 테두리 표시
        UpdateTileBorders();

        UpdateGunUI();
    }

    void ShootTile()
    {
        if (!hasBullet && (!isFeverMode || feverBulletUsed))
        {
            isGunMode = false;

            if (gunGuideAnimation != null)
            {
                gunGuideAnimation.Kill();
                gunGuideAnimation = null;
            }
            if (gunModeGuideText != null)
            {
                gunModeGuideText.transform.localScale = Vector3.one;
                gunModeGuideText.gameObject.SetActive(false);
            }

            foreach (var tile in activeTiles)
            {
                if (tile != null)
                {
                    tile.SetProtected(false, false);
                }
            }

            UpdateGunUI();
            return;
        }

        var topTwoValues = GetTopTwoTileValues();
        if (activeTiles.Count <= 2 || (topTwoValues.Item1 == 0 && topTwoValues.Item2 == 0))
        {
            Debug.Log("타일이 2개 이하이거나 보호된 타일만 남았습니다!");
            isGunMode = false;

            if (gunGuideAnimation != null)
            {
                gunGuideAnimation.Kill();
                gunGuideAnimation = null;
            }
            if (gunModeGuideText != null)
            {
                gunModeGuideText.transform.localScale = Vector3.one;
                gunModeGuideText.gameObject.SetActive(false);
            }

            foreach (var tile in activeTiles)
            {
                if (tile != null)
                {
                    tile.SetProtected(false, false);
                }
            }

            UpdateGunUI();
            return;
        }

        Canvas canvas = gridContainer.GetComponentInParent<Canvas>();
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            gridContainer,
            Input.mousePosition,
            cam,
            out localPoint
        );

        Tile targetTile = null;
        float minDistance = cellSize / 2;

        foreach (var tile in activeTiles)
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
            var currentTopTwo = GetTopTwoTileValues();
            
            if (targetTile.value == currentTopTwo.Item1 || targetTile.value == currentTopTwo.Item2)
            {
                Debug.Log($"❌ 가장 큰 값 타일({targetTile.value})은 부술 수 없습니다! Top2: {currentTopTwo.Item1}, {currentTopTwo.Item2}");
                return;
            }

            int oldHeat = currentHeat;
            currentHeat = maxHeat;
            UpdateHeatUI(false);
            
            int recovery = currentHeat - oldHeat;
            if (recovery > 0)
            {
                ShowHeatChangeText(recovery);
            }
            
            Debug.Log("💚 총 발사! 체력 전부 회복!");

            Vector3 tilePos = targetTile.transform.position;
            Vector2Int pos = targetTile.gridPosition;

            targetTile.PlayGunDestroyEffect();

            tiles[pos.x, pos.y] = null;
            activeTiles.Remove(targetTile);
            Destroy(targetTile.gameObject);

            if (isFeverMode)
            {
                feverBulletUsed = true;
                mergeGauge = 0;
                hasBullet = false;
                Debug.Log("FEVER SHOT! Bullet used, cannot shoot again");

                if (bossManager != null)
                {
                    bossManager.AddTurns(3);
                    Debug.Log("🔥 FEVER SHOT! 보스 공격 턴 +3");
                }
                feverAtkBonus++;
                Debug.Log($"🔥 FEVER ATK BONUS +1! (Total: {feverAtkBonus})");

                FeverMergeIncreaseAtk++;
                Debug.Log($"🔥 FEVER GUN! Fever 머지 증가량 +1 (Now: {FeverMergeIncreaseAtk})");
            }
            else
            {
                mergeGauge = Mathf.Max(0, mergeGauge - GAUGE_FOR_BULLET);
                hasBullet = (mergeGauge >= GAUGE_FOR_BULLET);
                Debug.Log($"GUN SHOT! Remaining charge: {mergeGauge}/40");
            }

            isGunMode = false;

            if (gunGuideAnimation != null)
            {
                gunGuideAnimation.Kill();
                gunGuideAnimation = null;
            }
            if (gunModeGuideText != null)
            {
                gunModeGuideText.transform.localScale = Vector3.one;
                gunModeGuideText.gameObject.SetActive(false);
            }

            // ⭐ v5.0: 총 발사 후 모든 테두리 + 초음파 효과 제거
            foreach (var tile in activeTiles)
            {
                if (tile != null)
                {
                    tile.SetProtected(false, false);
                }
            }

            UpdateGunUI();

            if (!CanMove() && !hasBullet && !isFeverMode)
            {
                GameOver();
            }
        }
    }

    long GetAllTilesSum()
    {
        long sum = 0;
        foreach (var tile in activeTiles)
        {
            if (tile != null)
            {
                sum += tile.value;
            }
        }
        return sum;
    }

    void ShowHeatChangeText(int change, string bonusText = "")
    {
        if (damageTextPrefab == null || damageTextParent == null || heatText == null) return;

        GameObject heatChangeObj = Instantiate(damageTextPrefab, damageTextParent);
        TextMeshProUGUI heatChangeText = heatChangeObj.GetComponent<TextMeshProUGUI>();

        if (heatChangeText != null)
        {
            if (change > 0)
            {
                if (!string.IsNullOrEmpty(bonusText))
                {
                    heatChangeText.text = $"{bonusText}\n+{change}";
                    heatChangeText.alignment = TextAlignmentOptions.Center;
                }
                else
                {
                    heatChangeText.text = "+" + change;
                }
                heatChangeText.color = new Color(0.3f, 1f, 0.3f);
            }
            else
            {
                heatChangeText.text = change.ToString();
                heatChangeText.color = new Color(0.5f, 0.8f, 1f);
            }

            heatChangeText.fontSize = 40;

            RectTransform heatChangeRect = heatChangeObj.GetComponent<RectTransform>();
            RectTransform heatTextRect = heatText.GetComponent<RectTransform>();

            heatChangeRect.position = heatTextRect.position;

            CanvasGroup canvasGroup = heatChangeObj.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = heatChangeObj.AddComponent<CanvasGroup>();

            Sequence heatSequence = DOTween.Sequence();

            heatSequence.Append(heatChangeRect.DOAnchorPosY(heatChangeRect.anchoredPosition.y + 100f, 1.0f).SetEase(Ease.OutCubic));
            heatSequence.Join(canvasGroup.DOFade(0f, 1.0f).SetEase(Ease.InCubic));

            heatSequence.Insert(0f, heatChangeRect.DOScale(1.2f, 0.15f).SetEase(Ease.OutQuad));
            heatSequence.Insert(0.15f, heatChangeRect.DOScale(1f, 0.15f).SetEase(Ease.InQuad));

            heatSequence.OnComplete(() => {
                if (heatChangeObj != null) Destroy(heatChangeObj);
            });
        }
    }

    void UpdateGunUI()
    {
        if (bulletCountText != null)
        {
            if (isGunMode)
            {
                bulletCountText.text = "CANCEL";
            }
            else if (isFeverMode)
            {
                bulletCountText.text = "FEVER";
            }
            else if (hasBullet)
            {
                bulletCountText.text = "CHARGE";
            }
            else
            {
                bulletCountText.text = "RELOAD";
            }
        }

        if (turnsUntilBulletText != null)
        {
            if (!turnsTextInitialized)
            {
                RectTransform textRect = turnsUntilBulletText.GetComponent<RectTransform>();
                turnsTextOriginalY = textRect.anchoredPosition.y;
                turnsTextInitialized = true;
            }

            int currentValue = isFeverMode ? feverTurnsRemaining : mergeGauge;
            int lastValue = isFeverMode ? lastFeverTurnsRemaining : lastMergeGauge;

            if (isFeverMode)
            {
                if (comboCount >= 2)
                {
                    turnsUntilBulletText.text = $"Remain {feverTurnsRemaining} COMBO!";
                }
                else
                {
                    turnsUntilBulletText.text = $"Remain {feverTurnsRemaining}";
                }
            }
            else
            {
                if (justEndedFeverWithoutShot && mergeGauge == 20)
                {
                    turnsUntilBulletText.text = "20/40 Fever Payback!";
                }
                else if (mergeGauge == 0)
                {
                    turnsUntilBulletText.text = "0/40";
                }
                else
                {
                    turnsUntilBulletText.text = $"{mergeGauge}/40";
                }
            }

            if (currentValue != lastValue)
            {
                if (isFeverMode)
                    lastFeverTurnsRemaining = feverTurnsRemaining;
                else
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

        if (progressBarFill != null)
        {
            float progress = isFeverMode ?
                Mathf.Clamp01((float)feverTurnsRemaining / FEVER_BASE_TURNS) :
                Mathf.Clamp01((float)mergeGauge / GAUGE_FOR_FEVER);

            float targetWidth = progressBarFill.parent.GetComponent<RectTransform>().rect.width * progress;

            progressBarFill.DOKill();
            progressBarFill.DOSizeDelta(
                new Vector2(targetWidth, progressBarFill.sizeDelta.y),
                0.3f
            ).SetEase(Ease.OutQuad);
        }

        if (gunButtonImage != null)
        {
            if (isGunMode)
                gunButtonImage.color = Color.red;
            else if (isFeverMode)
                gunButtonImage.color = new Color(1f, 0.3f, 0f);
            else if (hasBullet)
            {
                // ⭐ v5.0: mergeGauge 20이상일 때 파스텔 민트색
                if (mergeGauge >= 20)
                    gunButtonImage.color = new Color(0.6f, 0.95f, 0.85f); // 파스텔 민트
                else
                    gunButtonImage.color = new Color(0.6f, 0.95f, 0.85f); // hasBullet이면 항상 20이상
            }
            else
            {
                gunButtonImage.color = new Color(0.5f, 0.5f, 0.5f);
            }
        }

        if (gunButton != null)
        {
            gunButton.interactable = !isGameOver && !isBossTransitioning && (hasBullet || (isFeverMode && !feverBulletUsed)) && activeTiles.Count > 1;
        }

        bool shouldAnimate = hasBullet || (isFeverMode && !feverBulletUsed);
        UpdateGunButtonAnimationIfNeeded(shouldAnimate);
    }

    System.Collections.IEnumerator FlashOrangeOnDamage()
    {
        if (heatBarImage == null || heatText == null) yield break;

        Color originalBarColor = heatBarImage.color;
        Color originalTextColor = heatText.color;

        Color orangeColor = new Color(1f, 0.65f, 0f);
        heatBarImage.color = orangeColor;
        heatText.color = orangeColor;

        yield return new WaitForSeconds(0.15f);

        heatBarImage.color = originalBarColor;
        heatText.color = originalTextColor;
    }

    private bool lastGunButtonAnimationState = false;

    void UpdateGunButtonAnimationIfNeeded(bool shouldAnimate)
    {
        bool currentState = isGunMode || shouldAnimate;
        if (currentState == lastGunButtonAnimationState && gunButtonHeartbeat != null)
        {
            return;
        }

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
        else
        {
            gunButton.transform.localScale = Vector3.one;
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
        else
        {
            gunButton.transform.localScale = Vector3.one;
        }
    }

    void UpdateHeatUI(bool instant = false)
    {
        if (heatText != null)
        {
            heatText.text = $"HP : {currentHeat}/{maxHeat}";

            if (!heatTextInitialized)
            {
                RectTransform textRect = heatText.GetComponent<RectTransform>();
                heatTextOriginalY = textRect.anchoredPosition.y;
                heatTextInitialized = true;
            }

            if (currentHeat > lastCurrentHeat)
            {
                RectTransform textRect = heatText.GetComponent<RectTransform>();
                textRect.DOKill();

                Sequence seq = DOTween.Sequence();
                seq.Append(textRect.DOAnchorPosY(heatTextOriginalY + 12f, 0.2f).SetEase(Ease.OutQuad));
                seq.Append(textRect.DOAnchorPosY(heatTextOriginalY, 0.2f).SetEase(Ease.InQuad));
                seq.OnComplete(() => {
                    if (textRect != null)
                        textRect.anchoredPosition = new Vector2(textRect.anchoredPosition.x, heatTextOriginalY);
                });
            }

            lastCurrentHeat = currentHeat;

            float heatPercent = (float)currentHeat / maxHeat;
            Color heatColor;

            if (heatPercent <= 0.2f)
            {
                heatColor = new Color(1f, 0.6f, 0.7f);
            }
            else if (heatPercent <= 0.4f)
            {
                heatColor = new Color(1f, 0.5f, 0.65f);
            }
            else if (heatPercent <= 0.6f)
            {
                heatColor = new Color(1f, 0.4f, 0.6f);
            }
            else
            {
                heatColor = new Color(1f, 0.3f, 0.55f);
            }

            heatText.color = heatColor;

            if (heatBarImage != null)
            {
                heatBarImage.color = heatColor;
            }
        }

        if (heatSlider != null)
        {
            heatSlider.maxValue = maxHeat;

            heatSlider.DOKill();

            if (instant)
            {
                heatSlider.value = currentHeat;
            }
            else
            {
                heatSlider.DOValue(currentHeat, heatAnimationDuration)
                    .SetEase(Ease.OutCubic);
            }
        }

        if (lowHealthVignette != null)
        {
            if (instant)
            {
                lowHealthVignette.UpdateVignetteInstant(currentHeat, maxHeat);
            }
            else
            {
                lowHealthVignette.UpdateVignette(currentHeat, maxHeat);
            }
        }
    }

    void RecoverHeat(int amount)
    {
        int oldHeat = currentHeat;
        currentHeat += amount;
        if (currentHeat > maxHeat)
            currentHeat = maxHeat;

        int actualRecovery = currentHeat - oldHeat;

        UpdateHeatUI();

        if (actualRecovery != 0)
        {
            ShowHeatChangeText(actualRecovery);
        }

        Debug.Log($"히트 회복: +{amount} (Current: {currentHeat}/{maxHeat})");
    }

    void SpawnTile()
    {
        List<Vector2Int> emptyPositions = new List<Vector2Int>();

        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                if (tiles[x, y] == null)
                    emptyPositions.Add(new Vector2Int(x, y));
            }
        }

        if (emptyPositions.Count == 0) return;

        Vector2Int pos = emptyPositions[Random.Range(0, emptyPositions.Count)];
        int value = Random.value < 0.9f ? 2 : 4;

        GameObject tileObj = Instantiate(tilePrefab, gridContainer);
        Tile tile = tileObj.GetComponent<Tile>();
        RectTransform tileRect = tileObj.GetComponent<RectTransform>();

        tileRect.sizeDelta = new Vector2(cellSize, cellSize);
        tile.SetValue(value);

        TileColor randomColor = Random.value < 0.5f ? TileColor.Choco : TileColor.Berry;
        tile.SetColor(randomColor);

        tile.SetGridPosition(pos);
        tile.MoveTo(GetCellPosition(pos.x, pos.y), false);

        tiles[pos.x, pos.y] = tile;
        activeTiles.Add(tile);

        tileObj.transform.localScale = Vector3.zero;
        StartCoroutine(ScaleInAnimation(tileObj));
        if (isGunMode)
        {
            UpdateTileBorders();
        }
    }

    System.Collections.IEnumerator ScaleInAnimation(GameObject obj)
    {
        float duration = 0.2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (obj == null) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float s = 1.70158f;
            t = t - 1;
            float val = t * t * ((s + 1) * t + s) + 1;

            if (obj != null)
                obj.transform.localScale = Vector3.one * val;

            yield return null;
        }

        if (obj != null)
            obj.transform.localScale = Vector3.one;
    }

    void Move(Vector2Int direction)
    {
        StartCoroutine(MoveCoroutine(direction));
    }

    System.Collections.IEnumerator MoveCoroutine(Vector2Int direction)
    {
        isProcessing = true;
        bool moved = false;
        int totalMergedValue = 0;
        int mergeCountThisTurn = 0;

        int chocoMergeCount = 0;
        bool hadChocoMerge = false;
        int berryMergeCount = 0;
        bool hadBerryMerge = false;

        int oldHeat = currentHeat;

        bool anyMerged = true;
        while (anyMerged)
        {
            anyMerged = false;

            int startX = direction.x == 1 ? gridSize - 1 : 0;
            int startY = direction.y == 1 ? gridSize - 1 : 0;
            int dirX = direction.x != 0 ? -direction.x : 0;
            int dirY = direction.y != 0 ? -direction.y : 0;

            Tile[,] newTiles = new Tile[gridSize, gridSize];
            bool[,] merged = new bool[gridSize, gridSize];

            for (int i = 0; i < gridSize; i++)
            {
                for (int j = 0; j < gridSize; j++)
                {
                    int x = startX + (dirX == 0 ? j : i * dirX);
                    int y = startY + (dirY == 0 ? j : i * dirY);

                    if (tiles[x, y] == null) continue;

                    Tile tile = tiles[x, y];
                    Vector2Int targetPos = new Vector2Int(x, y);

                    while (true)
                    {
                        Vector2Int nextPos = targetPos + direction;

                        if (nextPos.x < 0 || nextPos.x >= gridSize || nextPos.y < 0 || nextPos.y >= gridSize)
                            break;

                        if (newTiles[nextPos.x, nextPos.y] == null)
                        {
                            targetPos = nextPos;
                        }
                        else if (newTiles[nextPos.x, nextPos.y].value == tile.value && !merged[nextPos.x, nextPos.y])
                        {
                            Tile targetTile = newTiles[nextPos.x, nextPos.y];
                            int mergedValue = tile.value * 2;
                            score += mergedValue;
                            totalMergedValue += mergedValue;

                            TileColor color1 = tile.tileColor;
                            TileColor color2 = targetTile.tileColor;

                            bool isColorBonus = false;

                            if (color1 == TileColor.Choco && color2 == TileColor.Choco)
                            {
                                chocoMergeCount++;
                                hadChocoMerge = true;

                                int bonusDamage = mergedValue * (chocoMergeDamageMultiplier - 1);
                                totalMergedValue += bonusDamage;

                                if (!isFeverMode)
                                {
                                    mergeGauge++;
                                }

                                Debug.Log($"CHOCO MERGE! Gauge +1 ({mergeGauge}/40)");
                                targetTile.PlayChocoMergeEffect();
                                isColorBonus = true;
                            }
                            else if (color1 == TileColor.Berry && color2 == TileColor.Berry)
                            {
                                berryMergeCount++;
                                hadBerryMerge = true;

                                int bonusHeal = berryMergeBaseHeal * berryMergeHealMultiplier;
                                currentHeat += bonusHeal;
                                if (currentHeat > maxHeat) currentHeat = maxHeat;

                                if (projectileManager != null && heatText != null)
                                {
                                    Vector3 berryPos = targetTile.transform.position;
                                    Vector3 heatUIPos = heatText.transform.position;
                                    Color berryColor = new Color(1f, 0.4f, 0.6f);

                                    projectileManager.FireKnifeProjectile(berryPos, heatUIPos, berryColor, null);
                                }

                                if (!isFeverMode)
                                {
                                    mergeGauge++;
                                }

                                Debug.Log($"BERRY MERGE! Gauge +1 ({mergeGauge}/40)");
                                targetTile.PlayBerryMergeEffect();
                                isColorBonus = true;
                            }
                            else
                            {
                                if (!isFeverMode)
                                {
                                    mergeGauge += 2;
                                }

                                score += mergedValue;
                                Debug.Log($"MIX MERGE! Gauge +2 ({mergeGauge}/40)");
                            }

                            if (isColorBonus)
                            {
                                targetTile.MergeWithoutParticle();
                            }
                            else
                            {
                                targetTile.MergeWith(tile);
                                targetTile.PlayMixMergeEffect();
                            }

                            TileColor newColor = Random.value < 0.5f ? TileColor.Choco : TileColor.Berry;
                            targetTile.SetColor(newColor);

                            merged[nextPos.x, nextPos.y] = true;
                            anyMerged = true;

                            lastMergedTilePosition = targetTile.transform.position;

                            mergeCountThisTurn++;

                            if (isFeverMode)
                            {
                                permanentAttackPower += FeverMergeIncreaseAtk;
                                Debug.Log($"🔥 FEVER MERGE! +ATK +{FeverMergeIncreaseAtk} (Total: {permanentAttackPower})");
                            }

                            activeTiles.Remove(tile);
                            Destroy(tile.gameObject);
                            tile = null;
                            moved = true;
                            break;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (tile != null)
                    {
                        if (targetPos != new Vector2Int(x, y))
                            moved = true;

                        tile.SetGridPosition(targetPos);
                        tile.MoveTo(GetCellPosition(targetPos.x, targetPos.y));
                        newTiles[targetPos.x, targetPos.y] = tile;
                    }
                }
            }

            tiles = newTiles;

            if (anyMerged)
            {
                yield return new WaitForSeconds(0.15f);
            }
        }

        if (moved)
        {
            currentTurn++;
            UpdateTurnUI();

            // ⭐ v5.0: 무한대 보스(stage 40)에서 20회 이동마다 공격력 증가
            if (bossManager != null && bossManager.IsInfiniteBoss())
            {
                infiniteBossMoveCount++;
                if (infiniteBossMoveCount % 20 == 0)
                {
                    bossManager.IncreaseInfiniteBossDamage();

                    // ⭐ v5.1: 비네트 효과도 같이 증가
                    if (lowHealthVignette != null)
                    {
                        lowHealthVignette.IncreaseInfiniteBossBonus();
                        lowHealthVignette.UpdateVignette(currentHeat, maxHeat);
                        UpdateGuideText(); // LowHP 가이드 업데이트
                    }

                    Debug.Log($"⚠️ 무한대 보스: {infiniteBossMoveCount}회 이동! 공격력 + 비네트 증가!");
                }
            }

            comboCount = mergeCountThisTurn;

            if (totalMergedValue > 0 && bossManager != null)
            {
                float comboMultiplier = 1.0f;
                if (mergeCountThisTurn > 1)
                {
                    comboMultiplier = Mathf.Pow(COMBO_MULTIPLIER_BASE, mergeCountThisTurn - 1);
                }

                long baseDamage = (long)Mathf.Floor(totalMergedValue * comboMultiplier);

                if (hadChocoMerge && permanentAttackPower > 0)
                {
                    baseDamage += permanentAttackPower * 2;
                    Debug.Log($"🍫 CHOCO MERGE! 추가 ATK 2배 적용: +{permanentAttackPower * 2}");
                }
                else
                {
                    baseDamage += permanentAttackPower;
                }

                if (isFeverMode)
                {
                    baseDamage = (long)(baseDamage * feverDamageMultiplier);
                }

                if (isFeverMode && feverMergeAtkBonus > 0)
                {
                    baseDamage += feverMergeAtkBonus;
                    Debug.Log($"🔥 FEVER MERGE! 공격력 +{feverMergeAtkBonus}");
                }

                if (isFeverMode && feverAtkBonus > 0)
                {
                    float bonusMultiplier = 1.0f + (feverAtkBonus * 0.1f);
                    baseDamage = (long)(baseDamage * bonusMultiplier);
                    Debug.Log($"🔥 FEVER ATK BONUS x{bonusMultiplier:F1}!");
                }

                long damage = baseDamage;

                // ⭐ v5.0: monsterImage RectTransform에서 world position 확실히 가져오기
                if (projectileManager != null && bossManager != null && bossManager.bossImageArea != null)
                {
                    RectTransform monsterRect = bossManager.bossImageArea.GetComponent<RectTransform>();
                    Vector3 bossPos = monsterRect.position; // ⭐ RectTransform.position 사용

                    Color laserColor = Color.white;
                    if (isFeverMode)
                    {
                        laserColor = new Color(1f, 0.5f, 0f);
                    }
                    else if (mergeCountThisTurn >= 2)
                    {
                        if (mergeCountThisTurn >= 5)
                            laserColor = new Color(1f, 0f, 1f);
                        else if (mergeCountThisTurn >= 4)
                            laserColor = new Color(1f, 0.3f, 0f);
                        else if (mergeCountThisTurn >= 3)
                            laserColor = new Color(1f, 0.6f, 0f);
                        else if (mergeCountThisTurn >= 2)
                            laserColor = new Color(0.5f, 1f, 0.5f);
                    }

                    projectileManager.FireKnifeProjectile(lastMergedTilePosition, bossPos, laserColor, () =>
                    {
                        bossManager.TakeDamage(damage);
                        ShowDamageText(damage, mergeCountThisTurn, false);
                        CameraShake.Instance?.ShakeLight();
                    });
                }
                else
                {
                    bossManager.TakeDamage(damage);
                    ShowDamageText(damage, mergeCountThisTurn, false);
                }
            }

            if (mergeCountThisTurn > 0)
            {
                int comboIndex = Mathf.Min(mergeCountThisTurn, comboHeatRecover.Length - 1);
                int heatRecovery = comboHeatRecover[comboIndex];
                if (hadBerryMerge)
                {
                    heatRecovery *= 2;
                    Debug.Log($"BERRY MERGE BONUS! Heat recovery x2: {heatRecovery}");
                }
                currentHeat += heatRecovery;
            }

            if (currentHeat > maxHeat)
                currentHeat = maxHeat;
            if (currentHeat < 0)
                currentHeat = 0;

            int netChange = currentHeat - oldHeat;

            UpdateHeatUI();

            if (netChange != 0)
            {
                ShowHeatChangeText(netChange);
            }

            if (!isFeverMode && mergeCountThisTurn >= 2)
            {
                int gaugeIncrease = 1;
                mergeGauge += gaugeIncrease;
                
                if (justEndedFeverWithoutShot && mergeGauge > 20)
                {
                    justEndedFeverWithoutShot = false;
                }
                
                Debug.Log($"🎯 {mergeCountThisTurn}콤보 달성! 게이지 +{gaugeIncrease} ({mergeGauge}/20)");
            }

            UpdateScoreUI();

            comboCount = mergeCountThisTurn;

            CheckGaugeAndFever();

            if (currentHeat <= 0)
            {
                Debug.Log("히트 고갈! 게임 오버");
                GameOver();
                yield break;
            }

            yield return new WaitForSeconds(0.2f);
            AfterMove();
        }
        else
        {
            isProcessing = false;
        }
    }

    void ShowDamageText(long damage, int comboNum, bool isGunDamage, bool isChoco = false)
    {
        if (damageTextPrefab == null || damageTextParent == null || hpText == null) return;

        GameObject damageObj = Instantiate(damageTextPrefab, damageTextParent);
        TextMeshProUGUI damageText = damageObj.GetComponent<TextMeshProUGUI>();

        if (damageText != null)
        {
            if (isGunDamage)
            {
                if (isChoco)
                {
                    damageText.text = $"-{damage}";
                    damageText.color = new Color(1f, 0.84f, 0f);
                }
                else
                {
                    damageText.text = $"-{damage}";
                    damageText.color = Color.yellow;
                }
                damageText.fontSize = 54;
            }
            else
            {
                if (comboNum >= 2)
                {
                    damageText.text = $"{comboNum} Combo!\n-{damage}";

                    if (comboNum >= 5)
                        damageText.color = new Color(1f, 0f, 1f);
                    else if (comboNum >= 4)
                        damageText.color = new Color(1f, 0.3f, 0f);
                    else if (comboNum >= 3)
                        damageText.color = new Color(1f, 0.6f, 0f);
                    else
                        damageText.color = new Color(0.5f, 1f, 0.5f);

                    damageText.fontSize = Mathf.Min(48 + comboNum * 2, 60);
                }
                else
                {
                    damageText.text = "-" + damage;
                    damageText.color = Color.white;
                    damageText.fontSize = 48;
                }
            }

            RectTransform damageRect = damageObj.GetComponent<RectTransform>();
            RectTransform hpTextRect = hpText.GetComponent<RectTransform>();

            damageRect.position = hpTextRect.position;

            CanvasGroup canvasGroup = damageObj.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = damageObj.AddComponent<CanvasGroup>();

            Sequence damageSequence = DOTween.Sequence();

            damageSequence.Append(damageRect.DOAnchorPosY(damageRect.anchoredPosition.y + 150f, 1.2f).SetEase(Ease.OutCubic));
            damageSequence.Join(canvasGroup.DOFade(0f, 1.2f).SetEase(Ease.InCubic));

            damageSequence.Insert(0f, damageRect.DOScale(1.2f, 0.15f).SetEase(Ease.OutQuad));
            damageSequence.Insert(0.15f, damageRect.DOScale(1f, 0.15f).SetEase(Ease.InQuad));

            damageSequence.OnComplete(() => {
                if (damageObj != null) Destroy(damageObj);
            });
        }
    }

    void AfterMove()
    {
        SpawnTile();

        if (isFeverMode && comboCount >= 2)
        {
            int extension = comboCount;
            feverTurnsRemaining += extension;

            if (feverTurnsRemaining > MAX_FEVER_TURNS)
                feverTurnsRemaining = MAX_FEVER_TURNS;

            Debug.Log($"FEVER EXTEND! +{extension} (Now: {feverTurnsRemaining})");
        }

        if (isFeverMode)
        {
            feverTurnsRemaining--;
            Debug.Log($"Fever turn -1: {feverTurnsRemaining} left");
        }

        CheckGaugeAndFever();

        // Fever 중이 아닐 때만 보스 턴 진행
        if (bossManager != null && !isFeverMode && !bossManager.IsFrozen())
        {
            bossManager.OnPlayerTurn();
        }

        if (!CanMove())
        {
            if (!isFeverMode || feverBulletUsed)
            {
                if (!hasBullet)
                {
                    GameOver();
                    return;
                }
            }
        }

        isProcessing = false;
        if (isGunMode)
        {
            UpdateTileBorders();
        }
    }

    bool CanMove()
    {
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                if (tiles[x, y] == null) return true;

                int currentValue = tiles[x, y].value;

                if (x < gridSize - 1)
                {
                    if (tiles[x + 1, y] == null || tiles[x + 1, y].value == currentValue)
                        return true;
                }

                if (y < gridSize - 1)
                {
                    if (tiles[x, y + 1] == null || tiles[x, y + 1].value == currentValue)
                        return true;
                }
            }
        }
        return false;
    }

    void GameOver()
    {
        isGameOver = true;
        Debug.Log("Game Over!");

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
        {
            bossManager.SetFrozen(false);
        }

        UpdateGunUI();

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);

            CanvasGroup canvasGroup = gameOverPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameOverPanel.AddComponent<CanvasGroup>();
            }

            canvasGroup.alpha = 0f;

            canvasGroup.DOFade(1f, 1f).SetDelay(2f).SetEase(Ease.InOutQuad);
        }
    }

    void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.text = score.ToString();

        if (score > bestScore)
        {
            bestScore = score;
            PlayerPrefs.SetString("BestScore", bestScore.ToString());
            PlayerPrefs.Save();
        }

        if (bestScoreText != null)
            bestScoreText.text = bestScore.ToString();
    }

    // ⭐ v5.1: 가이드 텍스트 통합 관리
    void UpdateGuideText()
    {
        if (gunModeGuideText == null) return;

        // Gun Mode 중이면 gun mode 전용 텍스트가 우선 (ToggleGunMode에서 직접 설정)
        if (isGunMode) return;

        if (isFeverMode)
        {
            // Fever 모드: Fever 가이드 표시
            if (!isShowingFeverGuide)
            {
                isShowingFeverGuide = true;
                isShowingLowHPGuide = false;
                gunModeGuideText.gameObject.SetActive(true);
                gunModeGuideText.text = $"Fever! Damage*{feverDamageMultiplier:F1}!\nmerge and get atk!!";

                if (gunGuideAnimation != null) gunGuideAnimation.Kill();
                gunModeGuideText.transform.localScale = Vector3.one;
                gunGuideAnimation = gunModeGuideText.transform.DOScale(1.1f, 0.6f)
                    .SetEase(Ease.InOutQuad)
                    .SetLoops(-1, LoopType.Yoyo);
            }
        }
        else
        {
            // Fever 종료
            if (isShowingFeverGuide)
            {
                isShowingFeverGuide = false;
            }

            // Low HP 체크 (비네트 효과 최대일 경우)
            bool shouldShowLowHP = !isFeverMode && lowHealthVignette != null && lowHealthVignette.IsVignetteAtMax(currentHeat);

            if (shouldShowLowHP)
            {
                if (!isShowingLowHPGuide)
                {
                    isShowingLowHPGuide = true;
                    gunModeGuideText.gameObject.SetActive(true);
                    gunModeGuideText.text = "Low HP!\nMerge 2 pink Block\nor use Gun or\nEnter Fever Mode!";

                    if (gunGuideAnimation != null) gunGuideAnimation.Kill();
                    gunModeGuideText.transform.localScale = Vector3.one;
                    gunGuideAnimation = gunModeGuideText.transform.DOScale(1.05f, 0.8f)
                        .SetEase(Ease.InOutQuad)
                        .SetLoops(-1, LoopType.Yoyo);
                }
            }
            else
            {
                // Low HP도 아니고 Fever도 아니면 가이드 숨기기
                if (isShowingLowHPGuide || isShowingFeverGuide)
                {
                    isShowingLowHPGuide = false;
                    isShowingFeverGuide = false;
                    if (gunGuideAnimation != null)
                    {
                        gunGuideAnimation.Kill();
                        gunGuideAnimation = null;
                    }
                    gunModeGuideText.transform.localScale = Vector3.one;
                    gunModeGuideText.gameObject.SetActive(false);
                }
                // 아무것도 표시하지 않는 상태일 때도 비활성화
                if (!gunModeGuideText.gameObject.activeSelf) { /* 이미 께져있음 */ }
                else if (!isShowingLowHPGuide && !isShowingFeverGuide)
                {
                    if (gunGuideAnimation != null)
                    {
                        gunGuideAnimation.Kill();
                        gunGuideAnimation = null;
                    }
                    gunModeGuideText.transform.localScale = Vector3.one;
                    gunModeGuideText.gameObject.SetActive(false);
                }
            }
        }
    }

    // ⭐ v5.0 UPDATED: Stage UI (stage 40 = infinite, stage 40 hpBar color)
    public void UpdateTurnUI()
    {
        if (turnText != null)
        {
            turnText.text = $"Turn: {currentTurn}";
        }

        if (stageText != null && bossManager != null)
        {
            int currentStage = bossManager.GetBossLevel();
            
            if (currentStage <= 40)
            {
                stageText.text = $"Stage {currentStage}/40";
            }
            else
            {
                stageText.text = "Endless";
            }
        }

        // ⭐ v5.0: 무한대 보스(stage 40)일 때 Enemy bar 밝은 붉은색
        if (bossManager != null && bossManager.IsInfiniteBoss())
        {
            UpdateInfiniteBossEnemyBarColor();
        }
    }

    // ⭐ v5.0: 무한대 보스 Enemy HP bar 색상 변경
    void UpdateInfiniteBossEnemyBarColor()
    {
        if (bossManager == null) return;
        
        // HP slider의 fill 이미지 색상을 밝은 붉은색으로
        if (bossManager.hpSlider != null)
        {
            Image fillImage = bossManager.hpSlider.fillRect?.GetComponent<Image>();
            if (fillImage != null)
            {
                fillImage.color = new Color(1f, 0.25f, 0.25f); // 밝은 붉은색
            }
        }
    }

    Vector2 GetCellPosition(int x, int y)
    {
        float gridWidth = gridContainer.rect.width;
        float startX = -gridWidth / 2 + cellSpacing + cellSize / 2;
        float startY = gridWidth / 2 - cellSpacing - cellSize / 2;

        float posX = startX + x * (cellSize + cellSpacing);
        float posY = startY - y * (cellSize + cellSpacing);

        return new Vector2(posX, posY);
    }

    public void SetBossAttacking(bool attacking)
    {
        isBossAttacking = attacking;
        Debug.Log($"Boss attacking: {attacking}");
    }

    public bool IsBossAttacking()
    {
        return isBossAttacking;
    }

    public void OnBossDefeated()
    {
        int currentStage = bossManager != null ? bossManager.GetBossLevel() : 0;
        
        // ⭐ v5.0: Stage 39 clear시(=stage 40 진입 직전) 최대체력 증가량 2
        int heatIncrease = BOSS_DEFEAT_MAX_HEAT_INCREASE;
        if (currentStage == 39)
        {
            heatIncrease = 2;
            Debug.Log("⭐ Stage 39 클리어! 최대 체력 +2!");
        }
        
        maxHeat += heatIncrease;
        Debug.Log($"보스 처치! 최대 히트 +{heatIncrease}: {maxHeat}");

        int oldHeat = currentHeat;
        currentHeat = maxHeat;

        UpdateHeatUI();

        int recovery = currentHeat - oldHeat;
        if (recovery > 0)
        {
            ShowHeatChangeText(recovery);
        }

        if (isFeverMode)
        {
            StartCoroutine(SyncFreezeWithBossRespawn());
        }
        
        // ⭐ v5.0: 무한대 보스 진입 시 이동 카운트 초기화
        if (currentStage == 39)
        {
            infiniteBossMoveCount = 0;
        }
    }

    System.Collections.IEnumerator SyncFreezeWithBossRespawn()
    {
        if (freezeImage1 != null)
        {
            freezeImage1.DOFade(0f, 0.5f).SetEase(Ease.InQuad);
        }
        if (freezeImage2 != null)
        {
            freezeImage2.DOFade(0f, 0.5f).SetEase(Ease.InQuad);
        }

        yield return new WaitForSeconds(1.5f);

        if (!isFeverMode)
        {
            Debug.Log("🧊 Fever 모드가 종료되어 Freeze 이미지 복원 안함");
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

    public void SetBossTransitioning(bool transitioning)
    {
        isBossTransitioning = transitioning;
        Debug.Log($"보스 리스폰 상태: {transitioning}");
        
        if (!transitioning)
        {
            if (gunButtonImage != null)
            {
                Color c = gunButtonImage.color;
                c.a = 1f;
                gunButtonImage.color = c;
                Debug.Log("🔫 Gun 버튼 alpha 복원: 1.0");
            }
            
            UpdateGunUI();
            Debug.Log("🔫 Gun UI 업데이트 완료! 버튼 활성화 상태 반영");
        }
    }

    public void TakeBossAttack(int damage)
    {
        Debug.Log($"💥💥💥 보스 공격 받음! 데미지: {damage} 💥💥💥");

        int oldHeat = currentHeat;
        currentHeat -= damage;

        if (currentHeat < 0)
            currentHeat = 0;

        UpdateHeatUI(false);
        StartCoroutine(FlashOrangeOnDamage());

        if (damageFlashImage != null)
        {
            StartCoroutine(FlashDamageImage());
        }

        int actualDamage = oldHeat - currentHeat;
        if (actualDamage > 0)
        {
            ShowHeatChangeText(-actualDamage);
        }

        Debug.Log($"⚠️ 보스 공격 피해: -{damage} Heat (Current: {currentHeat}/{maxHeat})");

        if (currentHeat <= 0)
        {
            Debug.Log("히트 고갈! 게임 오버");
            GameOver();
        }
    }

    System.Tuple<int, int> GetTopTwoTileValues()
    {
        if (activeTiles.Count == 0) return new System.Tuple<int, int>(0, 0);

        HashSet<int> uniqueValues = new HashSet<int>();
        foreach (var tile in activeTiles)
        {
            if (tile != null)
            {
                uniqueValues.Add(tile.value);
            }
        }

        List<int> sortedValues = new List<int>(uniqueValues);
        sortedValues.Sort((a, b) => b.CompareTo(a));

        int firstValue = sortedValues.Count > 0 ? sortedValues[0] : 0;
        int secondValue = sortedValues.Count > 1 ? sortedValues[1] : 0;

        return new System.Tuple<int, int>(firstValue, secondValue);
    }

    void UpdateTileBorders()
    {
        var topTwo = GetTopTwoTileValues();

        foreach (var tile in activeTiles)
        {
            if (tile == null) continue;

            bool isProtected = (tile.value == topTwo.Item1 || tile.value == topTwo.Item2);
            tile.SetProtected(isProtected, !isProtected && isGunMode);
        }
    }

    System.Collections.IEnumerator FlashDamageImage()
    {
        if (damageFlashImage == null) yield break;

        damageFlashImage.gameObject.SetActive(true);
        
        damageFlashImage.DOKill();
        
        float startAlpha = 190f / 255f;
        Color flashColor = damageFlashImage.color;
        damageFlashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, startAlpha);
        
        damageFlashImage.DOFade(0f, 0.05f).SetEase(Ease.OutCubic).OnComplete(() => {
            if (damageFlashImage != null)
            {
                damageFlashImage.gameObject.SetActive(false);
            }
        });
        
        yield break;
    }
}
