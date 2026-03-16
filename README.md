# VRCPinYin

一个为 VRChat 设计的 SteamVR Overlay 中文输入法工具。

## 背景

在使用 Meta Quest Pro 通过串流（Virtual Desktop / Oculus Link / Steam Link）玩 VRChat 时，头显自带的键盘**不支持中文输入法**，导致在 VR 中无法输入中文。

VRCPinYin 通过在 PC 端处理输入法，并将界面以 SteamVR Overlay 的形式叠加在 VRChat 之上，解决了这个问题。

## 核心特性

- ✅ **双模式文字输出**：OSC 模式（聊天框）+ 剪贴板模式（任意输入框）
- ✅ **系统输入法集成**：利用 Windows 输入法，词库丰富
- ✅ **SteamVR Overlay**：全局可用，不依赖 VRChat 世界
- ✅ **VR 交互**：手柄射线点击，操作直观

## 系统架构

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              SteamVR 环境                                │
│                                                                         │
│  ┌───────────────────────────────┐    ┌─────────────────────────────┐  │
│  │           VRChat              │    │      VRCPinYin Overlay      │  │
│  │           (游戏)               │    │      (Unity 程序)            │  │
│  │                               │    │                             │  │
│  │  ┌─────────────────────────┐  │    │  ┌───────────────────────┐  │  │
│  │  │      聊天框              │◄─┼────┼──│ OSC /chatbox/input    │  │  │
│  │  └─────────────────────────┘  │    │  └───────────────────────┘  │  │
│  │                               │    │                             │  │
│  │  ┌─────────────────────────┐  │    │  ┌───────────────────────┐  │  │
│  │  │     其他输入框           │◄─┼────┼──│ 剪贴板 + Ctrl+V       │  │  │
│  │  └─────────────────────────┘  │    │  └───────────────────────┘  │  │
│  └───────────────────────────────┘    └──────────────┬──────────────┘  │
│                                                       │                 │
│                              ┌────────────────────────┘                 │
│                              │ WebSocket (127.0.0.1:8765)               │
│                              ▼                                          │
└─────────────────────────────────────────────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                         PC 端服务 (Windows)                              │
│                                                                         │
│  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐     │
│  │  WebSocket 服务  │───▶│   输入法引擎     │───▶│   文字输出      │     │
│  └─────────────────┘    └─────────────────┘    └─────────────────┘     │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

## 工作流程

1. **唤起**：用户在 VR 中按下快捷键，VRCPinYin Overlay 浮现在视野中
2. **输入**：用户使用控制器点击虚拟键盘上的拼音字母
3. **处理**：按键通过 WebSocket 发送到 PC 端，PC 端调用系统输入法引擎
4. **展示**：PC 端返回候选词列表，Overlay 展示候选词
5. **选择**：用户点击选择候选词，组成句子
6. **输出**：
   - **OSC 模式**：直接发送到 VRChat 聊天框（默认）
   - **剪贴板模式**：复制到剪贴板，模拟 Ctrl+V 粘贴

## 文档

### 核心文档

| 文档 | 说明 | 状态 |
|------|------|------|
| [ARCHITECTURE.md](./docs/ARCHITECTURE.md) | 总体架构设计 | ✅ 已完成 |
| [DECISIONS.md](./docs/DECISIONS.md) | 技术决策记录 (ADR) | ✅ 已完成 |
| [PROTOCOL.md](./docs/PROTOCOL.md) | 通信协议定义 | ✅ 已完成 |

### 模块文档

