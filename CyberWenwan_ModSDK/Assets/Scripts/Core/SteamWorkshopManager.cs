using UnityEngine;
using Steamworks;
using System.IO;
using System.Collections;

public class SteamWorkshopManager : MonoBehaviour
{
    public static SteamWorkshopManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private IEnumerator Start()
    {
        // 等待 Steamworks 完全初始化
        yield return new WaitUntil(() => SteamManager.Initialized);
        
        Debug.Log("[WorkshopManager] Steam 已初始化，开始寻找玩家订阅的文玩...");
        LoadSubscribedItems();
    }

    public void LoadSubscribedItems()
    {
        if (!SteamManager.Initialized) return;

        // 1. 获取玩家订阅的总数量
        uint count = SteamUGC.GetNumSubscribedItems();
        if (count == 0)
        {
            Debug.Log("[WorkshopManager] 玩家当前没有订阅任何创意工坊文玩。");
            return;
        }

        // 2. 获取所有订阅物品的 ID 列表
        PublishedFileId_t[] subscribedIds = new PublishedFileId_t[count];
        uint fetchedCount = SteamUGC.GetSubscribedItems(subscribedIds, count);

        SpeciesManager speciesManager = FindObjectOfType<SpeciesManager>();
        if (speciesManager == null)
        {
            Debug.LogError("[WorkshopManager] 找不到 SpeciesManager，无法注入 Mod！");
            return;
        }

        int loadedCount = 0;

        // 3. 遍历每个 ID，寻找它在玩家电脑上的物理路径
        for (int i = 0; i < fetchedCount; i++)
        {
            ulong sizeOnDisk;
            string folderPath;
            uint timestamp;

            bool isInstalled = SteamUGC.GetItemInstallInfo(subscribedIds[i], out sizeOnDisk, out folderPath, 1024, out timestamp);

            if (isInstalled)
            {
                bool success = LoadModFromFolder(folderPath, speciesManager);
                if (success) loadedCount++;
            }
            else
            {
                Debug.LogWarning($"[WorkshopManager] Mod {subscribedIds[i]} 已订阅但尚未下载完成。");
            }
        }

        Debug.Log($"[WorkshopManager] 加载完毕！共成功动态加载了 {loadedCount} 个工坊文玩。");

        // 【新增成就逻辑】如果成功加载了至少一个工坊文玩，解锁“万物皆可盘！”成就
        if (loadedCount > 0 && SteamAchievementManager.Instance != null)
        {
            SteamAchievementManager.Instance.UnlockWorkshopAchievement();
        }

        // 4. 通知商店管理器同步新的工坊物品（价格设为0，免解锁）
        StoreManager storeManager = FindObjectOfType<StoreManager>();
        if (storeManager != null)
        {
            storeManager.SyncFromSpeciesManager(); 
        }
    }

    private bool LoadModFromFolder(string folderPath, SpeciesManager speciesManager)
    {
        // 寻找文件夹下我们上传工具打包的 .bundle 文件
        string[] bundleFiles = Directory.GetFiles(folderPath, "*.bundle", SearchOption.AllDirectories);
        
        if (bundleFiles.Length == 0) return false;

        try
        {
            // 加载 AssetBundle (Unity 资源压缩包)
            AssetBundle bundle = AssetBundle.LoadFromFile(bundleFiles[0]);
            if (bundle == null) return false;

            // 从包体里直接抽出 WenwanSpeciesConfig
            WenwanSpeciesConfig[] configs = bundle.LoadAllAssets<WenwanSpeciesConfig>();
            if (configs != null && configs.Length > 0)
            {
                WenwanSpeciesConfig modConfig = configs[0];
                modConfig.isWorkshopItem = true; // 强制上免死金牌标记

                // 防重复检查，然后强行塞进仓库第一位
                if (!speciesManager.allSpecies.Exists(x => x != null && x.speciesID == modConfig.speciesID))
                {
                    speciesManager.allSpecies.Insert(0, modConfig);
                    Debug.Log($"<color=#00FF00>[WorkshopManager] 成功加载工坊文玩: {modConfig.speciesID}</color>");
                }
                
                // 注意：由于后续还要实例化预制体，这里不能写 bundle.Unload(true)
                return true;
            }
            else
            {
                bundle.Unload(false);
                return false;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[WorkshopManager] 读取 Mod 发生异常: {ex.Message}");
            return false;
        }
    }
}