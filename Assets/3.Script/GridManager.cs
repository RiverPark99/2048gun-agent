// =====================================================
// GridManager.cs - v6.0
// Grid, Tile 생성/이동/머지/점수/턴 관리
// =====================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

public class GridManager : MonoBehaviour
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

    [Header("Turn & Stage UI")]
    [SerializeField] private TextMeshProUGUI turnText;
    [SerializeField] private TextMeshProUGUI stageText;

    [Header("References")]
    [SerializeField] private GunSystem gunSystem;
    [SerializeField] private PlayerHPSystem playerHP;
    [SerializeField] private BossBattleSystem bossBattle;
    [SerializeField] private BossManager bossManager;
    [SerializeField] private UnlockManager unlockManager;

    // Grid 데이터
    private Tile[,] tiles;
    private List<Tile> activeTiles = new List<Tile>();
    private float cellSize;

    // 점수
    private long score = 0;
    private long bestScore = 0;

    // 상태
    private bool isProcessing = false;
    private int currentTurn = 0;
    private int comboCount = 0;
    private Vector3 lastMergedTilePosition;



    // ⭐ v6.7: 콤보 데미지 배율 (Inspector에서 밸런싱 가능)
    [Header("Balance")]
    [SerializeField] private float comboMultiplierBase = 1.6f;

    // === 프로퍼티 ===
    public Tile[,] Tiles => tiles;
    public List<Tile> ActiveTiles => activeTiles;
    public float CellSize => cellSize;
    public bool IsProcessing { get => isProcessing; set => isProcessing = value; }
    public int CurrentTurn => currentTurn;
    public int ComboCount => comboCount;
    public long Score => score;
    public RectTransform GridContainer => gridContainer;

    public void Initialize()
    {
        string bestScoreStr = PlayerPrefs.GetString("BestScore", "0");
        if (long.TryParse(bestScoreStr, out long parsedScore))
            bestScore = parsedScore;
        else
            bestScore = 0;

        InitializeGrid();
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

    public void StartNewGame()
    {
        score = 0;
        currentTurn = 0;
        comboCount = 0;

        UpdateScoreUI();
        SpawnTile();
        SpawnTile();
    }

    public void ResetGrid()
    {
        foreach (var tile in activeTiles)
        {
            if (tile != null)
                Destroy(tile.gameObject);
        }
        activeTiles.Clear();
        tiles = new Tile[gridSize, gridSize];
    }

    // === 이동 ===
    public void Move(Vector2Int direction)
    {
        StartCoroutine(MoveCoroutine(direction));
    }

    IEnumerator MoveCoroutine(Vector2Int direction)
    {
        isProcessing = true;
        bool moved = false;
        int totalMergedValue = 0;
        int mergeCountThisTurn = 0;

        int chocoMergeCount = 0;
        int berryMergeCount = 0;
        bool hadBerryMerge = false;

        int oldHeat = playerHP.CurrentHeat;

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
                                // ⭐ v6.5: 초코+초코 = 4배 데미지
                                chocoMergeCount++;

                                int bonusDamage = mergedValue * 3; // 기본 mergedValue + 3배 추가 = 4배
                                totalMergedValue += bonusDamage;

                                if (!gunSystem.IsFeverMode)
                                {
                                    gunSystem.AddMergeGauge(1);
                                    gunSystem.ShowMergeGaugeChange(1, false);
                                }

                                Debug.Log($"CHOCO MERGE! x4 DMG, Gauge +1 ({gunSystem.MergeGauge}/40)");
                                targetTile.PlayChocoMergeEffect();
                                isColorBonus = true;
                            }
                            else if (color1 == TileColor.Berry && color2 == TileColor.Berry)
                            {
                                berryMergeCount++;
                                hadBerryMerge = true;

                                int bonusHeal = playerHP.GetBerryHealAmount();
                                playerHP.AddHeat(bonusHeal);

                                ProjectileManager pm = bossBattle.GetProjectileManager();
                                if (pm != null && playerHP.HeatText != null)
                                {
                                    Vector3 berryPos = targetTile.transform.position;
                                    Vector3 heatUIPos = playerHP.HeatText.transform.position;
                                    Color berryColor = new Color(1f, 0.4f, 0.6f);
                                    pm.FireKnifeProjectile(berryPos, heatUIPos, berryColor, null);
                                }

                                if (!gunSystem.IsFeverMode)
                                {
                                    gunSystem.AddMergeGauge(1);
                                    gunSystem.ShowMergeGaugeChange(1, false);
                                }

                                Debug.Log($"BERRY MERGE! Gauge +1 ({gunSystem.MergeGauge}/40)");
                                targetTile.PlayBerryMergeEffect();
                                isColorBonus = true;
                            }
                            else
                            {
                                // ⭐ v6.5: 믹스머지 = 2배 데미지 + HP 6% 회복
                                int mixHeal = playerHP.GetMixHealAmount();
                                playerHP.AddHeat(mixHeal);
                                totalMergedValue += mergedValue; // 기본 + 1배 추가 = 2배

                                if (!gunSystem.IsFeverMode)
                                {
                                    gunSystem.AddMergeGauge(1);
                                    gunSystem.ShowMergeGaugeChange(1, false);
                                }

                                score += mergedValue;
                                Debug.Log($"MIX MERGE! x2 DMG, HP+{mixHeal}(6%), Gauge +1 ({gunSystem.MergeGauge}/40)");
                            }

                            if (isColorBonus)
                                targetTile.MergeWithoutParticle();
                            else
                            {
                                targetTile.MergeWith(tile);
                                targetTile.PlayMixMergeEffect();
                            }

                            TileColor newColor = (unlockManager != null) ? unlockManager.GetMergeResultColorForStage()
                                : (Random.value < 0.5f ? TileColor.Choco : TileColor.Berry);
                            targetTile.SetColor(newColor);

                            merged[nextPos.x, nextPos.y] = true;
                            anyMerged = true;

                            lastMergedTilePosition = targetTile.transform.position;
                            mergeCountThisTurn++;

                            // Fever merge ATK 증가
                            if (gunSystem.IsFeverMode)
                            {
                                if (!bossManager.IsClearMode())
                                {
                                    gunSystem.AddFeverMergeATK();
                                    Debug.Log($"🔥 FEVER MERGE! +ATK +{gunSystem.FeverMergeIncreaseAtk} (Total: {gunSystem.PermanentAttackPower})");
                                }
                            }

                            // 머지마다 게이지 UI 즉시 업데이트 (Freeze 진입은 AfterMove에서)
                            gunSystem.UpdateGaugeUIOnly();

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
                yield return new WaitForSeconds(0.15f);
        }

        if (moved)
        {
            currentTurn++;
            UpdateTurnUI();

            comboCount = mergeCountThisTurn;

            // Freeze 중 머지 게이지는 추가하지 않음 (AfterMove에서 처리)

            // 보스 데미지 처리
            if (totalMergedValue > 0 && bossManager != null)
            {
                float comboMultiplier = 1.0f;
                if (mergeCountThisTurn > 1)
                    comboMultiplier = Mathf.Pow(comboMultiplierBase, mergeCountThisTurn - 1);

                long baseDamage = (long)Mathf.Floor(totalMergedValue * comboMultiplier);

                // ATK 보너스 추가
                baseDamage += gunSystem.PermanentAttackPower;

                // ⭐ v6.5: Freeze 중 턴별 1.14배율 누적
                if (gunSystem.IsFeverMode)
                {
                    float freezeMultiplier = gunSystem.GetFreezeDamageMultiplier();
                    baseDamage = (long)(baseDamage * freezeMultiplier);
                    Debug.Log($"❄️ Freeze DMG x{freezeMultiplier:F2}");
                }

                long damage = baseDamage;

                // Freeze 총 데미지 누적
                if (gunSystem.IsFeverMode)
                    gunSystem.AddFreezeTotalDamage(damage);

                bossBattle.FireDamageProjectile(lastMergedTilePosition, damage, mergeCountThisTurn, gunSystem.IsFeverMode);
            }

            // Heat 회복
            if (mergeCountThisTurn > 0)
            {
                int comboIndex = Mathf.Min(mergeCountThisTurn, playerHP.ComboHeatRecover.Length - 1);
                int heatRecovery = playerHP.ComboHeatRecover[comboIndex];
                if (hadBerryMerge)
                {
                    heatRecovery *= 2;
                    Debug.Log($"BERRY MERGE BONUS! Heat recovery x2: {heatRecovery}");
                }
                playerHP.AddHeat(heatRecovery);
            }

            playerHP.ClampHeat();

            int netChange = playerHP.CurrentHeat - oldHeat;
            playerHP.UpdateHeatUI();

            if (netChange != 0)
                playerHP.ShowHeatChangeText(netChange);

            // _16: HP 회복 시 HP bar 깠박임 (턴당 1회, 회복량 0이면 미발동)
            if (netChange > 0)
                playerHP.FlashHealGreen();

            // _6: progress bar/text 깠박임 (턴당 1회)
            gunSystem.FlashEndOfTurn(mergeCountThisTurn > 0);

            // 콤보 게이지 보너스 (Freeze 중이 아닐 때만)
            if (!gunSystem.IsFeverMode && mergeCountThisTurn >= 2)
            {
                gunSystem.AddMergeGauge(1);
                gunSystem.ClearFeverPaybackIfNeeded();
                gunSystem.ShowMergeGaugeChange(1, true); // cap 도달 시 내부에서 차단됨
            }

            UpdateScoreUI();
            comboCount = mergeCountThisTurn;

            if (playerHP.CurrentHeat <= 0)
            {
                Debug.Log("히트 고갈! 게임 오버");
                bossBattle.GameOver();
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

    void AfterMove()
    {
        SpawnTile();

        // Freeze 중: 이동 비용 -2, 콤보 보너스 +2*combo, 20/40 도달시 종료
        if (gunSystem.IsFeverMode)
        {
            gunSystem.ProcessFreezeAfterMove(comboCount);
        }

        // ⭐ v6.6: Freeze 진입 체크 — 보스 전환 중이면 리스폰 완료 후 지연 체크
        if (bossBattle.IsBossTransitioning)
            StartCoroutine(gunSystem.DelayedFreezeCheck());
        else if (bossManager.GetCurrentHP() <= 0)
            StartCoroutine(gunSystem.DelayedFreezeCheck()); // 보스 사망 직후
        else
            gunSystem.CheckGaugeAndFever();

        // 보스 턴 진행 (freeze 중에도 Guard ATK는 진행해야 함)
        if (bossManager != null)
        {
            bossManager.OnPlayerTurn();
        }

        if (!CanMove())
        {
            bool hasGun = gunSystem.HasBullet || (gunSystem.IsFeverMode && !gunSystem.FeverBulletUsed);
            if (!hasGun)
            {
                bossBattle.GameOver();
                return;
            }
            // ⭐ v6.4: 이동 불가 + Gun 있으면 긴급 깜박임
            gunSystem.SetEmergencyFlash(true);
        }
        else
        {
            gunSystem.SetEmergencyFlash(false);
        }

        isProcessing = false;
        if (gunSystem.IsGunMode)
            UpdateTileBorders();
    }

    // === Tile 생성 ===
    public void SpawnTile()
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

        TileColor tileColor = (unlockManager != null) ? unlockManager.GetTileColorForStage() 
            : (Random.value < 0.5f ? TileColor.Choco : TileColor.Berry);
        tile.SetColor(tileColor);

        tile.SetGridPosition(pos);
        tile.MoveTo(GetCellPosition(pos.x, pos.y), false);

        tiles[pos.x, pos.y] = tile;
        activeTiles.Add(tile);

        tileObj.transform.localScale = Vector3.zero;
        StartCoroutine(ScaleInAnimation(tileObj));

        if (gunSystem != null && gunSystem.IsGunMode)
            UpdateTileBorders();
    }

    IEnumerator ScaleInAnimation(GameObject obj)
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
            if (obj != null) obj.transform.localScale = Vector3.one * val;
            yield return null;
        }

        if (obj != null) obj.transform.localScale = Vector3.one;
    }

    // === 이동 가능 체크 ===
    public bool CanMove()
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

    // === Tile Top2 보호 ===
    public System.Tuple<int, int> GetTopTwoTileValues()
    {
        if (activeTiles.Count == 0) return new System.Tuple<int, int>(0, 0);

        HashSet<int> uniqueValues = new HashSet<int>();
        foreach (var tile in activeTiles)
        {
            if (tile != null)
                uniqueValues.Add(tile.value);
        }

        List<int> sortedValues = new List<int>(uniqueValues);
        sortedValues.Sort((a, b) => b.CompareTo(a));

        int firstValue = sortedValues.Count > 0 ? sortedValues[0] : 0;
        int secondValue = sortedValues.Count > 1 ? sortedValues[1] : 0;

        return new System.Tuple<int, int>(firstValue, secondValue);
    }

    public void UpdateTileBorders()
    {
        var topTwo = GetTopTwoTileValues();

        foreach (var tile in activeTiles)
        {
            if (tile == null) continue;
            bool isProtected = (tile.value == topTwo.Item1 || tile.value == topTwo.Item2);
            tile.SetProtected(isProtected, !isProtected && gunSystem.IsGunMode);
        }
    }

    public void ClearAllTileBorders()
    {
        foreach (var tile in activeTiles)
        {
            if (tile != null)
                tile.SetProtected(false, false);
        }
    }

    // ⭐ v6.4: Gun 모드 시 큰 타일 2개 어둡게 투명하게
    public void DimProtectedTiles(bool dim)
    {
        var topTwo = GetTopTwoTileValues();
        foreach (var tile in activeTiles)
        {
            if (tile == null) continue;
            bool isProtected = (tile.value == topTwo.Item1 || tile.value == topTwo.Item2);
            Image img = tile.GetComponent<Image>();
            if (img != null)
            {
                if (dim && isProtected)
                    img.color = new Color(img.color.r, img.color.g, img.color.b, 0.4f);
                else
                    img.color = new Color(img.color.r, img.color.g, img.color.b, 1f);
            }
        }
    }

    // === 점수 UI ===
    void UpdateScoreUI()
    {
        // Score/Best UI는 Damage Record로 대체됨 → 더 이상 갱신 안함
        // GunSystem의 currentRecordText / bestRecordText가 대신 사용됨
    }

    // === Turn/Stage UI ===
    // ⭐ v6.4: 이전 스테이지 추적 (DOTween 효과용)
    private int lastDisplayedStage = -1;

    public void UpdateTurnUI()
    {
        if (turnText != null)
            turnText.text = $"Turn: {currentTurn}";

        if (stageText != null && bossManager != null)
        {
            int currentStage = bossManager.GetBossLevel();

            if (bossManager.IsClearMode() && bossManager.GetBossLevel() >= 41)
            {
                stageText.text = "Challenge\nClear";
            }
            else if (bossManager.IsGuardMode() || bossManager.IsClearMode())
            {
                stageText.text = $"Challenge\n{currentStage}/40";
            }
            else if (currentStage <= 40)
            {
                stageText.text = $"Challenge\n{currentStage}/40";

                // ⭐ v6.4: 스테이지 변경 시 DOTween 효과 (Clear 이후는 제외)
                if (currentStage != lastDisplayedStage && lastDisplayedStage >= 0)
                {
                    RectTransform stageRect = stageText.GetComponent<RectTransform>();
                    stageRect.DOKill();
                    stageText.DOKill();

                    float originalY = stageRect.anchoredPosition.y;
                    Color originalColor = stageText.color;

                    Sequence seq = DOTween.Sequence();
                    // 위로 살짝 올람
                    seq.Append(stageRect.DOAnchorPosY(originalY + 10f, 0.15f).SetEase(Ease.OutQuad));
                    // 주황색으로 변경
                    seq.Join(stageText.DOColor(new Color(1f, 0.65f, 0.1f), 0.15f));
                    // 원래 자리로 복귀
                    seq.Append(stageRect.DOAnchorPosY(originalY, 0.2f).SetEase(Ease.InQuad));
                    // 원래 색상으로 복귀
                    seq.Join(stageText.DOColor(originalColor, 0.3f));
                    seq.OnComplete(() => {
                        if (stageRect != null) stageRect.anchoredPosition = new Vector2(stageRect.anchoredPosition.x, originalY);
                        if (stageText != null) stageText.color = originalColor;
                    });
                }
            }
            else
            {
                stageText.text = "Endless";
            }

            lastDisplayedStage = currentStage;
        }

        // ⭐ v5.0: 무한대 보스일 때 Enemy bar 색상
        if (bossManager != null && bossManager.IsInfiniteBoss())
            bossBattle.UpdateInfiniteBossEnemyBarColor();
    }

    // === 위치 계산 ===
    public Vector2 GetCellPosition(int x, int y)
    {
        float gridWidth = gridContainer.rect.width;
        float startX = -gridWidth / 2 + cellSpacing + cellSize / 2;
        float startY = gridWidth / 2 - cellSpacing - cellSize / 2;

        float posX = startX + x * (cellSize + cellSpacing);
        float posY = startY - y * (cellSize + cellSpacing);

        return new Vector2(posX, posY);
    }

    // === 타일 합계 ===
    public long GetAllTilesSum()
    {
        long sum = 0;
        foreach (var tile in activeTiles)
        {
            if (tile != null)
                sum += tile.value;
        }
        return sum;
    }

    // 하위 호환용
    public void ResetInfiniteBossMoveCount() { }
}
