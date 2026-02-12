using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class Projectile : MonoBehaviour
{
    public enum ProjectileType
    {
        Knife,
        Bullet,
        Freeze
    }

    private ProjectileType type;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private System.Action onHitCallback;
    private Image lineImage;

    void Awake()
    {
        lineImage = GetComponent<Image>();
        if (lineImage == null) lineImage = gameObject.AddComponent<Image>();
    }

    public void Initialize(ProjectileType projectileType, Vector3 start, Vector3 target, Color laserColor, System.Action onHit = null)
    {
        type = projectileType;
        startPosition = start;
        targetPosition = target;
        onHitCallback = onHit;

        onHitCallback?.Invoke();
        CreateLaserLine(laserColor);
    }

    void CreateLaserLine(Color laserColor)
    {
        RectTransform rect = GetComponent<RectTransform>();

        if (type == ProjectileType.Bullet)
        {
            lineImage.color = new Color(laserColor.r, laserColor.g, laserColor.b, 0.8f);
            rect.sizeDelta = new Vector2(0f, 8f);
        }
        else if (type == ProjectileType.Freeze)
        {
            lineImage.color = new Color(laserColor.r, laserColor.g, laserColor.b, 0.85f);
            rect.sizeDelta = new Vector2(0f, 18f);
        }
        else
        {
            lineImage.color = new Color(laserColor.r, laserColor.g, laserColor.b, 0.8f);
            rect.sizeDelta = new Vector2(0f, 12f);
        }

        // ⭐ v6.3: Canvas 좌표 기반으로 레이저 거리/각도 계산
        // world position → Canvas 로컬 좌표 변환
        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
        Camera cam = (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : Camera.main;

        Vector2 localStart, localEnd;

        if (canvasRect != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, RectTransformUtility.WorldToScreenPoint(cam, startPosition), cam, out localStart);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, RectTransformUtility.WorldToScreenPoint(cam, targetPosition), cam, out localEnd);
        }
        else
        {
            localStart = startPosition;
            localEnd = targetPosition;
        }

        Vector2 direction = localEnd - localStart;
        float distance = direction.magnitude;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // RectTransform을 Canvas 로컬 좌표에 배치
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = localStart;
        rect.localRotation = Quaternion.Euler(0, 0, angle);
        rect.pivot = new Vector2(0f, 0.5f);

        if (type == ProjectileType.Freeze)
        {
            rect.DOSizeDelta(new Vector2(distance, rect.sizeDelta.y), 0.08f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    lineImage.DOFade(0f, 0.3f).SetDelay(0.1f)
                        .OnComplete(() => Destroy(gameObject));
                });
        }
        else
        {
            rect.DOSizeDelta(new Vector2(distance, rect.sizeDelta.y), 0.05f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    lineImage.DOFade(0f, 0.15f)
                        .OnComplete(() => Destroy(gameObject));
                });
        }
    }

    void OnDestroy()
    {
        transform.DOKill();
    }
}
