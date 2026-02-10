using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ProjectileManager : MonoBehaviour
{
    [Header("Projectile Prefabs")]
    public GameObject projectilePrefab;

    private Transform canvas;
    private Transform bossTransform;

    void Awake()
    {
        canvas = FindAnyObjectByType<Canvas>()?.transform;
        
        if (projectilePrefab == null)
        {
            CreateProjectilePrefab();
        }
    }

    void CreateProjectilePrefab()
    {
        projectilePrefab = new GameObject("ProjectilePrefab");
        projectilePrefab.AddComponent<RectTransform>();
        projectilePrefab.AddComponent<Projectile>();
        
        projectilePrefab.SetActive(false);
    }

    public void FireKnifeProjectile(Vector3 startPos, Vector3 targetPos, Color laserColor, System.Action onHit = null)
    {
        CreateAndFireProjectile(Projectile.ProjectileType.Knife, startPos, targetPos, laserColor, onHit);
    }

    // ⭐ v5.0: Freeze 레이저 (두꺼운 얼음색 레이저)
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

        GameObject projectileObj = Instantiate(projectilePrefab, canvas);
        projectileObj.SetActive(true);
        projectileObj.name = $"Projectile_{type}";

        Projectile projectile = projectileObj.GetComponent<Projectile>();
        if (projectile != null)
        {
            projectile.Initialize(type, start, target, laserColor, onHit);
        }
    }

    public void SetBossTransform(Transform boss)
    {
        bossTransform = boss;
    }

    public Vector3 GetBossPosition()
    {
        return bossTransform != null ? bossTransform.position : Vector3.zero;
    }
}
