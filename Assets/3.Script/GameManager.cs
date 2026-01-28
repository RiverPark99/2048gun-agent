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
    
    private Tile[,] tiles;
    private List<Tile> activeTiles = new List<Tile>();
    private int score = 0;
    private int bestScore = 0;
    private float cellSize;
    private bool isProcessing = false;
    
    void Start()
    {
        bestScore = PlayerPrefs.GetInt("BestScore", 0);
        InitializeGrid();
        StartGame();
        
        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);
    }
    
    void Update()
    {
        if (isProcessing) return;
        
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            Move(Vector2Int.down);
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            Move(Vector2Int.up);
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            Move(Vector2Int.left);
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            Move(Vector2Int.right);
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
        UpdateScoreUI();
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
            // �ٽ�: �� �����Ӹ��� obj�� ���� �����ϴ��� Ȯ��
            if (obj == null) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Ease out back ����
            float s = 1.70158f;
            t = t - 1;
            float val = t * t * ((s + 1) * t + s) + 1;

            // ���� ������ �ٽ� �ѹ� üũ (�� ������)
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
                            anyMerged = true; // 합성이 일어났음을 표시
                            
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
                yield return new WaitForSeconds(0.15f); // 각 콤보 단계마다 0.15초 대기
            }
        }
        
        if (moved)
        {
            UpdateScoreUI();
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
        
        if (!CanMove())
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
                // 1. �� ĭ�� �ϳ��� ������ ������ �� ����
                if (tiles[x, y] == null) return true;

                int currentValue = tiles[x, y].value;

                // 2. ������ Ÿ�ϰ� �� (���� ���ų� �� ĭ�̸� �̵� ����)
                if (x < gridSize - 1)
                {
                    if (tiles[x + 1, y] == null || tiles[x + 1, y].value == currentValue)
                        return true;
                }

                // 3. �Ʒ��� Ÿ�ϰ� �� (���� ���ų� �� ĭ�̸� �̵� ����)
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
