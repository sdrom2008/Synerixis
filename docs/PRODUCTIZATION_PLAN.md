# Synerixis 产品化计划（跨境电商 AI 客服 SaaS）

> 目标读者：产品 / 前端 / 后端。语言：中文。首发平台：**Shopee**。  
> **定位对齐**：主产品是 **跨境多店客服工作台 + AI 辅助起草**（draft-first / human-in-the-loop），不是对外宣称的全自动 chatbot。详见 [`MARKET_FIT_AND_POSITIONING.md`](./MARKET_FIT_AND_POSITIONING.md)。

## 1. 目标

交付可对 **跨境电商（CBEC）商家** 售卖的 SaaS：

- 商家绑定 Shopee 店铺后，Webhook 入站 → 意图识别 → Agent 路由 → **生成回复草稿（默认坐席确认后发送）**；订单 / 物流上下文注入。
- 提供 **商家移动端**（`frontend/` uni-app）、**商家 PC 桌面工作台**（`merchant-web/` Vue3 + Element Plus）与 **运营 Admin 控制台**（`admin-console/`）——统一收件箱为 P0/P1 重心。
- 计费与用量可观测，首个付费店铺可闭环上线；Chat API / ISV 合规（禁止促销广播与 chatbot 滥用）写进 Onboarding。

**非目标（本阶段）**：多平台同时首发、完整营销站、玩具感 Demo UI、国内微信登录核心路径、竞品分析/文案 Agent。

## 2. 阶段划分

### P0 — 环境与后端闭环（大部分已完成）

- [x] .NET 分层 API、EF、Shopee OAuth / Webhook / Agent 路由
- [x] Box / 本机开发文档与本地 MySQL 示例配置
- [x] 健康检查 `/health`（live）与 `/health/ready`（含 EF DbContext）；迁移脚本仍需目标库按需执行
- [ ] 关键 Partner 沙箱联调清单（见 `docs/SHOPEE_CLOSED_LOOP.md`）

**产出**：商家可绑店、消息可进线；沙箱可草稿/按策略出站（默认人审路径优先）。

### P1 — 商家控制台重设计（frontend uni-app H5）

视觉标准：**现代 SaaS**（清晰栅格、统一间距、图表、空状态），专业而非玩具。

模块：

| 模块 | 说明 | 状态 |
|------|------|------|
| 设计 Token / 共享组件 | `styles/tokens.scss`、KpiCard、EmptyState、SessionTimeline、AppShell | [x] |
| 仪表盘 Dashboard | KPI：今日会话、自动解决率、待人工、店铺状态；折线/柱状占位；无数据用「—」 | [x] |
| 店铺绑定 | Shopee OAuth 入口、连接状态、Token 过期提示 UI | [x] |
| 收件箱 / 会话 | 待发送草稿角标、草稿编辑/发送/丢弃、超时排序字段 | [x] |
| AI 设置 | 语气、草稿生成、`OutboundMode`、营业时段、自动 handoff / 敏感词 | [x] |
| 计费 Billing | 套餐卡片对齐 `PRICING_DRAFT.md`；本月消息数接 `/api/merchant/usage` | [x] |
| 知识片段（简版） | merchant-web「快捷回复」CRUD + Inbox 插入；AI 起草注入前 N 条 | [x] |
| 真实 KPI / 图表数据 | `GET /api/merchant/dashboard` 已接真实聚合；趋势图/ECharts 仍占位 | [~] |

**产出**：H5/桌面宽屏可用的商家端主路径，中文文案统一。（2026-09-14 前端壳与主路径已落地）


### P1 深化（2026-09-14）

- 后端：`GET /api/merchant/dashboard`、`GET /api/merchant/usage`（诚实 DB 聚合；自动解决率无今日已结束会话时返回 `null`）。
- `SellerConfig` 新增 `EnableAutoReply` / `BusinessHoursStart` / `BusinessHoursEnd`；启动时 `SchemaPatcher` + `Migrations/AddSellerConfigAiSettings_20260914.sql`（因 `EnsureCreated` 不改已有表）。
- Webhook：关闭自动回复时仅落库不 `SendReply`（不打断验签与会话创建）。
- 商家 JWT：`shopId` 回退为 `Seller.Id`，修复 sessions 接口无 shop 声明失败。


### P1c — Draft-first 出站（2026-09-14）

- [x] `SellerConfig.OutboundMode` 默认 `DraftFirst`；`AutoSend` 显式开启
- [x] Webhook AI 路径落 `draft_messages`，默认不 `SendReplyAsync`
- [x] 商户 API：list drafts / get / approve / edit-send / discard
- [x] 会话暴露 `NeedsResponseBy` / `hoursSinceLastBuyerMsg`
- [x] 前端收件箱角标 + 会话详情草稿操作 + 设置风险文案
- [x] ISV 清单：`docs/ISV_APPLICATION_CHECKLIST.md`

