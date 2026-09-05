using UnityEngine;
using System.Diagnostics; // 引入系统进程库

public class AppLifecycle : MonoBehaviour
{
    public static AppLifecycle Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    // --- 已移除：右键菜单相关的 Update, OnGUI 和变量 ---

    // 保留此公共方法，供其他脚本（如系统托盘）调用以执行安全退出
    public void QuitApplication()
    {
        // 1. 尝试存档 (目前由 WenwanController 在 OnApplicationQuit 处理，这里保留接口)
        if (SaveManager.Instance != null)
        {
            // SaveManager.Instance.SaveGame(...); 
        }

        // 2. 优先让托盘脚本处理退出（清理图标）
        var tray = FindObjectOfType<TrayMenuController>();
        if (tray != null)
        {
            tray.QuitApp();
            return; 
        }

        // 3. 兜底强行退出
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Process.GetCurrentProcess().Kill();
#endif
    }
}