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
    [SerializeField] private Image bossPanelGroundImage; // Boss panel의 ground 이미지

    [Header("Boss Stats")]
    public int baseHP = 200;
    public int hpIncreasePerLevel = 200;
    private int maxHP;
    private int currentHP;

    [Header("보스 공격 시스템")]
    [SerializeField] private int baseTurnInterval = 8;
    [SerializeField] private int minTurnInterval = 3;
    [SerializeField] private int baseDamage = 28;
    [SerializeField] private int damageThreshold = 40;

    private int currentTurnInterval;
    private int currentTurnCount = 0;
    private int currentBossDamage;

    [Header("Boss Progression")]
    public int bossLevel = 1;

    [Header("HP Bar Animation")]
    public float animationDuration = 0.3f;
    public float bossSpawnDelay = 1.0f;

    [Header("Boss Attack Animation")]
    [SerializeField] private float attackMotionDuration = 0.5f;

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
    private int bonusTurnsFilled = 0;

    // ⭐ v5.0: 무한대 보스 전용 - 20회 이동마다 공격력 증가
    private int infiniteBossExtraDamage = 0;
    private const int MAX_TOTAL_DAMAGE = 50; // ⭐ v6.0: 70→50
    private bool pendingDamageIncrease = false;

    // ⭐ v6.0: Guard 모드 (Stage 40 무적 상태)
    private bool isGuardMode = false;
    private Sequence guardColorSequence; // Boss Image 색상 순회 DOTween

    // ⭐ v6.0: Clear 모드 (Guard 해제 후)
    private bool isClearMode = false;
    private int stage39SpriteIndex = -1; // stage 39의 sprite 인덱스 저장
    private Color originalGroundColor; // Boss panel ground 원래 색상
    private bool groundColorSaved = false;

    void Start()
    {
        gameManager = FindAnyObjectByType<GameManager>();

        // Boss panel ground 원래 색상 저장
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

        if (bossLevel == 39)
        {
            maxHP = 2147483647;
        }
        else if (bossLevel >= 40)
        {
            maxHP = 2147483647;
        }

        currentHP = maxHP;

        currentTurnInterval = Mathf.Max(minTurnInterval, baseTurnInterval - Mathf.FloorToInt((bossLevel - 1) * 0.2f));

        int tempDamage = baseDamage + (bossLevel - 1);
        
        if (tempDamage <= damageThreshold)
        {
            currentBossDamage = tempDamage;
        }
        else
        {
            int levelsOverThreshold = bossLevel - (damageThreshold - baseDamage + 1);
            int slowIncreaseCount = levelsOverThreshold / 5;
            currentBossDamage = damageThreshold + slowIncreaseCount;
        }

        infiniteBossExtraDamage = 0;
        currentTurnCount = currentTurnInterval;

        // ⭐ v6.0: Guard 모드 설정 (stage 40 진입 시)
        if (bossLevel >= 40 && !isClearMode)
        {
            isGuardMode = true;
            StartGuardColorAnimation();
        }

        UpdateUI(true);
        Debug.Log($"Boss Level {bossLevel} spawned! HP: {currentHP}/{maxHP}, 공격 주기: {currentTurnInterval}턴, 공격력: {currentBossDamage}, Guard: {isGuardMode}, Clear: {isClearMode}");
    }

    // ⭐ v6.0: Guard 모드 - Boss Image 두 색상 DOTween 순회
    void StartGuardColorAnimation()
    {
        if (bossImageArea == null) return;

        StopGuardColorAnimation();

        Color pastelBlueColor = new Color(0.55f, 0.75f, 0.95f, 1.0f);  // 푸른 파스텔톤
        Color pastelOrangeColor = new Color(1.0f, 0.75f, 0.5f, 1.0f);  // 파스텔 오렌지

        Material mat = new Material(Shader.Find("UI/Default"));
        mat.SetColor("_Color", pastelBlueColor);
        bossImageArea.material = mat;

        guardColorSequence = DOTween.Sequence();
        guardColorSequence.Append(
            DOTween.To(() => pastelBlueColor, x => {
                if (bossImageArea != null && bossImageArea.material != null)
                    bossImageArea.material.SetColor("_Color", x);
            }, pastelOrangeColor, 1.0f).SetEase(Ease.InOutSine)
        );
        guardColorSequence.Append(
            DOTween.To(() => pastelOrangeColor, x => {
                if (bossImageArea != null && bossImageArea.material != null)
                    bossImageArea.material.SetColor("_Color", x);
            }, pastelBlueColor, 1.0f).SetEase(Ease.InOutSine)
        );
        guardColorSequence.SetLoops(-1, LoopType.Restart);

        Debug.Log("🛡️ Guard 색상 애니메이션 시작!");
    }

    void StopGuardColorAnimation()
    {
        if (guardColorSequence != null)
        {
            guardColorSequence.Kill();
            guardColorSequence = null;
        }
    }

    // ⭐ v6.0: Guard 해제 → Clear 모드 전환
    public void ExitGuardMode()
    {
        if (!isGuardMode) return;

        isGuardMode = false;
        isClearMode = true;

        StopGuardColorAnimation();

        // Boss Image 파스텔 오렌지 고정
        ApplyOrangeColor();

        // HP를 21억으로 설정 (이제 쓰러뜨릴 수 있음)
        maxHP = 2147483647;
        currentHP = maxHP;

        // Boss panel ground 배경 색상 변경 (어두운 보라 계열)
        if (bossPanelGroundImage != null)
        {
            bossPanelGroundImage.DOColor(new Color(0.2f, 0.15f, 0.3f, 1f), 0.5f).SetEase(Ease.InOutQuad);
        }

        UpdateUI(true);
        
        if (gameManager != null)
        {
            gameManager.UpdateTurnUI();
        }

        Debug.Log("🏆 Guard 해제! Clear 모드 진입! HP: 2,147,483,647");
    }

    public void IncreaseInfiniteBossDamage()
    {
        if (bossLevel < 40) return;
        
        int currentTotal = currentBossDamage + infiniteBossExtraDamage;
        if (currentTotal >= MAX_TOTAL_DAMAGE)
        {
            // ⭐ v6.0: 50 도달시 Guard 해제
            if (isGuardMode)
            {
                ExitGuardMode();
            }
            Debug.Log($"⚠️ 무한대 보스 공격력 이미 최대: {currentTotal}/{MAX_TOTAL_DAMAGE}");
            return;
        }

        if (gameManager != null && gameManager.IsBossAttacking())
        {
            pendingDamageIncrease = true;
            Debug.Log("⚠️ 보스 공격 중 - 공격력 증가 대기");
            return;
        }

        ApplyDamageIncrease();
    }

    private void ApplyDamageIncrease()
    {
        int currentTotal = currentBossDamage + infiniteBossExtraDamage;
        if (currentTotal >= MAX_TOTAL_DAMAGE)
        {
            // ⭐ v6.0: 50 도달시 Guard 해제
            if (isGuardMode)
            {
                ExitGuardMode();
            }
            return;
        }

        infiniteBossExtraDamage++;
        Debug.Log($"⚠️ 무한대 보스 공격력 증가! base:{currentBossDamage} + extra:{infiniteBossExtraDamage} = {GetEffectiveDamage()}/{MAX_TOTAL_DAMAGE}");
        UpdateBossAttackUI();
        FlashAttackTextBlue();

        // ⭐ v6.0: 50 도달 체크
        if (currentBossDamage + infiniteBossExtraDamage >= MAX_TOTAL_DAMAGE && isGuardMode)
        {
            ExitGuardMode();
        }
    }

    public void ProcessPendingDamageIncrease()
    {
        if (pendingDamageIncrease)
        {
            pendingDamageIncrease = false;
            ApplyDamageIncrease();
        }
    }

    void FlashAttackTextBlue()
    {
        if (bossAttackInfoText == null) return;

        Color originalColor = bossAttackInfoText.color;
        Color blueColor = new Color(0.3f, 0.6f, 1f);

        bossAttackInfoText.color = blueColor;

        DOTween.Sequence()
            .AppendInterval(0.3f)
            .Append(bossAttackInfoText.DOColor(originalColor, 0.4f).SetEase(Ease.OutQuad));
    }

    private int GetEffectiveDamage()
    {
        int total = currentBossDamage + infiniteBossExtraDamage;
        return Mathf.Min(total, MAX_TOTAL_DAMAGE);
    }

    public void TakeDamage(long damage)
    {
        if (isTransitioning) return;

        // ⭐ v6.0: Guard 모드일 때만 무적 (Clear 모드는 데미지 받음)
        if (isGuardMode)
        {
            Debug.Log("🛡️ Guard 모드! 데미지 무효!");
            return;
        }

        int damageInt = (int)Mathf.Min(damage, int.MaxValue);
        currentHP -= damageInt;

        if (bossImageArea != null)
        {
            bossImageArea.transform.DOShakePosition(0.2f, strength: 10f, vibrato: 20, randomness: 90f);
        }

        if (currentHP <= 0)
        {
            currentHP = 0;
            StartCoroutine(OnBossDefeatedCoroutine());
        }

        Debug.Log($"Boss took {damage} damage! Current HP: {currentHP}/{maxHP}");
        UpdateUI(false);
    }

    public void AddTurns(int turns)
    {
        if (isTransitioning) return;

        currentTurnCount += turns;
        bonusTurnsAdded += turns;
        bonusTurnsFilled = 0;
        Debug.Log($"⏰ 보스 공격 턴 +{turns} (현재: {currentTurnCount}턴 남음, 보너스: {bonusTurnsAdded}, 채워짐: {bonusTurnsFilled})");
        UpdateBossAttackUI();
    }

    public void OnPlayerTurn()
    {
        if (isTransitioning) return;
        if (isFrozen) return;

        currentTurnCount--;
        
        if (currentTurnCount < 0 && bonusTurnsFilled < bonusTurnsAdded)
        {
            bonusTurnsFilled++;
            currentTurnCount = 0;
            Debug.Log($"⏰ 보너스 턴 채우는 중: {bonusTurnsFilled}/{bonusTurnsAdded}");
        }
        
        Debug.Log($"보스 공격까지 {currentTurnCount}턴 남음 (보너스: {bonusTurnsFilled}/{bonusTurnsAdded})");

        UpdateBossAttackUI();

        if (currentTurnCount <= 0 && bonusTurnsFilled >= bonusTurnsAdded)
        {
            AttackPlayer();
        }
    }

    private void AttackPlayer()
    {
        StartCoroutine(AttackPlayerCoroutine());
    }

    private IEnumerator AttackPlayerCoroutine()
    {
        Debug.Log($"⚠️ 보스 공격 준비!");

        if (gameManager != null)
        {
            gameManager.SetBossAttacking(true);
        }

        if (bossAttackInfoText != null)
        {
            bossAttackInfoText.text = GetAttackTurnText(0);

            if (attackBlinkAnimation != null)
            {
                attackBlinkAnimation.Kill();
            }

            attackBlinkAnimation = DOTween.Sequence()
                .Append(bossAttackInfoText.DOColor(Color.red, 0.4f))
                .Append(bossAttackInfoText.DOColor(Color.white, 0.4f))
                .SetLoops(-1, LoopType.Restart);
        }

        yield return new WaitForSeconds(0.5f);

        if (bossImageArea != null)
        {
            Vector3 originalPos = bossImageArea.transform.localPosition;

            Sequence attackSeq = DOTween.Sequence();
            attackSeq.Append(bossImageArea.transform.DOLocalMoveX(originalPos.x - 50f, attackMotionDuration * 0.3f)
                .SetEase(Ease.OutQuad));
            attackSeq.Append(bossImageArea.transform.DOLocalMoveX(originalPos.x, attackMotionDuration * 0.7f)
                .SetEase(Ease.OutBounce));

            yield return attackSeq.WaitForCompletion();
        }
        else
        {
            yield return new WaitForSeconds(attackMotionDuration);
        }

        if (gameManager != null)
        {
            int effectiveDamage = GetEffectiveDamage();
            Debug.Log($"⚠️ 보스 공격! {effectiveDamage} 데미지!");
            gameManager.TakeBossAttack(effectiveDamage);
            CameraShake.Instance?.ShakeMedium();
        }

        if (gameManager != null)
        {
            gameManager.SetBossAttacking(false);
        }

        if (attackBlinkAnimation != null)
        {
            attackBlinkAnimation.Kill();
            attackBlinkAnimation = null;
        }

        currentTurnCount = currentTurnInterval;
        bonusTurnsAdded = 0;
        bonusTurnsFilled = 0;
        UpdateBossAttackUI();

        Debug.Log($"보스 공격 완료! 턴 초기화: {currentTurnCount}, 보너스 턴 리셋");

        ProcessPendingDamageIncrease();
    }

    public void ResetTurnCount()
    {
        currentTurnCount = currentTurnInterval;
        Debug.Log($"💥 패링! 보스 공격 턴 초기화! ({currentTurnInterval}턴)");
        UpdateBossAttackUI();
    }

    // ⭐ v6.0: Freeze(Fever) 상태에서 보너스 턴 리셋 - Continue 후 총쏠 때 턴 표시 버그 방지
    public void ResetBonusTurns()
    {
        bonusTurnsAdded = 0;
        bonusTurnsFilled = 0;
        currentTurnCount = currentTurnInterval;
        UpdateBossAttackUI();
        Debug.Log($"🔄 보너스 턴 완전 리셋! 턴: {currentTurnCount}/{currentTurnInterval}");
    }

    void UpdateUI(bool instant = false)
    {
        if (hpSlider != null)
        {
            float targetValue = (float)currentHP / (float)maxHP;

            hpSlider.DOKill();

            if (instant)
            {
                hpSlider.value = targetValue;
            }
            else
            {
                hpSlider.DOValue(targetValue, animationDuration)
                    .SetEase(Ease.OutCubic);
            }
        }

        if (hpText != null)
        {
            // ⭐ v6.0: Guard 모드 = "HP: Guard", Clear 모드 = 숫자 표시
            if (isGuardMode)
            {
                hpText.text = "HP: Guard";
            }
            else
            {
                hpText.text = "HP: " + currentHP + " / " + maxHP;
            }
        }

        UpdateBossAttackUI();
    }

    string GetAttackTurnText(int remainingTurns)
    {
        string filledSymbol = "●";
        string emptySymbol = "○";
        string bonusFilledSymbol = "■";
        string bonusEmptySymbol = "□";

        int totalTurns = currentTurnInterval;
        int filledCount = totalTurns - remainingTurns;

        string symbols = "";
        
        for (int i = 0; i < filledCount; i++)
        {
            symbols += filledSymbol;
        }
        for (int i = filledCount; i < totalTurns; i++)
        {
            symbols += emptySymbol;
        }
        
        for (int i = 0; i < bonusTurnsFilled; i++)
        {
            symbols += bonusFilledSymbol;
        }
        for (int i = bonusTurnsFilled; i < bonusTurnsAdded; i++)
        {
            symbols += bonusEmptySymbol;
        }

        int effectiveDamage = GetEffectiveDamage();
        return $"ATK: {effectiveDamage}\nIn {symbols}";
    }

    void UpdateBossAttackUI()
    {
        if (bossAttackInfoText != null)
        {
            Color textColor = Color.white;

            if (currentTurnCount <= 1)
            {
                textColor = new Color(1f, 0.2f, 0.2f);
            }
            else if (currentTurnCount <= 3)
            {
                textColor = new Color(1f, 0.8f, 0.2f);
            }
            else
            {
                textColor = new Color(0.7f, 0.7f, 0.7f);
            }

            bossAttackInfoText.color = textColor;
            string attackText = GetAttackTurnText(currentTurnCount);
            bossAttackInfoText.text = attackText;
            
            if (bonusTurnsAdded > 0)
            {
                Debug.Log($"💎 UI 업데이트: {attackText} (보너스: {bonusTurnsFilled}/{bonusTurnsAdded})");
            }
        }
    }

    IEnumerator OnBossDefeatedCoroutine()
    {
        isTransitioning = true;

        if (gameManager != null)
        {
            gameManager.OnBossDefeated();
            gameManager.SetBossTransitioning(true);
        }

        Debug.Log("Boss " + bossLevel + " defeated!");

        SetBossUIActive(false);
        StopBossIdleAnimation();

        if (bossImageArea != null)
        {
            Sequence fadeSeq = DOTween.Sequence();
            fadeSeq.Append(bossImageArea.DOFade(0f, 0.5f).SetEase(Ease.InQuad));
            fadeSeq.Join(bossImageArea.transform.DOScale(0.8f, 0.5f).SetEase(Ease.InBack));
            yield return fadeSeq.WaitForCompletion();
        }

        yield return new WaitForSeconds(bossSpawnDelay);

        bossLevel++;

        // ⭐ v6.0: Clear 모드에서는 계속 stage 39 몬스터
        if (isClearMode)
        {
            SetupClearModeBoss();
        }
        else
        {
            SelectNextBossImage();
            
            // 새 보스 스탯 설정
            float exponent = Mathf.Pow(1.5f, bossLevel - 1);
            maxHP = baseHP + Mathf.RoundToInt(hpIncreasePerLevel * (exponent - 1f) / 0.5f);

            if (bossLevel == 39)
            {
                maxHP = 2147483647;
            }
            else if (bossLevel >= 40)
            {
                maxHP = 2147483647;
            }

            currentHP = maxHP;

            currentTurnInterval = Mathf.Max(minTurnInterval, baseTurnInterval - Mathf.FloorToInt((bossLevel - 1) * 0.2f));

            int tempDamage = baseDamage + (bossLevel - 1);
            
            if (tempDamage <= damageThreshold)
            {
                currentBossDamage = tempDamage;
            }
            else
            {
                int levelsOverThreshold = bossLevel - (damageThreshold - baseDamage + 1);
                int slowIncreaseCount = levelsOverThreshold / 5;
                currentBossDamage = damageThreshold + slowIncreaseCount;
            }

            // ⭐ v6.0: Stage 40 진입 시 Guard 모드 시작
            if (bossLevel >= 40 && !isClearMode)
            {
                isGuardMode = true;
                StartGuardColorAnimation();
            }
        }

        infiniteBossExtraDamage = 0;
        currentTurnCount = currentTurnInterval;
        bonusTurnsAdded = 0;
        bonusTurnsFilled = 0;

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
        Debug.Log($"🔄 Boss 리스폰 완료! Level {bossLevel}, ATK: {currentBossDamage}, 턴: {currentTurnCount}/{currentTurnInterval}, Guard: {isGuardMode}, Clear: {isClearMode}");
        
        if (!isFrozen)
        {
            StartBossIdleAnimation();
        }

        if (gameManager != null)
        {
            gameManager.SetBossTransitioning(false);
            gameManager.UpdateTurnUI();
        }

        isTransitioning = false;
    }

    // ⭐ v6.0: Clear 모드 보스 설정 - stage 39 몬스터 반복
    void SetupClearModeBoss()
    {
        // sprite를 stage 39 것으로 고정
        if (stage39SpriteIndex >= 0 && stage39SpriteIndex < bossSprites.Count)
        {
            bossImageArea.sprite = bossSprites[stage39SpriteIndex];
        }

        // 파스텔 오렌지색 고정
        ApplyOrangeColor();

        // HP 21억
        maxHP = 2147483647;
        currentHP = maxHP;

        // stage 39 수준의 공격 설정
        currentTurnInterval = Mathf.Max(minTurnInterval, baseTurnInterval - Mathf.FloorToInt(38 * 0.2f));
        
        int tempDamage = baseDamage + 38; // stage 39 기준
        if (tempDamage <= damageThreshold)
        {
            currentBossDamage = tempDamage;
        }
        else
        {
            int levelsOverThreshold = 39 - (damageThreshold - baseDamage + 1);
            int slowIncreaseCount = levelsOverThreshold / 5;
            currentBossDamage = damageThreshold + slowIncreaseCount;
        }

        Debug.Log($"🏆 Clear 모드 보스! sprite: stage39, HP: {maxHP}, ATK: {currentBossDamage}");
    }

    public void ResetBoss()
    {
        isFirstGame = false;
        bossLevel = 1;
        currentBossIndex = 0;
        isFrozen = false;
        bonusTurnsAdded = 0;
        bonusTurnsFilled = 0;
        infiniteBossExtraDamage = 0;

        // ⭐ v6.0: Guard/Clear 상태 초기화
        isGuardMode = false;
        isClearMode = false;
        stage39SpriteIndex = -1;
        StopGuardColorAnimation();

        // Boss panel ground 색상 초기화
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

        // HP bar fill 색상 초기화
        if (hpSlider != null)
        {
            Image fillImage = hpSlider.fillRect?.GetComponent<Image>();
            if (fillImage != null)
            {
                fillImage.color = Color.white; // 기본 색상으로 복원
            }
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
        if (bossSprites.Count == 0)
        {
            Debug.LogWarning("No boss sprites assigned!");
            return;
        }

        if (bossSprites.Count == 1)
        {
            if (bossImageArea.sprite == null)
            {
                bossImageArea.sprite = bossSprites[0];
            }
            ApplyOrangeColor();
        }
        else
        {
            int imageIndex;
            if (bossLevel == 1 && isFirstGame)
            {
                imageIndex = 0;
            }
            else
            {
                imageIndex = bossLevel - 1;
                
                if (imageIndex >= bossSprites.Count)
                {
                    imageIndex = bossSprites.Count - 1;
                }
            }

            currentBossIndex = imageIndex;

            // ⭐ v6.0: stage 39 sprite 인덱스 저장
            if (bossLevel == 39)
            {
                stage39SpriteIndex = currentBossIndex;
                Debug.Log($"📌 Stage 39 sprite 인덱스 저장: {stage39SpriteIndex}");
            }

            if (currentBossIndex < bossSprites.Count && bossSprites[currentBossIndex] != null)
            {
                bossImageArea.sprite = bossSprites[currentBossIndex];
            }
            else
            {
                Debug.LogWarning($"Boss sprite at index {currentBossIndex} is null or out of range!");
            }

            // Guard 모드가 아닐 때만 파스텔 오렌지 고정
            if (!isGuardMode)
            {
                ApplyOrangeColor();
            }
        }
    }

    void ApplyOrangeColor()
    {
        if (bossImageArea == null) return;

        Color pastelOrange = new Color(1.0f, 0.75f, 0.5f, 1.0f);

        Material mat = new Material(Shader.Find("UI/Default"));
        mat.SetColor("_Color", pastelOrange);
        bossImageArea.material = mat;
    }

    void StartBossIdleAnimation()
    {
        if (bossImageArea == null) return;

        if (bossIdleAnimation != null)
        {
            bossIdleAnimation.Kill();
        }

        if (isFrozen) return;

        bossIdleAnimation = bossImageArea.transform.DOLocalRotate(
            new Vector3(0f, 0f, 5f),
            2.0f
        )
        .SetEase(Ease.InOutSine)
        .SetLoops(-1, LoopType.Yoyo);

        Debug.Log("Boss idle animation started!");
    }

    void StopBossIdleAnimation()
    {
        if (bossIdleAnimation != null)
        {
            bossIdleAnimation.Kill();
            bossIdleAnimation = null;
        }

        if (bossImageArea != null)
        {
            bossImageArea.transform.localRotation = Quaternion.identity;
        }
    }

    void SetBossUIActive(bool active)
    {
        if (hpSlider != null)
            hpSlider.gameObject.SetActive(active);

        if (hpText != null)
            hpText.gameObject.SetActive(active);

        if (bossAttackInfoText != null)
            bossAttackInfoText.gameObject.SetActive(active);
    }

    public void SetFrozen(bool frozen)
    {
        isFrozen = frozen;

        if (frozen)
        {
            StopBossIdleAnimation();
            Debug.Log("🧊 Boss Frozen!");
        }
        else
        {
            StartBossIdleAnimation();
            Debug.Log("🔥 Boss Unfrozen!");
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
    public bool IsGuardMode() { return isGuardMode; } // ⭐ v6.0
    public bool IsClearMode() { return isClearMode; } // ⭐ v6.0
}
