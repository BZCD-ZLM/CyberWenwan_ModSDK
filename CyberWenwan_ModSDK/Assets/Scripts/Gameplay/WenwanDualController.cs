using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

/// <summary>
/// 专用于“成对模式 (Mode_Dual)”的控制器
/// 优化版：LRU 淘汰缓存池极速切换 + 移除运行时物理烘焙，完全保留原有所有逻辑
/// </summary>
public class WenwanDualController : MonoBehaviour
{
    public static WenwanDualController Instance { get; private set; }

    [Header("状态同步")]
    public bool isInspecting = false; 

    [Header("配置引用")]
    public WenwanSpeciesConfig currentConfig;

    [Header("当前运行实例 (动态生成)")]
    public GameObject currentDualInstance;
    public MeshRenderer[] targetRenderers;

    [Header("性能优化 (Performance)")]
    [Tooltip("内存中最多同时保留几套双核？(建议2-3，防止撑爆内存)")]
    public int maxPoolSize = 2; 

    // LRU 淘汰缓存池 (安全保留原有功能，不影响其他数据逻辑)
    private Dictionary<string, GameObject> _instancePool = new Dictionary<string, GameObject>();
    private List<string> _lruQueue = new List<string>();
    
    // 【新增】美术排版初始坐标缓存，解决复用时的位移累加穿模Bug
    private Dictionary<string, Vector3[]> _lruInitialPositions = new Dictionary<string, Vector3[]>();
    private Dictionary<string, Quaternion[]> _lruInitialRotations = new Dictionary<string, Quaternion[]>();

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
    private DualWalnutMotion _motionScript;
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
        _motionScript = GetComponent<DualWalnutMotion>();

        if (currentConfig != null)
        {
            LoadSpecificProgress();
            CalculateDustAccumulation();
        }

        RefreshSpeciesLook();
        StartCoroutine(ShowAfterTransparent());
    }

    private IEnumerator ShowAfterTransparent()
    {
        if (currentDualInstance != null)
        {
            currentDualInstance.transform.localScale = Vector3.zero;
        }
        
        yield return new WaitForSeconds(0.4f);
        
        if (currentDualInstance != null)
        {
            currentDualInstance.transform.localScale = Vector3.one;
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

    public void UpdateSpeciesVisuals(WenwanSpeciesConfig newConfig)
    {
        if (newConfig == null) return;
        
        if (currentConfig != null) SaveSpecificProgress();

        currentConfig = newConfig;
        
        LoadSpecificProgress();
        CalculateDustAccumulation();
        SaveToGlobalManager();

        StartCoroutine(SafeSwitchRoutine());
    }

    private IEnumerator SafeSwitchRoutine()
    {
        if (_motionScript != null) _motionScript.LockAndReset();
        
        RefreshSpeciesLook();
        UpdateMaterialVisuals();
        
        yield return new WaitForFixedUpdate();
        
        if (_motionScript != null) _motionScript.Unlock();
    }

    private void RefreshSpeciesLook()
    {
        if (currentConfig == null || _propBlock == null) return;

        string targetID = currentConfig.speciesID;

        // 1. 隐藏旧实例，替代原有的 Destroy
        if (currentDualInstance != null)
        {
            currentDualInstance.SetActive(false);
            currentDualInstance = null;
        }

        // 2. 尝试从缓存池读取
        if (_instancePool.TryGetValue(targetID, out GameObject pooledObj) && pooledObj != null)
        {
            currentDualInstance = pooledObj;
            currentDualInstance.SetActive(true);

            // 更新 LRU 队列，将其移到末尾表示“刚刚使用过”
            _lruQueue.Remove(targetID);
            _lruQueue.Add(targetID);
        }
        else
        {
            // 3. 检查配置是否合法并在缓存池没有时实例化新 Prefab
            if (currentConfig.dualPrefab == null)
            {
                Debug.LogError($"[WenwanDualController] Config {targetID} 缺少 Dual Prefab 配置！");
                return;
            }

            currentDualInstance = Instantiate(currentConfig.dualPrefab, transform);
            currentDualInstance.transform.localPosition = Vector3.zero;
            currentDualInstance.transform.localRotation = Quaternion.identity;

            // 放入缓存池
            _instancePool[targetID] = currentDualInstance;
            _lruQueue.Add(targetID);

            // 4. 超出最大容量，淘汰最久未使用的双核，释放内存及字典
            if (_lruQueue.Count > maxPoolSize)
            {
                string oldestID = _lruQueue[0];
                GameObject oldestObj = _instancePool[oldestID];
                if (oldestObj != null)
                {
                    Destroy(oldestObj);
                }
                _instancePool.Remove(oldestID);
                _lruInitialPositions.Remove(oldestID);
                _lruInitialRotations.Remove(oldestID);
                _lruQueue.RemoveAt(0);
            }
        }

        // 5. 获取当前活跃子物体的所有渲染器并覆盖材质
        targetRenderers = currentDualInstance.GetComponentsInChildren<MeshRenderer>();
        
        // 【核心修复】：绝对信任美术排版，恢复初始坐标
        if (!_lruInitialPositions.ContainsKey(targetID))
        {
            // 初次实例化，记录美术原生的排版坐标
            Vector3[] initPos = new Vector3[targetRenderers.Length];
            Quaternion[] initRot = new Quaternion[targetRenderers.Length];
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                initPos[i] = targetRenderers[i].transform.localPosition;
                initRot[i] = targetRenderers[i].transform.localRotation;
            }
            _lruInitialPositions[targetID] = initPos;
            _lruInitialRotations[targetID] = initRot;
        }
        else
        {
            // 从 LRU 复用，强行重置回初始美术坐标，彻底解决坐标累加穿模 BUG
            Vector3[] initPos = _lruInitialPositions[targetID];
            Quaternion[] initRot = _lruInitialRotations[targetID];
            for (int i = 0; i < targetRenderers.Length && i < initPos.Length; i++)
            {
                targetRenderers[i].transform.localPosition = initPos[i];
                targetRenderers[i].transform.localRotation = initRot[i];
            }
        }
        
        foreach (MeshRenderer r in targetRenderers)
        {
            if (r == null) continue;

            if (currentConfig.speciesMaterial != null)
            {
                r.sharedMaterial = currentConfig.speciesMaterial;
            }

            // 防漏处理：清空属性块
            _propBlock.Clear();
            r.SetPropertyBlock(_propBlock);
        }
    }

    private void UpdateMaterialVisuals()
    {
        if (currentConfig == null || targetRenderers == null || targetRenderers.Length == 0 || _propBlock == null) return;

        float evaluatedProgress = currentConfig.colorCurve.Evaluate(currentProgress);

        foreach (MeshRenderer r in targetRenderers)
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
        if (currentConfig == null || string.IsNullOrEmpty(currentConfig.speciesID)) return;
        
        string keyPrefix = "Progress_" + currentConfig.speciesID;
        currentProgress = PlayerPrefs.GetFloat(keyPrefix, 0f);
        currentJadeProgress = PlayerPrefs.GetFloat(keyPrefix + "_Jade", 0f);
        currentDust = PlayerPrefs.GetFloat(keyPrefix + "_Dust", 0f);
        totalInputEnergy = 0f;
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
    
    private void OnApplicationQuit()
    {
        SaveSpecificProgress();
        PlayerPrefs.Save();
    }
}