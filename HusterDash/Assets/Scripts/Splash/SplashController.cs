using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 文件名: SplashController.cs
/// 作用: 开屏动画控制器，播放"Huster Dash"逐字飞入动画后过渡到 MainMenu 场景
/// 主要功能:
///    1. 动态创建 Canvas、背景和逐字 TMP_Text 组件
///    2. 智能分配飞入方向（360度均匀分布，避免扎堆）
///    3. 用 EaseOutBounce 缓动实现弹跳落地效果
///    4. 字符错开飞入 → 保持静止 → 整体淡出 → 触发场景过渡
/// </summary>
public class SplashController : MonoBehaviour
{
    [Header("动画时间")]
    [Tooltip("飞入动画总时长（秒），所有字符从开始出发到全部归位的时间")]
    public float flyInDuration = 3.0f;

    [Tooltip("所有字符归位后静止保持的时长（秒）")]
    public float holdDuration = 1.0f;

    [Tooltip("文字淡出的时长（秒），背景始终保持不透明")]
    public float fadeOutDuration = 0.5f;

    [Header("文字排列")]
    [Tooltip("文字字体资产（TMP_FontAsset），拖入字体文件")]
    public TMP_FontAsset textFont;

    [Tooltip("文字字号大小")]
    public float fontSize = 80f;

    [Tooltip("字符间的水平间距（像素），控制最终排列时每个字符之间的空隙")]
    public float characterSpacing = 10f;

    [Header("视觉样式")]
    [Tooltip("文字颜色")]
    public Color textColor = new Color(0.906f, 0.322f, 0.624f, 1f); // #E7529F

    [Tooltip("背景颜色")]
    public Color backgroundColor = new Color(0.314f, 0.604f, 0.906f, 1f); // #509AE7

    /// <summary>
    /// 需要展示的文字内容
    /// </summary>
    private const string DisplayText = "Huster Dash";

    /// <summary>
    /// Canvas 参考分辨率宽度
    /// </summary>
    private const float ReferenceWidth = 1920f;

    /// <summary>
    /// Canvas 参考分辨率高度
    /// </summary>
    private const float ReferenceHeight = 1080f;

    /// <summary>
    /// 每个字符的独立飞行时间占总飞入时长的比例，
    /// 剩余时间平分给所有字符作为错开延迟
    /// </summary>
    private const float IndividualFlyRatio = 0.6f;

    /// <summary>
    /// 飞入距离在超出屏幕边界后的额外余量（像素）
    /// </summary>
    private const float OffScreenMargin = 120f;

    /// <summary>
    /// Canvas 组件引用
    /// </summary>
    private Canvas canvas;

    /// <summary>
    /// 全屏背景 Image
    /// </summary>
    private Image backgroundImage;

    /// <summary>
    /// 文字容器的 RectTransform，用于居中定位
    /// </summary>
    private RectTransform textContainerRect;

    /// <summary>
    /// 文字容器的 CanvasGroup，只控制文字淡出，不影响背景
    /// </summary>
    private CanvasGroup textContainerCanvasGroup;

    /// <summary>
    /// 所有字符的数据列表
    /// </summary>
    private List<CharacterData> characters = new List<CharacterData>();

    /// <summary>
    /// 非空格字符的索引列表（跳过空格）
    /// </summary>
    private List<int> activeCharIndices = new List<int>();

    /// <summary>
    /// 每个字符在屏幕上的目标位置（anchoredPosition）
    /// </summary>
    private Vector2[] targetPositions;

    /// <summary>
    /// 自动计算的字符错开延迟（秒）
    /// </summary>
    private float staggerDelay;

    /// <summary>
    /// 自动计算的飞入距离（像素），确保所有字符从屏幕外飞入
    /// </summary>
    private float flyStartDistance;

    /// <summary>
    /// 单个字符的数据结构，包含 TMP_Text 组件及其 RectTransform
    /// </summary>
    private struct CharacterData
    {
        public TMP_Text textComponent;
        public RectTransform rectTransform;
    }

    /// <summary>
    /// 作用: 初始化 Canvas UI 和字符组件
    /// </summary>
    private void Awake()
    {
        CreateCanvas();
        CreateBackground();
        CreateCharacters();
    }

    /// <summary>
    /// 作用: 启动开屏动画序列
    /// </summary>
    private void Start()
    {
        StartCoroutine(PlaySplashSequence());
    }

    /// <summary>
    /// 作用: 创建 Canvas，设置为 ScreenSpaceOverlay 模式
    /// </summary>
    private void CreateCanvas()
    {
        GameObject canvasGo = new GameObject("SplashCanvas");
        canvasGo.transform.SetParent(transform);

        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0; // 低于 SceneTransitionManager 的 9999，确保过渡遮罩覆盖开屏动画

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);

