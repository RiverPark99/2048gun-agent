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

    // 피버 시스템 (15 머지 → 피버 → 10 머지로 해제)
    private const int MERGES_FOR_FEVER = 15;
    private int bulletCount = 0;
    private int mergeCount = 0; // 전체 머지 카운트 (콤보용)
    private int mixMergeCount = 0; // 믹스 머지 카운트만 (장전용)
    private int feverMergeCount = 0; // 피버 중 머지 카운트
    private bool isGunMode = false;
    private bool isFeverMode = false;
    private bool hasFeverShot = false;

    private int currentHeat = 100;

    private const float CRITICAL_CHANCE = 0.25f;
    private const int CRITICAL_MULTIPLIER = 4;

    private const float COMBO_MULTIPLIER_BASE = 1.2f;
    private int comboCount = 0;

    // 총 레벨 시스템 (총 쏠 때마다 레벨업, 데미지 2배씩)
    private int gunLevel = 1;
    private int gunShotCount = 0;

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
        bulletCount = 0;
        mergeCount = 0;
        mixMergeCount = 0;
        feverMergeCount = 0;
        currentHeat = maxHeat;
        isGunMode = false;
        isBossTransitioning = false;
        isGameOver = false;
        isFeverMode = false;
        hasFeverShot = false;
        gunLevel = 1;
        gunShotCount = 0;

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

        StartGame();
    }

    void CheckFeverMode()
    {
        if (isFeverMode)
        {
            // 피버 중: 10 머지로 피버 해제
            if (feverMergeCount >= 10)
            {
                isFeverMode = false;
                hasFeverShot = false;
                feverMergeCount = 0;
                mixMergeCount = 0;
                Debug.Log("🔥 피버 모드 종료!");
            }
        }
        else
        {
            // 평상시: 15 믹스 머지로 피버 진입
            if (mixMergeCount >= MERGES_FOR_FEVER)
            {
                isFeverMode = true;
                hasFeverShot = true; // 피버 진입 시 1발 가능
                bulletCount = 1;
                feverMergeCount = 0;
                Debug.Log("🔥🔥🔥 FEVER MODE 진입! 🔥🔥🔥");
            }
        }

        UpdateGunUI();
    }

    void ToggleGunMode()
    {
        if (!isFeverMode && bulletCount <= 0) return;
        if (isFeverMode && !hasFeverShot) return;

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
        if (!isFeverMode && bulletCount <= 0)
        {
            isGunMode = false;
            UpdateGunUI();
            return;
        }

        if (isFeverMode && !hasFeverShot)
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
            int tileValue = targetTile.value;
            TileColor tileColor = targetTile.tileColor;

            // 총 레벨에 따른 데미지 배율 (1레벨=1x, 2레벨=2x, 3레벨=4x, 4레벨=8x...)
            float gunMultiplier = Mathf.Pow(2, gunLevel - 1);
            int totalDamage = Mathf.RoundToInt(tileValue * gunMultiplier);

            int colorBonus = 0;
            int healBonus = 0;

            // 초코 색상 or 피버: 데미지 2배
            if (tileColor == TileColor.Choco || isFeverMode)
            {
                colorBonus = totalDamage;
                totalDamage += colorBonus;
                Debug.Log($"🔫⚫ 초코/피버 보너스! +{colorBonus} 추가 데미지!");
            }

            // 베리 색상 or 피버: 회복 2배
            if (tileColor == TileColor.Berry || isFeverMode)
            {
                int baseHeal = gunShotHeatRecover;
                healBonus = baseHeal;
                Debug.Log($"🔫💖 핑크/피버 보너스! +{healBonus} 추가 회복!");
            }

            Vector3 tilePos = targetTile.transform.position;

            Vector2Int pos = targetTile.gridPosition;
            tiles[pos.x, pos.y] = null;
            activeTiles.Remove(targetTile);
            Destroy(targetTile.gameObject);

            if (projectileManager != null && bossManager != null && bossManager.bossImageArea != null)
            {
                Vector3 bossPos = bossManager.bossImageArea.transform.position;
                Color bulletColor = isFeverMode ? new Color(1f, 0.3f, 0f) : Color.yellow;

                projectileManager.FireBulletSalvo(tilePos, bossPos, 1, totalDamage, bulletColor, (damage) =>
                {
                    bossManager.TakeDamage(damage);
                });

                ShowDamageText(totalDamage, false, true, 1.0f, tileValue, gunLevel);
                CameraShake.Instance?.ShakeMedium();
            }
            else
            {
                if (bossManager != null)
                {
                    bossManager.TakeDamage(totalDamage);
                    ShowDamageText(totalDamage, false, true, 1.0f, tileValue, gunLevel);
                }
            }

            // 회복
            RecoverHeat(gunShotHeatRecover);
            if (healBonus > 0)
            {
                RecoverHeat(healBonus);
            }

            // 총 레벨 증가
            gunShotCount++;
            gunLevel = gunShotCount + 1;
            Debug.Log($"🔫 Gun Level UP! Lv.{gunLevel} (데미지 배율: x{Mathf.Pow(2, gunLevel - 1)})");

            bulletCount = 0;
            hasFeverShot = false;

            isGunMode = false;
            UpdateGunUI();

            if (!CanMove() && bulletCount <= 0 && !hasFeverShot)
            {
                GameOver();
            }
        }
    }

    void UpdateGunUI()
    {
        // bulletCountText: "Fever!" or "Lv.X"
        if (bulletCountText != null)
        {
            if (isFeverMode)
            {
                bulletCountText.text = "Fever!";
            }
            else
            {
                bulletCountText.text = $"Lv.{gunLevel}";
            }
        }

        // 총알 갯수 표시: 피버 때 숨김
        if (bulletCountDisplay != null)
        {
            bulletCountDisplay.SetActive(!isFeverMode);
        }

        // 진행도 표시
        if (turnsUntilBulletText != null)
        {
            if (isFeverMode)
            {
                turnsUntilBulletText.text = $"{feverMergeCount}/10";
            }
            else
            {
                turnsUntilBulletText.text = $"{mixMergeCount}/{MERGES_FOR_FEVER}";
            }
        }

        if (progressBarFill != null)
        {
            float progress = isFeverMode ?
                Mathf.Clamp01((float)feverMergeCount / 10f) :
                Mathf.Clamp01((float)mixMergeCount / MERGES_FOR_FEVER);
            progressBarFill.sizeDelta = new Vector2(
                progressBarFill.parent.GetComponent<RectTransform>().rect.width * progress,
                progressBarFill.sizeDelta.y
            );
        }

        if (gunButtonImage != null)
        {
            if (isFeverMode)
                gunButtonImage.color = new Color(1f, 0.3f, 0f);
            else if (isGunMode)
                gunButtonImage.color = new Color(1f, 0.8f, 0.2f);
            else if (bulletCount > 0)
                gunButtonImage.color = new Color(0.2f, 1f, 0.2f);
            else
                gunButtonImage.color = new Color(0.5f, 0.5f, 0.5f);
        }

        if (gunButton != null)
        {
            gunButton.interactable = !isGameOver && (bulletCount > 0 || hasFeverShot) && activeTiles.Count > 1;
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
                            bool isMixMerge = false;

                            if (color1 == TileColor.Choco && color2 == TileColor.Choco)
                            {
                                chocoMergeCount++;

                                int bonusDamage = mergedValue * (chocoMergeDamageMultiplier - 1);
                                totalMergedValue += bonusDamage;

                                Debug.Log($"🍫 CHOCO MERGE! +{bonusDamage} 추가 데미지");
                                targetTile.PlayChocoMergeEffect();
                                isColorBonus = true;
                            }
                            else if (color1 == TileColor.Berry && color2 == TileColor.Berry)
                            {
                                berryMergeCount++;

                                // 베리 머지: 콤보 힐량과 별개로 보너스 힐 적용
                                int bonusHeal = berryMergeBaseHeal * berryMergeHealMultiplier;

                                currentHeat += bonusHeal;
                                if (currentHeat > maxHeat) currentHeat = maxHeat;

                                Debug.Log($"🍓 BERRY MERGE! +{bonusHeal} Heat 즉시 회복 (기본 {berryMergeBaseHeal} x {berryMergeHealMultiplier})");

                                // Heat 회복 텍스트는 턴 종료 시 총합으로 표시

                                targetTile.PlayBerryMergeEffect();
                                isColorBonus = true;
                            }
                            else
                            {
                                // 믹스 머지: 장전 카운트 +1
                                isMixMerge = true;
                                mixMergeCount++;
                                score += mergedValue; // 스코어 2배
                                Debug.Log($"🌈 MIX MERGE! 스코어 2배, 장전 카운트 +1");
                            }

                            if (isColorBonus)
                            {
                                targetTile.MergeWithoutParticle();
                            }
                            else
                            {
                                targetTile.MergeWith(tile);
                            }

                            TileColor newColor = Random.value < 0.5f ? TileColor.Choco : TileColor.Berry;
                            targetTile.SetColor(newColor);

                            merged[nextPos.x, nextPos.y] = true;
                            anyMerged = true;

                            lastMergedTilePosition = targetTile.transform.position;

                            // 전체 머지 카운트 (콤보용 - 모든 머지)
                            mergeCount++;
                            mergeCountThisTurn++;

                            // 피버 중이면 피버 카운트
                            if (isFeverMode)
                            {
                                feverMergeCount++;
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

            UpdateScoreUI();

            // 피버 모드 체크
            CheckFeverMode();

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

    void ShowDamageText(int damage, bool isCritical, bool isGunDamage, float comboMultiplier = 1.0f, int baseTileValue = 0, int gunLevel = 1)
    {
        if (damageTextPrefab == null || damageTextParent == null || hpText == null) return;

        GameObject damageObj = Instantiate(damageTextPrefab, damageTextParent);
        TextMeshProUGUI damageText = damageObj.GetComponent<TextMeshProUGUI>();

        if (damageText != null)
        {
            if (isCritical)
            {
                damageText.text = "CRITICAL! -" + damage;
                damageText.color = Color.red;
                damageText.fontSize = 50;
            }
            else if (isGunDamage)
            {
                if (baseTileValue > 0 && gunLevel > 0)
                {
                    float multiplier = Mathf.Pow(2, gunLevel - 1);
                    damageText.text = $"Lv.{gunLevel} ({baseTileValue} x {multiplier:F0}) -{damage}";
                }
                else
                {
                    damageText.text = "-" + damage;
                }
                damageText.color = Color.yellow;
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

        // 피버 중에는 보스 턴 증가 X
        if (bossManager != null && !isFeverMode)
        {
            bossManager.OnPlayerTurn();
        }

        if (!CanMove() && bulletCount <= 0 && !hasFeverShot)
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