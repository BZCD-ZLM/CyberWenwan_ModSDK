using UnityEngine;
using Steamworks;
using System;

public class SteamAchievementManager : MonoBehaviour
{
    public static SteamAchievementManager Instance;

    // --- 本地持久化存储 Keys ---
    private const string PREF_TOTAL_PLAY_COUNT = "CyberWenwan_TotalPlayCount";
    private const string PREF_TOTAL_PLAY_TIME = "CyberWenwan_TotalPlayTime";
    private const string PREF_NIGHT_PLAY_TIME = "CyberWenwan_NightPlayTime";
    private const string PREF_APPRAISE_COINS = "CyberWenwan_AppraiseCoins";

    // --- 内存统计数据 ---
    private int totalPlayCount = 0;
    private float totalPlayTimeSeconds = 0f;
    private float nightPlayTimeSeconds = 0f;
    private int totalAppraiseCoins = 0;
    
    // 定时保存计时器，防止每帧写入 PlayerPrefs 造成卡顿
    private float saveTimer = 0f;

    // --- 成就 ID 定义 ---
    // 1. 盘玩次数 (稚手 -> 入神)
    private readonly int[] playCountThresholds = { 10, 100, 1000, 10000, 100000, 200000, 300000, 400000, 500000, 1000000 };
    private readonly string[] playCountAchIds = { "ACH_PLAY_1", "ACH_PLAY_2", "ACH_PLAY_3", "ACH_PLAY_4", "ACH_PLAY_5", "ACH_PLAY_6", "ACH_PLAY_7", "ACH_PLAY_8", "ACH_PLAY_9", "ACH_PLAY_10" };

    // 2. 盘完文玩数量 (初触菩提 -> 传世神品)
    private readonly int[] itemCountThresholds = { 1, 3, 5, 7, 9, 11, 13, 15, 17, 19 };
    private readonly string[] itemCountAchIds = { "ACH_ITEM_1", "ACH_ITEM_2", "ACH_ITEM_3", "ACH_ITEM_4", "ACH_ITEM_5", "ACH_ITEM_6", "ACH_ITEM_7", "ACH_ITEM_8", "ACH_ITEM_9", "ACH_ITEM_10" };

    // 3 & 4. 特殊行为
    private const string ACH_WORKSHOP = "ACH_EVENT_MOD";     // 万物皆可盘！
    private const string ACH_INSPECT = "ACH_EVENT_INSPECT";  // 真润！

    // 5. 新增：时长与经济成就
    private const string ACH_TIME_24H = "ACH_TIME_24H";             // 盘玩上瘾 (24小时)
    private const string ACH_TIME_1000H = "ACH_TIME_1000H";         // 十年一剑 (1000小时)
    private const string ACH_EARN_1M = "ACH_COIN_1M";               // 以玩养玩 (鉴定赚100万)
    private const string ACH_NIGHT_OWL = "ACH_TIME_NIGHT";          // 盘串上头 (凌晨2-6点累计2小时)
    private const string ACH_ITEM_500H = "ACH_TIME_SINGLE_500H";    // 传世之作 (单件500小时)
    private const string ACH_MASTER_80 = "ACH_EVENT_MASTER";        // 文玩老炮 (解锁80%成就)

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

    private void Start()
    {
        // 加载所有本地统计数据
        totalPlayCount = PlayerPrefs.GetInt(PREF_TOTAL_PLAY_COUNT, 0);
        totalPlayTimeSeconds = PlayerPrefs.GetFloat(PREF_TOTAL_PLAY_TIME, 0f);
        nightPlayTimeSeconds = PlayerPrefs.GetFloat(PREF_NIGHT_PLAY_TIME, 0f);
        totalAppraiseCoins = PlayerPrefs.GetInt(PREF_APPRAISE_COINS, 0);
    }

    /// <summary>
    /// 增加盘玩次数
    /// </summary>
    public void AddPlayCount(int count = 1)
    {
        totalPlayCount += count;
        PlayerPrefs.SetInt(PREF_TOTAL_PLAY_COUNT, totalPlayCount);

        if (!SteamManager.Initialized) return;

        for (int i = 0; i < playCountThresholds.Length; i++)
        {
            if (totalPlayCount >= playCountThresholds[i])
            {
                UnlockAchievement(playCountAchIds[i]);
            }
        }
    }

