using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Image))]
[RequireComponent(typeof(Button))]
public class UITabButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("状态颜色配置")]
    public Color normalColor = Color.white;
    public Color hoverColor = new Color(0.85f, 0.85f, 0.85f, 1f); // 鼠标悬停变灰
    public Color selectedColor = new Color(0.6f, 0.6f, 0.6f, 1f); // 选中变暗

    private Image _image;
    private Button _button;
    
    private bool _isSelected = false;
    private bool _isHovered = false;

    void Awake()
    {
        _image = GetComponent<Image>();
        _button = GetComponent<Button>();
        
        // 【核心】强制关闭 Unity 原生 Button 的颜色渐变，防止与本脚本的颜色控制发生冲突
        if (_button != null)
        {
            _button.transition = Selectable.Transition.None;
        }
        
        UpdateColor();
    }

    // 留给外部管理器（胶水代码）调用的接口
    public void SetSelected(bool isSelected)
    {
        _isSelected = isSelected;
        UpdateColor();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        UpdateColor();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        UpdateColor();
    }

    private void UpdateColor()
    {
        if (_image == null) return;

        // 优先级：选中色 > 悬停色 > 正常色
        if (_isSelected)
        {
            _image.color = selectedColor;
        }
        else if (_isHovered)
        {
            _image.color = hoverColor;
        }
        else
        {
            _image.color = normalColor;
        }
    }
}