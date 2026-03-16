# VRCPinYin 工程创建指南

本文说明如何从零创建 Unity 工程并接入 SteamVR Overlay，与仓库文档和模块设计保持一致。

---

## 前置条件

- **Windows 10/11**（TSF、SteamVR 均需 Windows）
- **Unity Hub** 已安装
- **Steam** 与 **SteamVR** 已安装（开发与运行 Overlay 时需要）
- 本仓库已克隆到本地（如 `F:\Openclaw\VRCPinYin`）

---

## 步骤 1：安装 Unity 2022.3 LTS

1. 打开 **Unity Hub**。
2. 进入 **安装 (Installs)** → **安装编辑器**。
3. 选择 **Unity 2022.3.x LTS**（建议选当前最新的 2022.3 小版本）。
4. 在 **模块** 中勾选：
   - **Windows Build Support**（含 MSVC）
   - 如需在编辑器中直接测 VR，可勾选 **Android Build Support** 等（非必须）。
5. 完成安装。

> 版本依据：[ADR-005](DECISIONS.md#adr-005-vr-端使用-unity-20223-lts) 选定 Unity 2022.3 LTS。

---

## 步骤 2：创建新 Unity 项目

1. 在 Unity Hub 中点击 **新建项目**。
2. 选择 **Unity 2022.3.x**。
3. **模板**：选择 **3D (Core)** 或 **3D (URP)** 均可；Overlay 以 UI 为主，3D Core 即可，后续可再改渲染管线。
4. **项目位置**：放在本仓库**子目录**下，便于文档与代码同仓管理，例如：
   - `F:\Openclaw\VRCPinYin\Unity`
   - 或 `F:\Openclaw\VRCPinYin\VRCPinYin`（与 README 中「Unity 项目目录」对应）
5. **项目名称**：如 `VRCPinYin`。
6. 点击 **创建项目**，等待 Unity 生成工程。

生成后，该目录下会有 `Assets/`、`ProjectSettings/`、`Packages/` 等，即「(Unity 项目目录)」。

---

## 步骤 3：导入 SteamVR Unity Plugin

1. 打开 [SteamVR Unity Plugin - Releases](https://github.com/ValveSoftware/steamvr_unity_plugin/releases)。
2. 下载最新 **2.x** 的 `.unitypackage`（例如 `SteamVR_2.x.x.unitypackage`）。
3. 在 Unity 菜单：**Assets → Import Package → Custom Package...**，选择刚下载的包。
4. 在导入窗口中全选，点击 **Import**。
5. 若出现 **SteamVR / Input 绑定** 等设置向导，按提示完成（可先选默认，后续再细调）。

> 技术栈约定：[README 技术栈](README.md#技术栈) 使用 SteamVR Unity Plugin 2.x。

---

## 步骤 4：创建项目目录结构

在 `Assets` 下按模块建立脚本与资源目录，与 [ARCHITECTURE 3.3](ARCHITECTURE.md#33-代码产出目录约定) 一致：

```
Assets/
├── Scripts/
│   ├── Overlay/       # 模块 1：Overlay 框架
│   ├── Keyboard/      # 模块 2：虚拟键盘 UI
│   ├── Candidates/    # 模块 3：候选词面板
│   ├── IME/           # 模块 4：输入法引擎（TSF）
│   └── Output/        # 模块 5：文字输出（OSC/剪贴板）
├── Prefabs/
├── Scenes/
└── (其他按需，如 Materials、UI 等)
```

在 Unity 中：

1. 在 **Project** 窗口右键 `Assets` → **Create → Folder**，创建 `Scripts`。
2. 在 `Scripts` 下创建子文件夹：`Overlay`、`Keyboard`、`Candidates`、`IME`、`Output`。
3. 若尚无 `Prefabs`、`Scenes`，同样创建（Scenes 可能已有默认场景）。

---

## 步骤 5：确认/创建初始场景

1. 打开默认场景（如 `Assets/Scenes/SampleScene.unity`），或新建一个场景（如 `VRCPinYin.unity`）并保存到 `Assets/Scenes/`。
2. 后续在 [1_overlay-framework](models/1_overlay-framework.md) 中会设计 Overlay 的 Unity 场景结构（相机、Canvas、EventSystem 等），这里只需先有一个可运行的空场景即可。

---

## 步骤 6：后续依赖（按开发阶段添加）

| 阶段     | 用途           | 添加方式 |
|----------|----------------|----------|
| 输入法   | Windows TSF    | P/Invoke 或 C++ 插件，见 [4_ime-engine](models/4_ime-engine.md) |
| OSC 输出 | VRChat 聊天框  | 通过 Package Manager 或 Git URL 添加 [OscCore](https://github.com/vrchat/OscCore) |
| JSON     | 配置等（如需）  | Newtonsoft.Json，Package Manager 或 Unity 内置 |

可在实现对应模块时再按文档引入，不必在创建工程时一次性安装。

---

## 工程与仓库的关系

- **推荐**：Unity 工程放在仓库**子目录**（如 `Unity/` 或 `VRCPinYin/`），与根目录的 `README.md`、`docs/` 并列。
- **.gitignore**：若 Unity 工程在仓库内，建议在仓库根目录添加或合并 Unity 常用忽略项（如 `[Ll]ibrary/`、`[Tt]emp/`、`[Oo]bj/`、`[Bb]uild/`、`*.csproj` 等；具体可参考 Unity 官方 .gitignore 模板）。

---

## 检查清单

- [ ] Unity 2022.3 LTS 已安装
- [ ] 新项目已创建在仓库子目录下
- [ ] SteamVR Unity Plugin 2.x 已导入
- [ ] `Assets/Scripts/` 下已建好 Overlay、Keyboard、Candidates、IME、Output 五个子目录
- [ ] 至少有一个可运行场景

完成后即可按 [ARCHITECTURE 开发路线图](ARCHITECTURE.md#5-开发路线图) 从阶段 1（Overlay + 键盘 + 候选词壳）开始实现。
