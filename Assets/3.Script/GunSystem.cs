// =====================================================
// GunSystem.cs - v7.0
// v7.0: 파티클 Screen.width 보정, BulletCount DOTween,
//       Ready 명칭, 게이지 변화 항상 표시, 치트 무한컨티뉴,
//       Damage Record, Guard ATK slider 재설계
// =====================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using DG.Tweening;

public class GunSystem : MonoBehaviour
{
    [Header("Gun UI")]
    [SerializeField] private Button gunButton;
    [SerializeField] private TextMeshProUGUI bulletCountText;
    [SerializeField] private TextMeshProUGUI turnsUntilBulletText;
    [SerializeField] private TextMeshProUGUI attackPowerText;
    [SerializeField] private TextMeshProUGUI gunModeGuideText;
    [SerializeField] private Image gunButtonImage;
    [SerializeField] private RectTransform progressBarFill;

    [Header("Gauge Change Text")]
    [SerializeField] private GameObject damageTextPrefab;
    [SerializeField] private Transform damageTextParent;

    [Header("Freeze Effects")]
    [SerializeField] private Transform feverParticleSpawnPoint;
    [SerializeField] private Image feverBackgroundImage;
    [SerializeField] private Image freezeImage1;

    [Header("Gun Mode Visual")]
    [SerializeField] private Image gunModeOverlayImage;
    [SerializeField] private Image hpBarBackgroundImage;
    [SerializeField] private Image progressBarGlowOverlay;

    [Header("Freeze UI")]
    [SerializeField] private TextMeshProUGUI freezeTurnText;
    [SerializeField] private TextMeshProUGUI freezeTotalDamageText;

    [Header("아이콘 이미지 (텍스트 색상/alpha 동기화)")]
    [SerializeField] private Image atkIconImage;       // 공격력 아이콘 (텍스트 옆)
    [SerializeField] private Image boostIconImage;      // Boost 아이콘 (freezeTurnText 옆)

    [Header("회복력 UI (Berry 1개 기준, challenge 3~ 표시)")]
    [SerializeField] private TextMeshProUGUI healPowerText;  // "3 ♥" 형태

    [Header("Continue")]
    [SerializeField] private TextMeshProUGUI continueGuideText;

    [Header("Damage Record (Score/Best 대체)")]
    [SerializeField] private TextMeshProUGUI currentRecordText;  // 현재 판 최고 데미지
    [SerializeField] private TextMeshProUGUI bestRecordText;     // 전체 최고 데미지 (PlayerPrefs 저장)

    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private PlayerHPSystem playerHP;
    [SerializeField] private BossBattleSystem bossBattle;
    [SerializeField] private BossManager bossManager;
    [SerializeField] private UnlockManager unlockManager;

    [Header("Developer Cheat")]
    [SerializeField] private bool cheatMode = false;
    [SerializeField] private bool cheatInfiniteContinue = false;

    // 상수
    private const int GAUGE_MAX = 40;
    private const int GAUGE_FOR_BULLET = 20;
    private const int FREEZE_MOVE_COST = 2;
    private const int FREEZE_COMBO_BONUS = 2;
    private const int GUN_SHOT_COST = 20;
    [Header("Balance")]
    [SerializeField] private float freezeTurnMultiplier = 1.06f;

    [Header("Freeze Tile Bonus Multiplier (최대 타일 값 기준)")]
    [SerializeField] private float[] freezeTileBonusMultipliers = new float[]
    {
        1.0f,  // 128
        1.05f, // 256
        1.1f,  // 512
        1.15f, // 1024
        1.2f,  // 2048
        1.3f,  // 4096
        1.4f,  // 8192
        1.5f,  // 16384
        1.6f,  // 32768
        1.8f,  // 65536
        2.0f,  // 131072
    };
    // 인덱스: 0=128, 1=256, 2=512 ... 10=131072
    private const int MAX_CONTINUES = 2;

    private static readonly Color GUN_READY_MINT = new Color(0.6f, 0.95f, 0.85f);
    private static readonly Color FREEZE_ORANGE = new Color(1f, 0.6f, 0.1f, 1f);
    private static readonly Color FREEZE_BLACK  = new Color(0f, 0f, 0f, 1f);

    // Gauge & Fever 상태
    private int mergeGauge = 0;
    private bool hasBullet = false;
    private bool isFeverMode = false;
    private bool feverBulletUsed = false;

    // Freeze 턴 배율
    private int freezeTurnCount = 0;
    private long freezeTotalDamage = 0;

    // ATK 보너스
    private long feverMergeIncreaseAtk = 1;
    private long permanentAttackPower = 0;

    // Gun 모드
    private bool isGunMode = false;
    private Sequence hpBarGunModeAnim;
    private Color hpBarOriginalBgColor;
    private bool hpBarBgColorSaved = false;

    // Progress bar glow
    private Sequence progressBarGlowAnim;

    // UI 상태
    private Tweener gunButtonHeartbeat;
    private bool lastGunButtonAnimationState = false;
    private float turnsTextOriginalY = 0f;
    private bool turnsTextInitialized = false;
    private float attackTextOriginalY = 0f;
    private bool attackTextInitialized = false;
    private long lastPermanentAttackPower = 0;
    private int lastMergeGauge = -1;
    private string lastBulletCountState = "";

    // Progress bar
    private Color progressBarOriginalColor;
    private bool progressBarColorSaved = false;

    // 파티클
    private GameObject activeFeverParticle;

    // 긴급 깜빡임
    private Sequence emergencyGunFlash;
    private bool isEmergencyFlashing = false;

    // ATK 색상
    private Color atkOriginalColor = Color.black;
    private bool atkColorSaved = false;
    private Sequence atkFreezeColorAnim;
    private Sequence freezeTurnColorAnim;
    private Sequence freezeTotalDmgColorAnim;

    // 아이콘 색상 동기화
    private Sequence atkIconFreezeAnim;
    private Sequence boostIconFreezeAnim;
    private Color atkIconOriginalColor;
    private bool atkIconColorSaved = false;
    private Color boostIconOriginalColor;
    private bool boostIconColorSaved = false;

    // Freeze UI 원래 위치 저장
    private Vector2 freezeTurnOriginalPos;
    private bool freezeTurnPosSaved = false;
    private Vector2 freezeTotalDmgOriginalPos;
    private bool freezeTotalDmgPosSaved = false;
    private Color freezeTotalDmgOriginalColor = Color.white;
    private bool freezeTotalDmgColorSaved = false;
    private float freezeTotalDmgOriginalFontSize = 0f;
    private bool freezeTotalDmgFontSizeSaved = false;

    [Header("Freeze Total Damage 글자 크기 (자릿수별: 8,9,10,11,12자리)")]
    [SerializeField] private float[] freezeDmgFontSizes = new float[] { 34f, 30f, 26f, 23f, 20f };

    // Continue 횟수
    private static int continueCount = 0;

    // Damage Record
    private long currentSessionBestDamage = 0;  // 현재 판 최고
    private long allTimeBestDamage = 0;          // 전체 최고 (PlayerPrefs)

    // === 프로퍼티 ===
    public bool IsFeverMode => isFeverMode;
    public bool IsGunMode => isGunMode;
    public bool HasBullet => hasBullet;
    public bool FeverBulletUsed => feverBulletUsed;
    public int MergeGauge => mergeGauge;
    public long PermanentAttackPower => permanentAttackPower;
    public long FeverMergeIncreaseAtk => feverMergeIncreaseAtk;
    public int ContinuesRemaining => MAX_CONTINUES - continueCount;
    public float GetFreezeDamageMultiplier()
    {
        float turnMult = Mathf.Pow(freezeTurnMultiplier, freezeTurnCount);
        float tileMult = GetFreezeTileBonusMultiplier();
        return turnMult * tileMult;
    }

