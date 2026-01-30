using UnityEngine;
using DG.Tweening;

public class CameraShake : MonoBehaviour
{
    private static CameraShake instance;
    public static CameraShake Instance
    {
        get
        {
            if (instance == null)
            {
                // Canvas를 찾아서 CameraShake 추가
                Canvas canvas = FindAnyObjectByType<Canvas>();
                if (canvas != null)
                {
                    instance = canvas.gameObject.AddComponent<CameraShake>();
                }
                else
                {
                    GameObject obj = new GameObject("CameraShake");
                    instance = obj.AddComponent<CameraShake>();
                }
            }
            return instance;
        }
    }

    private RectTransform canvasRect;
    private Vector3 originalPosition;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }
        instance = this;
        
        // Canvas의 RectTransform 찾기
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = FindAnyObjectByType<Canvas>();
        }
        
        if (canvas != null)
        {
            canvasRect = canvas.GetComponent<RectTransform>();
            originalPosition = canvasRect.localPosition;
        }
    }

    /// <summary>
    /// 화면 진동
    /// </summary>
    /// <param name="duration">지속 시간</param>
    /// <param name="strength">강도</param>
    /// <param name="vibrato">진동 횟수</param>
    public void Shake(float duration = 0.3f, float strength = 20f, int vibrato = 10)
    {
        if (canvasRect == null) return;

        canvasRect.DOKill(); // 기존 진동 취소
        
        canvasRect.DOShakePosition(duration, strength, vibrato, 90, false, true)
            .OnComplete(() =>
            {
                // 원래 위치로 복귀
                canvasRect.localPosition = originalPosition;
            });
    }

    /// <summary>
    /// 약한 진동
    /// </summary>
    public void ShakeLight()
    {
        Shake(0.15f, 10f, 5);
    }

    /// <summary>
    /// 중간 진동
    /// </summary>
    public void ShakeMedium()
    {
        Shake(0.25f, 20f, 10);
    }

    /// <summary>
    /// 강한 진동
    /// </summary>
    public void ShakeStrong()
    {
        Shake(0.4f, 30f, 15);
    }
}
