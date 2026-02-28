// =====================================================
// ParticleScaler.cs
// 파티클 보정 수치 계산 유틸리티 (정적)
// ParticleSettings ScriptableObject 기반
// Material 공유 풀로 메모리 최적화
// =====================================================

using UnityEngine;

public static class ParticleScaler
{
    // ── 공유 Material 캐시 (Shader.Find 반복 방지) ──
    private static Material _sharedUIMaterial;

    public static Material SharedUIMaterial
    {
        get
        {
            if (_sharedUIMaterial == null)
                _sharedUIMaterial = new Material(Shader.Find("UI/Default"));
            return _sharedUIMaterial;
        }
    }

    // ── ParticleSettings 캐시 ──
    private static ParticleSettings _settings;
    private static ParticleResolutionEntry _cachedEntry;
    private static int _cachedScreenWidth = -1;

    /// <summary>외부에서 초기화 시점에 한 번 주입 (GameManager.Start 등)</summary>
    public static void Initialize(ParticleSettings settings)
    {
        _settings = settings;
        RefreshCache();
    }

    static void RefreshCache()
    {
        if (_settings == null) return;
        _cachedScreenWidth = Screen.width;
        _cachedEntry = _settings.GetCurrentEntry();
    }

    static ParticleResolutionEntry Entry
    {
        get
        {
            // 해상도 변경 감지 (에디터 리사이즈 대응)
            if (_cachedEntry == null || Screen.width != _cachedScreenWidth)
                RefreshCache();
            return _cachedEntry ?? new ParticleResolutionEntry();
        }
    }

    // ── 색상 프로퍼티 (ParticleSettings에서 읽음) ──
    public static Color BerryColor  => _settings != null ? _settings.berryParticleColor : new Color(1f, 0.5f, 0.65f);
    public static Color ChocoColor  => _settings != null ? _settings.chocoParticleColor : new Color(0.55f, 0.40f, 0.28f);
    public static Color GunColor1   => _settings != null ? _settings.gunParticleColor1  : new Color(1f, 0.84f, 0f);
    public static Color GunColor2   => _settings != null ? _settings.gunParticleColor2  : Color.white;

    // ── 보정값 프로퍼티 ──
    /// <summary>일반 머지 파티클 크기 보정</summary>
    public static float MergeCorrection    => Entry.mergeParticleSizeCorrection;

    /// <summary>Gun 파티클 크기 보정</summary>
    public static float GunCorrection      => Entry.gunParticleSizeCorrection;

    /// <summary>소형 파티클 크기 보정 (Fever/Clear 불꽃)</summary>
    public static float SmallCorrection    => Entry.smallParticleSizeCorrection;

    /// <summary>UIParticle.scale 값</summary>
    public static float UIParticleScale    => Entry.uiParticleScale;

    // ── 기존 Tile static 메서드와 완전 호환 (하위 호환 래퍼) ──
    public static float ParticleSizeCorrectionStatic()      => MergeCorrection;
    public static float GunParticleSizeCorrectionStatic()   => GunCorrection;
    public static float SmallParticleSizeCorrectionStatic() => SmallCorrection;

    // ── 공통 파티클 빌드 헬퍼 ──

    /// <summary>
    /// 머지/Gun 파티클 공통 설정 적용.
    /// 호출 후 Play()와 Destroy()는 호출부에서 처리.
    /// </summary>
    public static void ApplyMergeParticleSettings(
        ParticleSystem ps,
        Color color,
        float startSize,
        float startSpeed,
        float lifetime,
        float shapeRadius,
        int minBurst = 50,
        int maxBurst = 80)
    {
        var main = ps.main;
        main.startLifetime  = lifetime;
        main.startSpeed     = startSpeed;
        main.startSize      = startSize;
        main.startColor     = color;
        main.maxParticles   = maxBurst + 10;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.playOnAwake    = false;
        main.loop           = false;

        var emission = ps.emission;
        emission.enabled        = true;
        emission.rateOverTime   = 0;
        emission.SetBursts(new ParticleSystem.Burst[]
            { new ParticleSystem.Burst(0f, (short)minBurst, (short)maxBurst) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = shapeRadius;

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
                new GradientAlphaKey(1f,  0f),
                new GradientAlphaKey(0.8f,0.5f),
                new GradientAlphaKey(0f,  1f)
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
        rend.material   = SharedUIMaterial;   // ← 공유 Material
    }

    /// <summary>UIParticle 컴포넌트 추가 및 scale 적용</summary>
    public static Coffee.UIExtensions.UIParticle AddUIParticle(GameObject obj)
    {
        var uiP = obj.AddComponent<Coffee.UIExtensions.UIParticle>();
        uiP.scale = UIParticleScale;
        return uiP;
    }
}