    float GetFreezeTileBonusMultiplier()
    {
        if (gridManager == null) return 1f;
        int maxTileValue = 0;
        foreach (var tile in gridManager.ActiveTiles)
        {
            if (tile != null && tile.value > maxTileValue)
                maxTileValue = tile.value;
        }
        // 128 = 2^7 → index 0, 256 = 2^8 → index 1 ...
        if (maxTileValue < 128) return 1f;
        int power = Mathf.RoundToInt(Mathf.Log(maxTileValue, 2)); // 128→7, 256→8
        int index = power - 7; // 128→0, 256→1 ...
        if (index < 0) return 1f;
        if (index >= freezeTileBonusMultipliers.Length)
            return freezeTileBonusMultipliers[freezeTileBonusMultipliers.Length - 1];
        return freezeTileBonusMultipliers[index];
    }

    public void AddFreezeTotalDamage(long dmg)
    {
        freezeTotalDamage += dmg;
        // 실시간 레코드 갱신
        CheckAndUpdateDamageRecord();
        if (freezeTotalDamageText != null && freezeTotalDamageText.gameObject.activeSelf)
        {
            freezeTotalDamageText.text = $"{freezeTotalDamage:N0}";
            // 자릿수별 폰트 크기 조절 (8자리 이상)
            ApplyFreezeDmgFontSize();
            RectTransform rt = freezeTotalDamageText.GetComponent<RectTransform>();
            rt.DOKill();
            rt.localScale = Vector3.one;
            if (freezeTotalDmgPosSaved)
            {
                rt.anchoredPosition = freezeTotalDmgOriginalPos;
                rt.DOAnchorPosY(freezeTotalDmgOriginalPos.y + 4f, 0.08f).SetEase(Ease.OutQuad)
                    .OnComplete(() => { if (rt != null) rt.DOAnchorPosY(freezeTotalDmgOriginalPos.y, 0.1f).SetEase(Ease.InQuad); });
            }
        }
    }

    void ApplyFreezeDmgFontSize()
    {
        if (freezeTotalDamageText == null || !freezeTotalDmgFontSizeSaved) return;
        // N0 포맷의 실제 글자수 (콤마 포함)
        int digitCount = freezeTotalDamageText.text.Length;
        if (digitCount < 8)
        {
            freezeTotalDamageText.fontSize = freezeTotalDmgOriginalFontSize;
        }
        else
        {
            // 8자리=index0, 9=1, 10=2, 11=3, 12+=4
            int idx = Mathf.Clamp(digitCount - 8, 0, freezeDmgFontSizes.Length - 1);
            freezeTotalDamageText.fontSize = freezeDmgFontSizes[idx];
        }
    }

    public void Initialize()
    {
        if (freezeImage1 == null)
        {
            GameObject obj = GameObject.Find("infoFreeze");
            if (obj != null) freezeImage1 = obj.GetComponent<Image>();
        }
        if (freezeImage1 != null) { freezeImage1.color = new Color(1f, 1f, 1f, 70f / 255f); freezeImage1.gameObject.SetActive(false); }

        if (progressBarFill != null && !progressBarColorSaved)
        {
            Image fillImg = progressBarFill.GetComponent<Image>();
            if (fillImg != null) { progressBarOriginalColor = fillImg.color; progressBarColorSaved = true; }
        }

        if (gunButton != null) gunButton.onClick.AddListener(ToggleGunMode);
        if (gunModeOverlayImage != null) gunModeOverlayImage.gameObject.SetActive(false);

        if (hpBarBackgroundImage != null && !hpBarBgColorSaved)
        {
            hpBarOriginalBgColor = hpBarBackgroundImage.color;
            hpBarBgColorSaved = true;
        }

        if (progressBarGlowOverlay != null)
        {
            Color c = progressBarGlowOverlay.color; c.a = 0f; progressBarGlowOverlay.color = c;
            progressBarGlowOverlay.gameObject.SetActive(false);
        }

        if (freezeTurnText != null)
        {
            if (!freezeTurnPosSaved) { freezeTurnOriginalPos = freezeTurnText.GetComponent<RectTransform>().anchoredPosition; freezeTurnPosSaved = true; }
            freezeTurnText.gameObject.SetActive(false);
        }
        if (freezeTotalDamageText != null)
        {
            if (!freezeTotalDmgPosSaved) { freezeTotalDmgOriginalPos = freezeTotalDamageText.GetComponent<RectTransform>().anchoredPosition; freezeTotalDmgPosSaved = true; }
            if (!freezeTotalDmgColorSaved) { freezeTotalDmgOriginalColor = freezeTotalDamageText.color; freezeTotalDmgColorSaved = true; }
            if (!freezeTotalDmgFontSizeSaved) { freezeTotalDmgOriginalFontSize = freezeTotalDamageText.fontSize; freezeTotalDmgFontSizeSaved = true; }
            freezeTotalDamageText.gameObject.SetActive(false);
        }

        if (attackPowerText != null && !atkColorSaved)
        {
            atkOriginalColor = attackPowerText.color;
            atkColorSaved = true;
        }
        if (atkIconImage != null && !atkIconColorSaved)
        {
            atkIconOriginalColor = atkIconImage.color;
            atkIconColorSaved = true;
        }
        if (boostIconImage != null && !boostIconColorSaved)
        {
            boostIconOriginalColor = boostIconImage.color;
            boostIconColorSaved = true;
        }
        if (healPowerText != null) healPowerText.gameObject.SetActive(false);

        if (cheatMode)
        {
            permanentAttackPower += 200000000;
            Debug.Log($"🔧 CHEAT MODE: ATK +200,000,000");
        }

        continueCount = 0;
        currentSessionBestDamage = 0;
        // allTimeBest: PlayerPrefs에서 로드
        string savedBest = PlayerPrefs.GetString("BestFreezeDamage", "0");
        if (long.TryParse(savedBest, out long parsed)) allTimeBestDamage = parsed;
        else allTimeBestDamage = 0;
        UpdateDamageRecordUI();
        UpdateContinueGuideText();
        UpdateGunUI();
    }

