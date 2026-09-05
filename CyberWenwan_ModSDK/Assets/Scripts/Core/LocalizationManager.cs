using System;
using System.Collections.Generic;
using UnityEngine;

public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }

    // 定义语言枚举
    public enum Language
    {
        Chinese,
        English
    }

    public Language CurrentLanguage { get; private set; }

    // 语言切换事件广播
    public static event Action OnLanguageChanged;

    // 文本字典
    private Dictionary<string, string> zhDictionary;
    private Dictionary<string, string> enDictionary;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        InitDictionaries();
        LoadLanguage();
    }

    private void InitDictionaries()
    {
        // 初始化中文文本
        zhDictionary = new Dictionary<string, string>()
        {
            { "Tab_Single", "单枚" },
            { "Tab_Dual", "对玩" },
            { "Tab_String", "手串" },
            { "Tab_Favorite", "收藏" },
            { "Tab_All", "全部" },
            { "Btn_Unlock", "解锁" },
            { "Btn_Appraise", "鉴定" },
            { "Menu_ShowHide", "显示/隐藏 仓库" },
            { "Menu_Language", "语言 (Language)" },
            { "Menu_Lang_ZH", "中文" },
            { "Menu_Lang_EN", "English" },
            { "Menu_Exit", "退出" },
            
            // --- 官方文玩名称 (中文原名映射) ---
            { "苹果园", "苹果园" },
            { "金刚菩提串", "金刚菩提串" }
            // 你可以在这里继续添加其他官方文玩的中文名
        };

        // 初始化英文文本
        enDictionary = new Dictionary<string, string>()
        {
            { "Tab_Single", "Single" },
            { "Tab_Dual", "Dual" },
            { "Tab_String", "String" },
            { "Tab_Favorite", "Favorite" },
            { "Tab_All", "All" },
            { "Btn_Unlock", "Unlock" },
            { "Btn_Appraise", "Appraise" },
            { "Menu_ShowHide", "Show/Hide Inventory" },
            { "Menu_Language", "Language" },
            { "Menu_Lang_ZH", "中文" },
            { "Menu_Lang_EN", "English" },
            { "Menu_Exit", "Exit" },

            // --- 官方文玩名称 (英文翻译) ---
            { "苹果园", "Apple Round Walnut" }, 
            { "金刚菩提串", "Rudraksha String" }
            // 你可以在这里继续添加其他官方文玩的英文翻译
        };
    }

    private void LoadLanguage()
    {
        // 默认读取系统语言，如果没存过，默认中文
        int langInt = PlayerPrefs.GetInt("CyberWenwan_Language", (int)Language.Chinese);
        CurrentLanguage = (Language)langInt;
    }

    public void SetLanguage(Language newLang)
    {
        if (CurrentLanguage == newLang) return;

        CurrentLanguage = newLang;
        PlayerPrefs.SetInt("CyberWenwan_Language", (int)CurrentLanguage);
        PlayerPrefs.Save();

        // 触发语言改变的事件
        OnLanguageChanged?.Invoke();
    }

    // 获取对应键值的文本
    public string GetText(string key)
    {
        Dictionary<string, string> targetDict = CurrentLanguage == Language.Chinese ? zhDictionary : enDictionary;
        
        if (targetDict.TryGetValue(key, out string value))
        {
            return value;
        }
        
        // 【核心修改】注释掉这行警告，支持创意工坊的 MOD 文玩静默回退原名
        // Debug.LogWarning($"Localization key not found: {key}");
        
        return key; // 如果找不到翻译（比如玩家自己做的 MOD），直接原样返回它的名字
    }
}