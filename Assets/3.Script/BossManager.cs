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

    [Header("⭐ v6.4: Enemy ATK 성장 설정")]
    [SerializeField] private int baseDamage = 28;
    [SerializeField] private int atkGrowthPerStep = 3;        // 일반 몬스터 고정 성장치
    [SerializeField] private int atkGrowthInterval = 2;       // 몇 Challenge마다 오르는지
    [SerializeField] private int bossAtkMaxTotal = 90;        // boss 공격력 한계치 (move 점진적 증가 포함)
    [SerializeField] private int clearModeFixedAtk = 60;      // Clear 이후 적 공격력 고정치

    private int currentTurnInterval;
    private int currentTurnCount = 0;
    private int currentBossDamage;

    [Header("Boss Progression")]
    public int bossLevel = 1;

    [Header("HP Bar Animation")]
    public float animationDuration = 0.3f;
    public float bossSpawnDelay = 1.0f;

    [Header("Boss Attack Animation")]
    [SerializeField] private float attackMotionDuration = 0.3f;

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
    private int bonusTurnsConsumed = 0; // □→■ 차오른 수
    private int bonusTurnsTotal = 0;    // 총 보너스턴 수 (표시용)

    private Color originalAttackInfoColor = Color.white;
    private bool attackInfoColorSaved = false;
    private static readonly Color ICE_BLUE = new Color(0.5f, 0.8f, 1f);

    private int infiniteBossExtraDamage = 0;
    private bool pendingDamageIncrease = false;

    // Guard 모드
    private bool isGuardMode = false;
    private Sequence guardColorSequence;

    // Clear 모드
    private bool isClearMode = false;
    private int stage39SpriteIndex = -1;
    private Color originalGroundColor;
    private bool groundColorSaved = false;

    // ⭐ v6.3: Guard 해제 후 HP bar 빛나는 효과
    private Sequence hpBarGlowSequence;
    private BossBattleSystem bossBattleSystem;
    private PlayerHPSystem playerHPSystem;

    void Start()
    {
        gameManager = FindAnyObjectByType<GameManager>();
        bossBattleSystem = FindAnyObjectByType<BossBattleSystem>();
        playerHPSystem = FindAnyObjectByType<PlayerHPSystem>();

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
        float exponent = Mathf.Pow(1.5f, bossLevel - 1);
        maxHP = baseHP + Mathf.RoundToInt(hpIncreasePerLevel * (exponent - 1f) / 0.5f);

        if (bossLevel == 39) maxHP = 2147483647;
        else if (bossLevel >= 40) maxHP = 2147483647;

        currentHP = maxHP;

        currentTurnInterval = Mathf.Max(minTurnInterval, baseTurnInterval - Mathf.FloorToInt((bossLevel - 1) * 0.2f));

        // ⭐ v6.4: ATK 성장
        currentBossDamage = baseDamage + ((bossLevel - 1) / atkGrowthInterval) * atkGrowthPerStep;

        infiniteBossExtraDamage = 0;
        currentTurnCount = currentTurnInterval;

        if (bossLevel >= 40 && !isClearMode)
        {
            isGuardMode = true;
            StartGuardColorAnimation();
        }

        // Clear 모드: 고정 공격력
        if (isClearMode)
        {
            currentBossDamage = clearModeFixedAtk;
            infiniteBossExtraDamage = 0;
        }
        else if (bossLevel >= 41 && !isGuardMode)
        {
            currentBossDamage = clearModeFixedAtk;
            infiniteBossExtraDamage = 0;
            ApplyDarkGrayColor();
        }

        // ⭐ v6.4: 비네트에 적 ATK 전달
        if (bossBattleSystem != null && bossBattleSystem.LowHealthVignette != null)
            bossBattleSystem.LowHealthVignette.SetEnemyAtk(GetEffectiveDamage());

        UpdateUI(true);
        Debug.Log($"Boss Level {bossLevel} spawned! HP: {currentHP}/{maxHP}, ATK: {GetEffectiveDamage()}, Guard: {isGuardMode}, Clear: {isClearMode}");
    }

    void StartGuardColorAnimation()
    {
        if (bossImageArea == null) return;
        StopGuardColorAnimation();

        // ⭐ v6.4: Guard 색상 주황↔붉은색 (푸른색 제거)
        Color pastelRedColor = new Color(0.9f, 0.2f, 0.15f, 1.0f);
        Color pastelOrangeColor = new Color(1.0f, 0.75f, 0.5f, 1.0f);

        Material mat = new Material(Shader.Find("UI/Default"));
        mat.SetColor("_Color", pastelOrangeColor);
        bossImageArea.material = mat;

        guardColorSequence = DOTween.Sequence();
        guardColorSequence.Append(
            DOTween.To(() => pastelOrangeColor, x => {
                if (bossImageArea != null && bossImageArea.material != null)
                    bossImageArea.material.SetColor("_Color", x);
            }, pastelRedColor, 1.0f).SetEase(Ease.InOutSine)
        );
        guardColorSequence.Append(
            DOTween.To(() => pastelRedColor, x => {
                if (bossImageArea != null && bossImageArea.material != null)
                    bossImageArea.material.SetColor("_Color", x);
            }, pastelOrangeColor, 1.0f).SetEase(Ease.InOutSine)
        );
        guardColorSequence.SetLoops(-1, LoopType.Restart);

        // ⭐ v6.4: Guard 상태에서 HP bar glow 시작
        StartHPBarGlowAnimation();
    }

    void StopGuardColorAnimation()
    {
        if (guardColorSequence != null) { guardColorSequence.Kill(); guardColorSequence = null; }
    }

    // ⭐ v6.3: Guard 해제 후 HP bar 주황↔붉은 빛나는 루프
    void StartHPBarGlowAnimation()
    {
        StopHPBarGlowAnimation();
        if (hpSlider == null) return;
        Image fillImage = hpSlider.fillRect?.GetComponent<Image>();
        if (fillImage == null) return;

        Color orangeColor = new Color(1f, 0.6f, 0.15f);
        Color redColor = new Color(0.9f, 0.2f, 0.15f);
        fillImage.color = orangeColor;

        hpBarGlowSequence = DOTween.Sequence();
        hpBarGlowSequence.Append(fillImage.DOColor(redColor, 1.5f).SetEase(Ease.InOutSine));
        hpBarGlowSequence.Append(fillImage.DOColor(orangeColor, 1.5f).SetEase(Ease.InOutSine));
        hpBarGlowSequence.SetLoops(-1, LoopType.Restart);
    }

    void StopHPBarGlowAnimation()
    {
        if (hpBarGlowSequence != null) { hpBarGlowSequence.Kill(); hpBarGlowSequence = null; }
    }

    // ⭐ v6.4: HP bar 붉은색 고정 (Clear 모드 적 공통)
    void SetHPBarRedFixed()
    {
        if (hpSlider == null) return;
        Image fillImage = hpSlider.fillRect?.GetComponent<Image>();
        if (fillImage != null)
        {
            fillImage.DOKill();
            fillImage.color = new Color(0.9f, 0.2f, 0.15f);
        }
    }

    public void ExitGuardMode()
    {
        if (!isGuardMode) return;
        isGuardMode = false;
        isClearMode = true;
        StopGuardColorAnimation();
        ApplyOrangeColor();

        // ⭐ v6.4: Guard 해제 → glow 종료 + HP bar 붉은색 고정
        StopHPBarGlowAnimation();
        SetHPBarRedFixed();

        maxHP = 2147483647;
        currentHP = maxHP;

        if (bossPanelGroundImage != null)
            bossPanelGroundImage.DOColor(new Color(0.2f, 0.15f, 0.3f, 1f), 0.5f).SetEase(Ease.InOutQuad);

        UpdateUI(true);
        if (gameManager != null) gameManager.UpdateTurnUI();
        Debug.Log("🏆 Guard 해제! Clear 모드 진입!");
    }

    public void IncreaseInfiniteBossDamage()
    {
        if (bossLevel < 40) return;
        // ⭐ v6.4: Clear 모드(41+) 에서는 ATK 증가 비활성화
        if (isClearMode) return;
        int currentTotal = currentBossDamage + infiniteBossExtraDamage;
        if (currentTotal >= bossAtkMaxTotal)
        {
            if (isGuardMode) ExitGuardMode();
            return;
        }
        if (gameManager != null && gameManager.IsBossAttacking())
        {
            pendingDamageIncrease = true;
            return;
        }
        ApplyDamageIncrease();
    }

    private void ApplyDamageIncrease()
    {
        int currentTotal = currentBossDamage + infiniteBossExtraDamage;
        if (currentTotal >= bossAtkMaxTotal)
        {
            if (isGuardMode) ExitGuardMode();
            return;
        }
        infiniteBossExtraDamage++;
        Debug.Log($"⚠️ 무한 보스 ATK 증가! {GetEffectiveDamage()}/{bossAtkMaxTotal}");
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

    // ⭐ v6.3: 주황색 플래시 (기존 파란색→주황색)
    void FlashAttackTextOrange()
    {
        if (bossAttackInfoText == null) return;
        Color originalColor = bossAttackInfoText.color;
        bossAttackInfoText.color = new Color(1f, 0.6f, 0.1f);
        DOTween.Sequence()
            .AppendInterval(0.3f)
            .Append(bossAttackInfoText.DOColor(originalColor, 0.4f).SetEase(Ease.OutQuad));
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

    // ⭐ v6.3: □ 보너스턴 추가 시 UI 갱신
    public void PlayBonusTurnEffect()
    {
        UpdateBossAttackUI();
    }

    public void OnPlayerTurn()
    {
        if (isTransitioning) return;
        if (isFrozen) return;

        // 보너스턴 소비 단계 (일반턴 다 찬 후)
        if (currentTurnCount <= 0 && bonusTurnsAdded > 0)
        {
            bonusTurnsAdded--;
            bonusTurnsConsumed++;
            UpdateBossAttackUI();

            if (bonusTurnsAdded <= 0)
            {
                // ⭐ v6.4: 보너스턴 소비 완료 → 즉시 input 차단 후 공격
                if (gameManager != null) gameManager.SetBossAttacking(true);
                StartCoroutine(AttackAfterBonusTurnsConsumed());
            }
            return;
        }

        currentTurnCount--;

        if (currentTurnCount <= 0 && bonusTurnsAdded <= 0)
        {
            // 보너스턴 없으면 즉시 공격
            AttackPlayer();
            return;
        }

        // currentTurnCount가 0이 되었지만 보너스턴이 있으면 → 이번 턴은 일반턴 다 찬 것만 표시
        UpdateBossAttackUI();
    }

    IEnumerator AttackAfterBonusTurnsConsumed()
    {
        // ■■■ 다 차오른 상태 0.3초 표시
        yield return new WaitForSeconds(0.3f);
        // ■■■ 사라짐 (리셋)
        bonusTurnsConsumed = 0;
        bonusTurnsTotal = 0;
        UpdateBossAttackUI();
        // 공격
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
            if (attackBlinkAnimation != null) attackBlinkAnimation.Kill();
            attackBlinkAnimation = DOTween.Sequence()
                .Append(bossAttackInfoText.DOColor(Color.red, 0.4f))
                .Append(bossAttackInfoText.DOColor(Color.white, 0.4f))
                .SetLoops(-1, LoopType.Restart);
        }

        yield return new WaitForSeconds(0.25f);

        if (bossImageArea != null)
        {
            Vector3 originalPos = bossImageArea.transform.localPosition;
            Sequence attackSeq = DOTween.Sequence();
            attackSeq.Append(bossImageArea.transform.DOLocalMoveX(originalPos.x - 50f, attackMotionDuration * 0.25f).SetEase(Ease.OutQuad));
            attackSeq.Append(bossImageArea.transform.DOLocalMoveX(originalPos.x, attackMotionDuration * 0.55f).SetEase(Ease.OutBounce));
            yield return attackSeq.WaitForCompletion();
            // ⭐ v6.4: 위치 강제 복원 (좌측 쓸림 방지)
            bossImageArea.transform.localPosition = originalPos;
        }
        else
            yield return new WaitForSeconds(attackMotionDuration);

        if (gameManager != null)
        {
            gameManager.TakeBossAttack(GetEffectiveDamage());
            CameraShake.Instance?.ShakeMedium();
        }

        if (gameManager != null) gameManager.SetBossAttacking(false);

        if (attackBlinkAnimation != null) { attackBlinkAnimation.Kill(); attackBlinkAnimation = null; }

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
            if (isGuardMode) hpText.text = "HP: Guard";
            else hpText.text = "HP: " + currentHP + " / " + maxHP;
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

        // ⭐ v6.3: 보너스턴 □/■ 표시 (소비된 것은 ■, 남은 것은 □)
        int totalBonus = bonusTurnsConsumed + bonusTurnsAdded;
        if (totalBonus > 0)
        {
            for (int i = 0; i < bonusTurnsConsumed; i++) symbols += "■";
            for (int i = 0; i < bonusTurnsAdded; i++) symbols += "□";
        }

        return $"ATK: {GetEffectiveDamage()}\n{symbols}";
    }

    void UpdateBossAttackUI()
    {
        if (bossAttackInfoText != null)
        {
            if (isFrozen)
                bossAttackInfoText.color = ICE_BLUE;
            else
            {
                if (currentTurnCount <= 1) bossAttackInfoText.color = new Color(1f, 0.2f, 0.2f);
                else if (currentTurnCount <= 3) bossAttackInfoText.color = new Color(1f, 0.8f, 0.2f);
                else bossAttackInfoText.color = new Color(0.7f, 0.7f, 0.7f);
            }
            bossAttackInfoText.text = GetAttackTurnText(currentTurnCount);
        }
    }

    IEnumerator OnBossDefeatedCoroutine()
    {
        isTransitioning = true;

        // ⭐ v6.4: Guard 보스(40) 처치 시 Challenge Clear
        bool shouldShowClear = (bossLevel == 40 && isClearMode && !isGuardMode);

        if (gameManager != null)
        {
            gameManager.OnBossDefeated();
            gameManager.SetBossTransitioning(true);
        }

        if (shouldShowClear && bossBattleSystem != null)
        {
            // 보스 쓰러짐 연출 후 Clear UI 표시
            StartCoroutine(ShowClearUIDelayed());
        }

        SetBossUIActive(false);
        StopBossIdleAnimation();

        if (bossImageArea != null)
        {
            Sequence fadeSeq = DOTween.Sequence();
            fadeSeq.Append(bossImageArea.DOFade(0f, 0.5f).SetEase(Ease.InQuad));
            fadeSeq.Join(bossImageArea.transform.DOScale(0.8f, 0.5f).SetEase(Ease.InBack));
            yield return fadeSeq.WaitForCompletion();
        }

        // ⭐ v6.4: Level UP 룰렛 완료 대기
        if (playerHPSystem != null)
        {
            while (playerHPSystem.IsLevelUpAnimating)
                yield return null;
        }

        yield return new WaitForSeconds(bossSpawnDelay);
        bossLevel++;

        if (isClearMode)
            SetupClearModeBoss();
        else
        {
            SelectNextBossImage();

            float exponent = Mathf.Pow(1.5f, bossLevel - 1);
            maxHP = baseHP + Mathf.RoundToInt(hpIncreasePerLevel * (exponent - 1f) / 0.5f);
            if (bossLevel >= 39) maxHP = 2147483647;
            currentHP = maxHP;

            currentTurnInterval = Mathf.Max(minTurnInterval, baseTurnInterval - Mathf.FloorToInt((bossLevel - 1) * 0.2f));

            currentBossDamage = baseDamage + ((bossLevel - 1) / atkGrowthInterval) * atkGrowthPerStep;

            if (bossLevel >= 40 && !isClearMode)
            {
                isGuardMode = true;
                StartGuardColorAnimation();
            }

            if (bossLevel >= 41 && !isClearMode && !isGuardMode)
            {
                currentBossDamage = clearModeFixedAtk;
                infiniteBossExtraDamage = 0;
                ApplyDarkGrayColor();
            }
        }

        infiniteBossExtraDamage = 0;
        currentTurnCount = currentTurnInterval;
        bonusTurnsAdded = 0;
        bonusTurnsConsumed = 0;
        bonusTurnsTotal = 0;

        if (bossImageArea != null)
        {
            bossImageArea.color = new Color(1f, 1f, 1f, 0f);
            bossImageArea.transform.localScale = Vector3.one * 1.2f;
            Sequence appearSeq = DOTween.Sequence();
            appearSeq.Append(bossImageArea.DOFade(1f, 0.5f).SetEase(Ease.OutQuad));
            appearSeq.Join(bossImageArea.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack));
            yield return appearSeq.WaitForCompletion();
        }

        UpdateUI(true);
        SetBossUIActive(true);
        UpdateBossAttackUI();

        if (!isFrozen) StartBossIdleAnimation();

        if (gameManager != null)
        {
            gameManager.SetBossTransitioning(false);
            gameManager.UpdateTurnUI();
        }

        isTransitioning = false;
    }

    void SetupClearModeBoss()
    {
        if (stage39SpriteIndex >= 0 && stage39SpriteIndex < bossSprites.Count)
            bossImageArea.sprite = bossSprites[stage39SpriteIndex];

        // ⭐ v6.4: Clear 모드 보스는 검회색 + HP bar 붉은색 고정
        ApplyDarkGrayColor();
        SetHPBarRedFixed();
        maxHP = 2147483647;
        currentHP = maxHP;
        currentTurnInterval = Mathf.Max(minTurnInterval, baseTurnInterval - Mathf.FloorToInt(38 * 0.2f));

        // Clear 모드: 고정 공격력
        currentBossDamage = clearModeFixedAtk;
        infiniteBossExtraDamage = 0;
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
        StopGuardColorAnimation();
        StopHPBarGlowAnimation();

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

        // ⭐ v6.3: HP bar 색상 초기화 (흰색이 아닌 원래 색으로)
        if (hpSlider != null)
        {
            Image fillImage = hpSlider.fillRect?.GetComponent<Image>();
            if (fillImage != null) fillImage.color = new Color(0.3f, 0.85f, 0.4f); // 기본 녹색
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

    // ⭐ v6.4: 41번째부터 Enemy 검회색이 아니고 붉은색
    void ApplyDarkGrayColor()
    {
        if (bossImageArea == null) return;
        Material mat = new Material(Shader.Find("UI/Default"));
        mat.SetColor("_Color", new Color(0.9f, 0.2f, 0.15f, 1.0f));
        bossImageArea.material = mat;
    }

    public void SetFrozen(bool frozen)
    {
        isFrozen = frozen;

        if (frozen)
        {
            StopBossIdleAnimation();
            if (bossAttackInfoText != null)
            {
                if (!attackInfoColorSaved)
                {
                    originalAttackInfoColor = bossAttackInfoText.color;
                    attackInfoColorSaved = true;
                }
                bossAttackInfoText.color = ICE_BLUE;
            }
        }
        else
        {
            StartBossIdleAnimation();
            attackInfoColorSaved = false;
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

    // ⭐ v6.3: Challenge Clear UI 지연 표시
    IEnumerator ShowClearUIDelayed()
    {
        yield return new WaitForSeconds(2.0f);
        if (bossBattleSystem != null)
            bossBattleSystem.ShowChallengeClearUI();
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