    public void ResetState()
    {
        mergeGauge = 0; hasBullet = false; isFeverMode = false;
        feverMergeIncreaseAtk = 1; permanentAttackPower = 0;
        feverBulletUsed = false; isGunMode = false;
        freezeTurnCount = 0; freezeTotalDamage = 0;
        lastPermanentAttackPower = 0; lastMergeGauge = -1;
        lastBulletCountState = "";

        if (gunButtonHeartbeat != null) { gunButtonHeartbeat.Kill(); gunButtonHeartbeat = null; }
        StopFreezeColorLoops();
        if (gunModeGuideText != null) gunModeGuideText.gameObject.SetActive(false);
        if (freezeImage1 != null) freezeImage1.gameObject.SetActive(false);
        if (activeFeverParticle != null) { Destroy(activeFeverParticle); activeFeverParticle = null; }
        if (feverBackgroundImage != null) { feverBackgroundImage.DOKill(); feverBackgroundImage.gameObject.SetActive(false); }
        if (gunModeOverlayImage != null) gunModeOverlayImage.gameObject.SetActive(false);
        ForceResetFreezeUITransforms();
        if (freezeTurnText != null) freezeTurnText.gameObject.SetActive(false);
        if (freezeTotalDamageText != null) freezeTotalDamageText.gameObject.SetActive(false);

        if (attackPowerText != null && atkColorSaved)
        {
            attackPowerText.DOKill();
            attackPowerText.color = atkOriginalColor;
        }
        if (atkIconImage != null && atkIconColorSaved)
        {
            atkIconImage.DOKill();
            atkIconImage.color = atkIconOriginalColor;
        }
        if (boostIconImage != null && boostIconColorSaved)
        {
            boostIconImage.DOKill();
            boostIconImage.color = boostIconOriginalColor;
        }
        if (healPowerText != null) healPowerText.gameObject.SetActive(false);

        if (cheatMode)
        {
            permanentAttackPower += 200000000;
            Debug.Log($"🔧 CHEAT MODE: ATK +200,000,000");
        }

        currentSessionBestDamage = 0;
        // allTimeBest 유지 (PlayerPrefs에서 이미 로드됨)
        UpdateDamageRecordUI();
        StopProgressBarGlow();
        RestoreProgressBarColor();
        StopHPBarGunModeAnim();
        StopEmergencyFlash();
        UpdateGunUI();
    }

    // === Damage Record (Score/Best 대체) ===
    void UpdateDamageRecordUI()
    {
        if (currentRecordText != null)
            currentRecordText.text = $"{currentSessionBestDamage:N0}";
        if (bestRecordText != null)
            bestRecordText.text = $"{allTimeBestDamage:N0}";
    }

    void CheckAndUpdateDamageRecord()
    {
        bool updated = false;
        if (freezeTotalDamage > currentSessionBestDamage)
        {
            currentSessionBestDamage = freezeTotalDamage;
            updated = true;
        }
        if (freezeTotalDamage > allTimeBestDamage)
        {
            allTimeBestDamage = freezeTotalDamage;
            PlayerPrefs.SetString("BestFreezeDamage", allTimeBestDamage.ToString());
            PlayerPrefs.Save();
            updated = true;
        }
        if (updated) UpdateDamageRecordUI();
    }

    // === 게이지 ===
    // UnlockManager에서 해금 직후 0/20 표시 보장용
    public void ForceGaugeDisplayCap(int cap)
    {
        if (turnsUntilBulletText != null)
            turnsUntilBulletText.text = $"{mergeGauge}/{cap}";
    }

    public void AddMergeGauge(int amount)
    {
        int cap = (unlockManager != null) ? unlockManager.GetGaugeCap() : GAUGE_MAX;
        if (cap <= 0) return; // Gun 미해금: 게이지 증가 안함
        mergeGauge += amount;
        if (mergeGauge > cap) mergeGauge = cap;
    }

    public void UpdateGaugeUIOnly() { UpdateGunUI(); }
    public void AddFeverMergeATK() { permanentAttackPower += feverMergeIncreaseAtk; }
    public void ClearFeverPaybackIfNeeded() { }

    // === 게이지 변화 표시 (Freeze 외에서도 사용) ===
    public void ShowMergeGaugeChange(int change, bool isCombo)
    {
        if (!isFeverMode)
        {
            // 게이지 측 도달 시 텍스트 안 보임
            int cap = (unlockManager != null) ? unlockManager.GetGaugeCap() : GAUGE_MAX;
            if (cap > 0 && mergeGauge >= cap) return;
            ShowGaugeChangeText(change, isCombo);
        }
    }

    // === Freeze 턴 처리 ===
    public void ProcessFreezeAfterMove(int comboCount)
    {
        if (!isFeverMode) return;

        freezeTurnCount++;
        int gaugeBeforeAll = mergeGauge;

        if (comboCount >= 2)
        {
            int bonus = FREEZE_COMBO_BONUS * comboCount;
            mergeGauge += bonus;
            if (mergeGauge > GAUGE_MAX) mergeGauge = GAUGE_MAX;
        }

        mergeGauge -= FREEZE_MOVE_COST;

        int netChange = mergeGauge - gaugeBeforeAll;
        bool isCombo = (comboCount >= 2);
        if (netChange != 0)
            ShowGaugeChangeText(netChange, isCombo);

        UpdateFreezeTurnUI();

        if (mergeGauge <= GAUGE_FOR_BULLET) EndFever();

        UpdateGunUI();
    }

    // === Gauge & Fever 체크 ===
    public void CheckGaugeAndFever()
    {
        if (isFeverMode) return;

        bool canFreeze = (unlockManager == null || unlockManager.CanFreeze());

        if (canFreeze && mergeGauge >= GAUGE_MAX)
            StartFever();
        else if (mergeGauge >= GAUGE_FOR_BULLET && !hasBullet)
        {
            hasBullet = true;
            UpdateGunButtonAnimation();
        }

        UpdateGunUI();
    }

    public IEnumerator DelayedFreezeCheck()
    {
        if (gunButtonImage != null)
        {
            while (gunButtonImage.color.a < 0.99f)
                yield return null;
        }
        yield return null;
        CheckGaugeAndFever();
    }

