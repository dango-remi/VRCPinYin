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

            Vector3 pos = poseAction.GetLocalPosition(source);
            Quaternion rot = poseAction.GetLocalRotation(source);
            Vector3 dir = rot * Vector3.forward;

            // 坐标转换：SteamVR tracking space → Unity world（需与 OverlayManager 一致）
            Vector3 worldPos = pos;
            Vector3 worldDir = dir;
            var vrcam = SteamVR_Render.Top();
            if (vrcam != null && vrcam.origin != null)
            {
                worldPos = vrcam.origin.TransformPoint(pos);
                worldDir = vrcam.origin.TransformDirection(dir);
            }

            Vector3 hitPoint;
            Vector2 uv;
            bool hit = overlayMgr.ComputeIntersection(worldPos, worldDir, out hitPoint, out uv);

            if (!hit)
            {
                if (IsPointerActive)
                    ClearPointerState();
                return;
            }

            IsPointerActive = true;
            CurrentUV = uv;

            UpdateCursor(uv, true);

            RenderTexture rt = overlayMgr.overlayTexture;
            if (rt == null) return;

            Vector2 pixelPos = new Vector2(uv.x * rt.width, uv.y * rt.height);
            _pointerData.position = pixelPos;

            _raycastResults.Clear();
            if (graphicRaycaster != null)
                graphicRaycaster.Raycast(_pointerData, _raycastResults);

            GameObject newTarget = _raycastResults.Count > 0 ? _raycastResults[0].gameObject : null;

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

        private void OnDisable()
        {
            ClearPointerState();
        }
    }
}
