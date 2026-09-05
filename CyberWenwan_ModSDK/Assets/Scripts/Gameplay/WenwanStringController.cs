using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// 专用于“手串模式 (Mode_String)”的控制器
/// 优化版：LRU 淘汰缓存池极速切换 + 移除运行时物理烘焙
/// </summary>
public class WenwanStringController : MonoBehaviour
{
    public static WenwanStringController Instance { get; private set; }

    [Header("状态同步")]
    public bool isInspecting = false; 

    [Header("配置引用")]
    public WenwanSpeciesConfig currentConfig;

    [Header("当前运行实例 (动态生成)")]
    public GameObject currentStringInstance;

    [Header("性能优化 (Performance)")]
    [Tooltip("内存中最多同时保留几串手串？(建议2-3，防止撑爆内存)")]
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
    public float currentDust = 0.0f; 

    [HideInInspector]
    public float totalInputEnergy = 0f;

    private List<MeshRenderer> _beadRenderers = new List<MeshRenderer>();
    private MaterialPropertyBlock _propBlock;
    private float _saveTimer = 0f;

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
        if (currentConfig != null)
        {
            LoadSpecificProgress();
            CalculateDustAccumulation();
        }
    }

    private void Update()
    {
        UpdateMaterialVisuals();

        _saveTimer += Time.deltaTime;
        if (_saveTimer > 10f)
        {
            SaveSpecificProgress();
            SaveToGlobalManager();
            _saveTimer = 0f;
        }
    }

    public void AddProgress(float evoAmount, float physAmount)
    {
        float physMultiplier = (currentConfig != null) ? currentConfig.physicsSpeedMultiplier : 1.0f;
        totalInputEnergy += (physAmount * physMultiplier);

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
                currentJadeProgress = Mathf.Clamp01(currentJadeProgress + (addedEvo - spaceLeft));
            }
        }
        else
        {
            currentJadeProgress = Mathf.Clamp01(currentJadeProgress + addedEvo);
        }
        
        if (currentConfig != null && currentDust > 0f)
        {
            currentDust = Mathf.Clamp01(currentDust - currentConfig.dustCleanPerInput);
        }
    }

    /// <summary>
    /// 核心逻辑：基于 LRU 缓存池极速切换手串，完全保留旧有的流程
    /// </summary>
    public void RefreshSpeciesLook()
    {
        if (currentConfig == null) return;

        // 1. 隐藏旧实例，而不是销毁
        if (currentStringInstance != null)
        {
            currentStringInstance.SetActive(false);
            currentStringInstance = null;
        }

        string targetID = currentConfig.speciesID;

        // 2. 尝试从缓存池读取
        if (_instancePool.TryGetValue(targetID, out GameObject pooledObj) && pooledObj != null)
        {
            currentStringInstance = pooledObj;
            currentStringInstance.SetActive(true);

            // 更新 LRU 队列，将其移到末尾表示“刚刚使用过”
            _lruQueue.Remove(targetID);
            _lruQueue.Add(targetID);
        }
        else
        {
            // 3. 缓存池没有，执行实例化 (仅首次加载此手串时会运行)
            if (currentConfig.stringPrefab == null)
            {
                Debug.LogError($"[WenwanStringController] Config {targetID} 缺少 String Prefab 配置！");
                return;
            }

            currentStringInstance = Instantiate(currentConfig.stringPrefab, transform);
            currentStringInstance.transform.localPosition = Vector3.zero;
            currentStringInstance.transform.localRotation = Quaternion.identity;

            Rigidbody rb = currentStringInstance.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = currentStringInstance.AddComponent<Rigidbody>();
            }
            rb.useGravity = false;
            rb.isKinematic = true; 
            rb.angularDrag = 0.05f;

            // 放入缓存池
            _instancePool[targetID] = currentStringInstance;
            _lruQueue.Add(targetID);

            // 4. 超出最大容量，淘汰最久未使用的手串，释放内存
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

        // 5. 刷新视觉与数据 (保留原有所有功能逻辑)
        RefreshBeadListAndAlign();
        LoadSpecificProgress();
        CalculateDustAccumulation();
        ApplyMaterials(); 
    }

    /// <summary>
    /// 为每颗珠子分配正确的材质球 (已移除 mc.convex = true)
    /// </summary>
    private void ApplyMaterials()
    {
        if (_beadRenderers.Count == 0 || currentConfig == null) return;

        for (int i = 0; i < _beadRenderers.Count; i++)
        {
            MeshRenderer r = _beadRenderers[i];
            if (r == null) continue;

            Material targetMat = currentConfig.speciesMaterial;

            // 保留特殊珠子材质替换功能
            if (currentConfig.specialBeads != null && currentConfig.specialBeads.Count > 0)
            {
                var special = currentConfig.specialBeads.Find(x => x.beadIndex == i);
                if (special != null && special.customMaterial != null)
                {
                    targetMat = special.customMaterial;
                }
            }

            if (targetMat != null)
            {
                r.sharedMaterial = targetMat;
            }

            _propBlock.Clear(); 
            r.SetPropertyBlock(_propBlock);
            
            // 【核心优化】移除了非常耗时的 mc.convex = true
        }
    }

    public void RefreshBeadListAndAlign()
    {
        _beadRenderers.Clear();
        
        if (currentStringInstance == null) return;

        MeshRenderer[] renderers = currentStringInstance.GetComponentsInChildren<MeshRenderer>();
        
        foreach (var r in renderers)
        {
            if (r != null) _beadRenderers.Add(r);
        }
    }

    private void UpdateMaterialVisuals()
    {
        if (currentConfig == null || _beadRenderers.Count == 0 || _propBlock == null) return;

        float evaluatedProgress = currentConfig.colorCurve.Evaluate(currentProgress);

        foreach (MeshRenderer r in _beadRenderers)
        {
            if (r == null) continue;
            
            r.GetPropertyBlock(_propBlock);
            
            _propBlock.SetFloat(ProgressID, evaluatedProgress);
            _propBlock.SetFloat(JadeProgressID, currentJadeProgress);
            _propBlock.SetFloat(DustAmountID, currentDust);
            _propBlock.SetColor(DustColorID, currentConfig.dustColor);
            
            if (currentConfig.dustMask != null)
            {
                _propBlock.SetTexture(DustMapID, currentConfig.dustMask);
            }
            
            r.SetPropertyBlock(_propBlock);
        }
    }

    private void SaveSpecificProgress()
    {
        if (currentConfig == null || string.IsNullOrEmpty(currentConfig.speciesID)) return;
        
        string keyPrefix = "Progress_" + currentConfig.speciesID;
        PlayerPrefs.SetFloat(keyPrefix, currentProgress);
        PlayerPrefs.SetFloat(keyPrefix + "_Jade", currentJadeProgress);
        PlayerPrefs.SetFloat(keyPrefix + "_Dust", currentDust);
        PlayerPrefs.SetString(keyPrefix + "_LastTime", System.DateTime.Now.ToBinary().ToString());
    }

    private void LoadSpecificProgress()
    {
        if (currentConfig != null && !string.IsNullOrEmpty(currentConfig.speciesID))
        {
            string keyPrefix = "Progress_" + currentConfig.speciesID;
            currentProgress = PlayerPrefs.GetFloat(keyPrefix, 0f);
            currentJadeProgress = PlayerPrefs.GetFloat(keyPrefix + "_Jade", 0f);
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

    private void OnDisable()
    {
        SaveSpecificProgress();
        PlayerPrefs.Save();
        SaveToGlobalManager();
    }
}