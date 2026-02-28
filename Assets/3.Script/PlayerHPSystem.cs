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
    [SerializeField] private TextMeshProUGUI levelUpRouletteText;  // 룰렛 배열 표시용 추가 텍스트
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

    [Header("HP Bar Heal Flash")]
    [SerializeField] private Color healFlashColor = new Color(0.2f, 1f, 0.4f);

    [Header("체력 변화 텍스트 크기")]
    [SerializeField] private float heatChangeTextSize = 40f;

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

    // =====================================================
    // LevelUp 룰렛: 실제 테이블 후보값 나열 + 랜덤 하이라이트
    // 브론즈(bronzeMin~bronzeMax) / 실버(silverMin~silverMax) / 골드(goldValue)
    // =====================================================

    // 실제 테이블 기반 모든 후보 값 목록 빌드
    int[] GetAllLevelUpValues()
    {
        var list = new System.Collections.Generic.List<int>();
        // 브론즈 범위
        for (int v = bronzeMin; v <= bronzeMax; v++) list.Add(v);
        // 실버 범위
        for (int v = silverMin; v <= silverMax; v++) list.Add(v);
        // 골드
        list.Add(goldValue);
        return list.ToArray();
    }

    // 배열 전체를 "3 4 5 ... 40" 형태로 빌드 (2줄, 절반 지점에서 줄바꿈)
    // 한 자리 수는 앞에 0 붙여 두 자리로 표시 (03, 04 ... 07)
    string FormatRouletteValue(int v) => v < 10 ? $"0{v}" : v.ToString();

    string BuildRouletteString(int[] values, int highlightIndex, Color highlightColor)
    {
        var sb = new System.Text.StringBuilder();
        int half = values.Length / 2;
        for (int i = 0; i < values.Length; i++)
        {
            if (i == half) sb.Append("\n");
            else if (i > 0) sb.Append(" ");

            string label = FormatRouletteValue(values[i]);
            if (i == highlightIndex)
            {
                string hex = ColorUtility.ToHtmlStringRGB(highlightColor);
                sb.Append($"<color=#{hex}><b>{label}</b></color>");
            }
            else
            {
                sb.Append($"<color=#888888>{label}</color>");
            }
        }
        return sb.ToString();
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

        // levelUpText: "Level UP!" 고정 라벨
        if (levelUpText != null)
        {
            levelUpText.text = "Level UP!";
            levelUpText.color = Color.white;
        }

        // 실제 테이블 후보 배열 + finalIncrease 인덱스
        int[] allValues = GetAllLevelUpValues();
        int finalIndex = System.Array.IndexOf(allValues, finalIncrease);
        if (finalIndex < 0) finalIndex = allValues.Length - 1;

        if (levelUpRouletteText != null)
        {
            levelUpRouletteText.gameObject.SetActive(true);
            levelUpRouletteText.text = BuildRouletteString(allValues, Random.Range(0, allValues.Length), SHUFFLE_COLORS[0]);
        }

        // === Phase 1: 룰렛 (카드 셔플 픽 연출, 3.5초) ===
        // 초반: 배열 안에서 랜덤 점프 (빠름) → 후반: finalIndex로 점점 수렴 (느림)
        float totalScan = 2.5f;
        float elapsed = 0f;
        int currentIdx = Random.Range(0, allValues.Length);
        int colorIdx = 0;
        int n = allValues.Length;

        while (elapsed < totalScan)
        {
            float progress = elapsed / totalScan;  // 0→1
            // 이차 곡선으로 자연스럽게 감속
            float interval = Mathf.Lerp(0.05f, 0.28f, Mathf.Pow(progress, 2.2f));

            if (progress < 0.65f)
            {
                // 초반 65%: 완전 랜덤 점프 (배열 안에서만)
                // 다만 직전 인덱스로 돌아가지 않게 제약
                int next;
                do { next = Random.Range(0, n); } while (next == currentIdx);
                currentIdx = next;
            }
            else if (progress < 0.88f)
            {
                // 중반 23%: finalIndex 방향으로 1칸씩 + 가끔 랜덤 직프
                int dist = finalIndex - currentIdx;
                if (dist == 0)
                    // 지나쳐서 주변 움직임
                    currentIdx = Mathf.Clamp(currentIdx + (Random.value > 0.5f ? 2 : -2), 0, n - 1);
                else
                    currentIdx += (int)Mathf.Sign(dist);
                currentIdx = Mathf.Clamp(currentIdx, 0, n - 1);
            }
            else
            {
                // 후반 12%: finalIndex로 1칸씩 수렴 (이미 갔으면 유지)
                int dist = finalIndex - currentIdx;
                if (dist != 0) currentIdx += (int)Mathf.Sign(dist);
                currentIdx = Mathf.Clamp(currentIdx, 0, n - 1);
            }

            colorIdx = (colorIdx + 1) % SHUFFLE_COLORS.Length;
            Color hColor = SHUFFLE_COLORS[colorIdx];

            if (levelUpRouletteText != null)
                levelUpRouletteText.text = BuildRouletteString(allValues, currentIdx, hColor);

            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        // === Phase 2: finalIndex 정착 + 픽스 연출 ===
        Color finalColor = GetColorForValue(finalIncrease);
        if (levelUpRouletteText != null)
        {
            levelUpRouletteText.text = BuildRouletteString(allValues, finalIndex, finalColor);
            RectTransform roulRT = levelUpRouletteText.GetComponent<RectTransform>();
            roulRT.DOKill();
            roulRT.localScale = Vector3.one * 1.25f;
            roulRT.DOScale(1f, 0.35f).SetEase(Ease.OutBack);
        }

        // levelUpText: 최종 수치 + 등급 색상
        if (levelUpText != null)
        {
            levelUpText.text = GetLabelForValue(finalIncrease);
            levelUpText.color = finalColor;

            float popScale = finalIncrease >= goldValue ? 1.8f : finalIncrease >= silverMin ? 1.5f : 1.3f;
            float popDur   = finalIncrease >= goldValue ? 0.35f : finalIncrease >= silverMin ? 0.28f : 0.20f;
            RectTransform tr = levelUpText.GetComponent<RectTransform>();
            tr.DOKill();
            tr.localScale = Vector3.one * popScale;
            tr.DOScale(1f, popDur).SetEase(Ease.OutBack);
        }

        // === Phase 3: 실제 HP 증가 + 회복 UI ===
        maxHeat += finalIncrease;
        int oldHeat = currentHeat;
        currentHeat = maxHeat;
        UpdateHeatUI();

        int recovery = currentHeat - oldHeat;
        if (recovery > 0) ShowHeatChangeText(recovery);

        Debug.Log($"[LevelUP] Max HP +{finalIncrease}: {maxHeat}");

        yield return new WaitForSeconds(0.7f);

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
        if (levelUpRouletteText != null)
        {
            RectTransform rrt = levelUpRouletteText.GetComponent<RectTransform>();
            if (rrt != null) rrt.DOKill();
            levelUpRouletteText.DOKill();
        }

        if (panelCG != null)
        {
            panelCG.DOKill();
            panelCG.DOFade(0f, 0.5f).SetEase(Ease.InQuad).OnComplete(() => {
                if (levelUpPanel != null) levelUpPanel.SetActive(false);
                if (levelUpRouletteText != null) levelUpRouletteText.gameObject.SetActive(false);
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

        // 1대 맞으면 죽을 피 + gun 있으면 느린 색상 루프
        if (gunSystem != null && lowHealthVignette != null)
        {
            bool oneHitDeath = lowHealthVignette.IsVignetteAtMax(currentHeat) && currentHeat > 0;
            bool hasGun = gunSystem.HasBullet || (gunSystem.IsFeverMode && !gunSystem.FeverBulletUsed);
            if (oneHitDeath && hasGun)
                gunSystem.StartSlowGunButtonLoop();
            else
                gunSystem.StopSlowGunButtonLoop();
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

            heatChangeText.fontSize = heatChangeTextSize;

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

    // _16: HP 회복 시 HP bar 깠박임 (턴당 1회, 0.15초, 색상 SerializeField)
    public void FlashHealGreen()
    {
        if (heatBarImage == null) return;
        heatBarImage.DOKill();
        heatBarImage.color = healFlashColor;
        heatBarImage.DOColor(GetCurrentBarColor(), 0.4f).SetEase(Ease.OutQuad);
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
