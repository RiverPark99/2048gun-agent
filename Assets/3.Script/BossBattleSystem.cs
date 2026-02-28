// =====================================================
// BossBattleSystem.cs - v7.1
// Boss 전투, 데미지, 게임오버, Challenge Clear UI
// Pause 시스템, Continue 횟수 제한, 데미지 텍스트 N0 포맷
// =====================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using DG.Tweening;

public class BossBattleSystem : MonoBehaviour
{
    [Header("Boss References")]
    [SerializeField] private BossManager bossManager;
    [SerializeField] private TextMeshProUGUI hpText;

    [Header("Game Over UI")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button continueButton;

    [Header("Pause UI")]
    [SerializeField] private Button pauseButton;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button pauseResumeButton;
    [SerializeField] private Button pauseRestartButton;
    [SerializeField] private Button pauseGoToTitleButton;
    [SerializeField] private string titleSceneName = "Title";

    [Header("Damage Text")]
    [SerializeField] private GameObject damageTextPrefab;
    [SerializeField] private Transform damageTextParent;

    [Header("Damage Text 위치 오프셋 (Y축, 위로 올리려면 양수)")]
    [SerializeField] private float dmgTextYOffset = 0f;

    [Header("Low Health Effect")]
    [SerializeField] private LowHealthVignette lowHealthVignette;

    [Header("Challenge Clear UI")]
    [SerializeField] private GameObject challengeClearPanel;
    [SerializeField] private TextMeshProUGUI clearStatsText;
    [SerializeField] private TextMeshProUGUI clearBestRecordText;
    [SerializeField] private TextMeshProUGUI clearNewRecordText;

    [Header("Combo Laser 색상 (1회, 2회, 3회, 4회, 5회+)")]
    [SerializeField] private Color laserColor1 = Color.white;
    [SerializeField] private Color laserColor2 = new Color(0.5f, 1f, 0.5f);
    [SerializeField] private Color laserColor3 = new Color(1f, 0.6f, 0f);
    [SerializeField] private Color laserColor4 = new Color(1f, 0.3f, 0f);
    [SerializeField] private Color laserColor5Plus = new Color(1f, 0f, 1f);
    [SerializeField] private Color laserColorFever = new Color(1f, 0.5f, 0f);

    [Header("Damage 텍스트 색상 (1회, 2회, 3회, 4회, 5회+)")]
    [SerializeField] private Color dmgTextColor1 = Color.white;
    [SerializeField] private Color dmgTextColor2 = new Color(0.5f, 1f, 0.5f);
    [SerializeField] private Color dmgTextColor3 = new Color(1f, 0.6f, 0f);
    [SerializeField] private Color dmgTextColor4 = new Color(1f, 0.3f, 0f);
    [SerializeField] private Color dmgTextColor5Plus = new Color(1f, 0f, 1f);

    [Header("Damage 텍스트 폰트 크기")]
    [SerializeField] private float dmgFontSize1 = 48f;
    [SerializeField] private float dmgFontSizeGun = 54f;
    [SerializeField] private float dmgFontSizeComboBase = 48f;
    [SerializeField] private float dmgFontSizeComboPerLevel = 2f;
    [SerializeField] private float dmgFontSizeComboMax = 60f;

    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private GunSystem gunSystem;
    [SerializeField] private PlayerHPSystem playerHP;
    [SerializeField] private UnlockManager unlockManager;

    private bool isBossAttacking = false;
    private bool isBossTransitioning = false;
    private bool isGameOver = false;
    private bool isChallengeClearShown = false;
    private bool isPaused = false;

    private ProjectileManager projectileManager;
    private Sequence clearNewRecordAnim;

    // DamageText Object Pool
    private GameObjectPool _damageTextPool;

    public bool IsBossAttacking => isBossAttacking;
    public bool IsBossTransitioning => isBossTransitioning;
    public bool IsGameOver => isGameOver;
    public bool IsChallengeClearShown => isChallengeClearShown;
    public bool IsPaused => isPaused;
    public LowHealthVignette LowHealthVignette => lowHealthVignette;

    public void Initialize()
    {
        projectileManager = FindAnyObjectByType<ProjectileManager>();

        // DamageText pool 초기화 (워밍 4개)
        if (damageTextPrefab != null && damageTextParent != null)
            _damageTextPool = new GameObjectPool(damageTextPrefab, damageTextParent, 4);

        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);
        if (continueButton != null)
            continueButton.onClick.AddListener(ContinueGame);
        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);

