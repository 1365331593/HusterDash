using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// 文件名: MusicManager.cs
/// 作用: 跨场景持久化音乐管理器，管理场景背景音乐、特殊失败音乐和按钮音效
/// 主要功能:
///    1. DontDestroyOnLoad 单例，跨场景存活
///    2. 根据场景名自动切换背景音乐（MainMenu ↔ Game），淡入淡出各 0.5 秒
///    3. 同一场景重新加载时不中断音乐（如 Game 场景重新开始）
///    4. 游戏失败时播放特殊音乐，播完后从中断处恢复 Game 音乐
///    5. 持久化音量设置（PlayerPrefs）：音乐音量、音效音量、特殊失败音乐开关
///    6. 支持 AudioMixer（推荐，避免 timeScale=0 时音乐中断）或直接 AudioSource 音量控制
/// </summary>
public class MusicManager : MonoBehaviour
{
    #region 单例

    private static MusicManager _instance;

    /// <summary>
    /// 全局单例引用。若不存在则自动查找或创建。
    /// </summary>
    public static MusicManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<MusicManager>();
                if (_instance == null)
                {
                    Debug.LogWarning("[MusicManager] 未找到实例，自动创建（音频片段未配置，将无法播放音乐）。" +
                                     "建议将 MusicManager 预制体放入首个场景。");
                    GameObject go = new GameObject("MusicManager");
                    _instance = go.AddComponent<MusicManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    #endregion

    #region Inspector 字段

    [Header("音频片段")]
    [Tooltip("MainMenu 场景的背景音乐")]
    public AudioClip mainMenuMusic;

    [Tooltip("Game 场景的背景音乐")]
    public AudioClip gameMusic;

    [Tooltip("游戏失败时播放的特殊音乐（需玩家在设置中启用）")]
    public AudioClip gameOverSpecialMusic;

    [Tooltip("按钮点击音效")]
    public AudioClip buttonSfx;

    [Header("音频混合器（推荐使用）")]
    [Tooltip("AudioMixer 资产。使用后可避免 timeScale=0 时音乐中断。留空则直接控制 AudioSource 音量。\n" +
             "需在 Mixer 中创建 Music 和 SFX 两个 Group，并分别暴露 MusicVolume、SFXVolume 参数。")]
    public AudioMixer audioMixer;

    [Tooltip("AudioMixer 中 Music 组的 Volume 参数名")]
    public string musicVolumeParam = "MusicVolume";

    [Tooltip("AudioMixer 中 SFX 组的 Volume 参数名")]
    public string sfxVolumeParam = "SFXVolume";

    [Header("启动与淡入淡出")]
    [Tooltip("游戏启动后等待多少秒才开始播放音乐（秒）")]
    public float startupDelay = 1f;

    [Tooltip("场景切换时音乐的淡出/淡入时长（秒）")]
    public float crossfadeDuration = 0.5f;

    [Tooltip("特殊失败音乐开始/恢复时的淡入淡出时长（秒）")]
    public float gameOverFadeDuration = 0.3f;

    #endregion

    #region 内部状态

    // 两个音乐 AudioSource（交替使用，实现淡入淡出）
    private AudioSource sourceA;
    private AudioSource sourceB;

    // 音效专用 AudioSource
    private AudioSource sfxSource;

    // 当前激活的音源索引：0 表示 sourceA 在播放，1 表示 sourceB 在播放
    private int activeIndex = 0;

    // 当前正在播放哪个场景的音乐
    private string currentTrack = "";

    // 游戏失败时保存的 Game 音乐播放进度（采样秒数）
    private float savedGameMusicTime = 0f;

    // 是否正在播放特殊失败音乐
    private bool isPlayingSpecialMusic = false;

    // 特殊音乐是否已自然播放完毕（等待下一帧处理）
    private bool specialMusicFinished = false;

    // 是否正在执行场景切换淡入淡出
    private bool isCrossfading = false;

    // 挂起的场景切换目标（由 OnSceneTransitionStart 设置）
    private string pendingTargetScene = "";

    // 持久化设置
    private float musicVolume = 1f;
    private float sfxVolume = 1f;
    private bool enableSpecialMusic = false;

    // PlayerPrefs 键名
    private const string PREF_MUSIC_VOLUME = "MusicVolume";
    private const string PREF_SFX_VOLUME = "SFXVolume";
    private const string PREF_SPECIAL_MUSIC = "EnableSpecialGameOverMusic";

    /// <summary>
    /// 音乐 AudioSource 的目标基准音量：
    /// 有 AudioMixer 时 AudioSource.volume 恒为 1f（由 Mixer 控制实际音量），
    /// 无 AudioMixer 时直接使用 musicVolume 控制。
    /// </summary>
    private float MusicSourceBaseVolume => audioMixer != null ? 1f : musicVolume;

    /// <summary>
    /// 音效 AudioSource 的目标基准音量：逻辑同上
    /// </summary>
    private float SFXSourceBaseVolume => audioMixer != null ? 1f : sfxVolume;

    #endregion

    #region Unity 生命周期

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            CreateAudioSources();
            LoadAllSettings();
            ApplyVolumeSettings();
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }
        else if (_instance != this)
        {
            // 如果已有实例是自动创建的空壳（没有音频剪辑引用），
            // 而当前实例已配置了音频剪辑，则销毁空壳并接管为正式实例。
            // 这解决了 Splash 场景作为首场景时，SceneTransitionManager
            // 提前触发 MusicManager.Instance 导致空壳实例阻塞后续
            // 正确配置的 MusicManager 的问题。
            bool existingEmpty = (_instance.mainMenuMusic == null && _instance.gameMusic == null);
            bool currentHasClips = (mainMenuMusic != null || gameMusic != null);

            if (existingEmpty && currentHasClips)
            {
                Destroy(_instance.gameObject);
                _instance = this;
                DontDestroyOnLoad(gameObject);
                CreateAudioSources();
                LoadAllSettings();
                ApplyVolumeSettings();
                SceneManager.activeSceneChanged += OnActiveSceneChanged;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }
    }

