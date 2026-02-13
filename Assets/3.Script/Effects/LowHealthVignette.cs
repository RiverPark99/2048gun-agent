using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class LowHealthVignette : MonoBehaviour
{
    [Header("Vignette Settings")]
    [SerializeField] private Image vignetteImage;
    [SerializeField] private Color vignetteColor = new Color(0.2f, 0.4f, 0.6f, 0f);
    [SerializeField] private float maxAlpha = 0.35f;

    private float currentAlpha = 0f;

    // ⭐ v6.4: 적 ATK 기반 동적 기준
    private int enemyAtk = 28;

    void Start()
    {
        if (vignetteImage == null) return;
        vignetteColor.a = 0f;
        vignetteImage.color = vignetteColor;
    }

    // ⭐ v6.4: 적 ATK 설정 (최소=ATK*2, 최대=ATK 이하)
    public void SetEnemyAtk(int atk)
    {
        enemyAtk = atk;
    }

    public void UpdateVignette(int currentHeat, int maxHeat)
    {
        if (vignetteImage == null) return;

        // 최소 활성화: HP <= ATK*2, 최대: HP <= ATK
        float targetAlpha = 0f;
        if (currentHeat <= enemyAtk)
            targetAlpha = maxAlpha;
        else if (currentHeat <= enemyAtk * 2)
            targetAlpha = maxAlpha * (1f - (float)(currentHeat - enemyAtk) / enemyAtk);

        vignetteImage.DOKill();
        Color targetColor = vignetteColor;
        targetColor.a = targetAlpha;
        vignetteImage.DOColor(targetColor, 0.3f).SetEase(Ease.InOutQuad);
        currentAlpha = targetAlpha;
    }

    public void UpdateVignetteInstant(int currentHeat, int maxHeat)
    {
        if (vignetteImage == null) return;

        float targetAlpha = 0f;
        if (currentHeat <= enemyAtk)
            targetAlpha = maxAlpha;
        else if (currentHeat <= enemyAtk * 2)
            targetAlpha = maxAlpha * (1f - (float)(currentHeat - enemyAtk) / enemyAtk);

        Color targetColor = vignetteColor;
        targetColor.a = targetAlpha;
        vignetteImage.color = targetColor;
        currentAlpha = targetAlpha;
    }

    // 레거시 호환용 (무해)
    public void IncreaseInfiniteBossBonus() { }
    public void ResetInfiniteBossBonus() { }
    public bool IsVignetteAtMax(int currentHeat) { return currentHeat <= enemyAtk; }
}
