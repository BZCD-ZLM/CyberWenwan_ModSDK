using UnityEngine;
using System.Collections.Generic;

public enum WenwanCategory
{
    Wood,           // 木质类
    SeedAndNut,     // 籽核果壳类
    JadeAndStone,   // 玉石宝石类
    BoneAndHorn,    // 牙骨角类
    BambooAndGourd, // 竹木匏器类
    MetalAndMisc    // 金属及其他杂项
}

// 显示模式枚举：支持单枚、对玩、以及整串模式
public enum DisplayMode
{
    Dual,   // 双核桃/对玩模式
    Single, // 单枚/手把件模式
    String  // 手串/项串模式 (整串导入)
}

// 定义特殊珠子的材质数据，用于处理混搭材质
[System.Serializable]
public class SpecialBeadData
{
    [Tooltip("这颗珠子在手串子物体中的索引编号 (从0开始)")]
    public int beadIndex; 
    
    [Tooltip("特殊隔珠/顶珠的材质球")]
    public Material customMaterial; 
}

[CreateAssetMenu(fileName = "NewWenwanConfig", menuName = "CyberWenwan/Wenwan Config")]
public class WenwanSpeciesConfig : ScriptableObject
{
    [Header("1. 基本身份信息")]
    public string speciesName = "新文玩名称"; 
    
    // 存档唯一ID (例如: "walnut_demo_01")
    public string speciesID = "unique_id_here"; 

    // 显示模式：单枚、成对、或手串
    public DisplayMode displayMode = DisplayMode.Dual;
    
    public WenwanCategory category = WenwanCategory.SeedAndNut; 
    
    [Tooltip("UI博古架上显示的预览图标")]
    public Sprite speciesIcon; // 新增：UI 必须要有 2D 图标支持
    
    [TextArea(3, 5)]
    public string speciesDescription = "填写该文玩的背景...";

    [Header("2. 模型与材质 (URP)")]
    [Tooltip("单枚模式专用：请将单枚文玩的 Prefab 拖到这里")]
    public GameObject singlePrefab; 

    [Tooltip("对玩模式专用：请将成对文玩的 Prefab 拖到这里（包含左右两个子物体）")]
    public GameObject dualPrefab; 
    
    [Tooltip("手串模式专用：请将手串的 Prefab 拖到这里（包含所有珠子的层级结构）")]
    public GameObject stringPrefab; 

    [Space(10)]
    [Tooltip("【新版核心】请将配置好的 WenwanEvolution 材质球拖入此处")]
    public Material speciesMaterial; 

    [Header("3. 进化曲线控制")]
    public AnimationCurve colorCurve = AnimationCurve.Linear(0, 0, 1, 1); 
    public AnimationCurve glossinessCurve = AnimationCurve.EaseInOut(0, 0.2f, 1, 0.9f); 

    [Header("4. 进化进度权重 (双轨系统 - 包浆)")]
    [Tooltip("键盘/鼠标输入对【包浆进度】的影响系数")]
    public float evolutionKeyboardWeight = 0.0001f; 
    public float evolutionClickWeight = 0.0001f; 
    public float evolutionScrollWeight = 0.0001f; 
    public float evolutionSpeedMultiplier = 1.0f; 

    [Header("5. 物理动力权重 (双轨系统 - 旋转)")]
    [Tooltip("键盘/鼠标输入对【旋转动力】的影响系数")]
    public float physicsKeyboardWeight = 0.05f; 
    public float physicsClickWeight = 0.05f; 
    public float physicsScrollWeight = 0.05f; 
    public float physicsSpeedMultiplier = 1.0f;

    [Header("6. 其他设置")]
    [Tooltip("物理旋转的摩擦力")]
    public float rotationFriction = 0.95f; 

    [Header("7. 手串混搭设置 (仅String模式生效)")]
    [Tooltip("如果你的手串里有特殊材质的隔珠、顶珠，请在这里添加并指定它们的索引")]
    public List<SpecialBeadData> specialBeads = new List<SpecialBeadData>();

    [Header("8. 灰尘/脏迹系统 (Dust System)")]
    [Tooltip("灰尘的基础颜色 (建议灰褐色: RGB 0.6, 0.55, 0.5)")]
    public Color dustColor = new Color(0.6f, 0.55f, 0.5f, 1.0f);

    [Tooltip("灰尘分布遮罩 (R通道: 灰尘积聚密度/AO图)")]
    public Texture2D dustMask;

    [Tooltip("自然积灰速度: 多少小时充满100%灰尘 (默认24小时)")]
    public float hoursToMaxDust = 24.0f;

    [Tooltip("盘玩清洁力度: 每次输入减少多少灰尘 (建议 0.002)")]
    public float dustCleanPerInput = 0.002f;

    [Header("9. 创意工坊设置")]
    [Tooltip("是否为创意工坊导入的物品？（勾选后在仓库中免解锁，直接游玩）")]
    public bool isWorkshopItem = false;
}