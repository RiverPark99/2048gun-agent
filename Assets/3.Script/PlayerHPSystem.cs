// =====================================================
// PlayerHPSystem.cs - v6.9
// Player HP(Heat) 관리, UI, 비네트, 피격 효과
// v6.9: 위험 HP 깜빡임, LevelUp 테이블 SerializeField
// =====================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using DG.Tweening;

public class PlayerHPSystem : MonoBehaviour
{
    [Header("Heat UI")]
    [SerializeField] private TextMeshProUGUI heatText;
    [SerializeField] private Slider heatSlider;
    [SerializeField] private Image heatBarImage;
    [SerializeField] private int maxHeat = 100;
    [SerializeField] private float heatAnimationDuration = 0.3f;

    [Header("Heat Recovery")]
    [SerializeField] private int[] comboHeatRecover = { 0, 0, 4, 10, 18, 30 };
    [SerializeField, Range(0f, 1f)] private float berryHealPercent = 0.12f;
    [SerializeField, Range(0f, 1f)] private float mixHealPercent = 0.06f;

    [Header("Level Up UI")]
    [SerializeField] private GameObject levelUpPanel;
    [SerializeField] private TextMeshProUGUI levelUpText;
    [SerializeField] private GameObject levelUpTapHintObj;

    [Header("Level Up 확률 테이블")]
    [SerializeField, Range(0f, 1f)] private float bronzeChance = 0.60f;
    [SerializeField] private int bronzeMin = 2;
    [SerializeField] private int bronzeMax = 5;
    [SerializeField, Range(0f, 1f)] private float silverChance = 0.39f;
    [SerializeField] private int silverMin = 10;
    [SerializeField] private int silverMax = 15;
    [SerializeField] private int goldValue = 40;

    [Header("Low Health Effect")]
    [SerializeField] private LowHealthVignette lowHealthVignette;

    [Header("피격 플래시 효과")]
    [SerializeField] private Image damageFlashImage;

    [Header("Damage Text")]
    [SerializeField] private GameObject damageTextPrefab;
    [SerializeField] private Transform damageTextParent;

    [Header("References")]
    [SerializeField] private GunSystem gunSystem;

    // HP 상태
    private int currentHeat = 100;
    private bool isLevelUpAnimating = false;
    public bool IsLevelUpAnimating => isLevelUpAnimating;

    // 등급별 색상
    private static readonly Color COLOR_BRONZE = new Color(0.80f, 0.50f, 0.20f);
    private static readonly Color COLOR_SILVER = new Color(0.75f, 0.75f, 0.80f);
    private static readonly Color COLOR_GOLD   = new Color(1.00f, 0.84f, 0.00f);

    // 셔플 중 알록달록 색상
    private static readonly Color[] SHUFFLE_COLORS = {
        new Color(0.4f, 0.8f, 1f),
        new Color(0.4f, 1f, 0.6f),
        new Color(1f, 0.85f, 0.3f),
        new Color(1f, 0.6f, 0.3f),
        new Color(0.3f, 1f, 0.3f),
        new Color(0.6f, 1f, 0.5f),
        new Color(0.85f, 1f, 0.2f),
        new Color(1f, 1f, 0.2f),
        new Color(1f, 0.35f, 0.55f),
    };

    // UI 애니메이션 상태
    private float heatTextOriginalY = 0f;
    private bool heatTextInitialized = false;
    private int lastCurrentHeat = 0;

    // 위험 HP 깜빡임
    private Sequence dangerHPFlashAnim;
    private bool isDangerHPFlashing = false;

    // === 프로퍼티 ===
    public int CurrentHeat => currentHeat;
    public int MaxHeat => maxHeat;
    public TextMeshProUGUI HeatText => heatText;
    public int[] ComboHeatRecover => comboHeatRecover;
    public LowHealthVignette LowHealthVignette => lowHealthVignette;

    // Mix merge 기준 Berry 1개 회복력 (내림)
    public int GetMixHealPower() => Mathf.FloorToInt(mixHealPercent * maxHeat);

