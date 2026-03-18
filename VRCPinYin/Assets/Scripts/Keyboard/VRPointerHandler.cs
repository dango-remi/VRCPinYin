// VRCPinYin - 模块 2：虚拟键盘 UI
// 每帧处理控制器射线 → ComputeIntersection → UV → GraphicRaycast → Pointer 事件。
// 同时服务于键盘按键与模块 3（候选词面板）的按钮点击。

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Valve.VR;

namespace VRCPinYin.Keyboard
{
    public enum PointerHand
    {
        Right,
        Left,
        Any
    }

    public class VRPointerHandler : MonoBehaviour
    {
        [Header("SteamVR Actions")]
        [Tooltip("控制器位姿 Action")]
        public SteamVR_Action_Pose poseAction;

        [Tooltip("扳机/点击 Action")]
        public SteamVR_Action_Boolean clickAction;

        [Header("References")]
        [Tooltip("OverlayCanvas 上的 GraphicRaycaster")]
        public GraphicRaycaster graphicRaycaster;

        [Tooltip("OverlayCamera（用于事件坐标系）")]
        public Camera overlayCamera;

        [Tooltip("射线光标 Image（Canvas 最顶层）")]
        public RectTransform cursorImage;

        [Header("Options")]
        [Tooltip("操作手柄选择")]
        public PointerHand pointerHand = PointerHand.Right;

        [Tooltip("是否显示射线光标")]
        public bool showCursor = true;

        // ── 对外属性 ──

        public bool IsPointerActive { get; private set; }
        public Vector2 CurrentUV { get; private set; }
        public GameObject CurrentHoverTarget { get; private set; }

        private PointerEventData _pointerData;
        private GameObject _pressTarget;
        private bool _wasPressed;
        private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();

        private bool _initialized;

        private void Start()
        {
            if (poseAction == null || clickAction == null)
            {
                Debug.LogWarning("[VRCPinYin.验收] poseAction 或 clickAction 未配置，VR 指针将不可用");
            }

            if (EventSystem.current != null)
            {
                _pointerData = new PointerEventData(EventSystem.current);
            }
            else
            {
                Debug.LogWarning("[VRCPinYin.验收] EventSystem 不存在，VRPointerHandler 将无法发送 Pointer 事件");
            }

            _initialized = true;

            string poseName = poseAction != null ? poseAction.GetShortName() : "null";
            string clickName = clickAction != null ? clickAction.GetShortName() : "null";
            Debug.Log("[VRCPinYin.验收] VRPointerHandler 初始化完成, poseAction=" + poseName + ", clickAction=" + clickName);
        }

        private void Update()
        {
            if (!_initialized) return;

            var overlayMgr = Overlay.OverlayManager.Instance;
            if (overlayMgr == null || !overlayMgr.IsVisible)
            {
                if (IsPointerActive)
                    ClearPointerState();
                return;
            }

            if (poseAction == null || clickAction == null)
                return;

            if (_pointerData == null && EventSystem.current != null)
                _pointerData = new PointerEventData(EventSystem.current);
            if (_pointerData == null)
                return;

            SteamVR_Input_Sources source = GetInputSource();

            // 从控制器 Pose 获取位置与旋转，并转换到 Tracking/World 空间，
            // 以匹配 Overlay 使用的 Tracking Space。
            Vector3 localPos = poseAction.GetLocalPosition(source);
            Quaternion localRot = poseAction.GetLocalRotation(source);

            var vrcam = SteamVR_Render.Top();
            Transform origin = vrcam != null ? vrcam.origin : null;

            Vector3 pos = origin != null ? origin.TransformPoint(localPos) : localPos;
            Quaternion rot = origin != null ? origin.rotation * localRot : localRot;
            Vector3 dir = rot * Vector3.forward;

            // ComputeIntersection 需要 tracking-space 坐标（内部处理 Unity↔OpenVR Z 翻转）
            Vector3 hitPoint;
            Vector2 uv;
            bool hit = overlayMgr.ComputeIntersection(pos, dir, out hitPoint, out uv);

            if (!hit)
            {
                // 射线未命中 Overlay，清理指针状态
                if (IsPointerActive)
                    ClearPointerState();
                return;
            }

            // OpenVR UV (0,0) = 左上角；Unity Canvas (0,0) = 左下角 → 翻转 Y 轴
            uv = new Vector2(uv.x, 1f - uv.y);

            IsPointerActive = true;
            CurrentUV = uv;

            UpdateCursor(uv, true);

            // ── 将 UV 坐标正确转换为屏幕坐标 ──
            Canvas canvas = graphicRaycaster.GetComponent<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[VRCPinYin.验收] GraphicRaycaster 所在的 Canvas 为 null");
                return;
            }

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            Vector2 canvasSize = canvasRect.rect.size;

            // UV (0,0) = 左下角 → Canvas 局部坐标（中心为原点）
            Vector2 localPosCan = new Vector2(
                (uv.x - 0.5f) * canvasSize.x,
                (uv.y - 0.5f) * canvasSize.y
            );

            // Canvas 局部坐标 → 世界坐标
            Vector3 worldPos = canvasRect.TransformPoint(localPosCan);

            // 世界坐标 → overlayCamera 屏幕坐标
            Vector2 screenPos = overlayCamera.WorldToScreenPoint(worldPos);
            _pointerData.position = screenPos;

            _raycastResults.Clear();
            if (graphicRaycaster != null)
                graphicRaycaster.Raycast(_pointerData, _raycastResults);

