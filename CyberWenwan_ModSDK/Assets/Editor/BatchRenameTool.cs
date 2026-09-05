using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CyberWenwan.EditorTools
{
    public class BatchRenameTool : EditorWindow
    {
        // --- 目标选项 ---
        private bool renameAssetObject = true;  // 修改外部文件名或物体名
        private bool renameConfigID = false;    // 修改 Config 内部 speciesID

        // --- 命名规则选项 ---
        private bool usePinyin = false;
        private bool useCustomPrefix = false;
        private string customPrefix = "walnut";


        public static void ShowWindow()
        {
            GetWindow<BatchRenameTool>("批量命名");
        }

        private void OnGUI()
        {
            GUILayout.Label("自动/自定义/拼音 批量重命名", EditorStyles.boldLabel);
            GUILayout.Label($"当前选中对象数量: {Selection.objects.Length}");

            GUILayout.Space(10);

            // ==========================================
            // 区块 1：修改目标选择
            // ==========================================
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("1. 选择要修改的目标 (可多选)", EditorStyles.boldLabel);
            renameAssetObject = EditorGUILayout.ToggleLeft(" 修改外部名称 (文件名 / Hierarchy物体名)", renameAssetObject);
            renameConfigID = EditorGUILayout.ToggleLeft(" 仅修改 Config 配置内的 Species ID", renameConfigID);
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // ==========================================
            // 区块 2：命名规则选择
            // ==========================================
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("2. 选择命名规则", EditorStyles.boldLabel);
            
            usePinyin = EditorGUILayout.Toggle("开启中文转拼音 (基于原名)", usePinyin);
            
            if (!usePinyin)
            {
                useCustomPrefix = EditorGUILayout.Toggle("启用自定义前缀", useCustomPrefix);
                if (useCustomPrefix)
                {
                    customPrefix = EditorGUILayout.TextField("输入前缀 (如 walnut)", customPrefix);
                }
                else
                {
                    EditorGUILayout.HelpBox("当前使用自动类型前缀 (如 Mesh_01, Item_01)", MessageType.Info);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("开启后，将读取文件原本的中文名转化为拼音作为前缀 (如 原始名'核桃' -> hetao_01)", MessageType.Info);
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(15);
            
            // 执行按钮
            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("一键执行修改", GUILayout.Height(40)))
            {
                RenameSelected();
            }
            GUI.backgroundColor = Color.white;
        }

        private void RenameSelected()
        {
            if (Selection.objects == null || Selection.objects.Length == 0)
            {
                Debug.LogWarning("请先选中需要修改的对象！");
                return;
            }

            if (!renameAssetObject && !renameConfigID)
            {
                Debug.LogWarning("请至少勾选一个修改目标（外部名称 或 Config ID）！");
                return;
            }

            // 按层级排序
            var sortedObjects = Selection.objects.OrderBy(o => 
            {
                if (o is GameObject go) return go.transform.GetSiblingIndex();
                return 0;
            }).ToList();

            Dictionary<string, int> typeCounts = new Dictionary<string, int>();
            int processedCount = 0;

            foreach (Object obj in sortedObjects)
            {
                string prefix;
                
                // 1. 确定前缀生成策略
                if (usePinyin)
                {
                    prefix = ConvertToPinyin(obj.name);
                    if (string.IsNullOrEmpty(prefix)) prefix = "Item"; // 兜底
                }
                else if (useCustomPrefix && !string.IsNullOrEmpty(customPrefix.Trim()))
                {
                    prefix = customPrefix.Trim();
                }
                else
                {
                    prefix = GetPrefixForObject(obj);
                }
                
                if (!typeCounts.ContainsKey(prefix))
                {
                    typeCounts[prefix] = 1;
                }

                // 2. 格式化新名称
                string newName = $"{prefix}_{typeCounts[prefix]:00}";
                typeCounts[prefix]++;

                // 3. 记录撤销
                Undo.RecordObject(obj, "Batch Modify");
                
                // 4. 执行目标修改 A：修改外部名称
                if (renameAssetObject)
                {
                    if (AssetDatabase.Contains(obj))
                    {
                        string assetPath = AssetDatabase.GetAssetPath(obj);
                        AssetDatabase.RenameAsset(assetPath, newName);
                    }
                    else
                    {
                        obj.name = newName;
                    }
                }

                // 5. 执行目标修改 B：修改 Config 内部 Species ID
                if (renameConfigID)
                {
                    SerializedObject so = new SerializedObject(obj);
                    SerializedProperty speciesIDProp = so.FindProperty("speciesID");
                    if (speciesIDProp != null)
                    {
                        speciesIDProp.stringValue = newName;
                        so.ApplyModifiedProperties();
                    }
                    else
                    {
                        Debug.LogWarning($"对象 {obj.name} 中未找到 speciesID 字段，已跳过 ID 修改。");
                    }
                }

                processedCount++;
            }
            
            // 保存修改
            if (!Application.isPlaying)
            {
                if (renameAssetObject && Selection.activeGameObject != null)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
                }
                
                if (renameAssetObject || renameConfigID)
                {
                    AssetDatabase.SaveAssets(); // 强制保存资产修改
                }
            }

            Debug.Log($"成功处理了 {processedCount} 个对象！");
        }

        private string GetPrefixForObject(Object obj)
        {
            if (obj is Material) return "Mat";
            if (obj is Texture) return "Tex";
            if (obj is AnimationClip) return "Anim";
            if (obj is AudioClip) return "Audio";
            
            if (obj is GameObject go)
            {
                if (go.GetComponent<MeshFilter>() != null || go.GetComponent<SkinnedMeshRenderer>() != null) return "Mesh";
                if (go.GetComponent<Camera>() != null) return "Cam";
                if (go.GetComponent<Light>() != null) return "Light";
                if (go.GetComponent<Canvas>() != null) return "Canvas";
                return "Obj"; 
            }
            return "Item"; 
        }

        // ==========================================
        // 简易文玩拼音转换器 (免第三方 DLL 方案)
        // ==========================================
        private string ConvertToPinyin(string originName)
        {
            // 移除原名称中可能带有的数字或下划线
            string cleanName = Regex.Replace(originName, @"[\d_]", "");
            
            StringBuilder sb = new StringBuilder();
            foreach (char c in cleanName)
            {
                if (IsChinese(c))
                {
                    if (wenwanPinyinDict.ContainsKey(c))
                    {
                        sb.Append(wenwanPinyinDict[c]);
                    }
                    else
                    {
                        sb.Append("x"); 
                    }
                }
                else
                {
                    sb.Append(char.ToLower(c));
                }
            }
            
            return sb.ToString().Trim();
        }

        private bool IsChinese(char c)
        {
            return c >= 0x4e00 && c <= 0x9fbb;
        }

        // 赛博文玩专属微型字库
        private readonly Dictionary<char, string> wenwanPinyinDict = new Dictionary<char, string>()
        {
            {'核', "he"}, {'桃', "tao"}, {'手', "shou"}, {'串', "chuan"},
            {'菩', "pu"}, {'提', "ti"}, {'金', "jin"}, {'刚', "gang"},
            {'绿', "lv"}, {'松', "song"}, {'石', "shi"}, {'蜜', "mi"},
            {'蜡', "la"}, {'星', "xing"}, {'月', "yue"}, {'凤', "feng"},
            {'眼', "yan"}, {'葫', "hu"}, {'芦', "lu"}, {'把', "ba"},
            {'件', "jian"}, {'沉', "chen"}, {'香', "xiang"}, {'紫', "zi"},
            {'檀', "tan"}, {'黄', "huang"}, {'花', "hua"}, {'梨', "li"},
            {'单', "dan"}, {'双', "shuang"}, {'对', "dui"}, {'玩', "wan"}, 
            {'大', "da"}, {'小', "xiao"}, {'老', "lao"}, {'新', "xin"}, 
            {'秋', "qiu"}, {'子', "zi"}, {'玉', "yu"}, {'玛', "ma"}, 
            {'瑙', "nao"}, {'崖', "ya"}, {'柏', "bai"}, {'官', "guan"}, 
            {'帽', "mao"}, {'狮', "shi"}, {'虎', "hu"}, {'骨', "gu"}, 
            {'木', "mu"}, {'铜', "tong"}, {'铁', "tie"},

            // 新增提取的字
            {'黑', "hei"}, {'麻', "ma"}, {'头', "tou"}, {'麒', "qi"},
            {'麟', "lin"}, {'白', "bai"}, {'鸡', "ji"}, {'翅', "chi"},
            {'长', "chang"}, {'水', "shui"}, {'晶', "jing"}, {'南', "nan"},
            {'红', "hong"}, {'血', "xue"}, {'龙', "long"}, {'多', "duo"},
            {'宝', "bao"}, {'山', "shan"}, {'缅', "mian"}, {'甸', "dian"},
            {'链', "lian"}, {'酸', "suan"}, {'枝', "zhi"}, {'柳', "liu"},
            {'祖', "zu"}, {'母', "mu"}, {'髓', "sui"}, {'镯', "zhuo"},
            {'贝', "bei"}, {'雕', "diao"}, {'猴', "hou"}, {'叶', "ye"},
            {'四', "si"}, {'座', "zuo"}, {'楼', "lou"}, {'古', "gu"},
            {'人', "ren"}, {'游', "you"}, {'园', "yuan"}, {'八', "ba"},
            {'九', "jiu"}, {'棱', "leng"}, {'至', "zhi"}, {'尊', "zun"}
        };
    }
}