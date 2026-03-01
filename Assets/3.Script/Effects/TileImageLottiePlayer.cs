// =====================================================
// TileImageLottiePlayer.cs
// =====================================================

using UnityEngine;
using System;
using System.Collections;
using Gilzoide.LottiePlayer;

public class TileImageLottiePlayer : ImageLottiePlayer
{
    private Action _onFirstFrameReady;
    private bool _waitingFirstFrame;
    private Coroutine _speedCoroutine;

    // 재생 배속 (1.5 = 1.5배 빠르게)
    public float PlaybackSpeed = 1.5f;

    public void PlayFromTile(LottieAnimationAsset asset, float startTime, Action onFirstFrameReady)
    {
        _onFirstFrameReady = onFirstFrameReady;
        _waitingFirstFrame = false;

        // 텍스처 해상도 512 고정
        _width = 512;
        _height = 512;

        // 같은 asset 재사용 시 캐시 우회
        if (_animationAsset == asset)
        {
            Pause();
            SetAnimationAsset(null);
        }

        SetAnimationAsset(asset);

        // 첫 프레임 즉시 렌더
        if (_animation.IsValid() && _texture != null)
        {
            uint firstFrame = _animation.GetFrameAtTime(startTime, false);
            _animation.Render(firstFrame, _texture, keepAspectRatio: false);
            _texture.Apply(true);
            SetVerticesDirty();
            onFirstFrameReady?.Invoke();
            _onFirstFrameReady = null;
        }
        else
        {
            _waitingFirstFrame = true;
        }

        // base Play 대신 배속 코루틴 직접 실행
        Pause();
        if (_speedCoroutine != null) StopCoroutine(_speedCoroutine);
        _speedCoroutine = StartCoroutine(SpeedPlayRoutine(startTime));
    }

    IEnumerator SpeedPlayRoutine(float startTime)
    {
        if (!_animation.IsValid() || _texture == null) yield break;

        float t = startTime;
        float duration = (float)_animation.GetDuration();
        uint lastFrame = uint.MaxValue;

        while (t < duration)
        {
            uint frame = _animation.GetFrameAtTime(t, false);
            if (frame != lastFrame)
            {
                // Job System 없이 직접 렌더 (메인스레드, 심플)
                _animation.Render(frame, _texture, keepAspectRatio: false);
                _texture.Apply(true);
                SetVerticesDirty();
                lastFrame = frame;
            }
            yield return null;
            t += Time.deltaTime * PlaybackSpeed;
        }
        _speedCoroutine = null;
    }

    protected override void Start()
    {
        // autoPlay 방지
    }

    private void LateUpdate()
    {
        if (!_waitingFirstFrame || _onFirstFrameReady == null) return;
        if (_animation.IsValid() && _texture != null)
        {
            _waitingFirstFrame = false;
            _onFirstFrameReady.Invoke();
            _onFirstFrameReady = null;
        }
    }
}