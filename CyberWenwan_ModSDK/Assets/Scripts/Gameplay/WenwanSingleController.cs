using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

/// <summary>
/// 专用于“单枚模式 (Mode_Single)”的控制器
/// 优化版：LRU 淘汰缓存池极速切换 + 移除运行时物理烘焙
/// </summary>
public class WenwanSingleController : MonoBehaviour
{
    public static WenwanSingleController Instance { get; private set; }

    [Header("状态同步")]
    public bool isInspecting = false; 

    [Header("配置引用")]
    public WenwanSpeciesConfig currentConfig;

    [Header("渲染目标 (运行时自动获取)")]
    [Tooltip("不再需要手动拖拽，代码会自动从生成的 Prefab 中抓取")]
    public MeshRenderer targetRenderer;

    [Header("性能优化 (Performance)")]
    [Tooltip("内存中最多同时保留几枚单核？(建议2-3，防止撑爆内存)")]
    public int maxPoolSize = 2; 

    // LRU 淘汰缓存池 (安全保留原有功能，不影响其他数据逻辑)
    private Dictionary<string, GameObject> _instancePool = new Dictionary<string, GameObject>();
    private List<string> _lruQueue = new List<string>();

    [Header("当前进度")]
    [Range(0f, 1f)]
    public float currentProgress = 0.0f;

    [Header("玉化进度 (只读/保存)")]
    [Range(0f, 1f)]
    public float currentJadeProgress = 0.0f;

    [Header("灰尘状态 (只读)")]
    [Range(0f, 1f)]
    public float currentDust = 0.0f; // 0=无灰, 1=满灰

    [HideInInspector]
    [Tooltip("此数值现在仅作为物理旋转的动力源")]
    public float totalInputEnergy = 0f;

    private MaterialPropertyBlock _propBlock;
    private Vector3 _initialScale = Vector3.one;
    private float _saveTimer = 0f;

    // 实例化的预制体记录
    private GameObject _currentInstance;

    // 旋转动力学
    private float _rotationVelocity = 0f;
    private const float Friction = 0.98f; 
    private float _lastEnergy;

    // Shader 属性 ID 缓存
    private static readonly int ProgressID = Shader.PropertyToID("_Progress");
    private static readonly int JadeProgressID = Shader.PropertyToID("_JadeProgress");
    private static readonly int DustAmountID = Shader.PropertyToID("_DustAmount");
    private static readonly int DustColorID = Shader.PropertyToID("_DustColor");
    private static readonly int DustMapID = Shader.PropertyToID("_DustMap");

    private void Awake()
    {
        if (Instance == null) Instance = this;
        _propBlock = new MaterialPropertyBlock();
    }

    private void Start()
    {
        // 关键：启动时强制加载一次
        if (currentConfig != null)
        {
            LoadSpecificProgress();
            CalculateDustAccumulation(); // 计算离线期间积灰
            _lastEnergy = totalInputEnergy;
        }

        RefreshSpeciesLook();
        UpdateMaterialVisuals();
        
        // 处理初始的放大弹出动画
        if (targetRenderer != null)
        {
            targetRenderer.transform.localScale = Vector3.zero;
        }
        StartCoroutine(ShowAfterTransparent());
    }

    private IEnumerator ShowAfterTransparent()
    {
        yield return new WaitForSeconds(0.4f);
        if (targetRenderer != null)
        {
            targetRenderer.transform.localScale = _initialScale;
        }
    }

    private void Update()
    {
        UpdateMaterialVisuals();
        HandleSelfRotation();

        // 自动保存计时器 (10秒一次)
        _saveTimer += Time.deltaTime;
        if (_saveTimer > 10f)
        {
            SaveSpecificProgress();
            SaveToGlobalManager();
            _saveTimer = 0f;
        }
    }

    private void HandleSelfRotation()
    {
        if (isInspecting || targetRenderer == null) return;

        // 这里的 totalInputEnergy 现在只由 physicsKeyboardWeight 驱动
        float energyDelta = totalInputEnergy - _lastEnergy;
        if (energyDelta > 0)
        {
            _rotationVelocity += energyDelta * 1000f; 
        }
        _lastEnergy = totalInputEnergy;

        if (_rotationVelocity > 0.1f)
        {
            // 驱动 Prefab 的根节点进行旋转
            targetRenderer.transform.Rotate(Vector3.up, _rotationVelocity * Time.deltaTime, Space.World);
            _rotationVelocity *= Friction;
        }
    }

