using UnityEngine;

public class WenwanInspect : MonoBehaviour
{
    [Header("观察设置")]
    public float rotationSpeed = 15.0f; 
    public bool invertX = false;       
    public bool invertY = false;       

    private Vector3 _lastMousePos;
    
    // 将状态拆分，分别记录左键和右键的按下情况
    private bool _isLeftDragging = false;
    private bool _isRightDragging = false;
    
    private Camera _mainCamera; 

    // 【新增成就逻辑】观察计时器
    private float _inspectTimer = 0f;
    private bool _achievementTriggered = false;

    void Start()
    {
        _mainCamera = Camera.main; 
    }

    void Update()
    {
        // 1. 按下鼠标左键：尝试开始观察 (冻结 + 旋转)
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                _isLeftDragging = true;
                _lastMousePos = Input.mousePosition;
                
                _inspectTimer = 0f;
                _achievementTriggered = false;

                RefreshInspectingState();
            }
        }

        // 2. 松开鼠标左键：结束观察
        if (Input.GetMouseButtonUp(0))
        {
            if (_isLeftDragging)
            {
                _isLeftDragging = false;
                _inspectTimer = 0f;

                RefreshInspectingState();
            }
        }

        // 3. 按下鼠标右键：仅冻结物理，配合拖拽窗口，不进行场景内旋转
        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                _isRightDragging = true;
                RefreshInspectingState();
            }
        }

        // 4. 松开鼠标右键：解除冻结
        if (Input.GetMouseButtonUp(1))
        {
            if (_isRightDragging)
            {
                _isRightDragging = false;
                RefreshInspectingState();
            }
        }

        // 5. 拖拽逻辑：仅在左键按下时才允许旋转物体
        if (_isLeftDragging)
        {
            Vector3 delta = Input.mousePosition - _lastMousePos;
            float xRot = delta.x * rotationSpeed * Time.deltaTime * (invertX ? -1 : 1);
            float yRot = delta.y * rotationSpeed * Time.deltaTime * (invertY ? -1 : 1);

            transform.Rotate(Vector3.up, -xRot, Space.World);
            transform.Rotate(Vector3.right, yRot, Space.World);

            _lastMousePos = Input.mousePosition;

            // 累计观察时间
            if (!_achievementTriggered)
            {
                _inspectTimer += Time.deltaTime;
                if (_inspectTimer >= 10f)
                {
                    _achievementTriggered = true;
                    if (SteamAchievementManager.Instance != null)
                    {
                        SteamAchievementManager.Instance.UnlockInspectAchievement();
                    }
                }
            }
        }
    }

    // 辅助方法：统一设置观察状态，只要左键或右键任意一个按住，就保持冻结
    private void RefreshInspectingState()
    {
        bool shouldInspect = _isLeftDragging || _isRightDragging;

        // 检查双核模式
        if (WenwanDualController.Instance != null && WenwanDualController.Instance.gameObject.activeInHierarchy)
        {
            WenwanDualController.Instance.isInspecting = shouldInspect;
        }
        // 检查单枚模式
        if (WenwanSingleController.Instance != null && WenwanSingleController.Instance.gameObject.activeInHierarchy)
        {
            WenwanSingleController.Instance.isInspecting = shouldInspect;
        }
        // 检查手串模式
        if (WenwanStringController.Instance != null && WenwanStringController.Instance.gameObject.activeInHierarchy)
        {
            WenwanStringController.Instance.isInspecting = shouldInspect;
        }
    }
}