    public void Initialize()
    {
        if (heatSlider != null)
        {
            heatSlider.minValue = 0;
            heatSlider.maxValue = maxHeat;
            heatSlider.value = maxHeat;
        }

        if (damageFlashImage != null)
        {
            damageFlashImage.color = new Color(damageFlashImage.color.r, damageFlashImage.color.g, damageFlashImage.color.b, 0f);
            damageFlashImage.gameObject.SetActive(false);
        }

        currentHeat = maxHeat;
        UpdateHeatUI(true);

        if (levelUpPanel != null) levelUpPanel.SetActive(false);
        if (levelUpTapHintObj != null) levelUpTapHintObj.SetActive(false);
    }

    public void ResetState()
    {
        maxHeat = 100;
        currentHeat = maxHeat;

        if (lowHealthVignette != null)
            lowHealthVignette.ResetInfiniteBossBonus();

        StopDangerHPFlash();

        if (levelUpPanel != null) levelUpPanel.SetActive(false);
        if (levelUpTapHintObj != null) levelUpTapHintObj.SetActive(false);
        UpdateHeatUI(true);
    }

    // === HP 조작 ===
    public void AddHeat(int amount)
    {
        currentHeat += amount;
        if (currentHeat > maxHeat)
            currentHeat = maxHeat;
    }

    public void SetHeatToMax()
    {
        currentHeat = maxHeat;
    }

    public void ClampHeat()
    {
        if (currentHeat > maxHeat)
            currentHeat = maxHeat;
        if (currentHeat < 0)
            currentHeat = 0;
    }

    public void TakeDamage(int damage)
    {
        int oldHeat = currentHeat;
        currentHeat -= damage;
        if (currentHeat < 0)
            currentHeat = 0;

        UpdateHeatUI(false);
        StartCoroutine(FlashOrangeOnDamage());

        if (damageFlashImage != null)
            StartCoroutine(FlashDamageImage());

        int actualDamage = oldHeat - currentHeat;
        if (actualDamage > 0)
            ShowHeatChangeText(-actualDamage);

        Debug.Log($"⚠️ 보스 공격 피해: -{damage} Heat (Current: {currentHeat}/{maxHeat})");
    }

    // === 보스 처치 보상 ===
    public void OnBossDefeated(int bossLevel, bool isClearMode)
    {
        if (isClearMode || bossLevel >= 40) return;

        // SerializeField 기반 확률 테이블
        float roll = Random.value;
        int heatIncrease;
        if (roll < bronzeChance)
            heatIncrease = Random.Range(bronzeMin, bronzeMax + 1);
        else if (roll < bronzeChance + silverChance)
            heatIncrease = Random.Range(silverMin, silverMax + 1);
        else
            heatIncrease = goldValue;

        Debug.Log($"보스 처치! Max HP +{heatIncrease} 예정 (roll={roll:F3})");

        if (levelUpCoroutine != null) StopCoroutine(levelUpCoroutine);
        levelUpCoroutine = StartCoroutine(LevelUpRouletteCoroutine(heatIncrease));
    }

    private Coroutine levelUpCoroutine;

    // 확률 테이블 기반 색상/라벨
    Color GetColorForValue(int value)
    {
        if (value >= goldValue) return COLOR_GOLD;
        if (value >= silverMin) return COLOR_SILVER;
        return COLOR_BRONZE;
    }

    string GetLabelForValue(int value)
    {
        return $"Level UP! MaxHP +{value}";
    }

    // 룰렛용 랜덤 값 생성
    int GetRandomLevelUpValue()
    {
        float roll = Random.value;
        if (roll < bronzeChance)
            return Random.Range(bronzeMin, bronzeMax + 1);
        else if (roll < bronzeChance + silverChance)
            return Random.Range(silverMin, silverMax + 1);
        else
            return goldValue;
    }

