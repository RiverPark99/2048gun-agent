// =====================================================
// BossBattleSystem.cs - v6.6
// Boss 전투, 데미지, 게임오버, Challenge Clear UI
// Continue 횟수 제한, 데미지 텍스트 N0 포맷
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

    [Header("Damage Text")]
    [SerializeField] private GameObject damageTextPrefab;
    [SerializeField] private Transform damageTextParent;

    [Header("Low Health Effect")]
    [SerializeField] private LowHealthVignette lowHealthVignette;

    [Header("Challenge Clear UI")]
    [SerializeField] private GameObject challengeClearPanel;
    [SerializeField] private TextMeshProUGUI clearStatsText;

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

    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private GunSystem gunSystem;
    [SerializeField] private PlayerHPSystem playerHP;
    [SerializeField] private UnlockManager unlockManager;

    private bool isBossAttacking = false;
    private bool isBossTransitioning = false;
    private bool isGameOver = false;
    private bool isChallengeClearShown = false;

    private ProjectileManager projectileManager;

    public bool IsBossAttacking => isBossAttacking;
    public bool IsBossTransitioning => isBossTransitioning;
    public bool IsGameOver => isGameOver;
    public bool IsChallengeClearShown => isChallengeClearShown;
    public LowHealthVignette LowHealthVignette => lowHealthVignette;

    public void Initialize()
    {
        projectileManager = FindAnyObjectByType<ProjectileManager>();

        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);
        if (continueButton != null)
            continueButton.onClick.AddListener(ContinueGame);
        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        if (challengeClearPanel != null)
            challengeClearPanel.SetActive(false);

        UpdateContinueButtonState();
    }

    public void ResetState()
    {
        isGameOver = false;
        isBossAttacking = false;
        isBossTransitioning = false;
        isChallengeClearShown = false;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (challengeClearPanel != null) challengeClearPanel.SetActive(false);
    }

    public ProjectileManager GetProjectileManager() { return projectileManager; }

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

    // === 데미지 텍스트 (N0 포맷) ===
    public void ShowDamageText(long damage, int comboNum, bool isGunDamage, bool isChoco = false)
    {
        if (damageTextPrefab == null || damageTextParent == null || hpText == null) return;

        GameObject damageObj = Instantiate(damageTextPrefab, damageTextParent);
        TextMeshProUGUI damageText = damageObj.GetComponent<TextMeshProUGUI>();

        if (damageText != null)
        {
            if (isGunDamage)
            {
                damageText.text = $"-{damage:N0}";
                damageText.color = isChoco ? new Color(1f, 0.84f, 0f) : Color.yellow;
                damageText.fontSize = 54;
            }
            else
            {
                if (comboNum >= 2)
                {
                    damageText.text = $"{comboNum} Combo!\n-{damage:N0}";
                    if (comboNum >= 5) damageText.color = dmgTextColor5Plus;
                    else if (comboNum >= 4) damageText.color = dmgTextColor4;
                    else if (comboNum >= 3) damageText.color = dmgTextColor3;
                    else damageText.color = dmgTextColor2;
                    damageText.fontSize = Mathf.Min(48 + comboNum * 2, 60);
                }
                else
                {
                    damageText.text = $"-{damage:N0}";
                    damageText.color = dmgTextColor1;
                    damageText.fontSize = 48;
                }
            }

            RectTransform damageRect = damageObj.GetComponent<RectTransform>();
            RectTransform hpTextRect = hpText.GetComponent<RectTransform>();
            damageRect.position = hpTextRect.position;

            CanvasGroup canvasGroup = damageObj.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = damageObj.AddComponent<CanvasGroup>();

            Sequence damageSequence = DOTween.Sequence();
            damageSequence.Append(damageRect.DOAnchorPosY(damageRect.anchoredPosition.y + 150f, 1.2f).SetEase(Ease.OutCubic));
            damageSequence.Join(canvasGroup.DOFade(0f, 1.2f).SetEase(Ease.InCubic));
            damageSequence.Insert(0f, damageRect.DOScale(1.2f, 0.15f).SetEase(Ease.OutQuad));
            damageSequence.Insert(0.15f, damageRect.DOScale(1f, 0.15f).SetEase(Ease.InQuad));
            damageSequence.OnComplete(() => { if (damageObj != null) Destroy(damageObj); });
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

        // v6.6: Continue 버튼 상태 업데이트
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

    // v6.6: Continue 버튼 활성/비활성
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
        return isGameOver || gridManager.IsProcessing || isBossTransitioning || isBossAttacking || isChallengeClearShown
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

        if (clearStatsText != null)
        {
            long clearScore = gridManager != null ? gridManager.Score : 0;
            int clearTurn = gridManager != null ? gridManager.CurrentTurn : 0;
            clearStatsText.text = $"Score: {clearScore:N0}\nTurn: {clearTurn}";
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
        fwRect.anchorMin = new Vector2(0.5f, 0.75f); fwRect.anchorMax = new Vector2(0.5f, 0.75f);
        fwRect.sizeDelta = Vector2.zero;

        ParticleSystem ps = fwObj.AddComponent<ParticleSystem>();
        float psc = Tile.SmallParticleSizeCorrectionStatic();
        var main = ps.main;
        main.startLifetime = 1.0f; main.startSpeed = 200f; main.startSize = 20f / psc;
        main.maxParticles = 100; main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.playOnAwake = false; main.loop = false;

        var emission = ps.emission; emission.enabled = true; emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 60, 100) });

        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 30f;

        var colorOL = ps.colorOverLifetime; colorOL.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.84f, 0f), 0f), new GradientColorKey(new Color(1f, 0.5f, 0f), 0.5f), new GradientColorKey(new Color(1f, 0.2f, 0.2f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) }
        );
        colorOL.color = new ParticleSystem.MinMaxGradient(gradient);

        var sizeOL = ps.sizeOverLifetime; sizeOL.enabled = true;
        AnimationCurve curve = new AnimationCurve(); curve.AddKey(0f, 1f); curve.AddKey(1f, 0f);
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = new Material(Shader.Find("UI/Default"));

        float pScale = 3f * ((float)Screen.width / 498f);
        var uiP = fwObj.AddComponent<Coffee.UIExtensions.UIParticle>(); uiP.scale = pScale;
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
