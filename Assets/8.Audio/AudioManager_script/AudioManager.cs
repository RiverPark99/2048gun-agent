using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[Serializable]
public class Sound
{
    public string name;
    public AudioClip clip;
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance = null;
    [SerializeField] private AudioMixer mixer;
    private void Awake()
    {
        if(instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        LoadAllVolumes();
        AutoSetting();
        InitializeDictionary();
    }

    [Header("AudioGroup")]
    [SerializeField] private AudioMixerGroup BGM;
    [SerializeField] private AudioMixerGroup SFX;

    [Header("AudioClip")]
    [SerializeField] private Sound[] BGM_clip;
    [Space(10f)]
    [SerializeField] private Sound[] SFX_clip;

    [Header("Audio Source")]
    [SerializeField] private AudioSource[] BGM_Player; // main, freeze theme으로 2종 bgm동시사용
    [SerializeField] private AudioSource SFX_Player;
    private void AutoSetting()
    {
        BGM_Player = transform.GetChild(0).GetComponents<AudioSource>();
        SFX_Player = transform.GetChild(1).GetComponent<AudioSource>();
    }

    private const float defaultVolume = 75f;
    private void LoadAllVolumes()
    {
        SetVolume("Master", PlayerPrefs.GetFloat("volume_Master", defaultVolume));
        SetVolume("BGM", PlayerPrefs.GetFloat("volume_BGM", defaultVolume));
        SetVolume("SFX", PlayerPrefs.GetFloat("volume_SFX", defaultVolume));
    }

    public void SetVolume(string parameter, float sliderValue)
    {
        // 1. 오디오 믹서 적용
        float normalizeValue = Mathf.Clamp(sliderValue / 100f, 0.0001f, 1f);
        float dB = Mathf.Log10(normalizeValue) * 20;
        mixer.SetFloat(parameter, dB);

        // 2. 데이터 저장
        PlayerPrefs.SetFloat("volume_" + parameter, sliderValue);
    }

    // 현재 저장된 볼륨 값을 가져오는 메서드 (슬라이더 초기화용)
    public float GetSavedVolume(string parameter)
    {
        return PlayerPrefs.GetFloat("volume_" + parameter, 75f);
    }

    [SerializeField] private float fadeDuration = 1.0f; // 페이드 속도 조절 변수
    private Coroutine bgmFadeCoroutine; // 메인 BGM 페이드 코루틴
    private Coroutine Freezethteme_FadeCoroutine; // 프리즈 BGM 페이드 코루틴

    public void PlayBothSync(string mainName, string freezeName)
    {
        AudioClip mainClip = null;
        AudioClip freezeClip = null;

        // 1. 이름으로 클립 찾기
        foreach (Sound s in BGM_clip)
        {
            if (s.name.Equals(mainName)) mainClip = s.clip;
            if (s.name.Equals(freezeName)) freezeClip = s.clip;
        }

        if (mainClip == null || freezeClip == null)
        {
            Debug.LogWarning("BGM 클립을 찾을 수 없습니다: " + mainName + " 또는 " + freezeName);
            return;
        }

        // 2. 각 소스 설정
        // 메인 플레이어
        BGM_Player[0].clip = mainClip;
        BGM_Player[0].outputAudioMixerGroup = BGM;
        BGM_Player[0].loop = true;
        BGM_Player[0].volume = 0f; // 페이드 인을 위해 0부터 시작

        // 프리즈 플레이어
        BGM_Player[1].clip = freezeClip;
        BGM_Player[1].outputAudioMixerGroup = BGM;
        BGM_Player[1].loop = true;
        BGM_Player[1].volume = 0f; // 처음엔 안 들리게 0으로 고정

        // 3. 정확한 미래 시간(dspTime) 계산 후 동시 실행 예약
        // 0.1초 정도 여유를 주어 하드웨어 준비 시간을 벌어줘서 싱크 맞추기
        double syncTime = AudioSettings.dspTime + 0.1;

        BGM_Player[0].PlayScheduled(syncTime);
        BGM_Player[1].PlayScheduled(syncTime);

        // 4. 메인 테마만 페이드 인 시작
        if (bgmFadeCoroutine != null) StopCoroutine(bgmFadeCoroutine);
        bgmFadeCoroutine = StartCoroutine(FadeInMainBGM());

        Debug.Log($"BGM 싱크 재생 예약됨: {syncTime}");
    }

    // 메인 BGM 페이드 인 로직
    private IEnumerator FadeInMainBGM()
    {
        while (BGM_Player[0].volume < 1.0f)
        {
            BGM_Player[0].volume += Time.deltaTime / fadeDuration;
            yield return null;
        }
        BGM_Player[0].volume = 1.0f;
        bgmFadeCoroutine = null;
    }

    // 프리즈 테마 서서히 켜기
    public void FadeinFreezeBGM()
    {
        if (Freezethteme_FadeCoroutine != null) StopCoroutine(Freezethteme_FadeCoroutine);
        Freezethteme_FadeCoroutine = StartCoroutine(FreezeFadeIn());
    }

    private IEnumerator FreezeFadeIn()
    {
        while (BGM_Player[1].volume < 1.0f)
        {
            BGM_Player[1].volume += Time.deltaTime / fadeDuration; // +로 페이드인
            yield return null;
        }
        BGM_Player[1].volume = 1.0f;
        Freezethteme_FadeCoroutine = null;
    }

    // 프리즈 테마 서서히 끄기 (볼륨 DOWN - 재생은 멈추지 않음)
    public void FadeoutFreezeBGM()
    {
        if (Freezethteme_FadeCoroutine != null) StopCoroutine(Freezethteme_FadeCoroutine);
        Freezethteme_FadeCoroutine = StartCoroutine(FreezeFadeOut());
    }

    private IEnumerator FreezeFadeOut()
    {
        while (BGM_Player[1].volume > 0f)
        {
            BGM_Player[1].volume -= Time.deltaTime / fadeDuration;
            yield return null;
        }
        BGM_Player[1].volume = 0f; // 완전히 0으로 고정
        Freezethteme_FadeCoroutine = null;
    }

    private Dictionary<string, AudioClip> sfxDictionary = new Dictionary<string, AudioClip>();

    private void InitializeDictionary()
    {
        foreach (Sound s in SFX_clip)
        {
            if (!sfxDictionary.ContainsKey(s.name))
                sfxDictionary.Add(s.name, s.clip);
        }
    }
    public void PlaySFX(string name, float volume = 1f)
    {
        if (sfxDictionary.TryGetValue(name, out AudioClip clip))
        {
            SFX_Player.PlayOneShot(clip, volume);
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }
}
