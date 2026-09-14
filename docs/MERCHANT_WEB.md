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
4. 普通 Agent 只能进收件箱（及只读概览）；Supervisor 可管团队

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
| /shops | ✓ | ✓ | ✓ | ✗ |
| /ai-settings | ✓ | ✓ | ✓ | ✗ |
| /billing | ✓ | ✓ | ✓ | ✗ |
| /team | ✓ | ✓ | ✓ | ✗ |

## 相关 API

- `POST /api/auth/phone-login` / `POST /api/auth/agent-login`
- `GET|POST /api/seller/team`，`PATCH /api/seller/team/{id}`，`POST .../reset-password`
- `GET /api/merchant/sessions`、`/drafts`、`/alerts` — Seller 与同店坐席 JWT 均可（按 `shopId`）
