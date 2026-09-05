using UnityEngine;
using System;

public class LightingController : MonoBehaviour
{
    [Header("组件设置")]
    [Tooltip("主光源 (太阳)")]
    public Light targetLight;
    
    [Tooltip("夜晚模拟的室内补光灯 (如点光源或聚光灯)")]
    public Light indoorLight;

    [Header("调试模式 (用于快速测试效果)")]
    [Tooltip("勾选后，不再读取电脑系统时间，而是用下方的滑块手动控制时间")]
    public bool useManualTime = false;
    [Tooltip("手动时间进度：0=午夜，0.25=日出，0.5=正午，0.75=日落，1=午夜。在运行状态下拖动它！")]
    [Range(0f, 1f)]
    public float manualTimePercent = 0.5f;

    [Header("真实光照参数")]
    [Tooltip("最高日照角度 (正午时灯光下压的角度，建议70度)")]
    public float maxSunAltitude = 70f;
    [Tooltip("最大太阳光照强度 (控制在0.8-1.0之间)")]
    public float maxIntensity = 1.0f;
    [Tooltip("室内补光灯最大强度")]
    public float maxIndoorIntensity = 1.5f;
    [Tooltip("阴影强度 (0-1，设为0.4左右)")]
    [Range(0f, 1f)]
    public float shadowStrength = 0.4f;

    // 自定义颜色关键帧结构
    private struct TimeColorKey
    {
        public float time;
        public Color color;

        public TimeColorKey(float t, string hex)
        {
            time = t;
            ColorUtility.TryParseHtmlString(hex, out color);
        }
    }

    private TimeColorKey[] _colorKeys;

    void Start()
    {
        if (targetLight == null)
        {
            targetLight = GetComponent<Light>();
        }

        if (targetLight != null)
        {
            targetLight.shadows = LightShadows.Soft;
            targetLight.shadowStrength = shadowStrength;
        }

        if (indoorLight != null)
        {
            // 室内灯也可以开启柔和阴影，增加立体感
            indoorLight.shadows = LightShadows.Soft;
            indoorLight.shadowStrength = shadowStrength * 0.8f; // 室内阴影稍微弱一点
            indoorLight.intensity = 0f; // 初始默认关闭
        }

        // 初始化代码内置的一天颜色变化 (你提的需求色号)
        _colorKeys = new TimeColorKey[]
        {
            new TimeColorKey(0.00f, "#0B0C10"), // 0点：午夜 (深蓝黑)
            new TimeColorKey(0.20f, "#4A235A"), // 凌晨4:40：紫罗兰
            new TimeColorKey(0.23f, "#D291BC"), // 早上5:30：晨曦粉紫
            new TimeColorKey(0.25f, "#FF7F50"), // 早上6:00：日出橘红
            new TimeColorKey(0.35f, "#FFD700"), // 上午8:30：晨光金黄
            new TimeColorKey(0.50f, "#FFFFFA"), // 中午12点：正午亮白
            new TimeColorKey(0.65f, "#FFD700"), // 下午3:30：午后金黄
            new TimeColorKey(0.75f, "#FF7F50"), // 傍晚6:00：日落橘红
            new TimeColorKey(0.77f, "#D291BC"), // 傍晚6:30：晚霞粉紫
            new TimeColorKey(0.80f, "#4A235A"), // 晚上7:10：暮色紫罗兰
            new TimeColorKey(1.00f, "#0B0C10")  // 24点：午夜 (深蓝黑)
        };
    }

    void Update()
    {
        if (targetLight == null) return;

        float timePercent;

        // 判断是读取真实时间，还是读取手动滑块
        if (useManualTime)
        {
            timePercent = manualTimePercent;
        }
        else
        {
            DateTime now = DateTime.Now;
            timePercent = (float)now.TimeOfDay.TotalSeconds / 86400f; 
        }

        UpdateSunTransform(timePercent);
        UpdateSunColorAndIntensity(timePercent);
        UpdateIndoorLight(timePercent);
    }

    private void UpdateSunTransform(float timePercent)
    {
        // 1. 方位角 (Yaw，Y轴)：从东(90度) -> 南(180度) -> 西(270度)
        float yaw = Mathf.Lerp(90f, 270f, (timePercent - 0.25f) / 0.5f);

        // 2. 高度角 (Pitch，X轴)：正午(0.5)最高，早晚贴近地平线
        float pitch = 0f;
        if (timePercent >= 0.25f && timePercent <= 0.75f)
        {
            float dayProgress = (timePercent - 0.25f) / 0.5f; 
            pitch = Mathf.Sin(dayProgress * Mathf.PI) * maxSunAltitude;
        }
        else
        {
            pitch = -15f; 
        }

        targetLight.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void UpdateSunColorAndIntensity(float timePercent)
    {
        // 取色
        targetLight.color = EvaluateCustomGradient(timePercent);

        // 光强过渡：白天亮，夜晚极微弱
        float targetIntensity = 0f;
        if (timePercent > 0.20f && timePercent < 0.80f)
        {
            float dayProgress = (timePercent - 0.20f) / 0.60f;
            targetIntensity = Mathf.Lerp(maxIntensity * 0.3f, maxIntensity, Mathf.Sin(dayProgress * Mathf.PI));
        }
        else
        {
            targetIntensity = maxIntensity * 0.15f; 
        }
        
        // 使用 Lerp 平滑过渡强度
        targetLight.intensity = Mathf.Lerp(targetLight.intensity, targetIntensity, Time.deltaTime * 5f);
    }

    private void UpdateIndoorLight(float timePercent)
    {
        if (indoorLight == null) return;

        float targetIndoorIntensity = 0f;

        // 如果时间在晚上 (日落 0.75 之后，或者日出 0.25 之前)，开启室内灯
        if (timePercent >= 0.75f || timePercent <= 0.25f)
        {
            targetIndoorIntensity = maxIndoorIntensity;
        }
        else
        {
            // 白天关闭室内灯
            targetIndoorIntensity = 0f;
        }

        // 使用 Lerp 平滑过渡室内灯的开关，避免突然亮起/熄灭
        indoorLight.intensity = Mathf.Lerp(indoorLight.intensity, targetIndoorIntensity, Time.deltaTime * 2f);
    }

    private Color EvaluateCustomGradient(float t)
    {
        t = Mathf.Clamp01(t);
        for (int i = 0; i < _colorKeys.Length - 1; i++)
        {
            if (t >= _colorKeys[i].time && t <= _colorKeys[i + 1].time)
            {
                float segmentLength = _colorKeys[i + 1].time - _colorKeys[i].time;
                float segmentT = (t - _colorKeys[i].time) / segmentLength;
                return Color.Lerp(_colorKeys[i].color, _colorKeys[i + 1].color, segmentT);
            }
        }
        return Color.black; 
    }
}