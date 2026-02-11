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

    // v6.0: Berry gradient -> mint
    private Color[] berryGradient = new Color[]
    {
        new Color(0.85f, 0.96f, 0.93f),  // 2
        new Color(0.75f, 0.93f, 0.88f),  // 4
        new Color(0.65f, 0.90f, 0.83f),  // 8
        new Color(0.55f, 0.87f, 0.78f),  // 16
        new Color(0.45f, 0.84f, 0.73f),  // 32
        new Color(0.35f, 0.80f, 0.68f),  // 64
        new Color(0.25f, 0.75f, 0.62f),  // 128
        new Color(0.15f, 0.70f, 0.56f),  // 256
        new Color(0.08f, 0.65f, 0.50f),  // 512
        new Color(0.02f, 0.58f, 0.44f),  // 1024
        new Color(0.00f, 0.50f, 0.38f),  // 2048
        new Color(0.00f, 0.42f, 0.32f),  // 4096
    };

    private Color goldColor = new Color(1f, 0.84f, 0f);
    // v6.0: Berry text -> dark chocolate
    private Color berryTextColor = new Color(0.25f, 0.15f, 0.10f);
    // v6.0: Berry merge particle -> HP bar mint color
    private readonly Color berryParticleColor = new Color(0.0f, 0.65f, 0.55f);

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

    // v6.0: particle size based on tile RectTransform (resolution-adaptive)
    float GetAdaptiveParticleSize(float baseRatio)
    {
        if (rectTransform == null) return 100f;
        float tileSize = Mathf.Max(rectTransform.rect.width, rectTransform.rect.height);
        return tileSize * baseRatio;
    }

    float GetAdaptiveShapeRadius()
    {
        if (rectTransform == null) return 50f;
        float tileSize = Mathf.Max(rectTransform.rect.width, rectTransform.rect.height);
        return tileSize * 0.35f;
    }

    float GetAdaptiveSpeed()
    {
        if (rectTransform == null) return 300f;
        float tileSize = Mathf.Max(rectTransform.rect.width, rectTransform.rect.height);
        return tileSize * 2.0f;
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

        // v6.0: adaptive sizing
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
        uiP.scale = 3f;
        uiP.autoScalingMode = Coffee.UIExtensions.UIParticle.AutoScalingMode.None;

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
        // v6.0: ratio-based size (was hardcoded 80f)
        SpawnParticleAtPosition(
            GetComponent<RectTransform>().anchoredPosition,
            new Color(1f, 0.8f, 0.2f),
            0.6f, // ~60% of tile size
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

    // v6.0: Berry merge particle -> HP bar mint color
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
        SpawnParticleAtPosition(pos, new Color(1f, 0.84f, 0f), 0.9f, 0.4f);
        SpawnParticleAtPosition(pos, Color.white, 1.1f, 0.2f);
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
            // v6.0: Berry -> mint background, dark chocolate text
            background.color = berryGradient[colorIndex];
            valueText.color = berryTextColor;
        }

        if (mergeParticle != null)
        {
            var main = mergeParticle.main;
            main.startColor = background.color;
        }
    }
}
