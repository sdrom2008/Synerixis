# merchant-web — 商家桌面工作台（主入口）

定位：跨境多店客服工作台 + **AI 起草 → 人审发送** + SLA 叫醒。**禁止全自动 chatbot。**

网页端（本目录）优先于 `frontend/` 移动端。

## 启动

```bash
# API（仓库根）
dotnet run --project Synerixis.Api

# 桌面工作台
cd merchant-web && npm install && npm run dev
```

默认登录后进入 `/inbox`。

## 如何登录

### 1. 商家手机登录

1. 打开登录页 → 「商家手机登录」
2. 输入手机号（可不含国家码，默认 +86）
3. 开发环境验证码填 `123456`
4. 成功后 JWT `userType=Seller`，可访问全菜单（含团队管理）

### 2. 坐席邮箱登录

1. 商家在「团队」页添加坐席（邮箱 / 姓名 / 初始密码 / 角色 Agent|Supervisor）
2. 登录页 → 「坐席邮箱登录」
3. JWT 含 `userType`（Agent/Supervisor/Admin）、`role`、`shopId`
4. 普通 Agent 只能进收件箱（及只读概览）；Supervisor 可管团队与店铺绑定

密码以 `sha256:salt:hash` 存储；历史明文测号在首次成功登录时自动升级。

### 3. 粘贴 Token

调试时粘贴 Bearer JWT 正文即可；前端会尽量从 payload 解析 `userType` / `shopId`。

### 开发测号

```http
POST /api/auth/init-agent
```

仅 Development：创建 `admin@test.com` / `Agent123!`（Admin 角色）。

完整演示种子（商家/会话/订单/模拟店）见 [`LOCAL_DEMO.md`](./LOCAL_DEMO.md)：`POST /api/dev/seed-demo`，手机 `13800138000` / `123456`。

## 路由权限

| 路由 | Seller | Supervisor | Admin | Agent |
|------|--------|------------|-------|-------|
| /inbox | ✓ | ✓ | ✓ | ✓ |
| /overview | ✓ | ✓ | ✓ | ✓（只读 KPI） |
| /shops | ✓ | ✓ | ✓ | ✗（友好提示） |
| /ai-settings | ✓ | ✓ | ✓ | ✗ |
| /billing | ✓ | ✓ | ✗（平台 Admin 非店角色；support 亦隐藏） | ✗ |
| /quick-replies | ✓ | ✓ | ✓ | ✗（列表 GET 坐席可读，供 Inbox 插入） |
| /team | ✓ | ✓ | ✓ | ✗ |
| /audit | ✓ | ✓ | ✓ | ✗ |
| /onboarding | ✓ | ✓ | ✓ | ✓ |

**店铺绑定 API**：`GetConnections` / `BindCallback` / `Unbind` / `refresh` 使用 `GetShopOwnerSellerId()`（Seller→UserId；Supervisor/Admin→JWT `shopId`）；Agent → 403。页面 `/shops` 对 Supervisor ✓ 且 API 真通。

**AI 设置**：`GET/PUT /api/seller/profile|config` 同样按店铺业主解析，Supervisor 可读写本店 `SellerConfig`。

## 相关 API