        // Pause 버튼
        if (pauseButton != null)
            pauseButton.onClick.AddListener(TogglePause);
        if (pauseResumeButton != null)
            pauseResumeButton.onClick.AddListener(ResumePause);
        if (pauseRestartButton != null)
            pauseRestartButton.onClick.AddListener(PauseRestart);
        if (pauseGoToTitleButton != null)
            pauseGoToTitleButton.onClick.AddListener(PauseGoToTitle);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        if (challengeClearPanel != null)
            challengeClearPanel.SetActive(false);
        if (pausePanel != null)
            pausePanel.SetActive(false);

        isPaused = false;
        UpdateContinueButtonState();
    }

    public void ResetState()
    {
        isGameOver = false;
        isBossAttacking = false;
        isBossTransitioning = false;
        isChallengeClearShown = false;
        isPaused = false;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (challengeClearPanel != null) challengeClearPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
    }

    public ProjectileManager GetProjectileManager() { return projectileManager; }

    // === Pause ===
    public void TogglePause()
    {
        if (isGameOver || isChallengeClearShown) return;
        if (isPaused) ResumePause();
        else
        {
            isPaused = true;
            Time.timeScale = 0f;
            if (pausePanel != null) pausePanel.SetActive(true);
        }
    }

    void ResumePause()
    {
        isPaused = false;
        Time.timeScale = 1f;
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    void PauseRestart()
    {
        isPaused = false;
        Time.timeScale = 1f;
        if (pausePanel != null) pausePanel.SetActive(false);
        RestartGame();
    }

    void PauseGoToTitle()
    {
        isPaused = false;
        Time.timeScale = 1f;
        if (pausePanel != null) pausePanel.SetActive(false);
        DOTween.KillAll();
        if (!string.IsNullOrEmpty(titleSceneName))
            SceneManager.LoadScene(titleSceneName);
        else
            SceneManager.LoadScene(0);
    }

    // === Boss 데미지 투사체 발사 ===
    public void FireDamageProjectile(Vector3 fromPos, long damage, int mergeCount, bool isFever)
    {
        if (projectileManager != null && bossManager != null && bossManager.bossImageArea != null)
        {
            RectTransform monsterRect = bossManager.bossImageArea.GetComponent<RectTransform>();
            Vector3 bossPos = monsterRect.position;

            Color laserColor = laserColor1;
            if (isFever) laserColor = laserColorFever;
            else if (mergeCount >= 5) laserColor = laserColor5Plus;
            else if (mergeCount >= 4) laserColor = laserColor4;
            else if (mergeCount >= 3) laserColor = laserColor3;
            else if (mergeCount >= 2) laserColor = laserColor2;

            projectileManager.FireKnifeProjectile(fromPos, bossPos, laserColor, () =>
            {
                bossManager.TakeDamage(damage);
                ShowDamageText(damage, mergeCount, false);
                CameraShake.Instance?.ShakeLight();
            });
        }
        else
        {
            if (bossManager != null)
            {
                bossManager.TakeDamage(damage);
                ShowDamageText(damage, mergeCount, false);
            }
        }
    }

    // === 데미지 텍스트 (N0 포맷, 반짝+올라감 동시) ===
    public void ShowDamageText(long damage, int comboNum, bool isGunDamage, bool isChoco = false)
    {
        if (damageTextParent == null || hpText == null) return;
        if (_damageTextPool == null && damageTextPrefab == null) return;

        // Pool에서 가져오기 (폴 없으면 Instantiate 폴백)
        GameObject damageObj = _damageTextPool != null
            ? _damageTextPool.Get(damageTextParent)
            : Instantiate(damageTextPrefab, damageTextParent);

        // 풀에서 꾼낼 때 잘로 남은 상태 방어적 초기화
        CanvasGroup existCG = damageObj.GetComponent<CanvasGroup>();
        if (existCG != null) { existCG.DOKill(); existCG.alpha = 1f; }
        RectTransform existRT = damageObj.GetComponent<RectTransform>();
        if (existRT != null) { existRT.DOKill(); existRT.localScale = Vector3.one; }
        TextMeshProUGUI existTMP = damageObj.GetComponent<TextMeshProUGUI>();
        if (existTMP != null)
        {
            existTMP.DOKill();
            Color ec = existTMP.color; ec.a = 1f; existTMP.color = ec;
        }

        damageObj.transform.SetAsLastSibling();
        TextMeshProUGUI damageText = damageObj.GetComponent<TextMeshProUGUI>();

        if (damageText != null)
        {
            // 1콤보 이하도 \n + 수치로 줄맞춤
            if (isGunDamage)
            {
                damageText.text = $"\n{damage:N0}";
                damageText.color = isChoco ? new Color(1f, 0.84f, 0f) : Color.yellow;
                damageText.fontSize = dmgFontSizeGun;
            }
            else
            {
                if (comboNum >= 2)
                {
                    damageText.text = $"{comboNum} Combo!\n{damage:N0}";
                    if (comboNum >= 5) damageText.color = dmgTextColor5Plus;
                    else if (comboNum >= 4) damageText.color = dmgTextColor4;
                    else if (comboNum >= 3) damageText.color = dmgTextColor3;
                    else damageText.color = dmgTextColor2;
                    damageText.fontSize = Mathf.Min(dmgFontSizeComboBase + comboNum * dmgFontSizeComboPerLevel, dmgFontSizeComboMax);
                }
                else
                {
                    damageText.text = $"\n{damage:N0}";
                    damageText.color = dmgTextColor1;
                    damageText.fontSize = dmgFontSize1;
                }
            }

            RectTransform damageRect = damageObj.GetComponent<RectTransform>();
            RectTransform hpTextRect = hpText.GetComponent<RectTransform>();
            damageRect.position = hpTextRect.position;
            // Y 오프셋 적용
            if (dmgTextYOffset != 0f)
                damageRect.anchoredPosition += new Vector2(0f, dmgTextYOffset);

            CanvasGroup canvasGroup = damageObj.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = damageObj.AddComponent<CanvasGroup>();

            // 반짝 + 스케일축소 + 올라가며 페이드아웃 (모두 동시)
            Color finalColor = damageText.color;
            Color flashWhite = new Color(1f, 0.95f, 0.8f);
            damageRect.localScale = Vector3.one * 1.5f;
            float startY = damageRect.anchoredPosition.y;

            Sequence seq = DOTween.Sequence();
            // 반짝반짝 3회 (색상, 0.44초) — 동시에 스케일 복귀 + 올라감 + 페이드 시작
            seq.Append(damageText.DOColor(flashWhite, 0.08f).SetEase(Ease.InOutSine));
            seq.Append(damageText.DOColor(finalColor, 0.08f).SetEase(Ease.InOutSine));
            seq.Append(damageText.DOColor(flashWhite, 0.08f).SetEase(Ease.InOutSine));
            seq.Append(damageText.DOColor(finalColor, 0.08f).SetEase(Ease.InOutSine));
            seq.Append(damageText.DOColor(flashWhite, 0.06f).SetEase(Ease.InOutSine));
            seq.Append(damageText.DOColor(finalColor, 0.06f).SetEase(Ease.InOutSine));
            // 스케일: 0초부터 0.3초간 1.5→1.0 (반짝과 동시)
            seq.Insert(0f, damageRect.DOScale(1f, 0.3f).SetEase(Ease.OutBack));
            // 올라감: 0초부터 전체 1.2초
            seq.Insert(0f, damageRect.DOAnchorPosY(startY + 120f, 1.2f).SetEase(Ease.OutCubic));
            // 페이드: 0.5초부터 (반짝 끝난 뒤) 0.7초간
            seq.Insert(0.5f, canvasGroup.DOFade(0f, 0.7f).SetEase(Ease.InCubic));
            seq.OnComplete(() =>
            {
                if (damageObj == null) return;

                // 풀 반환 전 시각적 상태 완전 초기화
                // 1) DOTween 잔여 kill (중간값 방지)
                damageRect.DOKill();
                damageText.DOKill();
                canvasGroup.DOKill();

                // 2) 모든 시각 상태 복원
                canvasGroup.alpha     = 1f;
                damageRect.localScale = Vector3.one;

                // 3) TMP color alpha 복원 (DOColor 중간에 kill되면 중간 alpha 잘로 남음)
                Color tc = damageText.color;
                tc.a = 1f;
                damageText.color = tc;

                if (_damageTextPool != null)
                    _damageTextPool.Return(damageObj);
                else
                    Destroy(damageObj);
            });
        }
    }

    public void SetBossAttacking(bool attacking) { isBossAttacking = attacking; }

    public void SetBossTransitioning(bool transitioning)
    {
        isBossTransitioning = transitioning;
        if (!transitioning) gunSystem.UpdateGunUI();
    }

    public void TakeBossAttack(int damage)
    {
        playerHP.TakeDamage(damage);
        if (playerHP.CurrentHeat <= 0) GameOver();
    }

    // === Boss 처치 ===
    public void OnBossDefeated()
    {
        int currentStage = bossManager != null ? bossManager.GetBossLevel() : 0;
        bool isClear = bossManager != null && bossManager.IsClearMode();

        playerHP.OnBossDefeated(currentStage, isClear);

        if (gunSystem.IsFeverMode)
            StartCoroutine(gunSystem.SyncFreezeWithBossRespawn());

        if (currentStage == 39)
            gridManager.ResetInfiniteBossMoveCount();
    }

    public void UpdateInfiniteBossEnemyBarColor()
    {
        if (bossManager == null) return;
        if (bossManager.hpSlider != null)
        {
            Image fillImage = bossManager.hpSlider.fillRect?.GetComponent<Image>();
            if (fillImage != null)
            {
                if (bossManager.IsGuardMode())
                    fillImage.color = new Color(1f, 0.25f, 0.25f);
            }
        }
    }

    // === 게임 오버 ===
    public void GameOver()
    {
        isGameOver = true;
        Debug.Log("Game Over!");

        gunSystem.CleanupFeverEffects();
        gunSystem.UpdateGunUI();

        UpdateContinueButtonState();

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            CanvasGroup canvasGroup = gameOverPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameOverPanel.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.DOFade(1f, 1f).SetDelay(2f).SetEase(Ease.InOutQuad);
        }
    }

    // === Continue (횟수 제한) ===
    void ContinueGame()
    {
        if (!isGameOver) return;
        if (!gunSystem.CanContinue()) return;

        gunSystem.UseContinue();
        isGameOver = false;
        gridManager.IsProcessing = false;

        playerHP.SetHeatToMax();
        playerHP.UpdateHeatUI(true);
        gunSystem.ContinueIntoFever();

        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        UpdateContinueButtonState();

        if (!gridManager.CanMove())
        {
            bool hasGun = gunSystem.HasBullet || (gunSystem.IsFeverMode && !gunSystem.FeverBulletUsed);
            if (hasGun) gunSystem.SetEmergencyFlash(true);
        }
    }

    void UpdateContinueButtonState()
    {
        if (continueButton == null) return;
        bool canCont = gunSystem != null && gunSystem.CanContinue();
        continueButton.interactable = canCont;
        Image btnImg = continueButton.GetComponent<Image>();
        if (btnImg != null)
        {
            Color c = btnImg.color;
            c.a = canCont ? 1f : 0.4f;
            btnImg.color = c;
        }
    }

    public void RestartGame()
    {
        StartCoroutine(RestartGameCoroutine());
    }

    private IEnumerator RestartGameCoroutine()
    {
        yield return new WaitForSecondsRealtime(0.6f);
        DOTween.KillAll();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public bool ShouldBlockInput()
    {
        return isGameOver || isPaused || gridManager.IsProcessing || isBossTransitioning || isBossAttacking || isChallengeClearShown
            || (unlockManager != null && unlockManager.IsUnlockAnimating);
    }

    // =====================================================
    // Challenge Clear UI
    // =====================================================

    public void ShowChallengeClearUI()
    {
        isChallengeClearShown = true;

        if (challengeClearPanel != null)
        {
            challengeClearPanel.SetActive(true);
            CanvasGroup cg = challengeClearPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = challengeClearPanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.DOFade(1f, 0.5f).SetEase(Ease.InOutQuad);
        }

        // Total Damage + Turn 표시
        if (clearStatsText != null)
        {
            long totalDmg = gunSystem != null ? gunSystem.PermanentAttackPower : 0;
            // Freeze total damage가 더 정확
            string bestStr = PlayerPrefs.GetString("BestFreezeDamage", "0");
            long bestRecord = 0;
            long.TryParse(bestStr, out bestRecord);

            int clearTurn = gridManager != null ? gridManager.CurrentTurn : 0;
            clearStatsText.text = $"Total Damage: {bestRecord:N0}\nTurn: {clearTurn}";
        }

        // Challenge 최고기록
        if (clearBestRecordText != null)
        {
            string bestStr = PlayerPrefs.GetString("BestClearTurn", "");
            int prevBestTurn = 0;
            if (!string.IsNullOrEmpty(bestStr)) int.TryParse(bestStr, out prevBestTurn);

            int currentTurn = gridManager != null ? gridManager.CurrentTurn : 0;
            bool isNewRecord = (prevBestTurn == 0 || currentTurn < prevBestTurn);

            if (isNewRecord)
            {
                PlayerPrefs.SetString("BestClearTurn", currentTurn.ToString());
                PlayerPrefs.Save();
                clearBestRecordText.text = $"Best: {currentTurn} Turns";
            }
            else
            {
                clearBestRecordText.text = $"Best: {prevBestTurn} Turns";
            }

            // New Record 표시 + 색상 루프
            if (clearNewRecordText != null)
            {
                if (isNewRecord)
                {
                    clearNewRecordText.gameObject.SetActive(true);
                    clearNewRecordText.text = "NEW RECORD!";
                    Color goldA = new Color(1f, 0.84f, 0f);
                    Color goldB = new Color(1f, 0.5f, 0f);
                    clearNewRecordText.color = goldA;

                    if (clearNewRecordAnim != null) clearNewRecordAnim.Kill();
                    clearNewRecordAnim = DOTween.Sequence();
                    clearNewRecordAnim.Append(clearNewRecordText.DOColor(goldB, 0.6f).SetEase(Ease.InOutSine));
                    clearNewRecordAnim.Append(clearNewRecordText.DOColor(goldA, 0.6f).SetEase(Ease.InOutSine));
                    clearNewRecordAnim.SetLoops(-1, LoopType.Restart);
                }
                else
                {
                    clearNewRecordText.gameObject.SetActive(false);
                }
            }
        }

        SpawnClearFirework();
        Debug.Log("Challenge Clear UI!");
    }

    void SpawnClearFirework()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject fwObj = new GameObject("ClearFirework");
        fwObj.transform.SetParent(canvas.transform, false);
        RectTransform fwRect = fwObj.AddComponent<RectTransform>();
        fwRect.anchorMin = new Vector2(0.5f, 0.75f);
        fwRect.anchorMax = new Vector2(0.5f, 0.75f);
        fwRect.sizeDelta  = Vector2.zero;

        ParticleSystem ps = fwObj.AddComponent<ParticleSystem>();

        // ParticleScaler 기반 설정
        ParticleScaler.ApplyMergeParticleSettings(
            ps,
            color:       new Color(1f, 0.84f, 0f),
            startSize:   20f / ParticleScaler.SmallCorrection,
            startSpeed:  200f,
            lifetime:    1.0f,
            shapeRadius: 30f,
            minBurst:    60,
            maxBurst:    100
        );

        // Shape 켜 술 줄 수정 (Circle 반경 30)
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 30f;

        // 파이어워크 전용 Gradient (3색)
        var colorOL = ps.colorOverLifetime;
        colorOL.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.84f, 0f), 0f),
                new GradientColorKey(new Color(1f, 0.5f,  0f), 0.5f),
                new GradientColorKey(new Color(1f, 0.2f, 0.2f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f,   0f),
                new GradientAlphaKey(0.8f, 0.5f),
                new GradientAlphaKey(0f,   1f)
            });
        colorOL.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleScaler.AddUIParticle(fwObj);
        ps.Play();
        Destroy(fwObj, 2f);
    }

    public void OnClearResume()
    {
        isChallengeClearShown = false;
        if (challengeClearPanel != null) challengeClearPanel.SetActive(false);
    }

    public void OnClearGoToTitle()
    {
        isChallengeClearShown = false;
        if (challengeClearPanel != null) challengeClearPanel.SetActive(false);
        RestartGame();
    }

    public void OnClearRestart()
    {
        isChallengeClearShown = false;
        if (challengeClearPanel != null) challengeClearPanel.SetActive(false);
        RestartGame();
    }
}
