using UnityEngine;
using UnityEngine.UI;

namespace CyberWenwan.UI
{
    public class TutorialPopupUI : MonoBehaviour
    {
        [Header("UI 组件引用")]
        [Tooltip("这是包含引导图和关闭按钮的总父物体")]
        public GameObject popupPanel; 
        
        [Tooltip("右上角的关闭按钮")]
        public Button closeButton;

        // 用于在本地注册表中记录是否看过的 Key
        private const string TutorialKey = "CyberWenwan_HasSeenTutorial";

        void Start()
        {
            // 防错检查：如果美术忘了拖拽引用，直接报错提醒
            if (popupPanel == null || closeButton == null)
            {
                Debug.LogError("TutorialPopupUI: 弹窗面板或关闭按钮未赋值！");
                return;
            }

            // 检查是否是第一次启动 (如果找不到这个 Key，默认返回 0 表示没看过)
            if (PlayerPrefs.GetInt(TutorialKey, 0) == 0)
            {
                // 是第一次，强制显示弹窗并拦截操作
                popupPanel.SetActive(true);
                
                // 绑定关闭按钮的点击事件
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(CloseTutorial);
            }
            else
            {
                // 以前看过了，直接隐藏弹窗，不打扰玩家
                popupPanel.SetActive(false);
            }
        }

        private void CloseTutorial()
        {
            // 玩家点击了关闭：隐藏弹窗
            popupPanel.SetActive(false);
            
            // 写入本地记录：标记为 1 (已看)
            PlayerPrefs.SetInt(TutorialKey, 1);
            PlayerPrefs.Save();
        }

        // ==========================================
        // 开发者调试工具：在 Inspector 中右键脚本名字可一键重置状态
        // ==========================================
        [ContextMenu("重置新手引导状态 (测试用)")]
        public void ResetTutorialState()
        {
            PlayerPrefs.DeleteKey(TutorialKey);
            Debug.Log("✅ 新手引导状态已重置！下次启动游戏（或重新运行场景）将再次显示弹窗。");
        }
    }
}