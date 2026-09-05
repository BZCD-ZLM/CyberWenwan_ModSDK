using UnityEngine;

[RequireComponent(typeof(WenwanStringController))]
public class StringMotion : MonoBehaviour
{
    [Header("物理动力权重 (由输入驱动)")]
    public float physicsKeyboardWeight = 0.05f; 
    public float physicsClickWeight = 0.05f; 
    public float physicsScrollWeight = 0.05f; 
    public float physicsSpeedMultiplier = 1.0f;
    public float rotationFriction = 0.95f; 

    [Header("基础物理手感")]
    public float spinPower = 2000f; 
    public float maxSpinSpeed = 1500f;
    public Vector3 rotationAxis = Vector3.up; 

    [Header("自动换向 (模拟真实拨动)")]
    public bool useAutoDirectionChange = true;
    public float directionSwitchSpeed = 2.0f;
    public float changeCheckInterval = 5.0f;
    [Range(0f, 1f)] public float changeProbability = 0.3f;

    private WenwanStringController _controller;
    private float _currentSpeed = 0f;

    private float _currentDirectionVal = 1f; 
    private float _targetDirectionVal = 1f;
    private float _directionTimer = 0f;
    private bool _isInitialized = false;

    void Start()
    {
        _controller = GetComponent<WenwanStringController>();
        if (_controller != null) _isInitialized = true;
    }

    // 新增：直接接收 Hook 传来的物理动作
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

        if (_controller != null && _controller.isInspecting)
        {
            _currentSpeed = Mathf.Lerp(_currentSpeed, 0, Time.deltaTime * 10f); 
            ApplyRotation();
            return;
        }

        float dt = Time.deltaTime;

        // 摩擦力从自身提取
        float friction = Mathf.Clamp(rotationFriction, 0.1f, 0.999f); 
        _currentSpeed *= Mathf.Pow(friction, dt * 60f); 
        _currentSpeed = Mathf.Clamp(_currentSpeed, 0, maxSpinSpeed);

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
        else _currentDirectionVal = 1f;

        ApplyRotation();
    }

    private void ApplyRotation()
    {
        if (_currentSpeed > 0.01f)
            transform.Rotate(rotationAxis, _currentSpeed * _currentDirectionVal * Time.deltaTime, Space.Self);
    }

    public void ResetMotion()
    {
        _currentSpeed = 0f;
        _currentDirectionVal = 1f;
        _targetDirectionVal = 1f;
        transform.localRotation = Quaternion.identity;

        if (_controller != null && _controller.currentStringInstance != null)
        {
            Rigidbody rb = _controller.currentStringInstance.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
        }
    }

    public void Unlock()
    {
        if (_controller != null && _controller.currentStringInstance != null)
        {
            Rigidbody rb = _controller.currentStringInstance.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.WakeUp();
            }
        }
    }
}