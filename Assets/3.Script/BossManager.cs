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
    [SerializeField] private int damageThreshold = 40; // 40 이상부터 천천히 증가
    [SerializeField] private int slowIncreaseRate = 4; // 4번 쓰러뜨릴 때마다 1씩

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
    private Tweener bossIdleAnimation; // ⭐ NEW: 보스 오뚜기 애니메이션
    private Sequence attackBlinkAnimation; // ⭐ NEW: 공격 시 점멸 애니메이션

    void Start()
    {
        gameManager = FindAnyObjectByType<GameManager>();
        InitializeBoss();
        StartBossIdleAnimation(); // ⭐ NEW: 보스 오뚜기 애니메이션 시작
    }

    void InitializeBoss()
    {
        float exponent = Mathf.Pow(1.5f, bossLevel - 1);
        maxHP = baseHP + Mathf.RoundToInt(hpIncreasePerLevel * (exponent - 1f) / 0.5f);

        currentHP = maxHP;

        currentTurnInterval = Mathf.Max(minTurnInterval, baseTurnInterval - Mathf.FloorToInt((bossLevel - 1) * 0.2f));

        // ⭐ UPDATED: 공격력 계산 (40 이상부터 천천히)
        if (bossLevel < damageThreshold)
        {
            // 40 미만: 기존 방식 (1씩 증가)
            currentBossDamage = baseDamage + (bossLevel - 1);
        }
        else
        {
            // 40 이상: 4번마다 1씩 증가
            int slowIncreaseCount = (bossLevel - damageThreshold) / slowIncreaseRate;
            currentBossDamage = baseDamage + (damageThreshold - 1) + slowIncreaseCount;
        }

        currentTurnCount = currentTurnInterval;

        // ⭐ FIXED: 중복 제거
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

    // ⭐ NEW: 턴 추가 (Fever 총 사용 시)
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

        // ⭐ NEW: In: 0 표시 + 점멸 시작
        if (bossAttackInfoText != null)
        {
            bossAttackInfoText.text = $"ATK: {currentBossDamage} | In: 0";

            // 빨강/흰색 점멸 애니메이션
            if (attackBlinkAnimation != null)
            {
                attackBlinkAnimation.Kill();
            }

            attackBlinkAnimation = DOTween.Sequence()
                .Append(bossAttackInfoText.DOColor(Color.red, 0.2f))
                .Append(bossAttackInfoText.DOColor(Color.white, 0.2f))
                .SetLoops(-1, LoopType.Restart);
        }

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
    

        // ⭐ NEW: 점멸 중지 + 턴 초기화
        if (attackBlinkAnimation != null)
        {
            attackBlinkAnimation.Kill();
            attackBlinkAnimation = null;
        }

// 턴 초기화 (공격 후)
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

    // ⭐ NEW: UI 비활성화
    SetBossUIActive(false);

    // ⭐ NEW: 오뚜기 애니메이션 정지
    StopBossIdleAnimation();

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
        // ⭐ FIXED: sprite가 null이면 기본 스프라이트 설정
        if (bossImageArea.sprite == null && bossSprites.Count > 0)
        {
            bossImageArea.sprite = bossSprites[0];
        }

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

    // ⭐ UPDATED: 공격력 계산 (40 이상부터 천천히)
    if (bossLevel < damageThreshold)
    {
        currentBossDamage = baseDamage + (bossLevel - 1);
    }
    else
    {
        int slowIncreaseCount = (bossLevel - damageThreshold) / slowIncreaseRate;
        currentBossDamage = baseDamage + (damageThreshold - 1) + slowIncreaseCount;
    }

    currentTurnCount = currentTurnInterval;

    UpdateUI(true);

    // ⭐ NEW: UI 활성화
    SetBossUIActive(true);

    // ⭐ NEW: 오뚜기 애니메이션 재시작
    StartBossIdleAnimation();

    if (gameManager != null)
    {
        gameManager.SetBossTransitioning(false);
    }

    isTransitioning = false;
}

public void ResetBoss()
{
    bossLevel = 1;
    currentBossIndex = 0; // ⭐ FIXED: 보스 이미지 인덱스 초기화

    // ⭐ FIXED: 보스 이미지 복원
    if (bossImageArea != null && bossSprites.Count > 0)
    {
        bossImageArea.sprite = bossSprites[0];
        bossImageArea.color = Color.white;
        bossImageArea.material = null; // 팔레트 스왑 제거
        bossImageArea.transform.localScale = Vector3.one;
    }

    InitializeBoss();
    isTransitioning = false;

    // ⭐ FIXED: UI 활성화 (게임 시작 시)
    StartCoroutine(ShowBossUIAfterDelay());

    // ⭐ NEW: 오뚜기 애니메이션 재시작
    StartBossIdleAnimation();
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
        // ⭐ 이미지 1개: 팔레트 스왑
        if (bossImageArea.sprite == null)
        {
            bossImageArea.sprite = bossSprites[0];
        }
        ApplyBrightnessBasedPaletteSwap();
    }
    else
    {
        // ⭐ 이미지 여러 개: 순환
        currentBossIndex = (currentBossIndex + 1) % bossSprites.Count;

        // ⭐ FIXED: null 체크
        if (bossSprites[currentBossIndex] != null)
        {
            bossImageArea.sprite = bossSprites[currentBossIndex];
        }
        else
        {
            Debug.LogWarning($"Boss sprite at index {currentBossIndex} is null!");
        }

        // 처음으로 돌아갔으면 팔레트 스왑
        if (currentBossIndex == 0)
        {
            ApplyBrightnessBasedPaletteSwap();
        }
    }
}

// ⭐ UPDATED: 밝기만 변경 (흰색→검은색, 최대 0.3까지)
void ApplyBrightnessBasedPaletteSwap()
{
    if (bossImageArea == null) return;

    // bossLevel이 높을수록 어두워짐 (최대 70%까지)
    float brightness = Mathf.Max(0.3f, 1.0f - (bossLevel * 0.02f));
    Color grayscaleColor = new Color(brightness, brightness, brightness, 1f);

    // ⭐ 팔레트 스왑 (Material 사용)
    Material mat = new Material(Shader.Find("UI/Default"));
    mat.SetColor("_Color", grayscaleColor);
    bossImageArea.material = mat;

    Debug.Log($"Boss palette swapped to brightness: {brightness}");
}

// ⭐ NEW: 보스 오뚜기 애니메이션
void StartBossIdleAnimation()
{
    if (bossImageArea == null) return;

    // 기존 애니메이션 정리
    if (bossIdleAnimation != null)
    {
        bossIdleAnimation.Kill();
    }

    // 좌우 회전 애니메이션 (-5도 ~ +5도)
    bossIdleAnimation = bossImageArea.transform.DOLocalRotate(
        new Vector3(0f, 0f, 5f), // 5도 회전
        2.0f // 2초 동안
    )
    .SetEase(Ease.InOutSine)
    .SetLoops(-1, LoopType.Yoyo); // 무한 반복

    Debug.Log("Boss idle animation started!");
}

// ⭐ NEW: 보스 오뚜기 애니메이션 정지
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