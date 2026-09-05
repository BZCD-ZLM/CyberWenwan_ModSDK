using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class WenwanConfigAssigner : EditorWindow
{
    // 已经将工具路径挪到了 Assets/CyberWenwan 下
    public static void AutoAssignConfigs()
    {
        // 1. 找到场景中的 SpeciesManager 实例
        SpeciesManager manager = FindObjectOfType<SpeciesManager>();
        if (manager == null)
        {
            Debug.LogError("当前场景中未找到 SpeciesManager！请确保当前处于 SampleScene，并且搭载该脚本的物体处于激活状态。");
            return;
        }

        // 2. 查找所有的 WenwanSpeciesConfig 资产
        string[] guids = AssetDatabase.FindAssets("t:WenwanSpeciesConfig");
        List<WenwanSpeciesConfig> loadedConfigs = new List<WenwanSpeciesConfig>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            WenwanSpeciesConfig config = AssetDatabase.LoadAssetAtPath<WenwanSpeciesConfig>(path);
            if (config != null)
            {
                loadedConfigs.Add(config);
            }
        }

        if (loadedConfigs.Count == 0)
        {
            Debug.LogWarning("未找到任何 WenwanSpeciesConfig 配置文件！请检查配置项所在目录。");
            return;
        }

        // 3. 使用 Undo 记录，以便操作失误可以 Ctrl+Z 撤销
        Undo.RecordObject(manager, "Auto Assign Species Configs");

        // 4. 精准定位 SpeciesManager 里的 allSpecies 列表 (已根据你的代码修正)
        SerializedObject serializedManager = new SerializedObject(manager);
        SerializedProperty listProperty = serializedManager.FindProperty("allSpecies"); 

        if (listProperty != null && listProperty.isArray)
        {
            listProperty.ClearArray();
            for (int i = 0; i < loadedConfigs.Count; i++)
            {
                listProperty.InsertArrayElementAtIndex(i);
                listProperty.GetArrayElementAtIndex(i).objectReferenceValue = loadedConfigs[i];
            }
            serializedManager.ApplyModifiedProperties();
            
            // 标记场景已修改，确保保存时会写入磁盘
            EditorUtility.SetDirty(manager);
            Debug.Log($"<color=#00FF00>导入成功！</color> 已将 {loadedConfigs.Count} 个文玩配置自动绑定到 SpeciesManager 中！");
        }
        else
        {
            Debug.LogError("自动绑定失败：仍然找不到 allSpecies 变量，请确保变量名拼写没有被更改。");
        }
    }
}