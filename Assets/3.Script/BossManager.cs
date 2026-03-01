using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;

public class BossManager : MonoBehaviour
{
    [Header("Boss UI References")]
    public Image bossImageArea;
    public Slider hpSlider;
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI bossAttackInfoText;

    [Header("Boss Panel Background")]
    [SerializeField] private Image bossPanelGroundImage;

    [Header("Boss Stats")]
    public int baseHP = 200;
    public int hpIncreasePerLevel = 200;
    private int maxHP;
    private int currentHP;

    [Header("보스 공격 시스템")]
    [SerializeField] private int baseTurnInterval = 8;
    [SerializeField] private int minTurnInterval = 3;

    [Header("Enemy ATK 성장 설정")]
    [SerializeField] private int baseDamage = 28;
    [SerializeField] private int atkGrowthPerStep = 3;
    [SerializeField] private int atkGrowthInterval = 2;
    [SerializeField] private int bossAtkMaxTotal = 90;
    [SerializeField] private int clearModeFixedAtk = 60;

    private int currentTurnInterval;
    private int currentTurnCount = 0;
    private int currentBossDamage;

    [Header("Boss Progression")]
    public int bossLevel = 1;

    [Header("HP Bar Animation")]
    public float animationDuration = 0.3f;
    public float bossSpawnDelay = 1.0f;

    [Header("Boss Attack Animation")]
    [SerializeField] private float attackMotionDuration = 0.22f;

    [Header("Enemy Data (ScriptableObject)")]
    [SerializeField] private EnemyData enemyData;

    [Header("Attack Info 색상 루프")]
    [SerializeField] private Color attackInfoColorA = new Color(0.7f, 0.7f, 0.7f);
    [SerializeField] private Color attackInfoColorB = new Color(1f, 0.85f, 0.5f);
    [SerializeField] private float attackInfoColorSpeed = 1.5f;
    private Sequence attackInfoColorLoop;

    [Header("ATK 아이콘 이미지 (텍스트 색상/alpha 동기화)")]
    [SerializeField] private Image atkIconImage;

    [Header("Guard ATK Slider (Enemy HP bar와 동일 구조)")]
    [SerializeField] private Slider guardAtkSlider;
    [SerializeField] private int guardAtkIncreaseTurns = 20;

    [Header("Guard 색상 루프 (bossImageArea)")]
    [SerializeField] private Color guardColorA = new Color(1.0f, 0.75f, 0.5f, 1.0f);  // 루프 A색 (기본 오렌지)
    [SerializeField] private Color guardColorB = new Color(0.9f, 0.2f, 0.15f, 1.0f);  // 루프 B색 (붉은색)
    [SerializeField] private float guardColorSpeed = 1.0f;

    [Header("Guard HP Bar 색상 루프")]
    [SerializeField] private Color guardHPBarColorA = new Color(1f, 0.6f, 0.15f, 1f);   // 루프 A색 (주황)
    [SerializeField] private Color guardHPBarColorB = new Color(0.9f, 0.2f, 0.15f, 1f); // 루프 B색 (붉은색)
    [SerializeField] private float guardHPBarColorSpeed = 1.5f;

    [Header("Guard 해제 후 색상 (bossImageArea / HP Bar)")]
    [SerializeField] private Color clearEnemyColor = new Color(0.9f, 0.2f, 0.15f, 1.0f);   // Guard 해제 후 보스 이미지색
    [SerializeField] private Color clearHPBarColor = new Color(0.9f, 0.2f, 0.15f, 1.0f);   // Guard 해제 후 HP bar색

    [Header("Guard 해제 후 Clear 모드 배경 색상")]
    [SerializeField] private Color clearModeGroundColor = new Color(0.2f, 0.15f, 0.3f, 1f);

    [Header("41 Stage 이후 Enemy color (Clear/Infinite 모드)")]
    [SerializeField] private Color infiniteEnemyColor = new Color(0.9f, 0.2f, 0.15f, 1.0f);

    [Header("공격 1턴 전 경고 색상 (bossImageArea에 적용)")]
    [SerializeField] private Color attackWarningColor = new Color(1f, 0.2f, 0.2f, 1.0f);
    [SerializeField] private float attackWarningFadeDuration = 0.35f;

    [Header("Freeze 상태 Enemy color (Guard Boss외)")]
    [SerializeField] private Color freezeEnemyColor = new Color(0.45f, 0.75f, 1.0f, 1.0f);

    [Header("스테이지 배경 색상 (Inspector 설정)")]
    [SerializeField] private Color stageColor_1_10  = new Color(0.25f, 0.25f, 0.35f, 1f);
    [SerializeField] private Color stageColor_11_20 = new Color(0.65f, 0.78f, 0.9f, 1f);
    [SerializeField] private Color stageColor_21_30 = new Color(0.9f, 0.7f, 0.8f, 1f);
    [SerializeField] private Color stageColor_31_40 = new Color(0.72f, 0.55f, 0.42f, 1f);

    [Header("Boss Images")]
    [SerializeField] private List<Sprite> bossSprites = new List<Sprite>();
    private int currentBossIndex = 0;

