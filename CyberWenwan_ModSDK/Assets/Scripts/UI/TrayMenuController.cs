using UnityEngine;
using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

#if UNITY_STANDALONE_WIN
using System.Windows.Forms;
using System.Drawing;
#endif

public class TrayMenuController : MonoBehaviour
{
#if UNITY_STANDALONE_WIN
    [Header("托盘图标设置")]
    [Tooltip("优先读取 StreamingAssets 文件夹下的原生 .ico 文件")]
    public string icoFileName = "trayIcon.ico"; 
    [Tooltip("作为兜底：如果找不到 .ico，则使用这张贴图转换")]
    public Texture2D trayIconTexture; 

    [Header("右键菜单 - 样式设置")]
    [Tooltip("菜单图标的大小 (像素)，默认 16，建议设为 32 或 48")]
    public int menuIconSize = 40; 

    [Header("右键菜单 - 图标设置")]
    public Texture2D menuOpenIcon; 
    public Texture2D menuExitIcon; 

    private NotifyIcon _notifyIcon;
    private ContextMenuStrip _contextMenu;
    private bool _shouldToggleWarehouse = false;
    private bool _isCleanedUp = false;

    // 菜单项引用，用于动态刷新文本
    private ToolStripMenuItem _openItem;
    private ToolStripMenuItem _exitItem;
    private ToolStripMenuItem _languageToggleItem;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    void Start()
    {
#if !UNITY_EDITOR
        try
        {
            CleanUp();
            InitializeContextMenu();
            InitializeNotifyIcon();
            
            // 初始化语言文本并监听切换事件
            UpdateMenuText();
            LocalizationManager.OnLanguageChanged += UpdateMenuText;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[Tray Error] Start 初始化失败: {e.Message}");
        }
#else
        // Editor 模式下也可以监听，防止报错
        LocalizationManager.OnLanguageChanged += UpdateMenuText;
#endif
    }

    void Update()
    {
        if (_shouldToggleWarehouse)
        {
            _shouldToggleWarehouse = false;
            
            SpeciesWindowUI ui = SpeciesWindowUI.Instance;
            if (ui == null) ui = FindObjectOfType<SpeciesWindowUI>(true);

            if (ui != null)
            {
                // 【核心修复】判断面板是否处于激活状态，实现开关反转 (Toggle)
                if (ui.warehousePanel != null && ui.warehousePanel.activeSelf)
                {
                    ui.CloseWindow();
                }
                else
                {
                    ui.OpenWindow();
                }
            }
        }

#if !UNITY_EDITOR
        try
        {
            System.Windows.Forms.Application.DoEvents();
        }
        catch { }
#endif
    }

    private void InitializeContextMenu()
    {
        _contextMenu = new ContextMenuStrip();
        
        // 使用自定义的"强力白化"渲染器
        _contextMenu.Renderer = new WhiteRenderer();

        // 设置图标显示尺寸
        _contextMenu.ImageScalingSize = new System.Drawing.Size(menuIconSize, menuIconSize);

        // 1. 创建 "打开" 菜单项
        _openItem = new ToolStripMenuItem();
        if (menuOpenIcon != null)
        {
            _openItem.Image = TextureToBitmap(menuOpenIcon);
        }
        _openItem.Click += (s, e) => { _shouldToggleWarehouse = true; };
        _contextMenu.Items.Add(_openItem);

        // 添加分隔线
        _contextMenu.Items.Add(new ToolStripSeparator());

        // 2. 创建 "语言切换" 菜单项 (单键 Toggle)
        _languageToggleItem = new ToolStripMenuItem();
        _languageToggleItem.Click += (s, e) => 
        {
            if (LocalizationManager.Instance.CurrentLanguage == LocalizationManager.Language.Chinese)
                LocalizationManager.Instance.SetLanguage(LocalizationManager.Language.English);
            else
                LocalizationManager.Instance.SetLanguage(LocalizationManager.Language.Chinese);
        };
        _contextMenu.Items.Add(_languageToggleItem);

        // 添加分隔线
        _contextMenu.Items.Add(new ToolStripSeparator());

        // 3. 创建 "退出" 菜单项
        _exitItem = new ToolStripMenuItem();
        if (menuExitIcon != null)
        {
            _exitItem.Image = TextureToBitmap(menuExitIcon);
        }
        _exitItem.Click += (s, e) => { QuitApp(); };
        _contextMenu.Items.Add(_exitItem);
    }

