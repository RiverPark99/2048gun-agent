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
    [SerializeField, Range(0f, 1f)] private float berryHealPercent = 0.12f;
    [SerializeField, Range(0f, 1f)] private float mixHealPercent = 0.06f;

    [Header("Level Up UI")]
    [SerializeField] private GameObject levelUpPanel;
    [SerializeField] private TextMeshProUGUI levelUpText;
    [SerializeField] private GameObject levelUpTapHintObj;

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

    // ⭐ v6.4: Level UP 랜덤 성장 테이블
    private struct LevelUpEntry
    {
        public int value;
        public Color color;
        public string label;
        public LevelUpEntry(int v, Color c, string l) { value = v; color = c; label = l; }
    }

    private static readonly LevelUpEntry[] allLevelUpEntries = {
        // 60% 테이블 (2~5)
        new LevelUpEntry(2,  new Color(0.4f, 0.8f, 1f),   "Level UP! MaxHP +2"),
        new LevelUpEntry(3,  new Color(0.4f, 1f, 0.6f),   "Level UP! MaxHP +3"),
        new LevelUpEntry(4,  new Color(1f, 0.85f, 0.3f),  "Level UP! MaxHP +4"),
        new LevelUpEntry(5,  new Color(1f, 0.6f, 0.3f),   "Level UP! MaxHP +5"),
        // 39% 테이블 (10~15)
        new LevelUpEntry(10, new Color(0.3f, 1f, 0.3f),   "Level UP! MaxHP +10"),
        new LevelUpEntry(11, new Color(0.5f, 1f, 0.4f),   "Level UP! MaxHP +11"),
        new LevelUpEntry(12, new Color(0.6f, 1f, 0.5f),   "Level UP! MaxHP +12"),
        new LevelUpEntry(13, new Color(0.7f, 1f, 0.3f),   "Level UP! MaxHP +13"),
        new LevelUpEntry(14, new Color(0.85f, 1f, 0.2f),  "Level UP! MaxHP +14"),
        new LevelUpEntry(15, new Color(1f, 1f, 0.2f),     "Level UP! MaxHP +15"),
        // 1% (40)
        new LevelUpEntry(40, new Color(1f, 0.35f, 0.55f), "Level UP! MaxHP +40"),
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

        if (levelUpPanel != null) levelUpPanel.SetActive(false);
        if (levelUpTapHintObj != null) levelUpTapHintObj.SetActive(false);
    }

    public void ResetState()
    {
        maxHeat = 100;
        currentHeat = maxHeat;

        if (lowHealthVignette != null)
            lowHealthVignette.ResetInfiniteBossBonus();

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
        // ⭐ v6.4: Guard(40)부터는 성장 안함, Clear 모드도 안함
        if (isClearMode || bossLevel >= 40) return;

        // 확률 테이블: 60% → 2~5, 39% → 10~15, 1% → 40
        float roll = Random.value;
        int heatIncrease;
        if (roll < 0.60f)
            heatIncrease = Random.Range(2, 6);   // 2,3,4,5
        else if (roll < 0.99f)
            heatIncrease = Random.Range(10, 16);  // 10~15
        else
            heatIncrease = 40;

        Debug.Log($"보스 처치! Max HP +{heatIncrease} 예정 (roll={roll:F3})");

        if (levelUpCoroutine != null) StopCoroutine(levelUpCoroutine);
        levelUpCoroutine = StartCoroutine(LevelUpRouletteCoroutine(heatIncrease));
    }

    private Coroutine levelUpCoroutine;

    // ⭐ v6.4: 확률 테이블에서 해당 value의 엔트리 찾기
    LevelUpEntry GetEntryForValue(int value)
    {
        foreach (var e in allLevelUpEntries)
            if (e.value == value) return e;
        return allLevelUpEntries[0];
    }

    IEnumerator LevelUpRouletteCoroutine(int finalIncrease)
    {
        isLevelUpAnimating = true;

        // ⭐ v6.4: 하이어라키 패널 활성화
        CanvasGroup panelCG = null;
        if (levelUpPanel != null)
        {
            levelUpPanel.SetActive(true);
            panelCG = levelUpPanel.GetComponent<CanvasGroup>();
            if (panelCG == null) panelCG = levelUpPanel.AddComponent<CanvasGroup>();
            panelCG.alpha = 1f;
        }

        bool hasText = (levelUpText != null);

        // === Phase 1: 랜덤 텍스트 섮임 (3.5초) ===
        float shuffleTime = 3.5f;
        float elapsed = 0f;
        while (elapsed < shuffleTime)
        {
            if (hasText)
            {
                LevelUpEntry randEntry = allLevelUpEntries[Random.Range(0, allLevelUpEntries.Length)];
                levelUpText.text = randEntry.label;
                levelUpText.color = randEntry.color;
            }
            // 점점 느려지는 간격 (0.06~0.18초)
            float interval = Mathf.Lerp(0.06f, 0.18f, elapsed / shuffleTime);
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        // === Phase 2: 최종 숫자 픽스 (1초 표시) ===
        if (hasText)
        {
            LevelUpEntry finalEntry = GetEntryForValue(finalIncrease);
            levelUpText.text = finalEntry.label;
            levelUpText.color = finalEntry.color;

            // 픽스 시 텍스트 스케일 펄스
            RectTransform tr = levelUpText.GetComponent<RectTransform>();
            tr.DOKill();
            tr.localScale = Vector3.one * 1.3f;
            tr.DOScale(1f, 0.2f).SetEase(Ease.OutBack);
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

        yield return new WaitForSeconds(1.0f);

        // === Phase 4: TimeScale 정지 → 터치 대기 ===
        Time.timeScale = 0f;

        // 탭 힌트 표시 + 투명도 깜빡임
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

        // 터치/클릭 대기
        while (!Input.GetMouseButtonDown(0) && !Input.anyKeyDown)
            yield return null;

        // 탭 힌트 숨기기
        if (tapHintTween != null) tapHintTween.Kill();
        if (levelUpTapHintObj != null) levelUpTapHintObj.SetActive(false);

        Time.timeScale = 1f;

        // 보스 스폰 허용
        isLevelUpAnimating = false;

        // === Phase 5: 1초 페이드아웃 ===
        if (panelCG != null)
        {
            panelCG.DOKill();
            panelCG.DOFade(0f, 1.0f).SetEase(Ease.InQuad).OnComplete(() => {
                if (levelUpPanel != null) levelUpPanel.SetActive(false);
            });
        }

        levelUpCoroutine = null;
    }

    // ⭐ v6.4: Berry+Berry 회복량: 최대HP의 12%
    public int GetBerryHealAmount()
    {
        return Mathf.FloorToInt(maxHeat * berryHealPercent);
    }

    // ⭐ v6.4: Mix(Berry+Choco) 회복량: 최대HP의 6%
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

            // ⭐ v6.4: HP bar 색상 % 기반 (핑크→연핑크)
            float hp = (float)currentHeat / maxHeat;
            Color hc;
            if (hp >= 0.8f)
                hc = new Color(1f, 0.3f, 0.55f);        // 100~80%: 핑크
            else if (hp >= 0.4f)
            {
                float t = (hp - 0.4f) / 0.4f;
                hc = Color.Lerp(
                    new Color(1f, 0.6f, 0.75f),          // 40%: 연핑크
                    new Color(1f, 0.3f, 0.55f),          // 80%: 핑크
                    t);
            }
            else
            {
                float t = hp / 0.4f;
                hc = Color.Lerp(
                    new Color(1f, 0.75f, 0.85f),          // 0%: 가장 연한 핑크
                    new Color(1f, 0.6f, 0.75f),           // 40%: 연핑크
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
