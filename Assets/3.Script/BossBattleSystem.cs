// =====================================================
// BossBattleSystem.cs - v6.3
// Boss 전투, 데미지, 게임오버, Challenge Clear UI
// Restart → Scene 재로드 (0.6초 딜레이)
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

    [Header("색상 조합 보너스")]
    [SerializeField] private int chocoMergeDamageMultiplier = 4;
    // ⭐ v6.3: Freeze 데미지 2배 상향 (1.5→2.0)
    [SerializeField] private float feverDamageMultiplier = 2.0f;

    [Header("Low Health Effect")]
    [SerializeField] private LowHealthVignette lowHealthVignette;

    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private GunSystem gunSystem;
    [SerializeField] private PlayerHPSystem playerHP;

    private bool isBossAttacking = false;
    private bool isBossTransitioning = false;
    private bool isGameOver = false;
    private bool isChallengeClearShown = false;

    private ProjectileManager projectileManager;

    public bool IsBossAttacking => isBossAttacking;
    public bool IsBossTransitioning => isBossTransitioning;
    public bool IsGameOver => isGameOver;
    public bool IsChallengeClearShown => isChallengeClearShown;
    public int ChocoMergeDamageMultiplier => chocoMergeDamageMultiplier;
    public float FeverDamageMultiplier => feverDamageMultiplier;
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
    }

    public void ResetState()
    {
        isGameOver = false;
        isBossAttacking = false;
        isBossTransitioning = false;
        isChallengeClearShown = false;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    public ProjectileManager GetProjectileManager() { return projectileManager; }

    // === Boss 데미지 투사체 발사 ===
    public void FireDamageProjectile(Vector3 fromPos, long damage, int mergeCount, bool isFever)
    {
        if (projectileManager != null && bossManager != null && bossManager.bossImageArea != null)
        {
            RectTransform monsterRect = bossManager.bossImageArea.GetComponent<RectTransform>();
            Vector3 bossPos = monsterRect.position;

            Color laserColor = Color.white;
            if (isFever) laserColor = new Color(1f, 0.5f, 0f);
            else if (mergeCount >= 5) laserColor = new Color(1f, 0f, 1f);
            else if (mergeCount >= 4) laserColor = new Color(1f, 0.3f, 0f);
            else if (mergeCount >= 3) laserColor = new Color(1f, 0.6f, 0f);
            else if (mergeCount >= 2) laserColor = new Color(0.5f, 1f, 0.5f);

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

    // === 데미지 텍스트 ===
    public void ShowDamageText(long damage, int comboNum, bool isGunDamage, bool isChoco = false)
    {
        if (damageTextPrefab == null || damageTextParent == null || hpText == null) return;

        GameObject damageObj = Instantiate(damageTextPrefab, damageTextParent);
        TextMeshProUGUI damageText = damageObj.GetComponent<TextMeshProUGUI>();

        if (damageText != null)
        {
            if (isGunDamage)
            {
                damageText.text = $"-{damage}";
                damageText.color = isChoco ? new Color(1f, 0.84f, 0f) : Color.yellow;
                damageText.fontSize = 54;
            }
            else
            {
                if (comboNum >= 2)
                {
                    damageText.text = $"{comboNum} Combo!\n-{damage}";
                    if (comboNum >= 5) damageText.color = new Color(1f, 0f, 1f);
                    else if (comboNum >= 4) damageText.color = new Color(1f, 0.3f, 0f);
                    else if (comboNum >= 3) damageText.color = new Color(1f, 0.6f, 0f);
                    else damageText.color = new Color(0.5f, 1f, 0.5f);
                    damageText.fontSize = Mathf.Min(48 + comboNum * 2, 60);
                }
                else
                {
                    damageText.text = "-" + damage;
                    damageText.color = Color.white;
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

        // ⭐ v6.3: 40번째부터 보스 잡아도 Player 체력 안 오름
        if (currentStage < 40)
            playerHP.OnBossDefeated(currentStage);
        else
            Debug.Log($"Stage {currentStage}: 체력 증가 없음 (40+)");

        if (gunSystem.IsFeverMode)
            StartCoroutine(gunSystem.SyncFreezeWithBossRespawn());

        if (currentStage == 39)
            gridManager.ResetInfiniteBossMoveCount();

        // ⭐ v6.3: Challenge Clear는 Guard 모드에서 ExitGuardMode 시에만 표시
        // Clear 모드에서 추가 보스 처치 시에는 표시 안 함
    }

    // === HP bar 색상 (Guard 모드) ===
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
                // Clear 모드는 HP bar glow 애니메이션이 관리 (BossManager)
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

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            CanvasGroup canvasGroup = gameOverPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameOverPanel.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.DOFade(1f, 1f).SetDelay(2f).SetEase(Ease.InOutQuad);
        }
    }

    // === Continue ===
    void ContinueGame()
    {
        if (!isGameOver) return;
        isGameOver = false;
        gridManager.IsProcessing = false;

        playerHP.SetHeatToMax();
        playerHP.UpdateHeatUI(true);
        gunSystem.ContinueIntoFever();

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    // ⭐ v6.3: Restart → 0.6초 후 현재 Scene 재로드
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
        return isGameOver || gridManager.IsProcessing || isBossTransitioning || isBossAttacking || isChallengeClearShown;
    }

    // =====================================================
    // Challenge Clear UI
    // =====================================================

    public void ShowChallengeClearUI()
    {
        isChallengeClearShown = true;
        CreateChallengeClearPanel();
        SpawnClearFirework();
        Debug.Log("🎉 Challenge Clear UI 표시!");
    }

    void CreateChallengeClearPanel()
    {
        GameObject existing = GameObject.Find("ChallengeClearPanel");
        if (existing != null) Destroy(existing);

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject panelObj = new GameObject("ChallengeClearPanel");
        panelObj.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one; panelRect.sizeDelta = Vector2.zero;

        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0f);
        panelImage.DOFade(0.7f, 0.5f).SetEase(Ease.InOutQuad);

        GameObject titleObj = new GameObject("ClearTitle");
        titleObj.transform.SetParent(panelObj.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.65f); titleRect.anchorMax = new Vector2(0.5f, 0.65f);
        titleRect.sizeDelta = new Vector2(600f, 120f);

        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "Challenge\nClear!";
        titleText.fontSize = 60; titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(1f, 0.84f, 0f); titleText.fontStyle = FontStyles.Bold;

        CreateClearButton(panelObj.transform, "ResumeBtn", "Resume", new Vector2(0.5f, 0.45f), OnClearResume);
        CreateClearButton(panelObj.transform, "GoToTitleBtn", "Title", new Vector2(0.5f, 0.35f), OnClearGoToTitle);
        CreateClearButton(panelObj.transform, "RestartBtn", "Restart", new Vector2(0.5f, 0.25f), OnClearRestart);
    }

    void CreateClearButton(Transform parent, string name, string label, Vector2 anchor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = anchor; btnRect.anchorMax = anchor; btnRect.sizeDelta = new Vector2(300f, 70f);

        Image btnImage = btnObj.AddComponent<Image>();
        btnImage.color = new Color(0.2f, 0.2f, 0.3f, 0.9f);

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImage;
        btn.onClick.AddListener(onClick);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI btnText = textObj.AddComponent<TextMeshProUGUI>();
        btnText.text = label; btnText.fontSize = 36; btnText.alignment = TextAlignmentOptions.Center; btnText.color = Color.white;
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
        var main = ps.main;
        main.startLifetime = 1.0f; main.startSpeed = 200f; main.startSize = 20f;
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

        var uiP = fwObj.AddComponent<Coffee.UIExtensions.UIParticle>(); uiP.scale = 3f;
        ps.Play();
        Destroy(fwObj, 2f);
    }

    void OnClearResume()
    {
        isChallengeClearShown = false;
        GameObject panel = GameObject.Find("ChallengeClearPanel");
        if (panel != null) Destroy(panel);
    }

    void OnClearGoToTitle()
    {
        isChallengeClearShown = false;
        GameObject panel = GameObject.Find("ChallengeClearPanel");
        if (panel != null) Destroy(panel);
        RestartGame();
    }

    void OnClearRestart()
    {
        isChallengeClearShown = false;
        GameObject panel = GameObject.Find("ChallengeClearPanel");
        if (panel != null) Destroy(panel);
        RestartGame();
    }
}