    /// <summary>
    /// 【核心升级】双轨增加进度与物理动力 + 盘玩除尘 + 溢出玉化
    /// </summary>
    public void AddProgress(float evoAmount, float physAmount)
    {
        // 1. 处理物理动力 (驱动旋转)
        float physMultiplier = (currentConfig != null) ? currentConfig.physicsSpeedMultiplier : 1.0f;
        totalInputEnergy += (physAmount * physMultiplier);

        // 2. 处理包浆与玉化进度
        float evoMultiplier = (currentConfig != null) ? currentConfig.evolutionSpeedMultiplier : 1.0f;
        float addedEvo = evoAmount * evoMultiplier;

        if (currentProgress < 1f)
        {
            float spaceLeft = 1f - currentProgress;
            if (addedEvo <= spaceLeft)
            {
                currentProgress += addedEvo;
            }
            else
            {
                currentProgress = 1f;
                // 溢出的部分转化为玉化
                currentJadeProgress = Mathf.Clamp01(currentJadeProgress + (addedEvo - spaceLeft));
            }
        }
        else
        {
            // 包浆满了，纯加玉化
            currentJadeProgress = Mathf.Clamp01(currentJadeProgress + addedEvo);
        }
        
        // 3. 处理除尘 (盘玩时减少灰尘)
        if (currentConfig != null && currentDust > 0f)
        {
            currentDust = Mathf.Clamp01(currentDust - currentConfig.dustCleanPerInput);
        }
    }

    public void RefreshSpeciesLook()
    {
        if (currentConfig == null || currentConfig.singlePrefab == null) return;

        LoadSpecificProgress();
        string targetID = currentConfig.speciesID;

        // 1. 隐藏旧的模型实例，替代原有的 Destroy
        if (_currentInstance != null)
        {
            _currentInstance.SetActive(false);
            _currentInstance = null;
        }

        // 2. 尝试从缓存池读取
        if (_instancePool.TryGetValue(targetID, out GameObject pooledObj) && pooledObj != null)
        {
            _currentInstance = pooledObj;
            _currentInstance.SetActive(true);

            // 更新 LRU 队列，将其移到末尾表示“刚刚使用过”
            _lruQueue.Remove(targetID);
            _lruQueue.Add(targetID);
        }
        else
        {
            // 3. 缓存池没有，动态实例化新的 Prefab
            _currentInstance = Instantiate(currentConfig.singlePrefab, transform);
            _currentInstance.transform.localPosition = Vector3.zero;
            _currentInstance.transform.localRotation = Quaternion.identity;

            // 放入缓存池
            _instancePool[targetID] = _currentInstance;
            _lruQueue.Add(targetID);

            // 4. 超出最大容量，淘汰最久未使用的模型，释放内存
            if (_lruQueue.Count > maxPoolSize)
            {
                string oldestID = _lruQueue[0];
                GameObject oldestObj = _instancePool[oldestID];
                if (oldestObj != null)
                {
                    Destroy(oldestObj);
                }
                _instancePool.Remove(oldestID);
                _lruQueue.RemoveAt(0);
            }
        }

        // 5. 自动抓取组件：在生成的/取出的预制体中寻找 MeshRenderer
        targetRenderer = _currentInstance.GetComponentInChildren<MeshRenderer>();
        
        if (targetRenderer != null)
        {
            // 记录美术在 Prefab 里设置的初始缩放比例
            _initialScale = targetRenderer.transform.localScale;

            // 赋予 Config 中的材质球
            if (currentConfig.speciesMaterial != null)
            {
                targetRenderer.sharedMaterial = currentConfig.speciesMaterial;
            }

            // 【物理系统自动装配】
            // 移除 mc.convex = true 兜底代码，绝对信任预制体已经设置好 Convex
            MeshCollider mc = targetRenderer.gameObject.GetComponent<MeshCollider>();
            if (mc == null) mc = targetRenderer.gameObject.AddComponent<MeshCollider>();

            // 强制接管刚体：设置为 isKinematic 阻止物理引擎下落冲突
            Rigidbody rb = targetRenderer.gameObject.GetComponent<Rigidbody>();
            if (rb == null) rb = targetRenderer.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            // 清理底层属性块数据，完美防止上一个文玩的贴图残留
            _propBlock.Clear();
            targetRenderer.SetPropertyBlock(_propBlock);
        }
    }

