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
    [SerializeField] private Image gunButtonImage; // 총 버튼이 활성화되었는지 시각적으로 표시
    [SerializeField] private RectTransform progressBarFill; // 진행도 바 Fill
    
    [Header("Boss System")]
    [SerializeField] private BossManager bossManager;
    
    private Tile[,] tiles;
    private List<Tile> activeTiles = new List<Tile>();
    private int score = 0;
    private int bestScore = 0;
    private float cellSize;
    private bool isProcessing = false;
    
    // 총알 시스템
    private int bulletCount = 0;
    private int mergeScore = 0; // 현재까지 획득한 점수 (합쳐진 타일 값의 합)
    private int scoreUntilBullet = 0; // 다음 총알까지 필요한 점수
    private bool isGunMode = false; // 총 모드 활성화 여부
    private int currentBulletLevel = 0; // 현재 총알 레벨 (0부터 시작)
    
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
        if (isProcessing) return;
        
        // 총 모드가 아닐 때만 키보드 입력 처리
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
        
        // 총 모드일 때 타일 클릭 처리
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
        mergeScore = 0;
        currentBulletLevel = 0;
        scoreUntilBullet = CalculateNextBulletScore(0); // 첫 총알은 64점
        isGunMode = false;
        
        UpdateScoreUI();
        UpdateGunUI();
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
    }
    
    // 총알 획득에 필요한 점수는 항상 64점 고정
    int CalculateNextBulletScore(int bulletNumber)
    {
        return 64;
    }
    
    void CheckBulletReward()
    {
        while (mergeScore >= scoreUntilBullet)
        {
            bulletCount++;
            currentBulletLevel++;
            mergeScore -= scoreUntilBullet; // 총알 획득 후 진행도 초기화 (0/64로 리셋)
            // scoreUntilBullet은 항상 64로 고정
            Debug.Log($"총알 획득! 현재 총알: {bulletCount}, 진행도: {mergeScore}/{scoreUntilBullet}");
        }
        UpdateGunUI();
    }
    
    void ToggleGunMode()
    {
        if (bulletCount <= 0) return;
        
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
        
        // Canvas의 RenderMode에 따라 다르게 처리
        Canvas canvas = gridContainer.GetComponentInParent<Canvas>();
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main;
        
        // 마우스 위치를 월드 좌표로 변환
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            gridContainer, 
            Input.mousePosition, 
            cam, 
            out localPoint
        );
        
        // 클릭한 위치에 있는 타일 찾기
        Tile targetTile = null;
        float minDistance = cellSize / 2; // 타일 크기의 절반 이내
        
        Debug.Log($"Gun Mode Click - LocalPoint: {localPoint}, CellSize: {cellSize}");
        
        foreach (var tile in activeTiles)
        {
            if (tile == null) continue;
            
            RectTransform tileRect = tile.GetComponent<RectTransform>();
            float distance = Vector2.Distance(localPoint, tileRect.anchoredPosition);
            
            Debug.Log($"Tile {tile.value} at {tileRect.anchoredPosition}, Distance: {distance}");
            
            if (distance < minDistance)
            {
                minDistance = distance;
                targetTile = tile;
            }
        }
        
        if (targetTile != null)
        {
            bulletCount--;
            int damage = targetTile.value; // 타일 값이 데미지
            
            // 총으로 쏘면 블록 즉시 파괴
            Vector2Int pos = targetTile.gridPosition;
            tiles[pos.x, pos.y] = null;
            activeTiles.Remove(targetTile);
            Destroy(targetTile.gameObject);
            
            // 보스에게 데미지 전달
            if (bossManager != null)
            {
                bossManager.TakeDamage(damage);
                Debug.Log($"보스에게 {damage} 데미지!");
            }
            
            isGunMode = false;
            UpdateGunUI();
            
            // 게임 오버 체크 (총알도 없고 움직일 수 없으면 게임 오버)
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
            turnsUntilBulletText.text = $"{mergeScore}/{scoreUntilBullet}";
        
        // 진행도 바 업데이트
        if (progressBarFill != null)
        {
            float progress = Mathf.Clamp01((float)mergeScore / scoreUntilBullet);
            progressBarFill.sizeDelta = new Vector2(progressBarFill.parent.GetComponent<RectTransform>().rect.width * progress, progressBarFill.sizeDelta.y);
        }
        
        // 총 버튼 시각적 표시
        if (gunButtonImage != null)
        {
            if (isGunMode)
            {
                gunButtonImage.color = Color.yellow; // 활성화: 노란색
            }
            else if (bulletCount > 0)
            {
                gunButtonImage.color = Color.white; // 사용 가능: 흰색
            }
            else
            {
                gunButtonImage.color = Color.gray; // 사용 불가: 회색
            }
        }
        
        // 총 버튼 활성화/비활성화
        if (gunButton != null)
        {
            gunButton.interactable = bulletCount > 0;
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

            // Ease out back
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
    
    // Move를 코루틴으로 변경하여 애니메이션을 볼 수 있게 함
    System.Collections.IEnumerator MoveCoroutine(Vector2Int direction)
    {
        isProcessing = true;
        bool moved = false;
        
        // 콤보를 위해 합성이 일어날 때까지 반복
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
                            score += tile.value * 2;
                            targetTile.MergeWith(tile);
                            merged[nextPos.x, nextPos.y] = true;
                            anyMerged = true;
                            
                            // 합성 점수 증가 (합쳐진 타일의 값)
                            mergeScore += tile.value * 2;
                            
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
            
            // 합성이 일어났다면 애니메이션을 보기 위해 잠시 대기
            if (anyMerged)
            {
                yield return new WaitForSeconds(0.15f);
            }
        }
        
        if (moved)
        {
            UpdateScoreUI();
            CheckBulletReward(); // 총알 획득 체크
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
        
        // 게임 오버 체크 (총알도 없고 움직일 수 없으면 게임 오버)
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
}
