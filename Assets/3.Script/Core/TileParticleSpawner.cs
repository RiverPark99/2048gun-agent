// =====================================================
// TileParticleSpawner.cs
// Tile 머지/파괴 파티클 생성 전담 컴포넌트
// Tile.cs에서 분리 — 로직 동일, 파티클 책임만 담당
// =====================================================

using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(RectTransform))]
public class TileParticleSpawner : MonoBehaviour
{
    [Header("파티클 색상")]
    [SerializeField] private Color berryParticleColor  = new Color(1f, 0.5f, 0.65f);
    [SerializeField] private Color chocoParticleColor  = new Color(0.55f, 0.40f, 0.28f);
    [SerializeField] private Color mergeParticleColor  = new Color(1f, 0.8f, 0.2f);
    [SerializeField] private Color gunParticleColor1   = new Color(1f, 0.84f, 0f);
    [SerializeField] private Color gunParticleColor2   = Color.white;

    private RectTransform rectTransform;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    // ── 적응형 수치 계산 (ParticleScaler 사용) ──

    float AdaptiveSize(float sizeRatio, float correction)
    {
        float tileSize = rectTransform != null
            ? Mathf.Max(rectTransform.rect.width, rectTransform.rect.height)
            : 100f;
        return tileSize * sizeRatio / correction;
    }

    float AdaptiveSpeed()
    {
        if (rectTransform == null) return 60f;
        return Mathf.Max(rectTransform.rect.width, rectTransform.rect.height) * 0.5f;
    }

    float AdaptiveRadius()
    {
        if (rectTransform == null) return 8f;
        return Mathf.Max(rectTransform.rect.width, rectTransform.rect.height) * 0.08f;
    }

    // ── 내부 파티클 생성 ──

    void SpawnParticle(Vector2 anchoredPos, Color color, float sizeRatio, float lifetime, float correction)
    {
        GameObject obj = new GameObject("MergeParticle");
        obj.transform.SetParent(transform.parent, false);

        RectTransform rt  = obj.AddComponent<RectTransform>();
        rt.anchorMin       = new Vector2(0.5f, 0.5f);
        rt.anchorMax       = new Vector2(0.5f, 0.5f);
        rt.pivot           = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = Vector2.zero;

        ParticleSystem ps = obj.AddComponent<ParticleSystem>();
        ParticleScaler.ApplyMergeParticleSettings(
            ps,
            color,
            startSize:   AdaptiveSize(sizeRatio, correction),
            startSpeed:  AdaptiveSpeed(),
            lifetime:    lifetime,
            shapeRadius: AdaptiveRadius()
        );

        ParticleScaler.AddUIParticle(obj);
        ps.Play();
        Destroy(obj, lifetime + 0.1f);
    }

    IEnumerator DelayedSpawn(Vector2 pos, Color color, float sizeRatio, float lifetime, float correction, float delay)
    {
        yield return new WaitForSeconds(delay);
        SpawnParticle(pos, color, sizeRatio, lifetime, correction);
    }

    // ── 공개 API (Tile.cs 에서 1:1 호출) ──

    public void PlayMergeEffect(Vector2 pos)
    {
        SpawnParticle(pos, mergeParticleColor, 0.6f, 0.3f, ParticleScaler.MergeCorrection);
    }

    public void PlayChocoEffect(Vector2 pos)
    {
        SpawnParticle(pos, chocoParticleColor, 0.75f, 0.35f, ParticleScaler.MergeCorrection);
        StartCoroutine(DelayedSpawn(pos, chocoParticleColor, 0.75f, 0.35f, ParticleScaler.MergeCorrection, 0.05f));
    }

    public void PlayBerryEffect(Vector2 pos)
    {
        SpawnParticle(pos, berryParticleColor, 0.75f, 0.35f, ParticleScaler.MergeCorrection);
        StartCoroutine(DelayedSpawn(pos, berryParticleColor, 0.75f, 0.35f, ParticleScaler.MergeCorrection, 0.05f));
    }

    public void PlayMixEffect(Vector2 pos)
    {
        SpawnParticle(pos, chocoParticleColor, 0.75f, 0.35f, ParticleScaler.MergeCorrection);
        StartCoroutine(DelayedSpawn(pos, berryParticleColor, 0.75f, 0.35f, ParticleScaler.MergeCorrection, 0.05f));
    }

    public void PlayGunDestroyEffect(Vector2 pos)
    {
        float gunCorr = ParticleScaler.GunCorrection;
        SpawnParticle(pos, gunParticleColor1, 0.9f, 0.4f, gunCorr);
        SpawnParticle(pos, gunParticleColor2, 1.1f, 0.2f, gunCorr);
    }
}
