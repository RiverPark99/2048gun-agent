// =====================================================
// TileParticleSpawner.cs
// Tile 머지/파괴 파티클 전담 컴포넌트
// Object Pool 적용 — 매 머지마다 Instantiate 제거
// =====================================================

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

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

    // ── 파티클 풀 (씬 전역 공유 static) ──
    // TileParticleSpawner는 Tile마다 하나씩 존재하므로 static 공유 풀이 효율적
    private static Queue<GameObject> _particlePool;
    private static bool _poolInitialized = false;
    private static Transform _poolRoot;   // 비활성 파티클 보관용 부모

    // 현재 Tile이 발사한 파티클 추적 (씬 정리용)
    private readonly List<GameObject> _activeParticles = new List<GameObject>();

    // ── 초기화 ──

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        EnsurePoolInitialized();
    }

    void OnDestroy()
    {
        // 이 Tile이 소유한 파티클들을 풀에 반환
        ReturnAllActive();
    }

    static void EnsurePoolInitialized()
    {
        if (_poolInitialized && _poolRoot != null) return;

        // Pool 루트 생성 (씬에 하나만 존재)
        GameObject root = new GameObject("[ParticlePool]");
        DontDestroyOnLoad(root);
        root.SetActive(false);          // 비활성화로 자식도 자동 비활성
        _poolRoot = root.transform;

        _particlePool = new Queue<GameObject>();
        _poolInitialized = true;
    }

    void ReturnAllActive()
    {
        for (int i = _activeParticles.Count - 1; i >= 0; i--)
        {
            var p = _activeParticles[i];
            if (p != null) ReturnParticle(p);
        }
        _activeParticles.Clear();
    }

    // ── 풀 Get / Return ──

    GameObject GetParticle(Transform parent)
    {
        GameObject obj;
        if (_particlePool != null && _particlePool.Count > 0)
        {
            obj = _particlePool.Dequeue();
            obj.transform.SetParent(parent, false);
            obj.SetActive(true);
        }
        else
        {
            obj = CreateParticleObject(parent);
        }
        return obj;
    }

    void ReturnParticle(GameObject obj)
    {
        if (obj == null) return;
        var ps = obj.GetComponent<ParticleSystem>();
        if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        obj.SetActive(false);
        if (_poolRoot != null)
            obj.transform.SetParent(_poolRoot, false);
        _particlePool?.Enqueue(obj);
        _activeParticles.Remove(obj);
    }

    // ── 파티클 오브젝트 생성 (풀에 없을 때) ──

    GameObject CreateParticleObject(Transform parent)
    {
        GameObject obj = new GameObject("MergeParticle");
        obj.transform.SetParent(parent, false);

        RectTransform rt   = obj.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = Vector2.zero;

        obj.AddComponent<ParticleSystem>();
        obj.AddComponent<Coffee.UIExtensions.UIParticle>();
        return obj;
    }

    // ── 파티클 설정 & 재생 ──

    void PlayParticle(Vector2 anchoredPos, Color color, float sizeRatio, float lifetime, float correction)
    {
        GameObject obj = GetParticle(transform.parent);
        _activeParticles.Add(obj);

        RectTransform rt   = obj.GetComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;

        ParticleSystem ps = obj.GetComponent<ParticleSystem>();
        ApplySettings(ps, color, sizeRatio, lifetime, correction);

        var uiP = obj.GetComponent<Coffee.UIExtensions.UIParticle>();
        if (uiP != null) uiP.scale = ParticleScaler.UIParticleScale;

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.Play();

        StartCoroutine(ReturnAfterLifetime(obj, lifetime + 0.1f));
    }

    void ApplySettings(ParticleSystem ps, Color color, float sizeRatio, float lifetime, float correction)
    {
        float tileSize = Mathf.Max(rectTransform.rect.width, rectTransform.rect.height);
        float speed    = tileSize * 0.5f;
        float radius   = tileSize * 0.08f;
        float size     = tileSize * sizeRatio / correction;

        var main = ps.main;
        main.startLifetime   = lifetime;
        main.startSpeed      = speed;
        main.startSize       = size;
        main.startColor      = color;
        main.maxParticles    = 90;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.playOnAwake     = false;
        main.loop            = false;

        var emission = ps.emission;
        emission.enabled      = true;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[]
            { new ParticleSystem.Burst(0f, (short)50, (short)80) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = radius;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(color,       0.5f),
                new GradientColorKey(color,       1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f,   0f),
                new GradientAlphaKey(0.8f, 0.5f),
                new GradientAlphaKey(0f,   1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 1f);
        curve.AddKey(1f, 0f);
        sol.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.material   = ParticleScaler.SharedUIMaterial;
    }

    IEnumerator ReturnAfterLifetime(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnParticle(obj);
    }

    IEnumerator DelayedPlay(Vector2 pos, Color color, float sizeRatio, float lifetime, float correction, float delay)
    {
        yield return new WaitForSeconds(delay);
        PlayParticle(pos, color, sizeRatio, lifetime, correction);
    }

    // ── 공개 API ──

    public void PlayMergeEffect(Vector2 pos)
        => PlayParticle(pos, mergeParticleColor, 0.6f, 0.3f, ParticleScaler.MergeCorrection);

    public void PlayChocoEffect(Vector2 pos)
    {
        PlayParticle(pos, chocoParticleColor, 0.75f, 0.35f, ParticleScaler.MergeCorrection);
        StartCoroutine(DelayedPlay(pos, chocoParticleColor, 0.75f, 0.35f, ParticleScaler.MergeCorrection, 0.05f));
    }

    public void PlayBerryEffect(Vector2 pos)
    {
        PlayParticle(pos, berryParticleColor, 0.75f, 0.35f, ParticleScaler.MergeCorrection);
        StartCoroutine(DelayedPlay(pos, berryParticleColor, 0.75f, 0.35f, ParticleScaler.MergeCorrection, 0.05f));
    }

    public void PlayMixEffect(Vector2 pos)
    {
        PlayParticle(pos, chocoParticleColor, 0.75f, 0.35f, ParticleScaler.MergeCorrection);
        StartCoroutine(DelayedPlay(pos, berryParticleColor, 0.75f, 0.35f, ParticleScaler.MergeCorrection, 0.05f));
    }

    public void PlayGunDestroyEffect(Vector2 pos)
    {
        float gunCorr = ParticleScaler.GunCorrection;
        PlayParticle(pos, gunParticleColor1, 0.9f, 0.4f, gunCorr);
        PlayParticle(pos, gunParticleColor2, 1.1f, 0.2f, gunCorr);
    }
}
