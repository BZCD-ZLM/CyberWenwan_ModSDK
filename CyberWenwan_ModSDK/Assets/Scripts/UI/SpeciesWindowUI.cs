using UnityEngine;
using UnityEngine.UI; 
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System;

public enum UITabType
{
    Single = 0,
    Dual = 1,
    String = 2,
    Favorite = 3,
    All = 4 
}

public class SpeciesWindowUI : MonoBehaviour
{
    public static SpeciesWindowUI Instance { get; private set; }

    [Header("UI 组件绑定")]
    public GameObject warehousePanel;   
    public Transform itemsContainer;    
    public GameObject itemTemplate;     

    [Header("分类标签 UI (独立组件引流)")]
    [Tooltip("按 0-单枚, 1-对玩, 2-手串, 3-收藏, 4-全部 的严格顺序拖入挂载了 UITabButton 的物体")]
    public UITabButton[] tabButtons;

    [Header("窗口设置")]
    public Vector2Int widgetSize = new Vector2Int(400, 400); 
    public Vector2Int expandedSize = new Vector2Int(1024, 768); 

    private bool _isWindowOpen = false;
    private Vector2Int _lastWidgetPos = new Vector2Int(100, 100); 
    private UITabType _currentTab = UITabType.All; 

    // --- Windows API ---
    [DllImport("user32.dll", SetLastError = true)] 
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
    
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("Dwmapi.dll")] private static extern uint DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    
    private struct MARGINS { public int cxLeftWidth; public int cxRightWidth; public int cyTopHeight; public int cyBottomHeight; }
    
    private const int GWL_STYLE = -16;
    private const uint WS_CAPTION = 0x00C00000;     
    private const uint WS_SYSMENU = 0x00080000;     
    private const uint WS_THICKFRAME = 0x00040000;  
    private const uint WS_POPUP = 0x80000000;       
    private const uint WS_VISIBLE = 0x10000000;

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;

    void Awake()
    {
        if (Instance == null) Instance = this;
        if (warehousePanel != null) warehousePanel.SetActive(false);
        if (itemTemplate != null) itemTemplate.SetActive(false);
    }

    void Start()
    {
        UpdateTabVisuals((int)_currentTab);

        // 初始化语言文本并监听切换事件
        RefreshUILanguage();
        LocalizationManager.OnLanguageChanged += RefreshUILanguage;
    }

    void OnDestroy()
    {
        LocalizationManager.OnLanguageChanged -= RefreshUILanguage;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (_isWindowOpen) CloseWindow();
            else OpenWindow();
        }

        if (Input.GetKeyDown(KeyCode.Escape) && _isWindowOpen)
        {
            CloseWindow();
        }
    }

    // 动态刷新仓库里的 UI 文本
    private void RefreshUILanguage()
    {
        // 1. 刷新顶部标签
        string[] tabKeys = { "Tab_Single", "Tab_Dual", "Tab_String", "Tab_Favorite", "Tab_All" };
        for (int i = 0; i < tabButtons.Length && i < tabKeys.Length; i++)
        {
            if (tabButtons[i] == null) continue;
            
            Text tabText = tabButtons[i].GetComponentInChildren<Text>(true);
            if (tabText != null)
            {
                tabText.text = LocalizationManager.Instance.GetText(tabKeys[i]);
            }
        }

        // 2. 刷新列表（以此更新文玩的名称）
        RefreshList();
    }

    public void OpenWindow()
    {
        if (warehousePanel == null || _isWindowOpen) return; 

        RecordCurrentPosition();

        _isWindowOpen = true;

        Screen.SetResolution(expandedSize.x, expandedSize.y, FullScreenMode.Windowed);
        warehousePanel.SetActive(true);

        if (MousePenetrationController.Instance != null)
            MousePenetrationController.Instance.SetForceSolid(true);

        SwitchTab((int)UITabType.All);
        
        StopAllCoroutines(); 
        StartCoroutine(RefreshWindowStyleLoop(false, false)); 
    }

    public void CloseWindow()
    {
        if (warehousePanel != null) warehousePanel.SetActive(false);
        _isWindowOpen = false;

        Screen.SetResolution(widgetSize.x, widgetSize.y, FullScreenMode.Windowed);

        if (MousePenetrationController.Instance != null)
            MousePenetrationController.Instance.SetForceSolid(false);
        
        StopAllCoroutines();
        StartCoroutine(RefreshWindowStyleLoop(true, true));
    }

    private void RecordCurrentPosition()
    {
#if !UNITY_EDITOR
        IntPtr hwnd = FindWindow("UnityWndClass", null);
        if (hwnd == IntPtr.Zero) hwnd = GetActiveWindow();
        if (hwnd != IntPtr.Zero)
        {
            RECT r;
            if (GetWindowRect(hwnd, out r))
            {
                _lastWidgetPos = new Vector2Int(r.Left, r.Top);
            }
        }
#endif
    }

    IEnumerator RefreshWindowStyleLoop(bool isTopMost, bool shouldRestorePosition)
    {
        yield return null; 
        for (int i = 0; i < 5; i++)
        {
            ApplyWin32Style(isTopMost, shouldRestorePosition);
            yield return new WaitForEndOfFrame(); 
        }
    }

    private void ApplyWin32Style(bool isTopMost, bool shouldRestorePosition)
    {
#if !UNITY_EDITOR
        IntPtr hwnd = FindWindow("UnityWndClass", null);
        if (hwnd == IntPtr.Zero) hwnd = GetActiveWindow();
        if (hwnd == IntPtr.Zero) return;

        SetForegroundWindow(hwnd);

        uint style = (uint)GetWindowLong(hwnd, GWL_STYLE);
        style &= ~WS_CAPTION;
        style &= ~WS_THICKFRAME;
        style &= ~WS_SYSMENU;
        style |= WS_POPUP | WS_VISIBLE;

        SetWindowLong(hwnd, GWL_STYLE, style);

        MARGINS margins = new MARGINS { cxLeftWidth = -1 };
        DwmExtendFrameIntoClientArea(hwnd, ref margins);

        IntPtr state = isTopMost ? HWND_TOPMOST : HWND_NOTOPMOST;
        
        uint flags = SWP_NOSIZE | SWP_FRAMECHANGED | SWP_SHOWWINDOW;
        int x = 0;
        int y = 0;

        if (shouldRestorePosition)
        {
            x = _lastWidgetPos.x;
            y = _lastWidgetPos.y;
        }
        else
        {
            flags |= SWP_NOMOVE;
        }

        SetWindowPos(hwnd, state, x, y, 0, 0, flags);
#endif
    }

    public void SwitchTab(int tabIndex)
    {
        _currentTab = (UITabType)tabIndex;
        UpdateTabVisuals(tabIndex);
        RefreshList();
    }

    private void UpdateTabVisuals(int selectedIndex)
    {
        if (tabButtons == null || tabButtons.Length == 0) return;

        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null) continue;
            tabButtons[i].SetSelected(i == selectedIndex);
        }
    }

    private void RefreshList()
    {
        if (itemsContainer == null || SpeciesManager.Instance == null) return;
        
        foreach (Transform child in itemsContainer) 
        { 
            if (child.gameObject != itemTemplate) Destroy(child.gameObject); 
        }

        var list = SpeciesManager.Instance.allSpecies;
        for (int i = 0; i < list.Count; i++)
        {
            var config = list[i];
            if (config == null) continue;

            if (_currentTab != UITabType.All)
            {
                if (_currentTab == UITabType.Favorite && !SaveManager.Instance.IsFavorite(config.speciesID)) continue;
                if (_currentTab == UITabType.Single && config.displayMode != DisplayMode.Single) continue;
                if (_currentTab == UITabType.Dual && config.displayMode != DisplayMode.Dual) continue;
                if (_currentTab == UITabType.String && config.displayMode != DisplayMode.String) continue;
            }

            int index = i; 
            GameObject newItem = Instantiate(itemTemplate, itemsContainer);
            newItem.SetActive(true);
            
            Text nameText = newItem.GetComponentInChildren<Text>();
            if (nameText != null) 
            {
                // 如果 LocalizationManager 中有对应文玩原名的英文翻译，就会显示英文。
                // 如果没有配置，GetText 会直接原样返回它的中文名 (config.speciesName)。
                nameText.text = LocalizationManager.Instance.GetText(config.speciesName);
            }
            
            Image iconImg = newItem.transform.Find("Icon")?.GetComponent<Image>() ?? newItem.GetComponentInChildren<Image>();
            if (iconImg != null) 
            {
                iconImg.sprite = config.speciesIcon;
                iconImg.enabled = (config.speciesIcon != null);
            }

            WenwanItemProgress progressScript = newItem.GetComponent<WenwanItemProgress>();
            if (progressScript != null)
            {
                progressScript.UpdateFill(config.speciesID);
            }
            
            Button mainBtn = newItem.GetComponent<Button>();
            if (mainBtn != null) 
            { 
                mainBtn.onClick.RemoveAllListeners(); 
                mainBtn.onClick.AddListener(() => OnSelectItem(index)); 
            }

            Transform favTransform = newItem.transform.Find("FavoriteButton");
            if (favTransform != null)
            {
                Button favBtn = favTransform.GetComponent<Button>();
                Image favIcon = favTransform.GetComponent<Image>();
                if (favBtn != null && favIcon != null)
                {
                    favBtn.onClick.RemoveAllListeners();
                    
                    bool isFav = SaveManager.Instance.IsFavorite(config.speciesID);
                    favIcon.color = isFav ? Color.red : Color.gray;

                    favBtn.onClick.AddListener(() => 
                    {
                        SaveManager.Instance.ToggleFavorite(config.speciesID);
                        RefreshList(); 
                    });
                }
            }
        }
    }

    private void OnSelectItem(int index)
    {
        if (SpeciesManager.Instance != null)
            SpeciesManager.Instance.ChangeSpeciesByIndex(index);
        CloseWindow();
    }
}