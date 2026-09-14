# Admin 控制台说明（平台运营台）

## 定位

运营侧**平台控制后台**：商家治理、连接/会话监控、用量 KPI、审计、安全运营开关。  
**不**替代商家工作台（`merchant-web/`）的人审发信。

## 端口与代理

| 服务 | 默认地址 |
|------|----------|
| Admin Vite | `http://localhost:3000` |
| API | `http://localhost:5000`（本机也可能是 `7092`，以 `dotnet run` 输出为准） |
| Vite proxy | `/api`、`/health` → API |

可选环境变量：`VITE_API_BASE_URL`（直连 API，不走代理）。

## 鉴权与演示登录

1. Development：优先 `POST /api/dev/seed-demo`（见 [`LOCAL_DEMO.md`](./LOCAL_DEMO.md)），创建 Admin `admin@test.com` / `Agent123!`
2. 登录页写明演示账号；可「填入演示账号」/「加载演示数据」
3. 调用 `POST /api/auth/agent-login`，校验 `role === Admin` 后进入 **Dashboard**
4. Token 存 `localStorage.sx_admin_token`；路由守卫拦截未登录
5. 后端 `[Authorize(Roles = "Admin")]` 保护 `/api/admin/*`

兜底（仅 Dev）：`POST /api/auth/init-agent`。

## 页面能力

| 页面 | 能力 |
|------|------|
| 概览 | 真 KPI（商家/连接/今日会话/待审草稿/转人工/SLA）；近 7 日趋势接 `usage/daily` |
| 商家 | 分页/搜索；启用禁用、改订阅（PATCH）；侧栏详情看连接数/会话数 |
| 店铺连接 | 分页列表；空态提示 seed-demo |
| 会话监控 | 最近会话只读；空态友好 |
| 用量计费 | 汇总 + 日趋势；导出 CSV（`/usage/export`） |
| 审计日志 | 列表 + 动作过滤；导出 CSV（`/audit-logs/export`） |
| 系统设置 | 保存 MaintenanceMode / DefaultOutboundMode / AllowNewRegistration；Health 检查 |
| 开发工具 | 仅 Development：说明并可一键调 `seed-demo`（带 Admin token） |

## API 一览

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/admin/dashboard` | 商家数、连接店铺、今日会话、待手审草稿、待人工、SLA overdue |
| GET | `/api/admin/merchants?page=&pageSize=&q=` | 商家分页（含 connectionCount / sessionCount） |
| GET | `/api/admin/merchants/{id}` | 商家详情 + 连接列表 + 会话/草稿摘要 |
| PATCH | `/api/admin/merchants/{id}/active` | 启用/禁用 |
| PATCH | `/api/admin/merchants/{id}/subscription` | 改订阅 Free/Basic/Pro |
| GET | `/api/admin/shops` 或 `/connections` | 店铺连接列表 |
| GET | `/api/admin/sessions?take=` | 最近会话只读 |
| GET | `/api/admin/usage` | 全站用量汇总（含 AI token / exact·estimated） |
| GET | `/api/admin/usage/daily?days=7` | 全站近 N 日会话/消息/AI 日趋势 |
| GET | `/api/admin/usage/export?days=` | 用量 CSV |
| GET | `/api/admin/audit-logs?take=&shopId=&action=` | 全站审计日志 |
| GET | `/api/admin/audit-logs/export` | 审计 CSV |
| GET/PUT | `/api/admin/settings` | 只读说明 + 可写运营开关 |
| GET | `/health` / `/health/ready` | 存活 / 就绪（ready 含 EF DB） |

无数据时返回空列表 / 0，前端不编造指标。

## 本地启动

```bash
# API
dotnet run --project Synerixis.Api

# Admin
cd admin-console && npm install && npm run dev
# http://localhost:3000
```

验收：seed 后用 `admin@test.com` / `Agent123!` 登录 → 各页有数据可操作 → 系统设置维护开关可保存。
