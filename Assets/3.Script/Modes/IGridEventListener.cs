// =====================================================
// IGridEventListener.cs
// GridManager가 모드별 처리를 위임하는 콜백 인터페이스
//
// 사용 방법:
//   GridManager는 이 인터페이스를 통해 이벤트를 발행.
//   각 모드(ChallengeMode, ClassicChocoMode, SurvivalMode)가 이를 구현.
// =====================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 그리드 이벤트를 수신하는 모드 컨트롤러 인터페이스
/// </summary>
public interface IGridEventListener
{
    // ───── 머지 이벤트 ─────

    /// <summary>
    /// 타일 머지가 발생했을 때 호출.
    /// </summary>
    /// <param name="info">머지에 관한 모든 정보</param>
    void OnTileMerged(MergeInfo info);

    /// <summary>
    /// 한 턴에서 모든 머지가 완료된 후 호출.
    /// </summary>
    /// <param name="summary">해당 턴의 머지 전체 요약</param>
    void OnTurnMergesComplete(TurnMergeSummary summary);

    // ───── 이동/턴 이벤트 ─────

    /// <summary>
    /// 타일 이동 후(AfterMove) 후처리 시점에 호출.
    /// </summary>
    void OnAfterMove();

    // ───── 상태 판정 이벤트 ─────

    /// <summary>
    /// 이동 불가 상태가 됐을 때 호출. 게임오버 판정에 사용.
    /// </summary>
    void OnBoardFull();

    /// <summary>
    /// 타일 생성 직후 호출.
    /// </summary>
    /// <param name="pos">생성된 그리드 위치</param>
    /// <param name="value">타일 값</param>
    void OnTileSpawned(Vector2Int pos, int value);

    // ───── 색상 결정 ─────

    /// <summary>
    /// 새 타일을 생성할 때 색상을 모드가 결정하도록 위임.
    /// null 반환 시 GridManager가 기본값 사용.
    /// </summary>
    TileColor? GetSpawnTileColor();

    /// <summary>
    /// 머지 결과 타일 색상을 모드가 결정하도록 위임.
    /// null 반환 시 GridManager가 기본값 사용.
    /// </summary>
    TileColor? GetMergeResultColor();
}

// ─────────────────────────────────────────────
// 이벤트 데이터 구조체
// ─────────────────────────────────────────────

/// <summary>
/// 단일 머지 이벤트 데이터
/// </summary>
public struct MergeInfo
{
    /// <summary>머지된 타일 값 (결과값, 예: 2+2 → 4)</summary>
    public int mergedValue;

    /// <summary>움직인 타일의 색상</summary>
    public TileColor color1;

    /// <summary>목표 타일의 색상</summary>
    public TileColor color2;

    /// <summary>머지된 그리드 위치</summary>
    public Vector2Int gridPos;

    /// <summary>머지된 월드 위치 (projectile 발사점 등에 사용)</summary>
    public Vector3 worldPos;
}

/// <summary>
/// 한 턴 전체 머지 결과 요약
/// </summary>
public struct TurnMergeSummary
{
    /// <summary>이번 턴 총 머지 횟수</summary>
    public int mergeCount;

    /// <summary>이번 턴 총 머지된 값 합계 (보너스 포함 전 기본값)</summary>
    public int totalMergedValueBase;

    /// <summary>Choco+Choco 머지 횟수</summary>
    public int chocoMergeCount;

    /// <summary>Berry+Berry 머지가 한 번이라도 있었는지</summary>
    public bool hadBerryMerge;

    /// <summary>Berry 머지 횟수</summary>
    public int berryMergeCount;

    /// <summary>마지막으로 머지된 월드 위치</summary>
    public Vector3 lastMergeWorldPos;

    /// <summary>모드가 데미지 계산에 사용할 상세 머지 항목 목록</summary>
    public List<GridManager.MergeEntry> mergeEntries;

    /// <summary>이번 턴 머지 전 Heat 값 (변화량 계산용)</summary>
    public int heatBefore;
}
