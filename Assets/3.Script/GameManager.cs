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
    [SerializeField] private TextMeshProUGUI bulletCountText;
    [SerializeField] private TextMeshProUGUI turnsUntilBulletText;
    [SerializeField] private Image gunButtonImage;
    [SerializeField] private RectTransform progressBarFill;

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
    //[SerializeField] private int bossDefeatHeatRecover = 999;
    [SerializeField] private int bossDefeatMaxHeatIncrease = 20;
    [SerializeField] private int gunShotHeatRecover = 8;
    [SerializeField] private float heatAnimationDuration = 0.3f;

    [Header("색상 조합 보너스")]
    [SerializeField] private int blackMergeDamageMultiplier = 4;
    [SerializeField] private int pinkMergeHealMultiplier = 4;

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

    private const int MAX_BULLETS = 6;
    private const int MERGES_PER_BULLET = 10;
    private const int MERGES_PER_BULLET_MIX = 30;
    private const int MAX_BULLETS_FEVER = 2;
    private int bulletCount = 0;
    private int mergeCount = 0;
    private bool isGunMode = false;
    private bool isFeverMode = false;

    private int currentHeat = 100;

    private const float CRITICAL_CHANCE = 0.25f;
    private const int CRITICAL_MULTIPLIER = 4;

    private const float COMBO_MULTIPLIER_BASE = 1.2f;
    private int comboCount = 0;

    private float[] gunDamageMultipliers = { 0f, 1f, 2f, 3f, 6f, 8f, 16f };

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
        currentHeat = maxHeat;
        isGunMode = false;
        isBossTransitioning = false;
        isGameOver = false;
        isFeverMode = false;

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

    void CheckBulletReward()
    {
        int oldBulletCount = bulletCount;

        if (mergeCount >= MERGES_PER_BULLET_MIX)
        {
            int bulletsToAdd = mergeCount / MERGES_PER_BULLET_MIX;
            mergeCount %= MERGES_PER_BULLET_MIX;

            bulletCount += bulletsToAdd;

            if (bulletCount >= MAX_BULLETS_FEVER)
            {
                bulletCount = MAX_BULLETS_FEVER;
                mergeCount = 0;

                if (!isFeverMode)
                {
                    isFeverMode = true;
                    Debug.Log("🔥🔥🔥 FEVER MODE! 🔥🔥🔥");
                }
            }

            Debug.Log($"총알 획득! 현재 총알: {bulletCount}/{MAX_BULLETS_FEVER} (FEVER MODE)");
        }

        if (bulletCount > oldBulletCount && bossManager != null)
        {
            bossManager.ResetTurnCount();
        }

        UpdateGunUI();
    }

    void ToggleGunMode()
    {
        if (bulletCount <= 0) return;

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
        if (bulletCount <= 0)
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
            float damageMultiplier = gunDamageMultipliers[bulletCount];
            int totalDamage = Mathf.RoundToInt(tileValue * damageMultiplier);

            int colorBonus = 0;
            if (tileColor == TileColor.Black)
            {
                colorBonus = totalDamage;
                totalDamage += colorBonus;
                Debug.Log($"🔫⚫ 검정 블록 파괴! +{colorBonus} 추가 데미지!");
            }
            else if (tileColor == TileColor.Pink)
            {
                int baseHeal = gunShotHeatRecover;
                colorBonus = baseHeal;
                Debug.Log($"🔫💖 핑크 블록 파괴! +{colorBonus} 추가 회복!");
            }

            Vector3 tilePos = targetTile.transform.position;

            Vector2Int pos = targetTile.gridPosition;
            tiles[pos.x, pos.y] = null;
            activeTiles.Remove(targetTile);
            Destroy(targetTile.gameObject);

            if (projectileManager != null && bossManager != null && bossManager.bossImageArea != null)
            {
                Vector3 bossPos = bossManager.bossImageArea.transform.position;
                int savedBulletCount = bulletCount;

                Color bulletColor = Color.yellow;

                projectileManager.FireBulletSalvo(tilePos, bossPos, savedBulletCount, totalDamage, bulletColor, (damage) =>
                {
                    bossManager.TakeDamage(damage);
                });

                ShowDamageText(totalDamage, false, true, 1.0f, tileValue, savedBulletCount);
                CameraShake.Instance?.ShakeMedium();
            }
            else
            {
                if (bossManager != null)
                {
                    bossManager.TakeDamage(totalDamage);
                    ShowDamageText(totalDamage, false, true, 1.0f, tileValue, bulletCount);
                }
            }

            RecoverHeat(gunShotHeatRecover);

            if (tileColor == TileColor.Pink && colorBonus > 0)
            {
                RecoverHeat(colorBonus);
            }

            bulletCount = 0;

            if (isFeverMode)
            {
                isFeverMode = false;
                Debug.Log("피버 모드 종료!");
            }

            isGunMode = false;
            UpdateGunUI();

            if (!CanMove() && bulletCount <= 0)
            {
                GameOver();
            }
        }
    }

    void UpdateGunUI()
    {
        if (bulletCountText != null)
            bulletCountText.text = isFeverMode ? $"🔥 {bulletCount}" : $"× {bulletCount}";

        if (turnsUntilBulletText != null)
            turnsUntilBulletText.text = $"{mergeCount}/{MERGES_PER_BULLET_MIX}";

        if (progressBarFill != null)
        {
            float progress = Mathf.Clamp01((float)mergeCount / MERGES_PER_BULLET_MIX);
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
            gunButton.interactable = !isGameOver && bulletCount > 0 && activeTiles.Count > 1;
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

        TileColor randomColor = Random.value < 0.5f ? TileColor.Black : TileColor.Pink;
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

        int blackMergeCount = 0;
        int pinkMergeCount = 0;

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

                            if (color1 == TileColor.Black && color2 == TileColor.Black)
                            {
                                blackMergeCount++;

                                int bonusDamage = mergedValue * (blackMergeDamageMultiplier - 1);
                                totalMergedValue += bonusDamage;

                                Debug.Log($"⚫ BLACK MERGE! +{bonusDamage} 추가 데미지 (합체값: {mergedValue})");
                                targetTile.PlayBlackMergeEffect();
                                isColorBonus = true;
                            }
                            else if (color1 == TileColor.Pink && color2 == TileColor.Pink)
                            {
                                pinkMergeCount++;

                                int baseHeal = Mathf.RoundToInt(mergedValue * 0.1f);
                                int bonusHeal = baseHeal * (pinkMergeHealMultiplier - 1);

                                currentHeat += bonusHeal;
                                if (currentHeat > maxHeat) currentHeat = maxHeat;

                                Debug.Log($"💖 PINK MERGE! +{bonusHeal} Heat 즉시 회복 (합체값: {mergedValue})");
                                targetTile.PlayPinkMergeEffect();
                                isColorBonus = true;
                            }
                            else
                            {
                                isMixMerge = true;
                                score += mergedValue;
                                Debug.Log($"🌈 MIX MERGE! 스코어 2배! +{mergedValue}");
                            }

                            if (isColorBonus)
                            {
                                targetTile.MergeWithoutParticle();
                            }
                            else
                            {
                                targetTile.MergeWith(tile);
                            }

                            TileColor newColor = Random.value < 0.5f ? TileColor.Black : TileColor.Pink;
                            targetTile.SetColor(newColor);

                            merged[nextPos.x, nextPos.y] = true;
                            anyMerged = true;

                            lastMergedTilePosition = targetTile.transform.position;

                            if (isMixMerge)
                            {
                                mergeCount += 3;
                            }
                            else
                            {
                                mergeCount += 1;
                            }
                            mergeCountThisTurn++;

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

            int oldHeat = currentHeat;
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
            CheckBulletReward();

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

    void ShowDamageText(int damage, bool isCritical, bool isGunDamage, float comboMultiplier = 1.0f, int baseTileValue = 0, int bulletCount = 0)
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
                if (baseTileValue > 0 && bulletCount > 0)
                {
                    float multiplier = gunDamageMultipliers[bulletCount];
                    damageText.text = $"({baseTileValue} x {multiplier:F0}) -{damage}";
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

        if (bossManager != null)
        {
            bossManager.OnPlayerTurn();
        }

        if (!CanMove() && bulletCount <= 0)
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