            // ── 调试日志：显示所有检测到的 UI 元素 ──
            //if (_raycastResults.Count > 0)
            //{
            //    System.Text.StringBuilder sb = new System.Text.StringBuilder();
            //    sb.AppendLine($"[VRCPinYin.验收] GraphicRaycast 检测到 {_raycastResults.Count} 个元素:");
            //    for (int i = 0; i < _raycastResults.Count; i++)
            //    {
            //        var result = _raycastResults[i];
            //        sb.AppendLine($"  [{i}] {result.gameObject.name} (Depth: {result.depth})");
            //    }
            //    Debug.Log(sb.ToString());
            //}

            // 获取检测到的第一个 GameObject，然后向上查找 Button 或 KeyButton
            GameObject rawTarget = _raycastResults.Count > 0 ? _raycastResults[0].gameObject : null;
            GameObject newTarget = FindClickableParent(rawTarget);

            // ── Hover 状态管理 ──
            if (newTarget != CurrentHoverTarget)
            {
                if (CurrentHoverTarget != null)
                {
                    ExecuteEvents.Execute(CurrentHoverTarget, _pointerData, ExecuteEvents.pointerExitHandler);
                    Debug.Log("[VRCPinYin.验收] PointerExit: " + CurrentHoverTarget.name);
                }
                if (newTarget != null)
                {
                    ExecuteEvents.Execute(newTarget, _pointerData, ExecuteEvents.pointerEnterHandler);
                    Debug.Log("[VRCPinYin.验收] PointerEnter: " + newTarget.name);
                }
                CurrentHoverTarget = newTarget;
            }

            // ── Click 状态管理 ──
            bool isPressed = clickAction.GetState(source);
            bool isDown = clickAction.GetStateDown(source);
            bool isUp = clickAction.GetStateUp(source);

            if (isDown && CurrentHoverTarget != null)
            {
                _pointerData.pointerPressRaycast = _raycastResults.Count > 0 ? _raycastResults[0] : default;
                ExecuteEvents.Execute(CurrentHoverTarget, _pointerData, ExecuteEvents.pointerDownHandler);
                Debug.Log("[VRCPinYin.验收] PointerDown: " + CurrentHoverTarget.name);
                _pressTarget = CurrentHoverTarget;
                _pointerData.pointerPress = _pressTarget;
            }

            if (isUp)
            {
                if (_pressTarget != null)
                {
                    ExecuteEvents.Execute(_pressTarget, _pointerData, ExecuteEvents.pointerUpHandler);

                    if (_pressTarget == CurrentHoverTarget)
                    {
                        ExecuteEvents.Execute(_pressTarget, _pointerData, ExecuteEvents.pointerClickHandler);
                        Debug.Log("[VRCPinYin.验收] PointerClick: " + _pressTarget.name);
                    }
                }
                _pressTarget = null;
                _pointerData.pointerPress = null;
            }

            _wasPressed = isPressed;
        }

        private SteamVR_Input_Sources GetInputSource()
        {
            switch (pointerHand)
            {
                case PointerHand.Right: return SteamVR_Input_Sources.RightHand;
                case PointerHand.Left: return SteamVR_Input_Sources.LeftHand;
                default: return SteamVR_Input_Sources.Any;
            }
        }

        private void ClearPointerState()
        {
            if (CurrentHoverTarget != null && _pointerData != null)
            {
                ExecuteEvents.Execute(CurrentHoverTarget, _pointerData, ExecuteEvents.pointerExitHandler);
                Debug.Log("[VRCPinYin.验收] PointerExit: " + CurrentHoverTarget.name);
            }
            if (_pressTarget != null && _pointerData != null)
            {
                ExecuteEvents.Execute(_pressTarget, _pointerData, ExecuteEvents.pointerUpHandler);
                _pressTarget = null;
            }
            CurrentHoverTarget = null;
            IsPointerActive = false;
            UpdateCursor(Vector2.zero, false);
        }

        private void UpdateCursor(Vector2 uv, bool visible)
        {
            if (cursorImage == null) return;

            bool shouldShow = visible && showCursor;
            if (cursorImage.gameObject.activeSelf != shouldShow)
                cursorImage.gameObject.SetActive(shouldShow);

            if (!shouldShow) return;

            var overlayMgr = Overlay.OverlayManager.Instance;
            if (overlayMgr == null || overlayMgr.overlayTexture == null) return;

            RenderTexture rt = overlayMgr.overlayTexture;

            // UV → Canvas 局部坐标：假设 cursorImage 的父级是 OverlayCanvas 的根 RectTransform
            RectTransform parentRect = cursorImage.parent as RectTransform;
            if (parentRect == null) return;

            Vector2 parentSize = parentRect.rect.size;
            float localX = uv.x * parentSize.x - parentSize.x * parentRect.pivot.x;
            float localY = uv.y * parentSize.y - parentSize.y * parentRect.pivot.y;
            cursorImage.localPosition = new Vector3(localX, localY, 0f);
        }

        /// <summary>
        /// 从指定的 GameObject 开始，向上查找父物体中第一个包含 Button 或 KeyButton 组件的物体。
        /// 如果找不到，返回原始 GameObject。
        /// </summary>
        private GameObject FindClickableParent(GameObject obj)
        {
            if (obj == null) return null;

            Transform current = obj.transform;
            while (current != null)
            {
                // 检查是否有 Button 组件
                if (current.GetComponent<UnityEngine.UI.Button>() != null)
                {
                    return current.gameObject;
                }
                // 检查是否有 KeyButton 组件
                if (current.GetComponent<KeyButton>() != null)
                {
                    return current.gameObject;
                }
                current = current.parent;
            }
            // 如果找不到可点击的父物体，返回原始对象
            return obj;
        }

        private void OnDisable()
        {
            ClearPointerState();
        }
    }
}
