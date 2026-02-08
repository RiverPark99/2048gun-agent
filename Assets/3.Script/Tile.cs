using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

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

    // ⭐ NEW: 보호 테두리
    private Outline borderOutline;
    private bool isProtected = false;
    private Coroutine blinkCoroutine;

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
    }

    void SpawnParticleAtPosition(Vector2 position, Color color, float size, float lifetime)
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

        var main = ps.main;
        main.startLifetime = lifetime;
        main.startSpeed = 300f;
        main.startSize = size;
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
        shape.radius = 50f;

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

        var uiParticle = particleObj.AddComponent<Coffee.UIExtensions.UIParticle>();
        uiParticle.scale = 3f;
        uiParticle.autoScalingMode = Coffee.UIExtensions.UIParticle.AutoScalingMode.None;

        ps.Play();
        Destroy(particleObj, lifetime + 0.1f);
    }

    public void SetValue(int newValue)
    {
        value = newValue;
        valueText.text = value.ToString();

        // ⭐ UPDATED: TMP Outline 0.05 + Shadow
        valueText.outlineWidth = 0.1f;
        valueText.outlineColor = new Color(0f, 0f, 0f, 1f);
        valueText.fontSharedMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.1f);
        valueText.fontSharedMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 1f));

        Shadow shadow = valueText.GetComponent<Shadow>();
        if (shadow == null)
        {
            shadow = valueText.gameObject.AddComponent<Shadow>();
        }
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(3f, -3f);

        valueText.UpdateMeshPadding();

        UpdateAppearance();
    }

    public void SetColor(TileColor color)
    {
        tileColor = color;
        UpdateAppearance();
    }

    public void SetGridPosition(Vector2Int pos)
    {
        gridPosition = pos;
    }

    public void MoveTo(Vector2 position, bool animate = true)
    {
        targetPosition = position;
        if (animate)
        {
            isMoving = true;
        }
        else
        {
            rectTransform.anchoredPosition = position;
        }
    }

    public void MergeWith(Tile other)
    {
        SetValue(value * 2);

        SpawnParticleAtPosition(
            GetComponent<RectTransform>().anchoredPosition,
            new Color(1f, 0.8f, 0.2f),
            80f,
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
        SpawnParticleAtPosition(
            GetComponent<RectTransform>().anchoredPosition,
            new Color(0.55f, 0.40f, 0.28f),
            100f,
            0.35f
        );

        StartCoroutine(DelayedParticle(
            GetComponent<RectTransform>().anchoredPosition,
            new Color(0.55f, 0.40f, 0.28f),
            100f,
            0.35f,
            0.05f
        ));
    }

    public void PlayBerryMergeEffect()
    {
        SpawnParticleAtPosition(
            GetComponent<RectTransform>().anchoredPosition,
            new Color(1f, 0.5f, 0.65f),
            100f,
            0.35f
        );

        StartCoroutine(DelayedParticle(
            GetComponent<RectTransform>().anchoredPosition,
            new Color(1f, 0.5f, 0.65f),
            100f,
            0.35f,
            0.05f
        ));
    }

    public void PlayMixMergeEffect()
    {
        SpawnParticleAtPosition(
            GetComponent<RectTransform>().anchoredPosition,
            new Color(0.55f, 0.40f, 0.28f),
            100f,
            0.35f
        );

        StartCoroutine(DelayedParticle(
            GetComponent<RectTransform>().anchoredPosition,
            new Color(1f, 0.5f, 0.65f),
            100f,
            0.35f,
            0.05f
        ));
    }

    System.Collections.IEnumerator DelayedParticle(Vector2 position, Color color, float size, float lifetime, float delay)
    {
        yield return new WaitForSeconds(delay);
        SpawnParticleAtPosition(position, color, size, lifetime);
    }

    public void PlayGunDestroyEffect()
    {
        SpawnParticleAtPosition(
            GetComponent<RectTransform>().anchoredPosition,
            new Color(1f, 0.84f, 0f),
            120f,
            0.4f
        );

        SpawnParticleAtPosition(
            GetComponent<RectTransform>().anchoredPosition,
            Color.white,
            150f,
            0.2f
        );
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

        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }

        if (borderOutline == null)
        {
            borderOutline = background.gameObject.GetComponent<Outline>();
            if (borderOutline == null)
            {
                borderOutline = background.gameObject.AddComponent<Outline>();
            }
        }

        if (isProtected)
        {
            borderOutline.effectColor = new Color(0.8f, 0.8f, 0.9f, 1f);
            borderOutline.effectDistance = new Vector2(8f, 8f);
            borderOutline.enabled = true;
        }
        else if (shouldBlink)
        {
            borderOutline.effectColor = new Color(1f, 1f, 0.3f, 1f);
            borderOutline.effectDistance = new Vector2(6f, 6f);
            borderOutline.enabled = true;

            blinkCoroutine = StartCoroutine(BlinkBorder());
        }
        else
        {
            borderOutline.enabled = false;
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
            while (elapsed < duration)
            {
                if (borderOutline == null || !borderOutline.enabled)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                borderOutline.effectColor = Color.Lerp(brightColor, dimColor, t);
                
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < duration)
            {
                if (borderOutline == null || !borderOutline.enabled)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                borderOutline.effectColor = Color.Lerp(dimColor, brightColor, t);
                
                yield return null;
            }
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
