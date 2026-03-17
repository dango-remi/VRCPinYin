// VRCPinYin - 模块 2：虚拟键盘 UI
// 挂载在每个按键 GameObject 上，标识键类型与键值，点击时通知 KeyboardManager。

using UnityEngine;

namespace VRCPinYin.Keyboard
{
    public enum KeyType
    {
        Letter,
        Backspace,
        Enter,
        Space,
        ToggleLang,
        Send,
        CopyClip
    }

    public class KeyButton : MonoBehaviour
    {
        [Tooltip("按键类型")]
        public KeyType keyType = KeyType.Letter;

        [Tooltip("字母键的字符值（仅 KeyType.Letter 有效），填小写字母")]
        public char letterValue = '\0';

        /// <summary>
        /// 由 Unity UI Button.onClick 或 VRPointerHandler 的 PointerClick 触发。
        /// </summary>
        public void OnKeyClicked()
        {
            if (KeyboardManager.Instance == null)
            {
                Debug.LogWarning("[VRCPinYin.验收] KeyButton.OnKeyClicked: KeyboardManager.Instance 为 null，无法分发按键事件");
                return;
            }
            KeyboardManager.Instance.HandleKeyPress(this);
        }
    }
}
