// =====================================================
// Projectile.cs
// 레이저/총알 발사체 시각 효과
// Object Pool 지원 — Destroy 대신 returnToPool 콜백 사용
// =====================================================

using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class Projectile : MonoBehaviour
{
    public enum ProjectileType { Knife, Bullet, Freeze }

    private ProjectileType type;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private System.Action onHitCallback;
    private System.Action returnToPool;     // ← 풀 반환 콜백
    private Image lineImage;

    void Awake()
    {
        lineImage = GetComponent<Image>();
        if (lineImage == null) lineImage = gameObject.AddComponent<Image>();
    }

    public void Initialize(
        ProjectileType projectileType,
        Vector3 start,
        Vector3 target,
        Color laserColor,
        System.Action onHit = null,
        System.Action returnToPool = null)
    {
        type              = projectileType;
        startPosition     = start;
        targetPosition    = target;
        onHitCallback     = onHit;
        this.returnToPool = returnToPool;

        // DOTween 잔여 킬 (풀에서 재사용 시 이전 tween 방지)
        transform.DOKill();
        GetComponent<RectTransform>()?.DOKill();

        // 알파 초기화
        if (lineImage != null)
            lineImage.color = new Color(lineImage.color.r, lineImage.color.g, lineImage.color.b, 1f);

        onHitCallback?.Invoke();
        CreateLaserLine(laserColor);
    }

    // 풀 반환 or 파괴
    void ReturnOrDestroy()
    {
        if (returnToPool != null)
            returnToPool.Invoke();
        else
            Destroy(gameObject);
    }

    float GetCanvasScaleRatio()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return 1f;
        Canvas root = canvas.rootCanvas;
        if (root == null) return 1f;
        RectTransform canvasRect = root.GetComponent<RectTransform>();
        if (canvasRect == null) return 1f;
        return canvasRect.rect.width / 1290f;
    }

    void CreateLaserLine(Color laserColor)
    {
        RectTransform rect = GetComponent<RectTransform>();
        float scale = GetCanvasScaleRatio();

        if (type == ProjectileType.Bullet)
            lineImage.color = new Color(laserColor.r, laserColor.g, laserColor.b, 0.8f);
        else if (type == ProjectileType.Freeze)
            lineImage.color = new Color(laserColor.r, laserColor.g, laserColor.b, 0.85f);
        else
            lineImage.color = new Color(laserColor.r, laserColor.g, laserColor.b, 0.8f);

        float thickness = type == ProjectileType.Freeze ? 24f * scale
                        : type == ProjectileType.Bullet  ? 12f * scale
                        : 18f * scale;
        rect.sizeDelta = new Vector2(0f, thickness);

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
            localEnd   = targetPosition;
        }

        Vector2 direction = localEnd - localStart;
        float distance    = direction.magnitude;
        float angle       = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        rect.anchorMin        = new Vector2(0.5f, 0.5f);
        rect.anchorMax        = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = localStart;
        rect.localRotation    = Quaternion.Euler(0, 0, angle);
        rect.pivot            = new Vector2(0f, 0.5f);

        if (type == ProjectileType.Freeze)
        {
            rect.DOSizeDelta(new Vector2(distance, rect.sizeDelta.y), 0.08f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                    lineImage.DOFade(0f, 0.3f).SetDelay(0.1f)
                        .OnComplete(ReturnOrDestroy));
        }
        else
        {
            rect.DOSizeDelta(new Vector2(distance, rect.sizeDelta.y), 0.05f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                    lineImage.DOFade(0f, 0.15f)
                        .OnComplete(ReturnOrDestroy));
        }
    }

    void OnDisable()
    {
        // 풀 반환 시: DOTween 잔여 정리 + 시각적 상태 완전 초기화
        transform.DOKill();
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.DOKill();
            rt.sizeDelta        = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.localRotation    = Quaternion.identity;
            rt.pivot            = new Vector2(0.5f, 0.5f);
        }
        // alpha 완전 복원 (DOFade가 0으로 만들어놓은 채 반환될 수 있음)
        if (lineImage != null)
        {
            Color c = lineImage.color;
            c.a = 1f;
            lineImage.color = c;
        }
    }
}
