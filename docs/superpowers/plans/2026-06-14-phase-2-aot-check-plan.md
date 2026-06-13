# Phase 2 AOT Check Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 交付 `AUC-TESTING-008 AOT Check` 的产品级测试闭环，使测试可以检查 runtime reflection、dynamic code 和 unbounded activator 等 Native AOT 风险。

**Architecture:** `AotCompatibilityCheck` 继续使用轻量 source pattern scanner，并新增默认规则集和 cancellation token。结果仍返回 immutable diagnostic snapshot，不引入真实 IL metadata scanner。

**Tech Stack:** .NET `net10.0` Debug 目标、xUnit、`engineering/check-docs.sh`、`engineering/check-public-api.sh`。

---

## 任务

- [ ] 更新 `docs/modules/testing/api-contracts.md`，记录 `ForbidDefaultAotPatterns` 和 `Evaluate(..., CancellationToken)`。
- [ ] 写默认规则失败测试。
- [ ] 写取消失败测试。
- [ ] 实现默认规则、重复 rule 防护和 token 观察。
- [ ] 更新 `AUC-TESTING-008` 状态并运行完整门禁。
