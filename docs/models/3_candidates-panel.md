# 模块 3：候选词面板

> 状态：✅ 已细化
> 依赖：模块 1（Overlay 框架）、模块 2（虚拟键盘 UI，提供 VR 指针交互）

---

## 概述

本文档描述候选词面板的详细设计。本模块负责：输入框区域（已组句文本 + 当前拼音组合态显示）、候选词列表展示与选择、翻页、🎙️ 语音入口（P3 占位）。

候选词数据由模块 4（输入法引擎）在进程内直接提供，无需网络。VR 指针交互由模块 2（虚拟键盘 UI）的 `VRPointerHandler` 统一处理——本模块的按钮作为 OverlayCanvas 上的标准 Unity UI Button，自然接收模块 2 发送的 Pointer 事件，**无需实现自己的射线检测逻辑**。

模块 4 未就绪时，本模块提供 **Mock 模式**：订阅模块 2 的按键事件，使用预设假数据生成候选词，可独立验收面板显示、选词、翻页等功能。

---

## 1. 输入框区域

### 1.1 布局

参照 [README UI 示意图](../../README.md#ui-示意图)，输入框区域位于 Overlay 最上方：

```
┌───────────────────────────────────────────┐  ┌────┐
│ 你好，wo...                               │  │ 🎙️ │
└───────────────────────────────────────────┘  └────┘
```

- **输入框文本**（只读 Text）：显示已组句文本 + 当前拼音组合态。已组句部分为正常颜色，拼音部分使用 Rich Text 高亮（如 `<color=#AAAAFF>wo</color>`），帮助用户区分已确认文字与正在输入的拼音。
- **🎙️ 语音按钮**：位于输入框右侧，P3 功能占位。点击时触发 `OnVoiceButtonClicked` 事件，当前版本不做实际语音处理，仅打 Log。

### 1.2 已组句文本与拼音组合态

- **已组句文本**：用户通过选词确认的汉字序列（如 "你好，"），由模块 4 维护并通过 `UpdateInputField` 传入。
- **拼音组合态**：用户当前正在键入的拼音字符串（如 "wo"），尚未选词确认，同样由模块 4 通过 `UpdateInputField` 传入。
- **显示规则**：输入框 Text 将两者拼接显示，拼音部分使用 Rich Text 以配置的高亮颜色（`pinyinHighlightColor`）包裹以示区别。当两者均为空时，显示占位提示文案（如淡灰色 "请输入拼音..."）。

### 1.3 🎙️ 语音按钮（P3）

- 按钮上显示 🎙️ 图标或文字，位于输入框右侧。
- 点击触发 `OnVoiceButtonClicked` 事件，当前版本仅打 Log；后续模块实现语音转文字时订阅此事件。
- 按钮始终可见，但当前版本无实际功能。

---

## 2. 候选词列表

### 2.1 布局

参照 [README UI 示意图](../../README.md#ui-示意图)，候选词区域位于输入框下方、键盘上方：

```
[ 1 你 ] [ 2 泥 ] [ 3 尼 ] [ 4 妮 ] [ 5 拟 ] [ 6 逆 ]
◀ 上一页                   1/3                  下一页 ▶
```

- **候选按钮行**：水平排列，每页默认显示 **6** 个候选词（可配置 `candidatesPerPage`）。每个按钮上显示 "[序号] [候选词]"，序号为当前页内的 1-based 编号。
- **翻页行**：◀ 上一页按钮 + 页码指示文本（如 "1/3"）+ 下一页 ▶ 按钮。

### 2.2 候选按钮

- 每个候选按钮使用**预创建**方式：场景中固定放置 `candidatesPerPage` 个按钮（默认 6 个），运行时根据实际候选数量显示或隐藏多余按钮。
- 每个按钮 GameObject 上挂载 `CandidateButton` 组件，记录该按钮在当前页中的**索引**（0-based）。
- 当 `CandidatesPanelManager.UpdateCandidates` 被调用时：
  - 按候选数组顺序更新每个按钮的文本（"[index+1] [candidate]"）。
  - 候选数量不足 `candidatesPerPage` 时，多余按钮设为不激活（`SetActive(false)`）。
  - 候选数量为 0 时，所有候选按钮隐藏。
- 用户点击候选按钮时，`CandidateButton` 通知 `CandidatesPanelManager`，后者触发 `OnCandidateSelected(int index)` 事件（index 为当前页内 0-based 索引），由模块 4 订阅处理。

### 2.3 空状态处理

- 当无候选词（拼音为空或无匹配）时：所有候选按钮隐藏，翻页按钮禁用（`Interactable = false`），页码显示为空或 "0/0"。
- 候选行的父容器保持可见（保持布局稳定），仅子元素根据数据状态显示/隐藏/禁用。

---

## 3. 翻页功能

### 3.1 上一页/下一页

- **上一页按钮（◀）**：点击时触发 `OnPagePrev` 事件，模块 4 收到后查询上一页候选词并调用 `UpdateCandidates` 刷新显示。
- **下一页按钮（▶）**：点击时触发 `OnPageNext` 事件，处理同上。
- 翻页按钮作为标准 Unity UI Button，由模块 2 的 VR 指针自然驱动。

### 3.2 页码指示

- 在上一页与下一页按钮之间显示页码文本，格式为 **"当前页/总页数"**（如 "2/5"）。
- 由 `UpdateCandidates` 传入的 `currentPage` 和 `totalPages` 参数更新。

### 3.3 边界处理

- **第一页**时：上一页按钮设为不可交互（`Interactable = false`），视觉上呈 Disabled 状态。
- **最后一页**时：下一页按钮同样设为不可交互。
- **仅一页或无候选**时：两个翻页按钮均不可交互。

---

## 4. 与其他模块的协作

### 4.1 与模块 4（输入法引擎）的数据流

```
模块 2 (键盘按键事件)
    │
    ▼
模块 4 (输入法引擎)
    │  更新拼音 → 查询候选词
    │
    ├──► 调用 CandidatesPanelManager.UpdateCandidates(candidates, page, totalPages)
    ├──► 调用 CandidatesPanelManager.UpdateInputField(committedText, pinyinComposition)
    │
    ◄── 订阅 CandidatesPanelManager.OnCandidateSelected / OnPagePrev / OnPageNext
    │  处理选词 → 更新组句 → 再次调用 UpdateCandidates / UpdateInputField
```

- **模块 3 不维护候选词数据或拼音状态**，仅负责 UI 展示与用户交互事件转发。
- 模块 4 是候选词数据的唯一来源；模块 3 是显示层。

### 4.2 与模块 2（虚拟键盘 UI）的关系

- 本模块的所有按钮（候选词、翻页、🎙️ 语音）均为 OverlayCanvas 上的标准 Unity UI Button，由模块 2 的 `VRPointerHandler` 统一处理射线→Pointer 事件，**本模块无需任何射线检测代码**。
- 候选词面板与键盘面板共用同一 OverlayCanvas 与同一 `GraphicRaycaster`。

### 4.3 Mock 模式（模块 4 未就绪时）

为支持模块 3 在模块 4 实现前独立验收，`CandidatesPanelManager` 提供 **Mock 模式**（`useMockData` 开关）：

- 启用后，`CandidatesPanelManager` 内部订阅 `KeyboardManager` 的按键事件（`OnLetterKey`、`OnBackspace`、`OnSpace`、`OnEnter`）。
- **字母键**：追加到内部 mock 拼音字符串，根据拼音生成预设假候选词（如 "候选1"、"候选2" ... "候选N"），调用自身的 `UpdateCandidates` 与 `UpdateInputField` 刷新显示。Mock 候选总数可设为 15~20 个以验证翻页。
- **退格键**：删除 mock 拼音最后一个字符，重新生成候选词。拼音为空时清空候选。
- **空格键**：模拟选中首个候选词上屏——将第一个候选词追加到 mock 已组句文本，清空拼音与候选词。
- **回车键**：将当前 mock 拼音原文追加到 mock 已组句文本，清空拼音与候选词。
- Mock 模式下选词（点击候选按钮）同样由 `OnCandidateSelected` 内部处理，将对应候选词追加到已组句文本。
- 当模块 4 就绪后，关闭 `useMockData`，由模块 4 接管按键处理与候选数据提供。

---

## 5. Unity 场景结构

### 5.1 Canvas 层级

在模块 1 已建立的 OverlayCanvas 下、模块 2 已创建的 KeyboardPanel 之前，本模块添加输入框与候选词面板：

```
[OverlayCanvas]                             // (模块 1 已有, World Space)
├── InputFieldRow                           // ★ 本模块
│   ├── InputFieldText                      // Text（只读）：已组句文本 + 拼音组合态
│   └── VoiceButton                         // Button：🎙️（P3 占位）
├── CandidatesRow                           // ★ 本模块，挂 CandidatesPanelManager
│   ├── CandidateButtonsRow                 // HorizontalLayoutGroup
│   │   ├── CandidateBtn_1                  // Button + CandidateButton(index=0)
│   │   ├── CandidateBtn_2                  // Button + CandidateButton(index=1)
│   │   ├── CandidateBtn_3                  // Button + CandidateButton(index=2)
│   │   ├── CandidateBtn_4                  // Button + CandidateButton(index=3)
│   │   ├── CandidateBtn_5                  // Button + CandidateButton(index=4)
│   │   └── CandidateBtn_6                  // Button + CandidateButton(index=5)
│   └── CandidateNavRow                     // HorizontalLayoutGroup
│       ├── Btn_PrevPage                    // Button：◀ 上一页
│       ├── PageIndicator                   // Text：页码 "1/3"
│       └── Btn_NextPage                    // Button：下一页 ▶
├── KeyboardPanel                           // (模块 2 已有)
│   └── ...
└── Cursor_Image                            // (模块 2 已有)
```

### 5.2 代码产出目录

本模块脚本放在 **Assets/Scripts/Candidates/**：

| 脚本 | 职责 |
|------|------|
| `CandidatesPanelManager.cs` | 单例，管理输入框与候选词面板的显示更新，触发选词/翻页事件，提供 Mock 模式 |
| `CandidateButton.cs` | 挂载在每个候选按钮 GameObject 上，记录页内索引，点击时通知 `CandidatesPanelManager` |

### 5.3 场景搭建指引

以下步骤基于模块 2 验收后的场景（已有 OverlayRig / OverlayCamera / OverlayCanvas / KeyboardPanel / VRPointerHandler / Cursor_Image）。若尚未搭建，请先按 [2_keyboard-ui.md 验收流程](./2_keyboard-ui.md#9-验收流程) 完成。

#### 步骤 1：创建 InputFieldRow

1. 在 **OverlayCanvas** 下（`KeyboardPanel` **之前**）右键 → **UI → Panel**，重命名为 `InputFieldRow`。
2. 调整在 Hierarchy 中的顺序，确保 `InputFieldRow` 排在 `CandidatesRow`（下一步创建）和 `KeyboardPanel` 之前。
3. 选中 `InputFieldRow`，在 Inspector 中：
   - **RectTransform**：Anchor 设为顶部拉伸（Stretch-Top），Height 约 60~80（根据字号调整），使输入框位于 Canvas 顶部。
   - **Image**：设置半透明深色背景（如 `RGBA(20, 20, 20, 220)`）。
   - 添加 **Horizontal Layout Group**：Child Alignment = Middle Left，Spacing = 8，Padding 适当（如 Left 10, Right 10）。Control Child Size 勾选 Height，勾选 Width。
4. 在 `InputFieldRow` 下创建子物体：
   - **InputFieldText**：右键 → **UI → Text**，重命名为 `InputFieldText`。Font Size 26~32，颜色白色，**Rich Text 勾选**（用于拼音高亮）。Alignment 为 Left-Center。添加 **Layout Element**，Flexible Width = 1（占据剩余空间）。**Raycast Target 取消勾选**（文本区域不需要响应射线）。
   - **VoiceButton**：右键 → **UI → Button**，重命名为 `VoiceButton`。子 Text 设为 `🎙️` 或 `语音`。添加 **Layout Element**，Preferred Width = 60~80，使按钮为固定宽度。

#### 步骤 2：创建 CandidatesRow

1. 在 **OverlayCanvas** 下（`InputFieldRow` 之后、`KeyboardPanel` 之前）右键 → **UI → Panel**，重命名为 `CandidatesRow`。
2. 选中 `CandidatesRow`，在 Inspector 中：
   - **RectTransform**：位于 InputFieldRow 下方。Height 约 100~120（含候选按钮行 + 翻页行）。
   - **Image**：设置半透明深色背景（如 `RGBA(25, 25, 25, 200)`），与输入框略有区分。
   - 添加 **Vertical Layout Group**：Child Alignment = Upper Center，Spacing = 4，Control Child Size 勾选 Width + Height。
3. 在 `CandidatesRow` 上添加 **CandidatesPanelManager** 脚本组件（`Assets/Scripts/Candidates/CandidatesPanelManager.cs`）。

#### 步骤 3：创建候选按钮行（CandidateButtonsRow）

1. 在 `CandidatesRow` 下创建空 GameObject，命名 `CandidateButtonsRow`。
2. 添加 **Horizontal Layout Group**：Child Alignment = Middle Center，Spacing = 6，Control Child Size 勾选 Width + Height。
3. 添加 **Layout Element**：Preferred Height = 50~60。
4. 在 `CandidateButtonsRow` 下依次创建 6 个 Button：

   | 物体名 | Button 子 Text（占位） | CandidateButton.candidateIndex |
   |--------|----------------------|-------------------------------|
   | CandidateBtn_1 | 1 候选 | 0 |
   | CandidateBtn_2 | 2 候选 | 1 |
   | CandidateBtn_3 | 3 候选 | 2 |
   | CandidateBtn_4 | 4 候选 | 3 |
   | CandidateBtn_5 | 5 候选 | 4 |
   | CandidateBtn_6 | 6 候选 | 5 |

   每个 Button 的设置：
   - **Button** 组件：Transition 保持 **Color Tint**，Navigation 设为 **None**。
   - 子 **Text**：占位文案（如 "1 候选"），Font Size 22~26，居中，白色。
   - 添加 **CandidateButton** 脚本组件（`Assets/Scripts/Candidates/CandidateButton.cs`），设置 `candidateIndex`。
   - 添加 **Layout Element**：Flexible Width = 1（均分宽度）。

#### 步骤 4：创建翻页行（CandidateNavRow）

1. 在 `CandidatesRow` 下（`CandidateButtonsRow` 之后）创建空 GameObject，命名 `CandidateNavRow`。
2. 添加 **Horizontal Layout Group**：Child Alignment = Middle Center，Spacing = 10，Control Child Size 勾选 Width + Height。
3. 添加 **Layout Element**：Preferred Height = 36~44。
4. 在 `CandidateNavRow` 下依次创建：
   - **Btn_PrevPage**：UI → Button，子 Text 设为 `◀ 上一页`，Font Size 20。添加 Layout Element，Preferred Width = 120。
   - **PageIndicator**：UI → Text，文本 `0/0`（运行时由代码更新），Font Size 20，居中，浅灰色。**Raycast Target 取消勾选**。添加 Layout Element，Flexible Width = 1。
   - **Btn_NextPage**：UI → Button，子 Text 设为 `下一页 ▶`，Font Size 20。添加 Layout Element，Preferred Width = 120。

#### 步骤 5：配置 CandidatesPanelManager Inspector

选中 `CandidatesRow`（挂载了 CandidatesPanelManager），在 Inspector 中：

| 字段 | 赋值 |
|------|------|
| **Input Field Text** | 拖入 `InputFieldRow/InputFieldText` 的 Text 组件 |
| **Candidate Buttons** | 拖入 6 个 CandidateBtn 物体（数组，按顺序 1~6） |
| **Page Indicator Text** | 拖入 `CandidateNavRow/PageIndicator` 的 Text 组件 |
| **Prev Page Button** | 拖入 `Btn_PrevPage` 的 Button 组件 |
| **Next Page Button** | 拖入 `Btn_NextPage` 的 Button 组件 |
| **Voice Button** | 拖入 `VoiceButton` 的 Button 组件 |
| **Use Mock Data** | 勾选（模块 4 未就绪时用于测试；就绪后取消勾选） |
| **Placeholder Text** | `请输入拼音...` |
| **Pinyin Highlight Color** | 拼音高亮颜色（如 `#AAAAFF`，浅蓝色） |

#### 步骤 6：确认层级与顺序

搭建完成后，OverlayCanvas 下的层级应为（加粗为本步骤新增）：

```
[OverlayCanvas]                                 // 模块 1
├── **InputFieldRow**                           // ★ 新增
│   ├── **InputFieldText**                      // ★ Text（只读, Rich Text）
│   └── **VoiceButton**                         // ★ Button（🎙️）
├── **CandidatesRow**                           // ★ 新增，挂 CandidatesPanelManager
│   ├── **CandidateButtonsRow**                 // ★ 6 个候选按钮
│   │   └── CandidateBtn_1 ~ CandidateBtn_6
│   └── **CandidateNavRow**                     // ★ 翻页行
│       ├── Btn_PrevPage
│       ├── PageIndicator
│       └── Btn_NextPage
├── KeyboardPanel                               // 模块 2（已有）
│   └── Row_QWERTY / Row_ASDF / Row_ZXCV / Row_Bottom
├── Cursor_Image                                // 模块 2（已有）
├── VRPointerHandler                            // 模块 2（已有）
├── [OverlayManager]                            // 模块 1（已有）
└── [EventSystem]                               // 已有
```

确保 `InputFieldRow` 在最上方、`CandidatesRow` 次之、`KeyboardPanel` 在下方，使 UI 自上而下排列为：输入框 → 候选词 → 键盘。

---

## 6. 配置项

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| candidatesPerPage | int | 6 | 每页显示的候选词数量（与场景中预创建的候选按钮数量一致） |
| useMockData | bool | false | 是否启用 Mock 模式（模块 4 未就绪时勾选） |
| placeholderText | string | "请输入拼音..." | 输入框为空时的占位提示文案 |
| pinyinHighlightColor | Color | #AAAAFF | 拼音组合态在输入框中的 Rich Text 高亮颜色 |

---

## 7. API 设计

### 7.1 对外接口（供模块 4 使用）

本模块通过 `CandidatesPanelManager` 单例对外提供以下能力：

**事件（模块 4 订阅）：**

| 接口 | 说明 |
|------|------|
| **event Action\<int\> OnCandidateSelected** | 用户点击候选词，参数为当前页内 0-based 索引 |
| **event Action OnPagePrev** | 用户点击上一页 |
| **event Action OnPageNext** | 用户点击下一页 |
| **event Action OnVoiceButtonClicked** | 用户点击 🎙️ 语音按钮（P3 占位） |

**显示更新方法（模块 4 调用）：**

| 接口 | 说明 |
|------|------|
| **void UpdateCandidates(string[] candidates, int currentPage, int totalPages)** | 更新候选词列表与页码。`candidates` 为当前页的候选词数组（长度 ≤ `candidatesPerPage`）；`currentPage` 和 `totalPages` 均为 1-based |
| **void UpdateInputField(string committedText, string pinyinComposition)** | 更新输入框显示：已组句文本 + 拼音组合态 |
| **void ClearAll()** | 清空输入框与候选词面板，恢复到初始状态（显示占位文案、隐藏候选按钮、翻页按钮禁用） |

### 7.2 依赖接口（本模块调用其他模块）

| 来源 | 接口 | 用途 |
|------|------|------|
| 模块 2 | `KeyboardManager.Instance.OnLetterKey` 等事件 | Mock 模式下订阅按键事件生成假候选词 |
| 模块 2 | VR 指针交互（通过 Unity EventSystem） | 候选词/翻页/语音按钮自动接收 Pointer 事件，无需显式调用 |

### 7.3 与模块 4 的协作

- 模块 4 订阅 `OnCandidateSelected` / `OnPagePrev` / `OnPageNext`，处理选词与翻页逻辑。
- 模块 4 调用 `UpdateCandidates` / `UpdateInputField` / `ClearAll` 刷新 UI。
- 模块 3 **不维护**候选词源数据或拼音/组句状态，仅作为显示层与交互转发层。

### 7.4 与模块 2 的关系

- VR 指针交互由模块 2 的 `VRPointerHandler` 统一提供。本模块的候选词按钮、翻页按钮、🎙️ 语音按钮均为 OverlayCanvas 上的标准 Unity UI Button，**自然接收** Pointer 事件（PointerEnter / Exit / Down / Up / Click），无需额外的射线检测代码。
- 本模块只需关注按钮的 `onClick` 回调。

---

## 8. 验收标准

满足以下条件时，视为本模块通过验收：

1. **输入框显示**：输入框能正确显示已组句文本与当前拼音组合态；拼音部分有视觉区分（高亮颜色）；为空时显示占位提示文案。
2. **候选词展示**：调用 `UpdateCandidates` 后，候选词按钮按序显示对应文字与编号；候选数量不足每页上限时多余按钮隐藏；无候选时全部隐藏。
3. **选词交互**：用手柄射线点击候选词按钮，能正确触发 `OnCandidateSelected` 事件并携带正确的页内索引（模块 4 未就绪时可通过 Log 确认事件已触发及索引值）。
4. **翻页功能**：点击上一页/下一页按钮，能触发 `OnPagePrev` / `OnPageNext` 事件；页码指示正确更新；首页时上一页不可交互，末页时下一页不可交互。
5. **Mock 模式**：启用 Mock 模式后，通过键盘输入拼音字母，候选词面板能自动显示 mock 候选词；选词、退格、翻页均能正常工作。
6. **🎙️ 语音按钮**：按钮可见、可点击，点击后触发 `OnVoiceButtonClicked` 事件（通过 Log 确认）。
7. **ClearAll**：调用后输入框与候选面板恢复初始状态。

**总验收说明**：当本模块与其余 4 个模块均完成并通过各自验收后，可按 [README 中的「工作流程」](../../README.md#工作流程) 进行端到端总验收（唤起 → 输入 → 处理 → 展示 → 选择 → 输出）。

---

## 9. 验收流程

遵循 [docs/models/.prompt.md 验收流程](.prompt.md#3-验收流程)：用户按下列**操作步骤**执行后，先做**人工观察**，再根据 **Log 验收清单** 读 Log 逐项打勾。实现侧须在对应逻辑中打 Log（前缀 `[VRCPinYin.验收]`），使清单中每条在当次运行中均有对应输出。

### 9.1 前置条件

- 模块 1（OverlayManager）与模块 2（KeyboardManager + VRPointerHandler）已通过验收，Overlay 可正常显示/隐藏，VR 指针可正常交互。
- Unity 工程已包含 `CandidatesPanelManager`（`Assets/Scripts/Candidates/CandidatesPanelManager.cs`）和 `CandidateButton`（`Assets/Scripts/Candidates/CandidateButton.cs`）。
- OverlayCanvas 下已搭建 InputFieldRow 与 CandidatesRow（见第 5 节场景结构）。
- `CandidatesPanelManager` 的 Inspector 字段已正确赋值。
- **`useMockData` 已勾选**（模块 4 未就绪时使用 Mock 模式验收）。
- 工程配置见 [SETUP.md](../SETUP.md)。

### 9.2 操作步骤

按顺序执行以下步骤，以便触发全部验收相关 Log 并便于人工观察：

1. **启动 SteamVR**：确保头显和至少一只手柄已连接。
2. **运行场景**：Unity 中打开场景（如 `Scenes/SampleScene`），点击 **Play**。
3. **显示 Overlay**：通过模块 1 的 Toggle（手柄快捷键或代码调用）显示 Overlay。
4. **观察初始状态**：确认输入框显示占位提示文案（淡灰色 "请输入拼音..."），候选词区域为空状态（无候选按钮可见，翻页按钮呈 Disabled 状态）。
5. **输入拼音**：用手柄依次点击键盘字母键（如 N、I），观察：
   - 输入框中出现拼音（如 "ni"），拼音部分有高亮颜色区分。
   - 候选词面板出现 mock 候选词（如 "1 候选1"、"2 候选2" ...），最多 6 个。
   - 翻页按钮状态根据候选总数更新。
6. **翻页测试**：
   - 点击 **下一页 ▶** 按钮，观察候选词更新为下一页、页码指示变化（如 "1/3" → "2/3"）。
   - 点击 **上一页 ◀** 按钮，观察回到上一页。
   - 在第一页时确认上一页按钮不可交互（Disabled 状态）。
   - 在最后一页时确认下一页按钮不可交互。
7. **选词测试**：点击某个候选词按钮（如第 2 个），观察：
   - 已选候选词追加到输入框的已组句文本中。
   - 拼音被清空，候选词被清空。
8. **继续输入并选词**：再次输入拼音（如 H、A、O），点击候选词，观察组句文本持续累积。
9. **退格测试**：输入几个拼音字母后，点击退格键，观察拼音缩短、候选词相应更新。
10. **回车上屏测试**：输入拼音后，点击回车键，观察拼音原文被追加到已组句文本。
11. **空格选首词测试**：输入拼音后，点击空格键，观察首个候选词被选中上屏。
12. **🎙️ 语音按钮测试**：点击 🎙️ 语音按钮，检查 Log 确认事件触发。
13. **ClearAll 测试**（可选）：通过脚本在运行时调用 `CandidatesPanelManager.Instance.ClearAll()`，观察输入框与候选面板恢复初始状态。
14. **隐藏 Overlay 后再显示**：通过 Toggle 隐藏再显示 Overlay，确认面板状态保持一致。
15. **停止运行**：点击 **Stop** 结束 Play。

### 9.3 人工观察要点

在按 9.2 操作后，用眼睛确认以下项（对应验收标准中依赖观察的部分）：

- **观察**：Overlay 显示后，布局自上而下为：输入框（含 🎙️ 按钮）→ 候选词区域 → 键盘面板。
- **观察**：初始状态下输入框显示占位提示文案（淡灰色），候选按钮不可见，翻页按钮呈 Disabled 颜色。
- **观察**：输入拼音后，输入框中拼音部分有**明显颜色区分**（高亮），与已组句文本在视觉上可区别。
- **观察**：候选词按钮水平排列，每个按钮显示 "[序号] [候选词]"，序号从 1 开始连续编号。
- **观察**：候选数量不足 6 个时，多余按钮位置为空（不可见），布局无异常。
- **观察**：翻页后，候选词内容变化，页码指示文本更新（如 "2/3"）。
- **观察**：选词后，所选候选词出现在输入框的已组句文本部分，拼音与候选词被清空。
- **观察**：候选词按钮与翻页按钮有 hover 高亮与 pressed 反馈（复用模块 2 的 Pointer 事件驱动 Button 状态）。
- **观察**：退格后拼音缩短，候选词相应变化。
- **观察**：🎙️ 语音按钮可见，位于输入框右侧，可正常点击（有按下反馈）。

### 9.4 Log 验收清单

当用户按 9.2 完成操作后，以下各项应在当次运行的 Log 中有对应输出（实现侧在 `CandidatesPanelManager` / `CandidateButton` 中已按本清单打 Log，前缀 `[VRCPinYin.验收]`）。验收时在 Console 中筛选 `[VRCPinYin.验收]`，逐项核对并在下表打勾。

| # | 验收点（Log 中应出现的内容） | 通过 |
|---|------------------------------|------|
| 1 | CandidatesPanelManager 初始化：出现 `[VRCPinYin.验收] CandidatesPanelManager 初始化完成, 候选按钮数量=..., useMockData=...` | ☐ |
| 2 | Mock 模式已启用：出现 `[VRCPinYin.验收] Mock 模式已启用，已订阅 KeyboardManager 按键事件` | ☐ |
| 3 | UpdateInputField 被调用：出现 `[VRCPinYin.验收] UpdateInputField 已调用, committedText='...', pinyinComposition='...'` | ☐ |
| 4 | UpdateCandidates 被调用：出现 `[VRCPinYin.验收] UpdateCandidates 已调用, 候选数=..., currentPage=..., totalPages=...` | ☐ |
| 5 | 候选按钮显示/隐藏：出现 `[VRCPinYin.验收] 候选按钮更新: 显示 ... 个, 隐藏 ... 个` | ☐ |
| 6 | OnCandidateSelected 触发：点击候选按钮后出现 `[VRCPinYin.验收] OnCandidateSelected 触发, index=...` | ☐ |
| 7 | OnPagePrev 触发：点击上一页后出现 `[VRCPinYin.验收] OnPagePrev 触发` | ☐ |
| 8 | OnPageNext 触发：点击下一页后出现 `[VRCPinYin.验收] OnPageNext 触发` | ☐ |
| 9 | 翻页按钮状态更新：出现 `[VRCPinYin.验收] 翻页按钮状态更新: prevInteractable=..., nextInteractable=...` | ☐ |
| 10 | 页码指示更新：出现 `[VRCPinYin.验收] 页码指示更新: .../...` | ☐ |
| 11 | OnVoiceButtonClicked 触发：点击 🎙️ 后出现 `[VRCPinYin.验收] OnVoiceButtonClicked 触发` | ☐ |
| 12 | ClearAll 被调用（若触发）：出现 `[VRCPinYin.验收] ClearAll 已调用, 面板已重置` | ☐ |
| 13 | Mock 选词处理：Mock 模式下点击候选后出现 `[VRCPinYin.验收] Mock 选词: index=..., 候选词='...', 追加到已组句文本` | ☐ |
| 14 | Mock 空格选首词：Mock 模式下按空格后出现 `[VRCPinYin.验收] Mock 空格选首词: '...', 追加到已组句文本` | ☐ |
| 15 | Mock 回车上屏：Mock 模式下按回车后出现 `[VRCPinYin.验收] Mock 回车上屏: 拼音原文='...', 追加到已组句文本` | ☐ |
| 16 | Mock 退格：Mock 模式下按退格后出现 `[VRCPinYin.验收] Mock 退格: 拼音='...'` | ☐ |
| 17 | 多实例（若发生）：场景中存在多个 CandidatesPanelManager 时，出现 `[VRCPinYin.验收] CandidatesPanelManager 已存在，将销毁重复实例。` | ☐ |

**说明**：若某条在本次操作中未触发（例如未调用 ClearAll 则第 12 条无；仅一个 Instance 则第 17 条无），可在该条标注「未触发」或「N/A」，其余条须在 Log 中有对应输出且含义/取值符合预期，方算 Log 验收通过。

### 9.5 验收结论（验收时由用户填写）

- **本次验收的实际操作步骤记录**：
  1. （待验收时填写）
- **人工观察**：（待验收时填写）
- **Log 验收（本次已验证通过）**：（待验收时填写）
- **Log 验收（本次未触发 / N/A）**：（待验收时填写）
- **Log 验收（本次未测：操作成本较高）**：（待验收时填写）

**结论**：（待验收时填写）

---

## 10. 参考资料

### 本仓库

- [ARCHITECTURE.md](../ARCHITECTURE.md) - 总体架构
- [DECISIONS.md](../DECISIONS.md) - ADR-001: 使用 SteamVR Overlay；ADR-009: 单 exe 架构
- [1_overlay-framework.md](./1_overlay-framework.md) - 模块 1：Overlay 框架，Overlay 显示/隐藏与射线相交
- [2_keyboard-ui.md](./2_keyboard-ui.md) - 模块 2：虚拟键盘 UI，VR 指针交互（本模块的按钮复用其 Pointer 事件）
- [4_ime-engine.md](./4_ime-engine.md) - 模块 4：输入法引擎，候选词数据来源
- [5_text-output.md](./5_text-output.md) - 模块 5：文字输出，发送/复制目标

### Unity UI

- [Unity UI Text Rich Text](https://docs.unity3d.com/2022.3/Documentation/Manual/StyledText.html) - Rich Text 格式标签（用于拼音高亮）
- [Unity HorizontalLayoutGroup](https://docs.unity3d.com/2022.3/Documentation/Manual/script-HorizontalLayoutGroup.html) - 水平布局组件
- [Unity Button](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/UI.Button.html) - Button 组件 API

---

*此文档已完成详细设计，实现时以本文与模块 1、2 提供的 API 为准。*
