using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    private Coffee.UIExtensions.UIParticle uiParticle; // UIParticle 컴포넌트

    private RectTransform rectTransform;
    private Vector2 targetPosition;
    private bool isMoving = false;
    private float moveSpeed = 12f;

    // 검정/핑크 그라데이션 (2~4096까지 12단계)
    // 검정 -> 초콜릿 계열로 변경 (흰색 -> 밝은 초콜릿 -> 초콜릿 -> 다크 초콜릿 -> 검정)
    private Color[] chocoGradient = new Color[]
    {
        new Color(1f, 1f, 1f),                 // 2 - 완전 흰색
        new Color(0.95f, 0.90f, 0.85f),        // 4 - 크림색
        new Color(0.85f, 0.75f, 0.65f),        // 8 - 밝은 베이지
        new Color(0.75f, 0.60f, 0.45f),        // 16 - 베이지
        new Color(0.65f, 0.50f, 0.35f),        // 32 - 밝은 초콜릿
        new Color(0.55f, 0.40f, 0.28f),        // 64 - 밀크 초콜릿
        new Color(0.45f, 0.32f, 0.22f),        // 128 - 초콜릿
        new Color(0.35f, 0.25f, 0.18f),        // 256 - 진한 초콜릿
        new Color(0.28f, 0.20f, 0.14f),        // 512 - 다크 초콜릿
        new Color(0.20f, 0.15f, 0.10f),        // 1024 - 매우 진한 다크 초콜릿
        new Color(0.12f, 0.09f, 0.06f),        // 2048 - 거의 검정 초콜릿
        new Color(0.0f, 0.0f, 0.0f),           // 4096 - 완전 검정
    };

    private Color[] berryGradient = new Color[]
    {
        new Color(1f, 0.92f, 0.95f),     // 2
        new Color(1f, 0.85f, 0.9f),      // 4
        new Color(1f, 0.77f, 0.85f),     // 8
        new Color(1f, 0.69f, 0.80f),     // 16
        new Color(1f, 0.61f, 0.75f),     // 32
        new Color(1f, 0.53f, 0.70f),     // 64
        new Color(1f, 0.45f, 0.65f),     // 128
        new Color(0.98f, 0.37f, 0.60f),  // 256
        new Color(0.95f, 0.29f, 0.55f),  // 512
        new Color(0.92f, 0.21f, 0.50f),  // 1024
        new Color(0.88f, 0.13f, 0.45f),  // 2048
        new Color(0.85f, 0.05f, 0.40f),  // 4096
    };

    private Color goldColor = new Color(1f, 0.84f, 0f);
    private Color silverColor = new Color(0.9f, 0.9f, 0.95f);

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        // CreateParticleSystem() 제거!
        // 파티클은 머지 시에만 독립적으로 생성
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

    // 독립 파티클 생성 함수
    void SpawnParticleAtPosition(Vector2 position, Color color, float size, float lifetime)
    {
        GameObject particleObj = new GameObject("MergeParticle");

        // gridContainer의 자식으로 생성 (타일과 독립)
        Transform gridContainer = transform.parent;
        particleObj.transform.SetParent(gridContainer, false);

        // RectTransform 설정
        RectTransform particleRect = particleObj.AddComponent<RectTransform>();
        particleRect.anchorMin = new Vector2(0.5f, 0.5f);
        particleRect.anchorMax = new Vector2(0.5f, 0.5f);
        particleRect.pivot = new Vector2(0.5f, 0.5f);
        particleRect.anchoredPosition = position; // 지정된 위치
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

        // 재생 및 자동 삭제
        ps.Play();
        Destroy(particleObj, lifetime + 0.1f);

        Debug.Log($"Independent particle spawned at {position}!");
    }


    public void SetValue(int newValue)
    {
        value = newValue;
        valueText.text = value.ToString();

        valueText.outlineWidth = 0.3f;
        valueText.outlineColor = new Color(0f, 0f, 0f, 0.8f);

        valueText.fontSharedMaterial.EnableKeyword("UNDERLAY_ON");
        valueText.fontMaterial.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.5f));
        valueText.fontMaterial.SetFloat("_UnderlayOffsetX", 0.5f);
        valueText.fontMaterial.SetFloat("_UnderlayOffsetY", -0.5f);
        valueText.fontMaterial.SetFloat("_UnderlayDilate", 0.3f);
        valueText.fontMaterial.SetFloat("_UnderlaySoftness", 0.1f);

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

        // 독립 파티클 생성 (타일 위치에 고정)
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
        Debug.Log("CHOCO particle!");

        // 독립 파티클 생성
        SpawnParticleAtPosition(
            GetComponent<RectTransform>().anchoredPosition,
            new Color(1f, 0.84f, 0f),
            100f,
            0.35f
        );
    }

    public void PlayBerryMergeEffect()
    {
        Debug.Log("BERRY particle!");

        // 독립 파티클 생성
        SpawnParticleAtPosition(
            GetComponent<RectTransform>().anchoredPosition,
            new Color(0.3f, 1f, 0.3f),
            100f,
            0.35f
        );
    }
    public void PlayMixMergeEffect()
    {
        Debug.Log("MIX particle!");

        // ⭐ Mix 머지 파티클 (초코색 + 핑크색 혼합)
        SpawnParticleAtPosition(
            GetComponent<RectTransform>().anchoredPosition,
            new Color(1f, 0.65f, 0.65f),  // 초코+핑크 중간색 (밝은 코랄 핑크)
            100f,
            0.35f
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