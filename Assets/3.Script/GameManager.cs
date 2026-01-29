using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

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

    [Header("Damage Text")]
    [SerializeField] private GameObject damageTextPrefab; // 데미지 텍스트 프리팹
    [SerializeField] private Transform damageTextParent; // Canvas

    [Header("Turn System")]
    [SerializeField] private TextMeshProUGUI turnCountText; // 턴 카운트 텍스트
    [SerializeField] private int maxTurnsPerBoss = 30; // 보스당 최대 턴 수

    private Tile[,] tiles;
    private List<Tile> activeTiles = new List<Tile>();
    private int score = 0;
    private int bestScore = 0;
    private float cellSize;
    private bool isProcessing = false;
    private bool isBossTransitioning = false; // 보스 리스폰 중 플래그 추가

    // 총알 시스템
    private const int MAX_BULLETS = 6; // 최대 6발
    private const int MERGES_PER_BULLET = 32; // 32번 합치면 총알 1개
    private int bulletCount = 0;
    private int mergeCount = 0;
    private bool isGunMode = false;

    // 턴 시스템
    private int currentTurn = 0;

    // 크리티컬 시스템
    private const float CRITICAL_CHANCE = 0.25f; // 25% 확률
    private const int CRITICAL_MULTIPLIER = 4; // 4배

    void Start()
    {
        bestScore = PlayerPrefs.GetInt("BestScore", 0);
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
        // 보스 리스폰 중이거나 처리 중이면 인풋 무시
        if (isProcessing || isBossTransitioning) return;

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
        currentTurn = 0;
        isGunMode = false;
        isBossTransitioning = false;

        UpdateScoreUI();
        UpdateGunUI();
        UpdateTurnUI();
        SpawnTile();
        SpawnTile();
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    public void RestartGame()
    {
        foreach (var tile in activeTiles)
        {
            if (tile != null)
                Destroy(tile.gameObject);
        }
        activeTiles.Clear();
        tiles = new Tile[gridSize, gridSize];
        StartGame();

        // 보스도 리셋
        if (bossManager != null)
            bossManager.ResetBoss();
    }

    void CheckBulletReward()
    {
        while (mergeCount >= MERGES_PER_BULLET && bulletCount < MAX_BULLETS)
        {
            bulletCount++;
            mergeCount -= MERGES_PER_BULLET;
            Debug.Log($"총알 획득! 현재 총알: {bulletCount}/{MAX_BULLETS}");
        }

        // 최대치 초과 방지
        if (bulletCount >= MAX_BULLETS)
        {
            bulletCount = MAX_BULLETS;
            mergeCount = 0;
        }

        UpdateGunUI();
    }

    void ToggleGunMode()
    {
        if (bulletCount <= 0) return;

        // 타일이 1개 이하면 총 모드 비활성화
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

        // 타일이 1개 이하면 총을 쏠 수 없음
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
            // 총알 개수에 따른 데미지 배수 계산
            int[] damageMultipliers = { 0, 1, 2, 4, 6, 8, 16 }; // 인덱스 0은 사용 안함
            int totalDamage = damageMultipliers[bulletCount];

            // 타일 파괴
            Vector2Int pos = targetTile.gridPosition;
            Vector3 tileWorldPos = targetTile.transform.position;
            tiles[pos.x, pos.y] = null;
            activeTiles.Remove(targetTile);
            Destroy(targetTile.gameObject);

            // 보스에게 데미지
            if (bossManager != null)
            {
                bossManager.TakeDamage(totalDamage);
                ShowDamageText(totalDamage, false, true, tileWorldPos); // 노란색 총 데미지
            }

            // 모든 총알 소진
            bulletCount = 0;
            currentTurn++; // 턴 증가

            isGunMode = false;
            UpdateGunUI();
            UpdateTurnUI();

            // 턴 제한 체크
            CheckTurnLimit();

            // 게임 오버 체크
            if (!CanMove() && bulletCount <= 0)
            {
                GameOver();
            }
        }
    }

    void UpdateGunUI()
    {
        if (bulletCountText != null)
            bulletCountText.text = $"× {bulletCount}";

        if (turnsUntilBulletText != null)
            turnsUntilBulletText.text = $"{mergeCount}/{MERGES_PER_BULLET}";

        if (progressBarFill != null)
        {
            float progress = Mathf.Clamp01((float)mergeCount / MERGES_PER_BULLET);
            progressBarFill.sizeDelta = new Vector2(
                progressBarFill.parent.GetComponent<RectTransform>().rect.width * progress,
                progressBarFill.sizeDelta.y
            );
        }

        if (gunButtonImage != null)
        {
            if (isGunMode)
                gunButtonImage.color = new Color(1f, 0.8f, 0.2f);
            else if (bulletCount > 0)
                gunButtonImage.color = new Color(0.2f, 1f, 0.2f);
            else
                gunButtonImage.color = new Color(0.5f, 0.5f, 0.5f);
        }

        if (gunButton != null)
        {
            // 타일이 1개 이하거나 총알이 없으면 버튼 비활성화
            gunButton.interactable = bulletCount > 0 && activeTiles.Count > 1;
        }
    }

    void UpdateTurnUI()
    {
        if (turnCountText != null)
            turnCountText.text = $"Turn: {currentTurn}/{maxTurnsPerBoss}";
    }

    void CheckTurnLimit()
    {
        if (currentTurn >= maxTurnsPerBoss)
        {
            Debug.Log("턴 제한 초과! 게임 오버");
            GameOver();
        }
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
        int totalMergedValue = 0; // 이번 턴에 합쳐진 값의 총합

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
                            totalMergedValue += mergedValue; // 합쳐진 값 누적

                            targetTile.MergeWith(tile);
                            merged[nextPos.x, nextPos.y] = true;
                            anyMerged = true;

                            mergeCount++; // 합성 횟수 증가

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
            // 칼 데미지 적용 (합쳐진 값)
            if (totalMergedValue > 0 && bossManager != null)
            {
                // 크리티컬 판정
                bool isCritical = Random.value < CRITICAL_CHANCE;
                int damage = isCritical ? totalMergedValue * CRITICAL_MULTIPLIER : totalMergedValue;

                bossManager.TakeDamage(damage);
                ShowDamageText(damage, isCritical, false, bossManager.transform.position);
            }

            currentTurn++; // 턴 증가
            UpdateScoreUI();
            UpdateTurnUI();
            CheckBulletReward();

            // 턴 제한 체크
            CheckTurnLimit();

            yield return new WaitForSeconds(0.2f);
            AfterMove();
        }
        else
        {
            isProcessing = false;
        }
    }

    void ShowDamageText(int damage, bool isCritical, bool isGunDamage, Vector3 worldPosition)
    {
        if (damageTextPrefab == null || damageTextParent == null) return;

        GameObject damageObj = Instantiate(damageTextPrefab, damageTextParent);
        TextMeshProUGUI damageText = damageObj.GetComponent<TextMeshProUGUI>();

        if (damageText != null)
        {
            if (isCritical)
            {
                damageText.text = "CRITICAL! -" + damage;
                damageText.color = Color.red; // 빨강: 크리티컬
            }
            else if (isGunDamage)
            {
                damageText.text = "-" + damage;
                damageText.color = Color.yellow; // 노랑: 총 데미지
            }
            else
            {
                damageText.text = "-" + damage;
                damageText.color = Color.white; // 흰색: 칼 데미지
            }

            // 월드 좌표를 스크린 좌표로 변환
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPosition);
            damageObj.transform.position = screenPos;

            StartCoroutine(DamageTextAnimation(damageObj));
        }
    }

    System.Collections.IEnumerator DamageTextAnimation(GameObject damageObj)
    {
        float duration = 1.5f;
        float elapsed = 0f;
        Vector3 startPos = damageObj.transform.position;
        CanvasGroup canvasGroup = damageObj.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = damageObj.AddComponent<CanvasGroup>();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;

            damageObj.transform.position = startPos + Vector3.up * (50 * progress);
            canvasGroup.alpha = 1 - progress;

            yield return null;
        }

        Destroy(damageObj);
    }

    void AfterMove()
    {
        SpawnTile();

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
        Debug.Log("Game Over!");
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

    // 보스 처치 시 턴 초기화
    public void OnBossDefeated()
    {
        currentTurn = 0;
        UpdateTurnUI();
    }

    // 보스 리스폰 상태 제어 (BossManager에서 호출)
    public void SetBossTransitioning(bool transitioning)
    {
        isBossTransitioning = transitioning;
        Debug.Log($"보스 리스폰 상태: {transitioning}");
    }
}