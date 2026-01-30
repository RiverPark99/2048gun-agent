using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// GameManager에 발사체 시스템을 추가하는 확장
/// GameManager의 partial class처럼 사용
/// </summary>
public class ProjectileManager : MonoBehaviour
{
    [Header("Projectile Prefabs")]
    public GameObject projectilePrefab; // 발사체 프리팹 (없으면 자동 생성)

    private Transform canvas;
    private Transform bossTransform;

    void Awake()
    {
        // Canvas 찾기
        canvas = FindAnyObjectByType<Canvas>()?.transform;
        
        // 발사체 프리팹이 없으면 생성
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
        
        // 비활성화 (프리팹으로만 사용)
        projectilePrefab.SetActive(false);
    }

    /// <summary>
    /// 타일 위치에서 보스로 레이저 발사 (칼 공격)
    /// </summary>
    public void FireKnifeProjectile(Vector3 startPos, Vector3 targetPos, Color laserColor, System.Action onHit = null)
    {
        CreateAndFireProjectile(Projectile.ProjectileType.Knife, startPos, targetPos, laserColor, onHit);
    }

    /// <summary>
    /// 타일 위치에서 보스로 총알 레이저 발사 (여러 발)
    /// </summary>
    public void FireBulletSalvo(Vector3 startPos, Vector3 targetPos, int bulletCount, int totalDamage, Color bulletColor, System.Action<int> onEachHit = null)
    {
        StartCoroutine(FireBulletsSequentially(startPos, targetPos, bulletCount, totalDamage, bulletColor, onEachHit));
    }

    IEnumerator FireBulletsSequentially(Vector3 startPos, Vector3 targetPos, int bulletCount, int totalDamage, Color bulletColor, System.Action<int> onEachHit = null)
    {
        // 총알당 데미지 계산
        int damagePerBullet = totalDamage / bulletCount;
        int remainder = totalDamage % bulletCount;

        for (int i = 0; i < bulletCount; i++)
        {
            // 남은 데미지를 마지막 총알에 추가
            int currentDamage = damagePerBullet + (i == bulletCount - 1 ? remainder : 0);
            
            // 총알 발사
            CreateAndFireProjectile(Projectile.ProjectileType.Bullet, startPos, targetPos, bulletColor, () =>
            {
                onEachHit?.Invoke(currentDamage);
                
                // 약한 화면 진동
                CameraShake.Instance?.ShakeLight();
            });

            // 연속 발사 간격 (슈슈슉!)
            yield return new WaitForSeconds(0.08f);
        }
    }

    void CreateAndFireProjectile(Projectile.ProjectileType type, Vector3 start, Vector3 target, Color laserColor, System.Action onHit = null)
    {
        if (projectilePrefab == null || canvas == null) return;

        // 발사체 생성
        GameObject projectileObj = Instantiate(projectilePrefab, canvas);
        projectileObj.SetActive(true);
        projectileObj.name = $"Projectile_{type}";

        // Projectile 컴포넌트 초기화
        Projectile projectile = projectileObj.GetComponent<Projectile>();
        if (projectile != null)
        {
            projectile.Initialize(type, start, target, laserColor, onHit);
        }
    }

    /// <summary>
    /// 보스 Transform 설정 (타겟 위치 계산용)
    /// </summary>
    public void SetBossTransform(Transform boss)
    {
        bossTransform = boss;
    }

    public Vector3 GetBossPosition()
    {
        return bossTransform != null ? bossTransform.position : Vector3.zero;
    }
}