    private bool isTransitioning = false;
    private GameManager gameManager;
    private Tweener bossIdleAnimation;
    private Sequence attackBlinkAnimation;
    private bool isFirstGame = true;

    private bool isFrozen = false;
    private int bonusTurnsAdded = 0;
    private int bonusTurnsConsumed = 0;
    private int bonusTurnsTotal = 0;

    private static readonly Color ICE_BLUE = new Color(0.5f, 0.8f, 1f);
    // bossImageArea freeze 색상 애니메이션
    private Sequence freezeEnemyColorAnim;
    // bossImageArea 경고색 기준 (Material 원본색)
    private Color bossMatOriginalColor = new Color(1.0f, 0.75f, 0.5f, 1.0f);

    private int infiniteBossExtraDamage = 0;
    private bool pendingDamageIncrease = false;

    // Guard 모드
    private bool isGuardMode = false;
    private Sequence guardColorSequence;
    private int guardAtkTurnCounter = 0; // Guard ATK 턴 카운터

    // Clear 모드
    private bool isClearMode = false;
    private int stage39SpriteIndex = -1;
    private Color originalGroundColor;
    private bool groundColorSaved = false;

    // HP bar glow
    private Sequence hpBarGlowSequence;
    private BossBattleSystem bossBattleSystem;
    private PlayerHPSystem playerHPSystem;
    private UnlockManager unlockManager;
    private GunSystem gunSystem;

    void Start()
    {
        gameManager = FindAnyObjectByType<GameManager>();
        bossBattleSystem = FindAnyObjectByType<BossBattleSystem>();
        playerHPSystem = FindAnyObjectByType<PlayerHPSystem>();
        unlockManager = FindAnyObjectByType<UnlockManager>();
        gunSystem = FindAnyObjectByType<GunSystem>();

        if (bossPanelGroundImage != null && !groundColorSaved)
        {
            originalGroundColor = bossPanelGroundImage.color;
            groundColorSaved = true;
        }

        InitializeBoss();
        StartBossIdleAnimation();
    }

    void InitializeBoss()
    {
        ApplyEnemyStats(bossLevel);

        currentHP = maxHP;
        currentTurnInterval = Mathf.Max(minTurnInterval, baseTurnInterval - Mathf.FloorToInt((bossLevel - 1) * 0.2f));

        infiniteBossExtraDamage = 0;
        currentTurnCount = currentTurnInterval;
        guardAtkTurnCounter = 0;

        if (bossLevel >= 40 && !isClearMode)
        {
            isGuardMode = true;
            StartGuardColorAnimation();
            ShowGuardAtkSlider();
        }
        else
        {
            HideGuardAtkSlider();
        }

        if (isClearMode)
        {
            currentBossDamage = clearModeFixedAtk;
            infiniteBossExtraDamage = 0;
        }
        else if (bossLevel >= 41 && !isGuardMode)
        {
            currentBossDamage = clearModeFixedAtk;
            infiniteBossExtraDamage = 0;
            ApplyRedColor();
        }

        if (bossBattleSystem != null && bossBattleSystem.LowHealthVignette != null)
            bossBattleSystem.LowHealthVignette.SetEnemyAtk(GetEffectiveDamage());

        UpdateUI(true);
        UpdateStageBackgroundColor();
        Debug.Log($"Boss Level {bossLevel} spawned! HP: {currentHP}/{maxHP}, ATK: {GetEffectiveDamage()}, Guard: {isGuardMode}, Clear: {isClearMode}");
    }

    // === Guard ATK Slider 재설계 ===
    void ShowGuardAtkSlider()
    {
        if (guardAtkSlider == null) return;
        guardAtkSlider.gameObject.SetActive(true);
        guardAtkSlider.minValue = 0f;
        guardAtkSlider.maxValue = 1f;
        guardAtkSlider.value = 0f;
        guardAtkTurnCounter = 0;
    }

    void HideGuardAtkSlider()
    {
        if (guardAtkSlider == null) return;
        guardAtkSlider.DOKill();
        guardAtkSlider.gameObject.SetActive(false);
    }

    void UpdateGuardAtkSliderProgress()
    {
        if (guardAtkSlider == null || !guardAtkSlider.gameObject.activeSelf) return;
        float progress = Mathf.Clamp01((float)guardAtkTurnCounter / guardAtkIncreaseTurns);
        guardAtkSlider.DOKill();
        guardAtkSlider.DOValue(progress, 0.25f).SetEase(Ease.OutQuad);
    }

    // Guard 턴 진행: 매 턴 카운터 증가 → 꽉 차면 ATK 증가 → 리셋
    public void ProcessGuardAtkTurn()
    {
        if (!isGuardMode) return;
        if (isClearMode) return;

        guardAtkTurnCounter++;
        UpdateGuardAtkSliderProgress();

        if (guardAtkTurnCounter >= guardAtkIncreaseTurns)
        {
            // 꽉 참 → ATK 증가
            guardAtkTurnCounter = 0;
            ApplyDamageIncrease();

            // 슬라이더 꽉 찬 뒤 0으로 리셋 (짧은 딜레이)
            if (guardAtkSlider != null)
            {
                guardAtkSlider.DOKill();
                guardAtkSlider.value = 1f;
                guardAtkSlider.DOValue(0f, 0.3f).SetEase(Ease.InQuad).SetDelay(0.15f);
            }
        }
    }

