using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 文玩品种管理器 (核心大脑)
/// 负责根据存档加载文玩、切换单/双/手串模态物体以及数据分发
/// </summary>
public class SpeciesManager : MonoBehaviour
{
    public static SpeciesManager Instance { get; private set; }
    
    [Header("1. 品种配置列表")]
    public List<WenwanSpeciesConfig> allSpecies = new List<WenwanSpeciesConfig>();
    public int CurrentIndex { get; private set; } = 0;

    [Header("2. 模式物体引用 (层级物体)")]
    [Tooltip("Hierarchy 中 Wenwan_DisplayRoot 下的 Mode_Single")]
    public GameObject modeSingleObject;
    
    [Tooltip("Hierarchy 中 Wenwan_DisplayRoot 下 the Mode_Dual")]
    public GameObject modeDualObject;

    [Tooltip("Hierarchy 中 Wenwan_DisplayRoot 下 the Mode_String")]
    public GameObject modeStringObject;

    void Awake()
    {
        if (Instance == null) 
        { 
            Instance = this; 
            DontDestroyOnLoad(gameObject); 
        }
        else 
        { 
            Destroy(gameObject); 
        }
    }

    void Start()
    {
        string savedID = "";
        if (SaveManager.Instance != null)
        {
            var data = SaveManager.Instance.LoadGame();
            if (data != null)
            {
                savedID = data.currentSpeciesID;
            }
        }

        int targetIndex = 0;
        if (!string.IsNullOrEmpty(savedID))
        {
            targetIndex = allSpecies.FindIndex(s => s != null && s.speciesID == savedID);
            if (targetIndex == -1) targetIndex = 0;
        }

        ChangeSpeciesByIndex(targetIndex);
    }

    /// <summary>
    /// 根据索引切换文玩品种
    /// </summary>
    public void ChangeSpeciesByIndex(int index)
    {
        if (index < 0 || index >= allSpecies.Count) return;
        
        CurrentIndex = index;
        WenwanSpeciesConfig config = allSpecies[index];

        // 1. 物理层级显隐控制
        DisplayMode mode = config.displayMode;
        if (modeDualObject != null) modeDualObject.SetActive(mode == DisplayMode.Dual);
        if (modeSingleObject != null) modeSingleObject.SetActive(mode == DisplayMode.Single);
        if (modeStringObject != null) modeStringObject.SetActive(mode == DisplayMode.String);

        float progressToSave = 0f;

        // 2. 逻辑分发：统一改为 GetComponent 直接抓取，防死锁和时序崩溃
        switch (mode)
        {
            case DisplayMode.Dual:
                if (modeDualObject != null)
                {
                    WenwanDualController dualCtrl = modeDualObject.GetComponent<WenwanDualController>();
                    if (dualCtrl != null)
                    {
                        dualCtrl.UpdateSpeciesVisuals(config);
                        progressToSave = dualCtrl.currentProgress;
                    }
                }
                break;

            case DisplayMode.String:
                if (modeStringObject != null)
                {
                    WenwanStringController stringCtrl = modeStringObject.GetComponent<WenwanStringController>();
                    if (stringCtrl != null)
                    {
                        stringCtrl.currentConfig = config;
                        stringCtrl.RefreshSpeciesLook(); 
                        progressToSave = stringCtrl.currentProgress;
                    }
                }
                break;

            case DisplayMode.Single:
                if (modeSingleObject != null)
                {
                    WenwanSingleController singleCtrl = modeSingleObject.GetComponent<WenwanSingleController>();
                    if (singleCtrl != null)
                    {
                        singleCtrl.currentConfig = config;
                        singleCtrl.RefreshSpeciesLook();
                        progressToSave = singleCtrl.currentProgress;
                    }
                }
                break;
        }

        // 3. 存档同步
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SaveGame(progressToSave, config.speciesID);
        }
    }

    public WenwanSpeciesConfig GetCurrentConfig() 
    {
        if (allSpecies.Count > 0 && CurrentIndex >= 0 && CurrentIndex < allSpecies.Count)
        {
            return allSpecies[CurrentIndex];
        }
        return null;
    }
}