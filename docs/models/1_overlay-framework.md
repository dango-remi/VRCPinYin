# 模块 1：Overlay 框架

> 状态：✅ 已细化
> 依赖：无（入口模块）

---

## 概述

本文档描述 SteamVR Overlay 框架的详细设计。Overlay 与输入法、文字输出均运行在**同一 Unity 进程**内，无需跨进程通信。本模块负责：创建并持有 Overlay、显示/隐藏、快捷键响应、位置与渲染设置，以及将 Unity Camera 渲染的 UI 提交为 Overlay 纹理。射线相交与点击由模块 2（虚拟键盘 UI）基于本模块提供的接口实现。

---

## 1. SteamVR Overlay 初始化

### 1.1 时机与前置条件

- **时机**：在 SteamVR 已初始化、`OpenVR.Overlay` 可用后创建 Overlay。建议在 `Start` 或首个 `Update` 中检测 `OpenVR.Overlay != null` 再执行创建。
- **应用类型**：Unity 项目需将 OpenVR 的 **Application Type** 设为 **Overlay**（Project Settings → XR Plug-in Management → OpenVR），否则 Overlay 可能无法正常显示（与 SteamVR_FirstPersonOverlay 要求一致）。

### 1.2 创建 Overlay

- 使用 **CreateOverlay**，不用 CreateDashboardOverlay（Dashboard 为 Steam 系统面板，本应用为独立输入法浮层）。
- **Overlay Key**：使用应用唯一键，例如 `"vrcpinyin.input"`，避免与默认的 `"unity:CompanyName.ProductName"` 冲突，便于识别与调试。
- **Overlay Name**：展示名称，如 `"VRCPinYin"`，会出现在 SteamVR  Overlay 列表中。
- **返回值**：通过 `ref ulong pOverlayHandle` 得到 handle，后续所有 API 均使用该 handle。保存为成员变量，并在 `OnDisable` / 应用退出时调用 **DestroyOverlay**。

### 1.3 错误处理

- 检查 **EVROverlayError**：创建失败时记录 `GetOverlayErrorNameFromEnum(error)` 并禁用 Overlay 逻辑或提示用户。
- 若运行时 Overlay 被系统回收（如 SteamVR 重启），可尝试 **FindOverlay(key, ref handle)** 重新获取 handle，再继续 Show/SetTexture 等（与 SteamVR_Overlay 中的恢复逻辑一致）。

---

## 2. 显示/隐藏管理

### 2.1 状态

- **显示**：`ShowOverlay(handle)`，并确保已设置有效纹理（见第 5 节）；否则 Overlay 可能不显示或闪黑。
- **隐藏**：`HideOverlay(handle)`。隐藏后不销毁 Overlay，仅不渲染，便于再次唤起时响应更快。

### 2.2 与快捷键联动

- 显示/隐藏由**手柄快捷键**驱动（见第 3 节）：首次按下为「显示」，再次按下为「隐藏」，即 toggle。
- 可选：在「发送」或「关闭」按钮点击后自动隐藏（由模块 2/3 通过本模块提供的 `Hide()` 调用）。

### 2.3 与纹理的配合

- 若未设置纹理或纹理无效，应在逻辑上视为「未就绪」，不执行 Show；或在 Show 前确保已调用一次 SetOverlayTexture。

---

## 3. 快捷键处理

### 3.1 需求

- 用户通过**手柄按键**唤起/关闭 Overlay（P1：手柄快捷键唤起）。
- 推荐：单手即可操作，例如**左手柄握把（Grip）**或**右手柄某个 face 键**，具体键位可在 SteamVR Input 中配置。

### 3.2 实现方式

- 使用 **SteamVR Input System**（SteamVR 2.x）：在 Unity 中创建或使用已有 Action，例如 Boolean 类型的 `OverlayToggle`，绑定到 Grip 或 A 键。
- 在 Overlay 框架的 `Update` 中轮询该 Action：`if (OverlayToggle.GetStateDown(hand))` 则执行 **Toggle 显示/隐藏**（若当前可见则 Hide，否则 Show）。
- 不依赖 Overlay 的「焦点」：只要应用在前台且 SteamVR 正常，即可收到输入。

### 3.3 防抖与冲突

