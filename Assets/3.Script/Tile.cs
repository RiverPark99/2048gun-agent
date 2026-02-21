using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;

public enum TileColor
{
    Choco,
    Berry
}

public class Tile : MonoBehaviour
{
    public int value;
    public Vector2Int gridPosition;
    public TileColor tileColor;

    [SerializeField] private Image background;
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private ParticleSystem mergeParticle;
    private Coffee.UIExtensions.UIParticle uiParticle;

    private Outline borderOutline;
    private bool isProtected = false;
    private Coroutine blinkCoroutine;

    private Coroutine ultrasonicCoroutine;
    private bool isUltrasonicActive = false;
    private List<GameObject> activeWaveObjects = new List<GameObject>();

    private RectTransform rectTransform;
    private Vector2 targetPosition;
    private bool isMoving = false;
    private float moveSpeed = 12f;

    private Color[] chocoGradient = new Color[]
    {
        new Color(0.95f, 0.90f, 0.85f),
        new Color(0.90f, 0.85f, 0.75f),
        new Color(0.85f, 0.75f, 0.65f),
        new Color(0.75f, 0.60f, 0.45f),
        new Color(0.65f, 0.50f, 0.35f),
        new Color(0.55f, 0.40f, 0.28f),
        new Color(0.45f, 0.32f, 0.22f),
        new Color(0.35f, 0.25f, 0.18f),
        new Color(0.28f, 0.20f, 0.14f),
        new Color(0.20f, 0.15f, 0.10f),
        new Color(0.12f, 0.09f, 0.06f),
        new Color(0.0f, 0.0f, 0.0f),
    };

    private Color[] berryGradient = new Color[]
    {
        new Color(1f, 0.80f, 0.85f),
        new Color(1f, 0.75f, 0.82f),
        new Color(1f, 0.70f, 0.79f),
        new Color(1f, 0.65f, 0.76f),
        new Color(1f, 0.60f, 0.73f),
        new Color(1f, 0.55f, 0.70f),
        new Color(1f, 0.50f, 0.67f),
        new Color(1f, 0.45f, 0.64f),
        new Color(0.98f, 0.40f, 0.61f),
        new Color(0.95f, 0.35f, 0.58f),
        new Color(0.92f, 0.25f, 0.52f),
        new Color(0.88f, 0.15f, 0.46f),
    };

    private Color goldColor = new Color(1f, 0.84f, 0f);
    private Color silverColor = new Color(0.9f, 0.9f, 0.95f);
    private Color berryParticleColor = new Color(1f, 0.5f, 0.65f);

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    void Update()
    {
        if (isMoving)
        {
            rectTransform.anchoredPosition = Vector2.Lerp(
                rectTransform.anchoredPosition,
                targetPosition,
                Time.deltaTime * moveSpeed
            );

            if (Vector2.Distance(rectTransform.anchoredPosition, targetPosition) < 0.1f)
            {
                rectTransform.anchoredPosition = targetPosition;
                isMoving = false;
            }
        }
    }

