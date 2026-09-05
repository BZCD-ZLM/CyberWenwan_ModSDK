using UnityEngine;
using UnityEngine.UI;

public class CoinUI : MonoBehaviour
{
    [Header("UI 引用")]
    public Text coinText; 

    private void Update()
    {
        // 确保实例存在且 UI 引用已赋值
        if (CoinManager.Instance != null && coinText != null)
        {
            // 实时更新金币数量
            coinText.text = CoinManager.Instance.GetCoins().ToString();
        }
        else if (CoinManager.Instance == null)
        {
            Debug.LogWarning("[CoinUI] 找不到 CoinManager 实例！请确保 SystemManager 上挂载了 CoinManager。");
        }
        else if (coinText == null)
        {
            Debug.LogWarning("[CoinUI] Text 组件未赋值！请在 Inspector 面板中将 Text 拖给 coinText 变量。");
        }
    }
}