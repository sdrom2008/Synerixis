# 明日演示：1 店 draft-first 人审闭环

> 目标：无需真实 Shopee Partner Key，用 **seed + mock 出站** 走通  
> **登录 → 收件箱 → 打开待审草稿 → 人审发送**。  
> 定位是人审工作台，**不是**全自动 chatbot / 群发。

相关：[`LOCAL_DEMO.md`](./LOCAL_DEMO.md) · [`MERCHANT_WEB.md`](./MERCHANT_WEB.md) · [`DEV_SETUP_BOX.md`](./DEV_SETUP_BOX.md) · [`LOCAL_DB_RESET.md`](./LOCAL_DB_RESET.md)

---

## 演示账号

| 角色 | 入口 | 凭证 |
|------|------|------|
| **商家（主路径）** | merchant-web 手机登录 | `13800138000` / 验证码 `123456` |
| 坐席 | merchant-web 坐席登录 | `agent@demo.synerixis.local` / `Agent123!` |
| Admin | admin-console | `admin@test.com` / `Agent123!` |

---

## 一键启动（本机 / Box）

### A. API（Development，默认 `http://127.0.0.1:7092`）

```bash
cd Synerixis
cp -n Synerixis.Api/appsettings.Development.example.json Synerixis.Api/appsettings.Development.json
# 需本机 MySQL：库 synerixis / 用户 synerixis / 密码 synerixis_dev（见 DEV_SETUP_BOX）
# 无 MySQL 时可不配连接串 → 自动降级 SQLite dev.db

export PATH="$HOME/.dotnet:$PATH"   # Box 若用官方安装脚本
cd Synerixis.Api && ASPNETCORE_ENVIRONMENT=Development dotnet run
```

### B. 商家工作台（主演示面）

```bash
cd merchant-web && npm install && npm run dev
# http://localhost:5174  （/api → 127.0.0.1:7092）
```

### C. （可选）Admin

```bash
cd admin-console && npm install && npm run dev
# http://localhost:3000  （/api → 127.0.0.1:7092）
```

### D. Seed + 冒烟

```bash
curl -sS -X POST http://127.0.0.1:7092/api/dev/seed-demo | head
BASE_URL=http://127.0.0.1:7092 ./scripts/smoke.sh
```

若库表漂移（`orders.CustomerId` 类型错、`Unknown column ConversationId` 等）：见 [`LOCAL_DB_RESET.md`](./LOCAL_DB_RESET.md)（`Migrations/FullReset_nexusai_db.sql`）。  
模型已忽略 `Conversation.Messages` 影子 FK；旧二进制可跑 `Migrations/Fix_ConversationId_after_fullreset.sql`。

---

## 点击路径（明天照做）

1. 打开 **http://localhost:5174**
2. 登录页点 **「加载演示数据」**（或先 `POST /api/dev/seed-demo`）
3. 点 **「填入商家号」** → 登录（`13800138000` / `123456`）
4. 进入 **收件箱**：
   - 可见多条会话（待审草稿 / 转人工 / 已超时 / 正常咨询 / TikTok 店）
   - 顶部 **店铺筛选**：至少「模拟 Shopee 店」与「模拟 TikTok 店」
   - 筛选「待发草稿」→ 打开 **演示买家·待审草稿**
   - 筛选「超时告警」→ **演示买家·已超时**（红标 SLA）
   - 筛选「待人工」→ **演示买家·转人工**（旧草稿仍可审发）
5. 右侧草稿面板：可改文案 → **保存** → **审核发送**
   - 演示店会 **模拟出站**（响应 `mocked: true`，不调真实 Shopee）
   - Toast + 成功条提示「演示闭环」；时间线坐席气泡带 **演示·模拟出站**；草稿变为已发送
6. （可选）上手指南 / 概览再点「一键注入测试消息」走 Webhook 同路径起草
7. （可选）Admin：`admin@test.com` / `Agent123!` 看概览 / 商家 / 用量

**出站模式**：seed 强制 `SellerConfig.OutboundMode = DraftFirst`；AutoSend 仅显式开启，默认关闭。

---

## API 速查

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/dev/seed-demo` | 演示种子（仅 Development） |
| POST | `/api/dev/simulate-inbound` | 注入买家消息 → AI 草稿 |
| POST | `/api/auth/phone-login` | 商家登录 |
| POST | `/api/auth/agent-login` | 坐席 / Admin |
| POST | `/api/auth/init-agent` | Dev：确保 **Admin** JWT（勿再返回普通坐席） |
| POST | `/api/merchant/sessions/{id}/draft/approve` | 人审发送（SIM 店 mock） |

---

## 换成真实 Shopee 时

填 Partner Key → 店铺绑定真实 OAuth（不覆盖 `SIM-SHOP-*`）→ Webhook 实机。  
**不要**把 `SIM-DEV` token 当生产凭证。详见 `SHOPEE_SANDBOX_CHECKLIST.md`。