    private void UpdateMenuText()
    {
        if (_openItem != null) _openItem.Text = LocalizationManager.Instance.GetText("Menu_ShowHide");
        if (_exitItem != null) _exitItem.Text = LocalizationManager.Instance.GetText("Menu_Exit");
        
        if (_languageToggleItem != null)
        {
            if (LocalizationManager.Instance.CurrentLanguage == LocalizationManager.Language.Chinese)
                _languageToggleItem.Text = "English";
            else
                _languageToggleItem.Text = "中文";
        }
    }

    private void InitializeNotifyIcon()
    {
        _notifyIcon = new NotifyIcon();

        // 核心修复：强制指定使用 UnityEngine 的 Application，避免与 WinForms 的 Application 撞车
        string icoPath = Path.Combine(UnityEngine.Application.streamingAssetsPath, icoFileName);
        
        if (File.Exists(icoPath))
        {
            try
            {
                _notifyIcon.Icon = new System.Drawing.Icon(icoPath);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[Tray] 加载原生 ICO 失败，退回旧版加载模式。报错: {e.Message}");
                ApplyFallbackIcon();
            }
        }
        else
        {
            ApplyFallbackIcon();
        }

        _notifyIcon.Text = "CyberWenwan";
        _notifyIcon.Visible = true;
        _notifyIcon.ContextMenuStrip = _contextMenu;
        _notifyIcon.DoubleClick += (s, e) => { _shouldToggleWarehouse = true; };
    }

    private void ApplyFallbackIcon()
    {
        if (trayIconTexture != null)
            _notifyIcon.Icon = TextureToIcon(trayIconTexture);
        else
            _notifyIcon.Icon = SystemIcons.Application;
    }

    private System.Drawing.Icon TextureToIcon(Texture2D texture)
    {
        try
        {
            byte[] bytes = texture.EncodeToPNG();
            using (MemoryStream ms = new MemoryStream(bytes))
            {
                using (Bitmap bmp = new Bitmap(ms))
                {
                    return System.Drawing.Icon.FromHandle(bmp.GetHicon());
                }
            }
        }
        catch { return SystemIcons.Application; }
    }

    private System.Drawing.Bitmap TextureToBitmap(Texture2D texture)
    {
        try
        {
            byte[] bytes = texture.EncodeToPNG();
            using (MemoryStream ms = new MemoryStream(bytes))
            {
                return new Bitmap(ms);
            }
        }
        catch { return null; }
    }

    public void QuitApp()
    {
        CleanUp();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Process.GetCurrentProcess().Kill(); 
#endif
    }

    void OnApplicationQuit() { CleanUp(); }
    void OnDestroy() { CleanUp(); }

    private void CleanUp()
    {
        if (_isCleanedUp) return;
        try
        {
            LocalizationManager.OnLanguageChanged -= UpdateMenuText;
            if (_notifyIcon != null) { _notifyIcon.Visible = false; _notifyIcon.Dispose(); _notifyIcon = null; }
            if (_contextMenu != null) { _contextMenu.Dispose(); _contextMenu = null; }
        }
        catch { }
        _isCleanedUp = true;
    }

    private class WhiteRenderer : ToolStripProfessionalRenderer
    {
        public WhiteRenderer() : base(new WhiteMarginColorTable()) { }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            using (var brush = new SolidBrush(System.Drawing.Color.White))
            {
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }
        }
    }

    private class WhiteMarginColorTable : ProfessionalColorTable
    {
        public override System.Drawing.Color ImageMarginGradientBegin => System.Drawing.Color.White;
        public override System.Drawing.Color ImageMarginGradientMiddle => System.Drawing.Color.White;
        public override System.Drawing.Color ImageMarginGradientEnd => System.Drawing.Color.White;
    }
#else
    void Start() {}
    void Update() {}
#endif
}