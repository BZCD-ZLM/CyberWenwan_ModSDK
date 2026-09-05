#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Steamworks;
using System.IO;
using System.Collections.Generic;

public class WenwanWorkshopUploader : EditorWindow
{
    [Header("Mod 基础信息")]
    private string targetModID = "";
    private string modTitle = "我的全新文玩 My new Wenwan";
    private string modDescription = "这是一个精美的赛博文玩Mod。";
    
    [Header("文件路径")]
    private string previewImagePath = "";

    // 内部状态
    private PublishedFileId_t currentFileId;

    // Steam 回调
    private CallResult<CreateItemResult_t> m_ItemCreated;
    private CallResult<SubmitItemUpdateResult_t> m_ItemSubmitted;

    public static void ShowWindow()
    {
        GetWindow<WenwanWorkshopUploader>("创意工坊上传器 / Workshop Uploader").minSize = new Vector2(500, 500);
    }

    public static void OpenForUpload(string modID)
    {
        WenwanWorkshopUploader window = GetWindow<WenwanWorkshopUploader>("创意工坊上传器 / Workshop Uploader");
        window.targetModID = modID;
        window.minSize = new Vector2(500, 500);
        window.Show();
    }

    private void OnEnable()
    {
        if (Application.isPlaying && SteamManager.Initialized)
        {
            m_ItemCreated = CallResult<CreateItemResult_t>.Create(OnItemCreated);
            m_ItemSubmitted = CallResult<SubmitItemUpdateResult_t>.Create(OnItemSubmitted);
        }
    }

    private void OnGUI()
    {
        GUIStyle btnStyle = new GUIStyle(GUI.skin.button) { richText = true };

        GUILayout.Label("Steam 创意工坊独立上传中心\n<color=#888888>Steam Workshop Upload Center</color>", new GUIStyle(EditorStyles.boldLabel) { richText = true });
        EditorGUILayout.Space();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("提示：上传 Steam 需要在游戏运行状态下进行！\n请先点击 Unity 顶部的 ▶ Play (运行) 按钮，本窗口的数据不会丢失。", MessageType.Warning);
        }
        else if (!SteamManager.Initialized)
        {
            EditorGUILayout.HelpBox("Steam 未初始化！请确认 Steam 客户端已开启，并且 steam_appid.txt 配置正确。", MessageType.Error);
        }

        EditorGUILayout.Space(15);

        DrawHeader("☁️", "创意工坊信息填写", "Fill Steam Workshop Info");

        targetModID = DrawDualTextField("文玩包ID (Mod ID)", "Target Mod ID", targetModID);
        modTitle = DrawDualTextField("Mod 标题 (必填)", "Mod Title (Required)", modTitle);
        
        GUILayout.BeginVertical();
        GUILayout.Label("Mod 简介 / Description:", GUILayout.Height(16));
        modDescription = EditorGUILayout.TextArea(modDescription, GUILayout.Height(60));
        GUILayout.EndVertical();
        
