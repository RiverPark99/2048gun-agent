// =====================================================
// BossBattleSystem.cs - v6.0
// Boss 전투, 데미지, 게임오버, Challenge Clear UI
// =====================================================

using UnityEngine;
using UnityEngine.UI;
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
    [SerializeField] private float feverDamageMultiplier = 1.5f;

    [Header("Low Health Effect")]
    [SerializeField] private LowHealthVignette lowHealthVignette;

    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private GunSystem gunSystem;
    [SerializeField] private PlayerHPSystem playerHP;

    // 상태
    private bool isBossAttacking = false;
    private bool isBossTransitioning = false;
    private bool isGameOver = false;

    // ⭐ v6.0: Challenge Clear UI
    private bool isChallengeClearShown = false;

    private ProjectileManager projectileManager;

    // === 프로퍼티 ===
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

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    public ProjectileManager GetProjectileManager()
    {
        return projectileManager;
    }

    // === Boss 데미지 투사체 발사 ===
    public void FireDamageProjectile(Vector3 fromPos, long damage, int mergeCount, bool isFever)
    {
        if (projectileManager != null && bossManager != null && bossManager.bossImageArea != null)
        {
            RectTransform monsterRect = bossManager.bossImageArea.GetComponent<RectTransform>();
            Vector3 bossPos = monsterRect.position;

            Color laserColor = Color.white;
            if (isFever)
            {
                laserColor = new Color(1f, 0.5f, 0f);
            }
            else if (mergeCount >= 2)
            {
                if (mergeCount >= 5)
                    laserColor = new Color(1f, 0f, 1f);
                else if (mergeCount >= 4)
                    laserColor = new Color(1f, 0.3f, 0f);
                else if (mergeCount >= 3)
                    laserColor = new Color(1f, 0.6f, 0f);
                else
                    laserColor = new Color(0.5f, 1f, 0.5f);
            }

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
                if (isChoco)
                {
                    damageText.text = $"-{damage}";
                    damageText.color = new Color(1f, 0.84f, 0f);
                }
                else
                {
                    damageText.text = $"-{damage}";
                    damageText.color = Color.yellow;
                }
                damageText.fontSize = 54;
            }
            else
            {
                if (comboNum >= 2)
                {
                    damageText.text = $"{comboNum} Combo!\n-{damage}";

                    if (comboNum >= 5)
                        damageText.color = new Color(1f, 0f, 1f);
                    else if (comboNum >= 4)
                        damageText.color = new Color(1f, 0.3f, 0f);
                    else if (comboNum >= 3)
                        damageText.color = new Color(1f, 0.6f, 0f);
                    else
                        damageText.color = new Color(0.5f, 1f, 0.5f);

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
            damageSequence.OnComplete(() => {
                if (damageObj != null) Destroy(damageObj);
            });
        }
    }

    // === Boss Attacking 상태 ===
    public void SetBossAttacking(bool attacking)
    {
        isBossAttacking = attacking;
        Debug.Log($"Boss attacking: {attacking}");
    }

    // === Boss Transitioning 상태 ===
    public void SetBossTransitioning(bool transitioning)
    {
        isBossTransitioning = transitioning;
        Debug.Log($"보스 리스폰 상태: {transitioning}");

        if (!transitioning)
        {
            gunSystem.UpdateGunUI();
            Debug.Log("🔫 Gun UI 업데이트 완료! 버튼 활성화 상태 반영");
        }
    }

    // === 보스 공격 받기 ===
    public void TakeBossAttack(int damage)
    {
        Debug.Log($"💥💥💥 보스 공격 받음! 데미지: {damage} 💥💥💥");
        playerHP.TakeDamage(damage);

        if (playerHP.CurrentHeat <= 0)
        {
            Debug.Log("히트 고갈! 게임 오버");
            GameOver();
        }
    }

    // === Boss 처치 ===
    public void OnBossDefeated()
    {
        int currentStage = bossManager != null ? bossManager.GetBossLevel() : 0;

        playerHP.OnBossDefeated(currentStage);

        if (gunSystem.IsFeverMode)
            StartCoroutine(gunSystem.SyncFreezeWithBossRespawn());

        // ⭐ v5.0: 무한대 보스 진입 시 이동 카운트 초기화
        if (currentStage == 39)
            gridManager.ResetInfiniteBossMoveCount();

        // ⭐ v6.0: Clear 모드에서 보스 처치 시 Clear UI 표시
        if (bossManager != null && bossManager.IsClearMode())
            StartCoroutine(ShowClearUIAfterRespawn());
    }

    // === 무한보스 Enemy HP bar 색상 ===
    public void UpdateInfiniteBossEnemyBarColor()
    {
        if (bossManager == null) return;

        if (bossManager.hpSlider != null)
        {
            Image fillImage = bossManager.hpSlider.fillRect?.GetComponent<Image>();
            if (fillImage != null)
            {
                // ⭐ v6.0: Guard 모드 = 빨강, Clear 모드 = 초록, 그 외 = 밝은 붉은색
                if (bossManager.IsGuardMode())
                    fillImage.color = new Color(1f, 0.25f, 0.25f);
                else if (bossManager.IsClearMode())
                    fillImage.color = new Color(0.3f, 0.85f, 0.4f);
                else
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

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);

            CanvasGroup canvasGroup = gameOverPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameOverPanel.AddComponent<CanvasGroup>();

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

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        Debug.Log("🎮 CONTINUE! 체력 전부 회복 + Freeze 10턴 진입!");
    }

    // === Restart ===
    public void RestartGame()
    {
        isGameOver = false;
        isBossAttacking = false;
        isBossTransitioning = false;
        isChallengeClearShown = false;

        if (bossManager != null)
            bossManager.ResetBoss();

        gridManager.ResetGrid();
        gunSystem.ResetState();
        playerHP.ResetState();

        if (lowHealthVignette != null)
            lowHealthVignette.ResetInfiniteBossBonus();

        gridManager.StartNewGame();
        gunSystem.UpdateGunUI();
        gridManager.UpdateTurnUI();
        playerHP.UpdateHeatUI(true);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    // === Quit ===
    void QuitGame()
    {
        Debug.Log("게임 종료");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // === Input 차단 체크 (GameManager에서 사용) ===
    public bool ShouldBlockInput()
    {
        return isGameOver || gridManager.IsProcessing || isBossTransitioning || isBossAttacking || isChallengeClearShown;
    }

    // =====================================================
    // ⭐ v6.0: Challenge Clear UI
    // =====================================================

    IEnumerator ShowClearUIAfterRespawn()
    {
        yield return new WaitForSeconds(1.5f);
        ShowChallengeClearUI();
    }

    void ShowChallengeClearUI()
    {
        isChallengeClearShown = true;
        CreateChallengeClearPanel();
        SpawnClearFirework();
        Debug.Log("🎉 Challenge Clear UI 표시!");
    }

    void CreateChallengeClearPanel()
    {
        // 기존 패널 제거
        GameObject existing = GameObject.Find("ChallengeClearPanel");
        if (existing != null) Destroy(existing);

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        // 패널 배경
        GameObject panelObj = new GameObject("ChallengeClearPanel");
        panelObj.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0f);
        panelImage.DOFade(0.7f, 0.5f).SetEase(Ease.InOutQuad);

        // 타이틀 텍스트
        GameObject titleObj = new GameObject("ClearTitle");
        titleObj.transform.SetParent(panelObj.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.65f);
        titleRect.anchorMax = new Vector2(0.5f, 0.65f);
        titleRect.sizeDelta = new Vector2(600f, 120f);

        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "Challenge\nClear!";
        titleText.fontSize = 60;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(1f, 0.84f, 0f); // 골드
        titleText.fontStyle = FontStyles.Bold;

        // 버튼 3개: Resume, GoToTitle, Restart
        CreateClearButton(panelObj.transform, "ResumeBtn", "Resume", new Vector2(0.5f, 0.45f), OnClearResume);
        CreateClearButton(panelObj.transform, "GoToTitleBtn", "Title", new Vector2(0.5f, 0.35f), OnClearGoToTitle);
        CreateClearButton(panelObj.transform, "RestartBtn", "Restart", new Vector2(0.5f, 0.25f), OnClearRestart);
    }

    void CreateClearButton(Transform parent, string name, string label, Vector2 anchor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = anchor;
        btnRect.anchorMax = anchor;
        btnRect.sizeDelta = new Vector2(300f, 70f);

        Image btnImage = btnObj.AddComponent<Image>();
        btnImage.color = new Color(0.2f, 0.2f, 0.3f, 0.9f);

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImage;
        btn.onClick.AddListener(onClick);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI btnText = textObj.AddComponent<TextMeshProUGUI>();
        btnText.text = label;
        btnText.fontSize = 36;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.color = Color.white;
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
        fwRect.sizeDelta = Vector2.zero;

        ParticleSystem ps = fwObj.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.startLifetime = 1.0f;
        main.startSpeed = 200f;
        main.startSize = 20f;
        main.maxParticles = 100;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.playOnAwake = false;
        main.loop = false;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, 60, 100)
        });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 30f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 0.84f, 0f), 0.0f),
                new GradientColorKey(new Color(1f, 0.5f, 0f), 0.5f),
                new GradientColorKey(new Color(1f, 0.2f, 0.2f), 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(0.8f, 0.5f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0.0f, 1.0f);
        curve.AddKey(1.0f, 0.0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = new Material(Shader.Find("UI/Default"));

        var uiP = fwObj.AddComponent<Coffee.UIExtensions.UIParticle>();
        uiP.scale = 3f;
        uiP.autoScalingMode = Coffee.UIExtensions.UIParticle.AutoScalingMode.None;

        ps.Play();
        Destroy(fwObj, 2f);
    }

    void OnClearResume()
    {
        isChallengeClearShown = false;
        GameObject panel = GameObject.Find("ChallengeClearPanel");
        if (panel != null) Destroy(panel);
        Debug.Log("🏆 Clear 모드 계속 진행!");
    }

    void OnClearGoToTitle()
    {
        isChallengeClearShown = false;
        GameObject panel = GameObject.Find("ChallengeClearPanel");
        if (panel != null) Destroy(panel);
        RestartGame(); // 타이틀 씬 없으므로 Restart로 대체
    }

    void OnClearRestart()
    {
        isChallengeClearShown = false;
        GameObject panel = GameObject.Find("ChallengeClearPanel");
        if (panel != null) Destroy(panel);
        RestartGame();
    }
}
