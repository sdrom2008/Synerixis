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

## 路由权限

| 路由 | Seller | Supervisor | Admin | Agent |
|------|--------|------------|-------|-------|
| /inbox | ✓ | ✓ | ✓ | ✓ |
| /overview | ✓ | ✓ | ✓ | ✓（只读 KPI） |
| /shops | ✓ | ✓ | ✓ | ✗（友好提示） |
| /ai-settings | ✓ | ✓ | ✓ | ✗ |
| /billing | ✓ | ✓ | ✓ | ✗（计费 API 仍可仅 Seller） |
| /team | ✓ | ✓ | ✓ | ✗ |

**店铺绑定 API**：`GetConnections` / `BindCallback` / `Unbind` / `refresh` 使用 `GetShopOwnerSellerId()`（Seller→UserId；Supervisor/Admin→JWT `shopId`）；Agent → 403。页面 `/shops` 对 Supervisor ✓ 且 API 真通。

**AI 设置**：`GET/PUT /api/seller/profile|config` 同样按店铺业主解析，Supervisor 可读写本店 `SellerConfig`。

## 相关 API

- `POST /api/auth/phone-login` / `POST /api/auth/agent-login`
- `GET|POST /api/seller/team`，`PATCH /api/seller/team/{id}`，`POST .../reset-password`
- `GET /api/merchant/sessions`（query：`status`、`platform`、`connectionId`、`platformShopId`）、`/drafts`、`/alerts` — Seller 与同店坐席 JWT 均可（按 `shopId`）
- `GET /api/merchant/shop-options` — 收件箱多店下拉（全角色）
- `GET /api/merchant/sessions/{id}/orders` — 会话关联订单（本地优先，空列表不炸）
- `GET /api/merchant/connections`、`POST .../bind/callback`、`POST .../unbind/{platform}`、`POST .../connections/{id}/refresh`
- 后台：`PlatformTokenRefreshHostedService` 约每 45 分钟扫描即将过期连接并刷新

## MVP 验收清单

- [ ] **登录**：商家手机 / 坐席邮箱均可进入 merchant-web
- [ ] **绑店（Seller）**：`/shops` 列出连接；可发起 Shopee OAuth；解绑；展示 nickname / shopId / platform；Token 状态提示
- [ ] **绑店（Supervisor）**：Supervisor 登录后打开 `/shops`，`GET /api/merchant/connections` **不 401**；可绑/解（API 真通）
- [ ] **Agent 禁绑店**：Agent 进 `/shops` 见友好无权限提示；API 返回 403
- [ ] **收件箱**：会话列表 + 待发草稿审发 + 转人工 + SLA 徽章
- [ ] **多店筛选**：顶栏「全部店铺 / 各已连接平台店铺」；列表项显示平台与店铺昵称
- [ ] **订单侧栏**：选中会话后「详情」区显示订单卡片（单号/状态/金额/时间）或明确空态
- [ ] **Token 刷新**：连接列表有过期提示；可手动「刷新 Token」；后台 HostedService 自动扫刷新
- [ ] **AI 设置（Supervisor）**：可读写本店语气 / OutboundMode / SLA

### 遗留（非本次 MVP）

- Admin-console 深化、计费深化、Webhook 幂等存储、Push/声音告警、RegimeTrader、全自动回复
