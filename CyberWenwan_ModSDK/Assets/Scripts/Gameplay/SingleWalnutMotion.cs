using UnityEngine;

public class SingleWalnutMotion : MonoBehaviour
{
    [Header("物理动力权重 (由输入驱动)")]
    public float physicsKeyboardWeight = 0.05f; 
    public float physicsClickWeight = 0.05f; 
    public float physicsScrollWeight = 0.05f; 
    public float physicsSpeedMultiplier = 1.0f;
    public float rotationFriction = 0.95f; 

    [Header("基础物理手感")]
    public float spinPower = 2500f; 
    public float maxSpinSpeed = 1200f; 

    [Header("自动换向 (指尖拨动模拟)")]
    public bool useAutoDirectionChange = true;
    public float directionSwitchSpeed = 2.0f;
    public float changeCheckInterval = 3.0f;
    [Range(0f, 1f)] public float changeProbability = 0.3f;

    [Header("混沌翻转 (Tumbling)")]
    public bool useChaosRotation = true;
    public float axisChangeSpeed = 1.5f; 

    // 核心对象：彻底私有化，全自动实时抓取，不在面板暴露防止拖错
    private Transform _targetWalnut;
    private WenwanSingleController _controller;

    private float _currentSpeed = 0f;
    
    private Vector3 _currentAxis = Vector3.up; 
    private Vector3 _targetAxis = Vector3.up;  
    private float _currentDirectionVal = 1f; 
    private float _targetDirectionVal = 1f;
    private float _directionTimer = 0f;
    private bool _isInitialized = false;

    void Start()
    {
        _controller = GetComponent<WenwanSingleController>();
        _currentAxis = Random.onUnitSphere;
        _targetAxis = Random.onUnitSphere;
        _isInitialized = true;
    }

    // 新增：像双核逻辑一样，实时同步渲染目标，永不丢失
    private void SyncReferenceDynamically()
    {
        if (_controller != null && _controller.targetRenderer != null)
        {
            _targetWalnut = _controller.targetRenderer.transform;
            
            // 确保物理引擎不干扰我们手写的代码旋转
            Rigidbody rb = _targetWalnut.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true; 
        }
        else
        {
            _targetWalnut = null;
        }
    }

    public void AddPhysicalInput(int keyCount, int clickCount, int scrollCount)
    {
        if (_controller != null && _controller.isInspecting) return;

        float physAmount = (keyCount * physicsKeyboardWeight) +
                           (clickCount * physicsClickWeight) +
                           (scrollCount * physicsScrollWeight);
        
        float impulse = physAmount * physicsSpeedMultiplier * spinPower;
        _currentSpeed += impulse;
    }

    void Update()
    {
        if (!_isInitialized) return;

        // 每次 Update 都确保目标没有丢失（完美支持在游戏中切换文玩）
        SyncReferenceDynamically();

        if (_targetWalnut == null) return;

        if (_controller != null && _controller.isInspecting)
        {
            _currentSpeed = Mathf.Lerp(_currentSpeed, 0, Time.deltaTime * 10f);
            ApplyPhysicalRotation(Time.deltaTime);
            return;
        }

        float dt = Time.deltaTime;

        // 摩擦力衰减
        float friction = Mathf.Clamp(rotationFriction, 0.1f, 0.999f);
        _currentSpeed *= Mathf.Pow(friction, dt * 60f);
        _currentSpeed = Mathf.Clamp(_currentSpeed, 0, maxSpinSpeed);

        HandleDirectionAndChaos(dt);
        ApplyPhysicalRotation(dt);
    }

    private void HandleDirectionAndChaos(float dt)
    {
        if (useAutoDirectionChange)
        {
            _directionTimer += dt;
            if (_directionTimer > changeCheckInterval)
            {
                _directionTimer = 0f;
                if (Random.value < changeProbability) _targetDirectionVal = -_targetDirectionVal; 
            }
            _currentDirectionVal = Mathf.Lerp(_currentDirectionVal, _targetDirectionVal, dt * directionSwitchSpeed);
        }

        if (useChaosRotation)
        {
            _currentAxis = Vector3.Slerp(_currentAxis, _targetAxis, dt * axisChangeSpeed);
            if (Vector3.Angle(_currentAxis, _targetAxis) < 5f) _targetAxis = Random.onUnitSphere;
        }
        else _currentAxis = Vector3.up; 
    }

    private void ApplyPhysicalRotation(float dt)
    {
        if (_currentSpeed > 0.01f)
            _targetWalnut.Rotate(_currentAxis, _currentSpeed * _currentDirectionVal * dt, Space.World);
    }

    public void ResetMotion()
    {
        _currentSpeed = 0f;
        _currentDirectionVal = 1f;
        _targetDirectionVal = 1f;
        if (_targetWalnut != null)
        {
            _targetWalnut.localRotation = Quaternion.identity;
            Rigidbody rb = _targetWalnut.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
        }
    }
}