        EditorGUILayout.Space(5);
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(EditorGUIUtility.labelWidth));
        GUILayout.Label("封面图路径 (绝对路径)", GUILayout.Height(16));
        GUILayout.Label("<color=#888888>Preview Image Path</color>", new GUIStyle(EditorStyles.miniLabel) { richText = true }, GUILayout.Height(12));
        GUILayout.EndVertical();
        previewImagePath = EditorGUILayout.TextField(previewImagePath, GUILayout.Height(28));
        if (GUILayout.Button("选择图片", GUILayout.Width(80), GUILayout.Height(28)))
        {
            string path = EditorUtility.OpenFilePanel("选择封面图 (正方形)", "", "png,jpg,jpeg");
            if (!string.IsNullOrEmpty(path)) previewImagePath = path;
        }
        GUILayout.EndHorizontal();

        EditorGUILayout.Space(15);
        
        GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
        EditorGUI.BeginDisabledGroup(!Application.isPlaying || !SteamManager.Initialized);
        if (GUILayout.Button("🚀 一键发布至 Steam 创意工坊\n<size=10>Publish to Steam Workshop</size>", btnStyle, GUILayout.Height(50)))
        {
            StartSteamUploadProcess();
        }
        EditorGUI.EndDisabledGroup();
        GUI.backgroundColor = Color.white;
    }

    private void DrawHeader(string icon, string cn, string en) { GUILayout.Label($"{icon} {cn}\n      <color=#888888>{en}</color>", new GUIStyle(EditorStyles.boldLabel) { richText = true }); }
    private string DrawDualTextField(string cn, string en, string val) { GUILayout.BeginHorizontal(); GUILayout.BeginVertical(GUILayout.Width(EditorGUIUtility.labelWidth)); GUILayout.Label(cn, GUILayout.Height(16)); GUILayout.Label($"<color=#888888>{en}</color>", new GUIStyle(EditorStyles.miniLabel) { richText = true }, GUILayout.Height(12)); GUILayout.EndVertical(); val = EditorGUILayout.TextField(val, GUILayout.Height(28)); GUILayout.EndHorizontal(); return val; }

    private void StartSteamUploadProcess()
    {
        if (string.IsNullOrEmpty(targetModID)) { EditorUtility.DisplayDialog("错误", "文玩ID不能为空！", "确定"); return; }
        if (string.IsNullOrEmpty(previewImagePath) || !File.Exists(previewImagePath)) { EditorUtility.DisplayDialog("封面缺失", "必须选择一张有效的本地封面图！", "确定"); return; }

        FileInfo previewFile = new FileInfo(previewImagePath);
        if (previewFile.Length > 1024 * 1024) { EditorUtility.DisplayDialog("尺寸违规", "Steam 强制规定封面图大小不能超过 1MB！", "确定"); return; }
        
        string ext = previewFile.Extension.ToLower();
        if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") { EditorUtility.DisplayDialog("格式违规", "封面图必须是 PNG 或 JPG 格式！", "确定"); return; }

        string bundlePath = $"WorkshopExportStaging/{targetModID}/{targetModID}.bundle";
        if (!File.Exists(Path.GetFullPath(bundlePath))) { EditorUtility.DisplayDialog("上传失败", $"找不到目标打包文件！缺失: {bundlePath}", "确定"); return; }

        if (m_ItemCreated == null) m_ItemCreated = CallResult<CreateItemResult_t>.Create(OnItemCreated);
        if (m_ItemSubmitted == null) m_ItemSubmitted = CallResult<SubmitItemUpdateResult_t>.Create(OnItemSubmitted);

        EditorUtility.DisplayProgressBar("Steam Upload", "正在向 Steam 申请物品ID...", 0.3f);
        SteamAPICall_t handle = SteamUGC.CreateItem(SteamUtils.GetAppID(), EWorkshopFileType.k_EWorkshopFileTypeCommunity);
        m_ItemCreated.Set(handle);
    }

    private void OnItemCreated(CreateItemResult_t pCallback, bool bIOFailure)
    {
        if (bIOFailure || pCallback.m_eResult != EResult.k_EResultOK)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError($"Steam 物品创建失败！错误码: {pCallback.m_eResult}");
            EditorUtility.DisplayDialog("Steam 错误", $"创建物品失败！错误码: {pCallback.m_eResult}", "确定");
            return;
        }

        currentFileId = pCallback.m_nPublishedFileId;
        EditorUtility.DisplayProgressBar("Steam Upload", "正在打包推送至 Steam 服务器...", 0.6f);

        UGCUpdateHandle_t updateHandle = SteamUGC.StartItemUpdate(SteamUtils.GetAppID(), currentFileId);

        // ========================================================
        // 🚨 终极路径净化：强制转换为 Windows 原生的反斜杠 (\) 🚨
        // ========================================================
        string relativeContentPath = $"WorkshopExportStaging/{targetModID}";
        
        // 强制替换正斜杠为反斜杠，并去除末尾的多余斜杠
        string absoluteContentPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), relativeContentPath)).Replace('/', '\\').TrimEnd('\\');
        string absolutePreviewPath = Path.GetFullPath(previewImagePath).Replace('/', '\\');
        
        // 打印路径到 Console，供查错使用
        Debug.Log($"<color=cyan>[Uploader] 内容路径 (Content Path):</color> {absoluteContentPath}");
        Debug.Log($"<color=cyan>[Uploader] 封面路径 (Preview Path):</color> {absolutePreviewPath}");

        if (!Directory.Exists(absoluteContentPath))
        {
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("致命错误", $"内容文件夹不存在！\n系统识别的路径是:\n{absoluteContentPath}", "确定");
            return;
        }

        SteamUGC.SetItemTitle(updateHandle, modTitle);
        SteamUGC.SetItemDescription(updateHandle, modDescription);
        SteamUGC.SetItemContent(updateHandle, absoluteContentPath);
        SteamUGC.SetItemPreview(updateHandle, absolutePreviewPath);
        
        IList<string> tags = new List<string> { "Mod" };
        SteamUGC.SetItemTags(updateHandle, tags);

        SteamAPICall_t handle = SteamUGC.SubmitItemUpdate(updateHandle, "通过独立上传器首次上传");
        m_ItemSubmitted.Set(handle);
    }

    private void OnItemSubmitted(SubmitItemUpdateResult_t pCallback, bool bIOFailure)
    {
        EditorUtility.ClearProgressBar();

        if (bIOFailure || pCallback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError($"[Uploader] 上传内容失败！错误码: {pCallback.m_eResult}");
            EditorUtility.DisplayDialog("上传失败", $"推送数据失败！错误码: {pCallback.m_eResult}\n\n⚠️ 请务必检查 Steamworks 后台是否开启了创意工坊并分配了云存储配额！", "确定");
            return;
        }

        Debug.Log($"<color=#00FF00>上传大成功！</color> Mod 已经成功发布到 Steam！");
        if (EditorUtility.DisplayDialog("上传成功 (Success)", "Mod 已成功发布到 Steam 创意工坊！\n是否打开网页查看？", "打开网页 (Open)", "关闭 (Close)"))
        {
            Application.OpenURL($"https://steamcommunity.com/sharedfiles/filedetails/?id={currentFileId}");
        }
    }
}
#endif