    private void Start()
    {
        // 启动延迟：等待指定秒数后再开始播放音乐。
        // 使用显式 StartCoroutine 而非 IEnumerator Start()，避免与 DontDestroyOnLoad 的潜在冲突。
        StartCoroutine(DelayedStartPlayback());
    }

    /// <summary>
    /// 作用: 启动延迟协程：等待 startupDelay 秒后以保存的音量直接播放音乐，
    ///       绕过 AudioMixer 异步延迟问题，然后平滑过渡到 Mixer 控制模式。
    /// </summary>
    private IEnumerator DelayedStartPlayback()
    {
        if (startupDelay > 0f)
        {
            Debug.Log($"[MusicManager] 启动延迟 {startupDelay} 秒后开始播放音乐...");
            yield return new WaitForSecondsRealtime(startupDelay);
        }

        string sceneName = SceneManager.GetActiveScene().name;

        if (string.IsNullOrEmpty(currentTrack))
        {
            AudioClip clip = GetClipForScene(sceneName);
            if (clip != null)
            {
                // 关键：用 musicVolume（来自 PlayerPrefs 的保存值）直接设置
                // AudioSource.volume，而不是依赖 Mixer 衰减。
                // AudioMixer.SetFloat 是异步操作（发往音频线程），如果 Mixer
                // 参数尚未生效就开始淡入，会导致一帧爆音。直接用 AudioSource
                // 音量控制，无论 Mixer 状态如何都不会爆音。
                // 之后 Mixer 参数异步生效后，再平滑过渡到 MusicSourceBaseVolume（1f），
                // 过渡期间感知音量不变（AudioSource 增 ≈ Mixer 减）。
                float initialVolume = audioMixer != null ? musicVolume : MusicSourceBaseVolume;

                sourceA.clip = clip;
                sourceA.time = 0f;
                sourceA.volume = initialVolume;
                sourceA.Play();
                currentTrack = sceneName;
                activeIndex = 0;

                if (audioMixer != null)
                {
                    // 音频已在流经 Mixer Group，SetFloat 发往音频线程
                    ApplyVolumeSettings();

                    // 读取回 Mixer 当前值，验证参数是否已生效
                    float currentDb;
                    if (audioMixer.GetFloat(musicVolumeParam, out currentDb))
                    {
                        Debug.Log($"[MusicManager] Mixer 当前 MusicVolume = {currentDb:F1} dB，" +
                                  $"目标 = {LinearToDecibel(musicVolume):F1} dB");
                    }

                    Debug.Log($"[MusicManager] 已从 PlayerPrefs 恢复音量设置：" +
                              $"音乐={musicVolume:F2} ({LinearToDecibel(musicVolume):F1}dB)、" +
                              $"音效={sfxVolume:F2} ({LinearToDecibel(sfxVolume):F1}dB)");

                    // 等待音频线程处理完 SetFloat（异步，通常需 100-200ms）
                    // 期间 sourceA.volume = musicVolume 确保输出电平安全
                    yield return new WaitForSecondsRealtime(0.2f);

                    // 平滑过渡：sourceA.volume 从 musicVolume → MusicSourceBaseVolume（1f）。
                    // Mixer 此时已接管衰减，过渡期间感知音量基本不变。
                    yield return StartCoroutine(
                        FadeSourceVolume(sourceA, musicVolume, MusicSourceBaseVolume, 0.3f));
                }
            }
        }
    }

