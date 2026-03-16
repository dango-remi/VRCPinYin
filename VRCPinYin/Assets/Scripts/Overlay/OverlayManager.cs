// VRCPinYin - 模块 1：Overlay 框架
// 负责 SteamVR Overlay 的创建、显示/隐藏、位置与纹理提交，以及射线相交供模块 2 使用。

using UnityEngine;
using Valve.VR;

namespace VRCPinYin.Overlay
{
    public class OverlayManager : MonoBehaviour
    {
        public const string OverlayKey = "vrcpinyin.input";
        public const string OverlayName = "VRCPinYin";

        [Header("Rendering")]
        [Tooltip("相机渲染的 RenderTexture，将作为 Overlay 纹理")]
        public RenderTexture overlayTexture;

        [Header("Layout")]
        [Tooltip("Overlay 与用户/头显的距离（米）")]
        [Range(0.5f, 3f)]
        public float distance = 1.25f;

        [Tooltip("Overlay 物理宽度（米）")]
        [Range(0.3f, 2f)]
        public float widthInMeters = 1f;

        [Tooltip("排序值越大越靠前，建议 100～500 浮在游戏之上")]
        public uint sortOrder = 200;

        [Tooltip("Overlay 透明度，0～1")]
        [Range(0f, 1f)]
        public float alpha = 1f;

        [Header("Input (SteamVR Action)")]
        [Tooltip("用于唤起/关闭 Overlay 的按键，例如左手柄 Grip。若为空则不响应快捷键")]
        public SteamVR_Action_Boolean toggleAction;

        public static OverlayManager Instance { get; private set; }

        public bool IsVisible => _visible;

        private ulong _handle = OpenVR.k_ulOverlayHandleInvalid;
        private bool _visible;
        private Texture_t _textureData;
        private bool _textureDataValid;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[VRCPinYin.验收] OverlayManager 已存在，将销毁重复实例。");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (OpenVR.Overlay == null)
            {
                Debug.LogError("[VRCPinYin.验收] OpenVR.Overlay 不可用，请确保 SteamVR 已初始化且 Application Type 为 Overlay。");
                return;
            }

            var err = OpenVR.Overlay.CreateOverlay(OverlayKey, OverlayName, ref _handle);
            if (err != EVROverlayError.None)
            {
                Debug.LogError("[VRCPinYin.验收] CreateOverlay 失败: " + OpenVR.Overlay.GetOverlayErrorNameFromEnum(err));
                return;
            }
            Debug.Log("[VRCPinYin.验收] Overlay 创建成功, handle=" + _handle);

            var bounds = new VRTextureBounds_t { uMin = 0, uMax = 1, vMin = 1, vMax = 0 };
            OpenVR.Overlay.SetOverlayTextureBounds(_handle, ref bounds);
            OpenVR.Overlay.SetOverlayWidthInMeters(_handle, widthInMeters);
            OpenVR.Overlay.SetOverlaySortOrder(_handle, sortOrder);
            OpenVR.Overlay.SetOverlayAlpha(_handle, alpha);
            OpenVR.Overlay.SetOverlayInputMethod(_handle, VROverlayInputMethod.None);

            if (SteamVR.instance != null && overlayTexture != null)
            {
                _textureData = new Texture_t
                {
                    handle = overlayTexture.GetNativeTexturePtr(),
                    eType = SteamVR.instance.textureType,
                    eColorSpace = EColorSpace.Auto
                };
                _textureDataValid = true;
            }
            else if (overlayTexture == null)
            {
                Debug.LogWarning("[VRCPinYin.验收] 未指定 overlayTexture，Overlay 将无法显示内容。");
            }
        }

        private void Update()
        {
            if (_handle == OpenVR.k_ulOverlayHandleInvalid) return;

            if (toggleAction != null && toggleAction.GetStateDown(SteamVR_Input_Sources.Any))
                Toggle();

            if (_visible)
                UpdateOverlayTransformAndTexture();
        }

