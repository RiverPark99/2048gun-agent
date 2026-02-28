// =====================================================
// Tile.cs - v7.1
// 타일 값/색상/이동/Outline 담당
// 파티클 생성 → TileParticleSpawner 위임
// =====================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;

public enum TileColor { Choco, Berry }

public class Tile : MonoBehaviour
{
    public int value;
    public Vector2Int gridPosition;
    public TileColor tileColor;

    [SerializeField] private Image background;
    [SerializeField] private TextMeshProUGUI valueText;
    // mergeParticle 필드 제거 — TileParticleSpawner가 전담

    // === Gun Mode Outline ===
    private Outline tileOutline;
    private bool isGunProtected = false;
    private bool isGunBlinking  = false;
    private Coroutine blinkCoroutine;

    [Header("Gun Outline 색상")]
    [SerializeField] private Color gunProtectColor = new Color(0.8f, 0.8f, 0.9f, 1f);
    [SerializeField] private Color gunBlinkColor   = new Color(1f,   1f,   0.3f, 1f);
    [SerializeField] private Vector2 gunProtectDist = new Vector2(8f, 8f);
    [SerializeField] private Vector2 gunBlinkDist   = new Vector2(6f, 6f);

    private Coroutine ultrasonicCoroutine;
    private bool isUltrasonicActive = false;
    private List<GameObject> activeWaveObjects = new List<GameObject>();

    [Header("Choco 배경색 (2~4096)")]
    [SerializeField] private Color[] chocoGradient = new Color[]
    {
        new Color(0.95f, 0.90f, 0.85f), new Color(0.90f, 0.85f, 0.75f),
        new Color(0.85f, 0.75f, 0.65f), new Color(0.75f, 0.60f, 0.45f),
        new Color(0.65f, 0.50f, 0.35f), new Color(0.55f, 0.40f, 0.28f),
        new Color(0.45f, 0.32f, 0.22f), new Color(0.35f, 0.25f, 0.18f),
        new Color(0.28f, 0.20f, 0.14f), new Color(0.20f, 0.15f, 0.10f),
        new Color(0.12f, 0.09f, 0.06f), new Color(0.0f,  0.0f,  0.0f),
    };

    [Header("Choco 텍스트색 (2~4096)")]
    [SerializeField] private Color[] chocoTextColors = new Color[]
    {
        new Color(1f,0.84f,0f),new Color(1f,0.84f,0f),new Color(1f,0.84f,0f),
        new Color(1f,0.84f,0f),new Color(1f,0.84f,0f),new Color(1f,0.84f,0f),
        new Color(1f,0.84f,0f),new Color(1f,0.84f,0f),new Color(1f,0.84f,0f),
        new Color(1f,0.84f,0f),new Color(1f,0.84f,0f),new Color(1f,0.84f,0f),
    };

    [Header("Berry 배경색 (2~4096)")]
    [SerializeField] private Color[] berryGradient = new Color[]
    {
        new Color(1f,   0.80f,0.85f), new Color(1f,   0.75f,0.82f),
        new Color(1f,   0.70f,0.79f), new Color(1f,   0.65f,0.76f),
        new Color(1f,   0.60f,0.73f), new Color(1f,   0.55f,0.70f),
        new Color(1f,   0.50f,0.67f), new Color(1f,   0.45f,0.64f),
        new Color(0.98f,0.40f,0.61f), new Color(0.95f,0.35f,0.58f),
        new Color(0.92f,0.25f,0.52f), new Color(0.88f,0.15f,0.46f),
    };

    [Header("Berry 텍스트색 (2~4096)")]
    [SerializeField] private Color[] berryTextColors = new Color[]
    {
        new Color(0.9f,0.9f,0.95f),new Color(0.9f,0.9f,0.95f),new Color(0.9f,0.9f,0.95f),
        new Color(0.9f,0.9f,0.95f),new Color(0.9f,0.9f,0.95f),new Color(0.9f,0.9f,0.95f),
        new Color(0.9f,0.9f,0.95f),new Color(0.9f,0.9f,0.95f),new Color(0.9f,0.9f,0.95f),
        new Color(0.9f,0.9f,0.95f),new Color(0.9f,0.9f,0.95f),new Color(0.9f,0.9f,0.95f),
    };

    private RectTransform rectTransform;
    private Vector2 targetPosition;
    private bool isMoving = false;
    private float moveSpeed = 12f;

    // 파티클 스포너 (같은 GameObject에 자동 추가됨)
    private TileParticleSpawner particleSpawner;

