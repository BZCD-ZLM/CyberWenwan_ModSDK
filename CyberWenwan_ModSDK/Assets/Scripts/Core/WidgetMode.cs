using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

public class WidgetMode : MonoBehaviour
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("Dwmapi.dll")]
    private static extern uint DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);

    private struct MARGINS { public int cxLeftWidth; public int cxRightWidth; public int cyTopHeight; public int cyBottomHeight; }

    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const uint WS_POPUP = 0x80000000;
    private const uint WS_VISIBLE = 0x10000000;
    private const uint WS_EX_LAYERED = 0x00080000;
    private const uint WS_EX_TOPMOST = 0x00000008;

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_NOMOVE = 0x0002;

    void Awake()
    {
        Application.runInBackground = true;
        // 移除 Awake 中的 SetResolution，避免启动报错
        // 我们完全依赖 SetWindowPos 来控制大小
    }

    void Start()
    {
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        StartCoroutine(ApplyWindowSettingsRoutine());
#endif
    }

    private IEnumerator ApplyWindowSettingsRoutine()
    {
        // 等待一小会儿，确保 Unity 窗口初始化完成
        yield return new WaitForSeconds(0.5f);
        
        IntPtr hWnd = FindWindow("UnityWndClass", null);
        if (hWnd != IntPtr.Zero)
        {
            // 1. 设置样式
            SetWindowLong(hWnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);
            SetWindowLong(hWnd, GWL_EXSTYLE, WS_EX_LAYERED | WS_EX_TOPMOST);
            
            // 2. 透明化
            MARGINS margins = new MARGINS();
            margins.cxLeftWidth = -1;
            DwmExtendFrameIntoClientArea(hWnd, ref margins);

            // 3. 强制锁定窗口大小为 400x400
            // 这行代码比 SetResolution 更霸道，更有效
            SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 400, 400, SWP_FRAMECHANGED | SWP_NOMOVE | SWP_SHOWWINDOW);
        }
    }
}