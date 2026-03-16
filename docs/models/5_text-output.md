# 模块 5：文字输出

> 状态：📝 待完成
> 依赖：无（进程内调用 OSC / 剪贴板）

---

## 概述

本文档描述文字输出模块的详细设计。本模块运行在 Unity 进程中，根据用户选择的输出模式将拼好的文字发送到 VRChat：OSC 模式发往聊天框，剪贴板模式复制后模拟 Ctrl+V。

**待完成内容：**

- [ ] OSC 输出实现（OscCore，/chatbox/input）
- [ ] 剪贴板输出实现（剪贴板 + Ctrl+V，需提示用户确保 VRChat 窗口焦点）
- [ ] SendInput 实现（备选）
- [ ] 模式切换（发送 / 复制到剪贴板按钮与 README UI 一致）
- [ ] 错误处理
- [ ] API 设计
- [ ] 代码示例

---

## 参考资料

- [ARCHITECTURE.md](../ARCHITECTURE.md) - 总体架构
- [DECISIONS.md](../DECISIONS.md) - ADR-003: 双模式文字输出；ADR-008: OscCore

---

*此文档将由指定 AI 完成详细设计*
