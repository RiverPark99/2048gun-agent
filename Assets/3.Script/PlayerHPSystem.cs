// =====================================================
// PlayerHPSystem.cs - v6.0
// Player HP(Heat) 관리, UI, 비네트, 피격 효과
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
    [SerializeField] private int berryMergeHealMultiplier = 4;
    [SerializeField] private int berryMergeBaseHeal = 5;

    [Header("Level Up UI")]
    [SerializeField] private TextMeshProUGUI levelUpText;

    [Header("Low Health Effect")]
    [SerializeField] private LowHealthVignette lowHealthVignette;

    [Header("피격 플래시 효과")]
    [SerializeField] private Image damageFlashImage;

    [Header("Damage Text")]
    [SerializeField] private GameObject damageTextPrefab;
    [SerializeField] private Transform damageTextParent;

    // HP 상태
    private int currentHeat = 100;
    private bool isLevelUpAnimating = false;
    public bool IsLevelUpAnimating => isLevelUpAnimating;

    // ⭐ v6.4: 숫자별 색상 (1~5)
    private static readonly Color[] levelUpColors = {
        new Color(0.4f, 1f, 0.4f),    // 1: 연두
        new Color(0.4f, 0.8f, 1f),    // 2: 하늘
        new Color(1f, 0.85f, 0.3f),   // 3: 금색
        new Color(1f, 0.5f, 0.3f),    // 4: 주황
        new Color(1f, 0.35f, 0.55f),  // 5: 핑크
    };

    // UI 애니메이션 상태
    private float heatTextOriginalY = 0f;
    private bool heatTextInitialized = false;
    private int lastCurrentHeat = 0;

    // Player HP 핑크 색상

    // === 프로퍼티 ===
    public int CurrentHeat => currentHeat;
    public int MaxHeat => maxHeat;
    public TextMeshProUGUI HeatText => heatText;
    public int[] ComboHeatRecover => comboHeatRecover;
    public int BerryMergeHealMultiplier => berryMergeHealMultiplier;
    public int BerryMergeBaseHeal => berryMergeBaseHeal;
    public LowHealthVignette LowHealthVignette => lowHealthVignette;

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
    }

    public void ResetState()
    {
        maxHeat = 100;
        currentHeat = maxHeat;

        if (lowHealthVignette != null)
            lowHealthVignette.ResetInfiniteBossBonus();

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
        if (isClearMode) return;

        int heatIncrease = Random.Range(1, 6); // 1~5 랜덤
        Debug.Log($"보스 처치! Max HP +{heatIncrease} 예정");

        // 이전 룰렛 정리
        if (levelUpCoroutine != null) StopCoroutine(levelUpCoroutine);
        levelUpCoroutine = StartCoroutine(LevelUpRouletteCoroutine(heatIncrease));
    }

    private Coroutine levelUpCoroutine;

    IEnumerator LevelUpRouletteCoroutine(int finalIncrease)
    {
        isLevelUpAnimating = true;

        bool hasUI = (levelUpText != null);
        CanvasGroup cg = null;

        if (hasUI)
        {
            // 페이드 중 재발동 시 즉시 리셋
            levelUpText.DOKill();
            levelUpText.gameObject.SetActive(true);
            cg = levelUpText.GetComponent<CanvasGroup>();
            if (cg == null) cg = levelUpText.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
        }

        // === Phase 1: 룰렛 (1.2초) - 숫자 1~5 랜덤 알록달록 ===
        int totalTicks = 15; // 1.2초 / 0.08초 = 15번
        for (int i = 0; i < totalTicks; i++)
        {
            if (hasUI)
            {
                int randomNum = Random.Range(1, 6);
                levelUpText.text = $"Level UP! Max HP+{randomNum}";
                levelUpText.color = levelUpColors[randomNum - 1];
            }
            yield return new WaitForSeconds(0.08f);
        }

        // === Phase 2: 최종 숫자 픽스 ===
        if (hasUI)
        {
            levelUpText.text = $"Level UP! Max HP+{finalIncrease}";
            levelUpText.color = levelUpColors[finalIncrease - 1];
        }

        // === Phase 3: 실제 HP 증가 + 회복 ===
        maxHeat += finalIncrease;
        int oldHeat = currentHeat;
        currentHeat = maxHeat;
        UpdateHeatUI();

        int recovery = currentHeat - oldHeat;
        if (recovery > 0)
            ShowHeatChangeText(recovery);

        Debug.Log($"[LevelUP] Max HP +{finalIncrease}: {maxHeat}");

        // 보스 스폰 허용
        isLevelUpAnimating = false;

        // === Phase 4: 2초 표시 후 페이드아웃 ===
        yield return new WaitForSeconds(1.5f);

        if (hasUI && cg != null)
        {
            levelUpText.DOKill();
            cg.DOFade(0f, 0.5f).SetEase(Ease.InQuad).OnComplete(() => {
                if (levelUpText != null) levelUpText.gameObject.SetActive(false);
            });
        }

        levelUpCoroutine = null;
    }

    // Berry 회복량: 최대HP의 3%
    public int GetBerryHealAmount()
    {
        return Mathf.FloorToInt(maxHeat * 0.03f);
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

            // ⭐ v6.4: HP bar 색상 % 기반 (핑크→푸른 연한색)
            float hp = (float)currentHeat / maxHeat;
            Color hc;
            if (hp >= 0.8f)
                hc = new Color(1f, 0.3f, 0.55f);        // 100~80%: 핑크
            else if (hp >= 0.4f)
            {
                float t = (hp - 0.4f) / 0.4f; // 1.0 at 80%, 0.0 at 40%
                hc = Color.Lerp(
                    new Color(0.55f, 0.7f, 0.9f),       // 40%: 푸른 연한색
                    new Color(1f, 0.3f, 0.55f),          // 80%: 핑크
                    t);
            }
            else
            {
                float t = hp / 0.4f; // 1.0 at 40%, 0.0 at 0%
                hc = Color.Lerp(
                    new Color(0.6f, 0.85f, 1f),          // 0%: 가장 연한 푸른색
                    new Color(0.55f, 0.7f, 0.9f),        // 40%: 푸른 연한색
                    t);
            }

            heatText.color = hc;

            if (heatBarImage != null)
                heatBarImage.color = hc;
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
