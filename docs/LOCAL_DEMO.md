# 本地演示种子包（无需真实平台账号）

> 目标：用**测试账号与模拟数据**先把网页端主路径跑通；以后再换成真实 Shopee / TikTok Partner Key。  
> **不要**也不需要代申请真实平台店铺账号。

相关：[`ISV_APPLICATION_CHECKLIST.md`](./ISV_APPLICATION_CHECKLIST.md) · [`SHOPEE_SANDBOX_CHECKLIST.md`](./SHOPEE_SANDBOX_CHECKLIST.md) · [`MERCHANT_WEB.md`](./MERCHANT_WEB.md) · [`ADMIN_CONSOLE.md`](./ADMIN_CONSOLE.md)

---

## 演示账号（Development）

| 角色 | 登录方式 | 凭证 |
|------|----------|------|
| 商家 | merchant-web 手机登录 | 手机 `13800138000`，验证码 `123456` |
| 坐席 | merchant-web 坐席登录 | `agent@demo.synerixis.local` / `Agent123!` |
| Admin | admin-console | `admin@test.com` / `Agent123!` |

手机号在库中存 E.164：`+8613800138000`（CountryCode 默认 `86`）。

---

## 一键 Seed

```http
POST /api/dev/seed-demo
```

- **仅 Development**（生产返回 404）
- **AllowAnonymous**，幂等：重复调用跳过/刷新已存在演示数据
- 写入内容：
  - 固定演示商家 + SellerConfig（DraftFirst）
  - 模拟 `PlatformConnection`（Shopee，`SIM-SHOP-*`，Token 远期）
  - 3 条会话：待审草稿 / 转人工 handoff / 正常咨询（含 ChatMessage）
  - 2 条本地 Order（含物流单号，Inbox 侧栏可见）
  - 坐席 `agent@demo.synerixis.local` + Admin `admin@test.com`
  - 若干 `[Demo]` QuickReply
  - 若干 `AiUsageLog`（Admin usage 非空）

前端入口（Vite `import.meta.env.DEV`）：

- 登录页：「加载演示数据」
- 上手指南 / 概览：「加载演示数据」「一键注入测试消息」

---

## 模拟进线（当前登录商家）

```http
POST /api/dev/simulate-inbound
Content-Type: application/json

{
  "message": "本地测试：还有货吗？",
  "platform": "SHOPEE"
}
```

- 带商家 JWT 时自动解析 `sellerId`；匿名调试可传 `"sellerId": "<guid>"`
- 自动确保模拟店连接，走与 Webhook 相同的入库 + AI 草稿路径（无 LLM Key 时降级本地文案，不 500）

---

## 建议验收路径

1. API：`dotnet run --project Synerixis.Api`（Development）
2. `curl -X POST http://localhost:7092/api/dev/seed-demo`（端口以实际为准）
3. merchant-web：`npm run dev` → 登录页加载演示数据 → 手机登录
4. **收件箱**：看到 3 类会话；打开待审草稿可人审；侧栏有本地订单/物流单号
5. **店铺绑定**：出现「模拟 Shopee 店」
6. **团队**：出现演示坐席
7. **admin-console（平台运营台）**：
   - 打开 `http://localhost:3000`，登录页可见演示账号；或点「加载演示数据」
   - `admin@test.com` / `Agent123!` → 进入 **概览**（KPI + 近 7 日趋势非空）
   - 商家：分页/搜索、详情侧栏（连接数/会话数）、启用禁用、改订阅
   - 店铺连接 / 会话监控 / 用量 / 审计：有数据；用量与审计可导出 CSV
   - 系统设置：改 MaintenanceMode 等并保存；Development 下可见「开发工具」调 seed-demo
   - （若尚无 Admin：再调一次 `seed-demo`，或 `POST /api/auth/init-agent`）

---

## 以后换成真实 Partner Key

1. 在配置 / 环境变量填入平台 `PartnerId` / `PartnerKey` / 回调 URL（见 `.env.example`、`docs/DOCKER.md`）
2. 商家在 **店铺绑定** 走真实 OAuth（会新增真实 `PlatformConnection`，**不覆盖** `SIM-SHOP-*` 模拟店）
3. 用平台沙箱或自有测试店验证 Webhook → 草稿 → 人审 → `SendReply`
4. 上线前可停用或删除模拟连接；**切勿**把 `SIM-DEV` token 当生产凭证
5. ISV 材料与口径见 [`ISV_APPLICATION_CHECKLIST.md`](./ISV_APPLICATION_CHECKLIST.md)（人审 draft-first，禁止宣称全自动 chatbot）

本仓库**不会**也不应代申请 Shopee / TikTok 真实账号。

---

## 相关 API 速查

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/dev/seed-demo` | 一键演示种子 |
| POST | `/api/dev/simulate-inbound` | 注入一条买家消息 |
| POST | `/api/auth/phone-login` | 商家登录（开发码 123456） |
| POST | `/api/auth/agent-login` | 坐席 / Admin 登录 |
| POST | `/api/auth/init-agent` | 仅 Dev：兜底创建 Admin |