- 使用 `GetStateDown` 而非 `GetState`，避免一帧内重复触发。
- 若与 VRChat 的同一按键冲突，可考虑使用 SteamVR 的 Action Set 或不同按键，并在文档中说明推荐绑定。

---

## 4. 位置和朝向

### 4.1 定位方式选择

- **SetOverlayTransformAbsolute**：相对于 **Tracking Universe**（站立/坐姿原点）的固定位置。适合「始终在用户前方某处」的 UI。
- **SetOverlayTransformTrackedDeviceRelative**：相对于指定 Tracked Device（如 HMD，device index 0）。适合「跟随头显」的 UI。

本项目推荐 **SetOverlayTransformAbsolute**：Overlay 出现在用户前方固定距离与高度，用户转头时 Overlay 不跟头，更符合「一块浮在面前的键盘」的预期；若需「跟随头显」可后续改为 TrackedDeviceRelative。

### 4.2 推荐参数

- **距离**：例如 `distance = 1.0f`～`1.5f` 米（沿用户/头显前向）。可配置为参数。
- **高度与朝向**：由「相对 SteamVR Camera origin 的偏移」或「相对 HMD 位姿的偏移」决定；通常 Overlay 在用户前方、与视线平齐或略低，平面朝向用户。若无 SteamVR Camera（纯 Overlay 场景），可从 OpenVR Compositor 获取 HMD 位姿，在头显前方放置 Overlay。
- 使用 **SteamVR_Utils.RigidTransform** 或等价方式构造 `HmdMatrix34_t`，再调用 `SetOverlayTransformAbsolute(handle, SteamVR.settings.trackingSpace, ref t)`（与 SteamVR_Overlay 一致）。

### 4.3 尺寸

- **SetOverlayWidthInMeters**：Overlay 的物理宽度（米）。建议 0.8～1.2m，使键盘与候选词在 VR 中易于点击且不占满视野。高度由纹理宽高比与 WidthInMeters 共同决定（按 UV 与 bounds 不变形）。

---

## 5. 渲染设置

### 5.1 纹理来源

- Overlay 内容来自 **Unity 渲染**：使用一台专用 **Camera** 渲染 **Canvas（World Space 或 Screen Space - Camera）**，该 Camera 的 **Target Texture** 为一张 **RenderTexture**。
- 每帧将该 RenderTexture 的 native handle 通过 **SetOverlayTexture** 提交给 OpenVR（推荐每帧提交，避免驱动或 RT 生命周期导致的显示异常）。

### 5.2 纹理与 UV

- **VRTextureBounds_t**：默认 `uMin=0, uMax=1, vMin=1, vMax=0`（OpenVR 纹理 V 轴向下时需翻转），与 SteamVR_FirstPersonOverlay 一致。
- **SetOverlayTextureBounds**：在创建 Overlay 后设置一次即可，除非需要裁切或缩放 UV。

### 5.3 透明度与层级

- **SetOverlayAlpha**：0～1，建议 1；若需半透明背景可在此调节。
- **SetOverlaySortOrder**：数值越大越靠前。建议设置一个大于默认游戏 Overlay 的值，使 VRCPinYin 浮在 VRChat 之上（具体数值可实测后定，例如 100～500）。

### 5.4 输入与射线

- **SetOverlayInputMethod**：设为 **VROverlayInputMethod.Mouse** 或 **None** 均可；本项目使用 **ComputeOverlayIntersection** 在模块 2 中自行计算射线与 Overlay 的交点，不依赖 Overlay 内置鼠标。若使用 Mouse 模式，可同时接收 Overlay 的鼠标事件（PollNextOverlayEvent），按需选择。

### 5.5 图形 API

- **Texture_t.eType**：根据 `SystemInfo.graphicsDeviceType` 设置为 DirectX / Vulkan / OpenGL 等（与 SteamVR_FirstPersonOverlay 中 GetTextureType() 一致），否则可能黑屏或报错。

---

## 6. Unity 场景结构

### 6.1 推荐层级

```
[OverlayRig]                    // 可选：挂载本模块的 Overlay 管理脚本
├── [OverlayCamera]            // 专用相机，Target Texture = OverlayRenderTexture
│   └── (Culling 只渲染 UI Layer)
└── [OverlayCanvas]             // Canvas (World Space)，Render Mode = World Space
    └── EventCamera = OverlayCamera
    ├── InputFieldRow           // 输入框行（模块 2/3 使用）
    ├── CandidatesRow           // 候选词行（模块 3）
    └── KeyboardRow             // 键盘行（模块 2）
```