- `POST /api/auth/phone-login` / `POST /api/auth/agent-login`
- `GET|POST /api/seller/team`，`PATCH /api/seller/team/{id}`，`POST .../reset-password`
- `GET /api/merchant/sessions`（query：`status`、`platform`、`connectionId`、`platformShopId`、`assignment=unassigned|mine`）— Seller/Supervisor 默认全店；**Agent 默认 mine**；support 全店
- `POST /api/pay/create` — **Seller + Supervisor**（Supervisor 经 ShopId）；Agent 403
- Admin 进入：URL `?supportToken=` 静默入会话（无「系统账号进入」toast）
- `GET /api/merchant/shop-options` — 收件箱多店下拉（全角色）
- `GET /api/merchant/sessions/{id}/orders` — 会话关联订单（本地优先；空则平台回源，失败 `items=[]` + `warning`，带来源 `source`）
- `GET /api/merchant/usage` — 计费用量：`draftsToday` / `sessionsToday` / `connectedShops` / `quota` / `subscription*` + **AI token**（`totalTokens*` / `exactTokens*` / `estimatedTokens*` / `estimatedCostUsd*`，`IsEstimated` 时 chars/4 粗估）
- `GET /api/merchant/usage/daily?days=7` — 近 N 日会话/消息/AI 调用日序列（Overview ECharts 真数据；无流量空态）
- `GET|POST|PUT|DELETE /api/merchant/quick-replies` — 快捷回复 CRUD（写：Seller/Supervisor；读：同店坐席）
- 健康检查：`GET /health`、`GET /health/ready`（ready 含 DB）
- `GET /api/merchant/connections`、`POST .../bind/callback`、**匿名 GET** `.../bind/callback`（及 `/api/oauth/{platform}/callback`）→ redirect `/shops?bound=1`
- `POST .../unbind/{platform}`、`POST .../connections/{id}/refresh`
- `GET /api/merchant/connections` 返回 `expiresAt` / `expiresInHours` / `status=ok|expiring|expired|unknown`（24h 内过期=expiring）
- `GET /api/merchant/audit-logs?take=50` — 本店操作审计（Seller/Supervisor）
- `GET /api/merchant/onboarding` — 上手清单真实勾选（登录/绑店/坐席/营业时间/SLA）
- 维护模式：后端 503 `code=MAINTENANCE` 时全局提示「系统维护中」；Admin 仍可通过 admin-console 关维护
- `GET /api/merchant/alerts` — SLA 告警 + **connection_token** 类条目
- 后台：`PlatformTokenRefreshHostedService` 约每 45 分钟扫描即将过期连接并刷新
- Webhook 幂等：`ProcessedWebhookEvent`（Platform+EventKey 唯一 try-insert）

## MVP 验收清单

- [ ] **登录**：商家手机 / 坐席邮箱均可进入 merchant-web
- [ ] **绑店（Seller）**：`/shops` 列出连接；可发起 Shopee OAuth；解绑；展示 nickname / shopId / platform；Token 状态提示
- [ ] **绑店（Supervisor）**：Supervisor 登录后打开 `/shops`，`GET /api/merchant/connections` **不 401**；可绑/解（API 真通）
- [ ] **Agent 禁绑店**：Agent 进 `/shops` 见友好无权限提示；API 返回 403
- [ ] **收件箱**：会话列表 + 待发草稿审发 + 转人工 + SLA 徽章
- [ ] **多店筛选**：顶栏「全部店铺 / 各已连接平台店铺」；列表项显示平台与店铺昵称
- [ ] **订单侧栏**：选中会话后「详情」区显示订单卡片（本地/平台来源标签）或明确空态 + warning
- [ ] **Token 刷新**：连接列表有过期提示；可手动「刷新 Token」；后台 HostedService 自动扫刷新
- [ ] **AI 设置（Supervisor）**：可读写本店语气 / OutboundMode / SLA
- [ ] **OAuth GET 回调**：授权完成后落到 `/shops?bound=1` 并 toast 刷新
- [ ] **SLA 声音/通知**：收件箱可开声音蜂鸣 + 浏览器 Notification（需点一次开启）
- [ ] **计费页**：真实 usage + 订阅档位/额度；套餐说明对齐 PRICING_DRAFT

### 遗留（非本次 MVP）

- Admin-console 深化、支付网关、RegimeTrader、全自动回复

## 本机联调端口（Windows）

| 服务 | 地址 |
|------|------|
| API | `http://0.0.0.0:7092`（`launchSettings` / Kestrel，绑所有网卡） |
| merchant-web | `http://localhost:5174`，Vite 代理 `/api` → `http://127.0.0.1:7092` |

1. 复制 `merchant-web/.env.development.example` → `merchant-web/.env.development`
2. 复制 `Synerixis.Api/appsettings.Development.example.json` → `appsettings.Development.json`，改库连接串
3. VS 启动 Api（确认控制台 `listening on http://0.0.0.0:7092`）
4. `cd merchant-web && npm run dev`，浏览器只用 `http://localhost:5174`（不要写死局域网 IP）
5. 自检：`curl http://127.0.0.1:7092/health`

双网卡时：本机网页端始终用 `127.0.0.1` 代理；真机/HBuilder 再改 `frontend` 的 BASE_URL 为手机同网段的本机 IP:7092。

