using UnityEngine;
using UnityEditor;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace CyberWenwan.EditorTools
{
    public class CheckChineseNamingTool
    {
        public static void CheckChineseNames()
        {
            // 获取项目中所有资产的路径
            string[] allAssetPaths = AssetDatabase.GetAllAssetPaths();
            List<Object> chineseNamedObjects = new List<Object>();
            int count = 0;

            Debug.Log("🔍 开始全盘扫描 Assets 目录下的中文命名文件...");

            foreach (string path in allAssetPaths)
            {
                // 只检查 Assets 目录，忽略底层的 Packages 等系统文件
                if (!path.StartsWith("Assets/")) continue;

                // 提取文件名或文件夹名（不包含前面的路径）
                string fileName = System.IO.Path.GetFileName(path);

                // 使用正则表达式匹配是否包含任意中文字符
                if (Regex.IsMatch(fileName, @"[\u4e00-\u9fa5]"))
                {
                    Debug.LogWarning($"⚠️ 发现中文命名文件/文件夹: {path}");
                    
                    // 加载该资源，存入列表
                    Object obj = AssetDatabase.LoadAssetAtPath<Object>(path);
                    if (obj != null)
                    {
                        chineseNamedObjects.Add(obj);
                    }
                    count++;
                }
            }

            // 反馈结果
            if (count > 0)
            {
                // 核心功能：直接在 Project 窗口中帮你把它们全部选中！
                Selection.objects = chineseNamedObjects.ToArray();
                Debug.LogError($"❌ 扫描完毕！共找到 {count} 个包含中文命名的文件/文件夹。\n它们已经在 Project 窗口中被高亮选中了！你可以直接用之前的批量重命名工具处理它们。");
            }
            else
            {
                Debug.Log("✅ 扫描完毕！太棒了，Assets 目录下没有发现任何中文命名的文件，环境非常纯净！");
            }
        }
    }
}