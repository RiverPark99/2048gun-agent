using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;

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
    [SerializeField] private int baseDamage = 10;
    [SerializeField] private int maxDamage = 30;

    private int currentTurnInterval;
    private int currentTurnCount = 0;
    private int currentBossDamage;

    [Header("Boss Progression")]
    public int bossLevel = 1;

    [Header("HP Bar Animation")]
    public float animationDuration = 0.3f;
    public float bossSpawnDelay = 1.0f;

    private bool isTransitioning = false;
    private GameManager gameManager;

    void Start()
    {
        gameManager = FindAnyObjectByType<GameManager>();
        InitializeBoss();
    }

    void InitializeBoss()
    {
        maxHP = baseHP + (bossLevel - 1) * hpIncreasePerLevel;
        currentHP = maxHP;

        currentTurnInterval = Mathf.Max(minTurnInterval, baseTurnInterval - Mathf.FloorToInt((bossLevel - 1) * 0.2f));
        currentBossDamage = Mathf.Min(maxDamage, baseDamage + (bossLevel - 1));
        currentTurnCount = currentTurnInterval;

        UpdateUI(true);
        Debug.Log($"Boss Level {bossLevel} spawned! HP: {currentHP}/{maxHP}, 공격 주기: {currentTurnInterval}턴, 공격력: {currentBossDamage}");
    }

    public void TakeDamage(int damage)
    {
        if (isTransitioning) return;

        currentHP -= damage;

        // 피격 시 작은 흔들림 효과
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

    public void OnPlayerTurn()
    {
        if (isTransitioning) return;

        currentTurnCount--;
        Debug.Log($"보스 공격까지 {currentTurnCount}턴 남음");

        UpdateBossAttackUI();

        if (currentTurnCount <= 0)
        {
            AttackPlayer();
            currentTurnCount = currentTurnInterval;
            UpdateBossAttackUI();
        }
    }

    private void AttackPlayer()
    {
        if (gameManager != null)
        {
            Debug.Log($"⚠️ 보스 공격! {currentBossDamage} 데미지!");

            //// 보스 이미지 펄싱 효과
            //if (bossImageArea != null)
            //{
            //    bossImageArea.transform.DOPunchScale(Vector3.one * 0.3f, 0.5f, 10, 1f);
            //}

            gameManager.TakeBossAttack(currentBossDamage);

            // 강한 화면 흔들림
            CameraShake.Instance?.ShakeMedium();
        }
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

        yield return new WaitForSeconds(bossSpawnDelay);

        bossLevel++;
        InitializeBoss();

        if (gameManager != null)
        {
            gameManager.SetBossTransitioning(false);
        }

        isTransitioning = false;
    }

    public void ResetBoss()
    {
        bossLevel = 1;
        InitializeBoss();
        isTransitioning = false;
    }

    public int GetCurrentHP() { return currentHP; }
    public int GetMaxHP() { return maxHP; }
    public int GetBossLevel() { return bossLevel; }
    public int GetTurnCount() { return currentTurnCount; }
    public int GetTurnInterval() { return currentTurnInterval; }
    public int GetBossDamage() { return currentBossDamage; }
}