    /// <summary>
    /// 增加盘玩时长 (需要在具体盘玩逻辑的 Update 中持续调用)
    /// </summary>
    public void AddPlayTime(float deltaTime)
    {
        totalPlayTimeSeconds += deltaTime;

        // 检查凌晨 2:00 - 6:00
        int currentHour = DateTime.Now.Hour;
        if (currentHour >= 2 && currentHour < 6)
        {
            nightPlayTimeSeconds += deltaTime;
        }

        // 每 5 秒保存一次数据并检查成就，避免性能消耗
        saveTimer += deltaTime;
        if (saveTimer >= 5f)
        {
            saveTimer = 0f;
            PlayerPrefs.SetFloat(PREF_TOTAL_PLAY_TIME, totalPlayTimeSeconds);
            PlayerPrefs.SetFloat(PREF_NIGHT_PLAY_TIME, nightPlayTimeSeconds);

            CheckTimeAchievements();
        }
    }

    private void CheckTimeAchievements()
    {
        if (!SteamManager.Initialized) return;

        float totalHours = totalPlayTimeSeconds / 3600f;
        float nightHours = nightPlayTimeSeconds / 3600f;

        if (totalHours >= 24f) UnlockAchievement(ACH_TIME_24H);
        if (totalHours >= 1000f) UnlockAchievement(ACH_TIME_1000H);
        if (nightHours >= 2f) UnlockAchievement(ACH_NIGHT_OWL);
    }

    /// <summary>
    /// 增加鉴定获得的总金币
    /// </summary>
    public void AddAppraiseCoins(int coins)
    {
        totalAppraiseCoins += coins;
        PlayerPrefs.SetInt(PREF_APPRAISE_COINS, totalAppraiseCoins);

        if (totalAppraiseCoins >= 1000000)
        {
            UnlockAchievement(ACH_EARN_1M);
        }
    }

    /// <summary>
    /// 检查是否有单件文玩盘玩时间超过 500 小时
    /// </summary>
    public void CheckSingleItemPlayTime(float itemTotalHours)
    {
        if (itemTotalHours >= 500f)
        {
            UnlockAchievement(ACH_ITEM_500H);
        }
    }

    /// <summary>
    /// 检查已盘完（满进度）的文玩数量
    /// </summary>
    public void CheckCompletedItemsCount(int completedCount)
    {
        if (!SteamManager.Initialized) return;

        for (int i = 0; i < itemCountThresholds.Length; i++)
        {
            if (completedCount >= itemCountThresholds[i])
            {
                UnlockAchievement(itemCountAchIds[i]);
            }
        }
    }

    public void UnlockWorkshopAchievement() => UnlockAchievement(ACH_WORKSHOP);
    public void UnlockInspectAchievement() => UnlockAchievement(ACH_INSPECT);

    /// <summary>
    /// 核心解锁逻辑
    /// </summary>
    private void UnlockAchievement(string achievementId)
    {
        if (!SteamManager.Initialized) return;

        bool isUnlocked;
        SteamUserStats.GetAchievement(achievementId, out isUnlocked);
        
        if (!isUnlocked)
        {
            SteamUserStats.SetAchievement(achievementId);
            SteamUserStats.StoreStats();
            Debug.Log($"[SteamAchievement] Achievement {achievementId} unlocked!");
            
            // 每次解锁新成就后，检查是否达到了 80% 的“文玩老炮”条件
            CheckMasterAchievement();
        }
    }

    /// <summary>
    /// 检查是否解锁了 80% 以上的成就
    /// </summary>
    private void CheckMasterAchievement()
    {
        if (!SteamManager.Initialized) return;

        uint totalAchievements = SteamUserStats.GetNumAchievements();
        if (totalAchievements == 0) return;

        uint unlockedCount = 0;
        for (uint i = 0; i < totalAchievements; i++)
        {
            string achName = SteamUserStats.GetAchievementName(i);
            bool isUnlocked;
            SteamUserStats.GetAchievement(achName, out isUnlocked);
            if (isUnlocked)
            {
                unlockedCount++;
            }
        }

        // 判断是否大于等于 80%
        if ((float)unlockedCount / totalAchievements >= 0.8f)
        {
            // 避免无限递归，直接调用底层 API 解锁
            bool masterUnlocked;
            SteamUserStats.GetAchievement(ACH_MASTER_80, out masterUnlocked);
            if (!masterUnlocked)
            {
                SteamUserStats.SetAchievement(ACH_MASTER_80);
                SteamUserStats.StoreStats();
                Debug.Log("[SteamAchievement] Achievement ACH_MASTER_80 unlocked!");
            }
        }
    }
}