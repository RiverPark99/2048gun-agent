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

    [Header("Boss Attack Animation")]
    [SerializeField] private float attackMotionDuration = 0.5f;

    [Header("Boss Images")]
    [SerializeField] private List<Sprite> bossSprites = new List<Sprite>(); // 보스 이미지 리스트
    private int currentBossIndex = 0;

    private bool isTransitioning = false;
    private GameManager gameManager;

    void Start()
    {
        gameManager = FindAnyObjectByType<GameManager>();
        InitializeBoss();
    }

    void InitializeBoss()
    {
        float exponent = Mathf.Pow(1.5f, bossLevel - 1);
        maxHP = baseHP + Mathf.RoundToInt(hpIncreasePerLevel * (exponent - 1f) / 0.5f);

        currentHP = maxHP;

        currentTurnInterval = Mathf.Max(minTurnInterval, baseTurnInterval - Mathf.FloorToInt((bossLevel - 1) * 0.2f));
        currentBossDamage = Mathf.Min(maxDamage, baseDamage + (bossLevel - 1));
        currentTurnCount = currentTurnInterval;

        // ⭐ UI 비활성화
        SetBossUIActive(false);

        currentTurnInterval = Mathf.Max(minTurnInterval, baseTurnInterval - Mathf.FloorToInt((bossLevel - 1) * 0.2f));
        currentBossDamage = Mathf.Min(maxDamage, baseDamage + (bossLevel - 1));
        currentTurnCount = currentTurnInterval;

        UpdateUI(true);
        Debug.Log($"Boss Level {bossLevel} spawned! HP: {currentHP}/{maxHP}, 공격 주기: {currentTurnInterval}턴, 공격력: {currentBossDamage}");
    }

    public void TakeDamage(long damage)
    {
        if (isTransitioning) return;

        // long을 int로 변환 (보스 체력은 int)
        int damageInt = (int)Mathf.Min(damage, int.MaxValue);
        currentHP -= damageInt;

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
        StartCoroutine(AttackPlayerCoroutine());
    }

    private IEnumerator AttackPlayerCoroutine()
    {
        Debug.Log($"⚠️ 보스 공격 준비!");

        // ⭐ NEW: 플레이어 입력 차단
        if (gameManager != null)
        {
            gameManager.SetBossAttacking(true);
        }

        // ⭐ NEW: 공격 모션 (앞으로 이동)
        if (bossImageArea != null)
        {
            Vector3 originalPos = bossImageArea.transform.localPosition;

            // 앞으로 돌진
            Sequence attackSeq = DOTween.Sequence();
            attackSeq.Append(bossImageArea.transform.DOLocalMoveX(originalPos.x - 50f, attackMotionDuration * 0.3f)
                .SetEase(Ease.OutQuad));
            // 원래 위치로
            attackSeq.Append(bossImageArea.transform.DOLocalMoveX(originalPos.x, attackMotionDuration * 0.7f)
                .SetEase(Ease.OutBounce));

            yield return attackSeq.WaitForCompletion();
        }
        else
        {
            yield return new WaitForSeconds(attackMotionDuration);
        }

        // ⭐ 모션 후 데미지
        if (gameManager != null)
        {
            Debug.Log($"⚠️ 보스 공격! {currentBossDamage} 데미지!");
            gameManager.TakeBossAttack(currentBossDamage);
            CameraShake.Instance?.ShakeMedium();
        }

        // ⭐ NEW: 플레이어 입력 재개
        if (gameManager != null)
        {
            gameManager.SetBossAttacking(false);
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

        // ⭐ NEW: UI 비활성화
        SetBossUIActive(false);

        // ⭐ NEW: 보스 이미지 사라지기 (DOTween)
        if (bossImageArea != null)
        {
            Sequence fadeSeq = DOTween.Sequence();
            fadeSeq.Append(bossImageArea.DOFade(0f, 0.5f).SetEase(Ease.InQuad));
            fadeSeq.Join(bossImageArea.transform.DOScale(0.8f, 0.5f).SetEase(Ease.InBack));
            yield return fadeSeq.WaitForCompletion();
        }

        yield return new WaitForSeconds(bossSpawnDelay);

        bossLevel++;

        // ⭐ NEW: 다음 보스 이미지 선택
        SelectNextBossImage();

        // ⭐ NEW: 보스 이미지 나타나기 (DOTween)
        if (bossImageArea != null)
        {
            bossImageArea.color = new Color(1f, 1f, 1f, 0f); // 투명
            bossImageArea.transform.localScale = Vector3.one * 1.2f;

            Sequence appearSeq = DOTween.Sequence();
            appearSeq.Append(bossImageArea.DOFade(1f, 0.5f).SetEase(Ease.OutQuad));
            appearSeq.Join(bossImageArea.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack));
            yield return appearSeq.WaitForCompletion();
        }

        // ⭐ NEW: 체력 설정 (지수 증가)
        float exponent = Mathf.Pow(1.5f, bossLevel - 1);
        maxHP = baseHP + Mathf.RoundToInt(hpIncreasePerLevel * (exponent - 1f) / 0.5f);
        currentHP = maxHP;

        currentTurnInterval = Mathf.Max(minTurnInterval, baseTurnInterval - Mathf.FloorToInt((bossLevel - 1) * 0.2f));
        currentBossDamage = Mathf.Min(maxDamage, baseDamage + (bossLevel - 1));
        currentTurnCount = currentTurnInterval;

        UpdateUI(true);

        // ⭐ NEW: UI 활성화
        SetBossUIActive(true);

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

    void SelectNextBossImage()
    {
        if (bossSprites.Count == 0)
        {
            Debug.LogWarning("No boss sprites assigned!");
            return;
        }

        if (bossSprites.Count == 1)
        {
            // ⭐ 이미지 1개: 팔레트 스왑
            ApplyRandomPaletteSwap();
        }
        else
        {
            // ⭐ 이미지 여러 개: 순환
            currentBossIndex = (currentBossIndex + 1) % bossSprites.Count;
            bossImageArea.sprite = bossSprites[currentBossIndex];

            // 처음으로 돌아갔으면 팔레트 스왑
            if (currentBossIndex == 0)
            {
                ApplyRandomPaletteSwap();
            }
        }
    }
    void ApplyRandomPaletteSwap()
    {
        if (bossImageArea == null) return;

        // ⭐ 랜덤 색상 (채도 높은 색상)
        Color randomColor = Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.8f, 1f);

        // ⭐ 팔레트 스왑 (Material 사용)
        Material mat = new Material(Shader.Find("UI/Default"));
        mat.SetColor("_Color", randomColor);
        bossImageArea.material = mat;

        Debug.Log($"Boss palette swapped to {randomColor}");
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