    void StartGuardColorAnimation()
    {
        if (bossImageArea == null) return;
        StopGuardColorAnimation();

        Material mat = new Material(Shader.Find("UI/Default"));
        mat.SetColor("_Color", guardColorA);
        bossImageArea.material = mat;

        Color currentA = guardColorA;
        guardColorSequence = DOTween.Sequence();
        guardColorSequence.Append(
            DOTween.To(() => currentA, x => {
                currentA = x;
                if (bossImageArea != null && bossImageArea.material != null)
                    bossImageArea.material.SetColor("_Color", x);
            }, guardColorB, guardColorSpeed).SetEase(Ease.InOutSine)
        );
        guardColorSequence.Append(
            DOTween.To(() => currentA, x => {
                currentA = x;
                if (bossImageArea != null && bossImageArea.material != null)
                    bossImageArea.material.SetColor("_Color", x);
            }, guardColorA, guardColorSpeed).SetEase(Ease.InOutSine)
        );
        guardColorSequence.SetLoops(-1, LoopType.Restart);

        StartHPBarGlowAnimation();
    }

    void StopGuardColorAnimation()
    {
        if (guardColorSequence != null) { guardColorSequence.Kill(); guardColorSequence = null; }
    }

    void StartHPBarGlowAnimation()
    {
        StopHPBarGlowAnimation();
        if (hpSlider == null) return;
        Image fillImage = hpSlider.fillRect?.GetComponent<Image>();
        if (fillImage == null) return;

        fillImage.color = guardHPBarColorA;

        hpBarGlowSequence = DOTween.Sequence();
        hpBarGlowSequence.Append(fillImage.DOColor(guardHPBarColorB, guardHPBarColorSpeed).SetEase(Ease.InOutSine));
        hpBarGlowSequence.Append(fillImage.DOColor(guardHPBarColorA, guardHPBarColorSpeed).SetEase(Ease.InOutSine));
        hpBarGlowSequence.SetLoops(-1, LoopType.Restart);
    }

    void StopHPBarGlowAnimation()
    {
        if (hpBarGlowSequence != null) { hpBarGlowSequence.Kill(); hpBarGlowSequence = null; }
    }

    void SetHPBarClearColor()
    {
        if (hpSlider == null) return;
        Image fillImage = hpSlider.fillRect?.GetComponent<Image>();
        if (fillImage != null)
        {
            fillImage.DOKill();
            fillImage.color = clearHPBarColor;
        }
    }

    public void ExitGuardMode()
    {
        if (!isGuardMode) return;
        isGuardMode = false;
        isClearMode = true;
        StopGuardColorAnimation();

        // Guard 해제 후 bossImageArea 색상
        if (bossImageArea != null)
        {
            if (bossImageArea.material == null)
                bossImageArea.material = new Material(Shader.Find("UI/Default"));
            bossImageArea.material.DOKill();
            DOTween.To(
                () => bossImageArea.material.GetColor("_Color"),
                x => { if (bossImageArea != null && bossImageArea.material != null) bossImageArea.material.SetColor("_Color", x); },
                clearEnemyColor, 0.4f
            ).SetEase(Ease.InOutSine);
        }

        StopHPBarGlowAnimation();
        SetHPBarClearColor();
        HideGuardAtkSlider();

        maxHP = 2147483647;
        currentHP = maxHP;

        if (bossPanelGroundImage != null)
            bossPanelGroundImage.DOColor(clearModeGroundColor, 0.5f).SetEase(Ease.InOutQuad);

        UpdateUI(true);
        if (gameManager != null) gameManager.UpdateTurnUI();
        Debug.Log("🏆 Guard 해제! Clear 모드 진입!");
    }

    // 기존 IncreaseInfiniteBossDamage는 ProcessGuardAtkTurn 으로 대체
    // 하위 호환용
    public void IncreaseInfiniteBossDamage()
    {
        // 이제 ProcessGuardAtkTurn()에서 처리
    }

    private void ApplyDamageIncrease()
    {
        if (isClearMode) return;
        int currentTotal = currentBossDamage + infiniteBossExtraDamage;
        if (currentTotal >= bossAtkMaxTotal)
        {
            if (isGuardMode) ExitGuardMode();
            return;
        }
        infiniteBossExtraDamage++;
        Debug.Log($"⚠️ Guard ATK 증가! {GetEffectiveDamage()}/{bossAtkMaxTotal}");
        UpdateBossAttackUI();
        FlashAttackTextOrange();
        if (bossBattleSystem != null && bossBattleSystem.LowHealthVignette != null)
            bossBattleSystem.LowHealthVignette.SetEnemyAtk(GetEffectiveDamage());
        if (currentBossDamage + infiniteBossExtraDamage >= bossAtkMaxTotal && isGuardMode)
            ExitGuardMode();
    }

    public void ProcessPendingDamageIncrease()
    {
        if (pendingDamageIncrease)
        {
            pendingDamageIncrease = false;
            ApplyDamageIncrease();
        }
    }