        private void UpdateOverlayTransformAndTexture()
        {
            var overlay = OpenVR.Overlay;
            if (overlay == null) return;

            var showErr = overlay.ShowOverlay(_handle);
            if (showErr == EVROverlayError.InvalidHandle || showErr == EVROverlayError.UnknownOverlay)
            {
                Debug.Log("[VRCPinYin.验收] ShowOverlay 返回 " + showErr + "，尝试 FindOverlay 恢复");
                if (overlay.FindOverlay(OverlayKey, ref _handle) != EVROverlayError.None)
                {
                    Debug.LogWarning("[VRCPinYin.验收] FindOverlay 恢复失败");
                    return;
                }
                Debug.Log("[VRCPinYin.验收] FindOverlay 恢复成功, 新 handle=" + _handle);
            }

            SteamVR_Utils.RigidTransform t;
            var vrcam = SteamVR_Render.Top();
            if (vrcam != null && vrcam.origin != null)
            {
                var offset = new SteamVR_Utils.RigidTransform(vrcam.origin, transform);
                offset.pos.x /= vrcam.origin.localScale.x;
                offset.pos.y /= vrcam.origin.localScale.y;
                offset.pos.z /= vrcam.origin.localScale.z;
                offset.pos.z += distance;
                t = offset;
            }
            else
            {
                t = GetOverlayTransformFromHmd();
            }

            var m = t.ToHmdMatrix34();
            overlay.SetOverlayTransformAbsolute(_handle, SteamVR.settings.trackingSpace, ref m);

            if (_textureDataValid && overlayTexture != null)
            {
                _textureData.handle = overlayTexture.GetNativeTexturePtr();
                overlay.SetOverlayTexture(_handle, ref _textureData);
            }
            overlay.SetOverlayAlpha(_handle, alpha);
        }

        private SteamVR_Utils.RigidTransform GetOverlayTransformFromHmd()
        {
            var compositor = OpenVR.Compositor;
            if (compositor == null)
                return new SteamVR_Utils.RigidTransform(transform);

            var pose = new TrackedDevicePose_t();
            var gamePose = new TrackedDevicePose_t();
            if (compositor.GetLastPoseForTrackedDeviceIndex(OpenVR.k_unTrackedDeviceIndex_Hmd, ref pose, ref gamePose) != EVRCompositorError.None)
                return new SteamVR_Utils.RigidTransform(transform);

            if (!gamePose.bPoseIsValid)
                return new SteamVR_Utils.RigidTransform(transform);

            var hmd = new SteamVR_Utils.RigidTransform(gamePose.mDeviceToAbsoluteTracking);
            var overlayPos = hmd.pos + hmd.rot * Vector3.forward * distance;
            var overlayRot = hmd.rot;
            return new SteamVR_Utils.RigidTransform(overlayPos, overlayRot);
        }

        public void Show()
        {
            if (OpenVR.Overlay == null || _handle == OpenVR.k_ulOverlayHandleInvalid) return;
            // 文档 2.3：纹理未设置时视为未就绪，不执行 Show
            if (!_textureDataValid || overlayTexture == null)
            {
                Debug.Log("[VRCPinYin.验收] Show() 纹理未就绪，未执行 ShowOverlay");
                return;
            }
            OpenVR.Overlay.ShowOverlay(_handle);
            _visible = true;
            Debug.Log("[VRCPinYin.验收] Show() 已调用, IsVisible=true");
        }

        public void Hide()
        {
            if (OpenVR.Overlay == null || _handle == OpenVR.k_ulOverlayHandleInvalid) return;
            OpenVR.Overlay.HideOverlay(_handle);
            _visible = false;
            Debug.Log("[VRCPinYin.验收] Hide() 已调用, IsVisible=false");
        }

        public void Toggle()
        {
            Debug.Log("[VRCPinYin.验收] Toggle() 已调用, 当前 IsVisible=" + _visible + " -> 将切换为 " + !_visible);
            if (_visible) Hide(); else Show();
        }

