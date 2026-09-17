# 5 分钟能跑通（演示清单）

> **人审工作台**，不是全自动 chatbot。无需真实 Shopee Partner / 无需 LLM Key。  
> 闭环：登录 → 收件箱 → 待审草稿 → **人审发送** → 时间线出现「演示·模拟出站」。

细节 / 排障 → [`LOCAL_DEMO.md`](./LOCAL_DEMO.md) · [`DEV_SETUP_BOX.md`](./DEV_SETUP_BOX.md) · [`LOCAL_DB_RESET.md`](./LOCAL_DB_RESET.md)

---

## ✅ 前置（已有本仓库）

| 项 | 要求 |
|----|------|
| 运行时 | .NET 8 SDK、Node 18+ |
| 数据库 | MySQL `synerixis` / `synerixis_dev`（无 MySQL 时 API **自动降级 SQLite**） |
| 配置 | 首次：`cp -n Synerixis.Api/appsettings.Development.example.json Synerixis.Api/appsettings.Development.json` |

| 服务 | 地址 |
|------|------|
| API | `http://127.0.0.1:7092` |
| merchant-web | `http://localhost:5174` |
| admin-console（可选） | `http://localhost:3000` |

| 角色 | 凭证 |
|------|------|
| **商家（主路径）** | `13800138000` / 验证码 `123456` |
| 坐席 | `agent@demo.synerixis.local` / `Agent123!` |
| Admin | `admin@test.com` / `Agent123!` |

---

## ✅ 1. 启动 API + Web（约 1–2 分钟）

```bash
# 终端 A — API
cd Synerixis
export PATH="$HOME/.dotnet:$PATH"
cd Synerixis.Api && ASPNETCORE_ENVIRONMENT=Development dotnet run
# 期望：Listening on http://0.0.0.0:7092
```

```bash
# 终端 B — merchant-web
cd Synerixis/merchant-web && npm install && npm run dev
# 期望：http://localhost:5174  （/api → 127.0.0.1:7092）
```

可选 Admin：`cd Synerixis/admin-console && npm install && npm run dev` → `http://localhost:3000`

---

## ✅ 2. Seed（约 10 秒）

任选其一：

```bash
curl -sS -X POST http://127.0.0.1:7092/api/dev/seed-demo | head
```

或打开 `http://localhost:5174` → 登录页点 **「加载演示数据」**。

冒烟（可选）：`BASE_URL=http://127.0.0.1:7092 ./scripts/smoke.sh`

---

## ✅ 3. 登录 → 人审发送（约 2 分钟）

1. 打开 **http://localhost:5174**
2. 点 **「填入商家号」** → 登录（`13800138000` / `123456`）
3. 进入 **收件箱**
4. 筛选 **「待发草稿」** → 打开 **演示买家·待审草稿**
5. 草稿面板：可改文案 → **保存草稿**（可选）→ **人审发送**
6. 期望：Toast「已模拟发送…」+ 成功条；时间线坐席气泡带 **演示·模拟出站**

无草稿也可：**手动起草** → 保存或直接 **人审发送**（仍先落 Pending 草稿再出站，draft-first）。

---

## ✅ 4. 可选（仍在 5 分钟内）

| 动作 | 怎么做 |
|------|--------|
| 注入测试 | 收件箱左上角 **「注入测试」**（无 LLM Key → 规则草稿，不 500） |
| 看 AI 状态 | **AI 设置** → 可见「未配置 AI / 规则草稿」；可填本店 DashScope Key |
| Admin | `http://localhost:3000` → `admin@test.com` / `Agent123!` |

**出站**：seed 强制 `DraftFirst`；AutoSend 默认关。「人审发送」≠ chatbot。  
**营业时间**：演示店 `00:00–23:59` 且关「营业外转人工」，避免晚间被闸打断。

---

## LLM Key（演示不强制）

| 来源 | 怎么配 |
|------|--------|
| 商家 UI | **AI 设置** → LLM API Key → `SellerConfig.LlmApiKey` |
| 平台配置 | `Llm:ApiKey` / `Tongyi:Qianwen:ApiKey` / `DashScope:ApiKey` |
| 环境变量 | `LLM_API_KEY` / `TONGYI_API_KEY` / `DASHSCOPE_API_KEY` |

**无 Key**：入站/inject 生成 `【未配置 AI·规则草稿】` Pending 草稿；Warning 日志；**不 500**。seed 预置草稿 + 人审 + SIM mock **不依赖** Key。

---

## API 速查

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/dev/seed-demo` | 演示种子（仅 Development） |
| POST | `/api/dev/simulate-inbound` | 注入买家消息 → AI/规则草稿 |
| POST | `/api/auth/phone-login` | 商家登录 |
| POST | `/api/auth/agent-login` | 坐席 / Admin |
| GET | `/api/seller/profile` | 含 `Llm.configured`（Key 已掩码） |
| PUT | `/api/seller/config` | 可写 `LlmApiKey`（空串清除） |
| PUT | `/api/merchant/sessions/{id}/draft` | 保存草稿；无草稿时创建 Pending |
| POST | `/api/merchant/sessions/{id}/draft/edit-send` | 编辑（或新建）后发送 |
| POST | `/api/merchant/sessions/{id}/draft/approve` | 人审发送（SIM 店 mock） |

---

## 换成真实 Shopee / 真实 LLM

1. 填 Partner Key → 绑真实 OAuth（勿覆盖 `SIM-SHOP-*`）
2. AI 设置或 `Llm:ApiKey` 填 DashScope Key → 入站走真实起草
3. **不要**把 `SIM-DEV` token 当生产凭证

详见 [`SHOPEE_SANDBOX_CHECKLIST.md`](./SHOPEE_SANDBOX_CHECKLIST.md) · [`LOCAL_DEMO.md`](./LOCAL_DEMO.md)
