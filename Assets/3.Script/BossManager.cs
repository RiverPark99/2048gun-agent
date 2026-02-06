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
    [SerializeField] private int damageThreshold = 40; // 40 이상부터 천천히 증가

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
    [SerializeField] private List<Sprite> bossSprites = new List<Sprite>(); // 보스 이미지 리스트 (0번: 기본, 1~16번: 루프용)
    private int currentBossIndex = 0;

    private bool isTransitioning = false;
    private GameManager gameManager;
    private Tweener bossIdleAnimation;
    private Sequence attackBlinkAnimation;
    private bool isFirstGame = true;

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

        currentHP = maxHP;

        currentTurnInterval = Mathf.Max(minTurnInterval, baseTurnInterval - Mathf.FloorToInt((bossLevel - 1) * 0.2f));

        // 공격력 계산: 공격력이 40 미만일 때는 레벨당 1씩, 40 이상부터는 8번마다 1씩 증가
        // baseDamage = 28
        // 레벨 1 = 28, 레벨 2 = 29, ..., 레벨 13 = 40
        // 레벨 13~20: 40 (8번 고정)
        // 레벨 21~28: 41 (8번 고정)
        int tempDamage = baseDamage + (bossLevel - 1);
        
        if (tempDamage < damageThreshold)
        {
            // 공격력이 40 미만: 그대로 사용
            currentBossDamage = tempDamage;
        }
        else
        {
            // 공격력이 40 이상: 40부터 8번마다 1씩 증가
            int levelsOver40 = bossLevel - (damageThreshold - baseDamage); // 40 도달 레벨 계산
            int slowIncreaseCount = levelsOver40 / 8; // 8번마다 1씩
            currentBossDamage = damageThreshold + slowIncreaseCount;
        }

        currentTurnCount = currentTurnInterval;

        UpdateUI(true);
        Debug.Log($"Boss Level {bossLevel} spawned! HP: {currentHP}/{maxHP}, 공격 주기: {currentTurnInterval}턴, 공격력: {currentBossDamage}");
    }

    public void TakeDamage(long damage)
    {
        if (isTransitioning) return;

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
        Debug.Log($"보스 공격 턴 +{turns} (현재: {currentTurnCount}턴 남음)");
        UpdateBossAttackUI();
    }

    public void OnPlayerTurn()
    {
        if (isTransitioning) return;

        currentTurnCount--;
        Debug.Log($"보스 공격까지 {currentTurnCount}턴 남음");

        UpdateBossAttackUI();

        if (currentTurnCount <= 0)
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
            bossAttackInfoText.text = $"ATK: {currentBossDamage} | In: 0";

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
        UpdateBossAttackUI();

        Debug.Log($"보스 공격 완료! 턴 초기화: {currentTurnCount}");
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
            hpText.text = "HP: " + currentHP + " / " + maxHP;
        }

        UpdateBossAttackUI();
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
            bossAttackInfoText.text = $"ATK: {currentBossDamage} | In: {currentTurnCount}";
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

        float exponent = Mathf.Pow(1.5f, bossLevel - 1);
        maxHP = baseHP + Mathf.RoundToInt(hpIncreasePerLevel * (exponent - 1f) / 0.5f);
        currentHP = maxHP;

        currentTurnInterval = Mathf.Max(minTurnInterval, baseTurnInterval - Mathf.FloorToInt((bossLevel - 1) * 0.2f));

        // 공격력 계산
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
        SetBossUIActive(true);
        StartBossIdleAnimation();

        if (gameManager != null)
        {
            gameManager.SetBossTransitioning(false);
        }

        isTransitioning = false;
    }

    public void ResetBoss()
    {
        isFirstGame = false;
        bossLevel = 1;
        currentBossIndex = 0;

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
            // 이미지 루프: 0~15 (16개)
            // 레벨 1: 이미지 -1 (기본, bossSprites[0])
            // 레벨 2~17: 이미지 0~15 (bossSprites[1~16])
            // 레벨 18~33: 이미지 0~15 (bossSprites[1~16])
            // ...반복

            int imageIndex;
            if (bossLevel == 1 && isFirstGame)
            {
                // 첫 게임 첫 보스: 0번 이미지 (기본, -1번 역할)
                imageIndex = 0;
            }
            else
            {
                // 레벨 2부터 시작
                int adjustedLevel = bossLevel - 2; // 레벨 2 = 0, 레벨 3 = 1, ...
                int loopPosition = adjustedLevel % 16; // 0~15
                imageIndex = loopPosition + 1; // bossSprites[1~16]
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

        // 루프 계산: 16개씩 루프
        int loopCount;
        if (bossLevel == 1 && isFirstGame)
        {
            loopCount = 0; // 첫 보스는 루프 0
        }
        else
        {
            // 레벨 2~17: 루프 0
            // 레벨 18~33: 루프 1
            // 레벨 34~49: 루프 2
            int adjustedLevel = bossLevel - 2;
            loopCount = adjustedLevel / 16;
        }

        Color newColor;
        // 루프 0: Berry(분홍), 루프 1: Choco(갈색), 루프 2: Berry, ...
        if (loopCount % 2 == 0)
        {
            // Berry: 분홍 계열
            newColor = new Color(1.0f, 0.4f, 0.6f, 1.0f);
        }
        else
        {
            // Choco: 갈색 계열
            newColor = new Color(0.75f, 0.55f, 0.35f, 1.0f);
        }

        Material mat = new Material(Shader.Find("UI/Default"));
        mat.SetColor("_Color", newColor);
        bossImageArea.material = mat;

        Debug.Log($"Boss Level {bossLevel}, Loop {loopCount}, Image {currentBossIndex}, Color: {(loopCount % 2 == 0 ? "Berry(분홍)" : "Choco(갈색)")}");
    }

    void StartBossIdleAnimation()
    {
        if (bossImageArea == null) return;

        if (bossIdleAnimation != null)
        {
            bossIdleAnimation.Kill();
        }

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

    public int GetCurrentHP() { return currentHP; }
    public int GetMaxHP() { return maxHP; }
    public int GetBossLevel() { return bossLevel; }
    public int GetTurnCount() { return currentTurnCount; }
    public int GetTurnInterval() { return currentTurnInterval; }
    public int GetBossDamage() { return currentBossDamage; }
}
