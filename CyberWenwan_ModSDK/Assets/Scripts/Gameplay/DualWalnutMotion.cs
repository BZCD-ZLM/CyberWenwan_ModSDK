using UnityEngine;

public class DualWalnutMotion : MonoBehaviour
{
    [Header("物理动力权重 (由输入驱动)")]
    public float physicsKeyboardWeight = 0.05f; 
    public float physicsClickWeight = 0.05f; 
    public float physicsScrollWeight = 0.05f; 
    public float physicsSpeedMultiplier = 1.0f;
    public float rotationFriction = 0.95f; 

    [Header("基础物理手感")]
    public float spinPower = 2000f; 
    [Range(0f, 2f)] public float orbitPowerRatio = 0.6f; 
    [Range(0f, 2f)] public float selfPowerRatio = 1.0f;  

    [Header("独立阻尼微调")]
    [Range(0.5f, 1f)] public float orbitFriction = 0.96f; 
    [Range(0.5f, 1f)] public float selfFriction = 0.98f; 

    [Header("速度限制")]
    public float maxOrbitSpeed = 500f; 
    public float maxSelfSpeed = 1000f;

    [Header("自动换向")]
    public bool useAutoDirectionChange = true;
    public float directionSwitchSpeed = 1.5f;
    public float changeCheckInterval = 4.0f;
    [Range(0f, 1f)] public float changeProbability = 0.4f;

    [Header("混沌设置")]
    public bool useChaosRotation = true;
    public float selfAxisChangeSpeed = 2.0f;
    public float orbitAxisChangeSpeed = 0.5f; 
    [Range(0f, 1f)] public float orbitStability = 0.85f; 

    private Transform _walnutLeft;
    private Transform _walnutRight;
    private WenwanDualController _controller; 
    
    private float _currentOrbitSpeed = 0f; 
    private float _currentSelfSpeed = 0f;
    
    private Vector3 _orbitAxis; 
    private Vector3 _leftSpinAxis;
    private Vector3 _rightSpinAxis;
    private Vector3 _leftTargetAxis;
    private Vector3 _rightTargetAxis;
    private Vector3 _chaosAxis = Vector3.forward;
    private Vector3 _chaosTarget = Vector3.forward;

    private float _currentDirectionVal = 1f; 
    private float _targetDirectionVal = 1f;
    private float _directionTimer = 0f;      
    private bool _isInitialized = false;

    void Start()
    {
        _controller = GetComponent<WenwanDualController>();
        _leftSpinAxis = Random.onUnitSphere;
        _rightSpinAxis = Random.onUnitSphere;
        _leftTargetAxis = Random.onUnitSphere;
        _rightTargetAxis = Random.onUnitSphere;
        _chaosAxis = GetHorizontalRandomAxis();
        _chaosTarget = GetHorizontalRandomAxis();
        _orbitAxis = Vector3.forward; 
        _isInitialized = true;
    }

    private void SyncReferencesDynamically()
    {
        if (_controller == null || _controller.targetRenderers == null || _controller.targetRenderers.Length < 2) 
        {
            _walnutLeft = null;
            _walnutRight = null;
            return;
        }
        _walnutLeft = _controller.targetRenderers[0].transform;
        _walnutRight = _controller.targetRenderers[1].transform;
    }

    public void AddPhysicalInput(int keyCount, int clickCount, int scrollCount)
    {
        if (_controller != null && _controller.isInspecting) return;

        float physAmount = (keyCount * physicsKeyboardWeight) +
                           (clickCount * physicsClickWeight) +
                           (scrollCount * physicsScrollWeight);
        
        float impulse = physAmount * physicsSpeedMultiplier * spinPower;
        _currentOrbitSpeed += impulse * orbitPowerRatio;
        _currentSelfSpeed += impulse * selfPowerRatio;
    }