### P1d — 商家桌面工作台（merchant-web）（2026-09-14）

独立于 uni-app `frontend/` 与 `admin-console/` 的 **PC 浏览器商家控制台**：

| 项 | 说明 |
|----|------|
| 技术 | Vue3 + Vite + TS + Element Plus + Pinia + Vue Router + Axios |
| 壳 | 顶栏 + 左导航：概览、收件箱、店铺绑定、AI 设置、计费、团队 |
| 收件箱 | 三栏：会话列表 \| 消息时间线+草稿审发 \| 订单侧栏；多店筛选 |
| API | MerchantController：sessions（platform/connectionId）/ orders / shop-options / connections（Supervisor 通） |
| 路径 | 仓库根目录 `merchant-web/`；详见 `docs/MERCHANT_WEB.md` |

**MVP 状态（网页端可演示）**：登录 → 绑店（Seller/Supervisor）→ 收件箱审发/转人工/SLA → 订单侧栏 → 多店筛选 → Token 后台刷新。详见验收清单。

**本轮增量（2026-09-14）**：Token 刷新失败 → 重绑引导（`LastRefreshError` + Shops UI）；Admin 登录/禁用商家/改订阅审计；出站优先写平台 `message_id`。

**不**替换移动端 `frontend/`。**AI token 记账**（`AiUsageLog`）已落地；Admin P1b 与 Webhook 幂等已落地。

### P1d — Handoff 硬闸 + SLA 超时唤醒（2026-09-14）

- [x] `ChatSession.PendingHumanHandoff`：转人工后停新 AI 草稿与 AutoSend（不破坏新建会话 draft-first）
- [x] 转人工可将旧 Pending 草稿标为 `Superseded`，人审仍可发送
- [x] `SellerConfig.ResponseSlaHours` / `AlertThresholdHours`；会话列表 `slaUrgency`
- [x] `GET /api/merchant/alerts` 应用内告警列表
- [x] 商家端：转人工按钮生效闸；收件箱「即将超时 / 已超时」徽章；AI 设置可改 SLA 小时

### P1e — 自动 handoff + 营业时间外策略（2026-09-14）

- [x] `SellerConfig`：`AutoHandoffOnLowConfidence` / `HandoffConfidenceThreshold`(0.45) / `SensitiveKeywords` / `HandoffOutsideBusinessHours` / `TimeZoneId`
- [x] `SchemaPatcher` + `Migrations/AddAutoHandoffAndBusinessHours_20260914.sql`
- [x] Webhook：敏感词或低置信 → `TransferToAgent`，不生成新 AI 草稿；打日志
- [x] Webhook：营业外禁止 AutoSend；默认 handoff +「营业外」系统提示草稿
- [x] merchant-web AiSettings 暴露上述配置

### P1b — Admin 控制台可用化（admin-console）（2026-09-14）

- [x] 登录接 `POST /api/auth/agent-login`（仅 `AgentRole.Admin`）+ 路由守卫
- [x] 后端 `/api/admin/*`：dashboard / merchants / shops|connections / sessions / usage / settings
- [x] 前端各页接真实 API；无数据不伪造
- [x] `docs/ADMIN_CONSOLE.md` + README（端口 3000，proxy → :5000）

| 侧栏 | 职责 |
|------|------|
| 概览 | 商家/连接店铺/今日会话/待手审/SLA overdue |
| 商家 | 分页列表（手机/昵称/订阅/额度/连接数） |
| 店铺连接 | PlatformConnection 列表 |
| 会话监控 | 最近会话只读 |
| 用量计费 | 全站消息/会话诚实计数 |
| 系统设置 | 只读配置说明 |

技术：Vue3 + Element Plus + Pinia + Vue Router + ECharts；支持 **深色 / 浅色** 专业风。

### P1f — 健康检查 / AI 用量 / 快捷回复（2026-09-14）

- [x] `/health`（liveness）+ `/health/ready`（含 DbContext）
- [x] `AiUsageLog` + SchemaPatcher；IntentClassifier / GeneralChatAgent 成功调用后记账
- [x] `GET /api/merchant/usage`、`GET /api/admin/usage` 增加今日/本月 token 与估算费用
- [x] Merchant CRUD `/api/merchant/quick-replies`；merchant-web 管理页 + Inbox 插入；AI 起草注入上下文
- [x] Admin Settings 可拉取 health 摘要

### P1g — 意图扩展 / 物流诚实 / Purpose 分桶（2026-09-14）

- [x] `IntentClassifier`：LogisticsQuery / CompetitorAnalysis 规则优先 + LLM 枚举；AgentRouter 已映射
- [x] `LogisticsAgent`：消息/Order.LogisticsNo 解析运单；无承运商 API 不造假
- [x] `GET /api/merchant/usage`、`GET /api/admin/usage` → `byPurpose[{purpose,calls,tokens,costUsd}]`
- [x] merchant-web Billing / admin-console Usage 表格展示 byPurpose

