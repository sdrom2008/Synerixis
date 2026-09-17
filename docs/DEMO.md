# 明日演示：账号路径与人审闭环（唯一入口）

> 目标：无需真实 Shopee Partner / 无需真实 LLM Key，用 **seed + SIM mock 出站** 走通  
> **登录 → 收件箱 → 待审草稿 → 人审发送 → 时间线出现模拟出站**。  
> 定位是人审工作台，**不是**全自动 chatbot / 群发。

相关细节：[`LOCAL_DEMO.md`](./LOCAL_DEMO.md) · [`MERCHANT_WEB.md`](./MERCHANT_WEB.md) · [`DEV_SETUP_BOX.md`](./DEV_SETUP_BOX.md) · [`LOCAL_DB_RESET.md`](./LOCAL_DB_RESET.md) · [`PRODUCT_STATUS.md`](./PRODUCT_STATUS.md)

---

## 端口与入口

| 服务 | 地址 | 说明 |
|------|------|------|
| API | `http://127.0.0.1:7092` | Development；绑定 `0.0.0.0:7092` |
| merchant-web | `http://localhost:5174` | **主演示面**；`/api` → `127.0.0.1:7092` |
| admin-console | `http://localhost:3000` | 可选；同代理到 API |

---

## 演示账号

| 角色 | 入口 | 凭证 |
|------|------|------|
| **商家（主路径）** | merchant-web 手机登录 | `13800138000` / 验证码 `123456` |
| 坐席 | merchant-web 坐席登录 | `agent@demo.synerixis.local` / `Agent123!` |
| Admin | admin-console | `admin@test.com` / `Agent123!` |

---

## 启动（本机 / Box）

### A. API

```bash
cd Synerixis
cp -n Synerixis.Api/appsettings.Development.example.json Synerixis.Api/appsettings.Development.json
# MySQL：synerixis / synerixis_dev（见 DEV_SETUP_BOX）；无 MySQL → 自动降级 SQLite

export PATH="$HOME/.dotnet:$PATH"
cd Synerixis.Api && ASPNETCORE_ENVIRONMENT=Development dotnet run
```

### B. 商家工作台

```bash
cd merchant-web && npm install && npm run dev
# http://localhost:5174
```

### C. （可选）Admin

```bash
cd admin-console && npm install && npm run dev
# http://localhost:3000
```

### D. Seed + 冒烟

```bash
curl -sS -X POST http://127.0.0.1:7092/api/dev/seed-demo | head
BASE_URL=http://127.0.0.1:7092 ./scripts/smoke.sh
```

库表漂移：见 [`LOCAL_DB_RESET.md`](./LOCAL_DB_RESET.md)。

---

## 点击路径（明天照做）

1. 打开 **http://localhost:5174**
2. 登录页点 **「加载演示数据」**（或先 `POST /api/dev/seed-demo`）
3. 点 **「填入商家号」** → 登录（`13800138000` / `123456`）
4. 进入 **收件箱**：
   - 可见多条会话（待审草稿 / 转人工 / 已超时 / 即将超时 / 正常咨询 / TikTok 店）
   - 顶部 **店铺筛选**：下拉里每店显示 **草稿 / 超时** 角标
   - 筛选「待发草稿」→ 打开 **演示买家·待审草稿**
   - 筛选「超时告警」→ **已超时**（红）与 **即将超时**（橙）
   - 筛选「待人工」→ **转人工**（旧草稿仍可人审发送）
   - 左上角 **「注入测试」**：`simulate-inbound`（无 LLM Key 时生成规则草稿，不 500）
5. 草稿面板（**回复草稿**）：
   - 有草稿：改文案 → **保存草稿** → **人审发送**
   - **无草稿也可手动起草**：撰写 → 保存或直接 **人审发送**（仍先落 Pending 草稿再出站）
   - 演示店 **模拟出站**（`mocked: true`）；Toast + 成功条；时间线坐席气泡带 **演示·模拟出站**
6. （可选）**AI 设置**：查看 LLM 状态；可填本店 DashScope Key；空 Key 不影响演示
7. （可选）Admin：`admin@test.com` / `Agent123!` 看概览 / 商家 / 用量 / seed

**出站模式**：seed 强制 `OutboundMode = DraftFirst`；AutoSend 默认关闭。坐席「人审发送」≠ chatbot。  
**营业时间**：seed 演示店为 `00:00–23:59` 且关闭「营业外转人工」，避免晚间演示被营业外闸打断。

---

## LLM Key（可选，演示不强制）

| 来源 | 怎么配 |
|------|--------|
| 商家 UI | merchant-web → **AI 设置** → LLM API Key（写入 `SellerConfig.LlmApiKey`） |
| 平台配置 | `Llm:ApiKey` / `Tongyi:Qianwen:ApiKey` / `DashScope:ApiKey` |
| 环境变量 | `LLM_API_KEY` / `TONGYI_API_KEY` / `DASHSCOPE_API_KEY` |

**无 Key 行为**：入站/inject 生成带前缀 `【未配置 AI·规则草稿】` 的 Pending 草稿；打 Warning 日志；**不 500**。seed 预置草稿与人审发送 / SIM mock **不依赖** Key。

---

## API 速查

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/dev/seed-demo` | 演示种子（仅 Development） |
| POST | `/api/dev/simulate-inbound` | 注入买家消息 → AI/规则草稿 |
| POST | `/api/auth/phone-login` | 商家登录 |
| POST | `/api/auth/agent-login` | 坐席 / Admin |
| POST | `/api/auth/init-agent` | Dev：确保 Admin JWT |
| GET | `/api/seller/profile` | 含 `Llm.configured` 状态（Key 已掩码） |
| PUT | `/api/seller/config` | 可写 `LlmApiKey`（空串清除） |
| PUT | `/api/merchant/sessions/{id}/draft` | 保存草稿；无草稿时创建 Pending |
| POST | `/api/merchant/sessions/{id}/draft/edit-send` | 编辑（或新建）后发送 |
| POST | `/api/merchant/sessions/{id}/draft/approve` | 人审发送（SIM 店 mock） |

---

## 换成真实 Shopee / 真实 LLM 时

1. 填 Partner Key → 绑真实 OAuth（勿覆盖 `SIM-SHOP-*`）  
2. 在 AI 设置或 `Llm:ApiKey` 填 DashScope Key → 入站走真实起草  
3. **不要**把 `SIM-DEV` token 当生产凭证  

详见 `SHOPEE_SANDBOX_CHECKLIST.md`。
