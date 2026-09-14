# Synerixis — 跨境多店客服工作台 + AI 辅助起草

面向 **CBEC 商家**（Shopee / TikTok Shop 等）的 **多店统一收件箱 + 订单上下文 AI 草稿（人审后发送）**，而非「全自动聊天机器人替人值班」，也非国内淘宝/抖店通用助手。战略说明见 [`docs/MARKET_FIT_AND_POSITIONING.md`](docs/MARKET_FIT_AND_POSITIONING.md)。

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET Version](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/)

## 定位（一句话）

卖家绑定跨境店铺 → 统一收件箱 → 意图 / 查单物流 → **AI 起草回复 → 坐席确认发送**；复杂场景转人工。自动发送仅限政策允许通道（官方 autoreply / 站外等），不以 Chat API 伪装 chatbot。

Phase 1 只打透 **Shopee + TikTok Shop**。淘宝 / 抖店 **不再作为 Phase1 目标**（历史代码与文案中如有残留，视为废弃方向）。

## 本地开发环境

| 组件 | 要求 |
|------|------|
| OS | Windows 11 |
| IDE | Visual Studio 2022（打开 `Synerixis.sln`） |
| 前端 | HBuilder（`frontend/`，多端 H5 / 小程序） |
| 数据库 | MySQL 8.x（连接串放 `.env.mysql`，已 gitignore） |
| 运行时 | .NET 8 |

密钥与店铺 Token 走配置（`Shopee:*` / `TikTok:*`），**不要**提交进仓库。

## Phase 1 平台

| 平台 | 地区侧重 | 状态 |
|------|----------|------|
| **Shopee** | 东南亚 + 台湾 | 客户端 / Webhook / 发信 / 订单查询（v2）已落地，闭环见 `docs/SHOPEE_CLOSED_LOOP.md` |
| **TikTok Shop** | 东南亚 + 英美 | 客户端与 Webhook 骨架已有，联调中 |

Phase 2 候选：Lazada、Amazon、AliExpress 等（不做承诺排期）。

## 仓库结构（Clean Architecture）

```
Synerixis.sln
├── Synerixis.Api/              # Webhook、商户端 API
├── Synerixis.Application/      # Agent、意图、会话服务
├── Synerixis.Domain/           # 实体与枚举
├── Synerixis.Infrastructure/   # Shopee/TikTok 客户端、EF、LLM
├── frontend/                   # HBuilder / uni-app 前端
└── docs/                       # 商业计划、闭环清单、定价草案
```

细节见 `PROJECT_STRUCTURE.md`；实施节奏见 `MVP_IMPLEMENTATION_PLAN.md`。

## 文档索引

| 文档 | 说明 |
|------|------|
| [`docs/MARKET_FIT_AND_POSITIONING.md`](docs/MARKET_FIT_AND_POSITIONING.md) | 市场适配与战略再定位（工作台 + AI 起草；Shopee Chat 合规） |
| [`docs/BUSINESS_PLAN_CBEC.md`](docs/BUSINESS_PLAN_CBEC.md) | CBEC 商业计划（替代国内 SME 幻想叙事；Y1 付费店目标 30–100） |
| [`docs/SHOPEE_CLOSED_LOOP.md`](docs/SHOPEE_CLOSED_LOOP.md) | Shopee 闭环缺口清单（OAuth→…→handoff） |
| [`docs/PRICING_DRAFT.md`](docs/PRICING_DRAFT.md) | 定价草案 |
| `业务计划书.docx` | 旧版 Word，**以 docs 下 Markdown 为准**，文件保留不删 |

## 当前工程重点

- 打透 Shopee：签名校验 → 会话落库 → 意图 → OrderAgent / 平台订单 API → `SendReplyAsync` → 商户转人工
- TikTok Shop 对齐同一套 `IPlatformClient` 契约
- 不碰 `RegimeTrader` / 量化模型；那是旁路资产，非本产品主线

## 许可证与联系

MIT License。GitHub: [sdrom2008/Synerixis](https://github.com/sdrom2008/Synerixis) · Email: sdrom2008@qq.com
