using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems; // 必须引用：用于检测UI

public class MousePenetrationController : MonoBehaviour
{
    public static MousePenetrationController Instance { get; private set; }

    // --- Windows API ---
    [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);
    [DllImport("user32.dll")] private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);

    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    private const int GWL_EXSTYLE = -20;
    private const uint WS_EX_TRANSPARENT = 0x00000020;
    private const int VK_LBUTTON = 0x01; 
    private const int VK_MBUTTON = 0x04; // 新增：鼠标中键代码

    private IntPtr _hWnd;
    private bool _isThrough = false; // 当前是否处于“穿透”状态
    private bool _forceSolid = false; // 外部强制实体化开关
    private Camera _mainCamera;
    
    [Header("Settings")]
    public LayerMask interactableLayer = -1; // 建议设置为 Default

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        _mainCamera = Camera.main;
        #if !UNITY_EDITOR
        _hWnd = GetActiveWindow();
        // 初始状态：设为不穿透，确保一开始能点
        SetClickThrough(false);
        #endif
    }

    void Update()
    {
        #if !UNITY_EDITOR
        // 0. 如果外部强制要求实体化 (比如打开了仓库)，直接跳过检测
        if (_forceSolid)
        {
            if (_isThrough) SetClickThrough(false);
            return;
        }

        // 1. 拖拽保护：如果按住左键 OR 中键，强制不穿透
        // 这样拖拽时即使鼠标滑出物体范围，也不会丢失焦点
        bool isLeftDown = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
        bool isMiddleDown = (GetAsyncKeyState(VK_MBUTTON) & 0x8000) != 0;

        if (isLeftDown || isMiddleDown)
        {
            if (_isThrough) SetClickThrough(false);
            return;
        }

        // 2. UI 检测 (关键修复)
        // 注意：EventSystem 只有在窗口“不穿透”时才能检测到。
        // 所以这个检测主要用于：鼠标已经移到了物体/UI上，窗口变实了，此时要保持住，别变回去。
        if (!_isThrough && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            // 鼠标在 UI 上，保持实体状态
            return; 
        }

        // 3. 3D 射线检测 (你原本的逻辑)
        // 计算鼠标在 Unity 窗口内的相对坐标
        GetCursorPos(out POINT globalMousePos);
        GetWindowRect(_hWnd, out RECT winRect);
        float unityMouseX = globalMousePos.X - winRect.Left;
        float unityMouseY = (winRect.Bottom - winRect.Top) - (globalMousePos.Y - winRect.Top);
        
        // 发射射线
        Ray ray = _mainCamera.ScreenPointToRay(new Vector3(unityMouseX, unityMouseY, 0));
        
        // 检测是否撞到了文玩
        if (Physics.Raycast(ray, 100f, interactableLayer))
        {
            // 撞到了 -> 变实体 (可点击)
            if (_isThrough) SetClickThrough(false);
        }
        else
        {
            // 没撞到文玩，也没按键 -> 变穿透 (操作桌面)
            if (!_isThrough) SetClickThrough(true);
        }
        #endif
    }

    // 核心切换方法
    private void SetClickThrough(bool allowThrough)
    {
        #if !UNITY_EDITOR
        uint currentStyle = GetWindowLong(_hWnd, GWL_EXSTYLE);
        if (allowThrough)
        {
            // 加上穿透标记
            SetWindowLong(_hWnd, GWL_EXSTYLE, currentStyle | WS_EX_TRANSPARENT);
            _isThrough = true;
        }
        else
        {
            // 移除穿透标记
            SetWindowLong(_hWnd, GWL_EXSTYLE, currentStyle & ~WS_EX_TRANSPARENT);
            _isThrough = false;
        }
        #endif
    }

    // --- 公开接口：供 UI 脚本调用 ---
    public void SetForceSolid(bool isSolid)
    {
        _forceSolid = isSolid;
        // 如果强制实体化，立刻执行一次切换
        if (isSolid && _isThrough) SetClickThrough(false);
    }
}