    void StartFever()
    {
        SpawnFeverParticle();

        if (feverBackgroundImage != null)
        {
            feverBackgroundImage.gameObject.SetActive(true);
            Color c = feverBackgroundImage.color; c.a = 1.0f; feverBackgroundImage.color = c;
            feverBackgroundImage.DOKill();
            feverBackgroundImage.DOFade(0.7f, 0.5f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
        }

        if (freezeImage1 != null) freezeImage1.gameObject.SetActive(true);
        if (bossManager != null) bossManager.SetFrozen(true);
        FireFeverFreezeLaser();

        isFeverMode = true;
        feverBulletUsed = false;
        mergeGauge = GAUGE_MAX;
        hasBullet = false;
        freezeTurnCount = 0;
        freezeTotalDamage = 0;

        UpdateGunButtonAnimation();
        SetProgressBarFreezeColor();
        StartFreezeColorLoops();

        if (freezeTurnText != null) { freezeTurnText.gameObject.SetActive(true); freezeTurnText.text = "0 (x1.00)"; }
        if (freezeTotalDamageText != null) { freezeTotalDamageText.gameObject.SetActive(true); freezeTotalDamageText.text = "0"; }

        if (!bossManager.IsClearMode()) feverMergeIncreaseAtk++;
    }

    void EndFever()
    {
        // Damage record 갱신
        CheckAndUpdateDamageRecord();

        if (activeFeverParticle != null) { Destroy(activeFeverParticle); activeFeverParticle = null; }
        if (feverBackgroundImage != null) { feverBackgroundImage.DOKill(); feverBackgroundImage.gameObject.SetActive(false); }
        if (freezeImage1 != null) freezeImage1.gameObject.SetActive(false);
        if (bossManager != null) bossManager.SetFrozen(false);

        isFeverMode = false;
        feverBulletUsed = false;
        RestoreProgressBarColor();
        StopFreezeColorLoops();

        ForceResetFreezeUITransforms();
        AnimateAndHideFreezeUI();

        hasBullet = (mergeGauge >= GAUGE_FOR_BULLET);
        freezeTurnCount = 0;

        UpdateGunUI();
    }

    void ForceResetFreezeUITransforms()
    {
        if (freezeTurnText != null)
        {
            freezeTurnText.DOKill();
            RectTransform rt = freezeTurnText.GetComponent<RectTransform>();
            rt.DOKill(); rt.localScale = Vector3.one;
            if (freezeTurnPosSaved) rt.anchoredPosition = freezeTurnOriginalPos;
            CanvasGroup cg = freezeTurnText.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;
        }
        if (freezeTotalDamageText != null)
        {
            freezeTotalDamageText.DOKill();
            RectTransform rt = freezeTotalDamageText.GetComponent<RectTransform>();
            rt.DOKill(); rt.localScale = Vector3.one;
            if (freezeTotalDmgPosSaved) rt.anchoredPosition = freezeTotalDmgOriginalPos;
            if (freezeTotalDmgColorSaved) freezeTotalDamageText.color = freezeTotalDmgOriginalColor;
            if (freezeTotalDmgFontSizeSaved) freezeTotalDamageText.fontSize = freezeTotalDmgOriginalFontSize;
            CanvasGroup cg = freezeTotalDamageText.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;
        }
        if (attackPowerText != null)
        {
            attackPowerText.DOKill();
            if (atkColorSaved) { Color c = atkOriginalColor; c.a = 0.35f; attackPowerText.color = c; }
        }
        if (atkIconImage != null)
        {
            atkIconImage.DOKill();
            if (atkIconColorSaved) { Color c = atkIconOriginalColor; c.a = 0.35f; atkIconImage.color = c; }
        }
        if (boostIconImage != null)
        {
            boostIconImage.DOKill();
            if (boostIconColorSaved) boostIconImage.color = boostIconOriginalColor;
        }
    }

    void AnimateAndHideFreezeUI()
    {
        float stayDuration = 2.5f;   // 잔류 시간
        float fadeDuration = 0.8f;   // 사라지는 시간

        if (freezeTurnText != null && freezeTurnText.gameObject.activeSelf)
        {
            RectTransform rt = freezeTurnText.GetComponent<RectTransform>();
            CanvasGroup cg = freezeTurnText.GetComponent<CanvasGroup>();
            if (cg == null) cg = freezeTurnText.gameObject.AddComponent<CanvasGroup>();
            rt.DOKill(); cg.DOKill(); freezeTurnText.DOKill();
            cg.alpha = 1f; rt.localScale = Vector3.one;
            freezeTurnText.color = FREEZE_BLACK;

            DOTween.Sequence()
                .AppendInterval(stayDuration)
                .Append(cg.DOFade(0f, fadeDuration).SetEase(Ease.InQuad))
                .OnComplete(() => {
                    if (freezeTurnText == null) return;
                    freezeTurnText.gameObject.SetActive(false);
                    cg.alpha = 1f; rt.localScale = Vector3.one;
                    if (freezeTurnPosSaved) rt.anchoredPosition = freezeTurnOriginalPos;
                });
        }

        if (freezeTotalDamageText != null && freezeTotalDamageText.gameObject.activeSelf)
        {
            freezeTotalDamageText.text = $"{freezeTotalDamage:N0}";
            RectTransform rt = freezeTotalDamageText.GetComponent<RectTransform>();
            CanvasGroup cg = freezeTotalDamageText.GetComponent<CanvasGroup>();
            if (cg == null) cg = freezeTotalDamageText.gameObject.AddComponent<CanvasGroup>();
            rt.DOKill(); cg.DOKill(); freezeTotalDamageText.DOKill();
            cg.alpha = 1f; rt.localScale = Vector3.one;
            if (freezeTotalDmgPosSaved) rt.anchoredPosition = freezeTotalDmgOriginalPos;

            // 반짝반짝 효과 (주황↔흰색 3회 반복) → 픽스 (FREEZE_BLACK) → 잔류 → 페이드아웃
            Color flashWhite = new Color(1f, 0.95f, 0.8f);
            Color flashOrange = FREEZE_ORANGE;

            // 픽스 시 확대 후 복귀 (눈에 띄게)
            rt.localScale = Vector3.one * 1.6f;

            Sequence seq = DOTween.Sequence();
            // 반짝반짝 3회 (0.8초)
            seq.Append(freezeTotalDamageText.DOColor(flashWhite, 0.12f).SetEase(Ease.InOutSine));
            seq.Append(freezeTotalDamageText.DOColor(flashOrange, 0.12f).SetEase(Ease.InOutSine));
            seq.Append(freezeTotalDamageText.DOColor(flashWhite, 0.12f).SetEase(Ease.InOutSine));
            seq.Append(freezeTotalDamageText.DOColor(flashOrange, 0.12f).SetEase(Ease.InOutSine));
            seq.Append(freezeTotalDamageText.DOColor(flashWhite, 0.10f).SetEase(Ease.InOutSine));
            seq.Append(freezeTotalDamageText.DOColor(flashOrange, 0.10f).SetEase(Ease.InOutSine));
            // 픽스: 검정으로 + 스케일 복귀
            seq.Append(freezeTotalDamageText.DOColor(FREEZE_BLACK, 0.15f).SetEase(Ease.OutQuad));
            seq.Join(rt.DOScale(1f, 0.2f).SetEase(Ease.OutBack));
            // 잔류
            seq.AppendInterval(stayDuration);
            // 페이드아웃
            seq.Append(cg.DOFade(0f, fadeDuration).SetEase(Ease.InQuad));
            seq.OnComplete(() => {
                if (freezeTotalDamageText == null) return;
                freezeTotalDamageText.gameObject.SetActive(false);
                cg.alpha = 1f; rt.localScale = Vector3.one;
                if (freezeTotalDmgPosSaved) rt.anchoredPosition = freezeTotalDmgOriginalPos;
                if (freezeTotalDmgColorSaved) freezeTotalDamageText.color = freezeTotalDmgOriginalColor;
                if (freezeTotalDmgFontSizeSaved) freezeTotalDamageText.fontSize = freezeTotalDmgOriginalFontSize;
            });
        }
    }

    void UpdateFreezeTurnUI()
    {
        if (freezeTurnText != null && isFeverMode)
        {
            float mult = GetFreezeDamageMultiplier();
            freezeTurnText.text = $"{freezeTurnCount} (x{mult:F2})";
            RectTransform rt = freezeTurnText.GetComponent<RectTransform>();
            rt.DOKill();
            rt.localScale = Vector3.one;
            rt.DOScale(1.03f, 0.06f).SetEase(Ease.OutQuad)
                .OnComplete(() => { if (rt != null) rt.DOScale(1f, 0.08f).SetEase(Ease.InQuad); });
        }
    }

    // === 주황↔검정 색상 루프 ===
    void StartFreezeColorLoops()
    {
        StopFreezeColorLoops();

        if (attackPowerText != null)
        {
            attackPowerText.DOKill();
            attackPowerText.color = FREEZE_ORANGE;
            atkFreezeColorAnim = DOTween.Sequence();
            atkFreezeColorAnim.Append(attackPowerText.DOColor(FREEZE_BLACK, 1.2f).SetEase(Ease.InOutSine));
            atkFreezeColorAnim.Append(attackPowerText.DOColor(FREEZE_ORANGE, 1.2f).SetEase(Ease.InOutSine));
            atkFreezeColorAnim.SetLoops(-1, LoopType.Restart);
        }

        if (freezeTurnText != null)
        {
            freezeTurnText.DOKill();
            freezeTurnText.color = FREEZE_ORANGE;
            freezeTurnColorAnim = DOTween.Sequence();
            freezeTurnColorAnim.Append(freezeTurnText.DOColor(FREEZE_BLACK, 1.2f).SetEase(Ease.InOutSine));
            freezeTurnColorAnim.Append(freezeTurnText.DOColor(FREEZE_ORANGE, 1.2f).SetEase(Ease.InOutSine));
            freezeTurnColorAnim.SetLoops(-1, LoopType.Restart);
        }

        if (freezeTotalDamageText != null)
        {
            freezeTotalDamageText.DOKill();
            freezeTotalDamageText.color = FREEZE_ORANGE;
            freezeTotalDmgColorAnim = DOTween.Sequence();
            freezeTotalDmgColorAnim.Append(freezeTotalDamageText.DOColor(FREEZE_BLACK, 0.7f).SetEase(Ease.InOutSine));
            freezeTotalDmgColorAnim.Append(freezeTotalDamageText.DOColor(FREEZE_ORANGE, 0.7f).SetEase(Ease.InOutSine));
            freezeTotalDmgColorAnim.SetLoops(-1, LoopType.Restart);
        }

        // 아이콘 이미지 색상 동기화 (ATK 텍스트와 같은 주기)
        if (atkIconImage != null)
        {
            atkIconImage.DOKill();
            atkIconImage.color = FREEZE_ORANGE;
            atkIconFreezeAnim = DOTween.Sequence();
            atkIconFreezeAnim.Append(atkIconImage.DOColor(FREEZE_BLACK, 1.2f).SetEase(Ease.InOutSine));
            atkIconFreezeAnim.Append(atkIconImage.DOColor(FREEZE_ORANGE, 1.2f).SetEase(Ease.InOutSine));
            atkIconFreezeAnim.SetLoops(-1, LoopType.Restart);
        }

        // Boost 아이콘 색상 동기화 (freezeTurnText와 같은 주기)
        if (boostIconImage != null)
        {
            boostIconImage.DOKill();
            boostIconImage.color = FREEZE_ORANGE;
            boostIconFreezeAnim = DOTween.Sequence();
            boostIconFreezeAnim.Append(boostIconImage.DOColor(FREEZE_BLACK, 1.2f).SetEase(Ease.InOutSine));
            boostIconFreezeAnim.Append(boostIconImage.DOColor(FREEZE_ORANGE, 1.2f).SetEase(Ease.InOutSine));
            boostIconFreezeAnim.SetLoops(-1, LoopType.Restart);
        }
    }

    void StopFreezeColorLoops()
    {
        if (atkFreezeColorAnim != null) { atkFreezeColorAnim.Kill(); atkFreezeColorAnim = null; }
        if (freezeTurnColorAnim != null) { freezeTurnColorAnim.Kill(); freezeTurnColorAnim = null; }
        if (freezeTotalDmgColorAnim != null) { freezeTotalDmgColorAnim.Kill(); freezeTotalDmgColorAnim = null; }
        if (atkIconFreezeAnim != null) { atkIconFreezeAnim.Kill(); atkIconFreezeAnim = null; }
        if (boostIconFreezeAnim != null) { boostIconFreezeAnim.Kill(); boostIconFreezeAnim = null; }

        if (attackPowerText != null)
        {
            attackPowerText.DOKill();
            if (atkColorSaved) { Color c = atkOriginalColor; c.a = 0.35f; attackPowerText.color = c; }
        }
        if (atkIconImage != null)
        {
            atkIconImage.DOKill();
            if (atkIconColorSaved) { Color c = atkIconOriginalColor; c.a = 0.35f; atkIconImage.color = c; }
        }
        if (boostIconImage != null)
        {
            boostIconImage.DOKill();
            if (boostIconColorSaved) boostIconImage.color = boostIconOriginalColor;
        }
    }

    void SetProgressBarFreezeColor()
    {
        if (progressBarFill == null) return;
        Image fillImg = progressBarFill.GetComponent<Image>();
        if (fillImg != null) fillImg.color = new Color(0.9f, 0.2f, 0.2f);
    }

    void RestoreProgressBarColor()
    {
        if (progressBarFill == null || !progressBarColorSaved) return;
        Image fillImg = progressBarFill.GetComponent<Image>();
        if (fillImg != null) fillImg.color = progressBarOriginalColor;
    }

    // === Continue ===
    public bool CanContinue()
    {
        if (cheatInfiniteContinue) return true;
        // 9 stage 전엔 continue 불가
        if (unlockManager != null && !unlockManager.IsFullGaugeUnlocked) return false;
        return continueCount < MAX_CONTINUES;
    }

    public void UseContinue()
    {
        if (!cheatInfiniteContinue) continueCount++;
        UpdateContinueGuideText();
    }

    public void UpdateContinueGuideText()
    {
        if (continueGuideText != null)
        {
            if (cheatInfiniteContinue)
                continueGuideText.text = "∞";
            else if (unlockManager != null && !unlockManager.IsFullGaugeUnlocked)
                continueGuideText.text = "Unlock at 9";
            else
                continueGuideText.text = $"{MAX_CONTINUES - continueCount}/{MAX_CONTINUES}";
        }
    }

    public void ContinueIntoFever()
    {
        isFeverMode = true; mergeGauge = GAUGE_MAX; feverBulletUsed = false; hasBullet = false;
        freezeTurnCount = 0; freezeTotalDamage = 0;

        SpawnFeverParticle();
        if (feverBackgroundImage != null)
        {
            feverBackgroundImage.gameObject.SetActive(true);
            Color c = feverBackgroundImage.color; c.a = 1.0f; feverBackgroundImage.color = c;
            feverBackgroundImage.DOKill();
            feverBackgroundImage.DOFade(0.7f, 0.5f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
        }
        if (freezeImage1 != null) freezeImage1.gameObject.SetActive(true);
        if (bossManager != null) { bossManager.SetFrozen(true); bossManager.ResetBonusTurns(); }
        SetProgressBarFreezeColor();
        StartFreezeColorLoops();
        FireFeverFreezeLaser();

        if (freezeTurnText != null) { freezeTurnText.gameObject.SetActive(true); freezeTurnText.text = "0 (x1.00)"; }
        if (freezeTotalDamageText != null) { freezeTotalDamageText.gameObject.SetActive(true); freezeTotalDamageText.text = "0"; }
        UpdateGunUI();
    }

    // === Freeze 레이저 ===
    void FireFeverFreezeLaser()
    {
        ProjectileManager pm = bossBattle.GetProjectileManager();
        if (pm == null || gunButton == null || bossManager == null || bossManager.bossImageArea == null) return;
        RectTransform monsterRect = bossManager.bossImageArea.GetComponent<RectTransform>();
        pm.FireFreezeLaser(gunButton.transform.position, monsterRect.position, new Color(0.5f, 0.85f, 1f, 0.9f), null);
    }

    // === Fever 파티클 (Screen.width 보정) ===
    void SpawnFeverParticle()
    {
        if (feverParticleSpawnPoint == null) return;
        if (activeFeverParticle != null) Destroy(activeFeverParticle);

        GameObject particleObj = new GameObject("FeverFlameParticle");
        particleObj.transform.SetParent(feverParticleSpawnPoint, false);
        particleObj.transform.localPosition = Vector3.zero;

        ParticleSystem ps = particleObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        float psc = Tile.SmallParticleSizeCorrectionStatic();
        main.startLifetime = 0.5f; main.startSpeed = 15f; main.startSize = 12f / psc;
        main.startColor = new Color(1f, 0.5f, 0f); main.maxParticles = 50;
        main.simulationSpace = ParticleSystemSimulationSpace.Local; main.playOnAwake = true; main.loop = true;

        var emission = ps.emission; emission.enabled = true; emission.rateOverTime = 20;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 15f; shape.radius = 3f;

        var col = ps.colorOverLifetime; col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(1f, 1f, 0f), 0f), new GradientColorKey(new Color(1f, 0.5f, 0f), 0.5f), new GradientColorKey(new Color(1f, 0f, 0f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = new ParticleSystem.MinMaxGradient(g);

        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.y = new ParticleSystem.MinMaxCurve(30f);
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = new Material(Shader.Find("UI/Default")); renderer.sortingOrder = 1;
        float pScale = 3f * ((float)Screen.width / 498f);
        var uiP = particleObj.AddComponent<Coffee.UIExtensions.UIParticle>(); uiP.scale = pScale;

        activeFeverParticle = particleObj;
    }

    // === Freeze Sync ===
    public IEnumerator SyncFreezeWithBossRespawn()
    {
        if (freezeImage1 != null) { freezeImage1.DOKill(); freezeImage1.gameObject.SetActive(false); }
        CleanupFreezeLasers();

        while (bossBattle.IsBossTransitioning)
            yield return null;

        // Clear 모드(41+)는 빠르게, 일반은 보스 등장 애니메이션 대기
        bool isClearMode = bossManager != null && bossManager.IsClearMode();
        yield return new WaitForSeconds(isClearMode ? 0.8f : 3.5f);

        if (!isFeverMode) yield break;

        FireFeverFreezeLaser();
        if (freezeImage1 != null)
        {
            freezeImage1.gameObject.SetActive(true);
            freezeImage1.color = new Color(1f, 1f, 1f, 0f);
            freezeImage1.DOFade(70f / 255f, 0.5f).SetEase(Ease.OutQuad);
        }
    }

    void CleanupFreezeLasers()
    {
        var projectiles = GameObject.FindObjectsByType<Projectile>(FindObjectsSortMode.None);
        foreach (var p in projectiles)
        {
            if (p != null && p.gameObject.name.Contains("Freeze"))
                Destroy(p.gameObject);
        }
    }

    // === ATK Floating Text (검정) ===
    void ShowATKChangeText(long increase)
    {
        if (damageTextPrefab == null || damageTextParent == null || attackPowerText == null) return;
        GameObject obj = Instantiate(damageTextPrefab, damageTextParent);
        TextMeshProUGUI txt = obj.GetComponent<TextMeshProUGUI>();
        if (txt != null)
        {
            txt.text = $"+{increase}";
            txt.color = Color.black;
            txt.fontSize = 32;
            RectTransform r = obj.GetComponent<RectTransform>();
            RectTransform atkRect = attackPowerText.GetComponent<RectTransform>();

            Vector3[] corners = new Vector3[4];
            atkRect.GetWorldCorners(corners);
            Vector3 rightEdgeWorld = (corners[2] + corners[3]) * 0.5f;
            r.position = rightEdgeWorld;

            CanvasGroup cg = obj.GetComponent<CanvasGroup>(); if (cg == null) cg = obj.AddComponent<CanvasGroup>();
            DOTween.Sequence()
                .Append(r.DOAnchorPosY(r.anchoredPosition.y + 60f, 0.7f).SetEase(Ease.OutCubic))
                .Join(cg.DOFade(0f, 0.7f).SetEase(Ease.InCubic))
                .Insert(0f, r.DOScale(1.3f, 0.1f).SetEase(Ease.OutQuad))
                .Insert(0.1f, r.DOScale(1f, 0.15f).SetEase(Ease.InQuad))
                .OnComplete(() => { if (obj != null) Destroy(obj); });
        }
    }

    // === Gun 모드 ===
    public void ToggleGunMode()
    {
        if (bossBattle.IsBossAttacking) return;
        if (isGunMode) { ExitGunMode(); return; }
        if (!hasBullet && (!isFeverMode || feverBulletUsed)) return;
        if (gridManager.ActiveTiles.Count <= 2) return;

        isGunMode = true;
        if (gunModeGuideText != null) { gunModeGuideText.gameObject.SetActive(true); gunModeGuideText.text = "Cancel"; }
        if (gunModeOverlayImage != null) gunModeOverlayImage.gameObject.SetActive(true);
        gridManager.UpdateTileBorders();
        gridManager.DimProtectedTiles(true);
        StartHPBarGunModeAnim();
        StartProgressBarGlow();
        UpdateGunUI();
    }

    void ExitGunMode()
    {
        isGunMode = false;
        if (gunModeOverlayImage != null) gunModeOverlayImage.gameObject.SetActive(false);
        gridManager.ClearAllTileBorders();
        gridManager.DimProtectedTiles(false);
        StopHPBarGunModeAnim();
        StopProgressBarGlow();
        UpdateGuideText();
        UpdateGunUI();
    }

    // === 총 발사 ===
    public void ShootTile()
    {
        if (!hasBullet && (!isFeverMode || feverBulletUsed)) { ExitGunMode(); return; }

        var topTwo = gridManager.GetTopTwoTileValues();
        if (gridManager.ActiveTiles.Count <= 2) { ExitGunMode(); return; }

        Canvas canvas = gridManager.GridContainer.GetComponentInParent<Canvas>();
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main;
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(gridManager.GridContainer, Input.mousePosition, cam, out localPoint);

        Tile targetTile = null;
        float minDist = gridManager.CellSize / 2;
        foreach (var tile in gridManager.ActiveTiles)
        {
            if (tile == null) continue;
            float d = Vector2.Distance(localPoint, tile.GetComponent<RectTransform>().anchoredPosition);
            if (d < minDist) { minDist = d; targetTile = tile; }
        }

        if (targetTile == null) return;
        var curTop = gridManager.GetTopTwoTileValues();
        if (targetTile.value == curTop.Item1 || targetTile.value == curTop.Item2) return;

        int oldHP = playerHP.CurrentHeat;
        playerHP.SetHeatToMax();
        playerHP.UpdateHeatUI(false);
        int recovery = playerHP.CurrentHeat - oldHP;
        if (recovery > 0) playerHP.ShowHeatChangeText(recovery);

        Vector2Int pos = targetTile.gridPosition;
        targetTile.PlayGunDestroyEffect();
        gridManager.Tiles[pos.x, pos.y] = null;
        gridManager.ActiveTiles.Remove(targetTile);
        Destroy(targetTile.gameObject);

        if (isFeverMode)
        {
            feverBulletUsed = true;
            hasBullet = false;
            mergeGauge -= GUN_SHOT_COST;
            if (mergeGauge < 0) mergeGauge = 0;
            EndFever();
        }
        else
        {
            mergeGauge = Mathf.Max(0, mergeGauge - GUN_SHOT_COST);
            hasBullet = (mergeGauge >= GAUGE_FOR_BULLET);
        }

        StopEmergencyFlash();

        // 손가락 튜토리얼 가이드 숨기기
        if (unlockManager != null) unlockManager.DismissFingerGuide();

        // progress text 강제 초기화 (총 쓴 후 주황색/스케일 잔류 방지)
        if (turnsUntilBulletText != null)
        {
            turnsUntilBulletText.DOKill();
            RectTransform tr = turnsUntilBulletText.GetComponent<RectTransform>();
            tr.DOKill();
            tr.localScale = Vector3.one;
            if (turnsTextInitialized)
                tr.anchoredPosition = new Vector2(tr.anchoredPosition.x, turnsTextOriginalY);
        }

        ExitGunMode();
        if (!gridManager.CanMove() && !hasBullet && !isFeverMode) bossBattle.GameOver();
    }

    // === Gauge Change Text ===
    void ShowGaugeChangeText(int change, bool isCombo = false)
    {
        if (damageTextPrefab == null || damageTextParent == null || turnsUntilBulletText == null) return;
        // Gun UI 미해금 시 텍스트 생성 안함
        if (unlockManager != null && !unlockManager.IsGunUnlocked) return;
        GameObject obj = Instantiate(damageTextPrefab, damageTextParent);
        TextMeshProUGUI txt = obj.GetComponent<TextMeshProUGUI>();
        if (txt != null)
        {
            if (isCombo)
                txt.text = change > 0 ? $"Combo! +{change}" : $"Combo! {change}";
            else
                txt.text = change > 0 ? $"+{change}" : change.ToString();

            txt.color = change > 0 ? new Color(0.9f, 0.2f, 0.2f) : new Color(0.6f, 0.6f, 0.6f);
            txt.fontSize = 36;
            RectTransform r = obj.GetComponent<RectTransform>();
            r.position = turnsUntilBulletText.GetComponent<RectTransform>().position;
            CanvasGroup cg = obj.GetComponent<CanvasGroup>(); if (cg == null) cg = obj.AddComponent<CanvasGroup>();
            DOTween.Sequence()
                .Append(r.DOAnchorPosY(r.anchoredPosition.y + 80f, 0.8f).SetEase(Ease.OutCubic))
                .Join(cg.DOFade(0f, 0.8f).SetEase(Ease.InCubic))
                .Insert(0f, r.DOScale(1.2f, 0.1f).SetEase(Ease.OutQuad))
                .Insert(0.1f, r.DOScale(1f, 0.1f).SetEase(Ease.InQuad))
                .OnComplete(() => { if (obj != null) Destroy(obj); });
        }
    }

    // === BulletCount 상태 변경 DOTween 효과 ===
    void AnimateBulletCountChange(string newState)
    {
        if (bulletCountText == null) return;
        if (newState == lastBulletCountState) return;
        lastBulletCountState = newState;

        RectTransform rt = bulletCountText.GetComponent<RectTransform>();
        rt.DOKill();
        rt.localScale = Vector3.one * 1.3f;
        rt.DOScale(1f, 0.25f).SetEase(Ease.OutBack);

        bulletCountText.DOKill();
        Color origColor = bulletCountText.color;
        bulletCountText.color = Color.white;
        bulletCountText.DOColor(origColor, 0.3f).SetDelay(0.1f);
    }

    // === Gun UI ===
    public void UpdateGunUI()
    {
        if (bulletCountText != null)
        {
            string newState;
            if (isFeverMode) newState = "FREEZE!";
            else if (hasBullet) newState = "CHARGE";
            else newState = "RELOAD";

            bulletCountText.text = newState;
            AnimateBulletCountChange(newState);
        }

        UpdateGuideText();

        if (turnsUntilBulletText != null)
        {
            if (!turnsTextInitialized)
            {
                turnsTextOriginalY = turnsUntilBulletText.GetComponent<RectTransform>().anchoredPosition.y;
                turnsTextInitialized = true;
            }

            int displayCap = (unlockManager != null) ? unlockManager.GetGaugeCap() : GAUGE_MAX;
            if (displayCap <= 0) displayCap = GAUGE_MAX; // 미해금 시에도 UI는 숨겨져 있으므로
            turnsUntilBulletText.text = $"{mergeGauge}/{displayCap}";

            // 20/20 또는 20/40, 40/40 도달 시 특별 효과
            if (mergeGauge != lastMergeGauge)
            {
                bool hitHalf = (lastMergeGauge < GAUGE_FOR_BULLET && mergeGauge >= GAUGE_FOR_BULLET);
                bool hitFull = (displayCap >= GAUGE_MAX) && (lastMergeGauge < GAUGE_MAX && mergeGauge >= GAUGE_MAX);
                // 반절 모드에서 20/20 도달도 hitFull 스타일
                bool isFullGaugeMode = (unlockManager == null || unlockManager.IsFullGaugeUnlocked);
                if (!isFullGaugeMode && mergeGauge >= displayCap && lastMergeGauge < displayCap)
                    hitFull = true;
                lastMergeGauge = mergeGauge;

                RectTransform tr = turnsUntilBulletText.GetComponent<RectTransform>();
                tr.DOKill();

                if (hitHalf || hitFull)
                {
                    // 색상 효과만 (스케일 없음)
                    tr.localScale = Vector3.one;
                    turnsUntilBulletText.DOKill();
                    Color origC = turnsUntilBulletText.color;
                    turnsUntilBulletText.color = new Color(1f, 0.6f, 0.1f);
                    turnsUntilBulletText.DOColor(origC, 0.5f).SetDelay(0.2f)
                        .OnComplete(() => { if (turnsUntilBulletText != null) turnsUntilBulletText.color = origC; });
                }
                else
                {
                    DOTween.Sequence()
                        .Append(tr.DOAnchorPosY(turnsTextOriginalY + 8f, 0.12f).SetEase(Ease.OutQuad))
                        .Append(tr.DOAnchorPosY(turnsTextOriginalY, 0.12f).SetEase(Ease.InQuad))
                        .OnComplete(() => { if (tr != null) tr.anchoredPosition = new Vector2(tr.anchoredPosition.x, turnsTextOriginalY); });
                }
            }
        }

        if (attackPowerText != null)
        {
            if (!attackTextInitialized)
            {
                attackTextOriginalY = attackPowerText.GetComponent<RectTransform>().anchoredPosition.y;
                attackTextInitialized = true;
            }

            attackPowerText.text = $"+{permanentAttackPower:N0}";

            if (!isFeverMode)
            {
                Color c = atkColorSaved ? atkOriginalColor : Color.black;
                c.a = 0.35f;
                if (atkFreezeColorAnim == null)
                    attackPowerText.color = c;
            }

            // atkIcon 색상/alpha 동기화 (freeze 중이 아닐 때)
            if (atkIconImage != null && !isFeverMode && atkIconFreezeAnim == null)
            {
                Color ic = atkIconColorSaved ? atkIconOriginalColor : Color.black;
                ic.a = attackPowerText.color.a;
                atkIconImage.color = ic;
            }

            if (permanentAttackPower != lastPermanentAttackPower)
            {
                long increase = permanentAttackPower - lastPermanentAttackPower;
                lastPermanentAttackPower = permanentAttackPower;
                RectTransform tr = attackPowerText.GetComponent<RectTransform>();
                tr.DOKill();
                DOTween.Sequence()
                    .Append(tr.DOAnchorPosY(attackTextOriginalY + 10f, 0.15f).SetEase(Ease.OutQuad))
                    .Append(tr.DOAnchorPosY(attackTextOriginalY, 0.15f).SetEase(Ease.InQuad))
                    .OnComplete(() => { if (tr != null) tr.anchoredPosition = new Vector2(tr.anchoredPosition.x, attackTextOriginalY); });
                ShowATKChangeText(increase);
            }
        }

        if (progressBarFill != null)
        {
            float progress = Mathf.Clamp01((float)mergeGauge / GAUGE_MAX);
            float targetW = progressBarFill.parent.GetComponent<RectTransform>().rect.width * progress;
            progressBarFill.DOKill();
            progressBarFill.DOSizeDelta(new Vector2(targetW, progressBarFill.sizeDelta.y), 0.3f).SetEase(Ease.OutQuad);
        }

        if (gunButtonImage != null && !isEmergencyFlashing)
        {
            if (isGunMode) gunButtonImage.color = Color.red;
            else if (isFeverMode) gunButtonImage.color = new Color(1f, 0.3f, 0f);
            else if (hasBullet) gunButtonImage.color = GUN_READY_MINT;
            else gunButtonImage.color = new Color(0.5f, 0.5f, 0.5f);
        }

        if (gunButton != null)
        {
            gunButton.interactable = !bossBattle.IsGameOver && !bossBattle.IsBossTransitioning
                && (hasBullet || (isFeverMode && !feverBulletUsed))
                && gridManager.ActiveTiles.Count > 1;
        }

        UpdateGunButtonAnimationIfNeeded(hasBullet || (isFeverMode && !feverBulletUsed));

        // 손가락 튜토리얼 가이드 체크
        if (unlockManager != null) unlockManager.CheckFingerGuide(mergeGauge);

        // 회복력 UI 갱신
        UpdateHealPowerUI();
    }

    public void UpdateGuideText()
    {
        if (gunModeGuideText == null) return;
        if (isGunMode) { gunModeGuideText.gameObject.SetActive(true); gunModeGuideText.text = "Cancel"; return; }
        gunModeGuideText.gameObject.SetActive(true);
        if (isFeverMode) gunModeGuideText.text = "Ready";
        else if (hasBullet) gunModeGuideText.text = "Ready";
        else gunModeGuideText.text = "";
    }

    // === Gun Button 애니메이션 ===
    void UpdateGunButtonAnimationIfNeeded(bool shouldAnimate)
    {
        bool currentState = isGunMode || shouldAnimate;
        if (currentState == lastGunButtonAnimationState && gunButtonHeartbeat != null) return;
        lastGunButtonAnimationState = currentState;

        if (gunButton == null || gunButtonImage == null) return;
        if (gunButtonHeartbeat != null) { gunButtonHeartbeat.Kill(); gunButtonHeartbeat = null; }

        Color c = gunButtonImage.color; c.a = 1f; gunButtonImage.color = c;
        gunButton.transform.localScale = Vector3.one;

        if (isGunMode)
            gunButtonHeartbeat = gunButton.transform.DOScale(1.15f, 0.3f).SetEase(Ease.InOutQuad).SetLoops(-1, LoopType.Yoyo);
        else if (shouldAnimate)
            gunButtonHeartbeat = gunButton.transform.DOScale(1.1f, 0.6f).SetEase(Ease.InOutQuad).SetLoops(-1, LoopType.Yoyo);
    }

    void UpdateGunButtonAnimation()
    {
        lastGunButtonAnimationState = false;
        UpdateGunButtonAnimationIfNeeded(hasBullet || (isFeverMode && !feverBulletUsed));
    }

    // === 긴급 깜빡임 (민트↔붉은색) ===
    public void SetEmergencyFlash(bool shouldFlash)
    {
        if (shouldFlash && gunButtonImage != null)
        {
            if (!isEmergencyFlashing) { isEmergencyFlashing = true; StartEmergencyFlashLoop(); }
        }
        else { StopEmergencyFlash(); }
    }

    void StartEmergencyFlashLoop()
    {
        if (gunButtonImage == null) return;
        if (emergencyGunFlash != null) { emergencyGunFlash.Kill(); emergencyGunFlash = null; }
        Color colorA = GUN_READY_MINT;
        Color colorB = new Color(1f, 0.25f, 0.25f);
        gunButtonImage.color = colorA;
        emergencyGunFlash = DOTween.Sequence();
        emergencyGunFlash.Append(gunButtonImage.DOColor(colorB, 0.35f).SetEase(Ease.InOutSine));
        emergencyGunFlash.Append(gunButtonImage.DOColor(colorA, 0.35f).SetEase(Ease.InOutSine));
        emergencyGunFlash.SetLoops(-1, LoopType.Restart);
    }

    void StopEmergencyFlash()
    {
        if (emergencyGunFlash != null) { emergencyGunFlash.Kill(); emergencyGunFlash = null; }
        isEmergencyFlashing = false;
        if (gunButtonImage != null) { Color c = gunButtonImage.color; c.a = 1f; gunButtonImage.color = c; }
    }

    // === Progress bar glow ===
    void StartProgressBarGlow()
    {
        StopProgressBarGlow();
        if (progressBarGlowOverlay == null) return;
        progressBarGlowOverlay.gameObject.SetActive(true);
        Color c = progressBarGlowOverlay.color; c.a = 0f; progressBarGlowOverlay.color = c;
        progressBarGlowAnim = DOTween.Sequence();
        progressBarGlowAnim.Append(progressBarGlowOverlay.DOFade(0.5f, 0.5f).SetEase(Ease.InOutSine));
        progressBarGlowAnim.Append(progressBarGlowOverlay.DOFade(0f, 0.5f).SetEase(Ease.InOutSine));
        progressBarGlowAnim.SetLoops(-1, LoopType.Restart);
    }

    void StopProgressBarGlow()
    {
        if (progressBarGlowAnim != null) { progressBarGlowAnim.Kill(); progressBarGlowAnim = null; }
        if (progressBarGlowOverlay != null)
        {
            progressBarGlowOverlay.DOKill();
            Color c = progressBarGlowOverlay.color; c.a = 0f; progressBarGlowOverlay.color = c;
            progressBarGlowOverlay.gameObject.SetActive(false);
        }
    }

    // === HP bar 배경 ===
    void StartHPBarGunModeAnim()
    {
        StopHPBarGunModeAnim();
        if (hpBarBackgroundImage == null) return;
        hpBarOriginalBgColor = hpBarBackgroundImage.color;
        Color greenColor = new Color(0.3f, 0.8f, 0.4f);
        hpBarGunModeAnim = DOTween.Sequence();
        hpBarGunModeAnim.Append(hpBarBackgroundImage.DOColor(greenColor, 0.5f).SetEase(Ease.InOutSine));
        hpBarGunModeAnim.Append(hpBarBackgroundImage.DOColor(hpBarOriginalBgColor, 0.5f).SetEase(Ease.InOutSine));
        hpBarGunModeAnim.SetLoops(-1, LoopType.Restart);
    }

    void StopHPBarGunModeAnim()
    {
        if (hpBarGunModeAnim != null) { hpBarGunModeAnim.Kill(); hpBarGunModeAnim = null; }
        if (hpBarBackgroundImage != null && hpBarBgColorSaved)
            hpBarBackgroundImage.color = hpBarOriginalBgColor;
    }

    // === Cleanup ===
    public void CleanupFeverEffects()
    {
        // Damage record 갱신
        CheckAndUpdateDamageRecord();

        if (activeFeverParticle != null) { Destroy(activeFeverParticle); activeFeverParticle = null; }
        if (feverBackgroundImage != null) { feverBackgroundImage.DOKill(); feverBackgroundImage.gameObject.SetActive(false); }
        if (freezeImage1 != null) freezeImage1.gameObject.SetActive(false);
        if (bossManager != null) bossManager.SetFrozen(false);
        RestoreProgressBarColor();
        StopHPBarGunModeAnim();
        StopProgressBarGlow();
        StopFreezeColorLoops();
        StopEmergencyFlash();
    }

    // === 회복력 UI (_13) ===
    // challenge 3 이상에서 표시, 레벨업 후 즉시 반영
    public void UpdateHealPowerUI()
    {
        if (healPowerText == null || playerHP == null || bossManager == null) return;
        int stage = bossManager.GetBossLevel();
        if (stage < 3)
        {
            healPowerText.gameObject.SetActive(false);
            return;
        }
        healPowerText.gameObject.SetActive(true);
        int healPower = playerHP.GetMixHealPower();
        healPowerText.text = $"{healPower} \u2665";
    }
}
