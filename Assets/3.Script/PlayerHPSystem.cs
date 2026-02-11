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

    [Header("Low Health Effect")]
    [SerializeField] private LowHealthVignette lowHealthVignette;

    [Header("피격 플래시 효과")]
    [SerializeField] private Image damageFlashImage;

    [Header("Damage Text")]
    [SerializeField] private GameObject damageTextPrefab;
    [SerializeField] private Transform damageTextParent;

    // HP 상태
    private int currentHeat = 100;
    private const int BOSS_DEFEAT_MAX_HEAT_INCREASE = 1;

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
    public void OnBossDefeated(int bossLevel)
    {
        int heatIncrease = BOSS_DEFEAT_MAX_HEAT_INCREASE;
        if (bossLevel == 39)
        {
            heatIncrease = 2;
            Debug.Log("⭐ Stage 39 클리어! 최대 체력 +2!");
        }

        maxHeat += heatIncrease;
        Debug.Log($"보스 처치! 최대 히트 +{heatIncrease}: {maxHeat}");

        int oldHeat = currentHeat;
        currentHeat = maxHeat;

        UpdateHeatUI();

        int recovery = currentHeat - oldHeat;
        if (recovery > 0)
            ShowHeatChangeText(recovery);
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

            float hp = (float)currentHeat / maxHeat;
            Color hc;
            if (hp <= 0.2f)
                hc = new Color(1f, 0.6f, 0.7f);
            else if (hp <= 0.4f)
                hc = new Color(1f, 0.5f, 0.65f);
            else if (hp <= 0.6f)
                hc = new Color(1f, 0.4f, 0.6f);
            else
                hc = new Color(1f, 0.3f, 0.55f);

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