- **OverlayRig**：可放在场景根或某空物体下，用于挂载「Overlay 框架」管理脚本（创建 Overlay、Show/Hide、更新 Transform/Texture、响应快捷键）。建议以**单例**方式供其他模块访问；若场景中存在多份，需约定仅保留一份有效。
- **OverlayCamera**：只渲染 UI 层，Clear 为 Solid Color 或 Skybox（若需透明则用透明背景并 Alpha 混合）；Near/Far 包含 Canvas 所在距离。
- **OverlayCanvas**：World Space，其下挂载输入框、候选词、键盘等子 UI，由模块 2/3 具体实现。Canvas 的 Plane Distance 与 OverlayCamera 的摆放需保证 UI 在相机视野内且不变形。

### 6.2 RenderTexture

- 创建 **RenderTexture**（如 1920×1080 或 1280×720），格式 RGBA32，Depth 可选 0 或 16（若 UI 不需要深度）。将该 RT 赋给 OverlayCamera.targetTexture，并在 Overlay 框架中将其 `GetNativeTexturePtr()` 填入 Texture_t 后调用 SetOverlayTexture。

### 6.3 代码产出目录

- 本模块脚本放在 **Assets/Scripts/Overlay/**，例如：
  - `OverlayManager.cs`：单例，负责 Create/Destroy、Show/Hide、UpdateOverlay（位置 + 纹理）、快捷键轮询。
  - （可选）`OverlaySettings.cs`：距离、宽度、SortOrder 等可配置项。

---

## 7. API 设计

### 7.1 对外接口（供模块 2、3、5 使用）

本模块对外提供以下能力，建议通过单例或显式依赖注入访问：

| 接口 | 说明 |
|------|------|
| **bool IsVisible** | 当前 Overlay 是否处于显示状态 |
| **void Show()** | 显示 Overlay |
| **void Hide()** | 隐藏 Overlay |
| **void Toggle()** | 切换显示/隐藏，并返回当前是否可见（可选） |
| **bool ComputeIntersection(Vector3 source, Vector3 direction, out Vector3 point, out Vector2 uv)** | 给定射线（世界空间），计算与 Overlay 平面的交点及 UV，供模块 2 做按键/候选点击。可封装 OpenVR `ComputeOverlayIntersection`，坐标系与 SteamVR_Overlay 保持一致（注意 OpenVR 的 Z 翻转）。 |
| **ulong OverlayHandle** | （可选）暴露 handle，供需要直接调 OpenVR 的代码使用；默认不暴露亦可。 |

### 7.2 与模块 2、3 的协作

- 模块 2（键盘）：通过 **ComputeIntersection** 将手柄射线与 Overlay 相交，得到 UV 或像素坐标，再映射到键盘按键与候选区域。
- 模块 3（候选词）：同上，点击候选词时由模块 2 或 3 处理逻辑，Overlay 框架只负责「显示/隐藏」与「射线相交」。
- 模块 5（文字输出）：不直接依赖 Overlay 框架；仅当用户点击「发送」后，可由业务层调用本模块的 **Hide()** 关闭浮层。

---

## 8. 验收标准

满足以下条件时，视为本模块通过验收：

1. **创建与销毁**：在 SteamVR 环境下启动应用，Overlay 能成功创建且无报错；退出或禁用时正确调用 DestroyOverlay，无残留。
2. **显示/隐藏**：按下配置的手柄快捷键，Overlay 显示；再次按下，Overlay 隐藏（Toggle）。通过 API 调用 Show()、Hide()、Toggle() 行为正确。
3. **位置与内容**：Overlay 显示时出现在用户前方合适距离与尺寸，由 RenderTexture 提供的内容清晰可见，浮在游戏画面之上（SortOrder 有效）。
4. **对外接口**：其他模块能通过本模块提供的接口获取 IsVisible、调用 Show()/Hide()/Toggle()，以及使用 ComputeIntersection(射线起点, 射线方向) 得到与 Overlay 平面的交点及 UV，用于后续点击判定。

**总验收说明**：当本模块与其余 4 个模块均完成并通过各自验收后，可按 [README 中的「工作流程」](../../README.md#工作流程) 进行端到端总验收（唤起 → 输入 → 处理 → 展示 → 选择 → 输出）。

---

## 9. 验收流程

遵循 [docs/models/.prompt.md 验收流程](.prompt.md#3-验收流程)：用户按下列**操作步骤**执行后，先做**人工观察**，再根据 **Log 验收清单** 读 Log 逐项打勾。实现侧须在对应逻辑中打 Log（建议前缀 `[VRCPinYin.验收]`），使清单中每条在当次运行中均有对应输出。工程创建与 SteamVR 配置见 [SETUP.md](../SETUP.md)。

### 9.1 前置条件

- Unity 工程已包含 `OverlayManager`（`Assets/Scripts/Overlay/OverlayManager.cs`）。
- **Edit → Project Settings → XR Plug-in Management → OpenVR** 中 **Application Type** 已设为 **Overlay**（`OpenVRSettings.asset` 中 `InitializationType: 1` 即表示 Overlay）。

### 9.2 操作步骤

按顺序执行以下步骤，以便触发全部验收相关 Log 并便于人工观察：

1. **搭建最小场景**（若尚未搭建）：打开或新建场景并保存；创建 RenderTexture（如 `OverlayRT`，1280×720）；创建 Camera（`OverlayCamera`），Target Texture 指向 `OverlayRT`，Culling Mask 仅 UI；创建 World Space Canvas（`OverlayCanvas`），Event Camera 为 `OverlayCamera`，其下添加 Panel/Text 内容如 "VRCPinYin Overlay"；创建空物体 `OverlayRig` 挂载 Overlay Manager，**Overlay Texture** 赋为 `OverlayRT`；可选在 **Toggle Action** 中绑定 Boolean Action（如 **GrabGrip**）。若无 EventSystem 则添加一个。
2. **启动 SteamVR**：在 Windows 中打开 Steam → 运行 SteamVR，头显/手柄连接（无头显也可验收，部分项依赖观察）。
3. **运行场景**：Unity 中打开该场景，点击 **Play**。
4. **触发显示/隐藏**：若已绑定 Toggle Action，用手柄按该键**至少两次**（先显示再隐藏，或反之）；若未绑定，通过其它脚本或调试在运行时调用一次 `OverlayManager.Instance.Show()`、一次 `Hide()`、一次 `Toggle()`。
5. **触发对外接口（可选）**：在运行中通过脚本调用一次 `OverlayManager.Instance.ComputeIntersection(射线起点, 射线方向, out point, out uv)`（例如从手柄射线传入），以便 Log 中能验收该路径。
6. **停止运行**：点击 **Stop** 结束 Play，以便触发 OnDisable 与 DestroyOverlay。

### 9.3 人工观察要点

在按 9.2 操作后，用眼睛确认以下项（对应验收标准中依赖观察的部分）：

- **观察**：Play 后 Console 中**无** Overlay 创建失败等报错（若有报错则创建/销毁或配置有问题）。
- **观察**：若已绑定 Toggle 并按下手柄键，Overlay 应先**显示**再**隐藏**（或反之），表现与 Toggle 一致。
- **观察**：Overlay **显示**时，在头显中或 SteamVR Overlay 列表中能看到名为「VRCPinYin」的 Overlay；其**内容**为 Canvas 上绘制的内容（如 "VRCPinYin Overlay"），且**浮在**游戏/桌面画面之上（SortOrder 有效）。
- **观察**：Overlay **位置与尺寸**在用户前方、易于观看，无严重错位或拉伸。

### 9.4 Log 验收清单

当用户按 9.2 完成操作后，以下各项应在当次运行的 Log 中有对应输出（实现侧在 `OverlayManager` 中已按本清单打 Log，前缀 `[VRCPinYin.验收]`）。验收时在 Console 中筛选 `[VRCPinYin.验收]`，逐项核对并在下表打勾。

| # | 验收点（Log 中应出现的内容） | 通过 |
|---|------------------------------|------|
| 1 | Overlay 创建成功：出现 `[VRCPinYin.验收] Overlay 创建成功, handle=...`（且非 0） | ☐ |
| 2 | Overlay 创建失败时：出现 `[VRCPinYin.验收] CreateOverlay 失败: ...` 或 `OpenVR.Overlay 不可用...` | ☐ |
| 3 | Show() 被调用：出现 `Show() 已调用, IsVisible=true` | ☐ |
| 4 | Hide() 被调用：出现 `Hide() 已调用, IsVisible=false` | ☐ |
| 5 | Toggle() 被调用：出现 `Toggle() 已调用, 当前 IsVisible=... -> 将切换为 ...` | ☐ |
| 6 | 手柄 Toggle 触发：已绑定 Toggle Action 并按键时，Log 中先后出现 Toggle + Show 或 Toggle + Hide（即 5 与 3/4 组合） | ☐ |
| 7 | 纹理未就绪时不执行 Show：未赋 Overlay Texture 时调用 Show()，出现 `Show() 纹理未就绪，未执行 ShowOverlay`；或 Start 时出现 `未指定 overlayTexture...` | ☐ |
| 8 | OnDisable / 销毁：Stop Play 时出现 `OnDisable: DestroyOverlay 已调用, handle 已置为 Invalid` | ☐ |
| 9 | ComputeIntersection 被调用：传入射线后出现 `ComputeIntersection 已调用, 结果 hit=true/false` 及必要时 point/uv/distance | ☐ |
| 10 | FindOverlay 恢复（若发生）：ShowOverlay 返回 InvalidHandle/UnknownOverlay 时出现 `ShowOverlay 返回 ... 尝试 FindOverlay 恢复`，成功时出现 `FindOverlay 恢复成功, 新 handle=...` | ☐ |
| 11 | 多实例（若发生）：场景中存在多个 OverlayManager 时，第二个出现 `OverlayManager 已存在，将销毁重复实例。` | ☐ |

**说明**：若某条在本次操作中未触发（例如未绑定 Toggle 则第 6 条无；无 handle 失效则第 10 条无；仅一个 Instance 则第 11 条无），可在该条标注「未触发」或「N/A」，其余条须在 Log 中有对应输出且含义/取值符合预期，方算 Log 验收通过。

### 9.5 验收结论

- **人工观察**：9.3 中各观察项均符合描述，则观察部分通过。
- **Log 验收**：9.4 清单中本次操作触发的条目均在 Log 中有对应输出且判定通过，则 Log 部分通过。
- 两部分均通过即本模块验收完成。后续模块 2～5 完成后，按 [README 工作流程](../../README.md#工作流程) 做端到端总验收。

---

## 10. 参考资料

### 本仓库

- [ARCHITECTURE.md](../ARCHITECTURE.md) - 总体架构
- [DECISIONS.md](../DECISIONS.md) - ADR-001: 使用 SteamVR Overlay；ADR-005: Unity 2022.3 LTS；ADR-009: 单 exe 架构

### SteamVR / OpenVR 开发

- [SteamVR Overlay Tutorial](https://developer.valvesoftware.com/wiki/SteamVR/Environments/Overlay_Tutorial) - Valve 官方 Overlay 教程（环境与 Overlay 概念、创建与管理）
- [SteamVR Unity Plugin](https://valvesoftware.github.io/steamvr_unity_plugin/index.html) - SteamVR Unity 插件文档（概览与高层概念）
- [CVROverlay API](https://valvesoftware.github.io/steamvr_unity_plugin/api/Valve.VR.CVROverlay.html) - Overlay API：CreateOverlay、ShowOverlay、HideOverlay、SetOverlayTexture、SetOverlayTransformAbsolute、ComputeOverlayIntersection 等
- [OpenVR API Documentation](https://github.com/ValveSoftware/openvr/wiki/API-Documentation) - OpenVR API 总览（SteamVR Overlay 所基于的底层 API）

### 本仓库内可参考实现

- `Assets/SteamVR/Scripts/SteamVR_Overlay.cs` - 官方 Overlay 组件（纹理、位置、相交）
- `Assets/SteamVR/Extras/SteamVR_FirstPersonOverlay.cs` - 第一人称相机投到 Overlay 的示例（创建、纹理、TrackedDeviceRelative、Destroy）

---

*此文档已完成详细设计，实现时以本文与 SteamVR 官方 API 为准。*