    void FlashAttackTextOrange()
    {
        if (bossAttackInfoText == null) return;
        Color flashColor = new Color(1f, 0.6f, 0.1f);
        Color originalColor = bossAttackInfoText.color;
        bossAttackInfoText.color = flashColor;
        if (atkIconImage != null) atkIconImage.color = flashColor;
        Sequence seq = DOTween.Sequence();
        seq.AppendInterval(0.3f);
        seq.Append(bossAttackInfoText.DOColor(originalColor, 0.4f).SetEase(Ease.OutQuad));
        if (atkIconImage != null)
            seq.Join(atkIconImage.DOColor(originalColor, 0.4f).SetEase(Ease.OutQuad));
    }

    private int GetEffectiveDamage()
    {
        return Mathf.Min(currentBossDamage + infiniteBossExtraDamage, bossAtkMaxTotal);
    }

    public void TakeDamage(long damage)
    {
        if (isTransitioning) return;
        if (isGuardMode)
        {
            Debug.Log("🛡️ Guard 모드! 데미지 무효!");
            return;
        }

        int damageInt = (int)Mathf.Min(damage, int.MaxValue);
        currentHP -= damageInt;

        if (bossImageArea != null)
        {
            Vector3 preShakePos = bossImageArea.transform.localPosition;
            bossImageArea.transform.DOShakePosition(0.2f, strength: 10f, vibrato: 20, randomness: 90f)
                .OnComplete(() => { if (bossImageArea != null) bossImageArea.transform.localPosition = preShakePos; });
            StartCoroutine(FlashBossWhite());
        }

        if (currentHP <= 0)
        {
            currentHP = 0;
            StartCoroutine(OnBossDefeatedCoroutine());
        }

        Debug.Log($"Boss took {damage} damage! HP: {currentHP}/{maxHP}");
        UpdateUI(false);
    }

    public void AddTurns(int turns)
    {
        if (isTransitioning) return;
        bonusTurnsAdded += turns;
        bonusTurnsTotal += turns;
        bonusTurnsConsumed = 0;
        UpdateBossAttackUI();
    }

    public void PlayBonusTurnEffect()
    {
        UpdateBossAttackUI();
    }

    public void OnPlayerTurn()
    {
        if (isTransitioning) return;

        // 해금 전: 적 공격 안함
        if (unlockManager != null && !unlockManager.CanEnemyAttack()) return;

        // #8: Guard ATK 턴은 freeze 중에도 진행
        if (isGuardMode && !isClearMode)
            ProcessGuardAtkTurn();

        if (isFrozen) return;

        if (currentTurnCount <= 0 && bonusTurnsAdded > 0)
        {
            bonusTurnsAdded--;
            bonusTurnsConsumed++;
            UpdateBossAttackUI();

            if (bonusTurnsAdded <= 0)
            {
                if (gameManager != null) gameManager.SetBossAttacking(true);
                StartCoroutine(AttackAfterBonusTurnsConsumed());
            }
            return;
        }

        currentTurnCount--;

        if (currentTurnCount <= 0 && bonusTurnsAdded <= 0)
        {
            AttackPlayer();
            return;
        }

        UpdateBossAttackUI();
    }

    IEnumerator AttackAfterBonusTurnsConsumed()
    {
        yield return new WaitForSeconds(0.3f);
        bonusTurnsConsumed = 0;
        bonusTurnsTotal = 0;
        UpdateBossAttackUI();
        AttackPlayer();
    }

    private void AttackPlayer()
    {
        StartCoroutine(AttackPlayerCoroutine());
    }

    private IEnumerator AttackPlayerCoroutine()
    {
        if (gameManager != null) gameManager.SetBossAttacking(true);

        if (bossAttackInfoText != null)
        {
            bossAttackInfoText.text = GetAttackTurnText(0);
            StopAttackInfoColorLoop();
            if (attackBlinkAnimation != null) { attackBlinkAnimation.Kill(); attackBlinkAnimation = null; }
            attackBlinkAnimation = DOTween.Sequence();
            attackBlinkAnimation.Append(bossAttackInfoText.DOColor(Color.red, 0.4f));
            if (atkIconImage != null)
                attackBlinkAnimation.Join(atkIconImage.DOColor(Color.red, 0.4f));
            attackBlinkAnimation.Append(bossAttackInfoText.DOColor(Color.white, 0.4f));
            if (atkIconImage != null)
                attackBlinkAnimation.Join(atkIconImage.DOColor(Color.white, 0.4f));
            attackBlinkAnimation.SetLoops(-1, LoopType.Restart);
        }

        yield return new WaitForSeconds(0.15f);

        if (bossImageArea != null)
        {
            Vector3 originalPos = bossImageArea.transform.localPosition;

            float rushDistance = 400f;
            if (playerHPSystem != null && playerHPSystem.HeatText != null)
            {
                Vector3 hpBarWorldPos = playerHPSystem.HeatText.transform.position;
                Vector3 hpBarLocalPos = bossImageArea.transform.parent.InverseTransformPoint(hpBarWorldPos);
                rushDistance = Mathf.Abs(originalPos.y - hpBarLocalPos.y);
            }

            yield return bossImageArea.transform.DOLocalMoveY(originalPos.y - rushDistance, attackMotionDuration * 0.35f)
                .SetEase(Ease.InQuad).WaitForCompletion();

            if (gameManager != null)
            {
                gameManager.TakeBossAttack(GetEffectiveDamage());
                CameraShake.Instance?.ShakeMedium();
            }

            yield return bossImageArea.transform.DOLocalMoveY(originalPos.y, attackMotionDuration * 0.65f)
                .SetEase(Ease.OutBack).WaitForCompletion();

            bossImageArea.transform.localPosition = originalPos;
            // 공격 후 원래색 복원
            RestoreEnemyColorAfterAttack();
        }
        else
        {
            yield return new WaitForSeconds(attackMotionDuration);
            if (gameManager != null)
            {
                gameManager.TakeBossAttack(GetEffectiveDamage());
                CameraShake.Instance?.ShakeMedium();
            }
            RestoreEnemyColorAfterAttack();
        }

        if (gameManager != null) gameManager.SetBossAttacking(false);

        if (attackBlinkAnimation != null) { attackBlinkAnimation.Kill(); attackBlinkAnimation = null; }
        // 공격 깜박임 후 중간색 잔류 방지
        if (bossAttackInfoText != null) bossAttackInfoText.DOKill();
        if (atkIconImage != null) atkIconImage.DOKill();

        currentTurnCount = currentTurnInterval;
        bonusTurnsAdded = 0;
        bonusTurnsConsumed = 0;
        bonusTurnsTotal = 0;
        UpdateBossAttackUI();
        ProcessPendingDamageIncrease();
    }

