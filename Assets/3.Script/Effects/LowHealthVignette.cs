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

    // ⭐ v6.4: 위험 색상 애니메이션 (맞으면 죽는 피)
    private Sequence dangerColorAnim;
    private bool isDangerAnimActive = false;
    private static readonly Color DANGER_BLUE = new Color(0.2f, 0.4f, 0.6f);
    private static readonly Color DANGER_RED  = new Color(0.7f, 0.15f, 0.15f);

    void Start()
    {
        if (vignetteImage == null) return;
        vignetteColor.a = 0f;
        vignetteImage.color = vignetteColor;
    }

    // ⭐ v6.4: 적 ATK 설정
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

        // ⭐ v6.4: 맞으면 죽는 피 → 색상 깜빡임
        bool shouldDanger = (currentHeat <= enemyAtk && currentHeat > 0);
        if (shouldDanger && !isDangerAnimActive)
            StartDangerColorAnim(targetAlpha);
        else if (!shouldDanger && isDangerAnimActive)
            StopDangerColorAnim();

        if (!isDangerAnimActive)
        {
            vignetteImage.DOKill();
            Color targetColor = vignetteColor;
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
            Color targetColor = vignetteColor;
            targetColor.a = targetAlpha;
            vignetteImage.color = targetColor;
        }

        currentAlpha = targetAlpha;
    }

    // ⭐ v6.4: 위험 색상 애니메이션 (푸른색↔붉은색)
    void StartDangerColorAnim(float alpha)
    {
        StopDangerColorAnim();
        isDangerAnimActive = true;

        Color blueC = DANGER_BLUE; blueC.a = alpha;
        Color redC  = DANGER_RED;  redC.a  = alpha;

        vignetteImage.DOKill();
        vignetteImage.color = blueC;

        dangerColorAnim = DOTween.Sequence();
        dangerColorAnim.Append(vignetteImage.DOColor(redC, 1.2f).SetEase(Ease.InOutSine));
        dangerColorAnim.Append(vignetteImage.DOColor(blueC, 1.2f).SetEase(Ease.InOutSine));
        dangerColorAnim.SetLoops(-1, LoopType.Restart);
    }

    void StopDangerColorAnim()
    {
        if (dangerColorAnim != null) { dangerColorAnim.Kill(); dangerColorAnim = null; }
        isDangerAnimActive = false;
        if (vignetteImage != null)
        {
            vignetteImage.DOKill();
            Color c = vignetteColor; c.a = 0f;
            vignetteImage.color = c;
        }
    }

    // 레거시 호환용 (무해)
    public void IncreaseInfiniteBossBonus() { }

    public void ResetInfiniteBossBonus()
    {
        StopDangerColorAnim();
        enemyAtk = 28;
        if (vignetteImage != null)
        {
            vignetteImage.DOKill();
            Color c = vignetteColor; c.a = 0f;
            vignetteImage.color = c;
        }
    }

    public bool IsVignetteAtMax(int currentHeat) { return currentHeat <= enemyAtk; }
}