        /// <summary>
        /// 计算射线与 Overlay 平面的交点，供模块 2（键盘/候选词）做点击判定。
        /// </summary>
        /// <param name="source">射线起点（世界空间）</param>
        /// <param name="direction">射线方向（世界空间，无需归一化）</param>
        /// <param name="point">交点（世界空间）</param>
        /// <param name="uv">Overlay 纹理 UV (0~1)</param>
        /// <returns>是否相交</returns>
        public bool ComputeIntersection(Vector3 source, Vector3 direction, out Vector3 point, out Vector2 uv)
        {
            point = Vector3.zero;
            uv = Vector2.zero;
            if (OpenVR.Overlay == null || _handle == OpenVR.k_ulOverlayHandleInvalid) return false;

            var input = new VROverlayIntersectionParams_t
            {
                eOrigin = SteamVR.settings.trackingSpace,
                vSource = new HmdVector3_t { v0 = source.x, v1 = source.y, v2 = -source.z },
                vDirection = new HmdVector3_t { v0 = direction.x, v1 = direction.y, v2 = -direction.z }
            };
            var output = new VROverlayIntersectionResults_t();
            bool hit = OpenVR.Overlay.ComputeOverlayIntersection(_handle, ref input, ref output);
            if (!hit)
            {
                Debug.Log("[VRCPinYin.验收] ComputeIntersection 已调用, 结果 hit=false");
                return false;
            }
            point = new Vector3(output.vPoint.v0, output.vPoint.v1, -output.vPoint.v2);
            uv = new Vector2(output.vUVs.v0, output.vUVs.v1);
            Debug.Log("[VRCPinYin.验收] ComputeIntersection 已调用, 结果 hit=true, point=" + point + ", uv=" + uv);
            return true;
        }

        /// <summary>
        /// 同上，返回更多相交信息（距离、法线等），便于模块 2 扩展使用。
        /// </summary>
        public bool ComputeIntersection(Vector3 source, Vector3 direction, out Vector3 point, out Vector3 normal, out Vector2 uv, out float hitDistance)
        {
            point = Vector3.zero;
            normal = Vector3.forward;
            uv = Vector2.zero;
            hitDistance = 0f;
            if (OpenVR.Overlay == null || _handle == OpenVR.k_ulOverlayHandleInvalid) return false;

            var input = new VROverlayIntersectionParams_t
            {
                eOrigin = SteamVR.settings.trackingSpace,
                vSource = new HmdVector3_t { v0 = source.x, v1 = source.y, v2 = -source.z },
                vDirection = new HmdVector3_t { v0 = direction.x, v1 = direction.y, v2 = -direction.z }
            };
            var output = new VROverlayIntersectionResults_t();
            bool hit = OpenVR.Overlay.ComputeOverlayIntersection(_handle, ref input, ref output);
            if (!hit)
            {
                Debug.Log("[VRCPinYin.验收] ComputeIntersection(含 distance) 已调用, hit=false");
                return false;
            }
            point = new Vector3(output.vPoint.v0, output.vPoint.v1, -output.vPoint.v2);
            normal = new Vector3(output.vNormal.v0, output.vNormal.v1, -output.vNormal.v2);
            uv = new Vector2(output.vUVs.v0, output.vUVs.v1);
            hitDistance = output.fDistance;
            Debug.Log("[VRCPinYin.验收] ComputeIntersection(含 distance) 已调用, hit=true, distance=" + hitDistance + ", uv=" + uv);
            return true;
        }

        private void OnDisable()
        {
            if (OpenVR.Overlay != null && _handle != OpenVR.k_ulOverlayHandleInvalid)
            {
                OpenVR.Overlay.DestroyOverlay(_handle);
                Debug.Log("[VRCPinYin.验收] OnDisable: DestroyOverlay 已调用, handle 已置为 Invalid");
                _handle = OpenVR.k_ulOverlayHandleInvalid;
            }

            if (Instance == this)
                Instance = null;
        }
    }
}
