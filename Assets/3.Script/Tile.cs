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
    private float moveSpeed = 12f; // 더 천천히 이동
    
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
        StartCoroutine(PopAnimation()); // ScaleAnimation에서 PopAnimation으로 변경
    }
    
    // 새로운 "펑!" 터지는 애니메이션
    private System.Collections.IEnumerator PopAnimation()
    {
        float duration = 0.3f; // 0.1에서 0.3으로 증가 (더 눈에 띄게)
        float elapsed = 0f;
        
        // 먼저 작아졌다가
        float shrinkDuration = duration * 0.3f;
        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / shrinkDuration;
            transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.8f, t);
            yield return null;
        }
        
        elapsed = 0f;
        
        // 크게 튀어나왔다가
        float popDuration = duration * 0.4f;
        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / popDuration;
            // Ease out elastic 효과
            float s = 1.70158f * 1.525f;
            t = t - 1;
            float val = t * t * ((s + 1) * t + s) + 1;
            transform.localScale = Vector3.Lerp(Vector3.one * 0.8f, Vector3.one * 1.2f, val);
            yield return null;
        }
        
        elapsed = 0f;
        
        // 원래 크기로 돌아옴
        float returnDuration = duration * 0.3f;
        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / returnDuration;
            transform.localScale = Vector3.Lerp(Vector3.one * 1.2f, Vector3.one, t);
            yield return null;
        }
        
        transform.localScale = Vector3.one;
    }
    
    private void UpdateAppearance()
    {
        int colorIndex = Mathf.Min((int)Mathf.Log(value, 2) - 1, tileColors.Length - 1);
        background.color = tileColors[colorIndex];
        
        valueText.color = value <= 4 ? new Color(0.47f, 0.43f, 0.40f) : Color.white;
    }
}
