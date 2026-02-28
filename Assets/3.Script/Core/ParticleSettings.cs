// =====================================================
// ParticleSettings.cs
// 해상도별 파티클 보정 수치 ScriptableObject
// Inspector에서 직접 수정 가능, 항목 추가로 해상도 세분화
// =====================================================

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ParticleResolutionEntry
{
    [Tooltip("식별용 라벨 (예: Mobile 360p, HD, Tablet)")]
    public string label = "New Entry";

    [Tooltip("이 항목이 적용되는 최대 스크린 너비 (px). 오름차순 정렬 필수.")]
    public int maxScreenWidth = 1080;

    [Header("Merge 파티클 보정")]
    [Tooltip("일반 머지 파티클 크기 보정값 (ParticleSizeCorrection)")]
    public float mergeParticleSizeCorrection = 10f;

    [Tooltip("Gun 파티클 크기 보정값 (GunParticleSizeCorrection)")]
    public float gunParticleSizeCorrection = 10f;

    [Tooltip("소형 파티클 크기 보정값 (SmallParticleSizeCorrection, Fever/Clear 파티클)")]
    public float smallParticleSizeCorrection = 4f;

    [Header("UIParticle Scale")]
    [Tooltip("UIParticle.scale 값. 기본: 3 * (Screen.width / 498)")]
    public float uiParticleScale = 3f;
}

[CreateAssetMenu(fileName = "ParticleSettings", menuName = "2048Gun/ParticleSettings")]
public class ParticleSettings : ScriptableObject
{
    [Header("파티클 색상")]
    public Color berryParticleColor = new Color(1f, 0.5f, 0.65f);
    public Color chocoParticleColor = new Color(0.55f, 0.40f, 0.28f);
    [Tooltip("Gun 파괴 파티클 색상 1 (기본: 금색)")]
    public Color gunParticleColor1  = new Color(1f, 0.84f, 0f);
    [Tooltip("Gun 파괴 파티클 색상 2 (기본: 흰색)")]
    public Color gunParticleColor2  = Color.white;

    [Tooltip("화면 너비 오름차순으로 정렬. 현재 해상도보다 maxScreenWidth가 큰 첫 항목 적용.\n마지막 항목은 가장 큰 해상도(상한 없음)에 적용됨.")]
    public List<ParticleResolutionEntry> resolutionEntries = new List<ParticleResolutionEntry>()
    {
        new ParticleResolutionEntry { label = "Mobile 360p",  maxScreenWidth = 498,  mergeParticleSizeCorrection = 1f,  gunParticleSizeCorrection = 1f,  smallParticleSizeCorrection = 1f,  uiParticleScale = 3f  },
        new ParticleResolutionEntry { label = "Mobile 480p",  maxScreenWidth = 600,  mergeParticleSizeCorrection = 5f,  gunParticleSizeCorrection = 4f,  smallParticleSizeCorrection = 2f,  uiParticleScale = 3.6f },
        new ParticleResolutionEntry { label = "Mobile HD",    maxScreenWidth = 1080, mergeParticleSizeCorrection = 55f, gunParticleSizeCorrection = 35f, smallParticleSizeCorrection = 6f,  uiParticleScale = 6.5f },
        new ParticleResolutionEntry { label = "Mobile FHD+",  maxScreenWidth = 1290, mergeParticleSizeCorrection = 67f, gunParticleSizeCorrection = 55f, smallParticleSizeCorrection = 8f,  uiParticleScale = 7.8f },
        new ParticleResolutionEntry { label = "Tablet / PC",  maxScreenWidth = 9999, mergeParticleSizeCorrection = 80f, gunParticleSizeCorrection = 65f, smallParticleSizeCorrection = 10f, uiParticleScale = 10f },
    };

    /// <summary>현재 Screen.width에 맞는 항목 반환. 항목이 없으면 기본값 반환.</summary>
    public ParticleResolutionEntry GetCurrentEntry()
    {
        int w = Screen.width;
        foreach (var entry in resolutionEntries)
        {
            if (w <= entry.maxScreenWidth)
                return entry;
        }
        // 목록이 비어있거나 모든 항목보다 크면 마지막 항목 반환
        if (resolutionEntries != null && resolutionEntries.Count > 0)
            return resolutionEntries[resolutionEntries.Count - 1];

        // 완전 폴백
        return new ParticleResolutionEntry();
    }
}