    public void ResetTurnCount()
    {
        currentTurnCount = currentTurnInterval;
        UpdateBossAttackUI();
    }

    public void ResetBonusTurns()
    {
        bonusTurnsAdded = 0;
        bonusTurnsConsumed = 0;
        bonusTurnsTotal = 0;
        currentTurnCount = currentTurnInterval;
        UpdateBossAttackUI();
    }

    void UpdateUI(bool instant = false)
    {
        if (hpSlider != null)
        {
            float targetValue = (float)currentHP / (float)maxHP;
            hpSlider.DOKill();
            if (instant) hpSlider.value = targetValue;
            else hpSlider.DOValue(targetValue, animationDuration).SetEase(Ease.OutCubic);
        }

        if (hpText != null)
        {
            if (isGuardMode) hpText.text = "HP : Guard";
            else hpText.text = $"HP : {currentHP:N0} / {maxHP:N0}";
        }

        UpdateBossAttackUI();
    }

    string GetAttackTurnText(int remainingTurns)
    {
        string filledSymbol = "●";
        string emptySymbol = "○";

        int totalTurns = currentTurnInterval;
        int filledCount = totalTurns - remainingTurns;

        string symbols = "";
        for (int i = 0; i < filledCount; i++) symbols += filledSymbol;
        for (int i = filledCount; i < totalTurns; i++) symbols += emptySymbol;

        int totalBonus = bonusTurnsConsumed + bonusTurnsAdded;
        if (totalBonus > 0)
        {
            for (int i = 0; i < bonusTurnsConsumed; i++) symbols += "■";
            for (int i = 0; i < bonusTurnsAdded; i++) symbols += "□";
        }

        return $"{GetEffectiveDamage():N0}    {symbols}";
    }

    void UpdateBossAttackUI()
    {
        if (bossAttackInfoText == null) return;

        // 3 stage 미만: 공격 UI 숨김
        if (unlockManager != null && !unlockManager.IsEnemyAttackUnlocked)
        {
            bossAttackInfoText.gameObject.SetActive(false);
            StopAttackInfoColorLoop();
            return;
        }

        // 보스 전환 중이면 UI 숨김 유지
        if (isTransitioning)
        {
            StopAttackInfoColorLoop();
            return;
        }

        bossAttackInfoText.gameObject.SetActive(true);

        if (isFrozen)
        {
            StopAttackInfoColorLoop();
            bossAttackInfoText.color = ICE_BLUE;
            SyncAtkIconColor(ICE_BLUE);
        }
        else if (currentTurnCount <= 1)
        {
            StopAttackInfoColorLoop();
            Color redColor = new Color(1f, 0.2f, 0.2f);
            bossAttackInfoText.color = redColor;
            SyncAtkIconColor(redColor);
            // bossImageArea도 경고색으로 부드럽게 변경
            FlashEnemyWarningColor();
        }
        else
        {
            // 색상 루프 시작 (반복)
            StartAttackInfoColorLoop();
        }
        bossAttackInfoText.text = GetAttackTurnText(currentTurnCount);
    }

    // 1턴 임박: bossImageArea를 경고색으로 변경 (즉시 적용)
    void FlashEnemyWarningColor()
    {
        if (bossImageArea == null || bossImageArea.material == null) return;
        if (isGuardMode) return; // Guard는 자체 루프 유지
        bossImageArea.material.DOKill();
        bossImageArea.material.SetColor("_Color", attackWarningColor);
    }

