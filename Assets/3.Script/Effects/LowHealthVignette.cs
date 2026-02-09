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
    [SerializeField] private float maxEffectHealthValue = 45f;  // ⭐ UPDATED: 45 HP 이하면 최대 효과

    private float currentAlpha = 0f;

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

    // ⭐ UPDATED: HP 45 기준으로 변경
    public void UpdateVignette(int currentHeat, int maxHeat)
    {
        if (vignetteImage == null) return;

        float targetAlpha = 0f;

        if (currentHeat <= maxEffectHealthValue)
        {
            // 45 HP 이하: 최대 효과
            targetAlpha = maxAlpha;
        }
        else
        {
            // 45 HP 초과: 효과 없음
            targetAlpha = 0f;
        }

        // 부드럽게 변화
        DOTween.Kill(vignetteImage);
        vignetteImage.DOKill();

        Color targetColor = vignetteColor;
        targetColor.a = targetAlpha;

        vignetteImage.DOColor(targetColor, 0.3f).SetEase(Ease.InOutQuad);

        currentAlpha = targetAlpha;
    }

    // ⭐ UPDATED: HP 45 기준으로 변경
    public void UpdateVignetteInstant(int currentHeat, int maxHeat)
    {
        if (vignetteImage == null) return;

        float targetAlpha = 0f;

        if (currentHeat <= maxEffectHealthValue)
        {
            // 45 HP 이하: 최대 효과
            targetAlpha = maxAlpha;
        }
        else
        {
            // 45 HP 초과: 효과 없음
            targetAlpha = 0f;
        }

        Color targetColor = vignetteColor;
        targetColor.a = targetAlpha;
        vignetteImage.color = targetColor;

        currentAlpha = targetAlpha;
    }
}