    void OnDestroy()
    {
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }
        StopUltrasonicEffect();
    }

    // === 파티클 해상도 대응 ===
    // 실측: 498px→보정1.0(OK), 1290px→보정~67(OK)
    // 중간 해상도(1080등)도 부드럽게 보간
    static float ScreenRatio() => (float)Screen.width / 498f;
    static float AdaptiveScale() => 3f * ScreenRatio();

    // Merge 파티클용 (해상도별 개별 대응)
    static float ParticleSizeCorrection()
    {
        int w = Screen.width;
        if (w <= 498) return 1f;
        if (w <= 600) return Mathf.Lerp(1f, 10f, Mathf.InverseLerp(498f, 600f, w));
        if (w <= 1080) return Mathf.Lerp(10f, 55f, Mathf.InverseLerp(600f, 1080f, w));
        return Mathf.Lerp(45f, 67f, Mathf.InverseLerp(1080f, 1290f, w));
    }

    // Gun destroy 파티클용
    static float GunParticleSizeCorrection()
    {
        int w = Screen.width;
        if (w <= 498) return 1f;
        if (w <= 1080) return Mathf.Lerp(1f, 35f, Mathf.InverseLerp(498f, 1080f, w));
        return Mathf.Lerp(35f, 55f, Mathf.InverseLerp(1080f, 1290f, w));
    }

    // Fever/Firework 파티클용 (약한 보정)
    static float SmallParticleSizeCorrection()
    {
        if (Screen.width <= 498) return 1f;
        float t = Mathf.InverseLerp(498f, 1290f, (float)Screen.width);
        return Mathf.Lerp(1f, 8f, Mathf.Clamp01(t));
    }

    public static float ParticleSizeCorrectionStatic() => ParticleSizeCorrection();
    public static float GunParticleSizeCorrectionStatic() => GunParticleSizeCorrection();
    public static float SmallParticleSizeCorrectionStatic() => SmallParticleSizeCorrection();

    float GetAdaptiveParticleSize(float baseRatio)
    {
        if (rectTransform == null) return 15f;
        float tileSize = Mathf.Max(rectTransform.rect.width, rectTransform.rect.height);
        return tileSize * baseRatio / ParticleSizeCorrection();
    }

    float GetAdaptiveShapeRadius()
    {
        if (rectTransform == null) return 8f;
        float tileSize = Mathf.Max(rectTransform.rect.width, rectTransform.rect.height);
        return tileSize * 0.08f;
    }

    float GetAdaptiveSpeed()
    {
        if (rectTransform == null) return 60f;
        float tileSize = Mathf.Max(rectTransform.rect.width, rectTransform.rect.height);
        return tileSize * 0.5f;
    }

    void SpawnParticleAtPosition(Vector2 position, Color color, float sizeRatio, float lifetime)
    {
        GameObject particleObj = new GameObject("MergeParticle");

        Transform gridContainer = transform.parent;
        particleObj.transform.SetParent(gridContainer, false);

        RectTransform particleRect = particleObj.AddComponent<RectTransform>();
        particleRect.anchorMin = new Vector2(0.5f, 0.5f);
        particleRect.anchorMax = new Vector2(0.5f, 0.5f);
        particleRect.pivot = new Vector2(0.5f, 0.5f);
        particleRect.anchoredPosition = position;
        particleRect.sizeDelta = Vector2.zero;

        ParticleSystem ps = particleObj.AddComponent<ParticleSystem>();

        float adaptiveSize = GetAdaptiveParticleSize(sizeRatio);
        float adaptiveSpeed = GetAdaptiveSpeed();
        float adaptiveRadius = GetAdaptiveShapeRadius();
        var main = ps.main;
        main.startLifetime = lifetime;
        main.startSpeed = adaptiveSpeed;
        main.startSize = adaptiveSize;
        main.startColor = color;
        main.maxParticles = 80;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.playOnAwake = false;
        main.loop = false;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, 50, 80)
        });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = adaptiveRadius;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0.0f),
                new GradientColorKey(color, 0.5f),
                new GradientColorKey(color, 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(0.8f, 0.5f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0.0f, 1.0f);
        curve.AddKey(1.0f, 0.0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = new Material(Shader.Find("UI/Default"));

        var uiP = particleObj.AddComponent<Coffee.UIExtensions.UIParticle>();
        uiP.scale = AdaptiveScale();

        ps.Play();
        Destroy(particleObj, lifetime + 0.1f);
    }

    // Gun destroy 전용 (별도 보정값 적용)
    void SpawnParticleAtPositionWithCorrection(Vector2 position, Color color, float sizeRatio, float lifetime, float correction)
    {
        GameObject particleObj = new GameObject("GunParticle");
        Transform gridContainer = transform.parent;
        particleObj.transform.SetParent(gridContainer, false);

        RectTransform particleRect = particleObj.AddComponent<RectTransform>();
        particleRect.anchorMin = new Vector2(0.5f, 0.5f);
        particleRect.anchorMax = new Vector2(0.5f, 0.5f);
        particleRect.pivot = new Vector2(0.5f, 0.5f);
        particleRect.anchoredPosition = position;
        particleRect.sizeDelta = Vector2.zero;

        ParticleSystem ps = particleObj.AddComponent<ParticleSystem>();

        float tileSize = (rectTransform != null) ? Mathf.Max(rectTransform.rect.width, rectTransform.rect.height) : 100f;
        float adaptiveSize = tileSize * sizeRatio / correction;
        float adaptiveSpeed = GetAdaptiveSpeed();
        float adaptiveRadius = GetAdaptiveShapeRadius();

        var main = ps.main;
        main.startLifetime = lifetime;
        main.startSpeed = adaptiveSpeed;
        main.startSize = adaptiveSize;
        main.startColor = color;
        main.maxParticles = 80;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.playOnAwake = false;
        main.loop = false;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 50, 80) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = adaptiveRadius;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 0.5f), new GradientColorKey(color, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 1f); curve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = new Material(Shader.Find("UI/Default"));

        var uiP = particleObj.AddComponent<Coffee.UIExtensions.UIParticle>();
        uiP.scale = AdaptiveScale();

        ps.Play();
        Destroy(particleObj, lifetime + 0.1f);
    }

    public void SetValue(int newValue)
    {
        value = newValue;
        valueText.text = value.ToString();

        Shadow shadow = valueText.GetComponent<Shadow>();
        if (shadow == null)
        {
            shadow = valueText.gameObject.AddComponent<Shadow>();
        }
        shadow.effectColor = new Color(0f, 0f, 0f, 1f);
        shadow.effectDistance = new Vector2(5f, -5f);
        shadow.useGraphicAlpha = true;

        UpdateAppearance();
    }

    public void SetColor(TileColor color)
    {
        tileColor = color;
        UpdateAppearance();
    }

    public void SetGridPosition(Vector2Int pos) { gridPosition = pos; }

    public void MoveTo(Vector2 position, bool animate = true)
    {
        targetPosition = position;
        if (animate) isMoving = true;
        else rectTransform.anchoredPosition = position;
    }

    public void MergeWith(Tile other)
    {
        SetValue(value * 2);
        SpawnParticleAtPosition(
            GetComponent<RectTransform>().anchoredPosition,
            new Color(1f, 0.8f, 0.2f),
            0.6f,
            0.3f
        );
        StartCoroutine(PopAnimation());
    }

    public void MergeWithoutParticle()
    {
        SetValue(value * 2);
        StartCoroutine(PopAnimation());
    }

    public void PlayChocoMergeEffect()
    {
        Vector2 pos = GetComponent<RectTransform>().anchoredPosition;
        SpawnParticleAtPosition(pos, new Color(0.55f, 0.40f, 0.28f), 0.75f, 0.35f);
        StartCoroutine(DelayedParticle(pos, new Color(0.55f, 0.40f, 0.28f), 0.75f, 0.35f, 0.05f));
    }

    public void PlayBerryMergeEffect()
    {
        Vector2 pos = GetComponent<RectTransform>().anchoredPosition;
        SpawnParticleAtPosition(pos, berryParticleColor, 0.75f, 0.35f);
        StartCoroutine(DelayedParticle(pos, berryParticleColor, 0.75f, 0.35f, 0.05f));
    }

    public void PlayMixMergeEffect()
    {
        Vector2 pos = GetComponent<RectTransform>().anchoredPosition;
        SpawnParticleAtPosition(pos, new Color(0.55f, 0.40f, 0.28f), 0.75f, 0.35f);
        StartCoroutine(DelayedParticle(pos, berryParticleColor, 0.75f, 0.35f, 0.05f));
    }

    System.Collections.IEnumerator DelayedParticle(Vector2 position, Color color, float sizeRatio, float lifetime, float delay)
    {
        yield return new WaitForSeconds(delay);
        SpawnParticleAtPosition(position, color, sizeRatio, lifetime);
    }

    public void PlayGunDestroyEffect()
    {
        Vector2 pos = GetComponent<RectTransform>().anchoredPosition;
        float gunCorr = GunParticleSizeCorrection();
        // Gun 파티클은 별도 보정
        SpawnParticleAtPositionWithCorrection(pos, new Color(1f, 0.84f, 0f), 0.9f, 0.4f, gunCorr);
        SpawnParticleAtPositionWithCorrection(pos, Color.white, 1.1f, 0.2f, gunCorr);
    }

    private System.Collections.IEnumerator PopAnimation()
    {
        float duration = 0.3f;
        float elapsed = 0f;

        float shrinkDuration = duration * 0.3f;
        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / shrinkDuration;
            transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.8f, t);
            yield return null;
        }

        elapsed = 0f;
        float popDuration = duration * 0.4f;
        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / popDuration;
            float s = 1.70158f * 1.525f;
            t = t - 1;
            float val = t * t * ((s + 1) * t + s) + 1;
            transform.localScale = Vector3.Lerp(Vector3.one * 0.8f, Vector3.one * 1.2f, val);
            yield return null;
        }

        elapsed = 0f;
        float returnDuration = duration * 0.3f;
        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / returnDuration;
            transform.localScale = Vector3.Lerp(Vector3.one * 1.2f, Vector3.one, t);
            yield return null;
        }

        transform.localScale = Vector3.one;
    }

    public void SetProtected(bool protected_, bool shouldBlink = false)
    {
        isProtected = protected_;

        if (blinkCoroutine != null) { StopCoroutine(blinkCoroutine); blinkCoroutine = null; }

        if (borderOutline == null)
        {
            borderOutline = background.gameObject.GetComponent<Outline>();
            if (borderOutline == null) borderOutline = background.gameObject.AddComponent<Outline>();
        }

        if (isProtected)
        {
            borderOutline.effectColor = new Color(0.8f, 0.8f, 0.9f, 1f);
            borderOutline.effectDistance = new Vector2(8f, 8f);
            borderOutline.enabled = true;
            StopUltrasonicEffect();
        }
        else if (shouldBlink)
        {
            borderOutline.effectColor = new Color(1f, 1f, 0.3f, 1f);
            borderOutline.effectDistance = new Vector2(6f, 6f);
            borderOutline.enabled = true;
            blinkCoroutine = StartCoroutine(BlinkBorder());
            StartUltrasonicEffect();
        }
        else
        {
            borderOutline.enabled = false;
            StopUltrasonicEffect();
        }
    }

    void StartUltrasonicEffect()
    {
        if (isUltrasonicActive) return;
        isUltrasonicActive = true;
        ultrasonicCoroutine = StartCoroutine(UltrasonicWaveCoroutine());
    }

    void StopUltrasonicEffect()
    {
        isUltrasonicActive = false;
        if (ultrasonicCoroutine != null) { StopCoroutine(ultrasonicCoroutine); ultrasonicCoroutine = null; }
        for (int i = activeWaveObjects.Count - 1; i >= 0; i--)
        {
            if (activeWaveObjects[i] != null) Destroy(activeWaveObjects[i]);
        }
        activeWaveObjects.Clear();
    }

    System.Collections.IEnumerator UltrasonicWaveCoroutine()
    {
        while (isUltrasonicActive)
        {
            GameObject waveObj = new GameObject("UltrasonicWave");
            waveObj.transform.SetParent(background.transform, false);
            activeWaveObjects.Add(waveObj);

            Image waveImg = waveObj.AddComponent<Image>();
            waveImg.color = new Color(1f, 1f, 0.3f, 0.15f);

            RectTransform waveRect = waveObj.GetComponent<RectTransform>();
            waveRect.anchorMin = new Vector2(0.5f, 0.5f);
            waveRect.anchorMax = new Vector2(0.5f, 0.5f);
            waveRect.pivot = new Vector2(0.5f, 0.5f);
            waveRect.anchoredPosition = Vector2.zero;
            
            float startSize = 10f;
            RectTransform bgRect = background.GetComponent<RectTransform>();
            float endSize = Mathf.Max(bgRect.rect.width, bgRect.rect.height);
            waveRect.sizeDelta = new Vector2(startSize, startSize);

            Outline waveOutline = waveObj.AddComponent<Outline>();
            waveOutline.effectColor = new Color(1f, 1f, 0.3f, 0.6f);
            waveOutline.effectDistance = new Vector2(3f, 3f);

            float waveDuration = 0.8f;
            float elapsed = 0f;

            while (elapsed < waveDuration && isUltrasonicActive)
            {
                if (waveObj == null) break;
                elapsed += Time.deltaTime;
                float t = elapsed / waveDuration;
                float currentSize = Mathf.Lerp(startSize, endSize, t);
                waveRect.sizeDelta = new Vector2(currentSize, currentSize);
                float alpha = Mathf.Lerp(0.4f, 0f, t);
                waveImg.color = new Color(1f, 1f, 0.3f, alpha * 0.3f);
                waveOutline.effectColor = new Color(1f, 1f, 0.3f, alpha);
                yield return null;
            }

            if (waveObj != null) { activeWaveObjects.Remove(waveObj); Destroy(waveObj); }
            yield return new WaitForSeconds(0.15f);
        }
    }

    System.Collections.IEnumerator BlinkBorder()
    {
        if (borderOutline == null) yield break;
        Color brightColor = new Color(1f, 1f, 0.3f, 1f);
        Color dimColor = new Color(1f, 1f, 0.3f, 0.2f);
        float duration = 0.6f;
        while (true)
        {
            float elapsed = 0f;
            while (elapsed < duration) { if (borderOutline == null || !borderOutline.enabled) yield break; elapsed += Time.deltaTime; borderOutline.effectColor = Color.Lerp(brightColor, dimColor, elapsed/duration); yield return null; }
            elapsed = 0f;
            while (elapsed < duration) { if (borderOutline == null || !borderOutline.enabled) yield break; elapsed += Time.deltaTime; borderOutline.effectColor = Color.Lerp(dimColor, brightColor, elapsed/duration); yield return null; }
        }
    }

    private void UpdateAppearance()
    {
        int colorIndex = Mathf.Min((int)Mathf.Log(value, 2) - 1, chocoGradient.Length - 1);

        if (tileColor == TileColor.Choco)
        {
            background.color = chocoGradient[colorIndex];
            valueText.color = goldColor;
        }
        else
        {
            background.color = berryGradient[colorIndex];
            valueText.color = silverColor;
        }

        if (mergeParticle != null)
        {
            var main = mergeParticle.main;
            main.startColor = background.color;
        }
    }
}
