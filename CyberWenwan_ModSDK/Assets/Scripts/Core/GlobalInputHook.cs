using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class GlobalInputHook : MonoBehaviour
{
    // ================= Windows API 定义 =================
    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public int x; public int y;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_MOUSEWHEEL = 0x020A;

    // ================= 状态变量 =================
    private const int SCROLL_THRESHOLD = 2880; 
    private int _currentScrollAccumulator = 0;

    private HookProc _keyboardProc;
    private HookProc _mouseProc;
    private IntPtr _keyboardHookID = IntPtr.Zero;
    private IntPtr _mouseHookID = IntPtr.Zero;

    // 记录每帧的动作次数
    private int _pendingKeys = 0;
    private int _pendingClicks = 0;
    private int _pendingScrolls = 0;
    
    // 【新增】用于单件文玩时长的本地累加器
    private float _timeAccumulator = 0f;

    // 修改：分别传递键盘、点击、滚轮的次数
    public static event Action<int, int, int> OnValidInput;

    void Awake() { Application.runInBackground = true; }

    void Start()
    {
#if !UNITY_EDITOR
        _keyboardProc = KeyboardHookCallback;
        _mouseProc = MouseHookCallback;
        using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
        using (var curModule = curProcess.MainModule)
        {
            _keyboardHookID = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, GetModuleHandle(curModule.ModuleName), 0);
            _mouseHookID = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(curModule.ModuleName), 0);
        }
#endif
    }

    void OnDestroy()
    {
#if !UNITY_EDITOR
        if (_keyboardHookID != IntPtr.Zero) UnhookWindowsHookEx(_keyboardHookID);
        if (_mouseHookID != IntPtr.Zero) UnhookWindowsHookEx(_mouseHookID);
#endif
    }

    void Update()
    {
        var currentConfig = GetActiveConfig();
        if (currentConfig == null) return;

        // 【新增成就逻辑】累计在线盘玩总时长 (每帧累加)
        if (SteamAchievementManager.Instance != null)
        {
            SteamAchievementManager.Instance.AddPlayTime(Time.deltaTime);
        }

        // 【新增成就逻辑】单件文玩时长统计 (每 5 秒保存一次，避免性能浪费)
        _timeAccumulator += Time.deltaTime;
        if (_timeAccumulator >= 5f)
        {
            string timeKey = "PlayTime_" + currentConfig.speciesID;
            float currentItemTime = PlayerPrefs.GetFloat(timeKey, 0f);
            currentItemTime += _timeAccumulator;
            PlayerPrefs.SetFloat(timeKey, currentItemTime);
            
            if (SteamAchievementManager.Instance != null)
            {
                // 将秒转换为小时传入验证
                SteamAchievementManager.Instance.CheckSingleItemPlayTime(currentItemTime / 3600f);
            }
            _timeAccumulator = 0f;
        }

        if (CheckIsInspecting(currentConfig))
        {
            _pendingKeys = 0; _pendingClicks = 0; _pendingScrolls = 0;
            return; 
        }

#if UNITY_EDITOR
        // 编辑器内输入模拟
        if (Input.anyKeyDown) _pendingKeys++;
        if (Input.GetMouseButtonDown(0)) _pendingClicks++;
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.1f)
        {
            _currentScrollAccumulator += (int)(Mathf.Abs(scroll) * 120);
            while (_currentScrollAccumulator >= SCROLL_THRESHOLD)
            {
                _pendingScrolls++;
                _currentScrollAccumulator -= SCROLL_THRESHOLD;
            }
        }
#endif

        if (_pendingKeys > 0 || _pendingClicks > 0 || _pendingScrolls > 0)
        {
            // 【新增成就逻辑】累加总盘玩次数
            if (SteamAchievementManager.Instance != null)
            {
                int totalActions = _pendingKeys + _pendingClicks + _pendingScrolls;
                SteamAchievementManager.Instance.AddPlayCount(totalActions);
            }

            // 包浆能量依然由 Config 决定
            float evoAmount = (_pendingKeys * currentConfig.evolutionKeyboardWeight) +
                              (_pendingClicks * currentConfig.evolutionClickWeight) +
                              (_pendingScrolls * currentConfig.evolutionScrollWeight);
            
            DistributeInput(currentConfig, evoAmount, _pendingKeys, _pendingClicks, _pendingScrolls);
            
            // 触发事件并分别传入三种输入次数
            OnValidInput?.Invoke(_pendingKeys, _pendingClicks, _pendingScrolls);
            
            _pendingKeys = 0; _pendingClicks = 0; _pendingScrolls = 0;
        }
    }

    private WenwanSpeciesConfig GetActiveConfig()
    {
        if (WenwanStringController.Instance != null && WenwanStringController.Instance.gameObject.activeInHierarchy && WenwanStringController.Instance.currentConfig != null)
            return WenwanStringController.Instance.currentConfig;
        if (SpeciesManager.Instance != null) return SpeciesManager.Instance.GetCurrentConfig();
        return null;
    }

    private bool CheckIsInspecting(WenwanSpeciesConfig config)
    {
        if (config.displayMode == DisplayMode.Dual) return WenwanDualController.Instance != null && WenwanDualController.Instance.isInspecting;
        else if (config.displayMode == DisplayMode.String) return WenwanStringController.Instance != null && WenwanStringController.Instance.isInspecting;
        else return WenwanSingleController.Instance != null && WenwanSingleController.Instance.isInspecting;
    }

    // 【核心剥离】物理分发与包浆分发彻底分离
    private void DistributeInput(WenwanSpeciesConfig config, float evoAmount, int keys, int clicks, int scrolls)
    {
        if (config.displayMode == DisplayMode.Dual && WenwanDualController.Instance != null)
        {
            WenwanDualController.Instance.AddProgress(evoAmount, 0f); 
            WenwanDualController.Instance.GetComponent<DualWalnutMotion>()?.AddPhysicalInput(keys, clicks, scrolls);
        }
        else if (config.displayMode == DisplayMode.String && WenwanStringController.Instance != null)
        {
            WenwanStringController.Instance.AddProgress(evoAmount, 0f);
            WenwanStringController.Instance.GetComponent<StringMotion>()?.AddPhysicalInput(keys, clicks, scrolls);
        }
        else if (WenwanSingleController.Instance != null)
        {
            WenwanSingleController.Instance.AddProgress(evoAmount, 0f);
            WenwanSingleController.Instance.GetComponent<SingleWalnutMotion>()?.AddPhysicalInput(keys, clicks, scrolls);
        }
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)) _pendingKeys++;
        return CallNextHookEx(_keyboardHookID, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            bool isClick = (wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)WM_RBUTTONDOWN || wParam == (IntPtr)WM_MBUTTONDOWN);
            if (isClick) _pendingClicks++;
            else if (wParam == (IntPtr)WM_MOUSEWHEEL)
            {
                MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                short delta = (short)((hookStruct.mouseData >> 16) & 0xFFFF);
                _currentScrollAccumulator += Mathf.Abs(delta);
                while (_currentScrollAccumulator >= SCROLL_THRESHOLD)
                {
                    _pendingScrolls++;
                    _currentScrollAccumulator -= SCROLL_THRESHOLD;
                }
            }
        }
        return CallNextHookEx(_mouseHookID, nCode, wParam, lParam);
    }
}