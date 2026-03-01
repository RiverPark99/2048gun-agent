// =====================================================
// GameModeBase.cs  v2.0
// ClassicChocoMode / BerryMode 공통 기반 (Template Method Pattern)
//
// v2.0 추가:
//   • 스코어 플로팅 텍스트 시스템 (머지 색상 + 콤보 색상)
//   • 베스트 스코어 / NewRecord 연출 공통화
// =====================================================

using UnityEngine;
using TMPro;
using DG.Tweening;

public abstract class GameModeBase : MonoBehaviour, IGridEventListener
{
    [Header("공통 참조")]
    [SerializeField] protected GridManager gridManager;
    [SerializeField] protected PlayerHPSystem playerHP;

    [Header("스코어 플로팅 텍스트")]
    [SerializeField] protected GameObject scoreFloatPrefab;   // damageTextPrefab 재사용 가능
    [SerializeField] protected Transform  scoreFloatParent;   // 스코어 텍스트 위 빈 Transform
    [SerializeField] protected float      scoreFloatSize   = 38f;
    [SerializeField] protected float      scoreFloatRiseY  = 70f;
    [SerializeField] protected float      scoreFloatDuration = 0.9f;

    [Header("콤보 플로팅 색상 (index = mergeCount, 0/1=단일, 2=노랑, 3=주황, 4=빨강, 5+=보라)")]
    [SerializeField] protected Color[] comboFloatColors = new Color[]
    {
        new Color(0.85f, 0.85f, 0.85f),  // 0: 미사용
        new Color(0.85f, 0.85f, 0.85f),  // 1: 1콤보 (회색)
        new Color(1.00f, 0.85f, 0.30f),  // 2: 2콤보 (노랑)
        new Color(1.00f, 0.55f, 0.10f),  // 3: 3콤보 (주황)
        new Color(1.00f, 0.25f, 0.10f),  // 4: 4콤보 (빨강)
        new Color(0.80f, 0.10f, 0.90f),  // 5+: 보라
    };