    // 공격 후 bossImageArea 원래색 복원
    void RestoreEnemyColorAfterAttack()
    {
        if (bossImageArea == null || bossImageArea.material == null) return;
        if (isFrozen) return; // Freeze 중이면 복원 대신 Freeze색 적용
        if (isGuardMode) return; // Guard는 자체 루프 유지
        Color targetColor = isClearMode || (bossLevel >= 41 && !isGuardMode)
            ? infiniteEnemyColor
            : new Color(1.0f, 0.75f, 0.5f, 1.0f);
        bossImageArea.material.DOKill();
        DOTween.To(
            () => bossImageArea.material.GetColor("_Color"),
            x => { if (bossImageArea != null && bossImageArea.material != null) bossImageArea.material.SetColor("_Color", x); },
            targetColor, attackWarningFadeDuration
        ).SetEase(Ease.InOutSine);
    }

    void StartAttackInfoColorLoop()
    {
        if (attackInfoColorLoop != null) return; // 이미 실행 중
        if (bossAttackInfoText == null) return;
        bossAttackInfoText.color = attackInfoColorA;
        SyncAtkIconColor(attackInfoColorA);
        attackInfoColorLoop = DOTween.Sequence();
        attackInfoColorLoop.Append(bossAttackInfoText.DOColor(attackInfoColorB, attackInfoColorSpeed).SetEase(Ease.InOutSine));
        if (atkIconImage != null)
            attackInfoColorLoop.Join(atkIconImage.DOColor(attackInfoColorB, attackInfoColorSpeed).SetEase(Ease.InOutSine));
        attackInfoColorLoop.Append(bossAttackInfoText.DOColor(attackInfoColorA, attackInfoColorSpeed).SetEase(Ease.InOutSine));
        if (atkIconImage != null)
            attackInfoColorLoop.Join(atkIconImage.DOColor(attackInfoColorA, attackInfoColorSpeed).SetEase(Ease.InOutSine));
        attackInfoColorLoop.SetLoops(-1, LoopType.Restart);
    }

    void StopAttackInfoColorLoop()
    {
        if (attackInfoColorLoop != null) { attackInfoColorLoop.Kill(); attackInfoColorLoop = null; }
        // Kill 후 중간 색상 잔류 방지: 텍스트와 아이콘 모두 DOKill
        if (bossAttackInfoText != null) bossAttackInfoText.DOKill();
        if (atkIconImage != null) atkIconImage.DOKill();
    }

    void SyncAtkIconColor(Color c)
    {
        if (atkIconImage != null)
        {
            atkIconImage.DOKill();
            atkIconImage.color = c;
        }
        // 텍스트도 확실히 동기화
        if (bossAttackInfoText != null)
        {
            bossAttackInfoText.DOKill();
            bossAttackInfoText.color = c;
        }
    }

    IEnumerator OnBossDefeatedCoroutine()
    {
        isTransitioning = true;

        bool shouldShowClear = (bossLevel == 40 && isClearMode && !isGuardMode);

        if (gameManager != null)
        {
            gameManager.OnBossDefeated();
            gameManager.SetBossTransitioning(true);
        }

        if (shouldShowClear && bossBattleSystem != null)
            StartCoroutine(ShowClearUIDelayed());

        SetBossUIActive(false);
        StopBossIdleAnimation();

        if (bossImageArea != null)
        {
            Sequence fadeSeq = DOTween.Sequence();
            fadeSeq.Append(bossImageArea.DOFade(0f, 0.5f).SetEase(Ease.InQuad));
            fadeSeq.Join(bossImageArea.transform.DOScale(0.8f, 0.5f).SetEase(Ease.InBack));
            yield return fadeSeq.WaitForCompletion();
        }

        if (playerHPSystem != null)
        {
            while (playerHPSystem.IsLevelUpAnimating)
                yield return null;
        }

        yield return new WaitForSeconds(bossSpawnDelay);
        bossLevel++;

        // 해금 체크
        if (unlockManager != null) unlockManager.OnStageChanged(bossLevel);

        if (isClearMode)
            SetupClearModeBoss();
        else
        {
            SelectNextBossImage();
            ApplyEnemyStats(bossLevel);
            currentHP = maxHP;

            if (bossLevel >= 40 && !isClearMode)
            {
                isGuardMode = true;
                StartGuardColorAnimation();
                ShowGuardAtkSlider();
            }

            if (bossLevel >= 41 && !isClearMode && !isGuardMode)
            {
                currentBossDamage = clearModeFixedAtk;
                infiniteBossExtraDamage = 0;
                ApplyRedColor();
            }
        }

        infiniteBossExtraDamage = 0;
        currentTurnCount = currentTurnInterval;
        bonusTurnsAdded = 0;
        bonusTurnsConsumed = 0;
        bonusTurnsTotal = 0;
        guardAtkTurnCounter = 0;

        if (bossBattleSystem != null && bossBattleSystem.LowHealthVignette != null)
            bossBattleSystem.LowHealthVignette.SetEnemyAtk(GetEffectiveDamage());

        if (bossImageArea != null)
        {
            bossImageArea.color = new Color(1f, 1f, 1f, 0f);
            bossImageArea.transform.localScale = Vector3.one * 1.2f;
            Sequence appearSeq = DOTween.Sequence();
            appearSeq.Append(bossImageArea.DOFade(1f, 0.5f).SetEase(Ease.OutQuad));
            appearSeq.Join(bossImageArea.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack));
            yield return appearSeq.WaitForCompletion();
        }

