using UnityEngine;
using UnityEditor;

public class WenwanMaterialBatchEditor : EditorWindow
{
    // --- 材质属性黑板引用名 (Shader Graph Reference Names) ---
    private const string PROP_JADE_COLOR = "_Jade_Color";
    private const string PROP_SMOOTHA = "_SmoothA";
    private const string PROP_SMOOTHB = "_SmoothB";
    private const string PROP_SOFTNESS = "_Softness";
    private const string PROP_DUST_AMOUNT = "_DustAmount";
    private const string PROP_DUST_COLOR = "_DustColor";
    private const string PROP_DUST_SPREAD = "_DustSpread";
    private const string PROP_COLORA_TINT = "_ColorA_Tint";
    private const string PROP_COLORB_TINT = "_ColorB_Tint";
    private const string PROP_NORMAL_STRENGTH = "_Normal_Strength";
    private const string PROP_OCCLUSION_STRENGTH = "_Occlusion_Strength";
    private const string PROP_JADE_PROGRESS = "_JadeProgress";

    // --- UI 变量 ---
    private Color jadeColor = new Color(0.3f, 0f, 0f);
    private float smoothA = 0.2f;
    private float smoothB = 0.9f;
    private float softness = 0.5f;
    private float dustAmount = 0f;
    private Color dustColor = Color.gray;
    private float dustSpread = 3f;
    private Color colorATint = Color.white;
    private Color colorBTint = Color.white;
    private float normalStrength = 0.25f;
    private float occlusionStrength = 0.6f;
    private float jadeProgress = 0f;

    // --- 应用开关 (勾选才应用) ---
    private bool applyJadeColor, applySmoothA, applySmoothB, applySoftness;
    private bool applyDustAmount, applyDustColor, applyDustSpread;
    private bool applyColorATint, applyColorBTint, applyNormalStrength;
    private bool applyOcclusionStrength, applyJadeProgress;

    private Vector2 scrollPos;

    public static void ShowWindow()
    {
        GetWindow<WenwanMaterialBatchEditor>("批量材质调整");
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("1. 在 Project 面板中按住 Ctrl 选中多个材质球。\n2. 勾选左侧选框以启用需要同步的属性。\n3. 点击底部应用按钮。", EditorStyles.helpBox);
        GUILayout.Space(10);

        scrollPos = GUILayout.BeginScrollView(scrollPos);

        DrawPropertyRow("Jade Color", ref applyJadeColor, ref jadeColor);
        DrawPropertyRow("SmoothA", ref applySmoothA, ref smoothA, 0f, 1f);
        DrawPropertyRow("SmoothB", ref applySmoothB, ref smoothB, 0f, 1f);
        DrawPropertyRow("Softness", ref applySoftness, ref softness, 0f, 1f);
        DrawPropertyRow("DustAmount", ref applyDustAmount, ref dustAmount, 0f, 1f);
        DrawPropertyRow("DustColor", ref applyDustColor, ref dustColor);
        DrawPropertyRow("DustSpread", ref applyDustSpread, ref dustSpread);
        DrawPropertyRow("ColorA_Tint", ref applyColorATint, ref colorATint);
        DrawPropertyRow("ColorB_Tint", ref applyColorBTint, ref colorBTint);
        DrawPropertyRow("Normal_Strength", ref applyNormalStrength, ref normalStrength);
        DrawPropertyRow("Occlusion_Strength", ref applyOcclusionStrength, ref occlusionStrength, 0f, 1f);
        DrawPropertyRow("JadeProgress", ref applyJadeProgress, ref jadeProgress, 0f, 1f);

        GUILayout.EndScrollView();

        GUILayout.Space(10);

        if (GUILayout.Button("应用到选中的材质球 (Apply to Selected)", GUILayout.Height(40)))
        {
            ApplyToSelectedMaterials();
        }
        
        GUILayout.Space(5);
        
        if (GUILayout.Button("排错：打印选中材质的真实属性名 (Print Reference Names)", GUILayout.Height(30)))
        {
            PrintMaterialProperties();
        }
    }

    private void DrawPropertyRow(string label, ref bool toggle, ref Color val)
    {
        GUILayout.BeginHorizontal();
        toggle = GUILayout.Toggle(toggle, "", GUILayout.Width(20));
        EditorGUI.BeginDisabledGroup(!toggle);
        val = EditorGUILayout.ColorField(label, val);
        EditorGUI.EndDisabledGroup();
        GUILayout.EndHorizontal();
    }

    private void DrawPropertyRow(string label, ref bool toggle, ref float val, float min = float.NaN, float max = float.NaN)
    {
        GUILayout.BeginHorizontal();
        toggle = GUILayout.Toggle(toggle, "", GUILayout.Width(20));
        EditorGUI.BeginDisabledGroup(!toggle);
        if (!float.IsNaN(min) && !float.IsNaN(max))
            val = EditorGUILayout.Slider(label, val, min, max);
        else
            val = EditorGUILayout.FloatField(label, val);
        EditorGUI.EndDisabledGroup();
        GUILayout.EndHorizontal();
    }

    private void ApplyToSelectedMaterials()
    {
        Object[] selectedObjects = Selection.GetFiltered(typeof(Material), SelectionMode.DeepAssets);
        if (selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "请先在 Project 面板中选中至少一个材质球！", "确定");
            return;
        }

        int count = 0;
        foreach (Object obj in selectedObjects)
        {
            Material mat = obj as Material;
            if (mat != null)
            {
                Undo.RecordObject(mat, "Batch Edit Materials");

                if (applyJadeColor) mat.SetColor(PROP_JADE_COLOR, jadeColor);
                if (applySmoothA) mat.SetFloat(PROP_SMOOTHA, smoothA);
                if (applySmoothB) mat.SetFloat(PROP_SMOOTHB, smoothB);
                if (applySoftness) mat.SetFloat(PROP_SOFTNESS, softness);
                if (applyDustAmount) mat.SetFloat(PROP_DUST_AMOUNT, dustAmount);
                if (applyDustColor) mat.SetColor(PROP_DUST_COLOR, dustColor);
                if (applyDustSpread) mat.SetFloat(PROP_DUST_SPREAD, dustSpread);
                if (applyColorATint) mat.SetColor(PROP_COLORA_TINT, colorATint);
                if (applyColorBTint) mat.SetColor(PROP_COLORB_TINT, colorBTint);
                if (applyNormalStrength) mat.SetFloat(PROP_NORMAL_STRENGTH, normalStrength);
                if (applyOcclusionStrength) mat.SetFloat(PROP_OCCLUSION_STRENGTH, occlusionStrength);
                if (applyJadeProgress) mat.SetFloat(PROP_JADE_PROGRESS, jadeProgress);

                EditorUtility.SetDirty(mat);
                count++;
            }
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("完成", $"成功将选定属性应用到 {count} 个材质球上！", "确定");
    }

    private void PrintMaterialProperties()
    {
        Object[] selectedObjects = Selection.GetFiltered(typeof(Material), SelectionMode.DeepAssets);
        if (selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "请先选中一个材质球！", "确定");
            return;
        }
        
        Material mat = selectedObjects[0] as Material;
        Shader shader = mat.shader;
        int propertyCount = shader.GetPropertyCount();
        string log = $"【{mat.name}】的真实属性名(请用右侧替换脚本顶部的常量)：\n\n";
        
        for (int i = 0; i < propertyCount; i++)
        {
            string refName = shader.GetPropertyName(i);
            string displayName = shader.GetPropertyDescription(i);
            log += $"面板名: [{displayName}]  =>  代码引用名: \"{refName}\"\n";
        }
        
        Debug.Log(log);
    }
}