// VRCPinYin - 模块 3：候选词面板
// 挂载在每个候选按钮 GameObject 上，记录页内索引，点击时通知 CandidatesPanelManager。

using UnityEngine;

namespace VRCPinYin.Candidates
{
    public class CandidateButton : MonoBehaviour
    {
        [Tooltip("当前页内的候选索引（0-based），在 Inspector 中按顺序设置 0~5")]
        public int candidateIndex;

        /// <summary>
        /// 由 Unity UI Button.onClick 触发（通过 CandidatesPanelManager 在 Start 中注册）。
        /// </summary>
        public void OnCandidateClicked()
        {
            if (CandidatesPanelManager.Instance == null)
            {
                Debug.LogWarning("[VRCPinYin.验收] CandidateButton.OnCandidateClicked: CandidatesPanelManager.Instance 为 null，无法分发选词事件");
                return;
            }
            CandidatesPanelManager.Instance.HandleCandidateClick(candidateIndex);
        }
    }
}
