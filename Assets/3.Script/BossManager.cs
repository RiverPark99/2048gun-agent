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
    private int bonusTurnsAdded = 0; // ⭐ NEW: Fever Gun으로 추가된 총 보너스 턴 수
    private int bonusTurnsFilled = 0; // ⭐ NEW: 채워진 보너스 턴 수

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

        if (bossLevel == 40)
        {
            maxHP = 2147483647;
        }

        currentHP = maxHP;

        currentTurnInterval = Mathf.Max(minTurnInterval, baseTurnInterval - Mathf.FloorToInt((bossLevel - 1) * 0.2f));

        int tempDamage = baseDamage + (bossLevel - 1);
        
        if (tempDamage < damageThreshold)
        {
            currentBossDamage = tempDamage;
        }
        else
        {
            int levelsOver40 = bossLevel - (damageThreshold - baseDamage);
            int slowIncreaseCount = levelsOver40 / 8;
            currentBossDamage = damageThreshold + slowIncreaseCount;
        }

        currentTurnCount = currentTurnInterval;

        UpdateUI(true);
        Debug.Log($"Boss Level {bossLevel} spawned! HP: {currentHP}/{maxHP}, 공격 주기: {currentTurnInterval}턴, 공격력: {currentBossDamage}");
    }

    public void TakeDamage(long damage)
    {
        if (isTransitioning) return;

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
        // ⭐ CRITICAL: Frozen 체크 제거 - Fever Gun은 Frozen 상태에서도 턴 추가 가능
        // if (isFrozen) return; // 제거

        currentTurnCount += turns;
        bonusTurnsAdded += turns; // ⭐ NEW: 총 보너스 턴 수 기록
        bonusTurnsFilled = 0; // ⭐ NEW: 채워진 보너스는 0부터 시작
        Debug.Log($"⏰ 보스 공격 턴 +{turns} (현재: {currentTurnCount}턴 남음, 보너스: {bonusTurnsAdded}, 채워짐: {bonusTurnsFilled})");
        UpdateBossAttackUI(); // ⭐ CRITICAL: 즉시 UI 업데이트
    }

    public void OnPlayerTurn()
    {
        if (isTransitioning) return;
        if (isFrozen) return;

        currentTurnCount--;
        
        // ⭐ NEW: 기본 턴이 다 차면 보너스 턴 채우기 시작
        if (currentTurnCount < 0 && bonusTurnsFilled < bonusTurnsAdded)
        {
            bonusTurnsFilled++;
            currentTurnCount = 0; // 기본 턴은 0 유지
            Debug.Log($"⏰ 보너스 턴 채우는 중: {bonusTurnsFilled}/{bonusTurnsAdded}");
        }
        
        Debug.Log($"보스 공격까지 {currentTurnCount}턴 남음 (보너스: {bonusTurnsFilled}/{bonusTurnsAdded})");

        UpdateBossAttackUI();

        // ⭐ NEW: 기본 턴 + 보너스 턴 모두 소진되면 공격
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
            Debug.Log($"⚠️ 보스 공격! {currentBossDamage} 데미지!");
            gameManager.TakeBossAttack(currentBossDamage);
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
        bonusTurnsAdded = 0; // ⭐ NEW: 공격 후 보너스 턴 리셋
        bonusTurnsFilled = 0; // ⭐ NEW: 채워진 보너스 턴도 리셋
        UpdateBossAttackUI();

        Debug.Log($"보스 공격 완료! 턴 초기화: {currentTurnCount}, 보너스 턴 리셋");
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

    // ⭐ UPDATED: 보너스 턴 빈 네모(□) → 채워진 네모(■) 방식
    string GetAttackTurnText(int remainingTurns)
    {
        string filledSymbol = "●"; // 기본 턴: 채워진 원
        string emptySymbol = "○";  // 기본 턴: 빈 원
        string bonusFilledSymbol = "■";  // ⭐ NEW: 채워진 보너스 턴 (검은 네모)
        string bonusEmptySymbol = "□";   // ⭐ NEW: 빈 보너스 턴 (흰 네모)

        int totalTurns = currentTurnInterval;
        int filledCount = totalTurns - remainingTurns;

        string symbols = "";
        
        // 기본 턴 표시
        for (int i = 0; i < filledCount; i++)
        {
            symbols += filledSymbol;
        }
        for (int i = filledCount; i < totalTurns; i++)
        {
            symbols += emptySymbol;
        }
        
        // ⭐ NEW: 보너스 턴 표시 (채워진 개수만큼 ■, 나머지는 □)
        for (int i = 0; i < bonusTurnsFilled; i++)
        {
            symbols += bonusFilledSymbol;
        }
        for (int i = bonusTurnsFilled; i < bonusTurnsAdded; i++)
        {
            symbols += bonusEmptySymbol;
        }

        return $"ATK: {currentBossDamage}\nIn {symbols}";
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
            
            // ⭐ DEBUG: 보너스 턴 UI 텍스트 확인
            if (bonusTurnsAdded > 0)
            {
                Debug.Log($"💎 UI 업데이트: {attackText} (보너스: {bonusTurnsFilled}/{bonusTurnsAdded})");
            }
        }
    }

    IEnumerator OnBossDefeatedCoroutine()
    {
        isTransitioning = true;

        // ⭐ UPDATED: Freeze 해제를 여기서 하지 않음 (Fever 유지)

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

        float exponent = Mathf.Pow(1.5f, bossLevel - 1);
        maxHP = baseHP + Mathf.RoundToInt(hpIncreasePerLevel * (exponent - 1f) / 0.5f);

        if (bossLevel == 40)
        {
            maxHP = 2147483647;
        }

        currentHP = maxHP;

        currentTurnInterval = Mathf.Max(minTurnInterval, baseTurnInterval - Mathf.FloorToInt((bossLevel - 1) * 0.2f));

        int tempDamage = baseDamage + (bossLevel - 1);
        
        if (tempDamage < damageThreshold)
        {
            currentBossDamage = tempDamage;
        }
        else
        {
            int levelsOver40 = bossLevel - (damageThreshold - baseDamage);
            int slowIncreaseCount = levelsOver40 / 8;
            currentBossDamage = damageThreshold + slowIncreaseCount;
        }

        currentTurnCount = currentTurnInterval;
        bonusTurnsAdded = 0; // ⭐ NEW: 보스 리스폰 시 보너스 턴 리셋
        bonusTurnsFilled = 0; // ⭐ NEW: 채워진 보너스 턴도 리셋

        UpdateUI(true);
        SetBossUIActive(true);
        
        // ⭐ CRITICAL: Boss 리스폰 완료 후 UI 다시 업데이트 (보너스 턴 초기화 확인)
        UpdateBossAttackUI();
        Debug.Log($"🔄 Boss 리스폰 완료! UI 업데이트: 기본 턴 {currentTurnCount}/{currentTurnInterval}, 보너스: {bonusTurnsFilled}/{bonusTurnsAdded}");
        
        // ⭐ UPDATED: Freeze 상태라면 애니메이션 시작 안 함
        if (!isFrozen)
        {
            StartBossIdleAnimation();
        }

        if (gameManager != null)
        {
            gameManager.SetBossTransitioning(false);
            gameManager.UpdateTurnUI(); // ⭐ NEW: Boss 리스폰 완료 후 Stage UI 업데이트
        }

        isTransitioning = false;
    }

    public void ResetBoss()
    {
        isFirstGame = false;
        bossLevel = 1;
        currentBossIndex = 0;
        isFrozen = false;
        bonusTurnsAdded = 0; // ⭐ NEW: 보너스 턴 리셋
        bonusTurnsFilled = 0; // ⭐ NEW: 채워진 보너스 턴도 리셋

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
            // ⭐ UPDATED: 루프 제거, 순서대로 계속 이어지기
            int imageIndex;
            if (bossLevel == 1 && isFirstGame)
            {
                imageIndex = 0;
            }
            else
            {
                // 순환 없이 계속 증가
                imageIndex = bossLevel - 1;
                
                // 이미지가 부족하면 마지막 이미지 유지
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

        // ⭐ UPDATED: 루프 제거, 항상 핑크색 고정
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
    public int GetBossDamage() { return currentBossDamage; }
}
