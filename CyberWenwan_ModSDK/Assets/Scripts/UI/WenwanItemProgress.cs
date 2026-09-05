using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

// 这是一个独立组件，负责单个文玩图标的进度条显示与解锁、鉴定状态
public class WenwanItemProgress : MonoBehaviour
{
    [Header("UI 引用")]
    public Image fillImage;
    [Tooltip("新增：用于显示鉴定获得的金币数字的 Text 组件（现作为克隆模板）")]
    public Text appraiseResultText; 

    [Header("解锁系统引用")]
    public GameObject lockOverlay; // 灰色遮罩图层 (Image节点)
    public Button unlockButton;    // 解锁按钮
    public Text priceText;         // 解锁按钮上的价格文字

    [Header("鉴定系统引用")]
    public Button appraiseButton;  // 鉴定按钮

    [Header("鉴定金币公式参数")]
    [Tooltip("公式：((随机数1(Min~Max) * 倍率) + 随机数2(Min~Max)) * 进度比例")]
    public int random1Min = 1;
    public int random1Max = 100;
    public int multiplier = 1000;
    public int random2Min = 1;
    public int random2Max = 999;

    private string _currentSpeciesID;
    private bool _isAppraising = false; // 防连点状态锁

    private void Start()
    {
        // 自动给按钮绑定点击事件
        if (unlockButton != null) unlockButton.onClick.AddListener(OnUnlockButtonClicked);
        if (appraiseButton != null) appraiseButton.onClick.AddListener(OnAppraiseButtonClicked);
    }

    public void UpdateFill(string speciesID)
    {
        _currentSpeciesID = speciesID;

        if (fillImage == null || string.IsNullOrEmpty(speciesID)) return;

        // 1. 原有功能：刷新进度条
        float progress = PlayerPrefs.GetFloat("Progress_" + speciesID, 0f);
        fillImage.fillAmount = progress;
        
        // 2. 刷新解锁与遮罩状态
        if (StoreManager.Instance != null)
        {
            bool isUnlocked = StoreManager.Instance.IsUnlocked(speciesID);
            
            if (isUnlocked)
            {
                if (lockOverlay != null) lockOverlay.SetActive(false);
                
                string lastAppraiseDate = PlayerPrefs.GetString("LastAppraiseDate_" + speciesID, "");
                string todayDate = DateTime.Now.ToString("yyyyMMdd");
                
                bool alreadyAppraisedToday = (lastAppraiseDate == todayDate);

                if (appraiseButton != null)
                {
                    appraiseButton.gameObject.SetActive(!alreadyAppraisedToday);
                    
                    Text appraiseBtnText = appraiseButton.GetComponentInChildren<Text>(true);
                    if (appraiseBtnText != null)
                    {
                        appraiseBtnText.text = LocalizationManager.Instance.GetText("Btn_Appraise");
                    }
                }

                // 确保模板本体始终处于隐藏状态
                if (appraiseResultText != null) 
                {
                    appraiseResultText.gameObject.SetActive(false);
                }
            }
            else
            {
                if (lockOverlay != null) lockOverlay.SetActive(true);
                if (appraiseButton != null) appraiseButton.gameObject.SetActive(false);
                if (appraiseResultText != null) appraiseResultText.gameObject.SetActive(false);
                
                if (priceText != null)
                {
                    int price = StoreManager.Instance.GetPrice(speciesID);
                    string unlockStr = LocalizationManager.Instance.GetText("Btn_Unlock");
                    
                    // 强制文字溢出显示，防止太长被自动截断或换行隐藏
                    priceText.horizontalOverflow = HorizontalWrapMode.Overflow;
                    priceText.verticalOverflow = VerticalWrapMode.Overflow;
                    
                    priceText.text = $"{unlockStr} ({price})";
                }
            }
        }

        // 检查并统计已盘完的文玩数量 (满进度 >= 1.0f)
        if (SteamAchievementManager.Instance != null && SpeciesManager.Instance != null)
        {
            int completedCount = 0;
            foreach (var sp in SpeciesManager.Instance.allSpecies)
            {
                if (sp != null)
                {
                    float spProgress = PlayerPrefs.GetFloat("Progress_" + sp.speciesID, 0f);
                    if (spProgress >= 1f)
                    {
                        completedCount++;
                    }
                }
            }
            SteamAchievementManager.Instance.CheckCompletedItemsCount(completedCount);
        }
    }

