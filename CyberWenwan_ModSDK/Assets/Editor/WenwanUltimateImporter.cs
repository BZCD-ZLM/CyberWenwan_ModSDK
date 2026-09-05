#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class WenwanUltimateImporter : EditorWindow
{
    private DefaultAsset targetFolder;
    private Vector2 scrollPos;

    // --- 独立ID设定 ---
    private string customSpeciesID = "myplay_01";

    // --- 折叠菜单状态控制 ---
    private bool foldoutPath = true;
    private bool foldoutAutomation = false;
    private bool foldoutDifficulty = true;
    private bool foldoutCurve = false;
    private bool foldoutWeights = false;
    private bool foldoutMaterial = false;
    private bool foldoutDust = false;
    private bool foldoutRender = false;

    // --- 难度预设枚举 ---
    public enum DifficultyPreset
    {
        高难度_盘玩慢_Hard = 0,
        中难度_盘玩中_Normal = 1,
        低难度_盘玩快_Easy = 2
    }
    private DifficultyPreset currentDifficulty = DifficultyPreset.中难度_盘玩中_Normal;

    // --- 形态识别覆盖 ---
    private int manualMeshCountOverride = 0; 

    // --- 自动化处理开关 ---
    private bool autoFixNormalMap = true;
    private bool autoFixLinearTexture = true;
    private bool autoSetIconToSprite = true;
    private bool autoGenerateMissingTextures = true;
    
    // --- 顶点AO设置 ---
    private bool autoBakeVertexAO = false; 
    private int aoSamples = 256; 
    private float aoRadius = 0.5f; 
    private float aoIntensity = 0.6f; 

    private bool autoRegisterToManager = true;
    private bool isWorkshopItem = true; 
    private bool autoCloseAfterImport = false; 

    // --- 缺失贴图生成强度 ---
    private float genTexASaturation = 0.75f;
    private float genTexAValue = 1.15f;
    private float genMaskContrast = 0.2f;

    // --- 进化曲线 ---
    private AnimationCurve colorCurve;
    private AnimationCurve glossinessCurve;

    // --- 进化进度权重 ---
    private float evoKeyboard = 0.0001f;
    private float evoClick = 0.0001f;
    private float evoScroll = 0.0001f;
    private float evoSpeed = 1.0f;

    // --- 物理动力权重 ---
    private float physKeyboard = 0.05f;
    private float physClick = 0.05f;
    private float physScroll = 0.05f;
    private float physSpeed = 1.0f;
    private float rotationFriction = 0.95f;

    // --- 材质渲染参数 ---
    private Color colorATint;
    private Color colorBTint;
    private Color jadeColor;
    private float occlusionStrength = 0.6f;
    private float normalStrength = 0.25f;
    private float softness = 0.5f; 

    // --- 灰尘系统参数 ---
    private float dustAmount = 0f;
    private Color dustColor;
    private float hoursToMaxDust = 720f;
    private float dustCleanPerInput = 0.02f;

    // --- 反射探针设置 ---
    private bool autoAddReflectionProbe = true;
    private Texture customReflectionTexture;

    [MenuItem("Tools/CyberWenwan/一键究极资产导入 (Ultimate Importer)")]
    public static void ShowWindow()
    {
        WenwanUltimateImporter window = GetWindow<WenwanUltimateImporter>("究极导入器 Ultimate Importer");
        window.minSize = new Vector2(500, 850);
        window.autoCloseAfterImport = false;
        window.isWorkshopItem = true;
        window.AutoGenerateSpeciesID(); 
        window.Show();
    }

    public static void OpenFromUploader(string relativeFolderPath)
    {
        WenwanUltimateImporter window = GetWindow<WenwanUltimateImporter>("参数设置与生成 / Parameters & Generation");
        window.minSize = new Vector2(500, 850);
        
        if (!string.IsNullOrEmpty(relativeFolderPath))
        {
            window.targetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relativeFolderPath);
        }
        
        window.isWorkshopItem = true;
        window.autoCloseAfterImport = true;
        window.AutoGenerateSpeciesID(); 
        window.Show();
    }

    private void OnEnable()
    {
        colorATint = Color.white;
        colorBTint = Color.white;
        ColorUtility.TryParseHtmlString("#400300", out jadeColor);
        ColorUtility.TryParseHtmlString("#676767", out dustColor);
        
        Keyframe[] easeOutKeys = new Keyframe[] { new Keyframe(0f, 0f, 2f, 2f), new Keyframe(1f, 1f, 0f, 0f) };
        colorCurve = new AnimationCurve(easeOutKeys);
        
        Keyframe[] glossKeys = new Keyframe[] { new Keyframe(0f, 0.2f, 1.5f, 1.5f), new Keyframe(1f, 0.9f, 0f, 0f) };
        glossinessCurve = new AnimationCurve(glossKeys);

        ApplyDifficultyPreset(); 
        customReflectionTexture = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Art/Textures/3.png");
    }

    private void AutoGenerateSpeciesID()
    {
        string baseID = "myplay";
        int index = 1;
        string newID = $"{baseID}_{index:D2}";
        while (CheckIfSpeciesIDExists(newID)) { index++; newID = $"{baseID}_{index:D2}"; }
        customSpeciesID = newID;
    }

    private bool CheckIfSpeciesIDExists(string id)
    {
        string path = $"Assets/Scripts/Configs/{id}_Config.asset";
        return File.Exists(Path.GetFullPath(path));
    }

    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        GUILayout.Space(10);

        // --- 1. 基础路径与ID设定 ---
        foldoutPath = DrawFoldoutHeader("📁", "1. 选择资产与设定ID", "1. Select Asset & Set ID", foldoutPath);
        if (foldoutPath)
        {
            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("📁 从外部文件夹导入素材 (Import from External Folder)", GUILayout.Height(30)))
            {
                string path = EditorUtility.OpenFolderPanel("选择外部美术素材文件夹", "", "");
                if (!string.IsNullOrEmpty(path)) ImportExternalFolder(path);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
            
            targetFolder = DrawDualObjectField("包含FBX与贴图的文件夹", "Folder containing FBX & Textures", targetFolder);
            
            GUILayout.Space(5);
            EditorGUI.BeginChangeCheck();
            customSpeciesID = DrawDualTextField("文玩唯一ID (限英文/数字)", "Unique Species ID (En/Num Only)", customSpeciesID);
            if (EditorGUI.EndChangeCheck()) customSpeciesID = Regex.Replace(customSpeciesID.ToLower(), @"[^a-z0-9_]", "");

            GUILayout.Space(10);
            manualMeshCountOverride = DrawDualIntField("▶ 形态识别覆盖（0=自动识别）", "Mesh Count Override (0=Auto)", manualMeshCountOverride);
            if (manualMeshCountOverride < 0) manualMeshCountOverride = 0; 
            GUILayout.Space(10);
        }

        // --- 2. 自动化处理 ---
        GUILayout.Space(5);
        foldoutAutomation = DrawFoldoutHeader("⚙️", "2. 自动化处理参数设置", "2. Automation Settings", foldoutAutomation);
        if (foldoutAutomation)
        {
            GUILayout.Space(5);
            autoFixNormalMap = DrawDualToggle("自动转为 Normal map 格式", "Auto fix Normal map format", autoFixNormalMap);
            autoFixLinearTexture = DrawDualToggle("自动修正 _Mask 为 Linear", "Auto fix Mask to Linear (No sRGB)", autoFixLinearTexture);
            autoSetIconToSprite = DrawDualToggle("自动将 _Icon 转为 UI Sprite", "Auto convert Icon to Sprite", autoSetIconToSprite);
            autoGenerateMissingTextures = DrawDualToggle("自动生成缺失的贴图", "Auto generate missing textures", autoGenerateMissingTextures);
            
            autoBakeVertexAO = DrawDualToggle("自动烘焙顶点AO (耗时, 无需贴图)", "Auto Bake Vertex AO (Slow)", autoBakeVertexAO);
            if (autoBakeVertexAO)
            {
                EditorGUI.indentLevel++;
                aoSamples = DrawDualIntField("▶ AO 采样数 (默认256)", "AO Samples (Quality vs Time)", aoSamples);
                aoRadius = DrawDualFloat("▶ AO 检测半径 (根据模型大小微调)", "AO Radius (Shadow Spread)", aoRadius);
                aoIntensity = DrawDualSlider("▶ AO 阴影强度", "AO Intensity", aoIntensity, 0f, 1f);
                EditorGUI.indentLevel--;
            }

            autoRegisterToManager = DrawDualToggle("自动注册到 SpeciesManager", "Auto register to SpeciesManager", autoRegisterToManager);
            isWorkshopItem = DrawDualToggle("标记为创意工坊物品 (免解锁)", "Mark as Workshop Item (Free Unlock)", isWorkshopItem); 
            autoCloseAfterImport = DrawDualToggle("导入完成后自动关闭本窗口", "Auto close window after import", autoCloseAfterImport); 
            
            GUILayout.Space(10);
            GUILayout.Label("▶ 缺失贴图生成强度 (需开启自动生成)", EditorStyles.boldLabel);
            genTexASaturation = DrawDualSlider("底图饱和度乘数 (TexA Saturation)", "Base texture saturation multiplier", genTexASaturation, 0f, 2f);
            genTexAValue = DrawDualSlider("底图明度乘数 (TexA Value)", "Base texture brightness multiplier", genTexAValue, 0f, 2f);
            genMaskContrast = DrawDualSlider("遮罩对比度偏移 (Mask Contrast)", "Mask contrast generation offset", genMaskContrast, 0f, 0.5f);
            GUILayout.Space(10);
        }

        // --- 3. 难度预设 ---
        GUILayout.Space(5);
        foldoutDifficulty = DrawFoldoutHeader("🎮", "3. 盘玩难度设置", "3. Play Difficulty Settings", foldoutDifficulty);
        if (foldoutDifficulty)
        {
            GUILayout.Space(5);
            EditorGUI.BeginChangeCheck();
            currentDifficulty = DrawDualEnum("基础盘玩难度", "Base Play Difficulty", currentDifficulty);
            if (EditorGUI.EndChangeCheck()) ApplyDifficultyPreset();
            GUILayout.Space(10);
        }

        // --- 4. 进化曲线 ---
        GUILayout.Space(5);
        foldoutCurve = DrawFoldoutHeader("📈", "4. 进化曲线控制", "4. Evolution Curve Control", foldoutCurve);
        if (foldoutCurve)
        {
            GUILayout.Space(5);
            colorCurve = DrawDualCurve("包浆颜色曲线", "Patina Color Curve", colorCurve);
            glossinessCurve = DrawDualCurve("包浆光泽度曲线", "Patina Glossiness Curve", glossinessCurve);
            GUILayout.Space(10);
        }

        // --- 5. 详细权重 ---
        GUILayout.Space(5);
        foldoutWeights = DrawFoldoutHeader("⚖️", "5. 进化与物理权重 (受难度预设影响)", "5. Evolution & Physics Weights", foldoutWeights);
        if (foldoutWeights)
        {
            GUILayout.Space(5);
            evoKeyboard = DrawDualFloat("键盘盘玩权重", "Evolution Keyboard Weight", evoKeyboard);
            evoClick = DrawDualFloat("点击盘玩权重", "Evolution Click Weight", evoClick);
            evoScroll = DrawDualFloat("滚轮盘玩权重", "Evolution Scroll Weight", evoScroll);
            evoSpeed = DrawDualFloat("包浆基础倍率", "Evolution Speed Multiplier", evoSpeed);
            GUILayout.Space(5);
            physKeyboard = DrawDualFloat("键盘物理动力", "Physics Keyboard Power", physKeyboard);
            physClick = DrawDualFloat("点击物理动力", "Physics Click Power", physClick);
            physScroll = DrawDualFloat("滚轮物理动力", "Physics Scroll Power", physScroll);
            rotationFriction = DrawDualSlider("物理摩擦力", "Rotation Friction", rotationFriction, 0.8f, 1f);
            GUILayout.Space(10);
        }

        // --- 6. 材质参数 ---
        GUILayout.Space(5);
        foldoutMaterial = DrawFoldoutHeader("🎨", "6. 材质默认参数", "6. Default Material Parameters", foldoutMaterial);
        if (foldoutMaterial)
        {
            GUILayout.Space(5);
            colorATint = DrawDualColor("包浆前颜色乘数", "ColorA Tint", colorATint);
            colorBTint = DrawDualColor("包浆后颜色乘数", "ColorB Tint", colorBTint);
            jadeColor = DrawDualColor("玉化底色", "Jade Color", jadeColor);
            occlusionStrength = DrawDualSlider("AO 遮蔽强度", "Occlusion Strength", occlusionStrength, 0f, 1f);
            normalStrength = DrawDualSlider("法线强度", "Normal Strength", normalStrength, 0f, 5f);
            softness = DrawDualSlider("过渡柔和度 (Softness)", "Edge Softness", softness, 0f, 1f); 
            GUILayout.Space(10);
        }

        // --- 7. 灰尘参数 ---
        GUILayout.Space(5);
        foldoutDust = DrawFoldoutHeader("🌫️", "7. 灰尘系统参数", "7. Dust System Parameters", foldoutDust);
        if (foldoutDust)
        {
            GUILayout.Space(5);
            dustAmount = DrawDualSlider("初始灰尘量", "Initial Dust Amount", dustAmount, 0f, 1f);
            dustColor = DrawDualColor("灰尘颜色", "Dust Color", dustColor);
            hoursToMaxDust = DrawDualFloat("积满灰尘所需时间(小时)", "Hours To Max Dust", hoursToMaxDust);
            dustCleanPerInput = DrawDualFloat("每次按键清洁力度", "Dust Clean Per Input", dustCleanPerInput);
            GUILayout.Space(10);
        }

        // --- 8. 环境光与反射 ---
        GUILayout.Space(5);
        foldoutRender = DrawFoldoutHeader("✨", "8. 渲染与环境反射", "8. Render & Environment Reflection", foldoutRender);
        if (foldoutRender)
        {
            GUILayout.Space(5);
            autoAddReflectionProbe = DrawDualToggle("自动添加反射探针 (解决金属变黑)", "Auto Add Reflection Probe", autoAddReflectionProbe);
            if (autoAddReflectionProbe) customReflectionTexture = DrawDualTextureField("环境反射贴图 (Cubemap/HDR)", "Custom Reflection Texture", customReflectionTexture);
            GUILayout.Space(10);
        }

        // ==========================================
        // 🚀 第一步：生成游戏资产按钮
        // ==========================================
        GUILayout.Space(15);
        EditorGUILayout.HelpBox($"提示：当前设定的 ID 为 [{customSpeciesID}]。点击下方按钮将生成资产并【自动打包】。\n⚠️必须在【编辑模式 (Edit Mode)】下点击此按钮！", MessageType.Info);
        
        GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);
        if (GUILayout.Button("🛠️ 第一步：生成游戏资产并打包 (Generate & Build Bundle)", GUILayout.Height(45)))
        {
            if (Application.isPlaying) EditorUtility.DisplayDialog("错误 (Error)", "打包资产必须在编辑模式下进行！\n请先停止运行游戏！", "确定 (OK)");
            else if (targetFolder == null) EditorUtility.DisplayDialog("错误 (Error)", "请先选择一个资产文件夹！\nPlease select an asset folder first!", "确定 (OK)");
            else if (string.IsNullOrEmpty(customSpeciesID)) EditorUtility.DisplayDialog("错误 (Error)", "请填写一个有效的唯一ID！\nPlease provide a valid Unique ID!", "确定 (OK)");
            else EditorApplication.delayCall += () => { ProcessImportOnly(); };
        }
        GUI.backgroundColor = Color.white;
        GUILayout.Space(20);

        // ==========================================
        // 🚀 第二步：打开上传界面
        // ==========================================
        GUILayout.Space(10);
        EditorGUILayout.HelpBox("完成第一步后，点击 Unity 顶部的 ▶ Play 按钮运行游戏。然后再点击下方按钮打开上传器。", MessageType.Warning);
        
        GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
        // 【核心修改点】：按钮不再直接上传，而是呼出分离出去的 WenwanWorkshopUploader 窗口
        if (GUILayout.Button("☁️ 第二步：打开 Steam 上传工具 (Open Steam Uploader)", GUILayout.Height(45)))
        {
            WenwanWorkshopUploader.OpenForUpload(customSpeciesID);
            if (autoCloseAfterImport) this.Close();
        }
        GUI.backgroundColor = Color.white;
        GUILayout.Space(20);
        
        EditorGUILayout.EndScrollView();
    }

    // --- UI 渲染辅助方法 ---
    private bool DrawFoldoutHeader(string icon, string cn, string en, bool foldout) { GUIStyle style = new GUIStyle(EditorStyles.foldoutHeader) { richText = true }; return EditorGUILayout.Foldout(foldout, $"{icon} {cn} <color=#888888>{en}</color>", true, style); }
    private void ApplyDifficultyPreset() { switch (currentDifficulty) { case DifficultyPreset.高难度_盘玩慢_Hard: evoKeyboard = 0.00002f; evoClick = 0.00002f; evoScroll = 0.00002f; evoSpeed = 0.5f; break; case DifficultyPreset.中难度_盘玩中_Normal: evoKeyboard = 0.0001f; evoClick = 0.0001f; evoScroll = 0.0001f; evoSpeed = 1.0f; break; case DifficultyPreset.低难度_盘玩快_Easy: evoKeyboard = 0.0005f; evoClick = 0.0005f; evoScroll = 0.0005f; evoSpeed = 3.0f; break; } }
    private string DrawDualTextField(string cn, string en, string val) { GUILayout.BeginHorizontal(); GUILayout.BeginVertical(GUILayout.Width(EditorGUIUtility.labelWidth)); GUILayout.Label(cn, GUILayout.Height(16)); GUILayout.Label($"<color=#888888>{en}</color>", new GUIStyle(EditorStyles.miniLabel) { richText = true }, GUILayout.Height(12)); GUILayout.EndVertical(); val = EditorGUILayout.TextField(val, GUILayout.Height(28)); GUILayout.EndHorizontal(); return val; }
    private bool DrawDualToggle(string cn, string en, bool val) { GUILayout.BeginHorizontal(); val = EditorGUILayout.Toggle(val, GUILayout.Width(20)); GUILayout.BeginVertical(); GUILayout.Label(cn, GUILayout.Height(16)); GUILayout.Label($"<color=#888888>{en}</color>", new GUIStyle(EditorStyles.miniLabel) { richText = true }, GUILayout.Height(12)); GUILayout.EndVertical(); GUILayout.EndHorizontal(); return val; }
    private DefaultAsset DrawDualObjectField(string cn, string en, DefaultAsset val) { GUILayout.BeginHorizontal(); GUILayout.BeginVertical(GUILayout.Width(EditorGUIUtility.labelWidth)); GUILayout.Label(cn, GUILayout.Height(16)); GUILayout.Label($"<color=#888888>{en}</color>", new GUIStyle(EditorStyles.miniLabel) { richText = true }, GUILayout.Height(12)); GUILayout.EndVertical(); val = (DefaultAsset)EditorGUILayout.ObjectField(val, typeof(DefaultAsset), false, GUILayout.Height(28)); GUILayout.EndHorizontal(); return val; }
    private Texture DrawDualTextureField(string cn, string en, Texture val) { GUILayout.BeginHorizontal(); GUILayout.BeginVertical(GUILayout.Width(EditorGUIUtility.labelWidth)); GUILayout.Label(cn, GUILayout.Height(16)); GUILayout.Label($"<color=#888888>{en}</color>", new GUIStyle(EditorStyles.miniLabel) { richText = true }, GUILayout.Height(12)); GUILayout.EndVertical(); val = (Texture)EditorGUILayout.ObjectField(val, typeof(Texture), false, GUILayout.Width(64), GUILayout.Height(64));GUILayout.EndHorizontal(); return val; }
    private int DrawDualIntField(string cn, string en, int val) { GUILayout.BeginHorizontal(); GUILayout.BeginVertical(GUILayout.Width(EditorGUIUtility.labelWidth)); GUILayout.Label(cn, GUILayout.Height(16)); GUILayout.Label($"<color=#888888>{en}</color>", new GUIStyle(EditorStyles.miniLabel) { richText = true }, GUILayout.Height(12)); GUILayout.EndVertical(); val = EditorGUILayout.IntField(val, GUILayout.Height(28)); GUILayout.EndHorizontal(); return val; }
    private float DrawDualFloat(string cn, string en, float val) { GUILayout.BeginHorizontal(); GUILayout.BeginVertical(GUILayout.Width(EditorGUIUtility.labelWidth)); GUILayout.Label(cn, GUILayout.Height(16)); GUILayout.Label($"<color=#888888>{en}</color>", new GUIStyle(EditorStyles.miniLabel) { richText = true }, GUILayout.Height(12)); GUILayout.EndVertical(); val = EditorGUILayout.FloatField(val, GUILayout.Height(28)); GUILayout.EndHorizontal(); return val; }
    private float DrawDualSlider(string cn, string en, float val, float min, float max) { GUILayout.BeginHorizontal(); GUILayout.BeginVertical(GUILayout.Width(EditorGUIUtility.labelWidth)); GUILayout.Label(cn, GUILayout.Height(16)); GUILayout.Label($"<color=#888888>{en}</color>", new GUIStyle(EditorStyles.miniLabel) { richText = true }, GUILayout.Height(12)); GUILayout.EndVertical(); val = GUILayout.HorizontalSlider(val, min, max, GUILayout.ExpandWidth(true), GUILayout.Height(28)); val = EditorGUILayout.FloatField(val, GUILayout.Width(50), GUILayout.Height(28)); GUILayout.EndHorizontal(); return val; }
    private Color DrawDualColor(string cn, string en, Color val) { GUILayout.BeginHorizontal(); GUILayout.BeginVertical(GUILayout.Width(EditorGUIUtility.labelWidth)); GUILayout.Label(cn, GUILayout.Height(16)); GUILayout.Label($"<color=#888888>{en}</color>", new GUIStyle(EditorStyles.miniLabel) { richText = true }, GUILayout.Height(12)); GUILayout.EndVertical(); val = EditorGUILayout.ColorField(val, GUILayout.Height(28)); GUILayout.EndHorizontal(); return val; }
    private AnimationCurve DrawDualCurve(string cn, string en, AnimationCurve val) { GUILayout.BeginHorizontal(); GUILayout.BeginVertical(GUILayout.Width(EditorGUIUtility.labelWidth)); GUILayout.Label(cn, GUILayout.Height(16)); GUILayout.Label($"<color=#888888>{en}</color>", new GUIStyle(EditorStyles.miniLabel) { richText = true }, GUILayout.Height(12)); GUILayout.EndVertical(); val = EditorGUILayout.CurveField(val, GUILayout.Height(28)); GUILayout.EndHorizontal(); return val; }
    private DifficultyPreset DrawDualEnum(string cn, string en, DifficultyPreset val) { GUILayout.BeginHorizontal(); GUILayout.BeginVertical(GUILayout.Width(EditorGUIUtility.labelWidth)); GUILayout.Label(cn, GUILayout.Height(16)); GUILayout.Label($"<color=#888888>{en}</color>", new GUIStyle(EditorStyles.miniLabel) { richText = true }, GUILayout.Height(12)); GUILayout.EndVertical(); val = (DifficultyPreset)EditorGUILayout.EnumPopup(val, GUILayout.Height(28)); GUILayout.EndHorizontal(); return val; }

    // --- 外部文件导入逻辑 ---
    private void ImportExternalFolder(string path)
    {
        string folderName = new DirectoryInfo(path).Name.ToLower();
        string targetUnityPath = $"Assets/Art/WorkshopRaw/{folderName}";
        if (!Directory.Exists("Assets/Art/WorkshopRaw")) Directory.CreateDirectory("Assets/Art/WorkshopRaw");
        CopyDirectory(path, targetUnityPath, true);
        AssetDatabase.Refresh();
        targetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(targetUnityPath);
        Debug.Log($"[究极导入器] 外部文件已拷贝至 {targetUnityPath}");
    }

    private void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
    {
        var dir = new DirectoryInfo(sourceDir);
        DirectoryInfo[] dirs = dir.GetDirectories();
        Directory.CreateDirectory(destinationDir);
        foreach (FileInfo file in dir.GetFiles()) { if (file.Extension.ToLower() == ".meta") continue; file.CopyTo(Path.Combine(destinationDir, file.Name), true); }
        if (recursive) foreach (DirectoryInfo subDir in dirs) CopyDirectory(subDir.FullName, Path.Combine(destinationDir, subDir.Name), true);
    }

    private void ProcessImportOnly()
    {
        string folderPath = AssetDatabase.GetAssetPath(targetFolder);
        string folderName = Path.GetFileName(folderPath);
        string speciesID = customSpeciesID; 

        try
        {
            EditorUtility.DisplayProgressBar("Asset Import", "正在初始化目录结构 (Initializing Directories)...", 0.1f);
            EnsureDirectories();

            string[] fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { folderPath });
            if (fbxGuids.Length == 0) { Debug.LogError($"[究极导入器] 在 {folderPath} 未找到 FBX 模型！"); EditorUtility.ClearProgressBar(); return; }
            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(fbxGuids[0]));

            EditorUtility.DisplayProgressBar("Asset Import", "正在处理UI图标 (Processing UI Icon)...", 0.3f);
            Sprite speciesIcon = ProcessAndLoadIcon(folderPath);

            EditorUtility.DisplayProgressBar("Asset Import", "正在生成材质球与贴图 (Generating Materials & Textures)...", 0.5f);
            Material newMat = ProcessTexturesAndCreateMaterial(folderPath, speciesID);

            EditorUtility.DisplayProgressBar("Asset Import", "正在构建预制体 (Building Prefab)...", 0.7f);
            GameObject prefab = CreateAndProcessPrefab(fbxAsset, speciesID, newMat);
            
            EditorUtility.DisplayProgressBar("Asset Import", "正在生成配置文件 (Generating Config)...", 0.8f);
            WenwanSpeciesConfig newConfig = CreateConfig(folderName, speciesID, prefab, newMat, speciesIcon);

            if (autoRegisterToManager && newConfig != null) RegisterToSpeciesManager(newConfig);

            EditorUtility.DisplayProgressBar("Asset Import", "正在打包 AssetBundle...", 0.9f);
            string configPath = AssetDatabase.GetAssetPath(newConfig);
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            BuildAssetBundleOnly(speciesID, configPath, prefabPath);

            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("生成成功 (Success)", $"资产 [{speciesID}] 已成功生成并完成打包！\n如果你需要上传创意工坊，请先点击顶部的 Play 运行游戏，然后执行第二步。", "确定 (OK)");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[究极导入器] 导入过程发生异常: {ex.Message}\n{ex.StackTrace}");
            EditorUtility.DisplayDialog("导入失败 (Failed)", "导入过程发生错误，请查看 Console 报错信息。", "确定 (OK)");
            EditorUtility.ClearProgressBar();
        }
    }

    private void BuildAssetBundleOnly(string modID, string configPath, string prefabPath)
    {
        string exportDir = $"WorkshopExportStaging/{modID}";
        if (!Directory.Exists(exportDir)) Directory.CreateDirectory(exportDir);

        AssetBundleBuild[] buildMap = new AssetBundleBuild[1];
        buildMap[0].assetBundleName = $"{modID}.bundle";
        buildMap[0].assetNames = new string[] { configPath, prefabPath };

        BuildPipeline.BuildAssetBundles(exportDir, buildMap, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);
        Debug.Log($"[究极导入器] 资产已打包为 AssetBundle: {exportDir}/{modID}.bundle");
    }

    private void EnsureDirectories()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
        if (!AssetDatabase.IsValidFolder("Assets/Scripts")) AssetDatabase.CreateFolder("Assets", "Scripts");
        if (!AssetDatabase.IsValidFolder("Assets/Art/Prefabs")) AssetDatabase.CreateFolder("Assets/Art", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Art/Materials")) AssetDatabase.CreateFolder("Assets/Art", "Materials");
        if (!AssetDatabase.IsValidFolder("Assets/Scripts/Configs")) AssetDatabase.CreateFolder("Assets/Scripts", "Configs");
    }

    private Sprite ProcessAndLoadIcon(string folderPath)
    {
        string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
        foreach (string guid in texGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.ToLower().Contains("_icon"))
            {
                if (autoSetIconToSprite)
                {
                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null && (importer.textureType != TextureImporterType.Sprite || !importer.alphaIsTransparency))
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        importer.alphaIsTransparency = true; 
                        importer.SaveAndReimport();
                    }
                }
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
        }
        return null;
    }

    private Material ProcessTexturesAndCreateMaterial(string folderPath, string speciesID)
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
        Texture2D texA = null, texB = null, normal = null, mask = null, metallic = null;

        foreach (string guid in guids)
        {
            string texPath = AssetDatabase.GUIDToAssetPath(guid);
            string lowerPath = texPath.ToLower();
            if (lowerPath.Contains("_icon")) continue;
            TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer == null) continue;

            bool needReimport = false;
            if (lowerPath.Contains("_texa")) { texA = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath); if (!importer.sRGBTexture) { importer.sRGBTexture = true; needReimport = true; } }
            else if (lowerPath.Contains("_texb")) { texB = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath); if (!importer.sRGBTexture) { importer.sRGBTexture = true; needReimport = true; } }
            else if (lowerPath.Contains("_normal")) { normal = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath); if (autoFixNormalMap && importer.textureType != TextureImporterType.NormalMap) { importer.textureType = TextureImporterType.NormalMap; needReimport = true; } }
            else if (lowerPath.Contains("_mask")) { mask = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath); if (autoFixLinearTexture && importer.sRGBTexture) { importer.sRGBTexture = false; needReimport = true; } }
            else if (lowerPath.Contains("_metallic")) { metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath); if (autoFixLinearTexture && importer.sRGBTexture) { importer.sRGBTexture = false; needReimport = true; } }

            if (needReimport) importer.SaveAndReimport();
        }

        if (autoGenerateMissingTextures)
        {
            if (texA == null && texB != null) texA = GenerateTextureFrom(texB, folderPath, speciesID, "_TexA", (color) => { float h, s, v; Color.RGBToHSV(color, out h, out s, out v); return Color.HSVToRGB(h, Mathf.Clamp01(s * genTexASaturation), Mathf.Clamp01(v * genTexAValue)); });
            if (mask == null) { Texture2D baseColorTex = texB != null ? texB : texA; if (baseColorTex != null) mask = GenerateTextureFrom(baseColorTex, folderPath, speciesID, "_Mask", (color) => { float gray = color.grayscale; gray = gray > 0.5f ? Mathf.Clamp01(gray + genMaskContrast) : Mathf.Clamp01(gray - genMaskContrast); return new Color(gray, gray, gray, 1f); }); }
        }

        string matPath = $"Assets/Art/Materials/{speciesID}_Mat.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            Shader shader = Shader.Find("Shader Graphs/WenwanEvolution");
            if (shader == null) { Debug.LogError("[究极导入器] 找不到 Shader Graphs/WenwanEvolution！"); return null; }
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
        }

        if (texA != null) { mat.SetTexture("_TexA_Base", texA); mat.SetTexture("TexA_Base", texA); }
        if (texB != null) { mat.SetTexture("_TexB_Base", texB); mat.SetTexture("TexB_Base", texB); }
        if (normal != null) { mat.SetTexture("_NormalMap", normal); mat.SetTexture("NormalMap", normal); }
        if (mask != null) { mat.SetTexture("_MaskMap", mask); mat.SetTexture("MaskMap", mask); }
        if (metallic != null) { mat.SetTexture("_MetallicMap", metallic); mat.SetTexture("MetallicMap", metallic); }
        if (texB != null) { mat.SetTexture("_OcclusionMap", texB); mat.SetTexture("OcclusionMap", texB); }

        mat.SetColor("_ColorA_Tint", colorATint); mat.SetColor("ColorA_Tint", colorATint);
        mat.SetColor("_ColorB_Tint", colorBTint); mat.SetColor("ColorB_Tint", colorBTint);
        mat.SetFloat("_Occlusion_Strength", occlusionStrength); mat.SetFloat("Occlusion_Strength", occlusionStrength);
        mat.SetColor("_JadeColor", jadeColor); mat.SetColor("Jade Color", jadeColor); mat.SetColor("_Jade_Color", jadeColor); 
        mat.SetFloat("_Normal_Strength", normalStrength); mat.SetFloat("Normal_Strength", normalStrength);
        mat.SetFloat("_Softness", softness); mat.SetFloat("Softness", softness);
        mat.SetFloat("_DustAmount", dustAmount); mat.SetFloat("DustAmount", dustAmount);
        mat.SetColor("_DustColor", dustColor); mat.SetColor("DustColor", dustColor);

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private Texture2D GenerateTextureFrom(Texture2D source, string folderPath, string baseName, string suffix, System.Func<Color, Color> pixelProcessor)
    {
        string sourcePath = AssetDatabase.GetAssetPath(source);
        TextureImporter importer = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
        
        bool wasReadable = importer.isReadable;
        if (!wasReadable) { importer.isReadable = true; importer.SaveAndReimport(); }

        Color[] pixels = source.GetPixels();
        for (int i = 0; i < pixels.Length; i++) pixels[i] = pixelProcessor(pixels[i]);

        Texture2D newTex = new Texture2D(source.width, source.height);
        newTex.SetPixels(pixels);
        newTex.Apply();

        string newPath = $"{folderPath}/{baseName}{suffix}.png";
        byte[] bytes = newTex.EncodeToPNG();
        File.WriteAllBytes(newPath, bytes);
        AssetDatabase.ImportAsset(newPath);

        TextureImporter newImporter = AssetImporter.GetAtPath(newPath) as TextureImporter;
        newImporter.sRGBTexture = suffix != "_Mask"; 
        newImporter.SaveAndReimport();

        if (!wasReadable) { importer.isReadable = false; importer.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(newPath);
    }

    private GameObject CreateAndProcessPrefab(GameObject fbxAsset, string speciesID, Material mat)
    {
        GameObject instance = (GameObject)Object.Instantiate(fbxAsset);
        GameObject root = new GameObject(speciesID + "_Prefab");
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        int childCount = instance.transform.childCount;
        if (childCount == 0 && instance.GetComponent<MeshFilter>() != null)
        {
            instance.transform.SetParent(root.transform);
            instance.transform.localPosition = Vector3.zero;
        }
        else
        {
            Transform[] children = new Transform[childCount];
            for (int i = 0; i < childCount; i++) children[i] = instance.transform.GetChild(i);
            foreach (Transform child in children) child.SetParent(root.transform);
            DestroyImmediate(instance); 
        }

        if (autoAddReflectionProbe)
        {
            GameObject probeObj = new GameObject("ReflectionProbe");
            probeObj.transform.SetParent(root.transform);
            probeObj.transform.localPosition = Vector3.zero;
            ReflectionProbe probe = probeObj.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Custom;
            probe.size = new Vector3(1000, 1000, 1000); 
            if (customReflectionTexture != null) probe.customBakedTexture = customReflectionTexture;
        }

        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>();
        foreach (MeshFilter mf in meshFilters)
        {
            GameObject obj = mf.gameObject;
            if (obj.GetComponent<Animator>()) DestroyImmediate(obj.GetComponent<Animator>());
            if (obj.GetComponent<Rigidbody>()) DestroyImmediate(obj.GetComponent<Rigidbody>());

            if (autoBakeVertexAO) BakeVertexAO(obj, speciesID);

            MeshCollider mc = obj.GetComponent<MeshCollider>();
            if (mc == null) mc = obj.AddComponent<MeshCollider>();
            
            if (mf.sharedMesh != null)
            {
                Mesh lowPolyColMesh = CreateLowPolyCollisionMesh(mf.sharedMesh.bounds);
                string colMeshPath = AssetDatabase.GenerateUniqueAssetPath($"Assets/Art/Prefabs/{speciesID}_{obj.name}_Col.asset");
                AssetDatabase.CreateAsset(lowPolyColMesh, colMeshPath);
                mc.sharedMesh = lowPolyColMesh; 
            }
            mc.convex = true; 

            MeshRenderer mr = obj.GetComponent<MeshRenderer>();
            if (mr != null && mat != null) mr.sharedMaterial = mat;
        }

        string prefabPath = $"Assets/Art/Prefabs/{speciesID}.prefab";
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        DestroyImmediate(root); 
        return savedPrefab;
    }

    private Mesh CreateLowPolyCollisionMesh(Bounds bounds)
    {
        Mesh mesh = new Mesh();
        mesh.name = "LowPolyCollider";
        float t = (1.0f + Mathf.Sqrt(5.0f)) / 2.0f;
        Vector3[] vertices = new Vector3[] { new Vector3(-1, t, 0).normalized, new Vector3( 1, t, 0).normalized, new Vector3(-1, -t, 0).normalized, new Vector3( 1, -t, 0).normalized, new Vector3( 0, -1, t).normalized, new Vector3( 0, 1, t).normalized, new Vector3( 0, -1, -t).normalized, new Vector3( 0, 1, -t).normalized, new Vector3( t, 0, -1).normalized, new Vector3( t, 0, 1).normalized, new Vector3(-t, 0, -1).normalized, new Vector3(-t, 0, 1).normalized };
        for (int i = 0; i < vertices.Length; i++) vertices[i] = new Vector3(vertices[i].x * bounds.extents.x, vertices[i].y * bounds.extents.y, vertices[i].z * bounds.extents.z) + bounds.center;
        mesh.vertices = vertices;
        mesh.triangles = new int[] { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8, 3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
        mesh.RecalculateNormals();
        return mesh;
    }

    private void BakeVertexAO(GameObject go, string speciesID)
    {
        MeshFilter mf = go.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        Mesh mesh = mf.sharedMesh;
        Mesh newMesh = Object.Instantiate(mesh);
        newMesh.name = mesh.name + "_AO";
        string meshPath = AssetDatabase.GenerateUniqueAssetPath($"Assets/Art/Prefabs/{speciesID}_{go.name}_Mesh.asset");
        AssetDatabase.CreateAsset(newMesh, meshPath);
        mf.sharedMesh = newMesh;

        Vector3[] vertices = newMesh.vertices;
        Vector3[] normals = newMesh.normals;
        Color[] colors = new Color[vertices.Length];
        int originalLayer = go.layer;
        int tempLayer = 31; 
        go.layer = tempLayer;
        MeshCollider tempCollider = go.AddComponent<MeshCollider>();
        tempCollider.sharedMesh = newMesh;
        tempCollider.convex = false;
        Physics.SyncTransforms();

        int layerMask = 1 << tempLayer; 
        Vector3[] dirs = new Vector3[aoSamples];
        float phi = Mathf.PI * (3f - Mathf.Sqrt(5f)); 
        for (int i = 0; i < aoSamples; i++) { float y = 1f - (i / (float)(aoSamples - 1)); float r = Mathf.Sqrt(1f - y * y); float theta = phi * i; dirs[i] = new Vector3(Mathf.Cos(theta) * r, y, Mathf.Sin(theta) * r); }
        for (int i = 0; i < vertices.Length; i++) {
            if (i % 500 == 0) EditorUtility.DisplayProgressBar("Baking Vertex AO", $"计算光线追踪遮挡中... {i}/{vertices.Length}", (float)i / vertices.Length);
            Vector3 worldPos = go.transform.TransformPoint(vertices[i]);
            Vector3 worldNormal = go.transform.TransformDirection(normals[i]).normalized;
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, worldNormal);
            float hits = 0;
            for (int j = 0; j < aoSamples; j++) { Vector3 rayDir = rot * dirs[j]; if (Physics.Raycast(worldPos + worldNormal * 0.0001f, rayDir, aoRadius, layerMask)) hits++; }
            float rawAO = 1.0f - (hits / aoSamples);
            float finalAO = Mathf.Lerp(1.0f, rawAO, aoIntensity);
            colors[i] = new Color(finalAO, finalAO, finalAO, 1f); 
        }
        newMesh.colors = colors;
        EditorUtility.SetDirty(newMesh);
        AssetDatabase.SaveAssets();
        DestroyImmediate(tempCollider);
        go.layer = originalLayer; 
    }

    private WenwanSpeciesConfig CreateConfig(string folderName, string speciesID, GameObject prefab, Material mat, Sprite icon)
    {
        string configPath = $"Assets/Scripts/Configs/{speciesID}_Config.asset";
        WenwanSpeciesConfig config = AssetDatabase.LoadAssetAtPath<WenwanSpeciesConfig>(configPath);
        
        if (config == null) { config = ScriptableObject.CreateInstance<WenwanSpeciesConfig>(); AssetDatabase.CreateAsset(config, configPath); }
        SerializedObject serializedConfig = new SerializedObject(config);

        serializedConfig.FindProperty("speciesID").stringValue = speciesID;
        serializedConfig.FindProperty("speciesName").stringValue = folderName;
        serializedConfig.FindProperty("speciesMaterial").objectReferenceValue = mat;
        if (icon != null) serializedConfig.FindProperty("speciesIcon").objectReferenceValue = icon;

        int actualMeshCount = prefab.GetComponentsInChildren<MeshFilter>().Length;
        int effectiveMeshCount = manualMeshCountOverride > 0 ? manualMeshCountOverride : actualMeshCount;
        SerializedProperty displayModeProp = serializedConfig.FindProperty("displayMode");

        if (effectiveMeshCount == 1) { serializedConfig.FindProperty("singlePrefab").objectReferenceValue = prefab; SetEnumByNameSafe(displayModeProp, "Single"); }
        else if (effectiveMeshCount == 2) { serializedConfig.FindProperty("dualPrefab").objectReferenceValue = prefab; SetEnumByNameSafe(displayModeProp, "Dual"); }
        else if (effectiveMeshCount >= 3) { serializedConfig.FindProperty("stringPrefab").objectReferenceValue = prefab; SetEnumByNameSafe(displayModeProp, "String"); }

        serializedConfig.FindProperty("colorCurve").animationCurveValue = colorCurve;
        serializedConfig.FindProperty("glossinessCurve").animationCurveValue = glossinessCurve;
        serializedConfig.FindProperty("evolutionKeyboardWeight").floatValue = evoKeyboard;
        serializedConfig.FindProperty("evolutionClickWeight").floatValue = evoClick;
        serializedConfig.FindProperty("evolutionScrollWeight").floatValue = evoScroll;
        serializedConfig.FindProperty("evolutionSpeedMultiplier").floatValue = evoSpeed;
        serializedConfig.FindProperty("physicsKeyboardWeight").floatValue = physKeyboard;
        serializedConfig.FindProperty("physicsClickWeight").floatValue = physClick;
        serializedConfig.FindProperty("physicsScrollWeight").floatValue = physScroll;
        serializedConfig.FindProperty("physicsSpeedMultiplier").floatValue = physSpeed;
        serializedConfig.FindProperty("rotationFriction").floatValue = rotationFriction;
        serializedConfig.FindProperty("hoursToMaxDust").floatValue = hoursToMaxDust;
        serializedConfig.FindProperty("dustCleanPerInput").floatValue = dustCleanPerInput;
        serializedConfig.FindProperty("isWorkshopItem").boolValue = isWorkshopItem;

        serializedConfig.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        return config;
    }

    private void SetEnumByNameSafe(SerializedProperty prop, string targetName)
    {
        if (prop == null || prop.propertyType != SerializedPropertyType.Enum) return;
        for (int i = 0; i < prop.enumNames.Length; i++) { if (prop.enumNames[i].IndexOf(targetName, System.StringComparison.OrdinalIgnoreCase) >= 0) { prop.enumValueIndex = i; return; } }
    }

    private void RegisterToSpeciesManager(WenwanSpeciesConfig newConfig)
    {
        DoRegister(newConfig);
        Debug.Log($"<color=green>[究极导入器] 已将 {newConfig.speciesID} 临时注册到场景中预览！</color>");
        if (Application.isPlaying) { string path = AssetDatabase.GetAssetPath(newConfig); string existing = EditorPrefs.GetString("CyberWenwan_PendingConfigs", ""); if (!existing.Contains(path)) { existing += path + ";"; EditorPrefs.SetString("CyberWenwan_PendingConfigs", existing); } }
    }

    public static bool DoRegister(WenwanSpeciesConfig newConfig)
    {
        bool isChanged = false;
        MonoBehaviour speciesManager = null;
        Object[] allObjects = Resources.FindObjectsOfTypeAll(typeof(MonoBehaviour));
        foreach (Object obj in allObjects) { if (obj.GetType().Name == "SpeciesManager") { GameObject go = ((MonoBehaviour)obj).gameObject; if (go.scene.IsValid()) { speciesManager = (MonoBehaviour)obj; break; } } }
        if (speciesManager != null) { SerializedObject so = new SerializedObject(speciesManager); SerializedProperty prop = so.FindProperty("allSpecies"); if (prop != null && prop.isArray) { bool exists = false; for (int i = 0; i < prop.arraySize; i++) { if (prop.GetArrayElementAtIndex(i).objectReferenceValue == newConfig) { exists = true; break; } } if (!exists) { prop.InsertArrayElementAtIndex(0); prop.GetArrayElementAtIndex(0).objectReferenceValue = newConfig; so.ApplyModifiedProperties(); isChanged = true; StoreManager storeManager = Object.FindObjectOfType<StoreManager>(); if (storeManager != null) { storeManager.SyncFromSpeciesManager(); EditorUtility.SetDirty(storeManager); } EditorUtility.SetDirty(speciesManager); } } }
        return isChanged;
    }
}

[InitializeOnLoad]
public static class WenwanPlayModeRestorer
{
    static WenwanPlayModeRestorer() { EditorApplication.playModeStateChanged += OnPlayModeStateChanged; }
    private static void OnPlayModeStateChanged(PlayModeStateChange state) { if (state == PlayModeStateChange.EnteredEditMode) { string pending = EditorPrefs.GetString("CyberWenwan_PendingConfigs", ""); if (!string.IsNullOrEmpty(pending)) { string[] paths = pending.Split(new char[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries); bool sceneChanged = false; foreach (string path in paths) { WenwanSpeciesConfig config = AssetDatabase.LoadAssetAtPath<WenwanSpeciesConfig>(path); if (config != null) { if (WenwanUltimateImporter.DoRegister(config)) { sceneChanged = true; } } } if (sceneChanged) { var activeScene = EditorSceneManager.GetActiveScene(); EditorSceneManager.MarkSceneDirty(activeScene); EditorSceneManager.SaveScene(activeScene); Debug.Log("<color=yellow>[时空魔法生效]</color> <color=green>已自动将你在运行模式下导入的文玩同步回场景，并完成了永久保存！</color>"); } EditorPrefs.DeleteKey("CyberWenwan_PendingConfigs"); } } }
}
#endif