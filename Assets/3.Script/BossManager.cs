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
    public float bossSpawnDelay = 1.0f; // 보스 리젠 대기 시간

    private bool isTransitioning = false; // 보스 교체 중인지

    void Start()
    {
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
        if (isTransitioning) return; // 보스 교체 중엔 데미지 무시

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
                // 자연스럽게 줄어들기만
                hpSlider.DOValue(targetValue, animationDuration)
                    .SetEase(Ease.OutCubic);
            }

            Debug.Log($"HP Bar updated! value: {targetValue} ({currentHP}/{maxHP})");
        }
        else
        {
            Debug.LogWarning("hpSlider is NULL! Please connect it in Inspector.");
        }

        if (hpText != null)
        {
            hpText.text = "HP: " + currentHP + " / " + maxHP;
        }
        else
        {
            Debug.LogWarning("hpText is NULL! Please connect it in Inspector.");
        }
    }

    IEnumerator OnBossDefeatedCoroutine()
    {
        isTransitioning = true;

        Debug.Log("Boss " + bossLevel + " defeated!");

        // 보스 처치 후 대기
        yield return new WaitForSeconds(bossSpawnDelay);

        // 다음 보스 준비
        bossLevel++;

        // 보스 이미지 변경 여기서
        // if (bossImageArea != null)
        // {
        //     bossImageArea.sprite = nextBossSprite;
        // }

        InitializeBoss();
        isTransitioning = false;
    }

    public int GetCurrentHP() { return currentHP; }
    public int GetMaxHP() { return maxHP; }
    public int GetBossLevel() { return bossLevel; }
}