using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class LowHealthVignette : MonoBehaviour
{
    [Header("Vignette Settings")]
    [SerializeField] private Image vignetteImage;  // 비네팅 이미지
    [SerializeField] private Color vignetteColor = new Color(0.2f, 0.4f, 0.6f, 0f);  // 푸른색 (초기 투명)
    [SerializeField] private float maxAlpha = 0.35f;  // 최대 투명도 (0.6 -> 0.35)

    [Header("Thresholds")]
    [SerializeField] private float startHealthPercent = 0.4f;  // 40% 이하부터 효과 시작
    [SerializeField] private float maxEffectHealthPercent = 0.2f;  // 20% 이하면 최대 효과

    private float currentAlpha = 0f;

    void Start()
    {
        if (vignetteImage == null)
        {
            Debug.LogError("Vignette Image가 할당되지 않았습니다!");
            return;
        }

        // 초기 색상 설정
        vignetteColor.a = 0f;
        vignetteImage.color = vignetteColor;
    }

    // GameManager에서 체력 변화 시 호출
    public void UpdateVignette(int currentHeat, int maxHeat)
    {
        if (vignetteImage == null) return;

        float healthPercent = (float)currentHeat / maxHeat;

        float targetAlpha = 0f;

        if (healthPercent <= maxEffectHealthPercent)
        {
            // 20% 이하: 최대 효과
            targetAlpha = maxAlpha;
        }
        else if (healthPercent <= startHealthPercent)
        {
            // 20%~40%: 점진적 효과
            float t = 1f - ((healthPercent - maxEffectHealthPercent) / (startHealthPercent - maxEffectHealthPercent));
            targetAlpha = t * maxAlpha;
        }
        else
        {
            // 40% 이상: 효과 없음
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

    // 즉시 업데이트 (게임 시작 시)
    public void UpdateVignetteInstant(int currentHeat, int maxHeat)
    {
        if (vignetteImage == null) return;

        float healthPercent = (float)currentHeat / maxHeat;

        float targetAlpha = 0f;

        if (healthPercent <= maxEffectHealthPercent)
        {
            targetAlpha = maxAlpha;
        }
        else if (healthPercent <= startHealthPercent)
        {
            float t = 1f - ((healthPercent - maxEffectHealthPercent) / (startHealthPercent - maxEffectHealthPercent));
            targetAlpha = t * maxAlpha;
        }

        Color targetColor = vignetteColor;
        targetColor.a = targetAlpha;
        vignetteImage.color = targetColor;

        currentAlpha = targetAlpha;
    }
}