    private void UpdateMaterialVisuals()
    {
        if (currentConfig == null || targetRenderer == null) return;

        // 提取曲线处理后的进度值
        float evaluatedProgress = currentConfig.colorCurve.Evaluate(currentProgress);

        targetRenderer.GetPropertyBlock(_propBlock);
        
        // 1. 传包浆进度
        _propBlock.SetFloat(ProgressID, evaluatedProgress);
        
        // 2. 传玉化进度
        _propBlock.SetFloat(JadeProgressID, currentJadeProgress);

        // 3. 传灰尘数据
        _propBlock.SetFloat(DustAmountID, currentDust);
        _propBlock.SetColor(DustColorID, currentConfig.dustColor);
        if (currentConfig.dustMask != null)
        {
            _propBlock.SetTexture(DustMapID, currentConfig.dustMask);
        }

        targetRenderer.SetPropertyBlock(_propBlock);
    }

    // --- 核心存档逻辑 ---

    private void SaveSpecificProgress() 
    {
        if (currentConfig == null) return;
        if (string.IsNullOrEmpty(currentConfig.speciesID)) return;

        string keyPrefix = "Progress_" + currentConfig.speciesID;
        
        // 保存包浆进度
        PlayerPrefs.SetFloat(keyPrefix, currentProgress);

        // 保存玉化进度
        PlayerPrefs.SetFloat(keyPrefix + "_Jade", currentJadeProgress);
        
        // 保存灰尘值
        PlayerPrefs.SetFloat(keyPrefix + "_Dust", currentDust);
        
        // 保存当前时间 (用于下次计算离线积灰)
        PlayerPrefs.SetString(keyPrefix + "_LastTime", System.DateTime.Now.ToBinary().ToString());
    }

    private void LoadSpecificProgress() 
    {
        if (currentConfig != null && !string.IsNullOrEmpty(currentConfig.speciesID))
        {
            string keyPrefix = "Progress_" + currentConfig.speciesID;
            currentProgress = PlayerPrefs.GetFloat(keyPrefix, 0f);
            
            // 读取玉化
            currentJadeProgress = PlayerPrefs.GetFloat(keyPrefix + "_Jade", 0f);

            // 读取灰尘
            currentDust = PlayerPrefs.GetFloat(keyPrefix + "_Dust", 0f);
            
            totalInputEnergy = 0f;
        }
    }

    private void CalculateDustAccumulation()
    {
        if (currentConfig == null) return;

        string keyPrefix = "Progress_" + currentConfig.speciesID;
        string lastTimeStr = PlayerPrefs.GetString(keyPrefix + "_LastTime", "");

        if (!string.IsNullOrEmpty(lastTimeStr))
        {
            long temp = Convert.ToInt64(lastTimeStr);
            DateTime lastTime = DateTime.FromBinary(temp);
            TimeSpan diff = DateTime.Now - lastTime;
            float hoursPassed = (float)diff.TotalHours;

            if (hoursPassed > 0)
            {
                float dustIncrease = hoursPassed / Mathf.Max(currentConfig.hoursToMaxDust, 0.1f);
                currentDust = Mathf.Clamp01(currentDust + dustIncrease);
                Debug.Log($"[DustSystem-Single] 离线 {hoursPassed:F2} 小时, 增加灰尘 {dustIncrease:F2}");
            }
        }
    }

    private void SaveToGlobalManager() 
    {
        if (SaveManager.Instance != null && currentConfig != null)
        {
            SaveManager.Instance.SaveGame(currentProgress, currentConfig.speciesID);
        }
    }

    // --- 生命周期保护 ---

    private void OnDisable() 
    {
        SaveSpecificProgress();
        PlayerPrefs.Save(); 
        SaveToGlobalManager();
    }

    private void OnApplicationQuit()
    {
        SaveSpecificProgress();
        PlayerPrefs.Save(); 
        SaveToGlobalManager();
    }
}