    IEnumerator LevelUpRouletteCoroutine(int finalIncrease)
    {
        isLevelUpAnimating = true;

        CanvasGroup panelCG = null;
        if (levelUpPanel != null)
        {
            levelUpPanel.SetActive(true);
            panelCG = levelUpPanel.GetComponent<CanvasGroup>();
            if (panelCG == null) panelCG = levelUpPanel.AddComponent<CanvasGroup>();
            panelCG.alpha = 1f;
        }

        bool hasText = (levelUpText != null);

        // === Phase 1: 랜덤 텍스트 셔플 (2.0초) ===
        float shuffleTime = 2.0f;
        float elapsed = 0f;
        while (elapsed < shuffleTime)
        {
            if (hasText)
            {
                int randVal = GetRandomLevelUpValue();
                levelUpText.text = GetLabelForValue(randVal);
                levelUpText.color = SHUFFLE_COLORS[Random.Range(0, SHUFFLE_COLORS.Length)];
            }
            float interval = Mathf.Lerp(0.06f, 0.18f, elapsed / shuffleTime);
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        // === Phase 2: 최종 숫자 픽스 ===
        if (hasText)
        {
            levelUpText.text = GetLabelForValue(finalIncrease);
            levelUpText.color = GetColorForValue(finalIncrease);

            float popScale;
            float popDuration;
            if (finalIncrease >= goldValue)       { popScale = 1.8f; popDuration = 0.35f; }
            else if (finalIncrease >= silverMin)  { popScale = 1.5f; popDuration = 0.28f; }
            else                                  { popScale = 1.3f; popDuration = 0.20f; }

            RectTransform tr = levelUpText.GetComponent<RectTransform>();
            tr.DOKill();
            tr.localScale = Vector3.one * popScale;
            tr.DOScale(1f, popDuration).SetEase(Ease.OutBack);
        }

        // === Phase 3: 실제 HP 증가 + 회복 + 회복력 UI 즉시 반영 ===
        maxHeat += finalIncrease;
        int oldHeat = currentHeat;
        currentHeat = maxHeat;
        UpdateHeatUI();

        int recovery = currentHeat - oldHeat;
        if (recovery > 0)
            ShowHeatChangeText(recovery);

        // 회복력 UI 즉시 갱신 (레벨업과 동시)
        if (gunSystem != null) gunSystem.UpdateHealPowerUI();

        Debug.Log($"[LevelUP] Max HP +{finalIncrease}: {maxHeat}");

        yield return new WaitForSeconds(1.0f);

        // === Phase 4: TimeScale 정지 → 터치 대기 ===
        Time.timeScale = 0f;

        Tweener tapHintTween = null;
        if (levelUpTapHintObj != null)
        {
            levelUpTapHintObj.SetActive(true);
            CanvasGroup tapCG = levelUpTapHintObj.GetComponent<CanvasGroup>();
            if (tapCG == null) tapCG = levelUpTapHintObj.AddComponent<CanvasGroup>();
            tapCG.alpha = 1f;
            tapHintTween = tapCG.DOFade(0.5f, 0.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
        }

        yield return new WaitForSecondsRealtime(0.2f);

        while (!Input.GetMouseButtonDown(0) && !Input.anyKeyDown)
            yield return null;

        if (tapHintTween != null) tapHintTween.Kill();
        if (levelUpTapHintObj != null) levelUpTapHintObj.SetActive(false);

        Time.timeScale = 1f;
        isLevelUpAnimating = false;

        // === Phase 5: 페이드아웃 ===
        if (panelCG != null)
        {
            panelCG.DOKill();
            panelCG.DOFade(0f, 1.0f).SetEase(Ease.InQuad).OnComplete(() => {
                if (levelUpPanel != null) levelUpPanel.SetActive(false);
            });
        }

        levelUpCoroutine = null;
    }

    public int GetBerryHealAmount()
    {
        return Mathf.FloorToInt(maxHeat * berryHealPercent);
    }

    public int GetMixHealAmount()
    {
        return Mathf.FloorToInt(maxHeat * mixHealPercent);
    }

    // === Heat UI ===
    public void UpdateHeatUI(bool instant = false)
    {
        if (heatText != null)
        {
            heatText.text = $"HP : {currentHeat}/{maxHeat}";

            if (!heatTextInitialized)
            {
                RectTransform textRect = heatText.GetComponent<RectTransform>();
                heatTextOriginalY = textRect.anchoredPosition.y;
                heatTextInitialized = true;
            }

            if (currentHeat > lastCurrentHeat)
            {
                RectTransform textRect = heatText.GetComponent<RectTransform>();
                textRect.DOKill();

                Sequence seq = DOTween.Sequence();
                seq.Append(textRect.DOAnchorPosY(heatTextOriginalY + 12f, 0.2f).SetEase(Ease.OutQuad));
                seq.Append(textRect.DOAnchorPosY(heatTextOriginalY, 0.2f).SetEase(Ease.InQuad));
                seq.OnComplete(() => {
                    if (textRect != null)
                        textRect.anchoredPosition = new Vector2(textRect.anchoredPosition.x, heatTextOriginalY);
                });
            }

            lastCurrentHeat = currentHeat;

            // HP bar 색상 % 기반
            float hp = (float)currentHeat / maxHeat;
            Color hc;
            if (hp >= 0.8f)
                hc = new Color(1f, 0.3f, 0.55f);
            else if (hp >= 0.4f)
            {
                float t = (hp - 0.4f) / 0.4f;
                hc = Color.Lerp(new Color(1f, 0.6f, 0.75f), new Color(1f, 0.3f, 0.55f), t);
            }
            else
            {
                float t = hp / 0.4f;
                hc = Color.Lerp(new Color(1f, 0.75f, 0.85f), new Color(1f, 0.6f, 0.75f), t);
            }

            // 위험 HP 깜빡임 체크
            bool shouldDangerFlash = (lowHealthVignette != null && lowHealthVignette.IsVignetteAtMax(currentHeat) && currentHeat > 0);
            if (shouldDangerFlash && !isDangerHPFlashing)
                StartDangerHPFlash();
            else if (!shouldDangerFlash && isDangerHPFlashing)
                StopDangerHPFlash();

            // 깜빡임 중이 아닐 때만 색상 직접 설정
            if (!isDangerHPFlashing)
            {
                heatText.color = hc;
                if (heatBarImage != null) heatBarImage.color = hc;
            }
        }

        if (heatSlider != null)
        {
            heatSlider.maxValue = maxHeat;
            heatSlider.DOKill();

            if (instant)
                heatSlider.value = currentHeat;
            else
                heatSlider.DOValue(currentHeat, heatAnimationDuration).SetEase(Ease.OutCubic);
        }

        if (lowHealthVignette != null)
        {
            if (instant)
                lowHealthVignette.UpdateVignetteInstant(currentHeat, maxHeat);
            else
                lowHealthVignette.UpdateVignette(currentHeat, maxHeat);
        }
    }

    // === 위험 HP 깜빡임 (붉은색↔흰색) ===
    void StartDangerHPFlash()
    {
        StopDangerHPFlash();
        isDangerHPFlashing = true;

        Color flashRed = new Color(1f, 0.2f, 0.2f);
        Color flashWhite = Color.white;

        dangerHPFlashAnim = DOTween.Sequence();

        if (heatText != null)
        {
            heatText.DOKill();
            heatText.color = flashRed;
            dangerHPFlashAnim.Append(heatText.DOColor(flashWhite, 0.2f).SetEase(Ease.InOutSine));
            dangerHPFlashAnim.Append(heatText.DOColor(flashRed, 0.2f).SetEase(Ease.InOutSine));
        }

        if (heatBarImage != null)
        {
            heatBarImage.DOKill();
            heatBarImage.color = flashRed;
            dangerHPFlashAnim.Join(heatBarImage.DOColor(flashWhite, 0.2f).SetEase(Ease.InOutSine));
            dangerHPFlashAnim.Join(heatBarImage.DOColor(flashRed, 0.2f).SetEase(Ease.InOutSine));
        }

        dangerHPFlashAnim.SetLoops(-1, LoopType.Restart);
    }

    void StopDangerHPFlash()
    {
        if (dangerHPFlashAnim != null) { dangerHPFlashAnim.Kill(); dangerHPFlashAnim = null; }
        isDangerHPFlashing = false;
    }

    // === 체력 변화 텍스트 ===
    public void ShowHeatChangeText(int change, string bonusText = "")
    {
        if (damageTextPrefab == null || damageTextParent == null || heatText == null) return;

        GameObject heatChangeObj = Instantiate(damageTextPrefab, damageTextParent);
        TextMeshProUGUI heatChangeText = heatChangeObj.GetComponent<TextMeshProUGUI>();

        if (heatChangeText != null)
        {
            if (change > 0)
            {
                if (!string.IsNullOrEmpty(bonusText))
                {
                    heatChangeText.text = $"{bonusText}\n+{change}";
                    heatChangeText.alignment = TextAlignmentOptions.Center;
                }
                else
                {
                    heatChangeText.text = "+" + change;
                }
                heatChangeText.color = new Color(0.3f, 1f, 0.3f);
            }
            else
            {
                heatChangeText.text = change.ToString();
                heatChangeText.color = new Color(0.5f, 0.8f, 1f);
            }

            heatChangeText.fontSize = 40;

            RectTransform heatChangeRect = heatChangeObj.GetComponent<RectTransform>();
            RectTransform heatTextRect = heatText.GetComponent<RectTransform>();

            heatChangeRect.position = heatTextRect.position;

            CanvasGroup canvasGroup = heatChangeObj.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = heatChangeObj.AddComponent<CanvasGroup>();

            Sequence heatSequence = DOTween.Sequence();

            heatSequence.Append(heatChangeRect.DOAnchorPosY(heatChangeRect.anchoredPosition.y + 100f, 1.0f).SetEase(Ease.OutCubic));
            heatSequence.Join(canvasGroup.DOFade(0f, 1.0f).SetEase(Ease.InCubic));

            heatSequence.Insert(0f, heatChangeRect.DOScale(1.2f, 0.15f).SetEase(Ease.OutQuad));
            heatSequence.Insert(0.15f, heatChangeRect.DOScale(1f, 0.15f).SetEase(Ease.InQuad));

            heatSequence.OnComplete(() => {
                if (heatChangeObj != null) Destroy(heatChangeObj);
            });
        }
    }

    // _16: Heal laser 시 HP bar 초록색 점멸 후 원래색 복관 (0.2초)
    public void FlashHealGreen()
    {
        if (heatBarImage == null) return;
        heatBarImage.DOKill();
        heatBarImage.color = new Color(0.2f, 1f, 0.4f);
        // heatSlider value 기반으로 최신 색상 계산해서 복원 (만탕 시에도 정확하게)
        heatBarImage.DOColor(GetCurrentBarColor(), 0.2f).SetEase(Ease.OutQuad);
    }

    // 현재 HP 비율 기준 bar 색상 (UpdateHeatUI와 동일한 로직)
    Color GetCurrentBarColor()
    {
        float hp = (float)currentHeat / maxHeat;
        if (hp >= 0.8f)
            return new Color(1f, 0.3f, 0.55f);
        else if (hp >= 0.4f)
        {
            float t = (hp - 0.4f) / 0.4f;
            return Color.Lerp(new Color(1f, 0.6f, 0.75f), new Color(1f, 0.3f, 0.55f), t);
        }
        else
        {
            float t = hp / 0.4f;
            return Color.Lerp(new Color(1f, 0.75f, 0.85f), new Color(1f, 0.6f, 0.75f), t);
        }
    }

    // === 피격 플래시 효과 ===
    IEnumerator FlashOrangeOnDamage()
    {
        if (heatBarImage == null || heatText == null) yield break;

        Color originalBarColor = heatBarImage.color;
        Color originalTextColor = heatText.color;

        Color orangeColor = new Color(1f, 0.65f, 0f);
        heatBarImage.color = orangeColor;
        heatText.color = orangeColor;

        yield return new WaitForSeconds(0.15f);

        heatBarImage.color = originalBarColor;
        heatText.color = originalTextColor;
    }

    IEnumerator FlashDamageImage()
    {
        if (damageFlashImage == null) yield break;

        damageFlashImage.gameObject.SetActive(true);
        damageFlashImage.DOKill();

        float startAlpha = 190f / 255f;
        Color flashColor = damageFlashImage.color;
        damageFlashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, startAlpha);

        damageFlashImage.DOFade(0f, 0.05f).SetEase(Ease.OutCubic).OnComplete(() => {
            if (damageFlashImage != null)
                damageFlashImage.gameObject.SetActive(false);
        });

        yield break;
    }
}