    void Update()
    {
        if (!_isInitialized) return;

        SyncReferencesDynamically();
        if (_walnutLeft == null || _walnutRight == null) return;

        if (_controller != null && _controller.isInspecting)
        {
            _currentOrbitSpeed = Mathf.Lerp(_currentOrbitSpeed, 0, Time.deltaTime * 10f);
            _currentSelfSpeed = Mathf.Lerp(_currentSelfSpeed, 0, Time.deltaTime * 10f);
            ApplyRotation(Time.deltaTime);
            return; 
        }

        float dt = Time.deltaTime;

        float baseFriction = Mathf.Clamp(rotationFriction, 0.1f, 0.999f); 
        float finalOrbitFriction = baseFriction * orbitFriction;
        float finalSelfFriction = baseFriction * selfFriction;

        _currentOrbitSpeed *= Mathf.Pow(finalOrbitFriction, dt * 60f);
        _currentSelfSpeed *= Mathf.Pow(finalSelfFriction, dt * 60f);

        _currentOrbitSpeed = Mathf.Clamp(_currentOrbitSpeed, 0, maxOrbitSpeed);
        _currentSelfSpeed = Mathf.Clamp(_currentSelfSpeed, 0, maxSelfSpeed);

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

        if (useChaosRotation)
        {
            UpdateChaosAxis(ref _leftSpinAxis, ref _leftTargetAxis, selfAxisChangeSpeed, false);
            UpdateChaosAxis(ref _rightSpinAxis, ref _rightTargetAxis, selfAxisChangeSpeed, false);
            UpdateChaosAxis(ref _chaosAxis, ref _chaosTarget, orbitAxisChangeSpeed, true);
        }

        ApplyRotation(dt);
    }

    private void ApplyRotation(float dt)
    {
        if (_walnutLeft != null && _walnutRight != null)
        {
            Vector3 fixedOrbitCenter = transform.position;
            Vector3 connectionLine = _walnutRight.position - _walnutLeft.position;
            Vector3 geometricAxis = Vector3.Cross(connectionLine, Vector3.up).normalized;

            if (geometricAxis.sqrMagnitude < 0.001f) geometricAxis = Vector3.forward; 
            if (Vector3.Dot(geometricAxis, _orbitAxis) < 0) geometricAxis = -geometricAxis;

            Vector3 targetAxis = geometricAxis;
            if (useChaosRotation) targetAxis = Vector3.Slerp(_chaosAxis, geometricAxis, orbitStability);
            
            targetAxis.y = 0;
            targetAxis.Normalize();

            _orbitAxis = Vector3.Slerp(_orbitAxis, targetAxis, dt * 5.0f);

            if (_currentOrbitSpeed > 0.01f)
            {
                float finalOrbitSpeed = _currentOrbitSpeed * _currentDirectionVal;
                _walnutLeft.RotateAround(fixedOrbitCenter, _orbitAxis, finalOrbitSpeed * dt);
                _walnutRight.RotateAround(fixedOrbitCenter, _orbitAxis, finalOrbitSpeed * dt);
            }
            if (_currentSelfSpeed > 0.01f)
            {
                float finalSelfSpeed = _currentSelfSpeed * _currentDirectionVal;
                _walnutLeft.Rotate(_leftSpinAxis, finalSelfSpeed * dt, Space.World);
                _walnutRight.Rotate(_rightSpinAxis, finalSelfSpeed * dt, Space.World);
            }
        }
    }

    Vector3 GetHorizontalRandomAxis()
    {
        Vector2 circle = Random.insideUnitCircle.normalized;
        return new Vector3(circle.x, 0, circle.y);
    }

    void UpdateChaosAxis(ref Vector3 currentAxis, ref Vector3 targetAxis, float speed, bool forceHorizontal)
    {
        currentAxis = Vector3.Slerp(currentAxis, targetAxis, Time.deltaTime * speed);
        if (Vector3.Angle(currentAxis, targetAxis) < 5f)
            targetAxis = forceHorizontal ? GetHorizontalRandomAxis() : Random.onUnitSphere;
    }

    public void LockAndReset()
    {
        SyncReferencesDynamically();
        _currentOrbitSpeed = 0f;
        _currentSelfSpeed = 0f;
        _currentDirectionVal = 1f;
        _targetDirectionVal = 1f;
        ResetSingle(_walnutLeft);
        ResetSingle(_walnutRight);
    }

    public void Unlock()
    {
        UnlockSingle(_walnutLeft);
        UnlockSingle(_walnutRight);
    }

    private void ResetSingle(Transform t)
    {
        if (t == null) return;
        
        // 【核心修复】：移除了原来会导致位移无限累加的硬编码坐标重置代码
        // ( t.localPosition = new Vector3(t.localPosition.x, 0, 0); )
        // 现在绝对信任 Controller 从美术缓存中恢复的完美坐标。
        
        Rigidbody rb = t.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true; 
        }
    }

    private void UnlockSingle(Transform t)
    {
        if (t == null) return;
        Rigidbody rb = t.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false; 
            rb.WakeUp();
        }
    }
}