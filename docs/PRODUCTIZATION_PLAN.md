# Synerixis 产品化计划（跨境电商 AI 客服 SaaS）

> 目标读者：产品 / 前端 / 后端。语言：中文。首发平台：**Shopee**。

## 1. 目标

交付可对 **跨境电商（CBEC）商家** 售卖的 SaaS：

- 商家绑定 Shopee 店铺后，Webhook 入站 → 意图识别 → Agent 路由 → 自动回复（订单 / 物流等）。
- 提供 **专业商家控制台**（uni-app H5，可兼顾桌面宽屏）与 **运营 Admin 控制台**（Vue3 + Element Plus）。
- 计费与用量可观测，首个付费店铺可闭环上线。

**非目标（本阶段）**：多平台同时首发、完整营销站、玩具感 Demo UI。

## 2. 阶段划分

### P0 — 环境与后端闭环（大部分已完成）

- [x] .NET 分层 API、EF、Shopee OAuth / Webhook / Agent 路由
- [x] Box / 本机开发文档与本地 MySQL 示例配置
- [ ] 迁移脚本在目标库一键应用；健康检查 `/health` 含 DB
- [ ] 关键 Partner 沙箱联调清单（见 `docs/SHOPEE_CLOSED_LOOP.md`）

**产出**：商家可绑店、消息可进线并自动回复（沙箱）。

### P1 — 商家控制台重设计（frontend uni-app H5）

视觉标准：**现代 SaaS**（清晰栅格、统一间距、图表、空状态），专业而非玩具。

模块：

| 模块 | 说明 | 状态 |
|------|------|------|
| 设计 Token / 共享组件 | `styles/tokens.scss`、KpiCard、EmptyState、SessionTimeline、AppShell | [x] |
| 仪表盘 Dashboard | KPI：今日会话、自动解决率、待人工、店铺状态；折线/柱状占位；无数据用「—」 | [x] |
| 店铺绑定 | Shopee OAuth 入口、连接状态、Token 过期提示 UI | [x] |
| 收件箱 / 会话 | 会话列表、消息时间线、转人工标记（接 `/api/merchant/sessions*`） | [x] |
| AI 设置 | 语气、自动回复开关、业务时段（本地 + SellerConfig；时段字段待后端） | [x] |
| 计费 Billing | 套餐卡片对齐 `PRICING_DRAFT.md`（静态）；用量/发票待 API | [x] |
| 知识片段（简版） | AI 设置内知识库片段编辑 | [ ] |
| 真实 KPI / 图表数据 | 商家端专用报表埋点与 ECharts 接入 | [ ] |

**产出**：H5/桌面宽屏可用的商家端主路径，中文文案统一。（2026-09-14 前端壳与主路径已落地）

### P1b — Admin 控制台从零搭建（admin-console）

当前 `admin-console` 仅为依赖壳；本阶段交付可构建的专业壳：

| 侧栏 | 职责 |
|------|------|
| 概览 | 租户/商家/会话/费用 KPI（占位，禁止伪造生产指标） |
| 商家 | 租户与商家账号、启停 |
| 店铺连接 | 平台连接与健康 |
| 会话监控 | 实时/近期会话抽样、失败原因 |
| 用量计费 | Token / 消息用量、模型成本 |
| 系统设置 | 环境、特性开关、管理员 |

技术：Vue3 + Element Plus + Pinia + Vue Router + ECharts；支持 **深色 / 浅色** 专业风。

### P2 — 可靠性与人工协同

- Token 刷新（Shopee refresh）与过期告警
- 人工接管（handoff）状态机与商家端入口
- 幂等表（Webhook / 出站消息去重）
- 基础审计日志

### P3 — 增长面

- 营销站点 + 定价页（对齐 `docs/PRICING_DRAFT.md`）
- 自助 Onboarding（注册 → 绑店 → 首条自动回复）
- 文档中心与状态页

## 3. UI 设计原则

1. **专业，不玩具**：克制插画、避免花哨渐变堆叠；留白与对齐优先。
2. **中文文案**：按钮/空状态/错误提示全部中文；术语与 Shopee 商家习惯一致。
3. **双端重心**：
   - Admin：**桌面优先**（≥1280），侧栏 + 顶栏。
   - 商家：H5 优先，桌面宽屏自适应（卡片栅格）。
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

