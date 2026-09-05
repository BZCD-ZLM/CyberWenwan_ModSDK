using UnityEngine;
using Steamworks;

/// <summary>
/// 简单的 Steam 初始化检查器
/// 确认插件是否正常运行并能读取到玩家信息
/// </summary>
public class SteamInitChecker : MonoBehaviour
{
    void Start()
    {
        // 1. 检查 SteamManager 是否已经初始化
        if (!SteamManager.Initialized)
        {
            Debug.LogError("<color=red>Steam 未能初始化！</color> 请检查 Steam 客户端是否正在运行，以及 steam_appid.txt 是否配置正确。");
            return;
        }

        // 2. 获取并显示当前玩家的 Steam 昵称
        string name = SteamFriends.GetPersonaName();
        Debug.Log($"<color=#00FF00>Steam 连接成功！</color> 你好，[ {name} ]。CyberWenwan 已准备好接入创意工坊。");
    }
}