    private void Update()
    {
        // 检测特殊失败音乐是否播放完毕
        if (isPlayingSpecialMusic && specialMusicFinished)
        {
            AudioSource specialSrc = GetInactiveSource();
            if (specialSrc != null && !specialSrc.isPlaying)
            {
                isPlayingSpecialMusic = false;
                specialMusicFinished = false;
                StartCoroutine(ResumeGameMusicFromSavedPosition());
            }
        }
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        if (_instance == this)
            _instance = null;
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 作用: 创建两个音乐 AudioSource 和一个音效 AudioSource，
    ///       并在指定 AudioMixer 时自动路由到对应 Group
    /// </summary>
    private void CreateAudioSources()
    {
        // 音乐音源 A 和 B（用于淡入淡出交叉切换）
        sourceA = gameObject.AddComponent<AudioSource>();
        sourceA.loop = true;
        sourceA.playOnAwake = false;
        sourceA.volume = 0f;

        sourceB = gameObject.AddComponent<AudioSource>();
        sourceB.loop = true;
        sourceB.playOnAwake = false;
        sourceB.volume = 0f;

        // 音效音源（初始音量使用后续 LoadAllSettings 加载的保存值）
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
        sfxSource.volume = SFXSourceBaseVolume;

        // 尝试自动绑定 AudioMixer Group
        // 注意：如果预制体中已通过 Inspector 手动设置 outputAudioMixerGroup，则无需此步骤
        if (audioMixer != null)
        {
            bool foundMusic = false;
            bool foundSFX = false;
            AudioMixerGroup[] groups = audioMixer.FindMatchingGroups("");
            foreach (var group in groups)
            {
                if (group.name == "Music")
                {
                    sourceA.outputAudioMixerGroup = group;
                    sourceB.outputAudioMixerGroup = group;
                    foundMusic = true;
                }
                else if (group.name == "SFX")
                {
                    sfxSource.outputAudioMixerGroup = group;
                    foundSFX = true;
                }
            }

            if (!foundMusic)
                Debug.LogError("[MusicManager] 未在 MusicMixer 中找到 \"Music\" Group！请确保已创建该子组。");
            if (!foundSFX)
                Debug.LogError("[MusicManager] 未在 MusicMixer 中找到 \"SFX\" Group！请确保已创建该子组。");
            if (foundMusic && foundSFX)
                Debug.Log("[MusicManager] AudioMixer Group 绑定成功：Music → sourceA/B，SFX → sfxSource");
        }
    }

    /// <summary>
    /// 作用: 从 PlayerPrefs 加载所有持久化设置
    /// </summary>
    private void LoadAllSettings()
    {
        musicVolume = PlayerPrefs.GetFloat(PREF_MUSIC_VOLUME, 1f);
        sfxVolume = PlayerPrefs.GetFloat(PREF_SFX_VOLUME, 1f);
        enableSpecialMusic = PlayerPrefs.GetInt(PREF_SPECIAL_MUSIC, 0) == 1;
    }

    /// <summary>
    /// 作用: 将当前音量设置写入 AudioMixer（若已配置），否则直接更新 AudioSource 音量
    /// </summary>
    private void ApplyVolumeSettings()
    {
        if (audioMixer != null)
        {
            // 先验证参数名是否存在（Unity 的 SetFloat 对不存在的参数会静默忽略）
            float testVal;
            bool musicParamExists = audioMixer.GetFloat(musicVolumeParam, out testVal);
            bool sfxParamExists = audioMixer.GetFloat(sfxVolumeParam, out testVal);

            if (!musicParamExists)
                Debug.LogError($"[MusicManager] AudioMixer 中未找到暴露参数 \"{musicVolumeParam}\"！" +
                               "请在 MusicMixer 中暴露 Music Group 的 Volume，并命名为 \"MusicVolume\"。");
            if (!sfxParamExists)
                Debug.LogError($"[MusicManager] AudioMixer 中未找到暴露参数 \"{sfxVolumeParam}\"！" +
                               "请在 MusicMixer 中暴露 SFX Group 的 Volume，并命名为 \"SFXVolume\"。");

            if (musicParamExists)
                audioMixer.SetFloat(musicVolumeParam, LinearToDecibel(musicVolume));
            if (sfxParamExists)
                audioMixer.SetFloat(sfxVolumeParam, LinearToDecibel(sfxVolume));
        }
        else
        {
            // 无 AudioMixer：直接设置 AudioSource 音量
            float target = MusicSourceBaseVolume;
            AudioSource activeSrc = GetActiveSource();
            if (activeSrc != null && activeSrc.isPlaying && !isCrossfading && !isPlayingSpecialMusic)
                activeSrc.volume = target;
        }
    }

    #endregion

    #region 场景音乐切换

    /// <summary>
    /// 作用: 监听场景切换事件。场景名相同时不中断音乐（重新开始），
    ///       场景名不同时执行淡入淡出切换。
    /// </summary>
    private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        // 首次启动：音乐尚未开始播放（currentTrack 为空），
        // 由 DelayedStartPlayback 协程负责初始播放。
        // activeSceneChanged 在场景首次加载完成后也会触发，
        // 若不跳过则会通过 FullCrossfade 绕过延迟协程直接播放音乐。
        if (string.IsNullOrEmpty(currentTrack))
            return;

        // 如果正在播放特殊音乐，先取消并恢复 Game 音乐
        if (isPlayingSpecialMusic)
        {
            CancelSpecialMusic();
        }

        // 有挂起的过渡目标（由 OnSceneTransitionStart 设置）
        if (!string.IsNullOrEmpty(pendingTargetScene))
        {
            string target = pendingTargetScene;
            pendingTargetScene = "";

            // 目标场景与当前相同 → 不中断音乐
            if (target == currentTrack)
            {
                Debug.Log($"[MusicManager] 同一场景 ({target}) 重新加载，音乐不中断。");
                return;
            }

            // 淡出已在 OnSceneTransitionStart 中启动，这里等待完成后淡入新音乐
            StartCoroutine(WaitFadeOutThenFadeIn(target));
            return;
        }

        // 未通过 OnSceneTransitionStart 通知的场景切换（如编辑器直接加载场景）
        if (oldScene.name != newScene.name)
        {
            if (newScene.name == currentTrack) return;

            StartCoroutine(FullCrossfade(newScene.name));
        }
    }

