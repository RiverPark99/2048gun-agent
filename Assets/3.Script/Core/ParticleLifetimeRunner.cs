// =====================================================
// ParticleLifetimeRunner.cs
// 파티클 수명 관리 전용 MonoBehaviour.
// TileParticleSpawner와 독립적으로 존재하므로
// Tile이 Destroy돼도 코루틴이 끊기지 않고 파티클이 재생 완료됨.
// =====================================================

using UnityEngine;
using System.Collections;

public class ParticleLifetimeRunner : MonoBehaviour
{
    private Coroutine _returnCoroutine;

    public void ScheduleReturn(GameObject obj, float delay)
    {
        if (_returnCoroutine != null) StopCoroutine(_returnCoroutine);
        _returnCoroutine = StartCoroutine(ReturnAfterDelay(obj, delay));
    }

    IEnumerator ReturnAfterDelay(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        _returnCoroutine = null;
        TileParticleSpawner.ReturnParticle(obj);
    }
}
