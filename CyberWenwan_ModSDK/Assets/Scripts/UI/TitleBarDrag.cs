using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems; 

public class TitleBarDrag : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [DllImport("user32.dll", SetLastError = true)] 
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

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
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

#if !UNITY_EDITOR
    private IntPtr _hwnd; 
    private POINT _lastCursorPos;
    private bool _isDragging = false;
#endif

    public void OnPointerDown(PointerEventData eventData)
    {
#if !UNITY_EDITOR
        _hwnd = FindWindow(null, "CyberWenwan");
        if (_hwnd == IntPtr.Zero) _hwnd = GetActiveWindow();

        if (_hwnd != IntPtr.Zero)
        {
            GetCursorPos(out _lastCursorPos);
            _isDragging = true;
        }
#endif
    }

    public void OnDrag(PointerEventData eventData)
    {
#if !UNITY_EDITOR
        if (!_isDragging || _hwnd == IntPtr.Zero) return;

        POINT currentCursorPos;
        if (GetCursorPos(out currentCursorPos))
        {
            int deltaX = currentCursorPos.X - _lastCursorPos.X;
            int deltaY = currentCursorPos.Y - _lastCursorPos.Y;

            if (deltaX != 0 || deltaY != 0)
            {
                RECT winRect;
                if (GetWindowRect(_hwnd, out winRect))
                {
                    SetWindowPos(_hwnd, IntPtr.Zero, winRect.Left + deltaX, winRect.Top + deltaY, 0, 0, 0x0001 | 0x0004 | 0x0040); 
                    _lastCursorPos = currentCursorPos;
                }
            }
        }
#endif
    }
}