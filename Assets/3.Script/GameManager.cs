// =====================================================
// GameManager.cs - UPDATED VERSION v4.0
// Date: 2026-02-08
// 
// 수정사항 v4.0:
// 1. 스코프 이미지 제거
// 2. Game Over UI: Quit/Restart/Continue 버튼 추가
// 3. Continue 시 체력 전부 회복 + 피버 10턴 즉시 진입
// 4. 피격 시 1프레임 이미지 플래시 효과
// 5. Heat Slider 기본 색상 핑크로 변경
// 6. 블록 색상 조정
// 7. 총 발사 시 보너스 제거 + 체력 전부 회복
// 8. Fever 중 Enemy 정지 + Freeze 이미지
// 9. Berry 회복 레이저 파티클
// 10. 턴/스테이지 표시 UI
// 11. 39/40번째 적 특수 처리
// 12. 적 공격 턴 UI 기호 변경
// 13. 블록 텍스트 Outline
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
    [SerializeField] private Button quitButton; // ⭐ NEW
    [SerializeField] private Button continueButton; // ⭐ NEW

    [Header("Gun System")]
    [SerializeField] private Button gunButton;
    [SerializeField] private TextMeshProUGUI bulletCountText;
    [SerializeField] private TextMeshProUGUI turnsUntilBulletText;
    [SerializeField] private TextMeshProUGUI attackPowerText;
    [SerializeField] private TextMeshProUGUI gunModeGuideText;

    [SerializeField] private Image gunButtonImage;
    [SerializeField] private RectTransform progressBarFill;
    [SerializeField] private GameObject bulletCountDisplay;

    // ⭐ 스코프 관련 코드 제거
    private Tweener gunGuideAnimation;
    private bool isBossAttacking = false;
    private GameObject activeFeverParticle;

    [Header("Fever Effects")]
    [SerializeField] private Transform feverParticleSpawnPoint;
    [SerializeField] private GameObject feverParticlePrefab;
    [SerializeField] private Image feverBackgroundImage;
    [SerializeField] private Image freezeImage1; // ⭐ NEWexpectedDamageText : Fever 중 Freeze 이미지 1
    [SerializeField] private Image freezeImage2; // ⭐ NEW: Fever 중 Freeze 이미지 2

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
    [SerializeField] private int bossDefeatMaxHeatIncrease = 20;
    [SerializeField] private float heatAnimationDuration = 0.3f;

    [Header("색상 조합 보너스")]
    [SerializeField] private int chocoMergeDamageMultiplier = 4;
    [SerializeField] private int berryMergeHealMultiplier = 4;
    [SerializeField] private int berryMergeBaseHeal = 5;
    [SerializeField] private float feverDamageMultiplier = 1.5f;

    [Header("Low Health Effect")]
    [SerializeField] private LowHealthVignette lowHealthVignette;

    [Header("피격 플래시 효과")] // ⭐ NEW
    [SerializeField] private Image damageFlashImage; // 1프레임 플래시용 이미지

    [Header("Turn & Stage UI")] // ⭐ NEW
    [SerializeField] private TextMeshProUGUI turnText; // 턴 표시
    [SerializeField] private TextMeshProUGUI stageText; // 스테이지 표시 (Stage 1/40)

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
    private int feverEventCount = 0;
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

    private int currentTurn = 0; // ⭐ NEW: 턴 카운트

    private float heatTextOriginalY = 0f;
    private bool heatTextInitialized = false;
    private int lastCurrentHeat = 0;

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

        InitializeGrid();
        StartGame();

        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);

        // ⭐ NEW: Continue/Quit 버튼
        if (continueButton != null)
            continueButton.onClick.AddListener(ContinueGame);

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);

        if (gunButton != null)
            gunButton.onClick.AddListener(ToggleGunMode);

        UpdateGunUI();
        UpdateTurnUI(); // ⭐ NEW
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
        feverEventCount = 0;
        FeverMergeIncreaseAtk = 1;
        permanentAttackPower = 0;
        feverBulletUsed = false;
        currentHeat = maxHeat;
        isGunMode = false;
        isBossTransitioning = false;
        isGameOver = false;
        currentTurn = 0; // ⭐ NEW

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

        // ⭐ NEW: Freeze 이미지 비활성화
        if (freezeImage1 != null) freezeImage1.gameObject.SetActive(false);
        if (freezeImage2 != null) freezeImage2.gameObject.SetActive(false);

        UpdateScoreUI();
        UpdateGunUI();
        UpdateHeatUI(true);
        UpdateTurnUI(); // ⭐ NEW
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
        feverEventCount = 0;
        FeverMergeIncreaseAtk = 1;

        StartGame();
    }

    // ⭐ NEW: Continue 기능
    void ContinueGame()
    {
        if (!isGameOver) return;

        isGameOver = false;
        isProcessing = false;

        // 체력 전부 회복
        currentHeat = maxHeat;
        UpdateHeatUI(true);

        // 피버 10턴 즉시 진입
        isFeverMode = true;
        feverTurnsRemaining = 10;
        feverBulletUsed = false;
        mergeGauge = 0;
        hasBullet = false;

        // 피버 이펙트 활성화
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

        // ⭐ NEW: Freeze 이미지 활성화
        if (freezeImage1 != null) freezeImage1.gameObject.SetActive(true);
        if (freezeImage2 != null) freezeImage2.gameObject.SetActive(true);

        // ⭐ NEW: Enemy 정지
        if (bossManager != null)
        {
            bossManager.SetFrozen(true);
        }

        UpdateGunUI();

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        Debug.Log("🎮 CONTINUE! 체력 전부 회복 + 피버 10턴 진입!");
    }

    // ⭐ NEW: Quit 기능
    void QuitGame()
    {
        Debug.Log("게임 종료");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
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

                // ⭐ NEW: Freeze 이미지 비활성화
                if (freezeImage1 != null) freezeImage1.gameObject.SetActive(false);
                if (freezeImage2 != null) freezeImage2.gameObject.SetActive(false);

                // ⭐ NEW: Enemy 정지 해제
                if (bossManager != null)
                {
                    bossManager.SetFrozen(false);
                }

                isFeverMode = false;

                if (feverBulletUsed)
                {
                    mergeGauge = 0;
                    hasBullet = false;
                    Debug.Log("FEVER END! Shot used, reset to 0/40");
                }
                else
                {
                    mergeGauge = 20;
                    hasBullet = true;
                    Debug.Log("FEVER END! No shot, keep 20/40");
                }
                feverBulletUsed = false;
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

                // ⭐ NEW: Freeze 이미지 활성화
                if (freezeImage1 != null) freezeImage1.gameObject.SetActive(true);
                if (freezeImage2 != null) freezeImage2.gameObject.SetActive(true);

                // ⭐ NEW: Enemy 정지
                if (bossManager != null)
                {
                    bossManager.SetFrozen(true);
                }

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
        renderer.sortingOrder = 5;

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

            // ⭐ CRITICAL: Gun 모드 종료 시 모든 타일 테두리 제거
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
            gunModeGuideText.text = "Tap Tile to Blast and Restore HP!";

            if (gunGuideAnimation != null)
            {
                gunGuideAnimation.Kill();
            }
            gunModeGuideText.transform.localScale = Vector3.one;

            gunGuideAnimation = gunModeGuideText.transform.DOScale(1.1f, 0.6f)
                .SetEase(Ease.InOutQuad)
                .SetLoops(-1, LoopType.Yoyo);
        }

        // ⭐ Gun 모드 진입 시 타일 테두리 표시
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

            // ⭐ 모든 테두리 제거
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

            // ⭐ 모든 테두리 제거
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
            if (targetTile.value == topTwoValues.Item1 || targetTile.value == topTwoValues.Item2)
            {
                Debug.Log($"가장 큰 값 타일({targetTile.value})은 부술 수 없습니다!");
                return;
            }

            currentHeat = maxHeat;
            UpdateHeatUI(true);
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

                if (bossManager != null && !bossManager.IsFrozen())
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

            // ⭐ CRITICAL: 총 발사 후 모든 테두리 제거
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
                gunButtonImage.color = new Color(0.2f, 1f, 0.2f);
            else
                gunButtonImage.color = new Color(0.5f, 0.5f, 0.5f);
        }

        if (gunButton != null)
        {
            gunButton.interactable = !isGameOver && (hasBullet || (isFeverMode && !feverBulletUsed)) && activeTiles.Count > 1;
        }

        if (bulletCountDisplay != null)
        {
            bulletCountDisplay.SetActive(!isFeverMode);
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

            // ⭐ NEW: 핑크 색상으로 변경
            float heatPercent = (float)currentHeat / maxHeat;
            Color heatColor;

            if (heatPercent <= 0.2f)
            {
                // 매우 낮음: 연한 핑크
                heatColor = new Color(1f, 0.6f, 0.7f);
            }
            else if (heatPercent <= 0.4f)
            {
                // 낮음: 핑크
                heatColor = new Color(1f, 0.5f, 0.65f);
            }
            else if (heatPercent <= 0.6f)
            {
                // 중간: 진한 핑크
                heatColor = new Color(1f, 0.4f, 0.6f);
            }
            else
            {
                // 높음: 매우 진한 핑크
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

                                // ⭐ NEW: Berry 회복 레이저 파티클
                                if (projectileManager != null && heatText != null)
                                {
                                    Vector3 berryPos = targetTile.transform.position;
                                    Vector3 heatUIPos = heatText.transform.position;
                                    Color berryColor = new Color(1f, 0.4f, 0.6f); // 핑크색

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
            // ⭐ NEW: 턴 증가
            currentTurn++;
            UpdateTurnUI();

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

                if (projectileManager != null && bossManager != null && bossManager.bossImageArea != null)
                {
                    Vector3 bossPos = bossManager.bossImageArea.transform.position;

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

            currentHeat -= heatDecreasePerTurn;

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

        // ⭐ Fever 중이 아닐 때만 보스 턴 진행
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

        // ⭐ NEW: Freeze 이미지 비활성화
        if (freezeImage1 != null) freezeImage1.gameObject.SetActive(false);
        if (freezeImage2 != null) freezeImage2.gameObject.SetActive(false);

        // ⭐ NEW: Enemy 정지 해제
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

    // ⭐ NEW: 턴/스테이지 UI 업데이트
    void UpdateTurnUI()
    {
        if (turnText != null)
        {
            turnText.text = $"Turn: {currentTurn}";
        }

        if (stageText != null && bossManager != null)
        {
            int currentStage = bossManager.GetBossLevel();
            stageText.text = $"Stage {currentStage}/40";
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

        UpdateTurnUI(); // ⭐ NEW: 스테이지 업데이트
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

        // ⭐ NEW: 1프레임 플래시 효과
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
        sortedValues.Sort((a, b) => b.CompareTo(a)); // 내림차순

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

    // ⭐ NEW: 1프레임 플래시 효과
    System.Collections.IEnumerator FlashDamageImage()
    {
        if (damageFlashImage == null) yield break;

        damageFlashImage.gameObject.SetActive(true);
        yield return null; // 1프레임 대기
        damageFlashImage.gameObject.SetActive(false);
    }
}
