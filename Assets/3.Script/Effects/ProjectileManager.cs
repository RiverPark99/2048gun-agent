// =====================================================
// ProjectileManager.cs
// Projectile 발사 관리 + Object Pool 적용
// =====================================================

using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ProjectileManager : MonoBehaviour
{
    [Header("Projectile Prefabs")]
    public GameObject projectilePrefab;

    [Header("Pool Settings")]
    [SerializeField] private int initialPoolSize = 8;

    private Transform canvas;
    private GameObjectPool _projectilePool;

    void Awake()
    {
        canvas = FindAnyObjectByType<Canvas>()?.transform;

        if (projectilePrefab == null)
            CreateProjectilePrefab();

        // Pool 초기화
        _projectilePool = new GameObjectPool(projectilePrefab, transform, initialPoolSize);
    }

    void CreateProjectilePrefab()
    {
        projectilePrefab = new GameObject("ProjectilePrefab");
        projectilePrefab.AddComponent<RectTransform>();
        projectilePrefab.AddComponent<Image>();
        projectilePrefab.AddComponent<Projectile>();
        projectilePrefab.SetActive(false);
    }

    public void FireKnifeProjectile(Vector3 startPos, Vector3 targetPos, Color laserColor, System.Action onHit = null)
    {
        CreateAndFireProjectile(Projectile.ProjectileType.Knife, startPos, targetPos, laserColor, onHit);
    }

    public void FireFreezeLaser(Vector3 startPos, Vector3 targetPos, Color laserColor, System.Action onHit = null)
    {
        CreateAndFireProjectile(Projectile.ProjectileType.Freeze, startPos, targetPos, laserColor, onHit);
    }

    public void FireBulletSalvo(Vector3 startPos, Vector3 targetPos, int bulletCount, int totalDamage, Color bulletColor, System.Action<int> onEachHit = null)
    {
        StartCoroutine(FireBulletsSequentially(startPos, targetPos, bulletCount, totalDamage, bulletColor, onEachHit));
    }

    IEnumerator FireBulletsSequentially(Vector3 startPos, Vector3 targetPos, int bulletCount, int totalDamage, Color bulletColor, System.Action<int> onEachHit = null)
    {
        int damagePerBullet = totalDamage / bulletCount;
        int remainder = totalDamage % bulletCount;

        for (int i = 0; i < bulletCount; i++)
        {
            int currentDamage = damagePerBullet + (i == bulletCount - 1 ? remainder : 0);

            CreateAndFireProjectile(Projectile.ProjectileType.Bullet, startPos, targetPos, bulletColor, () =>
            {
                onEachHit?.Invoke(currentDamage);
                CameraShake.Instance?.ShakeLight();
            });

            yield return new WaitForSeconds(0.08f);
        }
    }

    void CreateAndFireProjectile(Projectile.ProjectileType type, Vector3 start, Vector3 target, Color laserColor, System.Action onHit = null)
    {
        if (projectilePrefab == null || canvas == null) return;

        // Pool에서 꺼내기
        GameObject projectileObj = _projectilePool.Get(canvas);
        projectileObj.name = $"Projectile_{type}";

        Projectile projectile = projectileObj.GetComponent<Projectile>();
        if (projectile != null)
        {
            // 풀 반환 콜백을 onHit 이후 애니메이션 완료 시점에 연결
            projectile.Initialize(type, start, target, laserColor, onHit,
                returnToPool: () => _projectilePool.Return(projectileObj));
        }
    }

    public Vector3 GetBossPosition() => Vector3.zero;
}
