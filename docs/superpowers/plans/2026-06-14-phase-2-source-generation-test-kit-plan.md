# Phase 2 Source Generation Test Kit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 交付 `AUC-TESTING-007 Source Generation Kit` 的最小产品级闭环，使 source generator 测试可以运行 Roslyn generator、收集 generated source snapshot 和 diagnostics。

**Architecture:** `SourceGenerationTestCase` 继续负责保存 source inputs 和 expected diagnostics，并新增 `Run(ISourceGenerator, CancellationToken)`。Runner 在内存中构建 `CSharpCompilation`，执行 generator driver，按 hint name 生成 `GeneratedSourceSnapshot`，并返回 `SourceGenerationTestResult`。

**Tech Stack:** .NET `net10.0` Debug 目标、`Microsoft.CodeAnalysis.CSharp`、xUnit、`engineering/check-docs.sh`、`engineering/check-public-api.sh`。

---

## 范围

包含：

- `AtomUI.City.Testing` 引用 `Microsoft.CodeAnalysis.CSharp`。
- `SourceGenerationTestCase.Run(ISourceGenerator, CancellationToken)`。
- `SourceGenerationTestResult` 公开 snapshot、diagnostics 和 output compilation diagnostics。
- cancellation token 在 parse 和 generator driver 前观察。

不包含：

- incremental generator overload。
- additional files。
- analyzer config options。
- snapshot 文件写入。

## 任务

- [ ] 更新 `docs/modules/testing/api-contracts.md`，记录 `SourceGenerationTestCase.Run` 和 `SourceGenerationTestResult`。
- [ ] 写 runner 失败测试：运行一个测试 generator，断言 generated source snapshot。
- [ ] 写 cancellation 失败测试。
- [ ] 实现最小 runner。
- [ ] 更新 `AUC-TESTING-007` 状态并运行完整门禁。
