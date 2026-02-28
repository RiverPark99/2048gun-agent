// =====================================================
// TileParticleSpawner.cs
// Tile 머지/파괴 파티클 전담 컴포넌트
// - 색상: ParticleSettings ScriptableObject (ParticleScaler 경유)
// - Merge 파티클: static Queue Pool 재사용
//   파티클은 gridContainer에 귀속 → Tile Destroy와 수명 완전 독립
// - Gun 파괴 파티클: Instantiate/Destroy (Tile과 수명 독립)
// =====================================================

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(RectTransform))]
public class TileParticleSpawner : MonoBehaviour
{
    private RectTransform rectTransform;

    private static Queue<GameObject> _particlePool;
    private static bool _poolInitialized = false;
    private static Transform _poolRoot;

    private Transform GridContainer => transform.parent;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        EnsurePoolInitialized();
    }

    static void EnsurePoolInitialized()
    {
        if (_poolInitialized && _poolRoot != null) return;
        GameObject root = new GameObject("[ParticlePool]");
        DontDestroyOnLoad(root);
        root.SetActive(false);
        _poolRoot = root.transform;
        _particlePool = new Queue<GameObject>();
        _poolInitialized = true;
    }

    static GameObject GetParticle(Transform parent)
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

    public static void ReturnParticle(GameObject obj)
    {
        if (obj == null) return;
        var ps = obj.GetComponent<ParticleSystem>();
        if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        obj.SetActive(false);
        if (_poolRoot != null)
            obj.transform.SetParent(_poolRoot, false);
        _particlePool?.Enqueue(obj);
    }

    static GameObject CreateParticleObject(Transform parent)
    {
        GameObject obj = new GameObject("MergeParticle");
        obj.transform.SetParent(parent, false);

        var rt       = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;

        obj.AddComponent<ParticleSystem>();
        obj.AddComponent<Coffee.UIExtensions.UIParticle>();
        obj.AddComponent<ParticleLifetimeRunner>();
        return obj;
    }

    void PlayParticle(Vector2 anchoredPos, Color color, float sizeRatio, float lifetime, float correction)
    {
        Transform container = GridContainer ?? transform;
        GameObject obj = GetParticle(container);

        var rt = obj.GetComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;

        var ps = obj.GetComponent<ParticleSystem>();
        ApplySettings(ps, color, sizeRatio, lifetime, correction);

        var uiP = obj.GetComponent<Coffee.UIExtensions.UIParticle>();
        if (uiP != null) uiP.scale = ParticleScaler.UIParticleScale;

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.Play();

        var runner = obj.GetComponent<ParticleLifetimeRunner>();
        if (runner != null) runner.ScheduleReturn(obj, lifetime + 0.1f);
    }

    void ApplySettings(ParticleSystem ps, Color color, float sizeRatio, float lifetime, float correction)
    {
        float tileSize = Mathf.Max(rectTransform.rect.width, rectTransform.rect.height);
        float speed    = tileSize * 0.5f;
        float radius   = tileSize * 0.08f;
        float safeCorrection = Mathf.Max(correction, 1f);
        float size = tileSize * sizeRatio / safeCorrection;

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

    IEnumerator DelayedPlay(Vector2 pos, Color color, float sizeRatio, float lifetime, float correction, float delay)
    {
        yield return new WaitForSeconds(delay);
        PlayParticle(pos, color, sizeRatio, lifetime, correction);
    }

    // ── 공개 API ──

    public void PlayChocoEffect(Vector2 pos)
    {
        PlayParticle(pos, ParticleScaler.ChocoColor, 0.75f, 0.35f, ParticleScaler.MergeCorrection);
        StartCoroutine(DelayedPlay(pos, ParticleScaler.ChocoColor, 0.75f, 0.35f, ParticleScaler.MergeCorrection, 0.05f));
    }

    public void PlayBerryEffect(Vector2 pos)
    {
        PlayParticle(pos, ParticleScaler.BerryColor, 0.75f, 0.35f, ParticleScaler.MergeCorrection);
        StartCoroutine(DelayedPlay(pos, ParticleScaler.BerryColor, 0.75f, 0.35f, ParticleScaler.MergeCorrection, 0.05f));
    }

    public void PlayMixEffect(Vector2 pos)
    {
        PlayParticle(pos, ParticleScaler.ChocoColor, 0.75f, 0.35f, ParticleScaler.MergeCorrection);
        StartCoroutine(DelayedPlay(pos, ParticleScaler.BerryColor, 0.75f, 0.35f, ParticleScaler.MergeCorrection, 0.05f));
    }

    // ── Gun 파괴 전용: Instantiate/Destroy ──

    public void PlayGunDestroyEffect(Vector2 pos)
    {
        float tileSize = Mathf.Max(rectTransform.rect.width, rectTransform.rect.height);
        SpawnGunParticle(pos, ParticleScaler.GunColor1, 0.9f, 0.4f, ParticleScaler.GunCorrection, tileSize);
        SpawnGunParticle(pos, ParticleScaler.GunColor2, 1.1f, 0.2f, ParticleScaler.GunCorrection, tileSize);
    }

    void SpawnGunParticle(Vector2 anchoredPos, Color color, float sizeRatio, float lifetime, float correction, float tileSize)
    {
        Transform container = GridContainer ?? transform;

        GameObject obj = new GameObject("GunDestroyParticle");
        obj.transform.SetParent(container, false);

        var rt           = obj.AddComponent<RectTransform>();
        rt.anchorMin     = new Vector2(0.5f, 0.5f);
        rt.anchorMax     = new Vector2(0.5f, 0.5f);
        rt.pivot         = new Vector2(0.5f, 0.5f);
        rt.sizeDelta     = Vector2.zero;
        rt.anchoredPosition = anchoredPos;

        var ps = obj.AddComponent<ParticleSystem>();

        float speed          = tileSize * 0.5f;
        float radius         = tileSize * 0.08f;
        float safeCorrection = Mathf.Max(correction, 1f);
        float size           = tileSize * sizeRatio / safeCorrection;

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

        var uiP = obj.AddComponent<Coffee.UIExtensions.UIParticle>();
        uiP.scale = ParticleScaler.UIParticleScale;

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.material   = ParticleScaler.SharedUIMaterial;

        ps.Play();
        Destroy(obj, lifetime + 0.2f);
    }
}