**下一轮缺口**：真实承运商轨迹；ConversationService 与 Webhook 统一；支付生产；Token 告警 UI。

### P2 — 可靠性与人工协同

- Token 刷新（Shopee refresh）与过期告警
- [x] 人工接管（handoff）硬闸与商家端入口
- [x] 敏感词 / 低置信度自动 handoff（`AutoHandoffOnLowConfidence` + `SensitiveKeywords`）
- [x] 营业时间外不 AutoSend；`HandoffOutsideBusinessHours` 默认转人工
- 幂等表（Webhook / 出站消息去重）
- 基础审计日志
- [x] SLA 超时唤醒 UI + alerts API；merchant-web 收件箱 **声音提醒**已落地；**浏览器 Notification** 已有
- [ ] ~~Push 推送~~ **真实 APNs/FCM Push 仍无**（本阶段不做）

### P3 — 增长面

- 营销站点 + 定价页（对齐 `docs/PRICING_DRAFT.md`）
- 自助 Onboarding（注册 → 绑店 → 首条自动回复）
- 文档中心与状态页

## 3. UI 设计原则

1. **专业，不玩具**：克制插画、避免花哨渐变堆叠；留白与对齐优先。
2. **中文文案**：按钮/空状态/错误提示全部中文；术语与 Shopee 商家习惯一致。
3. **双端重心**：
   - Admin：**桌面优先**（≥1280），侧栏 + 顶栏。
   - 商家移动：`frontend/` H5 / 小程序优先。
   - 商家桌面：`merchant-web/` PC 宽屏（顶栏+侧栏；收件箱三栏）。
4. **品牌色建议**：
   - 主色：`#2563EB`（可信蓝，SaaS 通用）
   - 辅色：`#0F172A` 侧栏 / 标题；成功 `#16A34A`；警告 `#D97706`；危险 `#DC2626`
   - 浅色背景 `#F8FAFC`，深色模式背景 `#0B1220`
5. **空状态**：无店铺 / 无会话时给出「下一步」CTA，而非空白页。
6. **数据诚实**：未接通 API 的 KPI 显示「—」或「暂无数据」，**禁止编造生产指标**。

## 4. 文件 / 模块清单

### 后端（已有为主）

- `Synerixis.Api/Controllers/*` — Auth、OAuth、Webhook、会话、计费桩
- `Synerixis.Application/Agents/*`、`IntentClassifier`、`AgentRouter`
- `Synerixis.Infrastructure/Clients/Shopee*`、`Data/AppDbContext`
- `Synerixis.Api/appsettings.Development.example.json`

### 商家端 frontend（P1）

- `frontend/src/pages/` — dashboard、shops、inbox、ai-settings、billing
- `frontend/src/components/` — KpiCard、EmptyState、SessionTimeline、ChartPanel
- `frontend/src/api/` — 对接后端 REST
- `frontend/src/styles/` — 设计 token（色、间距、圆角）

### Admin（P1b，脚手架起步）

- `admin-console/src/main.ts`、`App.vue`
- `admin-console/src/router/index.ts`
- `admin-console/src/layouts/AdminLayout.vue` — 侧栏导航
- `admin-console/src/views/Login.vue`、`Dashboard.vue`
- `admin-console/src/views/{Merchants,Shops,Sessions,Usage,Settings}.vue` — 占位页
- `admin-console/src/components/KpiCard.vue`、`ThemeSwitch`（可选）
- `admin-console/README.md`

### 文档

- `docs/DEV_SETUP_BOX.md` — Box 环境
- `docs/PRODUCTIZATION_PLAN.md` — 本文
- `docs/SHOPEE_CLOSED_LOOP.md`、`docs/PRICING_DRAFT.md`

## 5. 成功指标

| 指标 | 定义 | 目标（首期） |
|------|------|----------------|
| 首个付费店铺 | 完成绑店 + 套餐付费（含测试支付亦可） | ≥ 1 |
| 自动解决率 | AI 闭环结束且无需人工的会话占比 | 基线建立后持续提升（先埋点） |
| Look & feel 评审 | 内部对商家端 / Admin 的「是否像正经 SaaS」打分 | 通过（非 Demo 感） |
| 构建健康 | `dotnet build`、admin `npm run build`、frontend install | 绿灯 |

## 6. 风险与依赖

- Shopee Partner 审核与沙箱配额
- LLM 成本需在 Admin「用量计费」可见，避免沉默烧钱
- Token 过期导致静默失败 → P2 必须做刷新与告警

## 7. 近期执行顺序（建议）

1. 固化 P0 配置与文档（本提交）
2. Admin 脚手架可 build（本提交）
3. 商家端 Dashboard + 店铺绑定 + 收件箱 + AI/计费视觉重做（已完成主路径）
4. Admin 商家/店铺/用量接真实 API
5. P2 幂等与 handoff

