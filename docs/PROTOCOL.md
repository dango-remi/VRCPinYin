# VRCPinYin 通信协议

> 最后更新：2026-03-16
> 版本：1.0.0
> 状态：设计阶段

---

## 目录

1. [概述](#1-概述)
2. [连接规范](#2-连接规范)
3. [消息格式](#3-消息格式)
4. [消息类型](#4-消息类型)
5. [状态机](#5-状态机)
6. [错误处理](#6-错误处理)
7. [示例流程](#7-示例流程)
8. [类型定义](#8-类型定义)

---

## 1. 概述

### 1.1 目的

本文档定义了 VRCPinYin 项目中 VR 端（Overlay）和 PC 端（Server）之间的 WebSocket 通信协议。

### 1.2 适用范围

- 模块 2：WebSocket 服务（PC 端）
- 模块 8：通信客户端（VR 端）
- 所有需要理解消息格式的模块

### 1.3 设计原则

| 原则 | 说明 |
|------|------|
| **简洁** | 消息格式尽量简单，减少解析开销 |
| **可扩展** | 预留扩展字段，方便未来添加功能 |
| **明确** | 每个字段都有明确的类型和含义 |
| **容错** | 客户端和服务端都应该处理未知消息类型 |

---

## 2. 连接规范

### 2.1 WebSocket 端点

| 属性 | 值 |
|------|-----|
| 协议 | WebSocket (ws://) |
| 地址 | 127.0.0.1 |
| 端口 | 8765 |
| 完整 URL | `ws://127.0.0.1:8765` |

### 2.2 连接流程

```
VR 端                                     PC 端
  │                                         │
  │  1. WebSocket Connect                   │
  │────────────────────────────────────────▶│
  │                                         │
  │  2. Connection Accepted                 │
  │◀────────────────────────────────────────│
  │                                         │
  │  3. Initialize (可选)                   │
  │────────────────────────────────────────▶│
  │                                         │
  │  4. Initialize Response                 │
  │◀────────────────────────────────────────│
  │                                         │
  │  5. 正常通信 (按键、候选词等)            │
  │◀───────────────────────────────────────▶│
  │                                         │
```

### 2.3 心跳机制

为了检测连接是否存活，双方需要实现心跳机制：

| 参数 | 值 | 说明 |
|------|-----|------|
| 心跳间隔 | 30 秒 | VR 端每 30 秒发送一次 ping |
| 超时时间 | 10 秒 | PC 端 10 秒内未收到 pong 则认为连接断开 |
| 重连延迟 | 3 秒 | 连接断开后等待 3 秒重连 |

#### 心跳消息

**Ping (VR → PC)**
```json
{
  "type": "ping",
  "timestamp": 1234567890
}
```

**Pong (PC → VR)**
```json
{
  "type": "pong",
  "timestamp": 1234567890
}
```

### 2.4 重连机制

VR 端应该实现自动重连：

```
1. 检测到连接断开
   │
   ▼
2. 等待 3 秒
   │
   ▼
3. 尝试重新连接
   │
   ├─── 成功 ──▶ 恢复正常通信
   │
   └─── 失败 ──▶ 返回步骤 2（最多重试 10 次）
                  │
                  └─── 10 次失败 ──▶ 显示错误提示，等待用户手动重试
```

---

## 3. 消息格式

### 3.1 基本结构

所有消息都是 JSON 格式，具有以下基本结构：

```typescript
interface BaseMessage {
  type: string;        // 消息类型
  id?: string;         // 消息 ID（可选，用于请求-响应模式）
  timestamp?: number;  // 时间戳（可选，Unix 时间戳，毫秒）
  data?: any;          // 消息数据（根据 type 不同而不同）
}
```

### 3.2 消息 ID

消息 ID 用于请求-响应模式，确保响应与请求对应：

- 格式：UUID v4
- 示例：`"550e8400-e29b-41d4-a716-446655440000"`
- 用途：需要响应的消息（如 `initialize`、`get_candidates`）

### 3.3 时间戳

时间戳用于调试和日志：

- 格式：Unix 时间戳（毫秒）
- 示例：`1234567890123`
- 用途：所有消息都可以包含时间戳

---

## 4. 消息类型

### 4.1 消息类型总览

| 方向 | 类型 | 说明 | 是否需要响应 |
|------|------|------|--------------|
| VR → PC | `initialize` | 初始化连接 | ✅ 是 |
| PC → VR | `initialize_response` | 初始化响应 | - |
| VR → PC | `key_press` | 按键事件 | ❌ 否 |
| VR → PC | `key_release` | 按键释放 | ❌ 否 |
| PC → VR | `candidates` | 候选词列表 | - |
| VR → PC | `select_candidate` | 选择候选词 | ❌ 否 |
| VR → PC | `send_text` | 发送文本 | ✅ 是 |
| PC → VR | `send_text_response` | 发送文本响应 | - |
| VR → PC | `set_output_mode` | 设置输出模式 | ✅ 是 |
| PC → VR | `set_output_mode_response` | 设置输出模式响应 | - |
| VR → PC | `clear_input` | 清空输入 | ❌ 否 |
| VR → PC | `backspace` | 退格 | ❌ 否 |
| VR → PC | `ping` | 心跳 Ping | ✅ 是 |
| PC → VR | `pong` | 心跳 Pong | - |
| PC → VR | `error` | 错误消息 | - |

### 4.2 详细消息定义

---

#### 4.2.1 initialize（初始化）

**方向**：VR → PC

**说明**：VR 端连接成功后，发送初始化消息，告知 PC 端自己的信息。

**请求**：
```json
{
  "type": "initialize",
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "timestamp": 1234567890123,
  "data": {
    "version": "1.0.0",
    "platform": "windows",
    "device": "Meta Quest Pro",
    "steamvr_version": "2.0.0"
  }
}
```

**字段说明**：

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| version | string | 是 | VR 端版本号 |
| platform | string | 是 | 平台（windows/macos/linux） |
| device | string | 否 | VR 设备名称 |
| steamvr_version | string | 否 | SteamVR 版本 |

---

#### 4.2.2 initialize_response（初始化响应）

**方向**：PC → VR

**说明**：PC 端响应初始化消息，返回服务端信息。

**响应**：
```json
{
  "type": "initialize_response",
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "timestamp": 1234567890123,
  "data": {
    "version": "1.0.0",
    "success": true,
    "ime_available": true,
    "ime_name": "微软拼音",
    "osc_available": true,
    "default_output_mode": "osc"
  }
}
```

**字段说明**：

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| version | string | 是 | PC 端版本号 |
| success | boolean | 是 | 是否初始化成功 |
| ime_available | boolean | 是 | 输入法是否可用 |
| ime_name | string | 否 | 当前输入法名称 |
| osc_available | boolean | 是 | OSC 是否可用 |
| default_output_mode | string | 是 | 默认输出模式（osc/clipboard） |

---

#### 4.2.3 key_press（按键事件）

**方向**：VR → PC

**说明**：用户按下虚拟键盘上的按键时发送。

**请求**：
```json
{
  "type": "key_press",
  "timestamp": 1234567890123,
  "data": {
    "key": "n",
    "modifiers": []
  }
}
```

**字段说明**：

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| key | string | 是 | 按键值（见按键编码表） |
| modifiers | string[] | 否 | 修饰键（shift/ctrl/alt） |

**按键编码表**：

| 类型 | 按键 | 编码 |
|------|------|------|
| 字母 | A-Z | "a" - "z" |
| 数字 | 0-9 | "0" - "9" |
| 空格 | Space | "space" |
| 退格 | Backspace | "backspace" |
| 回车 | Enter | "enter" |
| 删除 | Delete | "delete" |
| Tab | Tab | "tab" |
| Escape | Escape | "escape" |
| 方向键 | 上/下/左/右 | "up"/"down"/"left"/"right" |
| 功能键 | F1-F12 | "f1" - "f12" |

---

#### 4.2.4 key_release（按键释放）

**方向**：VR → PC

**说明**：用户释放虚拟键盘上的按键时发送（可选，用于支持长按）。

**请求**：
```json
{
  "type": "key_release",
  "timestamp": 1234567890123,
  "data": {
    "key": "n"
  }
}
```

---

#### 4.2.5 candidates（候选词列表）

**方向**：PC → VR

**说明**：PC 端根据当前拼音返回候选词列表。

**响应**：
```json
{
  "type": "candidates",
  "timestamp": 1234567890123,
  "data": {
    "pinyin": "ni",
    "raw_pinyin": "ni",
    "candidates": [
      { "index": 0, "text": "ni", "type": "raw" },
      { "index": 1, "text": "你", "type": "word" },
      { "index": 2, "text": "尼", "type": "word" },
      { "index": 3, "text": "妮", "type": "word" },
      { "index": 4, "text": "拟", "type": "word" },
      { "index": 5, "text": "逆", "type": "word" }
    ],
    "page": 1,
    "page_size": 6,
    "total_pages": 3,
    "total_count": 15,
    "selected_text": "你好"
  }
}
```

**字段说明**：

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| pinyin | string | 是 | 当前正在输入的拼音 |
| raw_pinyin | string | 是 | 原始拼音（未分词） |
| candidates | Candidate[] | 是 | 候选词数组 |
| page | number | 是 | 当前页码（从 1 开始） |
| page_size | number | 是 | 每页候选词数量 |
| total_pages | number | 是 | 总页数 |
| total_count | number | 是 | 候选词总数 |
| selected_text | string | 否 | 已选择的文本（之前选中的词） |

**Candidate 结构**：

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| index | number | 是 | 候选词索引（从 0 开始） |
| text | string | 是 | 候选词文本 |
| type | string | 是 | 类型（raw=原始拼音/word=词语/phrase=短语） |

---

#### 4.2.6 select_candidate（选择候选词）

**方向**：VR → PC

**说明**：用户选择一个候选词时发送。

**请求**：
```json
{
  "type": "select_candidate",
  "timestamp": 1234567890123,
  "data": {
    "index": 1,
    "text": "你"
  }
}
```

**字段说明**：

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| index | number | 是 | 候选词索引 |
| text | string | 是 | 候选词文本（用于验证） |

---

#### 4.2.7 send_text（发送文本）

**方向**：VR → PC

**说明**：用户完成输入，请求发送文本到 VRChat。

**请求**：
```json
{
  "type": "send_text",
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "timestamp": 1234567890123,
  "data": {
    "text": "你好，我是 VRChat 用户",
    "mode": "osc"
  }
}
```

**字段说明**：

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| text | string | 是 | 要发送的文本 |
| mode | string | 是 | 输出模式（osc/clipboard） |

---

#### 4.2.8 send_text_response（发送文本响应）

**方向**：PC → VR

**说明**：PC 端响应发送文本请求。

**响应（成功）**：
```json
{
  "type": "send_text_response",
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "timestamp": 1234567890123,
  "data": {
    "success": true,
    "text": "你好，我是 VRChat 用户",
    "mode": "osc"
  }
}
```

**响应（失败）**：
```json
{
  "type": "send_text_response",
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "timestamp": 1234567890123,
  "data": {
    "success": false,
    "error": {
      "code": "OSC_SEND_FAILED",
      "message": "OSC 发送失败：VRChat 未响应"
    }
  }
}
```

---

#### 4.2.9 set_output_mode（设置输出模式）

**方向**：VR → PC

**说明**：用户切换输出模式时发送。

**请求**：
```json
{
  "type": "set_output_mode",
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "timestamp": 1234567890123,
  "data": {
    "mode": "clipboard"
  }
}
```

**字段说明**：

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| mode | string | 是 | 输出模式（osc/clipboard） |

---

#### 4.2.10 set_output_mode_response（设置输出模式响应）

**方向**：PC → VR

**说明**：PC 端响应设置输出模式请求。

**响应**：
```json
{
  "type": "set_output_mode_response",
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "timestamp": 1234567890123,
  "data": {
    "success": true,
    "mode": "clipboard"
  }
}
```

---

#### 4.2.11 clear_input（清空输入）

**方向**：VR → PC

**说明**：用户清空当前输入时发送。

**请求**：
```json
{
  "type": "clear_input",
  "timestamp": 1234567890123,
  "data": {}
}
```

---

#### 4.2.12 backspace（退格）

**方向**：VR → PC

**说明**：用户按退格键时发送。

**请求**：
```json
{
  "type": "backspace",
  "timestamp": 1234567890123,
  "data": {
    "count": 1
  }
}
```

**字段说明**：

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| count | number | 否 | 删除字符数（默认 1） |

---

#### 4.2.13 ping / pong（心跳）

见 [2.3 心跳机制](#23-心跳机制)

---

#### 4.2.14 error（错误消息）

**方向**：PC → VR

**说明**：PC 端发生错误时发送。

**消息**：
```json
{
  "type": "error",
  "timestamp": 1234567890123,
  "data": {
    "code": "IME_NOT_AVAILABLE",
    "message": "输入法不可用",
    "details": "未检测到中文输入法，请先安装"
  }
}
```

---

## 5. 状态机

### 5.1 输入状态

VR 端和 PC 端需要维护同步的输入状态：

```
┌─────────────────────────────────────────────────────────────┐
│                        输入状态                              │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  state: "idle" | "typing" | "selecting" | "confirming"     │
│  pinyin: string          // 当前输入的拼音                   │
│  selected_text: string   // 已选择的文本                     │
│  candidates: Candidate[] // 当前候选词列表                   │
│  output_mode: "osc" | "clipboard"  // 输出模式              │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 5.2 状态转换

```
                    ┌─────────┐
                    │  idle   │
                    └────┬────┘
                         │ key_press (字母)
                         ▼
                  ┌──────────────┐
         ┌───────▶│   typing     │◀───────┐
         │        └──────┬───────┘        │
         │               │                │
         │               │ candidates 返回 │
         │               ▼                │
         │        ┌──────────────┐        │
         │        │  selecting   │        │
         │        └──────┬───────┘        │
         │               │                │
         │  ┌────────────┼────────────┐   │
         │  │            │            │   │
         │  │ 继续输入    │ 选择候选词  │   │
         │  │            │            │   │
         │  ▼            ▼            │   │
         │  └────────────┴────────────┘   │
         │               │                │
         │               │ send_text      │ backspace (清空)
         │               ▼                │
         │        ┌──────────────┐        │
         │        │ confirming   │        │
         │        └──────┬───────┘        │
         │               │                │
         │               │ success        │
         │               ▼                │
         │        ┌──────────────┐        │
         └────────│    idle      │◀───────┘
                  └──────────────┘
```

### 5.3 状态说明

| 状态 | 说明 | 触发条件 |
|------|------|----------|
| idle | 空闲状态，无输入 | 初始状态；发送成功；清空输入 |
| typing | 正在输入拼音 | 用户按下字母键 |
| selecting | 正在选择候选词 | PC 端返回候选词列表 |
| confirming | 确认发送 | 用户点击发送按钮 |

---

## 6. 错误处理

### 6.1 错误码

| 错误码 | 说明 | 处理建议 |
|--------|------|----------|
| `CONNECTION_ERROR` | 连接错误 | 检查网络连接，尝试重连 |
| `IME_NOT_AVAILABLE` | 输入法不可用 | 提示用户安装中文输入法 |
| `IME_ERROR` | 输入法错误 | 重启 PC 端服务 |
| `OSC_SEND_FAILED` | OSC 发送失败 | 检查 VRChat 是否运行；切换到剪贴板模式 |
| `CLIPBOARD_ERROR` | 剪贴板错误 | 检查 VRChat 是否有焦点 |
| `INVALID_MESSAGE` | 无效消息 | 检查消息格式 |
| `UNKNOWN_MESSAGE_TYPE` | 未知消息类型 | 忽略该消息 |
| `VERSION_MISMATCH` | 版本不匹配 | 更新 VR 端或 PC 端 |

### 6.2 错误消息格式

```json
{
  "type": "error",
  "timestamp": 1234567890123,
  "data": {
    "code": "ERROR_CODE",
    "message": "错误描述",
    "details": "详细信息（可选）",
    "request_id": "原始请求 ID（如果是对请求的响应）"
  }
}
```

### 6.3 错误处理策略

| 场景 | 处理方式 |
|------|----------|
| WebSocket 连接断开 | VR 端自动重连，显示"连接中..."提示 |
| 心跳超时 | PC 端主动断开连接，VR 端触发重连 |
| 消息解析失败 | 忽略该消息，记录日志 |
| 未知消息类型 | 忽略该消息，记录日志 |
| IME 不可用 | 显示错误提示，禁用输入功能 |
| OSC 发送失败 | 提示用户切换到剪贴板模式 |

---

## 7. 示例流程

### 7.1 完整输入流程

```
VR 端                                           PC 端
  │                                               │
  │  1. initialize                                │
  │──────────────────────────────────────────────▶│
  │                                               │
  │  2. initialize_response                       │
  │◀──────────────────────────────────────────────│
  │                                               │
  │  3. key_press: "n"                            │
  │──────────────────────────────────────────────▶│
  │                                               │ (查询候选词)
  │  4. candidates: ["n", "你", "尼", ...]        │
  │◀──────────────────────────────────────────────│
  │                                               │
  │  5. key_press: "i"                            │
  │──────────────────────────────────────────────▶│
  │                                               │ (查询候选词)
  │  6. candidates: ["ni", "你", "尼", ...]       │
  │◀──────────────────────────────────────────────│
  │                                               │
  │  7. select_candidate: "你"                    │
  │──────────────────────────────────────────────▶│
  │                                               │ (记录选择)
  │  8. candidates: ["", "好", "号", ...]         │
  │◀──────────────────────────────────────────────│
  │                 (selected_text: "你")         │
  │                                               │
  │  9. key_press: "h"                            │
  │──────────────────────────────────────────────▶│
  │                                               │
  │  10. key_press: "a"                           │
  │──────────────────────────────────────────────▶│
  │                                               │
  │  11. key_press: "o"                           │
  │──────────────────────────────────────────────▶│
  │                                               │
  │  12. candidates: ["hao", "好", "号", ...]     │
  │◀──────────────────────────────────────────────│
  │                 (selected_text: "你")         │
  │                                               │
  │  13. select_candidate: "好"                   │
  │──────────────────────────────────────────────▶│
  │                                               │
  │  14. candidates: []                           │
  │◀──────────────────────────────────────────────│
  │               (selected_text: "你好")         │
  │                                               │
  │  15. send_text: "你好", mode: "osc"           │
  │──────────────────────────────────────────────▶│
  │                                               │ (发送 OSC)
  │  16. send_text_response: success              │
  │◀──────────────────────────────────────────────│
  │                                               │
  │  (状态重置为 idle)                             │
  │                                               │
```

### 7.2 切换输出模式流程

```
VR 端                                           PC 端
  │                                               │
  │  1. set_output_mode: "clipboard"              │
  │──────────────────────────────────────────────▶│
  │                                               │
  │  2. set_output_mode_response: success         │
  │◀──────────────────────────────────────────────│
  │                                               │
```

### 7.3 错误处理流程

```
VR 端                                           PC 端
  │                                               │
  │  1. send_text: "你好", mode: "osc"            │
  │──────────────────────────────────────────────▶│
  │                                               │ (OSC 发送失败)
  │  2. send_text_response: failed                │
  │◀──────────────────────────────────────────────│
  │     (error: OSC_SEND_FAILED)                  │
  │                                               │
  │  (显示错误提示，建议切换模式)                   │
  │                                               │
```

---

## 8. 类型定义

### 8.1 TypeScript 类型定义

```typescript
// 基础消息
interface BaseMessage {
  type: string;
  id?: string;
  timestamp?: number;
  data?: any;
}

// 初始化消息
interface InitializeMessage extends BaseMessage {
  type: "initialize";
  data: {
    version: string;
    platform: "windows" | "macos" | "linux";
    device?: string;
    steamvr_version?: string;
  };
}

// 初始化响应
interface InitializeResponseMessage extends BaseMessage {
  type: "initialize_response";
  data: {
    version: string;
    success: boolean;
    ime_available: boolean;
    ime_name?: string;
    osc_available: boolean;
    default_output_mode: "osc" | "clipboard";
  };
}

// 按键事件
interface KeyPressMessage extends BaseMessage {
  type: "key_press";
  data: {
    key: string;
    modifiers?: ("shift" | "ctrl" | "alt")[];
  };
}

// 候选词
interface Candidate {
  index: number;
  text: string;
  type: "raw" | "word" | "phrase";
}

// 候选词列表
interface CandidatesMessage extends BaseMessage {
  type: "candidates";
  data: {
    pinyin: string;
    raw_pinyin: string;
    candidates: Candidate[];
    page: number;
    page_size: number;
    total_pages: number;
    total_count: number;
    selected_text?: string;
  };
}

// 选择候选词
interface SelectCandidateMessage extends BaseMessage {
  type: "select_candidate";
  data: {
    index: number;
    text: string;
  };
}

// 发送文本
interface SendTextMessage extends BaseMessage {
  type: "send_text";
  data: {
    text: string;
    mode: "osc" | "clipboard";
  };
}

// 发送文本响应
interface SendTextResponseMessage extends BaseMessage {
  type: "send_text_response";
  data: {
    success: boolean;
    text?: string;
    mode?: "osc" | "clipboard";
    error?: {
      code: string;
      message: string;
    };
  };
}

// 设置输出模式
interface SetOutputModeMessage extends BaseMessage {
  type: "set_output_mode";
  data: {
    mode: "osc" | "clipboard";
  };
}

// 错误消息
interface ErrorMessage extends BaseMessage {
  type: "error";
  data: {
    code: string;
    message: string;
    details?: string;
    request_id?: string;
  };
}

// 输入状态
interface InputState {
  state: "idle" | "typing" | "selecting" | "confirming";
  pinyin: string;
  selected_text: string;
  candidates: Candidate[];
  output_mode: "osc" | "clipboard";
}
```

### 8.2 C# 类型定义

```csharp
// 文件: Protocol/MessageTypes.cs

namespace VRCPinYin.Protocol
{
    // 基础消息
    public class BaseMessage
    {
        public string Type { get; set; }
        public string Id { get; set; }
        public long Timestamp { get; set; }
    }

    // 按键事件消息
    public class KeyPressMessage : BaseMessage
    {
        public KeyPressData Data { get; set; }
    }

    public class KeyPressData
    {
        public string Key { get; set; }
        public string[] Modifiers { get; set; }
    }

    // 候选词
    public class Candidate
    {
        public int Index { get; set; }
        public string Text { get; set; }
        public string Type { get; set; } // "raw" | "word" | "phrase"
    }

    // 候选词列表消息
    public class CandidatesMessage : BaseMessage
    {
        public CandidatesData Data { get; set; }
    }

    public class CandidatesData
    {
        public string Pinyin { get; set; }
        public string RawPinyin { get; set; }
        public Candidate[] Candidates { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
        public string SelectedText { get; set; }
    }

    // 发送文本消息
    public class SendTextMessage : BaseMessage
    {
        public SendTextData Data { get; set; }
    }

    public class SendTextData
    {
        public string Text { get; set; }
        public string Mode { get; set; } // "osc" | "clipboard"
    }

    // 输出模式
    public enum OutputMode
    {
        Osc,
        Clipboard
    }

    // 输入状态
    public enum InputState
    {
        Idle,
        Typing,
        Selecting,
        Confirming
    }
}
```

---

## 附录

### A. 版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| 1.0.0 | 2026-03-16 | 初始版本 |

### B. 待办事项

- [ ] 添加翻页相关消息（next_page, prev_page）
- [ ] 添加语音输入支持（未来）
- [ ] 添加多输入法支持（五笔等）

---

*本文档由 VRCPinYin 团队维护*
