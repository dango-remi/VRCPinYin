// VRCPinYin - 模块 3：候选词面板
// 单例，管理输入框与候选词面板的显示更新，触发选词/翻页事件，提供 Mock 模式。

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VRCPinYin.Candidates
{
    public class CandidatesPanelManager : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("输入框文本（Text 组件，需启用 Rich Text）")]
        public Text inputFieldText;

        [Tooltip("候选按钮数组，按顺序拖入 CandidateBtn_1 ~ 6 的 GameObject")]
        public CandidateButton[] candidateButtons;

        [Tooltip("页码指示文本（Text 组件）")]
        public Text pageIndicatorText;

        [Tooltip("上一页按钮")]
        public Button prevPageButton;

        [Tooltip("下一页按钮")]
        public Button nextPageButton;

        [Tooltip("🎙️ 语音按钮（P3 占位）")]
        public Button voiceButton;

        [Header("Options")]
        [Tooltip("每页显示的候选词数量")]
        public int candidatesPerPage = 6;

        [Tooltip("启用 Mock 模式（模块 4 未就绪时勾选）")]
        public bool useMockData;

        [Tooltip("输入框为空时的占位提示文案")]
        public string placeholderText = "请输入拼音...";

        [Tooltip("拼音组合态在输入框中的高亮颜色")]
        public Color pinyinHighlightColor = new Color(0.667f, 0.667f, 1f, 1f); // #AAAAFF

        // ── 对外事件（模块 4 订阅） ──

        public event Action<int> OnCandidateSelected;
        public event Action OnPagePrev;
        public event Action OnPageNext;
        public event Action OnVoiceButtonClicked;

        // ── 单例 ──

        public static CandidatesPanelManager Instance { get; private set; }

        // ── Mock 模式内部状态 ──

        private string _mockPinyin = "";
        private string _mockCommitted = "";
        private List<string> _mockAllCandidates = new List<string>();
        private int _mockCurrentPage = 1;

        // 当前显示的候选词（供 Mock 选词使用）
        private string[] _currentPageCandidates;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[VRCPinYin.验收] CandidatesPanelManager 已存在，将销毁重复实例。");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            int btnCount = candidateButtons != null ? candidateButtons.Length : 0;

            // 注册候选按钮 onClick
            if (candidateButtons != null)
            {
                foreach (var cb in candidateButtons)
                {
                    if (cb == null) continue;
                    var btn = cb.GetComponent<Button>();
                    if (btn != null)
                    {
                        var captured = cb;
                        btn.onClick.AddListener(() => captured.OnCandidateClicked());
                    }
                }
            }

            // 注册翻页按钮
            if (prevPageButton != null)
                prevPageButton.onClick.AddListener(HandlePrevPage);
            if (nextPageButton != null)
                nextPageButton.onClick.AddListener(HandleNextPage);

            // 注册语音按钮
            if (voiceButton != null)
                voiceButton.onClick.AddListener(HandleVoiceButton);

            // 初始化为空状态
            ClearAll();

            Debug.Log("[VRCPinYin.验收] CandidatesPanelManager 初始化完成, 候选按钮数量=" + btnCount + ", useMockData=" + useMockData);

            // Mock 模式：订阅键盘事件
            if (useMockData)
                SubscribeMockEvents();
        }

        // ── 对外显示更新方法（供模块 4 调用） ──

        /// <summary>
        /// 更新输入框显示：已组句文本 + 拼音组合态。
        /// </summary>
        public void UpdateInputField(string committedText, string pinyinComposition)
        {
            Debug.Log("[VRCPinYin.验收] UpdateInputField 已调用, committedText='" + committedText + "', pinyinComposition='" + pinyinComposition + "'");

            if (inputFieldText == null) return;

            if (string.IsNullOrEmpty(committedText) && string.IsNullOrEmpty(pinyinComposition))
            {
                inputFieldText.text = "<color=#888888>" + placeholderText + "</color>";
                return;
            }

            string colorHex = ColorUtility.ToHtmlStringRGB(pinyinHighlightColor);
            string display = committedText ?? "";
            if (!string.IsNullOrEmpty(pinyinComposition))
                display += "<color=#" + colorHex + ">" + pinyinComposition + "</color>";

            inputFieldText.text = display;
        }

        /// <summary>
        /// 更新候选词列表与页码。candidates 为当前页候选词；currentPage/totalPages 均为 1-based。
        /// </summary>
        public void UpdateCandidates(string[] candidates, int currentPage, int totalPages)
        {
            int count = candidates != null ? candidates.Length : 0;
            Debug.Log("[VRCPinYin.验收] UpdateCandidates 已调用, 候选数=" + count + ", currentPage=" + currentPage + ", totalPages=" + totalPages);

            _currentPageCandidates = candidates;
            int shown = 0;
            int hidden = 0;

            if (candidateButtons != null)
            {
                for (int i = 0; i < candidateButtons.Length; i++)
                {
                    if (candidateButtons[i] == null) continue;
                    if (i < count)
                    {
                        candidateButtons[i].gameObject.SetActive(true);
                        var txt = candidateButtons[i].GetComponentInChildren<Text>();
                        if (txt != null)
                            txt.text = (i + 1) + " " + candidates[i];
                        shown++;
                    }
                    else
                    {
                        candidateButtons[i].gameObject.SetActive(false);
                        hidden++;
                    }
                }
            }

            Debug.Log("[VRCPinYin.验收] 候选按钮更新: 显示 " + shown + " 个, 隐藏 " + hidden + " 个");

            UpdatePageIndicator(currentPage, totalPages);
            UpdateNavButtons(currentPage, totalPages);
        }

        /// <summary>
        /// 清空输入框与候选词面板，恢复到初始状态。
        /// </summary>
        public void ClearAll()
        {
            Debug.Log("[VRCPinYin.验收] ClearAll 已调用, 面板已重置");

            _currentPageCandidates = null;

            if (inputFieldText != null)
                inputFieldText.text = "<color=#888888>" + placeholderText + "</color>";

            if (candidateButtons != null)
            {
                foreach (var cb in candidateButtons)
                {
                    if (cb != null)
                        cb.gameObject.SetActive(false);
                }
            }

            UpdatePageIndicator(0, 0);
            UpdateNavButtons(0, 0);
        }

        // ── 按钮事件处理 ──

        /// <summary>
        /// 由 CandidateButton.OnCandidateClicked 调用。
        /// </summary>
        public void HandleCandidateClick(int index)
        {
            Debug.Log("[VRCPinYin.验收] OnCandidateSelected 触发, index=" + index);
            OnCandidateSelected?.Invoke(index);

            if (useMockData)
                MockHandleCandidateSelected(index);
        }

        private void HandlePrevPage()
        {
            Debug.Log("[VRCPinYin.验收] OnPagePrev 触发");
            OnPagePrev?.Invoke();

            if (useMockData)
                MockHandlePrevPage();
        }

        private void HandleNextPage()
        {
            Debug.Log("[VRCPinYin.验收] OnPageNext 触发");
            OnPageNext?.Invoke();

            if (useMockData)
                MockHandleNextPage();
        }

        private void HandleVoiceButton()
        {
            Debug.Log("[VRCPinYin.验收] OnVoiceButtonClicked 触发");
            OnVoiceButtonClicked?.Invoke();
        }

        // ── 内部辅助 ──

        private void UpdatePageIndicator(int currentPage, int totalPages)
        {
            string text = currentPage + "/" + totalPages;
            Debug.Log("[VRCPinYin.验收] 页码指示更新: " + text);

            if (pageIndicatorText != null)
                pageIndicatorText.text = text;
        }

        private void UpdateNavButtons(int currentPage, int totalPages)
        {
            bool prevInteractable = currentPage > 1;
            bool nextInteractable = currentPage < totalPages;

            Debug.Log("[VRCPinYin.验收] 翻页按钮状态更新: prevInteractable=" + prevInteractable + ", nextInteractable=" + nextInteractable);

            if (prevPageButton != null)
                prevPageButton.interactable = prevInteractable;
            if (nextPageButton != null)
                nextPageButton.interactable = nextInteractable;
        }

        // ══════════════════════════════════════════
        //  Mock 模式
        // ══════════════════════════════════════════

        private void SubscribeMockEvents()
        {
            var kb = Keyboard.KeyboardManager.Instance;
            if (kb == null)
            {
                Debug.LogWarning("[VRCPinYin.验收] Mock 模式: KeyboardManager.Instance 为 null，延迟订阅");
                Invoke(nameof(RetrySubscribeMockEvents), 0.1f);
                return;
            }

            kb.OnLetterKey += MockOnLetterKey;
            kb.OnBackspace += MockOnBackspace;
            kb.OnSpace += MockOnSpace;
            kb.OnEnter += MockOnEnter;

            Debug.Log("[VRCPinYin.验收] Mock 模式已启用，已订阅 KeyboardManager 按键事件");
        }

        private void RetrySubscribeMockEvents()
        {
            var kb = Keyboard.KeyboardManager.Instance;
            if (kb == null)
            {
                Debug.LogWarning("[VRCPinYin.验收] Mock 模式: KeyboardManager.Instance 仍为 null，无法订阅");
                return;
            }

            kb.OnLetterKey += MockOnLetterKey;
            kb.OnBackspace += MockOnBackspace;
            kb.OnSpace += MockOnSpace;
            kb.OnEnter += MockOnEnter;

            Debug.Log("[VRCPinYin.验收] Mock 模式已启用，已订阅 KeyboardManager 按键事件");
        }

        private void MockOnLetterKey(char c)
        {
            _mockPinyin += c;
            MockGenerateCandidates();
            _mockCurrentPage = 1;
            MockRefreshDisplay();
        }

        private void MockOnBackspace()
        {
            if (_mockPinyin.Length > 0)
            {
                _mockPinyin = _mockPinyin.Substring(0, _mockPinyin.Length - 1);
                Debug.Log("[VRCPinYin.验收] Mock 退格: 拼音='" + _mockPinyin + "'");
            }
            else
            {
                Debug.Log("[VRCPinYin.验收] Mock 退格: 拼音=''");
            }

            if (_mockPinyin.Length == 0)
            {
                _mockAllCandidates.Clear();
                _mockCurrentPage = 1;
                UpdateCandidates(new string[0], 0, 0);
                UpdateInputField(_mockCommitted, "");
                return;
            }

            MockGenerateCandidates();
            _mockCurrentPage = 1;
            MockRefreshDisplay();
        }

        private void MockOnSpace()
        {
            if (_mockAllCandidates.Count == 0 || string.IsNullOrEmpty(_mockPinyin)) return;

            string firstCandidate = _mockAllCandidates[0];
            _mockCommitted += firstCandidate;
            Debug.Log("[VRCPinYin.验收] Mock 空格选首词: '" + firstCandidate + "', 追加到已组句文本");

            MockClearPinyinAndCandidates();
        }

        private void MockOnEnter()
        {
            if (string.IsNullOrEmpty(_mockPinyin)) return;

            _mockCommitted += _mockPinyin;
            Debug.Log("[VRCPinYin.验收] Mock 回车上屏: 拼音原文='" + _mockPinyin + "', 追加到已组句文本");

            MockClearPinyinAndCandidates();
        }

        private void MockHandleCandidateSelected(int index)
        {
            if (_currentPageCandidates == null || index < 0 || index >= _currentPageCandidates.Length)
                return;

            string selected = _currentPageCandidates[index];
            _mockCommitted += selected;
            Debug.Log("[VRCPinYin.验收] Mock 选词: index=" + index + ", 候选词='" + selected + "', 追加到已组句文本");

            MockClearPinyinAndCandidates();
        }

        private void MockHandlePrevPage()
        {
            if (_mockCurrentPage <= 1) return;
            _mockCurrentPage--;
            MockRefreshDisplay();
        }

        private void MockHandleNextPage()
        {
            int totalPages = MockGetTotalPages();
            if (_mockCurrentPage >= totalPages) return;
            _mockCurrentPage++;
            MockRefreshDisplay();
        }

        private void MockGenerateCandidates()
        {
            _mockAllCandidates.Clear();
            int mockTotal = Mathf.Min(_mockPinyin.Length * 5, 20);
            for (int i = 0; i < mockTotal; i++)
                _mockAllCandidates.Add(_mockPinyin + "词" + (i + 1));
        }

        private void MockRefreshDisplay()
        {
            int totalPages = MockGetTotalPages();
            if (_mockCurrentPage > totalPages)
                _mockCurrentPage = Mathf.Max(totalPages, 1);

            int startIdx = (_mockCurrentPage - 1) * candidatesPerPage;
            int count = Mathf.Min(candidatesPerPage, _mockAllCandidates.Count - startIdx);
            count = Mathf.Max(count, 0);

            string[] pageCandidates = new string[count];
            for (int i = 0; i < count; i++)
                pageCandidates[i] = _mockAllCandidates[startIdx + i];

            UpdateCandidates(pageCandidates, _mockCurrentPage, totalPages);
            UpdateInputField(_mockCommitted, _mockPinyin);
        }

        private void MockClearPinyinAndCandidates()
        {
            _mockPinyin = "";
            _mockAllCandidates.Clear();
            _mockCurrentPage = 1;
            UpdateCandidates(new string[0], 0, 0);
            UpdateInputField(_mockCommitted, "");
        }

        private int MockGetTotalPages()
        {
            if (_mockAllCandidates.Count == 0) return 0;
            return Mathf.CeilToInt((float)_mockAllCandidates.Count / candidatesPerPage);
        }

        // ── 生命周期 ──

        private void OnDisable()
        {
            if (useMockData)
            {
                var kb = Keyboard.KeyboardManager.Instance;
                if (kb != null)
                {
                    kb.OnLetterKey -= MockOnLetterKey;
                    kb.OnBackspace -= MockOnBackspace;
                    kb.OnSpace -= MockOnSpace;
                    kb.OnEnter -= MockOnEnter;
                }
            }

            if (Instance == this)
                Instance = null;
        }
    }
}