    /// <summary>
    /// 作用: 由 SceneTransitionManager 在开始加载目标场景前调用，提前启动淡出旧音乐。
    ///       若目标场景与当前相同则不做任何操作。
    /// </summary>
    /// <param name="targetSceneName">目标场景名称</param>
    public void OnSceneTransitionStart(string targetSceneName)
    {
        if (isCrossfading) return;

        string sceneName = ExtractSceneName(targetSceneName);

        // 同一场景重载，不淡出
        if (sceneName == currentTrack)
        {
            pendingTargetScene = "";
            return;
        }

        pendingTargetScene = sceneName;

        AudioSource activeSrc = GetActiveSource();
        if (activeSrc != null && activeSrc.isPlaying && activeSrc.clip != null)
        {
            StartCoroutine(FadeOutActiveSource());
        }
    }

    /// <summary>
    /// 作用: 淡出当前激活音源（由 OnSceneTransitionStart 触发）
    /// </summary>
    private IEnumerator FadeOutActiveSource()
    {
        isCrossfading = true;
        AudioSource src = GetActiveSource();

        float elapsed = 0f;
        float startVol = MusicSourceBaseVolume;
        while (elapsed < crossfadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            src.volume = Mathf.Lerp(startVol, 0f, elapsed / crossfadeDuration);
            yield return null;
        }

        src.volume = 0f;
        isCrossfading = false;
    }

