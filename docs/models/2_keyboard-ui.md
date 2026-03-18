# 模块 2：虚拟键盘 UI

> 状态：✅ 已细化
> 依赖：模块 1（Overlay 框架）

---

## 概述

本文档描述虚拟键盘 UI 的详细设计。本模块负责：键盘布局与按键渲染、VR 控制器射线与 Canvas 交互（指向 + 点击）、按键事件识别与分发、视觉反馈，以及底部功能栏（输入法名称、中/英切换、空格、发送、复制到剪贴板）。

VR 指针交互建立在模块 1 提供的 `ComputeIntersection` 之上：将控制器射线与 Overlay 的交点（UV）转换为 Canvas 像素坐标，再通过 `GraphicRaycaster` 命中具体 UI 元素，驱动 Unity EventSystem 的 Pointer 事件。这一通道**同时服务于**键盘按键与模块 3（候选词面板）的按钮点击，模块 3 无需实现自己的射线检测逻辑。

按键事件在进程内直接交给模块 4（输入法引擎），发送/复制操作交给模块 5（文字输出）。

---

## 1. 键盘布局

### 1.1 布局设计

参照 [README UI 示意图](../../README.md#ui-示意图)，键盘区域占 Overlay 下半部分，共 4 行：

```
┌───────────────────────────────────────────────────────────┐
│  Q  W  E  R  T  Y  U  I  O  P               [退格]       │
│   A  S  D  F  G  H  J  K  L                 [回车]       │
│    Z  X  C  V  B  N  M                                   │
│ [微软拼音输入法] [中/英] [_____空格_____] [发送] [复制到剪贴板] │
└───────────────────────────────────────────────────────────┘
```

- **第 1 行**：10 个字母键（Q–P）+ 退格键（Backspace）。
- **第 2 行**：9 个字母键（A–L）+ 回车键（Enter），水平略偏移以模拟 QWERTY 阶梯感。
- **第 3 行**：7 个字母键（Z–M），同样水平偏移。
- **第 4 行（功能行）**：
  - **输入法名称标签**（只读 Text）：显示当前 IME 名称，如 "微软拼音输入法"；由模块 4 提供数据，模块 4 未就绪时显示占位文案。
  - **中/英切换按钮**：点击切换输入模式；状态由模块 4 管理并回传，本模块仅更新显示。
  - **空格键**：中文模式下选中首选候选词上屏，英文模式下输入空格字符。宽度占功能行主体，便于 VR 中点击。
  - **发送按钮**：将当前已组句文本通过模块 5 以 OSC 模式发送到 VRChat。
  - **复制到剪贴板按钮**：将当前已组句文本通过模块 5 写入系统剪贴板。

### 1.2 按键分类

| 类型 | 按键 | 行为 |
|------|------|------|
| Letter | A–Z | 将字符交给模块 4（中文模式下为拼音字母；英文模式下为直接字符输入） |
| Backspace | 退格 | 通知模块 4 删除最后一个拼音字符或已输入字符 |
| Enter | 回车 | 通知模块 4：中文模式下将当前拼音原文上屏（不转换为汉字）；英文模式下行为由模块 4 定义 |
| Space | 空格 | 通知模块 4：中文模式下选中首选候选词上屏；英文模式下输入空格字符 |
| ToggleLang | 中/英 | 通知模块 4 切换中/英输入模式 |
| Send | 发送 | 从模块 4 获取待发送文本，调用模块 5 的 OSC 发送接口 |
| CopyClip | 复制到剪贴板 | 从模块 4 获取待发送文本，调用模块 5 的剪贴板接口 |

---

## 2. VR 指针交互

### 2.1 交互流水线

本模块提供控制器射线到 Canvas UI 事件的完整通道：

```
控制器位姿 (SteamVR Pose Action)
    │
    ▼
射线起点 + 方向
    │
    ▼
OverlayManager.ComputeIntersection(source, dir, out point, out uv)
    │  （模块 1 提供）
    ▼
UV → 像素坐标 (uv.x × RT.width,  uv.y × RT.height)
    │
    ▼
构造 PointerEventData（像素坐标 + OverlayCamera）
    │
    ▼
GraphicRaycaster.Raycast → 命中 UI 元素
    │
    ▼
发送 Pointer 事件（Enter / Exit / Down / Up / Click）
```

### 2.2 控制器输入

- **位姿（Pose）**：使用 `SteamVR_Action_Pose` 获取控制器在 Tracking Space 中的位置与朝向。射线起点为控制器位置，方向为控制器前向（`transform.forward`）。
- **点击（Click）**：使用 `SteamVR_Action_Boolean`（如 `InteractUI`）检测扳机按下。`GetStateDown` 为按下帧，`GetStateUp` 为松开帧，`GetState` 为持续按住。
- **手柄选择**：默认使用**惯用手**控制器（可配置为 Right / Left / Any）。设为 Any 时，优先响应最近一次产生命中的手柄。

### 2.3 UV 到 Canvas 坐标映射

- `ComputeIntersection` 返回的 UV 范围为 (0,0)–(1,1)，对应 Overlay 纹理的左下角到右上角。
- 映射到 RenderTexture 像素坐标：`pixelX = uv.x × renderTexture.width`，`pixelY = uv.y × renderTexture.height`。
- 将像素坐标赋值给 `PointerEventData.position`，搭配 OverlayCamera 作为事件相机，交由挂载在 OverlayCanvas 上的 `GraphicRaycaster` 做二维射线检测，即可命中 Canvas 上的 Button、Text 等 UI 元素。

### 2.4 Pointer 事件生命周期

参考 Unity `StandaloneInputModule` 的事件模型，本模块维护以下状态并通过 `ExecuteEvents` 发送事件：

| 事件 | 触发条件 | 说明 |
|------|----------|------|
| PointerEnter | 射线命中新的 UI 元素（与上一帧目标不同） | 触发 hover 视觉反馈 |
| PointerExit | 射线离开当前 UI 元素 | 取消 hover 视觉反馈 |
| PointerDown | Click Action 按下帧，且射线在 UI 元素上 | 触发 pressed 视觉反馈 |
| PointerUp | Click Action 松开帧 | 恢复按钮状态 |
| PointerClick | Down 与 Up 在同一 UI 元素上完成 | 触发按键逻辑 |

- 每帧仅处理一只手的射线（按 2.2 中的手柄选择策略）。
- Overlay 不可见时（`OverlayManager.Instance.IsVisible == false`）跳过全部射线处理。

### 2.5 射线未命中处理

- 射线未与 Overlay 相交（`ComputeIntersection` 返回 `false`），或相交后 `GraphicRaycaster` 未命中任何 UI 元素时：若当前有 hover 目标，发送 `PointerExit`；不执行任何按键操作。

---

## 3. 按键事件处理

### 3.1 事件分发

键盘区域每个按钮 GameObject 上挂载标识组件（`KeyButton`），记录该键的**类型**（Letter / Backspace / Enter / Space / ToggleLang / Send / CopyClip）与**键值**（字母键为 `'a'`–`'z'`）。

当 PointerClick 事件到达按钮时，`KeyboardManager` 读取该按钮上的 `KeyButton` 信息，按类型分发：

- **Letter / Backspace / Enter / Space / ToggleLang** → 触发对应事件（`OnLetterKey` / `OnBackspace` / `OnEnter` / `OnSpace` / `OnToggleLang`），模块 4 订阅这些事件。
- **Send / CopyClip** → 触发 `OnSendRequested` / `OnCopyRequested`，由业务层从模块 4 获取待发送文本并调用模块 5。

### 3.2 与模块 4 的协作

- 模块 2 **不维护**拼音/组句状态，仅负责将按键事件通过事件（event / Action）传递给模块 4。
- 模块 4 处理后：
  - 通知模块 3 更新候选词面板。
  - 调用本模块的 `UpdateIMELabel()` / `UpdateLangState()` 更新底部功能行显示。
- 模块 4 未就绪时，按键事件仍正常触发并打 Log，可独立验收。

### 3.3 与模块 5 的协作

- **发送按钮**：触发 `OnSendRequested`，监听方从模块 4 获取 `committedText`（已组句文本），调用模块 5 的 OSC 发送接口。发送后清空模块 4 的输入状态；可选调用 `OverlayManager.Instance.Hide()` 自动关闭 Overlay。
- **复制到剪贴板按钮**：触发 `OnCopyRequested`，流程同上但调用模块 5 的剪贴板接口。
- 模块 5 未就绪时，事件仍正常触发并打 Log，可独立验收。

---

## 4. 视觉反馈

### 4.1 按键状态

每个按键通过 Unity UI `Button` 组件的 `ColorBlock`（Color Tint 模式）实现以下视觉状态：

| 状态 | 表现 | 说明 |
|------|------|------|
| Normal | 默认底色 | 未交互 |
| Highlighted | 亮色或边框高亮 | 射线 hover 在该键上 |
| Pressed | 变暗或微缩 | 扳机按下时 |
| Disabled | 灰色（可选） | 按键不可用时 |

视觉状态由 Unity `Button` 组件根据收到的 Pointer 事件自动切换，本模块通过正确发送 Pointer 事件即可驱动。

### 4.2 射线光标

- 在 OverlayCanvas 最顶层放置一个 **Cursor Image**（小圆点或十字），位置随 UV 坐标每帧更新，帮助用户瞄准。
- 仅在射线与 Overlay 相交时显示，未相交时隐藏。
- 可通过配置项关闭（`showCursor = false`）。

### 4.3 中/英状态指示

- **中/英按钮文本**：中文模式显示 "中"，英文模式显示 "英"。由 `UpdateLangState(bool isChinese)` 更新。
- **输入法名称标签**：中文模式下显示 IME 名称（如 "微软拼音输入法"），英文模式下显示 "英文直输" 或保留 IME 名称。由 `UpdateIMELabel(string name)` 更新。
- 模块 4 未就绪时：按钮默认显示 "中"，标签显示占位文案 "输入法"。

---

## 5. Unity 场景结构

### 5.1 Canvas 层级

在模块 1 已建立的 OverlayCanvas 下，本模块添加键盘面板：

```
[OverlayCanvas]                             // (模块 1 已有, World Space)
├── InputFieldRow                           // (模块 3 负责)
├── CandidatesRow                           // (模块 3 负责)
├── KeyboardPanel                           // ★ 本模块
│   ├── Row_QWERTY                          // HorizontalLayoutGroup
│   │   ├── Key_Q                           // Button + KeyButton(Letter, 'q')
│   │   ├── Key_W
│   │   ├── ... (Key_E ~ Key_P)
│   │   └── Key_Backspace                   // Button + KeyButton(Backspace)
│   ├── Row_ASDF                            // HorizontalLayoutGroup
│   │   ├── Key_A ... Key_L
│   │   └── Key_Enter                       // Button + KeyButton(Enter)
│   ├── Row_ZXCV                            // HorizontalLayoutGroup
│   │   └── Key_Z ... Key_M
│   └── Row_Bottom                          // HorizontalLayoutGroup
│       ├── Label_IMEName                   // Text（只读）
│       ├── Key_ToggleLang                  // Button + KeyButton(ToggleLang)
│       ├── Key_Space                       // Button + KeyButton(Space)，LayoutElement 宽度占比较大
│       ├── Key_Send                        // Button + KeyButton(Send)
│       └── Key_CopyClip                    // Button + KeyButton(CopyClip)
└── Cursor_Image                            // ★ 本模块：射线光标（最顶层显示）
```

### 5.2 VR 指针处理器

```
[OverlayRig]                                // (模块 1 已有)
├── [OverlayCamera]
├── [OverlayCanvas]
└── VRPointerHandler                        // ★ 本模块：射线→UV→Canvas Pointer 事件
```

`VRPointerHandler` 需要引用：
- `OverlayManager`：调用 `ComputeIntersection`。
- `GraphicRaycaster`：OverlayCanvas 上的射线检测组件。
- `Camera`：OverlayCamera，用于 `PointerEventData` 的事件相机。
- `SteamVR_Action_Pose`：控制器位姿。
- `SteamVR_Action_Boolean`：扳机/点击。

### 5.3 代码产出目录

本模块脚本放在 **Assets/Scripts/Keyboard/**：

| 脚本 | 职责 |
|------|------|
| `KeyboardManager.cs` | 单例，初始化按键、接收 `KeyButton` 点击事件并通过 `Action` 分发给模块 4/5 |
| `KeyButton.cs` | 挂载在每个按键 GameObject 上，标识键类型与键值，点击时通知 `KeyboardManager` |
| `VRPointerHandler.cs` | 每帧处理控制器射线→UV→GraphicRaycast→Pointer 事件的完整流水线 |

### 5.4 场景搭建指引

以下步骤基于模块 1 验收后的场景（已有 OverlayRig / OverlayCamera / OverlayCanvas / OverlayManager / RenderTexture / EventSystem）。若尚未搭建模块 1 场景，请先按 [1_overlay-framework.md 验收流程](./1_overlay-framework.md#9-验收流程) 完成。

#### 步骤 1：创建 KeyboardPanel

1. 在 **OverlayCanvas** 下右键 → **UI → Panel**，重命名为 `KeyboardPanel`。
2. 选中 `KeyboardPanel`，在 Inspector 中：
   - **RectTransform**：Anchor 设为底部拉伸（Stretch-Bottom），使键盘面板占据 Canvas 下半区域。可调整 Height 占比（例如约 60% 高度留给键盘，上方留给模块 3 的输入框与候选词）。
   - **Image**：可设置半透明深色背景（如 `RGBA(30, 30, 30, 200)`），使按键可读。
   - 添加 **Vertical Layout Group** 组件：Child Alignment = Upper Center，Control Child Size 勾选 Width + Height，Spacing 适当（如 4~8）。
3. 在 `KeyboardPanel` 上添加 **KeyboardManager** 脚本组件（`Assets/Scripts/Keyboard/KeyboardManager.cs`）。

#### 步骤 2：创建 4 行按键容器

在 `KeyboardPanel` 下依次创建 4 个空 GameObject，分别命名为 `Row_QWERTY`、`Row_ASDF`、`Row_ZXCV`、`Row_Bottom`。每个 Row 上：

1. 添加 **Horizontal Layout Group**：Child Alignment = Middle Center，Spacing = 4~6，Control Child Size 勾选 Width + Height。
2. 添加 **Layout Element**（可选）：设置 Preferred Height 控制行高。
3. `Row_ASDF` 可在 Horizontal Layout Group 的 Padding.Left 加 15~20，模拟 QWERTY 阶梯偏移；`Row_ZXCV` 再多偏移一些（如 30~40）。

#### 步骤 3：创建字母键

以第 1 行为例（其余行同理）：

1. 在 `Row_QWERTY` 下右键 → **UI → Button**，重命名为 `Key_Q`。
2. 选中 `Key_Q`，在 Inspector 中：
   - **Button** 组件：Transition 保持 **Color Tint**（默认即可），确保 Normal / Highlighted / Pressed / Disabled 颜色有区分（可统一调色：Normal 深灰、Highlighted 浅灰、Pressed 更深灰）。Navigation 设为 **None**（避免方向键跳转干扰 VR 操作）。
   - 子物体 **Text**：文本设为 `Q`，字号建议 24~32（根据 Canvas 与 RenderTexture 分辨率调整），居中对齐，颜色白色。
   - 添加 **KeyButton** 脚本组件：`Key Type` 设为 **Letter**，`Letter Value` 填 `q`。
   - （可选）添加 **Layout Element**：设置 Preferred Width / Height 统一按键尺寸。
3. 复制 `Key_Q` 并依次重命名为 `Key_W` ~ `Key_P`（共 10 个），修改每个的 **Text** 和 **KeyButton.letterValue**：

   | 物体名 | Text | letterValue |
   |--------|------|-------------|
   | Key_Q | Q | q |
   | Key_W | W | w |
   | Key_E | E | e |
   | Key_R | R | r |
   | Key_T | T | t |
   | Key_Y | Y | y |
   | Key_U | U | u |
   | Key_I | I | i |
   | Key_O | O | o |
   | Key_P | P | p |

4. 在 `Row_QWERTY` 末尾再创建一个 Button，命名 `Key_Backspace`，Text 设为 `退格`，**KeyButton.keyType** 设为 **Backspace**（letterValue 留空）。可设置稍宽的 Preferred Width。

5. 对 `Row_ASDF` 按同样方式添加 A~L（9 个字母键）+ `Key_Enter`（Text = `回车`，keyType = **Enter**）。
6. 对 `Row_ZXCV` 添加 Z~M（7 个字母键）。

#### 步骤 4：创建功能行（Row_Bottom）

在 `Row_Bottom` 下依次创建以下子物体：

1. **Label_IMEName**：右键 → **UI → Text**（不要用 Button），文本 `输入法`，Font Size 20~24，Left 对齐，颜色浅灰。添加 **Layout Element**，Preferred Width 约 200（根据布局需要）。
2. **Key_ToggleLang**：UI → Button，子 Text 设为 `中`。**KeyButton** 组件：keyType = **ToggleLang**。Preferred Width 约 60。
3. **Key_Space**：UI → Button，子 Text 设为 `空格` 或留空。**KeyButton** 组件：keyType = **Space**。添加 **Layout Element**，设置 **Flexible Width = 1**（使空格键占据剩余空间，成为最宽按键）。
4. **Key_Send**：UI → Button，子 Text 设为 `发送`。**KeyButton** 组件：keyType = **Send**。Preferred Width 约 100。
5. **Key_CopyClip**：UI → Button，子 Text 设为 `复制到剪贴板`。**KeyButton** 组件：keyType = **CopyClip**。Preferred Width 约 160。

#### 步骤 5：配置 KeyboardManager Inspector

选中 `KeyboardPanel`（挂载了 KeyboardManager），在 Inspector 中：

| 字段 | 赋值 |
|------|------|
| **Ime Label Text** | 拖入 `Row_Bottom/Label_IMEName` 的 Text 组件 |
| **Lang Toggle Text** | 拖入 `Row_Bottom/Key_ToggleLang` 子物体 Text 的 Text 组件 |
| **Auto Hide On Send** | 按需勾选（默认不勾） |

#### 步骤 6：创建射线光标（Cursor_Image）

1. 在 **OverlayCanvas** 下（与 KeyboardPanel 同级，排在最后以保证渲染在最顶层）右键 → **UI → Image**，重命名为 `Cursor_Image`。
2. **RectTransform**：Width = 12，Height = 12，Pivot = (0.5, 0.5)。
3. **Image**：Source Image 可留空（使用默认白色方块）或指定圆形 Sprite；颜色设为醒目色（如白色或浅蓝色，Alpha = 220）。
4. 默认设为 **不激活**（取消勾选 GameObject 左上角的 Active 复选框），运行时由 VRPointerHandler 按需激活。
5. 取消勾选 **Raycast Target**（Image 组件上），避免光标自身被 GraphicRaycaster 命中。

#### 步骤 7：创建 VRPointerHandler

1. 在 **OverlayRig** 下（与 OverlayCamera、OverlayCanvas 同级）创建空 GameObject，命名 `VRPointerHandler`。
2. 添加 **VRPointerHandler** 脚本组件（`Assets/Scripts/Keyboard/VRPointerHandler.cs`）。
3. 在 Inspector 中配置：

| 字段 | 赋值 |
|------|------|
| **Pose Action** | 选择 SteamVR Pose Action（如 `/actions/default/in/Pose` 或项目中已有的 Pose Action） |
| **Click Action** | 选择 SteamVR Boolean Action（如 `InteractUI` 或 `GrabPinch`，即扳机键） |
| **Graphic Raycaster** | 拖入 `OverlayCanvas` 物体上的 GraphicRaycaster 组件 |
| **Overlay Camera** | 拖入 `OverlayCamera` |
| **Cursor Image** | 拖入步骤 6 创建的 `Cursor_Image` 的 RectTransform |
| **Pointer Hand** | 选择 Right（默认）或按需改为 Left / Any |
| **Show Cursor** | 勾选（默认 true） |

#### 步骤 9：确保 EventSystem 存在

场景中必须有一个 **EventSystem** 物体（模块 1 验收时通常已添加）。若没有：Hierarchy → 右键 → **UI → Event System**。

#### 步骤 10：确保 OverlayCanvas 有 GraphicRaycaster

选中 `OverlayCanvas`，确认 Inspector 中已有 **Graphic Raycaster** 组件。若没有，点击 **Add Component → Graphic Raycaster**。注意：World Space Canvas 默认自带此组件，通常无需手动添加。

#### 最终层级检查

搭建完成后，Hierarchy 应呈现如下结构（加粗为本步骤新增）：

```
[OverlayRig]                                    // 模块 1
├── [OverlayCamera]                             // 模块 1（Culling Mask 需含 UI layer）
├── [OverlayCanvas]                             // 模块 1（需有 GraphicRaycaster）
│   ├── (InputFieldRow)                         // 模块 3（暂可留空或占位）
│   ├── (CandidatesRow)                         // 模块 3（暂可留空或占位）
│   ├── **KeyboardPanel**                       // ★ 新增，挂 KeyboardManager
│   │   ├── **Row_QWERTY**                      // ★ Q~P + Key_Backspace
│   │   ├── **Row_ASDF**                        // ★ A~L + Key_Enter
│   │   ├── **Row_ZXCV**                        // ★ Z~M
│   │   └── **Row_Bottom**                      // ★ Label_IMEName + 功能键
│   └── **Cursor_Image**                        // ★ 新增，默认不激活
├── **VRPointerHandler**                        // ★ 新增，挂 VRPointerHandler 脚本
├── [OverlayManager]                            // 模块 1
└── [EventSystem]                               // 已有
```

---

## 6. 配置项

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| pointerHand | enum (Right / Left / Any) | Right | 用于操作键盘的手柄 |
| poseAction | SteamVR_Action_Pose | — | 控制器位姿 Action |
| clickAction | SteamVR_Action_Boolean | — | 扳机/点击 Action |
| showCursor | bool | true | 是否在 Canvas 上显示射线光标 |
| autoHideOnSend | bool | false | 发送后是否自动隐藏 Overlay |

---

## 7. API 设计

### 7.1 对外接口（供模块 3、4、5 使用）

本模块通过 `KeyboardManager` 单例对外提供以下能力：

| 接口 | 说明 |
|------|------|
| **event Action\<char\> OnLetterKey** | 字母键按下事件，参数为小写字符（'a'–'z'） |
| **event Action OnBackspace** | 退格键按下事件 |
| **event Action OnEnter** | 回车键按下事件 |
| **event Action OnSpace** | 空格键按下事件 |
| **event Action OnToggleLang** | 中/英切换按下事件 |
| **event Action OnSendRequested** | 发送按钮按下事件 |
| **event Action OnCopyRequested** | 复制到剪贴板按钮按下事件 |
| **void UpdateIMELabel(string imeName)** | 更新底部输入法名称标签（由模块 4 调用） |
| **void UpdateLangState(bool isChinese)** | 更新中/英按钮显示（由模块 4 调用） |

### 7.2 VR 指针接口

`VRPointerHandler` 提供的能力（主要内部使用，可选择性暴露）：

| 接口 | 说明 |
|------|------|
| **bool IsPointerActive** | 当前射线是否正在与 Overlay 相交 |
| **Vector2 CurrentUV** | 当前射线在 Overlay 上的 UV 坐标（未相交时无效） |
| **GameObject CurrentHoverTarget** | 当前 hover 的 UI 元素（未命中时为 null） |

### 7.3 依赖接口（本模块调用其他模块）

| 来源 | 接口 | 用途 |
|------|------|------|
| 模块 1 | `OverlayManager.Instance.ComputeIntersection(source, dir, out point, out uv)` | 射线与 Overlay 相交计算 |
| 模块 1 | `OverlayManager.Instance.IsVisible` | 仅在 Overlay 可见时处理射线 |
| 模块 1 | `OverlayManager.Instance.Hide()` | 发送后自动隐藏（可选） |
| 模块 4 | 按键输入接口（待模块 4 细化后确定） | 通过事件订阅方式，模块 4 订阅 `OnLetterKey` 等事件 |
| 模块 4 | 获取待发送文本接口（待模块 4 细化后确定） | 发送/复制时获取文本 |
| 模块 5 | OSC 发送接口（待模块 5 细化后确定） | 发送按钮触发 |
| 模块 5 | 剪贴板接口（待模块 5 细化后确定） | 复制按钮触发 |

### 7.4 与模块 3 的关系

- VR 指针交互由本模块（`VRPointerHandler`）统一提供，模块 3 的候选词按钮作为 OverlayCanvas 上的标准 Unity UI Button，自然接收本模块发送的 Pointer 事件，**无需额外的射线检测代码**。
- 模块 3 只需关注自身按钮的 `onClick` 回调即可。

---

## 8. 验收标准

满足以下条件时，视为本模块通过验收：

1. **射线点击**：Overlay 可见时，用手柄射线点击键盘区域，能正确识别所按按键（字母、退格、空格、回车等），并将按键事件分发（模块 4 未就绪时可通过 Log 确认事件已触发）。
2. **视觉反馈**：按键有可见的 hover 高亮与 pressed 按下反馈，用户能明确感知射线指向与点击生效。
3. **布局与 UI**：键盘布局与 [README UI 示意图](../../README.md#ui-示意图) 一致（QWERTY + 退格 + 回车 + 功能行）；输入法名称与中/英切换按钮有展示位置（可先用占位文案）。
4. **功能行**：发送按钮和复制到剪贴板按钮可被点击，事件已分发（模块 5 未就绪时可通过 Log 确认）。
5. **对外接口**：其他模块能通过事件订阅（`OnLetterKey` 等）接收按键通知；能调用 `UpdateIMELabel` / `UpdateLangState` 更新底部显示。

**总验收说明**：当本模块与其余 4 个模块均完成并通过各自验收后，可按 [README 中的「工作流程」](../../README.md#工作流程) 进行端到端总验收（唤起 → 输入 → 处理 → 展示 → 选择 → 输出）。

---

## 9. 验收流程

遵循 [docs/models/.prompt.md 验收流程](.prompt.md#3-验收流程)：用户按下列**操作步骤**执行后，先做**人工观察**，再根据 **Log 验收清单** 读 Log 逐项打勾。实现侧须在对应逻辑中打 Log（前缀 `[VRCPinYin.验收]`），使清单中每条在当次运行中均有对应输出。

### 9.1 前置条件

- 模块 1（OverlayManager）已通过验收，Overlay 可正常显示/隐藏。
- Unity 工程已包含 `KeyboardManager`（`Assets/Scripts/Keyboard/KeyboardManager.cs`）、`VRPointerHandler`（`Assets/Scripts/Keyboard/VRPointerHandler.cs`）、`KeyButton`（`Assets/Scripts/Keyboard/KeyButton.cs`）。
- OverlayCanvas 下已搭建 KeyboardPanel 及各行按键（见第 5 节场景结构）。
- `VRPointerHandler` 已配置 Pose Action 与 Click Action。
- 工程配置见 [SETUP.md](../SETUP.md)。

### 9.2 操作步骤

按顺序执行以下步骤，以便触发全部验收相关 Log 并便于人工观察：

1. **启动 SteamVR**：确保头显和至少一只手柄已连接。
2. **运行场景**：Unity 中打开场景（如 `Scenes/SampleScene`），点击 **Play**。
3. **显示 Overlay**：通过模块 1 的 Toggle（手柄快捷键或代码调用 `OverlayManager.Instance.Show()`）显示 Overlay。
4. **射线指向键盘**：将配置的手柄（默认右手）指向 Overlay 键盘区域，观察射线指示线从面板边缘延伸至光标圆点、光标出现及按键 hover 高亮。
5. **点击字母键**：扣下扳机，依次点击至少 3 个字母键（如 N、I、H），观察 pressed 反馈并检查 Log。
6. **点击退格键**：点击退格键，观察反馈并检查 Log。
7. **点击回车键**：点击回车键，观察反馈并检查 Log。
8. **点击空格键**：点击空格键，观察反馈并检查 Log。
9. **点击中/英切换**：点击中/英按钮，观察按钮文本在 "中"/"英" 间切换并检查 Log。
10. **点击发送按钮**：点击发送按钮，检查 Log。
11. **点击复制到剪贴板按钮**：点击复制到剪贴板按钮，检查 Log。
12. **射线移出**：将手柄指向 Overlay 以外的区域，观察 hover 高亮消失、射线光标隐藏。
13. **隐藏 Overlay**：通过 Toggle 隐藏 Overlay，确认隐藏后不再有射线命中 Log。
14. **调用 UpdateIMELabel / UpdateLangState**（可选）：通过脚本在运行时调用 `KeyboardManager.Instance.UpdateIMELabel("测试输入法")` 和 `UpdateLangState(false)`，观察底部标签与按钮文本变化并检查 Log。
15. **停止运行**：点击 **Stop** 结束 Play。

### 9.3 人工观察要点

在按 9.2 操作后，用眼睛确认以下项（对应验收标准中依赖观察的部分）：

- **观察**：Overlay 显示后，键盘布局为 QWERTY 排列，共 4 行（字母 ×3 + 功能行 ×1），含退格、回车、空格、中/英、发送、复制到剪贴板。
- **观察**：射线指向按键时有 **hover 高亮**（颜色变化），移开后恢复。
- **观察**：扣扳机点击按键时有 **pressed 反馈**（颜色变暗或其他视觉变化）。
- **观察**：射线指示线（若开启）从 Overlay 面板边缘延伸至光标圆点，随手柄移动实时更新；射线移出 Overlay 后指示线消失。
- **观察**：射线光标（若开启）跟随射线交点移动，位置与手柄指向一致；射线移出 Overlay 后光标隐藏。
- **观察**：中/英切换按钮点击后，按钮文本在 "中" 和 "英" 间切换。
- **观察**：底部显示输入法名称（占位文案或实际名称）。

### 9.4 Log 验收清单

当用户按 9.2 完成操作后，以下各项应在当次运行的 Log 中有对应输出（实现侧在 `KeyboardManager` / `VRPointerHandler` / `KeyButton` 中已按本清单打 Log，前缀 `[VRCPinYin.验收]`）。验收时在 Console 中筛选 `[VRCPinYin.验收]`，逐项核对并在下表打勾。

| # | 验收点（Log 中应出现的内容） | 通过 |
|---|------------------------------|------|
| 1 | KeyboardManager 初始化：出现 `[VRCPinYin.验收] KeyboardManager 初始化完成, 按键数量=...` | ☐ |
| 2 | VRPointerHandler 初始化：出现 `[VRCPinYin.验收] VRPointerHandler 初始化完成, poseAction=..., clickAction=...` | ☐ |
| 3 | Pose/Click Action 未配置警告（若发生）：出现 `[VRCPinYin.验收] poseAction 或 clickAction 未配置，VR 指针将不可用` | ☐ |
| 4 | 射线命中 Overlay：出现 `[VRCPinYin.验收] VRPointer 射线命中 Overlay, uv=(x, y)` | ☐ |
| 5 | GraphicRaycast 命中 UI 元素：出现 `[VRCPinYin.验收] GraphicRaycast 命中: <按键名称>` | ☐ |
| 6 | PointerEnter：出现 `[VRCPinYin.验收] PointerEnter: <按键名称>` | ☐ |
| 7 | PointerExit：出现 `[VRCPinYin.验收] PointerExit: <按键名称>` | ☐ |
| 8 | PointerDown：出现 `[VRCPinYin.验收] PointerDown: <按键名称>` | ☐ |
| 9 | PointerClick：出现 `[VRCPinYin.验收] PointerClick: <按键名称>` | ☐ |
| 10 | 字母键事件分发：出现 `[VRCPinYin.验收] OnLetterKey 触发, char='...'` | ☐ |
| 11 | 退格键事件分发：出现 `[VRCPinYin.验收] OnBackspace 触发` | ☐ |
| 12 | 回车键事件分发：出现 `[VRCPinYin.验收] OnEnter 触发` | ☐ |
| 13 | 空格键事件分发：出现 `[VRCPinYin.验收] OnSpace 触发` | ☐ |
| 14 | 中/英切换事件分发：出现 `[VRCPinYin.验收] OnToggleLang 触发` | ☐ |
| 15 | 发送按钮事件分发：出现 `[VRCPinYin.验收] OnSendRequested 触发` | ☐ |
| 16 | 复制按钮事件分发：出现 `[VRCPinYin.验收] OnCopyRequested 触发` | ☐ |
| 17 | UpdateIMELabel 被调用（若触发）：出现 `[VRCPinYin.验收] UpdateIMELabel 已调用, imeName='...'` | ☐ |
| 18 | UpdateLangState 被调用（若触发）：出现 `[VRCPinYin.验收] UpdateLangState 已调用, isChinese=...` | ☐ |
| 19 | 射线未命中时 PointerExit：射线移出所有 UI 元素后出现 `PointerExit` 且不再出现 `PointerEnter` | ☐ |
| 20 | Overlay 隐藏时跳过处理：Overlay 隐藏后出现 `[VRCPinYin.验收] Overlay 不可见, 跳过 VRPointer 处理` 或不再出现射线命中 Log | ☐ |
| 21 | 多实例（若发生）：场景中存在多个 KeyboardManager 时，出现 `[VRCPinYin.验收] KeyboardManager 已存在，将销毁重复实例。` | ☐ |

**说明**：若某条在本次操作中未触发（例如未配置 Action 则第 3 条触发、第 4–9 条无法触发；仅一个 Instance 则第 21 条无），可在该条标注「未触发」或「N/A」，其余条须在 Log 中有对应输出且含义/取值符合预期，方算 Log 验收通过。

### 9.5 验收结论（验收时由用户填写）

- **本次验收的实际操作步骤记录**：
  1. 启动 SteamVR，并连接头显/手柄设备。
  2. 使用 Unity 打开场景 `Scenes/SampleScene`。
  3. 在层级面板中启用 `SampleScene/验收/2_keyboard-ui` 物体（确保 `KeyboardManager` 和 `VRPointerHandler` 生效）。
  4. 点击 **Play** 进入运行模式，按 `GrabGrip` 触发显示/隐藏键盘面板。
  5. 将手柄指向键盘区域，观察射线光标出现，依次划过多个按键（J、I、U、Y、W、E、R、T、D、F、G、H、K、L 等）。
  6. 点击字母键 H、K、L，点击回车键 Enter。

- **人工观察**：本次测试中：
  - 键盘面板按手柄快捷键能正确显示/隐藏。
  - 面板在显示时始终出现在正前方并固定在虚拟空间中的固定位置。
  - 射线光标可见，跟随手柄移动。
  - 射线划过按键时，按键有高亮反馈（颜色变化）。
  - 点击按键时，按键有 pressed 反馈（颜色改变）。
  - 观察部分通过。

- **Log 验收（本次已验证通过）**：本次测试中，以下条目在 Log 中已出现且判定通过：
  - **#1** KeyboardManager 初始化完成（`KeyboardManager 初始化完成, 按键数量=31`）
  - **#2** VRPointerHandler 初始化完成（`VRPointerHandler 初始化完成, poseAction=Pose, clickAction=GrabPinch`）
  - **#6** PointerEnter：多次出现，如 `PointerEnter: Key_J`、`PointerEnter: Key_I`、`PointerEnter: Key_H` 等
  - **#7** PointerExit：多次出现，如 `PointerExit: Key_J`、`PointerExit: Key_I`、`PointerExit: Key_H` 等
  - **#8** PointerDown：出现 `PointerDown: Key_H`、`PointerDown: Key_K`、`PointerDown: Key_L`、`PointerDown: Key_Enter`
  - **#9** PointerClick：出现 `PointerClick: Key_H`、`PointerClick: Key_K`、`PointerClick: Key_L`、`PointerClick: Key_Enter`
  - **#10** 字母键事件分发：出现 `OnLetterKey 触发, char='h'`、`OnLetterKey 触发, char='k'`、`OnLetterKey 触发, char='l'`
  - **#12** 回车键事件分发：出现 `OnEnter 触发`
  - **#19** 射线未命中时 PointerExit：射线移出所有 UI 元素后出现 `PointerExit` 且不再出现 `PointerEnter`
  - **#20** Overlay 隐藏时跳过处理：Overlay 隐藏后不再出现射线命中 Log

- **Log 验收（本次未触发 / N/A）**：
  - **#3** Pose/Click Action 未配置警告：未触发（本次配置正确）
  - **#4** 射线命中 Overlay 日志：未触发（早期版本日志，后续已移除）
  - **#5** GraphicRaycast 命中 UI 元素日志：未触发（调试日志，后续可移除）
  - **#11** 退格键事件分发：未触发（本次未点击退格键）
  - **#13** 空格键事件分发：未触发（本次未点击空格键）
  - **#14** 中/英切换事件分发：未触发（本次未点击中/英按钮）
  - **#15** 发送按钮事件分发：未触发（本次未点击发送按钮）
  - **#16** 复制按钮事件分发：未触发（本次未点击复制按钮）
  - **#17** UpdateIMELabel 被调用：未触发（本次未调用该接口）
  - **#18** UpdateLangState 被调用：未触发（本次未调用该接口）
  - **#21** 多实例：未触发（场景中仅一个 KeyboardManager）

- **Log 验收（本次未测：操作成本较高）**：
  - **#11** 退格键、**#13** 空格键、**#14** 中/英切换、**#15** 发送按钮、**#16** 复制按钮：本次未逐一测试所有按键，后续可按 9.2 操作步骤补测。

**结论**：本次已完成并通过"键盘面板显示/隐藏 + 射线交互 + 字母键点击 + 回车键点击"的核心验收；其余未触发/未测条目可在后续补测时按 9.2 的操作步骤覆盖并在 9.4 清单中补勾选。模块 2 的核心功能（VR 指针交互、按键事件分发）已验证可用，可继续进行后续模块 3～5 的开发与验收。

---

## 10. 参考资料

### 本仓库

- [ARCHITECTURE.md](../ARCHITECTURE.md) - 总体架构
- [DECISIONS.md](../DECISIONS.md) - ADR-001: 使用 SteamVR Overlay；ADR-009: 单 exe 架构
- [1_overlay-framework.md](./1_overlay-framework.md) - 模块 1：Overlay 框架，提供 ComputeIntersection 与 Show/Hide
- [3_candidates-panel.md](./3_candidates-panel.md) - 模块 3：候选词面板，复用本模块的 VR 指针交互
- [4_ime-engine.md](./4_ime-engine.md) - 模块 4：输入法引擎，按键事件交付目标
- [5_text-output.md](./5_text-output.md) - 模块 5：文字输出，发送/复制目标

### Unity UI 与 VR 交互

- [Unity EventSystem](https://docs.unity3d.com/2022.3/Documentation/Manual/EventSystem.html) - Unity UI 事件系统概览
- [GraphicRaycaster](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/UI.GraphicRaycaster.html) - Canvas 上的 UI 射线检测
- [ExecuteEvents](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/EventSystems.ExecuteEvents.html) - 向 GameObject 发送 Pointer 事件

### SteamVR Input

- [SteamVR Input System](https://valvesoftware.github.io/steamvr_unity_plugin/articles/SteamVR-Input.html) - SteamVR 输入系统（Action、Pose、Boolean）
- [SteamVR_Action_Pose](https://valvesoftware.github.io/steamvr_unity_plugin/api/Valve.VR.SteamVR_Action_Pose.html) - 控制器位姿 Action API

---

*此文档已完成详细设计，实现时以本文与模块 1 提供的 API 为准。*
