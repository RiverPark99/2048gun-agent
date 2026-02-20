using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class LowHealthVignette : MonoBehaviour
{
    [Header("Vignette Settings")]
    [SerializeField] private Image vignetteImage;
    [SerializeField] private float maxAlpha = 0.5f;

    [Header("Vignette Colors (Inspector에서 수정 가능)")]
    [SerializeField] private Color vignetteBaseColor = new Color(0.2f, 0.4f, 0.6f, 0f);
    [SerializeField] private Color dangerColor1 = new Color(0.2f, 0.4f, 0.7f);
    [SerializeField] private Color dangerColor2 = new Color(0.5f, 0.15f, 0.6f);

    private float currentAlpha = 0f;

    // 적 ATK 기반 동적 기준
    private int enemyAtk = 28;

    // 위험 색상 애니메이션
    private Sequence dangerColorAnim;
    private bool isDangerAnimActive = false;

    void Start()
    {
        if (vignetteImage == null) return;
        Color c = vignetteBaseColor; c.a = 0f;
        vignetteImage.color = c;
    }

    public void SetEnemyAtk(int atk)
    {
        enemyAtk = atk;
    }

    public void UpdateVignette(int currentHeat, int maxHeat)
    {
        if (vignetteImage == null) return;

        float targetAlpha = 0f;
        if (currentHeat <= enemyAtk)
            targetAlpha = maxAlpha;
        else if (currentHeat <= enemyAtk * 2)
            targetAlpha = maxAlpha * (1f - (float)(currentHeat - enemyAtk) / enemyAtk);

        bool shouldDanger = (currentHeat <= enemyAtk && currentHeat > 0);
        if (shouldDanger && !isDangerAnimActive)
            StartDangerColorAnim(targetAlpha);
        else if (!shouldDanger && isDangerAnimActive)
            StopDangerColorAnim();

        if (!isDangerAnimActive)
        {
            vignetteImage.DOKill();
            Color targetColor = vignetteBaseColor;
            targetColor.a = targetAlpha;
            vignetteImage.DOColor(targetColor, 0.3f).SetEase(Ease.InOutQuad);
        }

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

        bool shouldDanger = (currentHeat <= enemyAtk && currentHeat > 0);
        if (shouldDanger && !isDangerAnimActive)
            StartDangerColorAnim(targetAlpha);
        else if (!shouldDanger && isDangerAnimActive)
            StopDangerColorAnim();

        if (!isDangerAnimActive)
        {
            Color targetColor = vignetteBaseColor;
            targetColor.a = targetAlpha;
            vignetteImage.color = targetColor;
        }

        currentAlpha = targetAlpha;
    }

    void StartDangerColorAnim(float alpha)
    {
        StopDangerColorAnim();
        isDangerAnimActive = true;

        Color c1 = dangerColor1; c1.a = alpha;
        Color c2 = dangerColor2; c2.a = alpha;

        vignetteImage.DOKill();
        vignetteImage.color = c1;

        dangerColorAnim = DOTween.Sequence();
        dangerColorAnim.Append(vignetteImage.DOColor(c2, 0.6f).SetEase(Ease.InOutSine));
        dangerColorAnim.Append(vignetteImage.DOColor(c1, 0.6f).SetEase(Ease.InOutSine));
        dangerColorAnim.SetLoops(-1, LoopType.Restart);
    }

    void StopDangerColorAnim()
    {
        if (dangerColorAnim != null) { dangerColorAnim.Kill(); dangerColorAnim = null; }
        isDangerAnimActive = false;
        if (vignetteImage != null)
        {
            vignetteImage.DOKill();
            Color c = vignetteBaseColor; c.a = 0f;
            vignetteImage.color = c;
        }
    }

    public void IncreaseInfiniteBossBonus() { }

    public void ResetInfiniteBossBonus()
    {
        StopDangerColorAnim();
        enemyAtk = 28;
        if (vignetteImage != null)
        {
            vignetteImage.DOKill();
            Color c = vignetteBaseColor; c.a = 0f;
            vignetteImage.color = c;
        }
    }

    public bool IsVignetteAtMax(int currentHeat) { return currentHeat <= enemyAtk; }
}
