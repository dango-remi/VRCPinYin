# 模块 4：输入法引擎

> 状态：📝 待完成
> 依赖：无（进程内调用 Windows TSF）

---

## 概述

本文档描述**进程内**输入法引擎的详细设计。本模块运行在 Unity 进程中，通过 P/Invoke 或 C++ 插件调用 Windows TSF API，将拼音转换为候选词，供模块 3（候选词面板）使用。

**待完成内容：**

- [ ] Windows TSF 在 Unity 中的调用方式（P/Invoke 或 Native Plugin）
- [ ] 拼音解析与候选词查询
- [ ] 词组组合
- [ ] 常用词记忆（可选）
- [ ] 多输入法支持（可选）
- [ ] API 设计（供模块 2、3、5 调用）
- [ ] 代码示例

---

## 参考资料

- [ARCHITECTURE.md](../ARCHITECTURE.md) - 总体架构
- [DECISIONS.md](../DECISIONS.md) - ADR-007: 使用 Windows TSF；ADR-009: 单 exe 架构（TSF 在 Unity 进程内）

---

*此文档将由指定 AI 完成详细设计*
