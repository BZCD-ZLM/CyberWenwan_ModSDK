using UnityEngine;
using UnityEngine.EventSystems; // 必须引用：用于检测鼠标进入/离开
using System.Collections;

// 挂载这个脚本的物体，鼠标放上去会自动放大，移开会自动复原
public class ItemHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("动画设置")]
    public float hoverScale = 1.2f;  // 放大倍数 (1.2倍)
    public float duration = 0.2f;    // 动画时间 (秒)

    private Vector3 _originalScale;
    private Coroutine _currentCoroutine;

    void Start()
    {
        // 记住一开始的大小 (通常是 1,1,1)
        _originalScale = transform.localScale;
    }

    // 当鼠标进入时触发
    public void OnPointerEnter(PointerEventData eventData)
    {
        StartScaleAnimation(_originalScale * hoverScale);
    }

    // 当鼠标离开时触发
    public void OnPointerExit(PointerEventData eventData)
    {
        StartScaleAnimation(_originalScale);
    }

    // 每次关闭窗口时，强制还原大小，防止下次打开时卡在变大的状态
    void OnDisable()
    {
        transform.localScale = Vector3.one; 
    }

    // 启动动画的辅助方法
    private void StartScaleAnimation(Vector3 target)
    {
        // 如果上一个动画还没做完，打断它，直接做新的
        if (_currentCoroutine != null) StopCoroutine(_currentCoroutine);
        _currentCoroutine = StartCoroutine(AnimateScale(target));
    }

    // 真正的平滑动画逻辑
    IEnumerator AnimateScale(Vector3 target)
    {
        Vector3 start = transform.localScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // 使用 SmoothStep 让动画看起来更丝滑 (两头慢中间快)
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            transform.localScale = Vector3.Lerp(start, target, t);
            yield return null; // 等待下一帧
        }
        transform.localScale = target; // 确保最后数值精确
    }
}