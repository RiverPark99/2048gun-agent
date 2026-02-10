using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class Projectile : MonoBehaviour
{
    public enum ProjectileType
    {
        Knife,      // 칼 공격 (레이저)
        Bullet,     // 총알 (레이저)
        Freeze      // ⭐ v5.0: 얼음 레이저 (Fever Freeze 연출)
    }

    private ProjectileType type;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private System.Action onHitCallback;
    private Image lineImage;

    void Awake()
    {
        lineImage = GetComponent<Image>();
        if (lineImage == null)
        {
            lineImage = gameObject.AddComponent<Image>();
        }
    }

    public void Initialize(ProjectileType projectileType, Vector3 start, Vector3 target, Color laserColor, System.Action onHit = null)
    {
        type = projectileType;
        startPosition = start;
        targetPosition = target;
        onHitCallback = onHit;

        // 즉시 데미지 적용
        onHitCallback?.Invoke();

        // 레이저 라인 생성
        CreateLaserLine(laserColor);
    }

    void CreateLaserLine(Color laserColor)
    {
        RectTransform rect = GetComponent<RectTransform>();
        
        // 타입에 따른 굵기 설정
        if (type == ProjectileType.Bullet)
        {
            lineImage.color = new Color(laserColor.r, laserColor.g, laserColor.b, 0.8f);
            rect.sizeDelta = new Vector2(0f, 8f); // 굵기 8
        }
        else if (type == ProjectileType.Freeze)
        {
            // ⭐ v5.0: Freeze 레이저 - 더 두꺼운 얼음색
            lineImage.color = new Color(laserColor.r, laserColor.g, laserColor.b, 0.85f);
            rect.sizeDelta = new Vector2(0f, 18f); // 굵기 18 (일반 12보다 두꺼움)
        }
        else // Knife
        {
            lineImage.color = new Color(laserColor.r, laserColor.g, laserColor.b, 0.8f);
            rect.sizeDelta = new Vector2(0f, 12f); // 굵기 12
        }

        // 시작점과 끝점 사이의 거리와 각도 계산
        Vector3 direction = targetPosition - startPosition;
        float distance = direction.magnitude;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // 레이저 위치와 회전 설정
        rect.position = startPosition;
        rect.rotation = Quaternion.Euler(0, 0, angle);
        rect.pivot = new Vector2(0f, 0.5f);

        if (type == ProjectileType.Freeze)
        {
            // ⭐ v5.0: Freeze 레이저는 살짝 느리게 + 더 오래 유지
            rect.DOSizeDelta(new Vector2(distance, rect.sizeDelta.y), 0.08f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    // 잠깐 유지 후 페이드아웃
                    lineImage.DOFade(0f, 0.3f)
                        .SetDelay(0.1f)
                        .OnComplete(() =>
                        {
                            Destroy(gameObject);
                        });
                });
        }
        else
        {
            // 기존 레이저 동작
            rect.DOSizeDelta(new Vector2(distance, rect.sizeDelta.y), 0.05f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    lineImage.DOFade(0f, 0.15f)
                        .OnComplete(() =>
                        {
                            Destroy(gameObject);
                        });
                });
        }
    }

    void OnDestroy()
    {
        transform.DOKill();
    }
}