| 模块 | 文档 | 说明 | 状态 |
|------|------|------|------|
| 模块 1 | PROTOCOL.md | 通信协议 | ✅ 已完成 |
| 模块 2 | [SERVER-WEBSOCKET.md](./docs/SERVER-WEBSOCKET.md) | PC 端 WebSocket 服务 | 📝 待完成 |
| 模块 3 | [SERVER-IME.md](./docs/SERVER-IME.md) | PC 端输入法引擎 | 📝 待完成 |
| 模块 4 | [SERVER-OUTPUT.md](./docs/SERVER-OUTPUT.md) | PC 端文字输出 | 📝 待完成 |
| 模块 5 | [OVERLAY-FRAMEWORK.md](./docs/OVERLAY-FRAMEWORK.md) | VR 端 Overlay 框架 | 📝 待完成 |
| 模块 6 | [OVERLAY-KEYBOARD.md](./docs/OVERLAY-KEYBOARD.md) | VR 端虚拟键盘 UI | 📝 待完成 |
| 模块 7 | [OVERLAY-CANDIDATES.md](./docs/OVERLAY-CANDIDATES.md) | VR 端候选词面板 | 📝 待完成 |
| 模块 8 | [OVERLAY-NETWORK.md](./docs/OVERLAY-NETWORK.md) | VR 端通信客户端 | 📝 待完成 |

## 技术栈

### VR 端 (Overlay)

| 组件 | 技术选型 | 版本 |
|------|----------|------|
| 游戏引擎 | Unity | 2022.3 LTS |
| VR 框架 | SteamVR Unity Plugin | 2.x |
| UI 框架 | Unity UI (Canvas) | 内置 |
| WebSocket | WebSocketSharp | 最新 |
| JSON 解析 | Newtonsoft.Json | 最新 |

### PC 端 (Server)

| 组件 | 技术选型 | 版本 |
|------|----------|------|
| 运行时 | .NET | 6/7 |
| WebSocket | WebSocketSharp 或 Fleck | 最新 |
| IME 交互 | Windows TSF API | Windows API |
| OSC | OscCore | 最新 |
| JSON 解析 | System.Text.Json | 内置 |

## 项目结构

```
VRCPinYin/
├── README.md                    # 项目介绍
├── docs/                        # 文档
│   ├── ARCHITECTURE.md          # 总体架构
│   ├── DECISIONS.md             # 技术决策记录
│   ├── PROTOCOL.md              # 通信协议
│   ├── SERVER-WEBSOCKET.md      # 模块 2 文档
│   ├── SERVER-IME.md            # 模块 3 文档
│   ├── SERVER-OUTPUT.md         # 模块 4 文档
│   ├── OVERLAY-FRAMEWORK.md     # 模块 5 文档
│   ├── OVERLAY-KEYBOARD.md      # 模块 6 文档
│   ├── OVERLAY-CANDIDATES.md    # 模块 7 文档
│   └── OVERLAY-NETWORK.md       # 模块 8 文档
├── Overlay/                     # Unity 项目 (VR 端)
│   └── Assets/
│       ├── Scripts/
│       │   ├── Overlay/         # 模块 5
│       │   ├── Keyboard/        # 模块 6
│       │   ├── Candidates/      # 模块 7
│       │   └── Network/         # 模块 8
│       ├── Prefabs/
│       └── Scenes/
└── Server/                      # PC 端服务
    └── VRCPinYinServer/
        ├── WebSocket/           # 模块 2
        ├── IME/                 # 模块 3
        └── Output/              # 模块 4
```

## 功能特性

### P0 (核心功能)

- [ ] 基础拼音输入
- [ ] 候选词展示与选择
- [ ] OSC 模式发送到聊天框
- [ ] 剪贴板模式输出

### P1 (重要功能)

- [ ] 手柄快捷键唤起
- [ ] 输出模式切换

### P2 (增强功能)

- [ ] 常用词组/短语记忆
- [ ] 键盘布局自定义

### P3 (未来功能)

- [ ] 多输入法支持（五笔等）

## 开发进度

🚧 项目处于**设计阶段**

- ✅ 架构设计完成
- ✅ 通信协议定义完成
- 📝 模块详细设计进行中
- ⬜ 开发阶段

## 许可证

MIT License

## 致谢

- [VRChat](https://vrchat.com) - 提供优秀的 VR 社交平台
- [SteamVR](https://store.steampowered.com/steamvr) - 提供 Overlay API
- [OscCore](https://github.com/vrchat/OscCore) - VRChat 团队开发的 OSC 库
