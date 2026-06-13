# Phase 2 Test Layers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 交付 `AUC-TESTING-009 Test Layers` 的产品级元数据闭环，使测试层级名称、attribute 和非法 layer 校验可稳定断言。

**Architecture:** `TestLayerNames` 提供标准 category snapshot 和 known-category 判断；`TestLayerAttribute` 支持 enum 和 string 两种声明方式，并拒绝未知 layer。

**Tech Stack:** .NET `net10.0` Debug 目标、xUnit、`engineering/check-docs.sh`、`engineering/check-public-api.sh`。

---

## 任务

- [ ] 更新 `docs/modules/testing/api-contracts.md`，记录 `AllCategories`、`IsKnownCategory` 和 `TestLayerAttribute(string)`。
- [ ] 写失败测试。
- [ ] 实现最小 API。
- [ ] 更新 `AUC-TESTING-009` 状态并运行完整门禁。