        isTransitioning = false; // UI 갱신 전에 해제해야 UpdateBossAttackUI가 정상 동작

        // Freeze 유지 중이면 새 보스도 즉시 파란색으로
        if (isFrozen && !isGuardMode && bossImageArea != null && bossImageArea.material != null)
        {
            bossImageArea.material.DOKill();
            bossImageArea.material.SetColor("_Color", freezeEnemyColor);
        }

        UpdateUI(true);
        SetBossUIActive(true);
        UpdateBossAttackUI();
        UpdateStageBackgroundColor();

        StartBossIdleAnimation();

        if (gameManager != null)
        {
            gameManager.SetBossTransitioning(false);
            gameManager.UpdateTurnUI();
        }

        // Continue 텍스트 갱신 (9 stage 해금 시)
        if (gunSystem != null) gunSystem.UpdateContinueGuideText();
    }

    void SetupClearModeBoss()
    {
        if (stage39SpriteIndex >= 0 && stage39SpriteIndex < bossSprites.Count)
            bossImageArea.sprite = bossSprites[stage39SpriteIndex];

        ApplyRedColor();
        SetHPBarClearColor();
        HideGuardAtkSlider();
        maxHP = 2147483647;
        currentHP = maxHP;
        currentTurnInterval = Mathf.Max(minTurnInterval, baseTurnInterval - Mathf.FloorToInt(38 * 0.2f));

        currentBossDamage = clearModeFixedAtk;
        infiniteBossExtraDamage = 0;

        if (bossBattleSystem != null && bossBattleSystem.LowHealthVignette != null)
            bossBattleSystem.LowHealthVignette.SetEnemyAtk(GetEffectiveDamage());
    }

    public void ResetBoss()
    {
        isFirstGame = false;
        bossLevel = 1;
        currentBossIndex = 0;
        isFrozen = false;
        bonusTurnsAdded = 0;
        bonusTurnsConsumed = 0;
        bonusTurnsTotal = 0;
        infiniteBossExtraDamage = 0;
        isGuardMode = false;
        isClearMode = false;
        stage39SpriteIndex = -1;
        guardAtkTurnCounter = 0;
        StopGuardColorAnimation();
        StopHPBarGlowAnimation();
        StopAttackInfoColorLoop();
        HideGuardAtkSlider();

        if (bossPanelGroundImage != null && groundColorSaved)
        {
            bossPanelGroundImage.DOKill();
            bossPanelGroundImage.color = originalGroundColor;
        }

        if (bossImageArea != null && bossSprites.Count > 0)
        {
            bossImageArea.sprite = bossSprites[0];
            bossImageArea.color = Color.white;
            bossImageArea.material = null;
            bossImageArea.transform.localScale = Vector3.one;
        }

        if (hpSlider != null)
        {
            Image fillImage = hpSlider.fillRect?.GetComponent<Image>();
            if (fillImage != null) fillImage.color = new Color(0.3f, 0.85f, 0.4f);
        }

        InitializeBoss();
        isTransitioning = false;
        StartBossIdleAnimation();
        StartCoroutine(ShowBossUIAfterDelay());
    }

    IEnumerator ShowBossUIAfterDelay()
    {
        yield return new WaitForSeconds(0.1f);
        SetBossUIActive(true);
    }

    void SelectNextBossImage()
    {
        if (bossSprites.Count == 0) return;

        if (bossSprites.Count == 1)
        {
            if (bossImageArea.sprite == null) bossImageArea.sprite = bossSprites[0];
            ApplyOrangeColor();
        }
        else
        {
            int imageIndex = bossLevel == 1 && isFirstGame ? 0 : Mathf.Min(bossLevel - 1, bossSprites.Count - 1);
            currentBossIndex = imageIndex;

            if (bossLevel == 39)
            {
                stage39SpriteIndex = currentBossIndex;
                Debug.Log($"📌 Stage 39 sprite 인덱스 저장: {stage39SpriteIndex}");
            }

            if (currentBossIndex < bossSprites.Count && bossSprites[currentBossIndex] != null)
                bossImageArea.sprite = bossSprites[currentBossIndex];

            if (!isGuardMode) ApplyOrangeColor();
        }
    }

    void ApplyOrangeColor()
    {
        if (bossImageArea == null) return;
        Material mat = new Material(Shader.Find("UI/Default"));
        mat.SetColor("_Color", new Color(1.0f, 0.75f, 0.5f, 1.0f));
        bossImageArea.material = mat;
    }

    void ApplyRedColor()
    {
        if (bossImageArea == null) return;
        Material mat = new Material(Shader.Find("UI/Default"));
        mat.SetColor("_Color", infiniteEnemyColor);
        bossImageArea.material = mat;
    }

    IEnumerator FlashBossWhite()
    {
        if (bossImageArea == null || bossImageArea.material == null) yield break;
        Color originalMatColor = bossImageArea.material.GetColor("_Color");
        bossImageArea.material.SetColor("_Color", Color.white);
        yield return new WaitForSeconds(0.07f);
        if (bossImageArea != null && bossImageArea.material != null)
            bossImageArea.material.SetColor("_Color", originalMatColor);
    }

    public void SetFrozen(bool frozen)
    {
        isFrozen = frozen;

        if (frozen)
        {
            StopBossIdleAnimation();
            StopAttackInfoColorLoop();
            if (attackBlinkAnimation != null) { attackBlinkAnimation.Kill(); attackBlinkAnimation = null; }
            SyncAtkIconColor(ICE_BLUE);

            // Guard Boss(이미 붉은색 루프 중)는 유지, 그 외는 Freeze색 적용
            if (!isGuardMode && bossImageArea != null && bossImageArea.material != null)
            {
                if (freezeEnemyColorAnim != null) { freezeEnemyColorAnim.Kill(); freezeEnemyColorAnim = null; }
                bossImageArea.material.DOKill();
                DOTween.To(
                    () => bossImageArea.material.GetColor("_Color"),
                    x => { if (bossImageArea != null && bossImageArea.material != null) bossImageArea.material.SetColor("_Color", x); },
                    freezeEnemyColor, 0.5f
                ).SetEase(Ease.InOutSine);
            }
        }
        else
        {
            // Freeze 해제: 원래 색상으로 복원 — 단, 1턴 임박면 경고색 우선
            if (!isGuardMode && bossImageArea != null && bossImageArea.material != null)
            {
                bossImageArea.material.DOKill();
                if (currentTurnCount <= 1)
                {
                    // 1턴 임박: 경고색 즉시
                    bossImageArea.material.SetColor("_Color", attackWarningColor);
                }
                else
                {
                    Color restoreColor = (isClearMode || bossLevel >= 41)
                        ? infiniteEnemyColor
                        : new Color(1.0f, 0.75f, 0.5f, 1.0f);
                    DOTween.To(
                        () => bossImageArea.material.GetColor("_Color"),
                        x => { if (bossImageArea != null && bossImageArea.material != null) bossImageArea.material.SetColor("_Color", x); },
                        restoreColor, 0.5f
                    ).SetEase(Ease.InOutSine);
                }
            }
            if (!isTransitioning)
                StartBossIdleAnimation();
            UpdateBossAttackUI();
        }
    }

    void StartBossIdleAnimation()
    {
        if (bossImageArea == null) return;
        if (bossIdleAnimation != null) bossIdleAnimation.Kill();
        if (isFrozen) return;
        bossIdleAnimation = bossImageArea.transform.DOLocalRotate(new Vector3(0f, 0f, 5f), 2.0f)
            .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
    }

    void StopBossIdleAnimation()
    {
        if (bossIdleAnimation != null) { bossIdleAnimation.Kill(); bossIdleAnimation = null; }
        if (bossImageArea != null) bossImageArea.transform.localRotation = Quaternion.identity;
    }

    void SetBossUIActive(bool active)
    {
        if (hpSlider != null) hpSlider.gameObject.SetActive(active);
        if (hpText != null) hpText.gameObject.SetActive(active);
        if (bossAttackInfoText != null) bossAttackInfoText.gameObject.SetActive(active);
    }

    IEnumerator ShowClearUIDelayed()
    {
        yield return new WaitForSeconds(2.0f);
        if (bossBattleSystem != null)
            bossBattleSystem.ShowChallengeClearUI();
    }

    // === 스테이지 배경색 (SerializeField 색상 사용) ===
    void UpdateStageBackgroundColor()
    {
        if (bossPanelGroundImage == null) return;
        if (isGuardMode || isClearMode) return;

        Color targetColor;
        if (bossLevel <= 10)
            targetColor = stageColor_1_10;
        else if (bossLevel <= 20)
            targetColor = stageColor_11_20;
        else if (bossLevel <= 30)
            targetColor = stageColor_21_30;
        else
            targetColor = stageColor_31_40;

        bossPanelGroundImage.DOKill();
        bossPanelGroundImage.DOColor(targetColor, 0.5f).SetEase(Ease.InOutQuad);
    }

    // === EnemyData 기반 스탯 적용 ===
    void ApplyEnemyStats(int level)
    {
        if (enemyData != null)
        {
            var data = enemyData.GetStageData(level);
            maxHP = data.hp;
            currentBossDamage = data.atk;
            currentTurnInterval = Mathf.Max(minTurnInterval, data.turnInterval);
        }
        else
        {
            // Fallback: EnemyData 미할당 시 기본값
            float exponent = Mathf.Pow(1.5f, level - 1);
            maxHP = baseHP + Mathf.RoundToInt(hpIncreasePerLevel * (exponent - 1f) / 0.5f);
            currentBossDamage = baseDamage + ((level - 1) / atkGrowthInterval) * atkGrowthPerStep;
            if (level >= 40) maxHP = 2147483647;
        }
    }

    public bool IsFrozen() { return isFrozen; }
    public int GetCurrentHP() { return currentHP; }
    public int GetMaxHP() { return maxHP; }
    public int GetBossLevel() { return bossLevel; }
    public int GetTurnCount() { return currentTurnCount; }
    public int GetTurnInterval() { return currentTurnInterval; }
    public int GetBossDamage() { return GetEffectiveDamage(); }
    public bool IsInfiniteBoss() { return bossLevel >= 40; }
    public bool IsGuardMode() { return isGuardMode; }
    public bool IsClearMode() { return isClearMode; }
}
