using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 存档管理器：负责记录最后一次玩的品种 ID 和收藏列表
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance;
    private string _savePath;
    
    // 缓存当前存档数据，避免每次保存都覆盖掉其他字段
    private SaveData _currentData;

    private void Awake()
    {
        if (Instance == null) 
        { 
            Instance = this; 
            DontDestroyOnLoad(gameObject); 
        }
        else 
        { 
            Destroy(gameObject); 
            return; 
        }

        _savePath = Path.Combine(Application.persistentDataPath, "save.json");
        Debug.Log($"[SaveManager] 存档路径: {_savePath}");
        
        // 初始化时加载一次数据到内存
        _currentData = LoadGame();
    }

    [System.Serializable]
    public class SaveData
    {
        public float currentProgress = 0f;
        public string currentSpeciesID = ""; // 记录当前选中的文玩ID
        public List<string> favoriteSpeciesIDs = new List<string>(); // 新增：收藏的文玩ID列表
    }

    /// <summary>
    /// 保存功能：记录当前品种及其进度到 JSON
    /// </summary>
    public void SaveGame(float progress, string speciesID)
    {
        if (string.IsNullOrEmpty(speciesID)) 
        {
            Debug.LogWarning("[SaveManager] 警告：尝试保存空 ID，操作已取消！");
            return;
        }

        // 更新内存中的数据，而不是 new 一个新的，防止丢失收藏列表
        _currentData.currentProgress = progress;
        _currentData.currentSpeciesID = speciesID;

        WriteToFile();
    }

    /// <summary>
    /// 将内存中的数据写入磁盘
    /// </summary>
    private void WriteToFile()
    {
        try 
        {
            string json = JsonUtility.ToJson(_currentData, true);
            File.WriteAllText(_savePath, json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveManager] JSON 写入失败: {e.Message}");
        }
    }

    /// <summary>
    /// 读取功能
    /// </summary>
    public SaveData LoadGame()
    {
        if (File.Exists(_savePath))
        {
            try 
            {
                string json = File.ReadAllText(_savePath);
                return JsonUtility.FromJson<SaveData>(json);
            }
            catch 
            { 
                Debug.LogWarning("[SaveManager] 存档文件损坏，已重置。");
                return new SaveData(); 
            }
        }
        return new SaveData(); 
    }

    // --- 新增：收藏系统接口 ---

    public bool IsFavorite(string speciesID)
    {
        if (_currentData == null || _currentData.favoriteSpeciesIDs == null) return false;
        return _currentData.favoriteSpeciesIDs.Contains(speciesID);
    }

    public void ToggleFavorite(string speciesID)
    {
        if (_currentData == null) return;
        
        if (_currentData.favoriteSpeciesIDs == null)
        {
            _currentData.favoriteSpeciesIDs = new List<string>();
        }

        if (_currentData.favoriteSpeciesIDs.Contains(speciesID))
        {
            _currentData.favoriteSpeciesIDs.Remove(speciesID);
        }
        else
        {
            _currentData.favoriteSpeciesIDs.Add(speciesID);
        }
        
        WriteToFile();
    }
    
    public List<string> GetFavorites()
    {
        return _currentData?.favoriteSpeciesIDs ?? new List<string>();
    }
}