        canvasGo.AddComponent<GraphicRaycaster>();
    }

    /// <summary>
    /// 作用: 创建全屏纯色背景 Image，背景始终不透明以遮挡空场景
    /// </summary>
    private void CreateBackground()
    {
        GameObject bgGo = new GameObject("Background");
        bgGo.transform.SetParent(canvas.transform, false);

        backgroundImage = bgGo.AddComponent<Image>();
        backgroundImage.color = backgroundColor;

        RectTransform bgRect = backgroundImage.rectTransform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// 作用: 为 DisplayText 中的每个字符创建独立的 TMP_Text，
    ///       使用 HorizontalLayoutGroup 自动排列后捕获位置，计算居中偏移，
    ///       再移除布局组使字符可独立移动。
    ///       同时为文字容器添加独立 CanvasGroup，仅控制文字淡出。
    /// </summary>
    private void CreateCharacters()
    {
        // 创建字符容器，居中于 Canvas
        GameObject container = new GameObject("TextContainer");
        textContainerRect = container.AddComponent<RectTransform>();
        textContainerRect.SetParent(canvas.transform, false);
        textContainerRect.anchorMin = new Vector2(0.5f, 0.5f);
        textContainerRect.anchorMax = new Vector2(0.5f, 0.5f);
        textContainerRect.sizeDelta = Vector2.zero;
        textContainerRect.anchoredPosition = Vector2.zero;

        // 文字容器独立的 CanvasGroup，淡出时只影响文字，不影响背景
        textContainerCanvasGroup = container.AddComponent<CanvasGroup>();

        // 使用 HorizontalLayoutGroup 自动水平排列字符
        HorizontalLayoutGroup layoutGroup = container.AddComponent<HorizontalLayoutGroup>();
        layoutGroup.childAlignment = TextAnchor.MiddleCenter;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = true;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.spacing = characterSpacing;

        // 为每个字符创建独立 TMP_Text
        for (int i = 0; i < DisplayText.Length; i++)
        {
            string charStr = DisplayText[i].ToString();

            GameObject charGo = new GameObject(string.Format("Char_{0}_{1}", i, charStr));
            charGo.transform.SetParent(textContainerRect, false);

            TMP_Text tmpText = charGo.AddComponent<TextMeshProUGUI>();
            tmpText.text = charStr;
            tmpText.fontSize = fontSize;
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.color = textColor;

            if (textFont != null)
            {
                tmpText.font = textFont;
            }

            // 空格字符设为不可见但保留占位宽度
            if (charStr == " ")
            {
                tmpText.color = new Color(0f, 0f, 0f, 0f);
            }

            RectTransform rect = tmpText.rectTransform;
            rect.sizeDelta = Vector2.zero;

            characters.Add(new CharacterData
            {
                textComponent = tmpText,
                rectTransform = rect
            });
        }

        // 强制立即重建布局，获取每个字符的最终排列位置
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(textContainerRect);

        // 捕获每个字符的布局位置
        targetPositions = new Vector2[characters.Count];
        for (int i = 0; i < characters.Count; i++)
        {
            targetPositions[i] = characters[i].rectTransform.anchoredPosition;
        }

        // 收集非空格字符索引
        for (int i = 0; i < characters.Count; i++)
        {
            if (characters[i].textComponent.text != " ")
            {
                activeCharIndices.Add(i);
            }
        }

        // 计算整体居中对齐偏移，确保 "Huster Dash" 在画面水平居中
        float minX = float.MaxValue, maxX = float.MinValue;
        for (int i = 0; i < characters.Count; i++)
        {
            float x = targetPositions[i].x;
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
        }
        float centerOffsetX = -(minX + maxX) * 0.5f;
        for (int i = 0; i < characters.Count; i++)
        {
            targetPositions[i].x += centerOffsetX;
        }

        // 移除布局组，此后字符各自独立定位，可自由移动
        Destroy(layoutGroup);
    }

    /// <summary>
    /// 作用: 播放完整的开屏动画序列：
    ///       自动计算错开延迟和飞入距离 → 逐字飞入 → 保持静止 → 文字淡出 → 触发场景过渡。
    ///       背景始终不透明，确保 MainMenu 加载前画面不出现空场景。
    /// </summary>
    private IEnumerator PlaySplashSequence()
    {
        // 第一阶段：逐字飞入动画
        yield return StartCoroutine(FlyInPhase());

        // 第二阶段：保持静止
        yield return new WaitForSecondsRealtime(holdDuration);

        // 第三阶段：仅文字淡出，背景保持不透明
        yield return StartCoroutine(FadeOutPhase());

        // 文字淡出后立即触发场景过渡，背景直到 MainMenu 加载完成前始终显示蓝色
        SceneTransitionManager.LaunchTransition("MainMenu");
    }

    /// <summary>
    /// 作用: 执行逐字飞入动画。
    ///       自动计算错开延迟和飞入距离，将 360° 均匀分成 N 个扇区，
    ///       每个字符在各自扇区内随机取一个角度，打乱后确保方向均匀分散。
    /// </summary>
    private IEnumerator FlyInPhase()
    {
        int activeCount = activeCharIndices.Count;
        int totalCount = characters.Count;

        // 自动计算错开延迟：最后一个字符的出发时间 + 飞行时间 = flyInDuration
        // 每个字符飞行时间 = flyInDuration * IndividualFlyRatio
        float individualFlyTime = flyInDuration * IndividualFlyRatio;
        staggerDelay = (flyInDuration - individualFlyTime) / Mathf.Max(1, activeCount - 1);

        // 生成均匀分布的飞入方向角度，仅对非空格字符
        float[] angles = GenerateDistributedAngles(activeCount);

        // 计算每个字符的飞入方向
        Vector2[] directions = new Vector2[totalCount];
        int angleIdx = 0;
        for (int i = 0; i < totalCount; i++)
        {
            if (characters[i].textComponent.text == " ")
            {
                directions[i] = Vector2.zero;
                continue;
            }
            directions[i] = new Vector2(Mathf.Cos(angles[angleIdx]), Mathf.Sin(angles[angleIdx]));
            angleIdx++;
        }

        // 自动计算飞入距离：确保所有字符从屏幕外出发
        flyStartDistance = CalculateFlyDistance(directions);

        // 计算每个字符的起始位置并设置初始状态
        Vector2[] startPositions = new Vector2[totalCount];
        for (int i = 0; i < totalCount; i++)
        {
            if (characters[i].textComponent.text == " ")
            {
                // 空格保持在目标位置，始终透明
                startPositions[i] = targetPositions[i];
                Color spaceColor = characters[i].textComponent.color;
                spaceColor.a = 0f;
                characters[i].textComponent.color = spaceColor;
                continue;
            }

            startPositions[i] = targetPositions[i] + directions[i] * flyStartDistance;

            // 设置初始状态：透明且在屏幕外起始位置
            characters[i].rectTransform.anchoredPosition = startPositions[i];
            Color c = textColor;
            c.a = 0f;
            characters[i].textComponent.color = c;
        }

        // 启动所有非空格字符的飞入协程（各自有错开延迟）
        List<Coroutine> flyCoroutines = new List<Coroutine>();
        int activeOrder = 0;
        for (int i = 0; i < totalCount; i++)
        {
            if (characters[i].textComponent.text == " ")
            {
                continue;
            }

            float delay = activeOrder * staggerDelay;
            activeOrder++;
            flyCoroutines.Add(StartCoroutine(
                FlyCharacter(characters[i], startPositions[i], targetPositions[i], delay, individualFlyTime)
            ));
        }

        // 等待所有字符飞入完成
        foreach (Coroutine coroutine in flyCoroutines)
        {
            yield return coroutine;
        }
    }

    /// <summary>
    /// 作用: 将 360° 分为 activeCount 个扇区，每个扇区内随机取一个角度，
    ///       然后 Fisher-Yates 洗牌打乱，确保视觉上方向均匀分散。
    /// </summary>
    /// <param name="activeCount">非空格字符数量</param>
    /// <returns>每个字符的飞入方向角度（弧度）</returns>
    private float[] GenerateDistributedAngles(int activeCount)
    {
        float[] angles = new float[activeCount];
        float sectorSize = 360f / activeCount;

        for (int i = 0; i < activeCount; i++)
        {
            float baseAngle = i * sectorSize;
            float margin = sectorSize * 0.15f;
            float randomOffset = Random.Range(margin, sectorSize - margin);
            angles[i] = (baseAngle + randomOffset) * Mathf.Deg2Rad;
        }

        for (int i = activeCount - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            float temp = angles[i];
            angles[i] = angles[j];
            angles[j] = temp;
        }

        return angles;
    }

    /// <summary>
    /// 作用: 自动计算飞入距离，确保所有字符从摄像机拍摄范围外飞入。
    ///       对每个非空格字符，计算从目标位置沿飞入方向到达屏幕边界所需的距离，
    ///       取最大值并加上额外余量。
    /// </summary>
    /// <param name="directions">每个字符的飞入方向向量</param>
    /// <returns>统一飞入距离（像素）</returns>
    private float CalculateFlyDistance(Vector2[] directions)
    {
        float halfW = ReferenceWidth * 0.5f;
        float halfH = ReferenceHeight * 0.5f;
        float maxDistance = 0f;

        for (int i = 0; i < characters.Count; i++)
        {
            if (characters[i].textComponent.text == " ")
            {
                continue;
            }

            Vector2 pos = targetPositions[i];
            Vector2 dir = directions[i];

            // 计算沿方向到达屏幕矩形边界的距离
            float distToRect = RayToRectDistance(pos, dir, halfW, halfH);
            if (distToRect > maxDistance)
            {
                maxDistance = distToRect;
            }
        }

        return maxDistance + OffScreenMargin;
    }

    /// <summary>
    /// 作用: 计算从点 point 沿方向 direction 到达矩形边界的距离。
    ///       矩形以原点为中心，半宽 halfW，半高 halfH。
    /// </summary>
    /// <param name="point">起点位置</param>
    /// <param name="direction">方向向量</param>
    /// <param name="halfW">矩形半宽</param>
    /// <param name="halfH">矩形半高</param>
    /// <returns>到矩形边界的距离，若方向背离矩形则返回 0</returns>
    private static float RayToRectDistance(Vector2 point, Vector2 direction, float halfW, float halfH)
    {
        float minDist = float.MaxValue;

        // 左边界 x = -halfW
        if (direction.x < -0.0001f)
        {
            float t = (-halfW - point.x) / direction.x;
            if (t > 0f && t < minDist) minDist = t;
        }
        // 右边界 x = +halfW
        if (direction.x > 0.0001f)
        {
            float t = (halfW - point.x) / direction.x;
            if (t > 0f && t < minDist) minDist = t;
        }
        // 下边界 y = -halfH
        if (direction.y < -0.0001f)
        {
            float t = (-halfH - point.y) / direction.y;
            if (t > 0f && t < minDist) minDist = t;
        }
        // 上边界 y = +halfH
        if (direction.y > 0.0001f)
        {
            float t = (halfH - point.y) / direction.y;
            if (t > 0f && t < minDist) minDist = t;
        }

        return minDist < float.MaxValue ? minDist : 0f;
    }

    /// <summary>
    /// 作用: 单个字符的飞入协程。
    ///       等待错开延迟后，用 EaseOutBounce 缓动从屏幕外弹跳到目标位置，同时淡入。
    /// </summary>
    /// <param name="charData">字符数据</param>
    /// <param name="startPos">起始位置</param>
    /// <param name="targetPos">目标位置</param>
    /// <param name="delay">错开延迟（秒）</param>
    /// <param name="duration">飞行时长（秒）</param>
    private IEnumerator FlyCharacter(CharacterData charData, Vector2 startPos, Vector2 targetPos,
        float delay, float duration)
    {
        // 等待错开延迟
        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // 位置使用 EaseOutBounce 缓动
            float bounceT = EaseOutBounce(t);
            charData.rectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, bounceT);

            // 透明度在前半段（0~0.5）内完成淡入
            Color c = charData.textComponent.color;
            c.a = Mathf.Lerp(0f, 1f, Mathf.Clamp01(t * 2f));
            charData.textComponent.color = c;

            yield return null;
        }

        // 确保最终状态精确到位
        charData.rectTransform.anchoredPosition = targetPos;
        Color finalColor = charData.textComponent.color;
        finalColor.a = 1f;
        charData.textComponent.color = finalColor;
    }

    /// <summary>
    /// 作用: 仅淡出文字容器（背景保持不透明），通过 CanvasGroup.alpha 从 1 渐变到 0
    /// </summary>
    private IEnumerator FadeOutPhase()
    {
        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeOutDuration);
            textContainerCanvasGroup.alpha = 1f - t;
            yield return null;
        }

        textContainerCanvasGroup.alpha = 0f;
    }

    /// <summary>
    /// 作用: EaseOutBounce 缓动函数，模拟物体落地弹跳的物理效果。
    ///       输入 t ∈ [0, 1]，输出在 0 附近有多次衰减反弹后最终趋于 1。
    /// </summary>
    /// <param name="t">归一化时间 [0, 1]</param>
    /// <returns>缓动后的值</returns>
    private static float EaseOutBounce(float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;

        if (t < 1f / d1)
        {
            return n1 * t * t;
        }
        else if (t < 2f / d1)
        {
            return n1 * (t -= 1.5f / d1) * t + 0.75f;
        }
        else if (t < 2.5f / d1)
        {
            return n1 * (t -= 2.25f / d1) * t + 0.9375f;
        }
        else
        {
            return n1 * (t -= 2.625f / d1) * t + 0.984375f;
        }
    }

    /// <summary>
    /// 作用: 销毁时清理所有运行中的协程
    /// </summary>
    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}
