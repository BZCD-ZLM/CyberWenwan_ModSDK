using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class StoreManager : MonoBehaviour
{
    public static StoreManager Instance { get; private set; }

    [System.Serializable]
    public class StoreItemData
    {
        public string speciesID;         // 文玩ID
        public int price = 500;          // 解锁价格（默认500）
        public bool isUnlockedByDefault; // 是否默认解锁（在 Inspector 打勾即可）
    }

    [Header("商品与解锁配置")]
    public List<StoreItemData> storeItems = new List<StoreItemData>();

    private void Awake()
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

    /// <summary>
    /// 检查文玩是否已解锁
    /// </summary>
    public bool IsUnlocked(string speciesID)
    {
        // 1. 动态防错位：跨脚本查询 SpeciesManager，判断是否为工坊物品
        // 这样即使是运行时动态加载的 MOD，也能直接免解锁
        SpeciesManager sm = FindObjectOfType<SpeciesManager>();
        if (sm != null)
        {
            var config = sm.allSpecies.Find(x => x != null && x.speciesID == speciesID);
            if (config != null && config.isWorkshopItem)
            {
                return true; // 创意工坊物品直接免解锁开绿灯
            }
        }

        // 2. 查找本地商店配置
        StoreItemData item = storeItems.Find(x => x.speciesID == speciesID);
        
        // 如果在 Inspector 中勾选了默认解锁，直接放行
        if (item != null && item.isUnlockedByDefault) return true;
        
        // 3. 否则检查存档，1 表示已购买解锁，0 表示未解锁
        return PlayerPrefs.GetInt("Unlocked_" + speciesID, 0) == 1;
    }

    /// <summary>
    /// 获取文玩的价格
    /// </summary>
    public int GetPrice(string speciesID)
    {
        // 工坊物品价格强制为 0，防止 UI 显示错乱
        SpeciesManager sm = FindObjectOfType<SpeciesManager>();
        if (sm != null)
        {
            var config = sm.allSpecies.Find(x => x != null && x.speciesID == speciesID);
            if (config != null && config.isWorkshopItem)
            {
                return 0; 
            }
        }

        StoreItemData item = storeItems.Find(x => x.speciesID == speciesID);
        return item != null ? item.price : 99999; 
    }

    /// <summary>
    /// 尝试购买解锁
    /// </summary>
    public bool TryBuyItem(string speciesID)
    {
        if (IsUnlocked(speciesID)) return true;

        int price = GetPrice(speciesID);
        
        if (CoinManager.Instance != null && CoinManager.Instance.SpendCoins(price))
        {
            // 扣费成功，记录解锁状态
            PlayerPrefs.SetInt("Unlocked_" + speciesID, 1);
            PlayerPrefs.Save();
            Debug.Log($"[StoreManager] 成功解锁文玩: {speciesID}");
            return true;
        }
        
        Debug.LogWarning($"[StoreManager] 金币不足，无法解锁: {speciesID}");
        return false; 
    }

    // ==========================================
    // 编辑器辅助工具：一键抓取 SpeciesManager 数据并严格排序
    // ==========================================
    [ContextMenu("一键同步 SpeciesManager 的文玩")]
    public void SyncFromSpeciesManager()
    {
        SpeciesManager sm = FindObjectOfType<SpeciesManager>();
        if (sm == null)
        {
            Debug.LogWarning("[StoreManager] 场景中找不到 SpeciesManager！");
            return;
        }

        if (sm.allSpecies == null || sm.allSpecies.Count == 0)
        {
            Debug.LogWarning("[StoreManager] SpeciesManager 中的 allSpecies 列表为空！");
            return;
        }

        // 创建一个新列表来保证顺序一致
        List<StoreItemData> newList = new List<StoreItemData>();

        foreach (var config in sm.allSpecies) 
        {
            if (config == null || string.IsNullOrEmpty(config.speciesID)) continue;
            
            // 尝试在旧列表中寻找是否已经有该数据
            StoreItemData existingItem = storeItems.Find(x => x.speciesID == config.speciesID);
            
            if (existingItem != null)
            {
                // 如果有，保留原有的价格和默认解锁状态，直接塞进新列表
                newList.Add(existingItem);
            }
            else
            {
                // 如果没有，创建新的默认数据。如果是工坊物品，不用管它默认解锁不解锁，上面逻辑已经拦截了。
                newList.Add(new StoreItemData 
                { 
                    speciesID = config.speciesID, 
                    price = 500, 
                    isUnlockedByDefault = config.isWorkshopItem // 如果是工坊物品顺手勾上默认解锁
                });
            }
        }
        
        // 覆盖为严格对齐排序的新列表
        storeItems = newList;
        Debug.Log("[StoreManager] 同步与排序完成！当前列表顺序已与 SpeciesManager 100% 保持一致。");
    }
}

// ==========================================
// 增加编辑器扩展，在 Inspector 暴露醒目的按钮
// ==========================================
#if UNITY_EDITOR
[CustomEditor(typeof(StoreManager))]
public class StoreManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 绘制默认面板
        DrawDefaultInspector();

        GUILayout.Space(15);
        GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f); // 浅绿色按钮
        
        // 添加一个大按钮
        if (GUILayout.Button("🔄 一键同步并排序 (与SpeciesManager对齐)", GUILayout.Height(40)))
        {
            StoreManager sm = (StoreManager)target;
            sm.SyncFromSpeciesManager();
            
            // 标记改动，确保保存场景时数据不丢失
            EditorUtility.SetDirty(sm); 
        }
        
        GUI.backgroundColor = Color.white;
    }
}
#endif