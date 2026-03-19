# VRCPinYin

一个为 VRChat 设计的 SteamVR Overlay 中文输入法工具。

## 背景

在使用 Meta Quest Pro 通过串流（Virtual Desktop / Oculus Link / Steam Link）玩 VRChat 时，头显自带的键盘**不支持中文输入法**，导致在 VR 中无法输入中文。

VRCPinYin 通过 SteamVR Overlay 提供虚拟键盘与候选词界面，调用 Windows 输入法（TSF）并经由 OSC 或剪贴板将文字送入 VRChat，解决上述问题。

## 核心特性

- ✅ **双模式文字输出**：OSC 模式（聊天框）+ 剪贴板模式（任意输入框）
- ✅ **系统输入法集成**：进程内调用 Windows TSF，词库丰富
- ✅ **SteamVR Overlay**：全局可用，无需安装 VRChat 插件即可使用
- ✅ **一键打开输入界面**：在vrc中打开系统输入框一般走圆盘菜单，不方便。VRCPinYin可以绑定一个按键，一键打开界面

## UI 示意图

Overlay 浮层大致布局

```
┌─────────────────────────────────────────────────────────┐
│  ┌───────────────────────────────────────────┐  ┌────┐  │
│  │ 你好，我是...                              │  │ 🎙️ │  │
│  └───────────────────────────────────────────┘  └────┘  │
├─────────────────────────────────────────────────────────┤
│  [ 1 你 ] [ 2 泥 ] [ 3 尼 ] [ 4 妮 ] [ 5 拟 ] [ 6 逆 ]   │
│  ◀ 上一页                                    下一页 ▶   │
├─────────────────────────────────────────────────────────┤
│  Q  W  E  R  T  Y  U  I  O  P      [退格]                │
│   A  S  D  F  G  H  J  K  L                             │
│    Z  X  C  V  B  N  M                                  │
│  [ 微软拼音输入法 ] [中/英]      [ 发送 ]  [ 复制到剪贴板 ]│
└─────────────────────────────────────────────────────────┘
```

## 工作流程

1. **唤起**：用户在 VR 中按下快捷键，VRCPinYin Overlay 浮现在视野中
2. **输入**：用户使用控制器点击虚拟键盘上的拼音字母；或点击输入框旁 🎙️ 使用语音转文字（P3）
3. **处理**：按键在进程内交给输入法引擎（Windows TSF），得到候选词
4. **展示**：候选词在 Overlay 的候选词面板中显示
5. **选择**：用户点击选择候选词，组成句子
6. **输出**：
   - **OSC 模式**：直接发送到 VRChat 聊天框（默认）
   - **剪贴板模式**：复制到剪贴板，可以在vrc的搜索框等处粘贴使用

## 系统架构

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              SteamVR 环境                               │
│                                                                         │
│  ┌───────────────────────────────┐    ┌─────────────────────────────┐  │
│  │           VRChat              │    │      VRCPinYin（单 exe）     │  │
│  │           (游戏)               │    │      Unity 应用             │  │
│  │                               │    │                             │  │
│  │  ┌─────────────────────────┐  │    │  Overlay + 虚拟键盘          │  │
│  │  │      聊天框              │◄─┼────┼── OSC /chatbox/input        │  │
│  │  └─────────────────────────┘  │    │  候选词 + TSF + 剪贴板       │  │
│  │  ┌─────────────────────────┐  │    │                             │  │
│  │  │     其他输入框           │◄─┼────┼── 剪贴板 + Ctrl+V           │  │
│  │  └─────────────────────────┘  │    │                             │  │
│  └───────────────────────────────┘    └─────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────┘
```

## 文档

### 核心文档

| 文档 | 说明 |
|------|------|
| [ARCHITECTURE.md](./docs/ARCHITECTURE.md) | 总体架构设计 |
| [DECISIONS.md](./docs/DECISIONS.md) | 技术决策记录 (ADR) |

### 模块文档（docs/models/）

| 模块 | 文档 | 说明 | 状态 |
|------|------|------|------|
| 1 | [1_overlay-framework.md](./docs/models/1_overlay-framework.md) | Overlay 框架 | ✅ 已验收 |
| 2 | [2_keyboard-ui.md](./docs/models/2_keyboard-ui.md) | 虚拟键盘 UI | ✅ 已验收 |
| 3 | [3_candidates-panel.md](./docs/models/3_candidates-panel.md) | 候选词面板 | 📝 待完成 |
| 4 | [4_ime-engine.md](./docs/models/4_ime-engine.md) | 输入法引擎（TSF） | 📝 待完成 |
| 5 | [5_text-output.md](./docs/models/5_text-output.md) | 文字输出（OSC/剪贴板） | 📝 待完成 |

## 技术栈

| 组件 | 技术选型 | 版本 |
|------|----------|------|
| 游戏引擎 | Unity | 2022.3 LTS |
| VR 框架 | SteamVR Unity Plugin | 2.x |
| UI 框架 | Unity UI (Canvas) | 内置 |
| 输入法 | Windows TSF API | P/Invoke 或 C++ 插件 |
| OSC | OscCore | 最新 |
| JSON | Newtonsoft.Json | 最新（如需） |

## 项目结构

```
VRCPinYin/
├── README.md                    # 项目介绍
├── .prompt.md                   # AI 协作说明
├── docs/                        # 文档
│   ├── ARCHITECTURE.md          # 总体架构
│   ├── DECISIONS.md             # 技术决策记录
│   └── models/                  # 模块文档
│       ├── 1_overlay-framework.md
│       ├── 2_keyboard-ui.md
│       ├── 3_candidates-panel.md
│       ├── 4_ime-engine.md
│       └── 5_text-output.md
└── (Unity 项目目录)             # 单 Unity 工程
    └── Assets/
        ├── Scripts/
        │   ├── Overlay/         # 模块 1
        │   ├── Keyboard/        # 模块 2
        │   ├── Candidates/      # 模块 3
        │   ├── IME/             # 模块 4
        │   └── Output/          # 模块 5
        ├── Prefabs/
        └── Scenes/
```

## 功能特性

### P0 (核心功能)

- [ ] 基础拼音输入
- [ ] 候选词展示与选择
- [ ] OSC 模式发送到聊天框
- [ ] 剪贴板模式输出

### P1 (重要功能)

- [x] 手柄快捷键唤起
- [ ] 输出模式切换

### P2 (增强功能)

- [ ] 常用词组/短语记忆
- [ ] 键盘布局自定义

### P3 (未来功能)

- [ ] 多输入法支持（五笔等）
- [ ] 支持语言转文字

## 开发进度

🚧 项目处于**开发阶段**

- ✅ 架构设计完成（单 exe）
- ✅ 模块详细设计完成（models/）
- 🔨 开发阶段进行中
  - ✅ 模块 1（Overlay 框架）已验收
  - ✅ 模块 2（虚拟键盘 UI）已验收
  - ⬜ 模块 3（候选词面板）
  - ⬜ 模块 4（输入法引擎）
  - ⬜ 模块 5（文字输出）

## 许可证

MIT License

## 致谢

- [VRChat](https://vrchat.com) - 提供优秀的 VR 社交平台
- [SteamVR](https://store.steampowered.com/steamvr) - 提供 Overlay API
- [OscCore](https://github.com/vrchat/OscCore) - VRChat 团队开发的 OSC 库
