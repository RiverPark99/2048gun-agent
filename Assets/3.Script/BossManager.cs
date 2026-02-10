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

    [Header("Boss Stats")]
    public int baseHP = 200;
    public int hpIncreasePerLevel = 200;
    private int maxHP;
    private int currentHP;

    [Header("보스 공격 시스템")]
    [SerializeField] private int baseTurnInterval = 8;
    [SerializeField] private int minTurnInterval = 3;
    [SerializeField] private int baseDamage = 28; // ⭐ 원래값 유지
    [SerializeField] private int damageThreshold = 40; // ⭐ 원래값 유지: stage 1~40 동안 28→최대 40까지 1씩 증가

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
    private const int MAX_TOTAL_DAMAGE = 70;
    private bool pendingDamageIncrease = false; // ⭐ v5.1: 공격 완료 후 증가 대기

    void Start()
    {
        gameManager = FindAnyObjectByType<GameManager>();
        InitializeBoss();
        StartBossIdleAnimation();
    }

    void InitializeBoss()
    {
        float exponent = Mathf.Pow(1.5f, bossLevel - 1);
        maxHP = baseHP + Mathf.RoundToInt(hpIncreasePerLevel * (exponent - 1f) / 0.5f);

        // ⭐ v5.0: 한 stage 앞당김
        // stage 39 = HP 2,147,483,647 (쓰러뜨릴 수 있음)
        // stage 40 = 무한대 (무적)
        if (bossLevel == 39)
        {
            maxHP = 2147483647;
        }
        else if (bossLevel >= 40)
        {
            maxHP = 2147483647; // 내부적으로 int.MaxValue, UI는 ∞ 표시
        }

        currentHP = maxHP;

        currentTurnInterval = Mathf.Max(minTurnInterval, baseTurnInterval - Mathf.FloorToInt((bossLevel - 1) * 0.2f));

        // ⭐ 원래 공격력 로직 복원:
        // stage 1~(damageThreshold-baseDamage+1): baseDamage(28) + (bossLevel-1), 즉 매 스테이지 1씩 증가
        // threshold(40) 도달 후: 5스테이지마다 1씩 증가 (기존 8 → 5로 변경)
        int tempDamage = baseDamage + (bossLevel - 1);
        
        if (tempDamage <= damageThreshold)
        {
            currentBossDamage = tempDamage;
        }
        else
        {
            // damageThreshold를 넘은 이후 5스테이지마다 1씩 증가
            int levelsOverThreshold = bossLevel - (damageThreshold - baseDamage + 1);
            int slowIncreaseCount = levelsOverThreshold / 5; // ⭐ 8 → 5로 변경
            currentBossDamage = damageThreshold + slowIncreaseCount;
        }

        // ⭐ v5.0: 무한대 보스 추가 데미지 초기화
        infiniteBossExtraDamage = 0;

        currentTurnCount = currentTurnInterval;

        UpdateUI(true);
        Debug.Log($"Boss Level {bossLevel} spawned! HP: {currentHP}/{maxHP}, 공격 주기: {currentTurnInterval}턴, 공격력: {currentBossDamage}");
    }

    // ⭐ v5.1: 무한대 보스 - 20회 이동마다 공격력 1 증가 (최대 70까지)
    // 보스가 공격 중이면 대기 후 공격 완료 후 처리
    public void IncreaseInfiniteBossDamage()
    {
        if (bossLevel < 40) return;
        
        int currentTotal = currentBossDamage + infiniteBossExtraDamage;
        if (currentTotal >= MAX_TOTAL_DAMAGE)
        {
            Debug.Log($"⚠️ 무한대 보스 공격력 이미 최대: {currentTotal}/{MAX_TOTAL_DAMAGE}");
            return;
        }

        // 보스가 공격 중이면 대기
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
        if (currentTotal >= MAX_TOTAL_DAMAGE) return;

        infiniteBossExtraDamage++;
        Debug.Log($"⚠️ 무한대 보스 공격력 증가! base:{currentBossDamage} + extra:{infiniteBossExtraDamage} = {GetEffectiveDamage()}/{MAX_TOTAL_DAMAGE}");
        UpdateBossAttackUI();
        FlashAttackTextBlue();
    }

    // ⭐ v5.1: 공격 완료 후 대기중인 데미지 증가 처리
    public void ProcessPendingDamageIncrease()
    {
        if (pendingDamageIncrease)
        {
            pendingDamageIncrease = false;
            ApplyDamageIncrease();
        }
    }

    // ⭐ v5.1: ATK 텍스트 푸른색 플래시
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

        // ⭐ v5.0: stage 40만 무적 (39는 HP 2,147,483,647이지만 쓰러뜨릴 수 있음)
        if (bossLevel >= 40)
        {
            Debug.Log("40번째 적은 무적!");
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

        // ⭐ v5.1: 공격 완료 후 대기중인 데미지 증가 처리
        ProcessPendingDamageIncrease();
    }

    public void ResetTurnCount()
    {
        currentTurnCount = currentTurnInterval;
        Debug.Log($"💥 패링! 보스 공격 턴 초기화! ({currentTurnInterval}턴)");
        UpdateBossAttackUI();
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
            // ⭐ v5.0: stage 40만 ∞, stage 39는 숫자 표시
            if (bossLevel >= 40)
            {
                hpText.text = "HP: ∞";
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

        SelectNextBossImage();

        if (bossImageArea != null)
        {
            if (bossImageArea.sprite == null && bossSprites.Count > 0)
            {
                bossImageArea.sprite = bossSprites[0];
            }

            bossImageArea.color = new Color(1f, 1f, 1f, 0f);
            bossImageArea.transform.localScale = Vector3.one * 1.2f;

            Sequence appearSeq = DOTween.Sequence();
            appearSeq.Append(bossImageArea.DOFade(1f, 0.5f).SetEase(Ease.OutQuad));
            appearSeq.Join(bossImageArea.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack));
            yield return appearSeq.WaitForCompletion();
        }

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

        // 공격력 계산 (원래 로직 + 5스테이지 변경)
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
        bonusTurnsAdded = 0;
        bonusTurnsFilled = 0;

        UpdateUI(true);
        SetBossUIActive(true);
        
        UpdateBossAttackUI();
        Debug.Log($"🔄 Boss 리스폰 완료! Level {bossLevel}, ATK: {currentBossDamage}, 턴: {currentTurnCount}/{currentTurnInterval}");
        
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

    public void ResetBoss()
    {
        isFirstGame = false;
        bossLevel = 1;
        currentBossIndex = 0;
        isFrozen = false;
        bonusTurnsAdded = 0;
        bonusTurnsFilled = 0;
        infiniteBossExtraDamage = 0;

        if (bossImageArea != null && bossSprites.Count > 0)
        {
            bossImageArea.sprite = bossSprites[0];
            bossImageArea.color = Color.white;
            bossImageArea.material = null;
            bossImageArea.transform.localScale = Vector3.one;
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
            ApplyColorBasedOnLoop();
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

            if (currentBossIndex < bossSprites.Count && bossSprites[currentBossIndex] != null)
            {
                bossImageArea.sprite = bossSprites[currentBossIndex];
            }
            else
            {
                Debug.LogWarning($"Boss sprite at index {currentBossIndex} is null or out of range!");
            }

            ApplyColorBasedOnLoop();
        }
    }

    void ApplyColorBasedOnLoop()
    {
        if (bossImageArea == null) return;

        Color pinkColor = new Color(1.0f, 0.4f, 0.6f, 1.0f);

        Material mat = new Material(Shader.Find("UI/Default"));
        mat.SetColor("_Color", pinkColor);
        bossImageArea.material = mat;

        Debug.Log($"Boss Level {bossLevel}, Image {currentBossIndex}, Color: Berry(핑크 고정)");
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
    public bool IsInfiniteBoss() { return bossLevel >= 40; } // ⭐ v5.0
}
