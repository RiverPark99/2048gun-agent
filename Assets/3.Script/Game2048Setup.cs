using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;

public class Game2048Setup : MonoBehaviour
{
    [MenuItem("2048/Setup Game")]
    public static void SetupGame()
    {
        // Canvas 찾기
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("Canvas not found!");
            return;
        }
        
        // Canvas Scaler 설정
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1290, 2796);
        scaler.matchWidthOrHeight = 0.5f;
        
        // Background 생성
        GameObject background = new GameObject("Background");
        background.transform.SetParent(canvas.transform, false);
        Image bgImage = background.AddComponent<Image>();
        bgImage.color = new Color(0.98f, 0.97f, 0.94f, 1f);
        RectTransform bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        
        // Header Panel 생성
        GameObject headerPanel = new GameObject("HeaderPanel");
        headerPanel.transform.SetParent(canvas.transform, false);
        RectTransform headerRect = headerPanel.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = new Vector2(1, 1);
        headerRect.sizeDelta = new Vector2(0, 400);
        headerRect.anchoredPosition = new Vector2(0, -200);
        
        // Title Text 생성
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(headerPanel.transform, false);
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "2048";
        titleText.fontSize = 140;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(0.47f, 0.43f, 0.4f, 1f);
        titleText.alignment = TextAlignmentOptions.Left;
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.05f, 0);
        titleRect.anchorMax = new Vector2(0.4f, 1);
        titleRect.sizeDelta = Vector2.zero;
        
        // Score Container 생성
        GameObject scoreContainer = new GameObject("ScoreContainer");
        scoreContainer.transform.SetParent(headerPanel.transform, false);
        RectTransform scoreContRect = scoreContainer.AddComponent<RectTransform>();
        scoreContRect.anchorMin = new Vector2(0.5f, 0);
        scoreContRect.anchorMax = new Vector2(0.95f, 1);
        scoreContRect.sizeDelta = Vector2.zero;
        
        // Score Panel 생성
        CreateScorePanel(scoreContainer.transform, "ScorePanel", "SCORE", new Vector2(0, 0), new Vector2(0.48f, 1));
        
        // Best Score Panel 생성
        CreateScorePanel(scoreContainer.transform, "BestScorePanel", "BEST", new Vector2(0.52f, 0), new Vector2(1, 1));
        
        // Grid Container 생성
        GameObject gridContainer = new GameObject("GridContainer");
        gridContainer.transform.SetParent(canvas.transform, false);
        RectTransform gridRect = gridContainer.AddComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0.5f, 0.5f);
        gridRect.anchorMax = new Vector2(0.5f, 0.5f);
        gridRect.sizeDelta = new Vector2(1200, 1200);
        gridRect.anchoredPosition = new Vector2(0, -200);
        Image gridBg = gridContainer.AddComponent<Image>();
        gridBg.color = new Color(0.73f, 0.68f, 0.63f, 1f);
        
        // Game Over Panel 생성
        CreateGameOverPanel(canvas.transform);
        
        // GameManager 생성
        GameObject gameManagerObj = new GameObject("GameManager");
        gameManagerObj.transform.SetParent(canvas.transform, false);
        
        // Cell Prefab 생성
        CreateCellPrefab();
        
        // Tile Prefab 생성
        CreateTilePrefab();
        
        Debug.Log("2048 Game Setup Complete!");
        Debug.Log("Next steps:");
        Debug.Log("1. Add the Tile.cs script to Assets/Scripts folder");
        Debug.Log("2. Add the GameManager.cs script to Assets/Scripts folder");
        Debug.Log("3. Attach GameManager script to GameManager object");
        Debug.Log("4. Assign references in GameManager inspector");
    }
    
    static void CreateScorePanel(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.73f, 0.68f, 0.63f, 1f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = anchorMin;
        panelRect.anchorMax = anchorMax;
        panelRect.sizeDelta = Vector2.zero;
        panelRect.offsetMin = new Vector2(10, 10);
        panelRect.offsetMax = new Vector2(-10, -10);
        
        // Label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(panel.transform, false);
        TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.fontSize = 32;
        labelText.color = new Color(0.93f, 0.89f, 0.85f, 1f);
        labelText.alignment = TextAlignmentOptions.Center;
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0, 0.5f);
        labelRect.anchorMax = new Vector2(1, 1);
        labelRect.sizeDelta = Vector2.zero;
        
        // Value
        GameObject valueObj = new GameObject("ValueText");
        valueObj.transform.SetParent(panel.transform, false);
        TextMeshProUGUI valueText = valueObj.AddComponent<TextMeshProUGUI>();
        valueText.text = "0";
        valueText.fontSize = 48;
        valueText.fontStyle = FontStyles.Bold;
        valueText.color = Color.white;
        valueText.alignment = TextAlignmentOptions.Center;
        RectTransform valueRect = valueObj.GetComponent<RectTransform>();
        valueRect.anchorMin = new Vector2(0, 0);
        valueRect.anchorMax = new Vector2(1, 0.5f);
        valueRect.sizeDelta = Vector2.zero;
        
        if (name == "ScorePanel")
        {
            valueObj.name = "ScoreText";
        }
        else
        {
            valueObj.name = "BestScoreText";
        }
    }
    
    static void CreateGameOverPanel(Transform parent)
    {
        GameObject panel = new GameObject("GameOverPanel");
        panel.transform.SetParent(parent, false);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.98f, 0.97f, 0.94f, 0.9f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;
        
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(panel.transform, false);
        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "Game Over!";
        text.fontSize = 100;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(0.47f, 0.43f, 0.4f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.1f, 0.6f);
        textRect.anchorMax = new Vector2(0.9f, 0.8f);
        textRect.sizeDelta = Vector2.zero;
        
        GameObject buttonObj = new GameObject("RestartButton");
        buttonObj.transform.SetParent(panel.transform, false);
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.55f, 0.48f, 0.43f, 1f);
        Button button = buttonObj.AddComponent<Button>();
        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.3f, 0.35f);
        buttonRect.anchorMax = new Vector2(0.7f, 0.45f);
        buttonRect.sizeDelta = Vector2.zero;
        
        GameObject buttonTextObj = new GameObject("Text");
        buttonTextObj.transform.SetParent(buttonObj.transform, false);
        TextMeshProUGUI buttonText = buttonTextObj.AddComponent<TextMeshProUGUI>();
        buttonText.text = "Restart";
        buttonText.fontSize = 60;
        buttonText.fontStyle = FontStyles.Bold;
        buttonText.color = Color.white;
        buttonText.alignment = TextAlignmentOptions.Center;
        RectTransform buttonTextRect = buttonTextObj.GetComponent<RectTransform>();
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.sizeDelta = Vector2.zero;
        
        panel.SetActive(false);
    }
    
    static void CreateCellPrefab()
    {
        GameObject cell = new GameObject("CellPrefab");
        RectTransform cellRect = cell.AddComponent<RectTransform>();
        cellRect.sizeDelta = new Vector2(100, 100);
        Image cellImage = cell.AddComponent<Image>();
        cellImage.color = new Color(0.8f, 0.76f, 0.71f, 0.35f);
        
        PrefabUtility.SaveAsPrefabAsset(cell, "Assets/CellPrefab.prefab");
        DestroyImmediate(cell);
    }
    
    static void CreateTilePrefab()
    {
        GameObject tile = new GameObject("TilePrefab");
        RectTransform tileRect = tile.AddComponent<RectTransform>();
        tileRect.sizeDelta = new Vector2(100, 100);
        
        Image tileImage = tile.AddComponent<Image>();
        tileImage.color = new Color(0.8f, 0.76f, 0.71f, 1f);
        
        GameObject textObj = new GameObject("ValueText");
        textObj.transform.SetParent(tile.transform, false);
        TextMeshProUGUI valueText = textObj.AddComponent<TextMeshProUGUI>();
        valueText.text = "2";
        valueText.fontSize = 72;
        valueText.fontStyle = FontStyles.Bold;
        valueText.color = new Color(0.47f, 0.43f, 0.4f, 1f);
        valueText.alignment = TextAlignmentOptions.Center;
        RectTransform valueRect = textObj.GetComponent<RectTransform>();
        valueRect.anchorMin = Vector2.zero;
        valueRect.anchorMax = Vector2.one;
        valueRect.sizeDelta = Vector2.zero;
        
        PrefabUtility.SaveAsPrefabAsset(tile, "Assets/TilePrefab.prefab");
        DestroyImmediate(tile);
    }
}
