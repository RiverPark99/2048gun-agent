using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class LowHealthVignette : MonoBehaviour
{
    [Header("Vignette Settings")]
    [SerializeField] private Image vignetteImage;
    [SerializeField] private Color vignetteColor = new Color(0.2f, 0.4f, 0.6f, 0f);
    [SerializeField] private float maxAlpha = 0.35f;

    [Header("Thresholds")]
    [SerializeField] private float maxEffectHealthValue = 45f;

    private float currentAlpha = 0f;

    // ⭐ v5.1: 무한대 보스 비네트 강화
    private int infiniteBossVignetteBonus = 0;

    void Start()
    {
        if (vignetteImage == null)
        {
            Debug.LogError("Vignette Image가 할당되지 않았습니다!");
            return;
        }

        vignetteColor.a = 0f;
        vignetteImage.color = vignetteColor;
    }

    public void UpdateVignette(int currentHeat, int maxHeat)
    {
        if (vignetteImage == null) return;

        float effectiveThreshold = maxEffectHealthValue + infiniteBossVignetteBonus;
        float targetAlpha = 0f;

        if (currentHeat <= effectiveThreshold)
        {
            targetAlpha = maxAlpha;
        }
        else
        {
            targetAlpha = 0f;
        }

        DOTween.Kill(vignetteImage);
        vignetteImage.DOKill();

        Color targetColor = vignetteColor;
        targetColor.a = targetAlpha;

        vignetteImage.DOColor(targetColor, 0.3f).SetEase(Ease.InOutQuad);

        currentAlpha = targetAlpha;
    }

    public void UpdateVignetteInstant(int currentHeat, int maxHeat)
    {
        if (vignetteImage == null) return;

        float effectiveThreshold = maxEffectHealthValue + infiniteBossVignetteBonus;
        float targetAlpha = 0f;

        if (currentHeat <= effectiveThreshold)
        {
            targetAlpha = maxAlpha;
        }
        else
        {
            targetAlpha = 0f;
        }

        Color targetColor = vignetteColor;
        targetColor.a = targetAlpha;
        vignetteImage.color = targetColor;

        currentAlpha = targetAlpha;
    }

    // ⭐ v5.1: 무한대 보스 비네트 강화 (20move마다 +1, 최대 +35)
    public void IncreaseInfiniteBossBonus()
    {
        if (infiniteBossVignetteBonus < 35)
        {
            infiniteBossVignetteBonus++;
            Debug.Log($"🔴 비네트 강화! threshold: {maxEffectHealthValue} + {infiniteBossVignetteBonus} = {maxEffectHealthValue + infiniteBossVignetteBonus}");
        }
    }

    // ⭐ v5.1: 리셋
    public void ResetInfiniteBossBonus()
    {
        infiniteBossVignetteBonus = 0;
        Debug.Log("🔴 비네트 보너스 리셋");
    }

    // ⭐ v5.1: 현재 비네트가 최대인지 (guide text 표시용)
    public bool IsVignetteAtMax(int currentHeat)
    {
        float effectiveThreshold = maxEffectHealthValue + infiniteBossVignetteBonus;
        return currentHeat <= effectiveThreshold;
    }
}
