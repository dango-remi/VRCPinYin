// VRCPinYin - 模块 2：虚拟键盘 UI
// 单例，收集场景中所有 KeyButton，接收按键点击并通过 event Action 分发给模块 4/5。
// 提供 UpdateIMELabel / UpdateLangState 供模块 4 更新底部功能行。

using System;
using UnityEngine;
using UnityEngine.UI;

namespace VRCPinYin.Keyboard
{
    public class KeyboardManager : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("底部输入法名称标签（Text 组件）")]
        public Text imeLabelText;

        [Tooltip("中/英切换按钮上的文本（Text 组件）")]
        public Text langToggleText;

        [Header("Options")]
        [Tooltip("发送后是否自动隐藏 Overlay")]
        public bool autoHideOnSend;

        // ── 对外事件（模块 4/5 订阅） ──

        public event Action<char> OnLetterKey;
        public event Action OnBackspace;
        public event Action OnEnter;
        public event Action OnSpace;
        public event Action OnToggleLang;
        public event Action OnSendRequested;
        public event Action OnCopyRequested;

        // ── 单例 ──

        public static KeyboardManager Instance { get; private set; }

        private bool _isChinese = true;
        private int _keyCount;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[VRCPinYin.验收] KeyboardManager 已存在，将销毁重复实例。");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            var keys = GetComponentsInChildren<KeyButton>(true);
            _keyCount = keys.Length;

            foreach (var kb in keys)
            {
                var btn = kb.GetComponent<Button>();
                if (btn != null)
                {
                    var captured = kb;
                    btn.onClick.AddListener(() => captured.OnKeyClicked());
                }
            }

            if (imeLabelText != null)
                imeLabelText.text = "输入法";
            if (langToggleText != null)
                langToggleText.text = _isChinese ? "中" : "英";

            Debug.Log("[VRCPinYin.验收] KeyboardManager 初始化完成, 按键数量=" + _keyCount);
        }

        /// <summary>
        /// 由 KeyButton.OnKeyClicked 调用，根据键类型分发事件。
        /// </summary>
        public void HandleKeyPress(KeyButton key)
        {
            switch (key.keyType)
            {
                case KeyType.Letter:
                    char c = char.ToLower(key.letterValue);
                    Debug.Log("[VRCPinYin.验收] OnLetterKey 触发, char='" + c + "'");
                    OnLetterKey?.Invoke(c);
                    break;

                case KeyType.Backspace:
                    Debug.Log("[VRCPinYin.验收] OnBackspace 触发");
                    OnBackspace?.Invoke();
                    break;

                case KeyType.Enter:
                    Debug.Log("[VRCPinYin.验收] OnEnter 触发");
                    OnEnter?.Invoke();
                    break;

                case KeyType.Space:
                    Debug.Log("[VRCPinYin.验收] OnSpace 触发");
                    OnSpace?.Invoke();
                    break;

                case KeyType.ToggleLang:
                    Debug.Log("[VRCPinYin.验收] OnToggleLang 触发");
                    OnToggleLang?.Invoke();
                    break;

                case KeyType.Send:
                    Debug.Log("[VRCPinYin.验收] OnSendRequested 触发");
                    OnSendRequested?.Invoke();
                    if (autoHideOnSend)
                    {
                        var overlay = Overlay.OverlayManager.Instance;
                        if (overlay != null) overlay.Hide();
                    }
                    break;

                case KeyType.CopyClip:
                    Debug.Log("[VRCPinYin.验收] OnCopyRequested 触发");
                    OnCopyRequested?.Invoke();
                    break;
            }
        }

        // ── 供模块 4 调用的显示更新接口 ──

        /// <summary>
        /// 更新底部输入法名称标签。
        /// </summary>
        public void UpdateIMELabel(string imeName)
        {
            Debug.Log("[VRCPinYin.验收] UpdateIMELabel 已调用, imeName='" + imeName + "'");
            if (imeLabelText != null)
                imeLabelText.text = imeName;
        }

        /// <summary>
        /// 更新中/英按钮显示。
        /// </summary>
        public void UpdateLangState(bool isChinese)
        {
            _isChinese = isChinese;
            Debug.Log("[VRCPinYin.验收] UpdateLangState 已调用, isChinese=" + isChinese);
            if (langToggleText != null)
                langToggleText.text = isChinese ? "中" : "英";
        }

        private void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
