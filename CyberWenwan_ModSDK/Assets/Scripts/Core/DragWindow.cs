using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class DragWindow : MonoBehaviour
{
    // --- Windows API ---
    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private IntPtr _hwnd;
    private bool _isDragging = false;
    private POINT _lastCursorPos;

    void Start()
    {
#if !UNITY_EDITOR
        _hwnd = GetActiveWindow();
#endif
    }

    void Update()
    {
        // 【修改点】 0 = 左键, 1 = 右键, 2 = 中键
        // 这里改为检测鼠标右键按下
        if (Input.GetMouseButtonDown(1))
        {
            // 只有点击到物体（核桃）时才允许拖拽
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray))
            {
                StartDrag();
            }
        }

        // 检测鼠标右键抬起
        if (Input.GetMouseButtonUp(1))
        {
            _isDragging = false;
        }

        // 执行拖拽逻辑
        if (_isDragging)
        {
            PerformDrag();
        }
    }

    private void StartDrag()
    {
#if !UNITY_EDITOR
        GetCursorPos(out POINT cursorPos);
        _lastCursorPos = cursorPos;
        _isDragging = true;
#endif
    }

    private void PerformDrag()
    {
#if !UNITY_EDITOR
        // 1. 获取当前屏幕鼠标位置
        GetCursorPos(out POINT currentCursorPos);

        // 2. 计算鼠标移动了多少像素 (Delta)
        int deltaX = currentCursorPos.X - _lastCursorPos.X;
        int deltaY = currentCursorPos.Y - _lastCursorPos.Y;

        // 3. 如果有移动，应用给窗口
        if (deltaX != 0 || deltaY != 0)
        {
            RECT winRect;
            GetWindowRect(_hwnd, out winRect);
            
            int newX = winRect.Left + deltaX;
            int newY = winRect.Top + deltaY;
            int width = winRect.Right - winRect.Left;
            int height = winRect.Bottom - winRect.Top;

            // 应用新位置
            SetWindowPos(_hwnd, IntPtr.Zero, newX, newY, width, height, 0x0040); // 0x0040 = SWP_SHOWWINDOW
            
            // 更新上一帧位置，确保连续平滑
            _lastCursorPos = currentCursorPos;
        }
#endif
    }
}