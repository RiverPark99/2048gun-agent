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

    [Header("Boss Stats")]
    public int maxHP = 256;
    private int currentHP;

    [Header("Boss Progression")]
    public int bossLevel = 1;
    public float hpMultiplier = 1.41421356f;

    [Header("HP Bar Animation")]
    public float animationDuration = 0.3f;
    public float bossSpawnDelay = 1.0f;

    private bool isTransitioning = false;
    private GameManager gameManager;

    void Start()
    {
        gameManager = FindObjectOfType<GameManager>();
        InitializeBoss();
    }

    void InitializeBoss()
    {
        maxHP = Mathf.RoundToInt(256 * Mathf.Pow(hpMultiplier, bossLevel - 1));
        currentHP = maxHP;
        UpdateUI(true);
        Debug.Log($"Boss Level {bossLevel} spawned! HP: {currentHP}/{maxHP}");
    }

    public void TakeDamage(int damage)
    {
        if (isTransitioning) return;

        currentHP -= damage;

        if (currentHP <= 0)
        {
            currentHP = 0;
            StartCoroutine(OnBossDefeatedCoroutine());
        }

        Debug.Log($"Boss took {damage} damage! Current HP: {currentHP}/{maxHP}");
        UpdateUI(false);
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
    }

    IEnumerator OnBossDefeatedCoroutine()
    {
        isTransitioning = true;

        // GameManager에 보스 리스폰 시작 알림
        if (gameManager != null)
        {
            gameManager.OnBossDefeated(); // 턴 초기화
            gameManager.SetBossTransitioning(true); // 인풋 막기
        }

        Debug.Log("Boss " + bossLevel + " defeated!");

        // 보스 처치 후 대기 (이 동안 플레이어 인풋 불가)
        yield return new WaitForSeconds(bossSpawnDelay);

        bossLevel++;
        InitializeBoss();

        // GameManager에 보스 리스폰 완료 알림
        if (gameManager != null)
        {
            gameManager.SetBossTransitioning(false); // 인풋 다시 허용
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
}