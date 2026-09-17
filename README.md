# Synerixis — 跨境多店客服工作台 + AI 辅助起草

面向 **CBEC 商家**（Shopee / TikTok Shop 等）的 **多店统一收件箱 + 订单上下文 AI 草稿（人审后发送）**，而非「全自动聊天机器人替人值班」，也非国内淘宝/抖店通用助手。战略说明见 [`docs/MARKET_FIT_AND_POSITIONING.md`](docs/MARKET_FIT_AND_POSITIONING.md)。

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET Version](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/)

## 明日本地演示（唯一点击路径）

见 **[`docs/DEMO.md`](docs/DEMO.md)**：账号 / 端口 / seed / 收件箱人审发送 / 注入 / Admin。  
无 LLM Key 也可演示（规则草稿 + SIM mock）；Key / BaseUrl 在 Admin「LLM Provider」、商家「AI 设置」或 `Llm:*`。见 [`docs/LLM_PROVIDERS.md`](docs/LLM_PROVIDERS.md)。

## 定位（一句话）

卖家绑定跨境店铺 → 统一收件箱 → 意图 / 查单物流 → **AI 起草回复 → 坐席确认发送**；复杂场景转人工。自动发送仅限政策允许通道（官方 autoreply / 站外等），不以 Chat API 伪装 chatbot。

Phase 1 只打透 **Shopee + TikTok Shop**。淘宝 / 抖店 **不再作为 Phase1 目标**（历史代码与文案中如有残留，视为废弃方向）。

## 本地一键（Docker）

```bash
cp .env.example .env && docker compose up -d --build
./scripts/smoke.sh
```

详见 [`docs/DOCKER.md`](docs/DOCKER.md)、[`docs/PRODUCT_STATUS.md`](docs/PRODUCT_STATUS.md)。

## 本地开发环境

| 组件 | 要求 |
|------|------|
| OS | Windows 11 |
| IDE | Visual Studio 2022（打开 `Synerixis.sln`） |
| 商家移动端 | HBuilder（`frontend/`，H5 / 小程序） |
| 商家桌面端 | `merchant-web/`（Vue3 + Vite，PC 浏览器） |
| 运营后台 | `admin-console/`（Vue3 + Element Plus） |
| 数据库 | MySQL 8.x（连接串放 `.env.mysql`，已 gitignore） |
| 运行时 | .NET 8 |

密钥与店铺 Token 走配置（`Shopee:*` / `TikTok:*`），**不要**提交进仓库。

## 商家主入口：merchant-web（桌面工作台）

**网页端优先于移动端。** PC 浏览器打开 `merchant-web/`（`npm run dev`，默认 Vite 代理 `/api`）。

| 登录方式 | 说明 |
|----------|------|
| 商家手机登录 | `POST /api/auth/phone-login`；开发环境验证码 **123456** |
| 坐席邮箱登录 | `POST /api/auth/agent-login`；密码为商家在「团队」页设置的初始密码（存 SHA256+salt） |
| 粘贴 Token | 调试用，粘贴已有 JWT |

- 登录后默认进入 **收件箱**（draft-first + SLA badge + 转人工）。
- **Seller / Supervisor / Admin**：概览、收件箱、店铺、AI 设置、计费、团队。
- **Agent（普通坐席）**：仅收件箱 + 只读概览 KPI；无团队/店铺/计费/AI 设置权限。
- 开发测号：`POST /api/auth/init-agent`（仅 Development）可创建 `admin@test.com` / `Agent123!`。

更多见 [`docs/MERCHANT_WEB.md`](docs/MERCHANT_WEB.md)。角色与权限矩阵（以代码为准）见 [`docs/ROLES.md`](docs/ROLES.md)。

AI 用量：专用 Agent（order/logistics/competitor/product 等）经 `IAiUsageRecorder` 记账；无模型 Usage 时 chars/4 估算并 `IsEstimated`；商家 Overview 近 7 日图接 `GET /api/merchant/usage/daily`。

Token 过期：店铺页状态标签 + 醒目警告条「立即刷新」；顶栏徽章跳转 `/shops`；审计日志见 `/audit` 与 Admin `/audit`。



## Phase 1 平台

| 平台 | 地区侧重 | 状态 |
|------|----------|------|
| **Shopee** | 东南亚 + 台湾 | 客户端 / Webhook / 草稿优先出站 / 订单查询（v2）已落地，见 [`docs/SHOPEE_CLOSED_LOOP.md`](docs/SHOPEE_CLOSED_LOOP.md)、[`docs/ISV_APPLICATION_CHECKLIST.md`](docs/ISV_APPLICATION_CHECKLIST.md) |
| **TikTok Shop** | 东南亚 + 英美 | 客户端与 Webhook 骨架已有，联调中 |

Phase 2 候选：Lazada、Amazon、AliExpress 等（不做承诺排期）。

## 仓库结构（Clean Architecture）

```
Synerixis.sln
├── Synerixis.Api/              # Webhook、商户端 API
├── Synerixis.Application/      # Agent、意图、会话服务
├── Synerixis.Domain/           # 实体与枚举
├── Synerixis.Infrastructure/   # Shopee/TikTok 客户端、EF、LLM
├── frontend/                   # HBuilder / uni-app 商家移动端
├── merchant-web/               # 商家 PC 桌面工作台（Vue3）
├── admin-console/              # 运营 Admin 控制台
└── docs/                       # 商业计划、闭环清单、定价草案
```

细节见 `PROJECT_STRUCTURE.md`；实施节奏见 `MVP_IMPLEMENTATION_PLAN.md`。

## 文档索引

| 文档 | 说明 |
|------|------|
| [`docs/ROLES.md`](docs/ROLES.md) | 角色与权限矩阵（Seller/Agent/Supervisor/Admin，以代码为准） |
| [`docs/MARKET_FIT_AND_POSITIONING.md`](docs/MARKET_FIT_AND_POSITIONING.md) | 市场适配与战略再定位（工作台 + AI 起草；Shopee Chat 合规） |
| [`docs/BUSINESS_PLAN_CBEC.md`](docs/BUSINESS_PLAN_CBEC.md) | CBEC 商业计划（替代国内 SME 幻想叙事；Y1 付费店目标 30–100） |
| [`docs/SHOPEE_CLOSED_LOOP.md`](docs/SHOPEE_CLOSED_LOOP.md) | Shopee 闭环缺口清单（OAuth→草稿→人审→handoff） |
| [`docs/PRODUCT_STATUS.md`](docs/PRODUCT_STATUS.md) | 产品状态总览（已完成 vs 需外部账号） |
| [`docs/ISV_APPLICATION_CHECKLIST.md`](docs/ISV_APPLICATION_CHECKLIST.md) | Shopee+TikTok ISV 申请证据清单 |
| [`docs/PRICING_DRAFT.md`](docs/PRICING_DRAFT.md) | 定价草案 |
| `业务计划书.docx` | 旧版 Word，**以 docs 下 Markdown 为准**，文件保留不删 |

## 当前工程重点

- 打透 Shopee：签名校验 → 会话落库 → 意图 → OrderAgent → **AI 草稿（默认）** → 人审 `SendReplyAsync` → 商户转人工
- TikTok Shop 对齐同一套 `IPlatformClient` 契约
- 不碰 `RegimeTrader` / 量化模型；那是旁路资产，非本产品主线

## 许可证与联系

MIT License。GitHub: [sdrom2008/Synerixis](https://github.com/sdrom2008/Synerixis) · Email: sdrom2008@qq.com
