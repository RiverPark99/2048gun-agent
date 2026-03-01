// =====================================================
// TileParticleSpawner.cs
// Tile 머지/파괴 Lottie 이펙트 전담 컴포넌트
// - Merge: Lottie (choco / berry / mix)
// - Gun 파괴: Lottie (gunexplosion)
// =====================================================

using UnityEngine;
using Gilzoide.LottiePlayer;

[RequireComponent(typeof(RectTransform))]
public class TileParticleSpawner : MonoBehaviour
{
    // ⭐ Lottie Assets (Inspector에서 할당 — 미할당 시 기존 파티클 fallback)
    [Header("Lottie Assets")]
    [SerializeField] private LottieAnimationAsset lottieChoco;
    [SerializeField] private LottieAnimationAsset lottieBerry;
    [SerializeField] private LottieAnimationAsset lottieMix;
    [SerializeField] private LottieAnimationAsset lottieGun;

    private RectTransform rectTransform;
    private Transform GridContainer => transform.parent;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        LoadLottieAssets();
    }

    void LoadLottieAssets()
    {
        if (lottieChoco == null) lottieChoco = Resources.Load<LottieAnimationAsset>("Lottie/choco");
        if (lottieBerry == null) lottieBerry = Resources.Load<LottieAnimationAsset>("Lottie/berry");
        if (lottieMix   == null) lottieMix   = Resources.Load<LottieAnimationAsset>("Lottie/mix");
        if (lottieGun   == null) lottieGun   = Resources.Load<LottieAnimationAsset>("Lottie/gunexplosion");
    }

    // ── 공개 API ──

    public void PlayChocoEffect(Vector2 pos) => SpawnLottie(lottieChoco, pos);
    public void PlayBerryEffect(Vector2 pos) => SpawnLottie(lottieBerry, pos);
    public void PlayMixEffect(Vector2 pos)   => SpawnLottie(lottieMix,   pos);

    // ── Lottie 재생 헬퍼 ──

    void SpawnLottie(LottieAnimationAsset asset, Vector2 anchoredPos)
    {
        Transform container = GridContainer ?? transform;
        float tileSize = Mathf.Max(rectTransform.rect.width, rectTransform.rect.height);
        LottieTileEffect.Spawn(asset, container, anchoredPos, tileSize * 5.0f);
    }

    void SpawnGunLottie(LottieAnimationAsset asset, Vector2 anchoredPos)
    {
        Transform container = GridContainer ?? transform;
        float tileSize = Mathf.Max(rectTransform.rect.width, rectTransform.rect.height);
        // Gun은 배속 느리게, 뒷부분 더 재생
        LottieTileEffect.Spawn(asset, container, anchoredPos, tileSize * 5.0f,
            startTime: 0.1f, endTime: 1.5f, playbackSpeed: 1.1f);
    }

    // ── Gun 파괴 전용: Instantiate/Destroy ──

    public void PlayGunDestroyEffect(Vector2 pos) => SpawnGunLottie(lottieGun, pos);
}
