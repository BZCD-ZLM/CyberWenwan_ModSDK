using UnityEngine;

/// <summary>
/// 独立的金币管理系统
/// </summary>
public class CoinManager : MonoBehaviour
{
    public static CoinManager Instance { get; private set; }

    private const string COIN_SAVE_KEY = "CyberWenwan_Coins";

    [Header("UI 拦截设置")]
    [Tooltip("绝对不要拖入 Canvas！请将仓库内部专属的 UI（例如 TabGroup 或 CloseButton）拖入此处。")]
    public GameObject tabGroupUI;

    [Header("金币获取额度设置")]
    [Tooltip("每次键盘按键获取的金币数")]
    public int coinsPerKey = 1;
    [Tooltip("每次鼠标点击获取的金币数")]
    public int coinsPerClick = 1;
    [Tooltip("每次鼠标滚轮滚动获取的金币数")]
    public int coinsPerScroll = 1;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 防止切场景销毁
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        // 订阅全局输入事件
        GlobalInputHook.OnValidInput += HandleValidInput;
    }

    private void OnDisable()
    {
        // 取消订阅，防止内存泄漏
        GlobalInputHook.OnValidInput -= HandleValidInput;
    }

    // 处理盘玩输入并根据额度计算金币
    private void HandleValidInput(int keys, int clicks, int scrolls)
    {
        // 核心拦截：检测 TabGroup 是否处于激活状态，如果是，说明仓库开着，直接跳过
        if (tabGroupUI != null && tabGroupUI.activeInHierarchy)
        {
            return;
        }

        int earnedCoins = (keys * coinsPerKey) + (clicks * coinsPerClick) + (scrolls * coinsPerScroll);
        
        if (earnedCoins > 0)
        {
            AddCoins(earnedCoins);
        }
    }

    /// <summary>
    /// 获取当前金币数量
    /// </summary>
    public int GetCoins()
    {
        return PlayerPrefs.GetInt(COIN_SAVE_KEY, 10000); // 默认 10000 金币
    }

    /// <summary>
    /// 增加金币
    /// </summary>
    public void AddCoins(int amount)
    {
        int currentCoins = GetCoins();
        PlayerPrefs.SetInt(COIN_SAVE_KEY, currentCoins + amount);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 消耗金币（购买物品时调用）
    /// 返回 true 表示扣除成功，false 表示余额不足
    /// </summary>
    public bool SpendCoins(int amount)
    {
        int currentCoins = GetCoins();
        if (currentCoins >= amount)
        {
            PlayerPrefs.SetInt(COIN_SAVE_KEY, currentCoins - amount);
            PlayerPrefs.Save();
            return true;
        }
        return false;
    }
}