    private void OnUnlockButtonClicked()
    {
        if (string.IsNullOrEmpty(_currentSpeciesID)) return;

        if (StoreManager.Instance != null)
        {
            bool success = StoreManager.Instance.TryBuyItem(_currentSpeciesID);
            if (success)
            {
                UpdateFill(_currentSpeciesID); 
            }
            else
            {
                Debug.Log("[商店] 金币不足，继续盘玩积累吧！");
            }
        }
    }

    private void OnAppraiseButtonClicked()
    {
        // 核心拦截：防止一瞬间的多次物理连点
        if (_isAppraising || string.IsNullOrEmpty(_currentSpeciesID)) return;

        string lastAppraiseDate = PlayerPrefs.GetString("LastAppraiseDate_" + _currentSpeciesID, "");
        string todayDate = DateTime.Now.ToString("yyyyMMdd");
        if (lastAppraiseDate == todayDate) return;

        float progress = PlayerPrefs.GetFloat("Progress_" + _currentSpeciesID, 0f);
        
        if (progress <= 0f)
        {
            Debug.Log("[鉴定] 当前盘玩进度为 0，无法鉴定！");
            return;
        }

        _isAppraising = true; // 上锁

        int part1 = UnityEngine.Random.Range(random1Min, random1Max + 1);
        int part2 = UnityEngine.Random.Range(random2Min, random2Max + 1);
        int maxPossibleCoins = (part1 * multiplier) + part2;
        
        int earnedCoins = Mathf.CeilToInt(maxPossibleCoins * progress);

        if (earnedCoins > 0)
        {
            CoinManager.Instance.AddCoins(earnedCoins);

            if (SteamAchievementManager.Instance != null)
            {
                SteamAchievementManager.Instance.AddAppraiseCoins(earnedCoins);
            }
            
            PlayerPrefs.SetString("LastAppraiseDate_" + _currentSpeciesID, todayDate);
            PlayerPrefs.Save();

            if (appraiseButton != null) 
            {
                appraiseButton.gameObject.SetActive(false);
            }

            if (appraiseResultText != null)
            {
                // 核心改动：不再由 UI 跑协程，而是丢给文字自己跑
                GameObject floatObj = Instantiate(appraiseResultText.gameObject);
                floatObj.SetActive(true);
                
                FloatingTextAnimator anim = floatObj.AddComponent<FloatingTextAnimator>();
                Canvas rootCanvas = GetComponentInParent<Canvas>();
                if (rootCanvas != null && !rootCanvas.isRootCanvas)
                {
                    rootCanvas = rootCanvas.rootCanvas; // 确保拿到最外层 Canvas
                }
                
                anim.PlayAnimation(earnedCoins, rootCanvas);
            }
            else
            {
                Debug.LogWarning("[鉴定] 未绑定 Appraise Result Text 组件！");
            }
        }
        
        _isAppraising = false; // 无论成功与否，立刻解开连点锁
    }
}

/// <summary>
/// 新增：独立的文字自毁动画组件。
/// 挂载在克隆出来的文字上，彻底脱离仓库 UI 的生命周期。
/// </summary>
public class FloatingTextAnimator : MonoBehaviour
{
    public void PlayAnimation(int coins, Canvas rootCanvas)
    {
        Text textComponent = GetComponent<Text>();
        if (textComponent != null)
        {
            textComponent.text = $"+{coins}";
            Color c = textComponent.color;
            c.a = 1f;
            textComponent.color = c;
        }

        RectTransform rt = GetComponent<RectTransform>();
        if (rootCanvas != null)
        {
            rt.SetParent(rootCanvas.transform, true);
            rt.SetAsLastSibling(); 
        }

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one; 

        StartCoroutine(AnimateAndDestroy(rt, textComponent));
    }

    private IEnumerator AnimateAndDestroy(RectTransform rt, Text textComponent)
    {
        float duration = 1.5f;       
        float floatSpeed = 150f;     
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            
            if (rt != null)
            {
                rt.anchoredPosition += Vector2.up * floatSpeed * Time.deltaTime;
            }

            if (textComponent != null)
            {
                Color color = textComponent.color;
                color.a = Mathf.Lerp(1f, 0f, timer / duration);
                textComponent.color = color;
            }

            yield return null;
        }

        // 动画结束，自毁
        Destroy(gameObject);
    }
}