    // ── 하위 호환 static 래퍼 (GunSystem/BossBattleSystem에서 호출) ──
    public static float ParticleSizeCorrectionStatic()      => ParticleScaler.MergeCorrection;
    public static float GunParticleSizeCorrectionStatic()   => ParticleScaler.GunCorrection;
    public static float SmallParticleSizeCorrectionStatic() => ParticleScaler.SmallCorrection;

    // ═══════════════════════════════════════════════
    // Unity 생명주기
    // ═══════════════════════════════════════════════

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        particleSpawner = GetComponent<TileParticleSpawner>();
        if (particleSpawner == null)
            particleSpawner = gameObject.AddComponent<TileParticleSpawner>();
    }

    void Update()
    {
        if (!isMoving) return;
        rectTransform.anchoredPosition = Vector2.Lerp(
            rectTransform.anchoredPosition, targetPosition, Time.deltaTime * moveSpeed);
        if (Vector2.Distance(rectTransform.anchoredPosition, targetPosition) < 0.1f)
        {
            rectTransform.anchoredPosition = targetPosition;
            isMoving = false;
        }
    }

    void OnDestroy()
    {
        if (blinkCoroutine != null) { StopCoroutine(blinkCoroutine); blinkCoroutine = null; }
        StopUltrasonicEffect();
    }

    // ═══════════════════════════════════════════════
    // 값 / 색상 / 이동
    // ═══════════════════════════════════════════════

    public void SetValue(int newValue)
    {
        value = newValue;
        valueText.text = value.ToString();
        Shadow shadow = valueText.GetComponent<Shadow>();
        if (shadow == null) shadow = valueText.gameObject.AddComponent<Shadow>();
        shadow.effectColor    = new Color(0f, 0f, 0f, 1f);
        shadow.effectDistance = new Vector2(5f, -5f);
        shadow.useGraphicAlpha = true;
        UpdateAppearance();
    }

    public void SetColor(TileColor color) { tileColor = color; UpdateAppearance(); }
    public void SetGridPosition(Vector2Int pos) { gridPosition = pos; }

    public void MoveTo(Vector2 position, bool animate = true)
    {
        targetPosition = position;
        if (animate) isMoving = true;
        else rectTransform.anchoredPosition = position;
    }

    void UpdateAppearance()
    {
        int idx = Mathf.Clamp((int)Mathf.Log(value, 2) - 1, 0, chocoGradient.Length - 1);
        if (tileColor == TileColor.Choco)
        {
            background.color = chocoGradient[idx];
            valueText.color  = idx < chocoTextColors.Length ? chocoTextColors[idx] : chocoTextColors[^1];
        }
        else
        {
            background.color = berryGradient[Mathf.Clamp(idx, 0, berryGradient.Length - 1)];
            valueText.color  = idx < berryTextColors.Length ? berryTextColors[idx] : berryTextColors[^1];
        }

    }

    // ═══════════════════════════════════════════════
    // 머지 효과 (파티클 → TileParticleSpawner 위임)
    // ═══════════════════════════════════════════════

    public void MergeWith(Tile other)
    {
        SetValue(value * 2);
        StartCoroutine(PopAnimation());
    }

    public void MergeWithoutParticle()
    {
        SetValue(value * 2);
        StartCoroutine(PopAnimation());
    }

    public void PlayChocoMergeEffect()  => particleSpawner.PlayChocoEffect(rectTransform.anchoredPosition);
    public void PlayBerryMergeEffect()  => particleSpawner.PlayBerryEffect(rectTransform.anchoredPosition);
    public void PlayMixMergeEffect()    => particleSpawner.PlayMixEffect(rectTransform.anchoredPosition);
    public void PlayGunDestroyEffect()  => particleSpawner.PlayGunDestroyEffect(rectTransform.anchoredPosition);

    // ═══════════════════════════════════════════════
    // Pop 애니메이션
    // ═══════════════════════════════════════════════

    IEnumerator PopAnimation()
    {
        float duration = 0.3f;
        float shrink = duration * 0.3f;
        float elapsed = 0f;

        while (elapsed < shrink)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.8f, elapsed / shrink);
            yield return null;
        }

        elapsed = 0f;
        float pop = duration * 0.4f;
        while (elapsed < pop)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / pop;
            float s = 1.70158f * 1.525f; t -= 1;
            float val = t * t * ((s + 1) * t + s) + 1;
            transform.localScale = Vector3.Lerp(Vector3.one * 0.8f, Vector3.one * 1.2f, val);
            yield return null;
        }

        elapsed = 0f;
        float ret = duration * 0.3f;
        while (elapsed < ret)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(Vector3.one * 1.2f, Vector3.one, elapsed / ret);
            yield return null;
        }
        transform.localScale = Vector3.one;
    }

    // ═══════════════════════════════════════════════
    // Gun Mode Outline
    // ═══════════════════════════════════════════════

    public void SetProtected(bool protected_, bool shouldBlink = false)
    {
        isGunProtected = protected_;
        isGunBlinking  = shouldBlink && !protected_;
        if (blinkCoroutine != null) { StopCoroutine(blinkCoroutine); blinkCoroutine = null; }
        if (!isGunProtected && !isGunBlinking) StopUltrasonicEffect();
        else if (isGunBlinking) { blinkCoroutine = StartCoroutine(BlinkBorder()); StartUltrasonicEffect(); }
        else StopUltrasonicEffect();
        RefreshOutline();
    }

    Outline GetOrCreateOutline()
    {
        if (tileOutline != null) return tileOutline;
        tileOutline = background.gameObject.GetComponent<Outline>();
        if (tileOutline == null) tileOutline = background.gameObject.AddComponent<Outline>();
        return tileOutline;
    }

    void RefreshOutline()
    {
        Outline ol = GetOrCreateOutline();
        if (isGunProtected) { ol.effectColor = gunProtectColor; ol.effectDistance = gunProtectDist; ol.enabled = true; return; }
        if (isGunBlinking)  { ol.effectColor = gunBlinkColor;   ol.effectDistance = gunBlinkDist;   ol.enabled = true; return; }
        ol.enabled = false;
    }

    IEnumerator BlinkBorder()
    {
        Outline ol = GetOrCreateOutline();
        Color bright = gunBlinkColor;
        Color dim    = new Color(gunBlinkColor.r, gunBlinkColor.g, gunBlinkColor.b, 0.2f);
        float dur    = 0.6f;
        while (true)
        {
            float e = 0f;
            while (e < dur) { if (ol == null || !ol.enabled) yield break; e += Time.deltaTime; ol.effectColor = Color.Lerp(bright, dim, e / dur); yield return null; }
            e = 0f;
            while (e < dur) { if (ol == null || !ol.enabled) yield break; e += Time.deltaTime; ol.effectColor = Color.Lerp(dim, bright, e / dur); yield return null; }
        }
    }

    // ═══════════════════════════════════════════════
    // Ultrasonic Wave (Gun 모드 대상 타일 효과)
    // ═══════════════════════════════════════════════

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
            if (activeWaveObjects[i] != null) Destroy(activeWaveObjects[i]);
        activeWaveObjects.Clear();
    }

    IEnumerator UltrasonicWaveCoroutine()
    {
        RectTransform bgRect = background.GetComponent<RectTransform>();
        float endSize = Mathf.Max(bgRect.rect.width, bgRect.rect.height);

        while (isUltrasonicActive)
        {
            GameObject waveObj = new GameObject("UltrasonicWave");
            waveObj.transform.SetParent(background.transform, false);
            activeWaveObjects.Add(waveObj);

            Image waveImg = waveObj.AddComponent<Image>();
            waveImg.color = new Color(1f, 1f, 0.3f, 0.15f);

            RectTransform waveRect = waveObj.GetComponent<RectTransform>();
            waveRect.anchorMin = new Vector2(0.5f, 0.5f); waveRect.anchorMax = new Vector2(0.5f, 0.5f);
            waveRect.pivot     = new Vector2(0.5f, 0.5f); waveRect.anchoredPosition = Vector2.zero;
            waveRect.sizeDelta = new Vector2(10f, 10f);

            Outline waveOutline = waveObj.AddComponent<Outline>();
            waveOutline.effectColor    = new Color(1f, 1f, 0.3f, 0.6f);
            waveOutline.effectDistance = new Vector2(3f, 3f);

            float dur = 0.8f, elapsed = 0f;
            while (elapsed < dur && isUltrasonicActive)
            {
                if (waveObj == null) break;
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                float sz = Mathf.Lerp(10f, endSize, t);
                waveRect.sizeDelta    = new Vector2(sz, sz);
                float alpha           = Mathf.Lerp(0.4f, 0f, t);
                waveImg.color         = new Color(1f, 1f, 0.3f, alpha * 0.3f);
                waveOutline.effectColor = new Color(1f, 1f, 0.3f, alpha);
                yield return null;
            }
            if (waveObj != null) { activeWaveObjects.Remove(waveObj); Destroy(waveObj); }
            yield return new WaitForSeconds(0.15f);
        }
    }

    // ═══════════════════════════════════════════════
    // 하위 호환 빈 스텁
    // ═══════════════════════════════════════════════
    public void StartFreezeTextLoop(float timeOffset = 0f) { }
    public void StopFreezeTextLoop() { }
    public void SetFreezeOutline(bool enable) { }
    public void StartGlowEffect() { }
    public void StopGlowEffect() { }
}
