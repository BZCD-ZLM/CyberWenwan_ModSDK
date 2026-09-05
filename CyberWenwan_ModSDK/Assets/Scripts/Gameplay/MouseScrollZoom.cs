using UnityEngine;

namespace CyberWenwan.Gameplay
{
    public class MouseScrollZoom : MonoBehaviour
    {
        [Header("目标设置")]
        [Tooltip("请拖入 WenwanPivot")]
        public Transform targetPivot;

        [Header("缩放参数")]
        public float zoomSpeed = 0.5f;
        public float minScale = 0.3f;
        public float maxScale = 2.5f;

        private Camera _mainCamera;

        void Start()
        {
            _mainCamera = Camera.main;
            if (targetPivot == null)
            {
                var obj = GameObject.Find("WenwanPivot");
                if (obj != null) targetPivot = obj.transform;
            }
        }

        void Update()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) < 0.001f) return;

            Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            // 只要指着东西（有Collider），就能缩放
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                ApplyZoom(scroll);
            }
        }

        void ApplyZoom(float scrollAmount)
        {
            if (targetPivot == null) return;
            Vector3 currentScale = targetPivot.localScale;
            float newScaleVal = currentScale.x + (scrollAmount * zoomSpeed);
            newScaleVal = Mathf.Clamp(newScaleVal, minScale, maxScale);
            targetPivot.localScale = Vector3.one * newScaleVal;
        }
    }
}