    [Header("NewRecord 연출")]
    [SerializeField] protected TextMeshProUGUI newRecordText;   // "NEW RECORD!" 라벨 (평소 비활성)
    [SerializeField] protected Color newRecordColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] protected float newRecordPopScale = 1.6f;

    protected long currentScore = 0;
    private bool _newRecordShown = false;   // 이번 판에서 최초 NewRecord 연출 여부

    // ─────────────────────────────────────────────
    // 초기화
    // ─────────────────────────────────────────────

    protected virtual void Awake()
    {
        if (gridManager == null) gridManager = GetComponent<GridManager>();
        if (playerHP   == null) playerHP   = GetComponent<PlayerHPSystem>();
    }

    public virtual void OnModeStart()
    {
        currentScore    = 0;
        _newRecordShown = false;
        if (newRecordText != null) newRecordText.gameObject.SetActive(false);
        UpdateScoreUI();
    }

    // ─────────────────────────────────────────────
    // IGridEventListener
    // ─────────────────────────────────────────────

    public virtual void OnTileMerged(MergeInfo info) { }

    public void OnTurnMergesComplete(TurnMergeSummary summary)
    {
        // 1. 머지별 즉각 보너스
        ProcessMergeBonus(summary);

        // 2. 점수 계산
        long score = CalculateScore(summary);

        // 3. 콤보 회복
        ApplyComboHeal(summary);

        // 4. HP 반영 & UI
        if (playerHP != null)
        {
            playerHP.ClampHeat();
            int netChange = playerHP.CurrentHeat - summary.heatBefore;
            playerHP.UpdateHeatUI();
            if (netChange != 0) playerHP.ShowHeatChangeText(netChange);
            if (netChange > 0)  playerHP.FlashHealGreen();
        }

        // 5. 점수 반영 + 플로팅 텍스트
        if (score > 0)
        {
            AddScore(score, summary);
        }

        // 6. 게임오버 체크
        if (playerHP != null && playerHP.CurrentHeat <= 0)
        {
            OnGameOver();
            return;
        }

        // 7. 모드별 후처리
        OnTurnProcessed(summary);
    }

    public virtual void OnAfterMove() { }
    public virtual void OnBoardFull() => OnGameOver();
    public virtual void OnTileSpawned(Vector2Int pos, int value) { }
    public virtual TileColor? GetSpawnTileColor()   => null;
    public virtual TileColor? GetMergeResultColor() => null;

    // ─────────────────────────────────────────────
    // Template Method Hooks
    // ─────────────────────────────────────────────

    protected virtual void ProcessMergeBonus(TurnMergeSummary summary) { }
    protected abstract long CalculateScore(TurnMergeSummary summary);

    protected virtual void ApplyComboHeal(TurnMergeSummary summary)
    {
        if (summary.mergeCount <= 0 || playerHP == null) return;
        int[] recover = playerHP.ComboHeatRecover;
        int idx  = Mathf.Min(summary.mergeCount, recover.Length - 1);
        int heal = recover[idx];
        if (heal > 0) playerHP.AddHeat(heal);
    }

    /// <summary>점수 누적 → UI 갱신 → 플로팅 텍스트 → NewRecord 체크</summary>
    protected void AddScore(long score, TurnMergeSummary summary)
    {
        currentScore += score;

        bool isNewRecord = CheckAndSaveBestScore(currentScore);
        UpdateScoreUI();

        // 플로팅 텍스트
        ShowScoreFloat(score, summary.mergeCount);

        // NewRecord 연출 (이번 판 최초 1회만 팝업 → 이후는 bestScoreText만 갱신)
        if (isNewRecord && !_newRecordShown)
        {
            _newRecordShown = true;
            PlayNewRecordEffect();
        }
    }

    // 하위 호환용 (summary 없이 호출 가능)
    protected void AddScore(long score)
    {
        currentScore += score;
        CheckAndSaveBestScore(currentScore);
        UpdateScoreUI();
    }

    protected abstract void UpdateScoreUI();
    protected virtual void OnTurnProcessed(TurnMergeSummary summary) { }

    protected virtual void OnGameOver()
    {
        Debug.Log($"[{GetType().Name}] Game Over. Score: {currentScore}");
    }

    // ─────────────────────────────────────────────
    // 베스트 스코어 (하위 클래스에서 Key 지정)
    // ─────────────────────────────────────────────

    protected abstract string BestScorePlayerPrefsKey { get; }

    protected long LoadBestScore()
    {
        string saved = PlayerPrefs.GetString(BestScorePlayerPrefsKey, "0");
        long.TryParse(saved, out long best);
        return best;
    }

    /// <summary>currentScore가 bestScore를 넘으면 저장 후 true 반환</summary>
    protected bool CheckAndSaveBestScore(long score)
    {
        long best = LoadBestScore();
        if (score > best)
        {
            PlayerPrefs.SetString(BestScorePlayerPrefsKey, score.ToString());
            PlayerPrefs.Save();
            return true;
        }
        return false;
    }

    // ─────────────────────────────────────────────
    // 스코어 플로팅 텍스트
    // ─────────────────────────────────────────────

    protected void ShowScoreFloat(long score, int mergeCount)
    {
        if (scoreFloatPrefab == null || scoreFloatParent == null) return;

        GameObject obj = Instantiate(scoreFloatPrefab, scoreFloatParent);
        TextMeshProUGUI txt = obj.GetComponent<TextMeshProUGUI>();
        if (txt == null) { Destroy(obj); return; }

        // 색상: 콤보 수에 따라
        Color floatColor = GetComboFloatColor(mergeCount);
        txt.color     = floatColor;
        txt.fontSize  = scoreFloatSize;
        txt.text      = $"+{score:N0}";

        RectTransform rt = obj.GetComponent<RectTransform>();
        CanvasGroup   cg = obj.GetComponent<CanvasGroup>() ?? obj.AddComponent<CanvasGroup>();
        cg.alpha = 1f;

        DOTween.Sequence()
            .Append(rt.DOAnchorPosY(rt.anchoredPosition.y + scoreFloatRiseY, scoreFloatDuration).SetEase(Ease.OutCubic))
            .Join(cg.DOFade(0f, scoreFloatDuration).SetEase(Ease.InCubic))
            .Insert(0f,   rt.DOScale(1.25f, 0.1f).SetEase(Ease.OutQuad))
            .Insert(0.1f, rt.DOScale(1f,    0.12f).SetEase(Ease.InQuad))
            .OnComplete(() => { if (obj != null) Destroy(obj); });
    }

    Color GetComboFloatColor(int mergeCount)
    {
        if (comboFloatColors == null || comboFloatColors.Length == 0)
            return Color.white;
        int idx = Mathf.Clamp(mergeCount, 0, comboFloatColors.Length - 1);
        return comboFloatColors[idx];
    }

    // ─────────────────────────────────────────────
    // NewRecord 연출
    // ─────────────────────────────────────────────

    protected virtual void PlayNewRecordEffect()
    {
        if (newRecordText == null) return;

        newRecordText.gameObject.SetActive(true);
        newRecordText.color = newRecordColor;

        RectTransform rt = newRecordText.GetComponent<RectTransform>();
        rt.DOKill();
        newRecordText.DOKill();
        rt.localScale = Vector3.one * newRecordPopScale;

        DOTween.Sequence()
            .Append(rt.DOScale(1f, 0.35f).SetEase(Ease.OutBack))
            .AppendInterval(1.5f)
            .Append(newRecordText.DOFade(0f, 0.4f).SetEase(Ease.InQuad))
            .OnComplete(() =>
            {
                if (newRecordText != null)
                {
                    newRecordText.gameObject.SetActive(false);
                    Color c = newRecordText.color; c.a = 1f; newRecordText.color = c;
                }
            });
    }
}
