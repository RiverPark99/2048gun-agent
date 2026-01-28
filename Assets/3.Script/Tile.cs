using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Tile : MonoBehaviour
{
    public int value;
    public Vector2Int gridPosition;
    
    [SerializeField] private Image background;
    [SerializeField] private TextMeshProUGUI valueText;
    
    private RectTransform rectTransform;
    private Vector2 targetPosition;
    private bool isMoving = false;
    private float moveSpeed = 20f;
    
    private Color[] tileColors = new Color[]
    {
        new Color(0.8f, 0.76f, 0.71f),  // 2
        new Color(0.93f, 0.89f, 0.85f), // 4
        new Color(0.95f, 0.69f, 0.47f), // 8
        new Color(0.96f, 0.58f, 0.39f), // 16
        new Color(0.96f, 0.49f, 0.37f), // 32
        new Color(0.96f, 0.37f, 0.23f), // 64
        new Color(0.93f, 0.81f, 0.45f), // 128
        new Color(0.93f, 0.80f, 0.38f), // 256
        new Color(0.93f, 0.78f, 0.31f), // 512
        new Color(0.93f, 0.77f, 0.25f), // 1024
        new Color(0.93f, 0.76f, 0.18f), // 2048
    };
    
    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }
    
    void Update()
    {
        if (isMoving)
        {
            rectTransform.anchoredPosition = Vector2.Lerp(
                rectTransform.anchoredPosition, 
                targetPosition, 
                Time.deltaTime * moveSpeed
            );
            
            if (Vector2.Distance(rectTransform.anchoredPosition, targetPosition) < 0.1f)
            {
                rectTransform.anchoredPosition = targetPosition;
                isMoving = false;
            }
        }
    }
    
    public void SetValue(int newValue)
    {
        value = newValue;
        valueText.text = value.ToString();
        UpdateAppearance();
    }
    
    public void SetGridPosition(Vector2Int pos)
    {
        gridPosition = pos;
    }
    
    public void MoveTo(Vector2 position, bool animate = true)
    {
        targetPosition = position;
        if (animate)
        {
            isMoving = true;
        }
        else
        {
            rectTransform.anchoredPosition = position;
        }
    }
    
    public void MergeWith(Tile other)
    {
        SetValue(value * 2);
        StartCoroutine(ScaleAnimation());
    }
    
    private System.Collections.IEnumerator ScaleAnimation()
    {
        float duration = 0.1f;
        float elapsed = 0f;
        Vector3 startScale = Vector3.one * 0.9f;
        Vector3 endScale = Vector3.one;
        
        transform.localScale = startScale;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Ease out back
            float s = 1.70158f;
            t = t - 1;
            float val = t * t * ((s + 1) * t + s) + 1;
            transform.localScale = Vector3.Lerp(startScale, endScale, val);
            yield return null;
        }
        
        transform.localScale = endScale;
    }
    
    private void UpdateAppearance()
    {
        int colorIndex = Mathf.Min((int)Mathf.Log(value, 2) - 1, tileColors.Length - 1);
        background.color = tileColors[colorIndex];
        
        valueText.color = value <= 4 ? new Color(0.47f, 0.43f, 0.40f) : Color.white;
    }
}
