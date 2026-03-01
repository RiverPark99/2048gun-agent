// =====================================================
// LottieTileEffect.cs
// =====================================================

using UnityEngine;
using System.Collections;
using Gilzoide.LottiePlayer;

public class LottieTileEffect : MonoBehaviour
{
    private static readonly System.Collections.Generic.Queue<LottieTileEffect> _pool
        = new System.Collections.Generic.Queue<LottieTileEffect>();
    private static Transform _poolRoot;

    private RectTransform _rt;
    private TileImageLottiePlayer _player;
    private CanvasGroup _cg;

    private float _endTime;
    private float _elapsed;
    private bool _playing;

    public static float DefaultStartTime = 0.44f;
    public static float DefaultEndTime   = 0.96f;

    // ─────────────────────────────────────────────
    // 정적 API
    // ─────────────────────────────────────────────

    public static void Spawn(LottieAnimationAsset asset, Transform parent, Vector2 anchoredPos, float size)
    {
        Spawn(asset, parent, anchoredPos, size, DefaultStartTime, DefaultEndTime);
    }

    public static void Spawn(LottieAnimationAsset asset, Transform parent, Vector2 anchoredPos, float size,
                             float startTime, float endTime, float playbackSpeed = -1f)
    {
        if (asset == null || parent == null) return;
        LottieTileEffect effect = GetFromPool(parent);
        effect._rt.anchoredPosition = anchoredPos;
        effect._rt.sizeDelta        = new Vector2(size, size);
        effect.BeginPlay(asset, startTime, endTime, playbackSpeed);
    }

    // ─────────────────────────────────────────────
    // 풀 관리
    // ─────────────────────────────────────────────

    static LottieTileEffect GetFromPool(Transform parent)
    {
        EnsurePoolRoot();
        LottieTileEffect effect = null;
        while (_pool.Count > 0 && effect == null)
            effect = _pool.Dequeue();
        if (effect == null)
            effect = CreateInstance();
        effect.transform.SetParent(parent, false);
        effect.gameObject.SetActive(true);
        return effect;
    }

    static void EnsurePoolRoot()
    {
        if (_poolRoot != null) return;
        var go = new GameObject("[LottieEffectPool]");
        Object.DontDestroyOnLoad(go);
        go.SetActive(false);
        _poolRoot = go.transform;
    }

    static LottieTileEffect CreateInstance()
    {
        var go = new GameObject("LottieTileEffect");
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        var cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        var player = go.AddComponent<TileImageLottiePlayer>();

        var effect = go.AddComponent<LottieTileEffect>();
        effect._rt = rt;
        effect._cg = cg;
        effect._player = player;
        return effect;
    }

    void ReturnToPool()
    {
        _playing = false;
        if (_cg != null) _cg.alpha = 0f;
        if (_player != null) _player.Pause();
        gameObject.SetActive(false);
        if (_poolRoot != null)
            transform.SetParent(_poolRoot, false);
        _pool.Enqueue(this);
    }

    // ─────────────────────────────────────────────
    // 재생
    // ─────────────────────────────────────────────

    void BeginPlay(LottieAnimationAsset asset, float startTime, float endTime, float playbackSpeed = -1f)
    {
        if (_cg != null) _cg.alpha = 0f;

        // playbackSpeed 지정 시 player에 적용, -1이면 player 기본값 유지
        if (_player != null && playbackSpeed > 0f)
            _player.PlaybackSpeed = playbackSpeed;

        float totalDuration = (asset != null && asset.Duration > 0.0) ? (float)asset.Duration : 1f;
        float clampedEnd    = (endTime > 0f) ? Mathf.Min(endTime, totalDuration) : totalDuration;
        float speed         = (_player != null) ? Mathf.Max(_player.PlaybackSpeed, 0.1f) : 1f;
        _endTime = (clampedEnd - startTime) / speed;
        if (_endTime <= 0f) _endTime = totalDuration / speed;

        _elapsed = 0f;
        _playing = true;

        // 해상도 설정 + 첫 프레임 동기 렌더 후 표시
        _player.PlayFromTile(asset, startTime, onFirstFrameReady: () =>
        {
            if (_cg != null) _cg.alpha = 1f;
        });
    }

    void Update()
    {
        if (!_playing) return;
        _elapsed += Time.deltaTime;
        if (_elapsed >= _endTime)
            ReturnToPool();
    }
}