    /// <summary>
    /// 作用: 等待淡出完成后，切换到新场景音乐并淡入
    /// </summary>
    private IEnumerator WaitFadeOutThenFadeIn(string targetScene)
    {
        // 等待淡出协程完成（最多等待 crossfadeDuration + 缓冲）
        float waited = 0f;
        while (isCrossfading && waited < crossfadeDuration + 0.2f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        // 确保旧音源完全静音并停止
        AudioSource oldSrc = GetActiveSource();
        oldSrc.volume = 0f;
        oldSrc.Stop();

        // 加载新场景音乐
        AudioClip newClip = GetClipForScene(targetScene);
        if (newClip == null)
        {
            Debug.LogWarning($"[MusicManager] 场景 \"{targetScene}\" 未配置对应音乐，音乐将停止。");
            currentTrack = targetScene;
            yield break;
        }

        // 切换到另一个音源
        SwitchActiveSource();
        AudioSource newSrc = GetActiveSource();
        newSrc.clip = newClip;
        newSrc.time = 0f;
        newSrc.volume = 0f;
        newSrc.Play();

        // 淡入新音乐（目标音量使用 MusicSourceBaseVolume，兼容有/无 AudioMixer 两种情况）
        yield return StartCoroutine(FadeSourceVolume(newSrc, 0f, MusicSourceBaseVolume, crossfadeDuration));

        currentTrack = targetScene;
    }

    /// <summary>
    /// 作用: 完整的淡出→切换→淡入流程（用于未经 OnSceneTransitionStart 通知的场景切换）
    /// </summary>
    private IEnumerator FullCrossfade(string targetScene)
    {
        if (isCrossfading) yield break;
        isCrossfading = true;

        // 阶段一：淡出当前音乐
        AudioSource oldSrc = GetActiveSource();
        if (oldSrc != null && oldSrc.isPlaying)
        {
            yield return StartCoroutine(FadeSourceVolume(oldSrc, oldSrc.volume, 0f, crossfadeDuration));
            oldSrc.volume = 0f;
            oldSrc.Stop();
        }

        // 阶段二：切换并淡入新音乐
        AudioClip newClip = GetClipForScene(targetScene);
        if (newClip == null)
        {
            Debug.LogWarning($"[MusicManager] 场景 \"{targetScene}\" 未配置对应音乐，音乐将停止。");
            currentTrack = targetScene;
            isCrossfading = false;
            yield break;
        }

        SwitchActiveSource();
        AudioSource newSrc = GetActiveSource();
        newSrc.clip = newClip;
        newSrc.time = 0f;
        newSrc.volume = 0f;
        newSrc.Play();

        yield return StartCoroutine(FadeSourceVolume(newSrc, 0f, MusicSourceBaseVolume, crossfadeDuration));

        currentTrack = targetScene;
        isCrossfading = false;
    }

    /// <summary>
    /// 作用: 立即播放指定场景的音乐（无淡入，用于首次启动）
    /// </summary>
    private void PlaySceneMusicImmediate(string sceneName)
    {
        if (!string.IsNullOrEmpty(currentTrack)) return;

        AudioClip clip = GetClipForScene(sceneName);
        if (clip == null) return;

        sourceA.clip = clip;
        sourceA.time = 0f;
        sourceA.volume = MusicSourceBaseVolume;
        sourceA.Play();

        currentTrack = sceneName;
        activeIndex = 0;
    }

    #endregion

    #region 游戏失败特殊音乐

    /// <summary>
    /// 作用: GameManager 在 GameOver 时调用。
    ///       若玩家在设置中启用特殊失败音乐且当前在 Game 场景，
    ///       则保存进度、淡出 Game 音乐、播放特殊音乐。
    /// </summary>
    public void OnGameOver()
    {
        if (isPlayingSpecialMusic) return;

        if (enableSpecialMusic && gameOverSpecialMusic != null && currentTrack == "Game")
        {
            StartCoroutine(HandleSpecialGameOverMusic());
        }
    }

    /// <summary>
    /// 作用: 播放特殊失败音乐的完整流程：
    ///       保存当前进度 → 淡出 Game 音乐 → 播放特殊音乐 → 等待播完 → 恢复
    /// </summary>
    private IEnumerator HandleSpecialGameOverMusic()
    {
        isPlayingSpecialMusic = true;
        specialMusicFinished = false;

        AudioSource gameSrc = GetActiveSource();

        // 保存当前 Game 音乐的播放进度（采样秒数）
        if (gameSrc != null && gameSrc.clip != null)
        {
            savedGameMusicTime = gameSrc.time;
        }

        // 淡出 Game 音乐，然后暂停（保留播放位置）
        if (gameSrc != null && gameSrc.isPlaying)
        {
            yield return StartCoroutine(FadeSourceVolume(gameSrc, gameSrc.volume, 0f, gameOverFadeDuration));
            gameSrc.volume = 0f;
            gameSrc.Pause();
        }

        // 使用闲置音源播放特殊失败音乐（关闭循环）
        AudioSource specialSrc = GetInactiveSource();
        specialSrc.loop = false;
        specialSrc.clip = gameOverSpecialMusic;
        specialSrc.time = 0f;
        specialSrc.volume = 0f;
        specialSrc.Play();

        // 淡入特殊音乐
        yield return StartCoroutine(FadeSourceVolume(specialSrc, 0f, MusicSourceBaseVolume, gameOverFadeDuration));

        // 标记特殊音乐已开始播放，等待自然结束
        specialMusicFinished = true;
        // Update() 中检测到 specialMusicFinished && !isPlaying → 恢复 Game 音乐
    }

    /// <summary>
    /// 作用: 特殊音乐播放完毕后，从中断处恢复 Game 音乐并淡入
    /// </summary>
    private IEnumerator ResumeGameMusicFromSavedPosition()
    {
        // 停止闲置音源的特殊音乐播放，恢复循环模式
        AudioSource specialSrc = GetInactiveSource();
        specialSrc.Stop();
        specialSrc.loop = true;

        // 从保存的进度恢复 Game 音乐
        AudioSource gameSrc = GetActiveSource();
        gameSrc.time = savedGameMusicTime;
        gameSrc.volume = 0f;
        gameSrc.UnPause();

        // 淡入恢复
        yield return StartCoroutine(FadeSourceVolume(gameSrc, 0f, MusicSourceBaseVolume, gameOverFadeDuration));
    }

    /// <summary>
    /// 作用: 取消正在播放的特殊音乐，恢复 Game 音乐。
    ///       用于场景切换等需要中断特殊音乐的场合。
    /// </summary>
    private void CancelSpecialMusic()
    {
        StopAllCoroutines();
        isPlayingSpecialMusic = false;
        specialMusicFinished = false;

        // 停止闲置音源的特殊音乐
        AudioSource specialSrc = GetInactiveSource();
        if (specialSrc != null)
        {
            specialSrc.Stop();
            specialSrc.loop = true;
        }

        // 恢复 Game 音乐播放
        AudioSource gameSrc = GetActiveSource();
        if (gameSrc != null)
        {
            gameSrc.UnPause();
            if (!gameSrc.isPlaying && gameSrc.clip != null)
            {
                gameSrc.Play();
            }
            gameSrc.volume = MusicSourceBaseVolume;
        }
    }

    #endregion

    #region 音量控制（Public API）

    /// <summary>
    /// 作用: 设置音乐音量（0~1 线性），保存到 PlayerPrefs，
    ///       并通过 AudioMixer 或直接 AudioSource.volume 立即生效
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(PREF_MUSIC_VOLUME, musicVolume);
        PlayerPrefs.Save();

        if (audioMixer != null)
        {
            audioMixer.SetFloat(musicVolumeParam, LinearToDecibel(musicVolume));
        }
        else
        {
            // 无 AudioMixer：直接更新正在播放的 AudioSource 音量
            float target = MusicSourceBaseVolume;
            AudioSource activeSrc = GetActiveSource();
            if (activeSrc != null && activeSrc.isPlaying && !isCrossfading)
                activeSrc.volume = target;
        }
    }

