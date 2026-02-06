// =====================================================
// GameManager.cs - UPDATED VERSION v3.0
// Date: 2026-02-06
// 
// 수정사항:
// 1. 크리티컬 제거, 콤보 배율 1.4배로 증가
// 2. Choco gun 데미지 3배, Fever gun 흡수 4배
// 3. Gun Mode 안내 텍스트 "Tap to Shoot Tile!" 추가
// 4. Fever 데미지 1.5배
// 5. Fever 중 게임오버 시 파티클/이미지 정리
// 6. Berry 보너스 텍스트 줄바꿈 개선
// 7. 총 쏠 때 보스 턴 +1
// 8. Fever 이미지 알파 애니메이션
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

    [Header("Gun System")]
    [SerializeField] private Button gunButton;
    [SerializeField] private TextMeshProUGUI bulletCountText; // "Fever!" 또는 "Lv.X"
    [SerializeField] private TextMeshProUGUI turnsUntilBulletText;
    [SerializeField] private TextMeshProUGUI attackPowerText;
    [SerializeField] private TextMeshProUGUI expectedDamageText;
    [SerializeField] private TextMeshProUGUI gunModeGuideText; // ⭐ NEW: "Tap to Shoot Tile!" 안내 텍스트

    [SerializeField] private Image gunButtonImage;
    [SerializeField] private RectTransform progressBarFill;
    [SerializeField] private GameObject bulletCountDisplay; // 총알 갯수 UI 오브젝트 (피버 시 숨김)
    [SerializeField] private Image scopeImage; // 스코프 이미지

    private Tweener scopeHeartbeat; // Scope 애니메이션
    private Tweener gunGuideAnimation; // ⭐ NEW: Gun Mode 안내 텍스트 애니메이션
    private bool isBossAttacking = false; // 보스 공격 중
    private GameObject activeFeverParticle; // Fever 파티클

    [Header("Fever Effects")]
    [SerializeField] private Transform feverParticleSpawnPoint;
    [SerializeField] private GameObject feverParticlePrefab; // 나중에
    [SerializeField] private Image feverBackgroundImage;

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
    [SerializeField] private int heatDecreasePerTurn = 5;
    [SerializeField] private int[] comboHeatRecover = { 0, 0, 4, 10, 18, 30 };
    [SerializeField] private int bossDefeatHeatRecover = 999;
    [SerializeField] private int bossDefeatMaxHeatIncrease = 20;
    [SerializeField] private int gunShotHeatRecover = 8;
    [SerializeField] private float heatAnimationDuration = 0.3f;

    [Header("색상 조합 보너스")]
    [SerializeField] private int chocoMergeDamageMultiplier = 4;
    [SerializeField] private int berryMergeHealMultiplier = 4;
    [SerializeField] private int berryMergeBaseHeal = 5; // Berry 머지 기본 힙량
    [SerializeField] private int chocoGunDamageMultiplier = 3; // ⭐ NEW: Choco 총 데미지 배율 (3배)
    [SerializeField] private int feverGunAbsorbMultiplier = 4; // ⭐ NEW: 피버 총 흡수 배율 (4배)
    [SerializeField] private float feverDamageMultiplier = 1.5f; // ⭐ NEW: 피버 데미지 배율 (1.5배)

    [Header("Low Health Effect")]
    [SerializeField] private LowHealthVignette lowHealthVignette;

    private Tile[,] tiles;
    private List<Tile> activeTiles = new List<Tile>();
    private long score = 0;
    private long bestScore = 0;
    private float cellSize;
    private bool isProcessing = false;
    private bool isBossTransitioning = false;
    private bool isGameOver = false;

    // Gun & Fever System v4.0 (0 → 20 → 40)
    private const int GAUGE_FOR_BULLET = 20;
    private const int GAUGE_FOR_FEVER = 40;
    private const int FEVER_BASE_TURNS = 10;
    private const int MAX_FEVER_TURNS = 10;

    private int mergeGauge = 0;
    private bool hasBullet = false;
    private bool isFeverMode = false;
    private int feverTurnsRemaining = 0;
    private int feverAtkBonus = 0; // ⭐ NEW: Fever 강화 보너스 (영구, Restart 시 초기화)
    private int feverMergeAtkBonus = 0; // ⭐ NEW: Fever 머지 공격력 증가분 (영구, Restart 시 초기화)
    private int feverEventCount = 0; // ⭐ NEW: Fever 진입/총 발사 누적 횟수
    private long FeverMergeIncreaseAtk = 1; // ⭐ NEW: Fever 머지 시 증가량 (Fever 진입/총 발사 시 +1)
    private long permanentAttackPower = 0;
    private bool isGunMode = false;
    private bool feverBulletUsed = false; // 피버 중 총 사용 여부

    // UI 위치 저장 (위치 초기화 문제)
    private float turnsTextOriginalY = 0f;
    private bool turnsTextInitialized = false;
    private float attackTextOriginalY = 0f;
    private bool attackTextInitialized = false;


    // DOTween용 이전 값 저장
    private long lastPermanentAttackPower = 0;
    private int lastMergeGauge = 0;
    private int lastFeverTurnsRemaining = 0;

    // ⭐ NEW: Gun Button 애니메이션
    private Tweener gunButtonHeartbeat;

    private int currentHeat = 100;

    // ⭐ REMOVED: 크리티컬 시스템 제거
    // private const float CRITICAL_CHANCE = 0.25f;
    // private const int CRITICAL_MULTIPLIER = 4;

    private const float COMBO_MULTIPLIER_BASE = 1.4f; // ⭐ UPDATED: 1.2 → 1.4 (1콤보당 1.4배)
    private int comboCount = 0;


    private ProjectileManager projectileManager;
    private Vector3 lastMergedTilePosition;

    void Start()
    {
        // ⭐ UPDATED: string에서 long으로 변환
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

        InitializeGrid();
        StartGame();

        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);

        if (gunButton != null)
            gunButton.onClick.AddListener(ToggleGunMode);

        UpdateGunUI();
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
        mergeGauge = 0;          // 변경
        hasBullet = false;        // 변경
        isFeverMode = false;      // 변경
        feverTurnsRemaining = 0;  // 추가
        feverAtkBonus = 0;        // ⭐ NEW: Fever 강화 보너스 초기화
        feverMergeAtkBonus = 0;   // ⭐ NEW: Fever 머지 공격력 증가분 초기화
        feverEventCount = 0;      // ⭐ NEW: Fever 이벤트 카운트 초기화
        FeverMergeIncreaseAtk = 1; // ⭐ NEW: Fever 머지 증가량 초기화
        permanentAttackPower = 0; // 추가
        feverBulletUsed = false;
        currentHeat = maxHeat;
        isGunMode = false;
        isBossTransitioning = false;
        isGameOver = false;

        // ⭐ NEW: Gun Button 애니메이션 정리
        if (gunButtonHeartbeat != null)
        {
            gunButtonHeartbeat.Kill();
            gunButtonHeartbeat = null;
        }

        // ⭐ NEW: 스코프 이미지 초기화
        if (scopeImage != null)
        {
            scopeImage.gameObject.SetActive(false);
            CanvasGroup canvasGroup = scopeImage.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
        }

        // ⭐ NEW: Scope 애니메이션 정리
        if (scopeHeartbeat != null)
        {
            scopeHeartbeat.Kill();
            scopeHeartbeat = null;
        }

        // ⭐ NEW: Gun Mode 안내 텍스트 애니메이션 정리
        if (gunGuideAnimation != null)
        {
            gunGuideAnimation.Kill();
            gunGuideAnimation = null;
        }
        if (gunModeGuideText != null)
        {
            gunModeGuideText.gameObject.SetActive(false);
        }

        UpdateScoreUI();
        UpdateGunUI();
        UpdateHeatUI(true);
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
        permanentAttackPower = 0; // ← 추가! (영구 공격력 초기화)
        feverAtkBonus = 0; // ⭐ NEW: Fever 강화 보너스 초기화
        feverMergeAtkBonus = 0; // ⭐ NEW: Fever 머지 공격력 증가분 초기화
        feverEventCount = 0; // ⭐ NEW: Fever 이벤트 카운트 초기화
        FeverMergeIncreaseAtk = 1; // ⭐ NEW: Fever 머지 증가량 초기화

        StartGame();

    }

    void CheckGaugeAndFever()
    {
        if (isFeverMode)
        {
            if (feverTurnsRemaining <= 0)
            {
                // ⭐ Fever 종료: 파티클 제거
                if (activeFeverParticle != null)
                {
                    Destroy(activeFeverParticle);
                    activeFeverParticle = null;
                }

                // ⭐ Fever 배경 이미지 비활성화
                if (feverBackgroundImage != null)
                {
                    feverBackgroundImage.DOKill(); // ⭐ 애니메이션 정리
                    feverBackgroundImage.gameObject.SetActive(false);
                }

                isFeverMode = false;

                // ⭐ UPDATED: 피버 중 총을 쐈으면 0, 안 쐈으면 20 유지
                if (feverBulletUsed)
                {
                    mergeGauge = 0;  // 총 쏨 → 0/40
                    hasBullet = false;
                    Debug.Log("FEVER END! Shot used, reset to 0/40");
                }
                else
                {
                    mergeGauge = 20;  // 총 안 쏨 → 20/40
                    hasBullet = true;
                    Debug.Log("FEVER END! No shot, keep 20/40");
                }
                feverBulletUsed = false; // ⭐ NEW: 리셋
            }
        }
        else
        {
            if (mergeGauge >= GAUGE_FOR_FEVER)
            {
                // ⭐ Fever 시작: 파티클 생성
                SpawnFeverParticle();

                // ⭐ Fever 배경 이미지 활성화 + 알파 애니메이션
                if (feverBackgroundImage != null)
                {
                    feverBackgroundImage.gameObject.SetActive(true);
                    // ⭐ NEW: 이글이글 효과 (alpha 0.7 ~ 1.0, 더 빠르게)
                    feverBackgroundImage.DOKill();

                    // 초기 alpha 설정
                    Color c = feverBackgroundImage.color;
                    c.a = 1.0f;
                    feverBackgroundImage.color = c;

                    // 애니메이션 시작
                    feverBackgroundImage.DOFade(0.7f, 0.5f)
                        .SetEase(Ease.InOutSine)
                        .SetLoops(-1, LoopType.Yoyo);
                }

                isFeverMode = true;
                feverBulletUsed = false; // ⭐ NEW: 피버 시작 시 리셋
                feverTurnsRemaining = FEVER_BASE_TURNS;
                hasBullet = false;
                Debug.Log($"FEVER MODE! {FEVER_BASE_TURNS} turns granted!");
                UpdateGunButtonAnimation(); // ⭐ NEW: 피버 시작 시 애니메이션 업데이트

                // ⭐ NEW: Fever 진입 시마다 Fever ATK Bonus +1
                feverAtkBonus++;
                Debug.Log($"🔥 FEVER 진입! Fever ATK Bonus +1 (Total: {feverAtkBonus})");

                // ⭐ NEW: Fever 진입 시마다 Fever 머지 증가량 +1
                FeverMergeIncreaseAtk++;
                Debug.Log($"🔥 FEVER 진입! Fever 머지 증가량 +1 (Now: {FeverMergeIncreaseAtk})");
            }
            else if (mergeGauge >= GAUGE_FOR_BULLET && !hasBullet)
            {
                hasBullet = true;
                Debug.Log($"Bullet ready! ({mergeGauge}/40)");
                UpdateGunButtonAnimation(); // ⭐ NEW: 상태 변경 시에만 애니메이션 업데이트
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

        // 기존 파티클 제거
        if (activeFeverParticle != null)
        {
            Destroy(activeFeverParticle);
        }

        // ⭐ 임시: 파티클 시스템 생성 (나중에 프리펩으로 교체)
        GameObject particleObj = new GameObject("FeverFlameParticle");
        particleObj.transform.SetParent(feverParticleSpawnPoint, false);
        particleObj.transform.localPosition = Vector3.zero;

        ParticleSystem ps = particleObj.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.startLifetime = 0.5f;
        main.startSpeed = 50f;
        main.startSize = 30f;
        main.startColor = new Color(1f, 0.5f, 0f); // 주황색
        main.maxParticles = 50;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.playOnAwake = true;
        main.loop = true; // ⭐ 지속적으로 생성

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 20; // 초당 20개

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 15f;
        shape.radius = 10f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
            new GradientColorKey(new Color(1f, 1f, 0f), 0.0f), // 노란색
            new GradientColorKey(new Color(1f, 0.5f, 0f), 0.5f), // 주황색
            new GradientColorKey(new Color(1f, 0f, 0f), 1.0f)  // 빨간색
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
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(100f); // 위로 올라감

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = new Material(Shader.Find("UI/Default"));
        renderer.sortingOrder = 5; // ⭐ 버튼과 배경 사이

        // UIParticle 추가
        var uiParticle = particleObj.AddComponent<Coffee.UIExtensions.UIParticle>();
        uiParticle.scale = 2f;

        activeFeverParticle = particleObj;

        Debug.Log("Fever flame particle spawned!");
    }


    void ToggleGunMode()
    {
        // ⭐ NEW: 보스 공격 중에는 Gun Mode 전환 불가
        if (isBossAttacking)
        {
            Debug.Log("보스 공격 중에는 Gun Mode 전환 불가!");
            return;
        }

        // ⭐ Gun Mode 중이면 즉시 취소 가능
        if (isGunMode)
        {
            isGunMode = false;

            // Gun Guide 정리
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

            // Scope 정리
            if (scopeHeartbeat != null)
            {
                scopeHeartbeat.Kill();
                scopeHeartbeat = null;
            }
            if (scopeImage != null)
            {
                scopeImage.transform.localScale = Vector3.one;
                CanvasGroup canvasGroup = scopeImage.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.DOFade(0f, 0.2f).SetEase(Ease.InQuad).OnComplete(() =>
                    {
                        if (scopeImage != null)
                            scopeImage.gameObject.SetActive(false);
                    });
                }
                else
                {
                    scopeImage.gameObject.SetActive(false);
                }
            }

            UpdateGunUI();
            return;
        }

        // ⭐ Gun Mode 활성화
        if (!hasBullet && (!isFeverMode || feverBulletUsed)) return;

        if (activeTiles.Count <= 1)
        {
            Debug.Log("타일이 1개 이하일 때는 총을 쓸 수 없습니다!");
            return;
        }

        isGunMode = true;

        // ⭐ NEW: Gun Mode 안내 텍스트 애니메이션
        if (gunModeGuideText != null)
        {
            if (isGunMode)
            {
                // Gun Mode 활성화: 텍스트 표시 + gun button과 같은 박자
                gunModeGuideText.gameObject.SetActive(true);
                gunModeGuideText.text = "Tap Tile to Shoot!";

                if (gunGuideAnimation != null)
                {
                    gunGuideAnimation.Kill();
                }
                // 초기 스케일 1.0으로 설정 (동기화)
                gunModeGuideText.transform.localScale = Vector3.one;


                // ⭐ UPDATED: gun button과 같은 박자 (0.3초, 1.15배)
                gunGuideAnimation = gunModeGuideText.transform.DOScale(1.1f, 0.6f)
                    .SetEase(Ease.InOutQuad)
                    .SetLoops(-1, LoopType.Yoyo);
            }
            else
            {
                // Gun Mode 비활성화: 텍스트 숨김
                if (gunGuideAnimation != null)
                {
                    gunGuideAnimation.Kill();
                    gunGuideAnimation = null;
                }
                gunModeGuideText.transform.localScale = Vector3.one;
                gunModeGuideText.gameObject.SetActive(false);
            }
        }

        // ⭐ NEW: 스코프 이미지 애니메이션
        if (scopeImage != null)
        {
            if (isGunMode)
            {
                // Gun Mode 활성화: 투명에서 나타나기
                scopeImage.gameObject.SetActive(true);

                CanvasGroup canvasGroup = scopeImage.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = scopeImage.gameObject.AddComponent<CanvasGroup>();
                }

                // ⭐ FIXED: 애니메이션 정지 후 즉시 표시
                canvasGroup.DOKill();
                canvasGroup.alpha = 1f; // 즉시 표시
            }
            else
            {
                // Gun Mode 비활성화
                // ⭐ 애니메이션 정지
                if (scopeHeartbeat != null)
                {
                    scopeHeartbeat.Kill();
                    scopeHeartbeat = null;
                }
                scopeImage.transform.localScale = Vector3.one;

                CanvasGroup canvasGroup = scopeImage.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.DOKill();
                    canvasGroup.alpha = 0f;
                }
                scopeImage.gameObject.SetActive(false);
            }
        }
        UpdateGunUI();
    }

    void ShootTile()
    {
        // 사격 가능 여부 체크
        if (!hasBullet && (!isFeverMode || feverBulletUsed))
        {
            isGunMode = false;

            // ⭐ NEW: Gun Guide 정리
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

            UpdateGunUI();
            return;
        }

        if (activeTiles.Count <= 1)
        {
            Debug.Log("타일이 1개 이하일 때는 총을 쓸 수 없습니다!");
            isGunMode = false;

            // ⭐ NEW: Gun Guide 정리
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

            UpdateGunUI();
            return;
        }

        // 타일 선택 로직
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
            TileColor tileColor = targetTile.tileColor;

            // === 1. 데미지 계산 ===
            // ⭐ UPDATED: long 자료형
            long allTilesSum = GetAllTilesSum();
            long baseDamage = allTilesSum + permanentAttackPower;

            // ⭐ UPDATED: Choco 보너스: 3배
            if (tileColor == TileColor.Choco)
            {
                baseDamage *= chocoGunDamageMultiplier;
                Debug.Log($"🔫🍫 Choco 보너스! 데미지 {chocoGunDamageMultiplier}배!");
            }

            // ⭐ NEW: 피버 모드 데미지 1.5배
            if (isFeverMode)
            {
                baseDamage = (long)(baseDamage * feverDamageMultiplier);
                Debug.Log($"🔥 FEVER! 데미지 {feverDamageMultiplier}배!");
            }

            // ⭐ NEW: Fever ATK Bonus 적용
            if (isFeverMode && feverAtkBonus > 0)
            {
                float bonusMultiplier = 1.0f + (feverAtkBonus * 0.1f); // 1 bonus = +10%
                baseDamage = (long)(baseDamage * bonusMultiplier);
                Debug.Log($"🔥 FEVER ATK BONUS x{bonusMultiplier:F1}!");
            }

            long finalDamage = baseDamage;

            // === 2. 체력 회복 ===
            int baseHeal = Mathf.FloorToInt(maxHeat * 0.25f); // 25%
            bool isBerry = (tileColor == TileColor.Berry);

            if (isBerry)
            {
                baseHeal = Mathf.FloorToInt(maxHeat * 0.75f); // 75%
                Debug.Log($"BERRY BONUS! 75% heal");
            }

            RecoverHeat(baseHeal);

            if (isBerry)
            {
                ShowHeatChangeText(baseHeal, "BERRY BONUS");
            }

            // === 3. 무한 성장 (공격력 흡수) ===
            // ⭐ UPDATED: 피버 시 흡수율 20% (4배)
            int absorbRate = isFeverMode ? (5 * feverGunAbsorbMultiplier) : 5; // 피버 중 20%, 평시 5%
            long absorbAmount = (long)Mathf.Floor(allTilesSum * absorbRate / 100f);
            permanentAttackPower += absorbAmount;

            Debug.Log($"💪 공격력 흡수! +{absorbAmount} (총 {permanentAttackPower}) [흡수율: {absorbRate}%]");

            // === 4. 타일 제거 및 공격 ===
            Vector3 tilePos = targetTile.transform.position;
            Vector2Int pos = targetTile.gridPosition;

            // ⭐ NEW: 파티클 먼저 재생
            targetTile.PlayGunDestroyEffect();

            tiles[pos.x, pos.y] = null;
            activeTiles.Remove(targetTile);
            Destroy(targetTile.gameObject);

            if (bossManager != null)
            {
                if (projectileManager != null && bossManager.bossImageArea != null)
                {
                    Vector3 bossPos = bossManager.bossImageArea.transform.position;
                    Color bulletColor = isFeverMode ? new Color(1f, 0.3f, 0f) : Color.yellow;

                    // ⭐ NEW: 모든 타일에서 레이저 발사 (연출용)
                    foreach (var tile in activeTiles)
                    {
                        if (tile == null) continue;

                        Vector3 fromPos = tile.transform.position;

                        // 레이저만 발사 (데미지 없음, 연출만)
                        projectileManager.FireKnifeProjectile(fromPos, bossPos, bulletColor, null);
                    }

                    // 실제 데미지는 부순 타일에서만
                    projectileManager.FireBulletSalvo(tilePos, bossPos, 1, (int)finalDamage, bulletColor, (damage) =>
                    {
                        bossManager.TakeDamage(finalDamage);
                    });

                    bool isChoco = (tileColor == TileColor.Choco);
                    ShowDamageText(finalDamage, 0, true, isChoco); // ⭐ UPDATED: comboNum = 0 (총 사용)

                    CameraShake.Instance?.ShakeMedium();

                    //scope 초기화
                    CanvasGroup canvasGroup = scopeImage.GetComponent<CanvasGroup>();
                    if (canvasGroup != null)
                    {
                        canvasGroup.DOFade(0f, 0.2f).SetEase(Ease.InQuad).OnComplete(() =>
                        {
                            if (scopeImage != null)
                                scopeImage.gameObject.SetActive(false);
                        });
                    }
                    else
                    {
                        scopeImage.gameObject.SetActive(false);
                    }
                }

                else
                {
                    bossManager.TakeDamage(finalDamage);
                    bool isChoco = (tileColor == TileColor.Choco);
                    ShowDamageText(finalDamage, 0, true, isChoco); // ⭐ UPDATED: comboNum = 0 (총 사용)
                }
            }

            // === 5. 게이지 초기화 ===
            if (isFeverMode)
            {
                feverBulletUsed = true; // ⭐ NEW: 피버 중 총 사용 기록
                mergeGauge = 0;
                hasBullet = false;
                Debug.Log("FEVER SHOT! Bullet used, cannot shoot again");

                // ⭐ NEW: Fever 총 사용 시 보스 턴 +3, Fever ATK Bonus +1
                if (bossManager != null)
                {
                    bossManager.AddTurns(3); // 보스 공격 턴 +3
                    Debug.Log("🔥 FEVER SHOT! 보스 공격 턴 +3");
                }
                feverAtkBonus++; // Fever 강화 보너스 +1 (영구)
                Debug.Log($"🔥 FEVER ATK BONUS +1! (Total: {feverAtkBonus})");

                // ⭐ NEW: Fever 총 사용 시에도 Fever 머지 증가량 +1
                FeverMergeIncreaseAtk++;
                Debug.Log($"🔥 FEVER GUN! Fever 머지 증가량 +1 (Now: {FeverMergeIncreaseAtk})");
            }
            else
            {
                // 평시 사격 → 잔여 차지 유지 (20을 빼기)
                mergeGauge = Mathf.Max(0, mergeGauge - GAUGE_FOR_BULLET);
                hasBullet = (mergeGauge >= GAUGE_FOR_BULLET);
                Debug.Log($"GUN SHOT! Remaining charge: {mergeGauge}/40");
            }

            isGunMode = false;

            // ⭐ NEW: Gun Guide 정리
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
                    // ⭐ UPDATED: Berry 보너스 텍스트 자연스럽게 줄바꿈
                    heatChangeText.text = $"{bonusText}\n+{change}";
                    heatChangeText.alignment = TextAlignmentOptions.Center; // 중앙 정렬
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
        // bulletCountText: 상태 표시
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

        // ⭐ UPDATED: 진행도 표시
        if (turnsUntilBulletText != null)
        {
            // 초기 Y 위치 저장 (한 번만)
            if (!turnsTextInitialized)
            {
                RectTransform textRect = turnsUntilBulletText.GetComponent<RectTransform>();
                turnsTextOriginalY = textRect.anchoredPosition.y;
                turnsTextInitialized = true;
            }

            int currentValue = isFeverMode ? feverTurnsRemaining : mergeGauge;
            int lastValue = isFeverMode ? lastFeverTurnsRemaining : lastMergeGauge;

            // 텍스트 설정
            if (isFeverMode)
            {
                // 콤보 여부 확인
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
                // 피버 종료 직후 0/40 표시 kitos수정사항
                if (mergeGauge == 0)
                {
                    turnsUntilBulletText.text = "0/40";
                }
                else if (mergeGauge < GAUGE_FOR_BULLET)
                {
                    turnsUntilBulletText.text = $"{mergeGauge}/40";
                }
                else
                {
                    turnsUntilBulletText.text = $"{mergeGauge}/40";
                }
            }

            // 값이 변경되었을 때만 DOTween 실행
            if (currentValue != lastValue)
            {
                if (isFeverMode)
                    lastFeverTurnsRemaining = feverTurnsRemaining;
                else
                    lastMergeGauge = mergeGauge;

                // 위로 튀어오르는 애니메이션 (저장된 originalY 사용)
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

        // ⭐ UPDATED: 추가 공격력 표시
        if (attackPowerText != null)
        {
            // 초기 Y 위치 저장 (한 번만)
            if (!attackTextInitialized)
            {
                RectTransform textRect = attackPowerText.GetComponent<RectTransform>();
                attackTextOriginalY = textRect.anchoredPosition.y;
                attackTextInitialized = true;
            }

            attackPowerText.text = $"+ATK: {permanentAttackPower}";

            // 값이 변경되었을 때만 DOTween 실행
            if (permanentAttackPower != lastPermanentAttackPower)
            {
                lastPermanentAttackPower = permanentAttackPower;

                // 위로 튀어오르는 애니메이션 (저장된 originalY 사용)
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

        // 기댓값 표시 (새 UI)
        if (expectedDamageText != null)
        {
            long expectedDamage = GetAllTilesSum() + permanentAttackPower;
            expectedDamageText.text = $"DMG: {expectedDamage}";
        }

        // 프로그레스 바
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

        // 버튼 색상
        if (gunButtonImage != null)
        {
            if (isGunMode)
                gunButtonImage.color = Color.red; // 빨간색 (취소 가능)
            else if (isFeverMode)
                gunButtonImage.color = new Color(1f, 0.3f, 0f); // 주황색 (피버)
            else if (hasBullet)
                gunButtonImage.color = new Color(0.2f, 1f, 0.2f); // 초록색 (준비)
            else
                gunButtonImage.color = new Color(0.5f, 0.5f, 0.5f); // 회색 (비활성)
        }

        // 버튼 활성화
        if (gunButton != null)
        {
            gunButton.interactable = !isGameOver && (hasBullet || (isFeverMode && !feverBulletUsed)) && activeTiles.Count > 1;
        }

        // 총알 표시 (피버 시 숨김)
        if (bulletCountDisplay != null)
        {
            bulletCountDisplay.SetActive(!isFeverMode);
        }

        // ⭐ UPDATED: Gun Button 애니메이션 - 상태 변경 시에만 업데이트
        bool shouldAnimate = hasBullet || (isFeverMode && !feverBulletUsed);
        UpdateGunButtonAnimationIfNeeded(shouldAnimate);

        // ⭐ NEW: Scope 심장박동 애니메이션
        if (scopeImage != null && isGunMode)
        {
            // 기존 애니메이션 정지
            if (scopeHeartbeat != null)
            {
                scopeHeartbeat.Kill();
                scopeHeartbeat = null;
            }

            // 원래 크기로 초기화
            scopeImage.transform.localScale = Vector3.one;

            // Gun Button과 동일한 템포 (Ease도 동일하게)
            scopeHeartbeat = scopeImage.transform.DOScale(1.1f, 0.6f)
                .SetEase(Ease.InOutQuad)
                .SetLoops(-1, LoopType.Yoyo);
        }
        else if (scopeImage != null && !isGunMode)
        {
            // Gun Mode 아닐 때 정지
            if (scopeHeartbeat != null)
            {
                scopeHeartbeat.Kill();
                scopeHeartbeat = null;
            }
            scopeImage.transform.localScale = Vector3.one;
        }
    }

    System.Collections.IEnumerator FlashOrangeOnDamage()
    {
        if (heatBarImage == null || heatText == null) yield break;

        // 현재 색상 저장
        Color originalBarColor = heatBarImage.color;
        Color originalTextColor = heatText.color;

        // 주황색으로 바꾸기
        Color orangeColor = new Color(1f, 0.65f, 0f);
        heatBarImage.color = orangeColor;
        heatText.color = orangeColor;

        yield return new WaitForSeconds(0.15f);

        // 원래 색상으로 복귀
        heatBarImage.color = originalBarColor;
        heatText.color = originalTextColor;
    }

    // ⭐ NEW: Gun Button 애니메이션 상태 추적
    private bool lastGunButtonAnimationState = false;

    void UpdateGunButtonAnimationIfNeeded(bool shouldAnimate)
    {
        // 상태가 변경되지 않았으면 아무것도 안 함
        bool currentState = isGunMode || shouldAnimate;
        if (currentState == lastGunButtonAnimationState && gunButtonHeartbeat != null)
        {
            return;
        }

        lastGunButtonAnimationState = currentState;

        if (gunButton == null || gunButtonImage == null) return;

        // 기존 애니메이션 정지
        if (gunButtonHeartbeat != null)
        {
            gunButtonHeartbeat.Kill();
            gunButtonHeartbeat = null;
        }

        // 원래 크기로 초기화
        gunButton.transform.localScale = Vector3.one;

        if (isGunMode)
        {
            // Gun Mode: 빠른 템포 (긴박하게)
            gunButtonHeartbeat = gunButton.transform.DOScale(1.15f, 0.3f)
                .SetEase(Ease.InOutQuad)
                .SetLoops(-1, LoopType.Yoyo);
        }
        else if (shouldAnimate)
        {
            // 총알 있음: 느린 템포 (심장 뛰듯)
            gunButtonHeartbeat = gunButton.transform.DOScale(1.1f, 0.6f)
                .SetEase(Ease.InOutQuad)
                .SetLoops(-1, LoopType.Yoyo);
        }
        else
        {
            // 비활성: 크기 고정
            gunButton.transform.localScale = Vector3.one;
        }
    }

    void UpdateGunButtonAnimation()
    {
        if (gunButton == null || gunButtonImage == null) return;

        // 기존 애니메이션 정지
        if (gunButtonHeartbeat != null)
        {
            gunButtonHeartbeat.Kill();
            gunButtonHeartbeat = null;
        }

        // 원래 크기로 초기화
        gunButton.transform.localScale = Vector3.one;

        if (isGunMode)
        {
            // Gun Mode: 빠른 템포 (긴박하게)
            gunButtonHeartbeat = gunButton.transform.DOScale(1.15f, 0.3f)
                .SetEase(Ease.InOutQuad)
                .SetLoops(-1, LoopType.Yoyo);
        }
        else if (hasBullet || (isFeverMode && !feverBulletUsed))
        {
            // 총알 있음: 느린 템포 (심장 뛰듯)
            gunButtonHeartbeat = gunButton.transform.DOScale(1.1f, 0.6f)
                .SetEase(Ease.InOutQuad)
                .SetLoops(-1, LoopType.Yoyo);
        }
        else
        {
            // 비활성: 크기 고정
            gunButton.transform.localScale = Vector3.one;
        }
    }

    void UpdateHeatUI(bool instant = false)
    {
        if (heatText != null)
        {
            heatText.text = $"Heat: {currentHeat}/{maxHeat}";

            float heatPercent = (float)currentHeat / maxHeat;
            Color heatColor;

            if (heatPercent <= 0.2f)
            {
                heatColor = new Color(0.7f, 0.9f, 1f);
            }
            else if (heatPercent <= 0.4f)
            {
                heatColor = new Color(0.4f, 0.8f, 1f);
            }
            else if (heatPercent <= 0.6f)
            {
                heatColor = new Color(0.3f, 1f, 0.8f);
            }
            else
            {
                heatColor = new Color(0.3f, 1f, 0.3f);
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

    void DecreaseHeat()
    {
        int oldHeat = currentHeat;
        currentHeat -= heatDecreasePerTurn;
        if (currentHeat < 0)
            currentHeat = 0;

        int actualDecrease = oldHeat - currentHeat;

        UpdateHeatUI();

        if (actualDecrease != 0)
        {
            ShowHeatChangeText(-actualDecrease);
        }

        if (currentHeat <= 0)
        {
            Debug.Log("히트 고갈! 게임 오버");
            GameOver();
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
        bool hadChocoMerge = false; // ⭐ NEW: 초코 머지 발생 여부
        int berryMergeCount = 0;
        bool hadBerryMerge = false; // ⭐ NEW: Berry 머지 발생 여부

        // Heat 변화 계산을 위해 턴 시작 시 Heat 저장
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
                                hadChocoMerge = true; // ⭐ NEW: 초코 머지 발생

                                int bonusDamage = mergedValue * (chocoMergeDamageMultiplier - 1);
                                totalMergedValue += bonusDamage;

                                // 게이지 증가
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
                                hadBerryMerge = true; // ⭐ NEW: Berry 머지 발생

                                int bonusHeal = berryMergeBaseHeal * berryMergeHealMultiplier;
                                currentHeat += bonusHeal;
                                if (currentHeat > maxHeat) currentHeat = maxHeat;

                                // 게이지 증가
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
                                // Mix 머지: 게이지 +2 (보너스)

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
                                targetTile.PlayMixMergeEffect(); // ⭐ NEW: Mix 머지 파티클 호출
                            }

                            TileColor newColor = Random.value < 0.5f ? TileColor.Choco : TileColor.Berry;
                            targetTile.SetColor(newColor);

                            merged[nextPos.x, nextPos.y] = true;
                            anyMerged = true;

                            lastMergedTilePosition = targetTile.transform.position;

                            // 전체 머지 카운트 (콤보용 - 모든 머지)
                            mergeCountThisTurn++;

                            // ⭐ Fever 중 머지 시 영구 공격력 증가 (FeverMergeIncreaseAtk만큼)
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
            comboCount = mergeCountThisTurn;

            if (totalMergedValue > 0 && bossManager != null)
            {
                // ⭐ UPDATED: 크리티컬 제거, 콤보 배율 적용 (1콤보 제외, 1.4배)
                // 콤보 배율: 1콤보=1.0배, 2콤보=1.4배, 3콤보=1.96배 (소수점 버림)
                float comboMultiplier = 1.0f;
                if (mergeCountThisTurn > 1)
                {
                    comboMultiplier = Mathf.Pow(COMBO_MULTIPLIER_BASE, mergeCountThisTurn - 1);
                }

                long baseDamage = (long)Mathf.Floor(totalMergedValue * comboMultiplier);

                // ⭐ NEW: Choco merge가 있었으면 추가 ATK를 2배로 적용
                if (hadChocoMerge && permanentAttackPower > 0)
                {
                    baseDamage += permanentAttackPower * 2; // 2배로 적용
                    Debug.Log($"🍫 CHOCO MERGE! 추가 ATK 2배 적용: +{permanentAttackPower * 2}");
                }
                else
                {
                    baseDamage += permanentAttackPower; // 일반 적용
                }

                // ⭐ NEW: 피버 모드 데미지 1.5배
                if (isFeverMode)
                {
                    baseDamage = (long)(baseDamage * feverDamageMultiplier);
                }

                // ⭐ NEW: Fever 머지 시 공격력 증가분 적용
                if (isFeverMode && feverMergeAtkBonus > 0)
                {
                    baseDamage += feverMergeAtkBonus;
                    Debug.Log($"🔥 FEVER MERGE! 공격력 +{feverMergeAtkBonus}");
                }

                // ⭐ NEW: Fever ATK Bonus 적용
                if (isFeverMode && feverAtkBonus > 0)
                {
                    float bonusMultiplier = 1.0f + (feverAtkBonus * 0.1f); // 1 bonus = +10%
                    baseDamage = (long)(baseDamage * bonusMultiplier);
                    Debug.Log($"🔥 FEVER ATK BONUS x{bonusMultiplier:F1}!");
                }

                long damage = baseDamage;

                if (projectileManager != null && bossManager != null && bossManager.bossImageArea != null)
                {
                    Vector3 bossPos = bossManager.bossImageArea.transform.position;

                    Color laserColor = Color.white;
                    if (isFeverMode)
                    {
                        // 피버 모드: 주황색
                        laserColor = new Color(1f, 0.5f, 0f);
                    }
                    else if (mergeCountThisTurn >= 2)
                    {
                        // 콤보: 콤보 수에 따라 색상 변경
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
                        ShowDamageText(damage, mergeCountThisTurn, false); // ⭐ UPDATED: 콤보 수 전달
                        CameraShake.Instance?.ShakeLight();
                    });
                }
                else
                {
                    bossManager.TakeDamage(damage);
                    ShowDamageText(damage, mergeCountThisTurn, false); // ⭐ UPDATED: 콤보 수 전달
                }
            }

            // oldHeat는 턴 시작 시 이미 저장됨 (Berry 머지 회복 이전 값)
            currentHeat -= heatDecreasePerTurn;

            if (mergeCountThisTurn > 0)
            {
                int comboIndex = Mathf.Min(mergeCountThisTurn, comboHeatRecover.Length - 1);
                int heatRecovery = comboHeatRecover[comboIndex];
                // ⭐ NEW: Berry 머지 시 회복량 2배
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

            // 콤보 달성 시 게이지 증가
            if (!isFeverMode && mergeCountThisTurn >= 2)
            {
                int gaugeIncrease = 1; // 2콤보 이상 = +1
                mergeGauge += gaugeIncrease;
                Debug.Log($"🎯 {mergeCountThisTurn}콤보 달성! 게이지 +{gaugeIncrease} ({mergeGauge}/20)");
            }

            UpdateScoreUI();

            comboCount = mergeCountThisTurn;

            // 피버 모드 체크
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

    // ⭐ UPDATED: 크리티컬 제거, 콤보 수로 변경
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
                    damageText.text = $"CHOCO x{chocoGunDamageMultiplier}\n-{damage}";
                    damageText.color = new Color(1f, 0.84f, 0f); // 금색
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
                // ⭐ UPDATED: 콤보 텍스트 (배율 표시 제거)
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

            // ⭐ UPDATED: 크리티컬 제거, 일반 애니메이션만
            damageSequence.Insert(0f, damageRect.DOScale(1.2f, 0.15f).SetEase(Ease.OutQuad));
            damageSequence.Insert(0.15f, damageRect.DOScale(1f, 0.15f).SetEase(Ease.InQuad));

            damageSequence.OnComplete(() => {
                if (damageObj != null) Destroy(damageObj);
            });
        }
    }

    void ShowHeatChangeText(int change)
    {
        if (damageTextPrefab == null || damageTextParent == null || heatText == null) return;

        GameObject heatChangeObj = Instantiate(damageTextPrefab, damageTextParent);
        TextMeshProUGUI heatChangeText = heatChangeObj.GetComponent<TextMeshProUGUI>();

        if (heatChangeText != null)
        {
            if (change > 0)
            {
                heatChangeText.text = "+" + change;
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

    void AfterMove()
    {
        SpawnTile();

        // 피버 턴 연장 먼저! (감소 전에)
        if (isFeverMode && comboCount >= 2)
        {
            int extension = comboCount;
            feverTurnsRemaining += extension;

            if (feverTurnsRemaining > MAX_FEVER_TURNS)
                feverTurnsRemaining = MAX_FEVER_TURNS;

            Debug.Log($"FEVER EXTEND! +{extension} (Now: {feverTurnsRemaining})");
        }

        // 피버 턴 감소
        if (isFeverMode)
        {
            feverTurnsRemaining--;
            Debug.Log($"Fever turn -1: {feverTurnsRemaining} left");
        }

        // 게이지 체크
        CheckGaugeAndFever();

        // 보스 턴
        if (bossManager != null && !isFeverMode)
        {
            bossManager.OnPlayerTurn();
        }

        // FIXED: 피버 중에도 게임오버 체크
        // 피버 총알까지 다 쓰고, 이동 불가능하면 게임오버
        if (!CanMove())
        {
            if (!isFeverMode || feverBulletUsed)
            {
                // 평시이거나, 피버 중 총알 이미 사용했으면 게임오버
                if (!hasBullet)
                {
                    GameOver();
                    return; // ⭐ 중요: 게임오버 후 isProcessing 리셋 안 함
                }
            }
        }

        isProcessing = false;
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

        // ⭐ FIXED: 피버 중 게임오버 시 파티클/이미지 정리
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

        UpdateGunUI();

        // ⭐ NEW: 2초 딜레이 + 서서히 나타나기
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);

            CanvasGroup canvasGroup = gameOverPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameOverPanel.AddComponent<CanvasGroup>();
            }

            // 초기 투명
            canvasGroup.alpha = 0f;

            // 2초 후 1초에 걸쳐 서서히 나타남
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
            // ⭐ UPDATED: long을 string으로 저장
            PlayerPrefs.SetString("BestScore", bestScore.ToString());
            PlayerPrefs.Save();
        }

        if (bestScoreText != null)
            bestScoreText.text = bestScore.ToString();
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


    public void OnBossDefeated()
    {
        maxHeat += bossDefeatMaxHeatIncrease;
        Debug.Log($"보스 처치! 최대 히트 증가: {maxHeat}");

        int oldHeat = currentHeat;
        currentHeat = maxHeat;

        UpdateHeatUI();

        int recovery = currentHeat - oldHeat;
        if (recovery > 0)
        {
            ShowHeatChangeText(recovery);
        }
    }

    public void SetBossTransitioning(bool transitioning)
    {
        isBossTransitioning = transitioning;
        Debug.Log($"보스 리스폰 상태: {transitioning}");
    }

    public void TakeBossAttack(int damage)
    {
        int oldHeat = currentHeat;
        currentHeat -= damage;

        if (currentHeat < 0)
            currentHeat = 0;

        UpdateHeatUI();
        StartCoroutine(FlashOrangeOnDamage());

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
}