// =====================================================
// GameManager.cs - FINAL VERSION v2.0
// Date: 2026-02-02 07:30
// 
// 변경사항:
// 1. 핑크 머지: 콤보마다 힐량 적용 확인
// 2. 파티클 Z-order 수정 (sortingOrder 높게)
// 3. 믹스 머지만 장전 카운트, 0/15로 변경
// 4. 피버: "Fever!" 표시, 총알 표시 끄기, 보스 턴 안 증가
// 5. 총 레벨 시스템: 2배씩 증가, 색상 보너스
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

    [SerializeField] private Image gunButtonImage;
    [SerializeField] private RectTransform progressBarFill;
    [SerializeField] private GameObject bulletCountDisplay; // 총알 갯수 UI 오브젝트 (피버 시 숨김)

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

    [Header("Low Health Effect")]
    [SerializeField] private LowHealthVignette lowHealthVignette;

    private Tile[,] tiles;
    private List<Tile> activeTiles = new List<Tile>();
    private int score = 0;
    private int bestScore = 0;
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
    private int permanentAttackPower = 0;
    private bool isGunMode = false;
    private bool feverBulletUsed = false; // 피버 중 총 사용 여부

    // UI 위치 저장 (위치 초기화 문제)
    private float turnsTextOriginalY = 0f;
    private bool turnsTextInitialized = false;
    private float attackTextOriginalY = 0f;
    private bool attackTextInitialized = false;


    // DOTween용 이전 값 저장
    private int lastPermanentAttackPower = 0;
    private int lastMergeGauge = 0;
    private int lastFeverTurnsRemaining = 0;

    private int currentHeat = 100;

    private const float CRITICAL_CHANCE = 0.25f;
    private const int CRITICAL_MULTIPLIER = 4;

    private const float COMBO_MULTIPLIER_BASE = 1.2f;
    private int comboCount = 0;


    private ProjectileManager projectileManager;
    private Vector3 lastMergedTilePosition;

    void Start()
    {
        bestScore = PlayerPrefs.GetInt("BestScore", 0);
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
        if (isGameOver || isProcessing || isBossTransitioning) return;

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
        permanentAttackPower = 0; // 추가
        feverBulletUsed = false;
        currentHeat = maxHeat;
        isGunMode = false;
        isBossTransitioning = false;
        isGameOver = false;

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

        StartGame();

    }

    void CheckGaugeAndFever()
    {
        if (isFeverMode)
        {
            if (feverTurnsRemaining <= 0)
            {
                isFeverMode = false;

                // ⭐ UPDATED: 피버 중 총을 쐈으면 0, 안 쐈으면 20 유지
                if (feverBulletUsed)
                {
                    mergeGauge = 0;  // 총 쏨 → 0/40
                    Debug.Log("FEVER END! Shot used, reset to 0/40");
                }
                else
                {
                    mergeGauge = 20;  // 총 안 쏨 → 20/40
                    Debug.Log("FEVER END! No shot, keep 20/40");
                }

                hasBullet = false;
                feverBulletUsed = false; // ⭐ NEW: 리셋
            }
        }
        else
        {
            if (mergeGauge >= GAUGE_FOR_FEVER)
            {
                isFeverMode = true;
                feverBulletUsed = false; // ⭐ NEW: 피버 시작 시 리셋
                feverTurnsRemaining = FEVER_BASE_TURNS;
                hasBullet = false;
                Debug.Log($"FEVER MODE! {FEVER_BASE_TURNS} turns granted!");
            }
            else if (mergeGauge >= GAUGE_FOR_BULLET && !hasBullet)
            {
                hasBullet = true;
                Debug.Log($"Bullet ready! ({mergeGauge}/40)");
            }
        }
        UpdateGunUI();
    }

    void ToggleGunMode()
    {
        if (!hasBullet && (!isFeverMode || feverBulletUsed)) return;

        if (activeTiles.Count <= 1)
        {
            Debug.Log("타일이 1개 이하일 때는 총을 쓸 수 없습니다!");
            return;
        }

        isGunMode = !isGunMode;
        UpdateGunUI();

    }

    void ShootTile()
    {
        // 사격 가능 여부 체크
        if (!hasBullet && (!isFeverMode || feverBulletUsed))
        {
            isGunMode = false;
            UpdateGunUI();
            return;
        }

        if (activeTiles.Count <= 1)
        {
            Debug.Log("타일이 1개 이하일 때는 총을 쓸 수 없습니다!");
            isGunMode = false;
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
            int allTilesSum = GetAllTilesSum(); // 판 위 모든 타일 합
            int baseDamage = allTilesSum + ((int)permanentAttackPower);

            // Choco 보너스: 2배
            if (tileColor == TileColor.Choco)
            {
                baseDamage *= 2;
                Debug.Log($"🔫🍫 Choco 보너스! 데미지 2배!");
            }

            int finalDamage = baseDamage;

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
            int absorbRate = isFeverMode ? 10 : 5; // 피버 중 10%, 평시 5%
            int absorbAmount = Mathf.FloorToInt(allTilesSum * absorbRate / 100f);
            permanentAttackPower += absorbAmount;

            Debug.Log($"💪 공격력 흡수! +{absorbAmount} (총 {permanentAttackPower}) [흡수율: {absorbRate}%]");

            // === 4. 타일 제거 및 공격 ===
            Vector3 tilePos = targetTile.transform.position;
            Vector2Int pos = targetTile.gridPosition;
            tiles[pos.x, pos.y] = null;
            activeTiles.Remove(targetTile);
            Destroy(targetTile.gameObject);

            if (bossManager != null)
            {
                if (projectileManager != null && bossManager.bossImageArea != null)
                {
                    Vector3 bossPos = bossManager.bossImageArea.transform.position;
                    Color bulletColor = isFeverMode ? new Color(1f, 0.3f, 0f) : Color.yellow;

                    projectileManager.FireBulletSalvo(tilePos, bossPos, 1, finalDamage, bulletColor, (damage) =>
                    {
                        bossManager.TakeDamage(damage);
                    });

                    bool isChoco = (tileColor == TileColor.Choco);
                    ShowDamageText(finalDamage, false, true, 1.0f, allTilesSum, permanentAttackPower, isChoco);

                    CameraShake.Instance?.ShakeMedium();
                }
                else
                {
                    bossManager.TakeDamage(finalDamage);
                    bool isChoco = (tileColor == TileColor.Choco);
                    ShowDamageText(finalDamage, false, true, 1.0f, allTilesSum, permanentAttackPower, isChoco);

                }
            }

            // === 5. 게이지 초기화 ===
            if (isFeverMode)
            {
                feverBulletUsed = true; // ⭐ NEW: 피버 중 총 사용 기록
                mergeGauge = 0;
                hasBullet = false;
                Debug.Log("FEVER SHOT! Bullet used, cannot shoot again");
            }
            else
            {
                // 평시 사격 → 잔여 차지 유지 (20을 빼기)
                mergeGauge = Mathf.Max(0, mergeGauge - GAUGE_FOR_BULLET);
                hasBullet = (mergeGauge >= GAUGE_FOR_BULLET);
                Debug.Log($"GUN SHOT! Remaining charge: {mergeGauge}/40");
            }

            isGunMode = false;
            UpdateGunUI();

            if (!CanMove() && !hasBullet && !isFeverMode)
            {
                GameOver();
            }
        }
    }
    int GetAllTilesSum()
    {
        int sum = 0;
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
                // 피버 종료 직후 0/20 표시
                if (mergeGauge == 0)
                {
                    turnsUntilBulletText.text = "0/20";
                }
                else if (mergeGauge < GAUGE_FOR_BULLET)
                {
                    turnsUntilBulletText.text = $"{mergeGauge}/20";
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
            int expectedDamage = GetAllTilesSum() + permanentAttackPower;
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
        int berryMergeCount = 0;

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

                            // 피버 중 머지 시 추가 공격력 +1
                            if (isFeverMode)
                            {
                                permanentAttackPower++;
                                Debug.Log($"FEVER MERGE! +ATK +1 (Total: {permanentAttackPower})");
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
            float comboMultiplier = Mathf.Pow(COMBO_MULTIPLIER_BASE, mergeCountThisTurn);

            comboCount = mergeCountThisTurn;

            if (totalMergedValue > 0 && bossManager != null)
            {
                bool isCritical = Random.value < CRITICAL_CHANCE;
                int baseDamage = Mathf.RoundToInt(totalMergedValue * comboMultiplier);

                baseDamage += permanentAttackPower;

                int damage = isCritical ? baseDamage * CRITICAL_MULTIPLIER : baseDamage;


                if (projectileManager != null && bossManager != null && bossManager.bossImageArea != null)
                {
                    Vector3 bossPos = bossManager.bossImageArea.transform.position;

                    Color laserColor = Color.white;
                    if (isCritical)
                    {
                        laserColor = Color.red;
                    }
                    else if (comboMultiplier > 1.0f)
                    {
                        int comboNum = Mathf.RoundToInt(Mathf.Log(comboMultiplier) / Mathf.Log(1.2f));
                        if (comboNum >= 5)
                            laserColor = new Color(1f, 0f, 1f);
                        else if (comboNum >= 4)
                            laserColor = new Color(1f, 0.3f, 0f);
                        else if (comboNum >= 3)
                            laserColor = new Color(1f, 0.6f, 0f);
                        else if (comboNum >= 2)
                            laserColor = new Color(0.5f, 1f, 0.5f);
                        else
                            laserColor = new Color(1f, 1f, 0.5f);
                    }

                    projectileManager.FireKnifeProjectile(lastMergedTilePosition, bossPos, laserColor, () =>
                    {
                        bossManager.TakeDamage(damage);
                        ShowDamageText(damage, isCritical, false, comboMultiplier);
                        CameraShake.Instance?.ShakeLight();
                    });
                }
                else
                {
                    bossManager.TakeDamage(damage);
                    ShowDamageText(damage, isCritical, false, comboMultiplier);
                }
            }

            // oldHeat는 턴 시작 시 이미 저장됨 (Berry 머지 회복 이전 값)
            currentHeat -= heatDecreasePerTurn;

            if (mergeCountThisTurn > 0)
            {
                int comboIndex = Mathf.Min(mergeCountThisTurn, comboHeatRecover.Length - 1);
                int heatRecovery = comboHeatRecover[comboIndex];
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
            CheckGaugeAndFever(); // CheckFeverMode() → CheckGaugeAndFever()로 변경!


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

    void ShowDamageText(int damage, bool isCritical, bool isGunDamage, float comboMultiplier = 1.0f, int baseTileValue = 0, int gunLevel = 1, bool isChoco = false)
    {
        if (damageTextPrefab == null || damageTextParent == null || hpText == null) return;

        GameObject damageObj = Instantiate(damageTextPrefab, damageTextParent);
        TextMeshProUGUI damageText = damageObj.GetComponent<TextMeshProUGUI>();

        if (damageText != null)
        {
            if (isCritical)
            {
                damageText.text = "CRITICAL!\n-" + damage;
                damageText.color = Color.red;
                damageText.fontSize = 50;
            }
            else if (isGunDamage)
            {
                if (isChoco)
                {
                    damageText.text = $"CHOCO x2\n-{damage}";
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
                if (comboMultiplier > 1.0f)
                {
                    int comboNum = Mathf.RoundToInt(Mathf.Log(comboMultiplier) / Mathf.Log(1.2f));
                    damageText.text = $"{comboNum}x COMBO!\n(x{comboMultiplier:F2})\n-{damage}";

                    if (comboNum >= 5)
                        damageText.color = new Color(1f, 0f, 1f);
                    else if (comboNum >= 4)
                        damageText.color = new Color(1f, 0.3f, 0f);
                    else if (comboNum >= 3)
                        damageText.color = new Color(1f, 0.6f, 0f);
                    else if (comboNum >= 2)
                        damageText.color = new Color(0.5f, 1f, 0.5f);
                    else
                        damageText.color = new Color(1f, 1f, 0.5f);

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

            if (isCritical)
            {
                damageSequence.Insert(0f, damageRect.DOScale(1.3f, 0.2f).SetEase(Ease.OutBack));
                damageSequence.Insert(0.2f, damageRect.DOScale(1f, 0.3f).SetEase(Ease.InOutQuad));
            }
            else
            {
                damageSequence.Insert(0f, damageRect.DOScale(1.2f, 0.15f).SetEase(Ease.OutQuad));
                damageSequence.Insert(0.15f, damageRect.DOScale(1f, 0.15f).SetEase(Ease.InQuad));
            }

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

        if (!CanMove() && !hasBullet && !isFeverMode)
        {
            GameOver();
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

        UpdateGunUI();

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);
    }

    void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.text = score.ToString();

        if (score > bestScore)
        {
            bestScore = score;
            PlayerPrefs.SetInt("BestScore", bestScore);
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