    /// <summary>
    /// 作用: 设置音效音量（0~1 线性），保存到 PlayerPrefs，
    ///       并通过 AudioMixer 或直接 AudioSource.volume 立即生效
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(PREF_SFX_VOLUME, sfxVolume);
        PlayerPrefs.Save();

        if (audioMixer != null)
        {
            audioMixer.SetFloat(sfxVolumeParam, LinearToDecibel(sfxVolume));
        }
        else
        {
            // 无 AudioMixer：直接更新音效 AudioSource 音量
            if (sfxSource != null)
                sfxSource.volume = sfxVolume;
        }
    }

    /// <summary>
    /// 作用: 设置是否启用特殊失败音乐，保存到 PlayerPrefs
    /// </summary>
    public void SetSpecialGameOverMusicEnabled(bool enabled)
    {
        enableSpecialMusic = enabled;
        PlayerPrefs.SetInt(PREF_SPECIAL_MUSIC, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public float GetMusicVolume() => musicVolume;
    public float GetSFXVolume() => sfxVolume;
    public bool GetSpecialGameOverMusicEnabled() => enableSpecialMusic;

    /// <summary>
    /// 作用: 播放按钮点击音效（通过 sfxSource 播放 buttonSfx 片段）
    /// </summary>
    public void PlayButtonSfx()
    {
        if (buttonSfx != null && sfxSource != null)
            sfxSource.PlayOneShot(buttonSfx);
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 作用: 根据场景名返回对应的 AudioClip
    /// </summary>
    private AudioClip GetClipForScene(string sceneName)
    {
        if (sceneName == "MainMenu") return mainMenuMusic;
        if (sceneName == "Game") return gameMusic;
        return null;
    }

    /// <summary>
    /// 作用: 获取当前正在使用的音乐 AudioSource
    /// </summary>
    private AudioSource GetActiveSource()
    {
        return activeIndex == 0 ? sourceA : sourceB;
    }

    /// <summary>
    /// 作用: 获取当前闲置的音乐 AudioSource（用于播放临时音乐）
    /// </summary>
    private AudioSource GetInactiveSource()
    {
        return activeIndex == 0 ? sourceB : sourceA;
    }

    /// <summary>
    /// 作用: 切换激活的音源索引（0↔1）
    /// </summary>
    private void SwitchActiveSource()
    {
        activeIndex = (activeIndex == 0) ? 1 : 0;
    }

    /// <summary>
    /// 作用: 在指定时长内平滑过渡 AudioSource 的音量
    /// </summary>
    /// <param name="src">目标 AudioSource</param>
    /// <param name="from">起始音量（0~1）</param>
    /// <param name="to">目标音量（0~1）</param>
    /// <param name="duration">过渡时长（秒）</param>
    private IEnumerator FadeSourceVolume(AudioSource src, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            src.volume = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        src.volume = to;
    }

    /// <summary>
    /// 作用: 将线性音量（0~1）转换为分贝值（-80~0 dB）
    /// </summary>
    private static float LinearToDecibel(float linear)
    {
        if (linear <= 0.0001f) return -80f;
        return Mathf.Log10(linear) * 20f;
    }

    /// <summary>
    /// 作用: 从场景路径中提取场景名（如 "Assets/Scenes/Game.unity" → "Game"）
    /// </summary>
    private static string ExtractSceneName(string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath)) return "";

        // 处理路径格式（包含 "/" 则为路径）
        if (scenePath.Contains("/"))
        {
            int start = scenePath.LastIndexOf('/') + 1;
            int end = scenePath.LastIndexOf('.');
            if (end > start)
                return scenePath.Substring(start, end - start);
            return scenePath.Substring(start);
        }

        return scenePath;
    }

    #endregion
}
