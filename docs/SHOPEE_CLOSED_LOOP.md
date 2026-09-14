# Shopee 闭环缺口清单

链路目标：

**OAuth 绑店 → Webhook 收信 → 意图识别 → 订单查询 → 自动回复 → 转人工（handoff）**

状态以当前仓库代码为准（2026-09-14）。`✅` 已有可用实现 · `⚠️` 半成品/演示 · `❌` TODO。

## 总览

| 环节 | 状态 | 主要位置 | 备注 |
|------|------|----------|------|
| OAuth / Token | ⚠️ | `MerchantController` + `MerchantPlatformService.BindShopAsync` + `ShopeePlatformClient` | 成功回调会 **upsert** `PlatformConnection`（ShopId/Access/Refresh/Platform=SHOPEE）；`GetShopInfo` 仍偏占位；无 GET 重定向回调与 state 落库 |
| Token 读取 | ✅ | `ShopeePlatformClient.ResolveShopCredentialsAsync` | 回复 / 查单：优先按 shop 读 `PlatformConnection`，库空或异常回退 `Shopee:AccessToken`/`ShopId` |
| Token 过期字段 | ❌ | — | `expire_in` 未落库（避免无迁移改表）；`RefreshToken` 已存，自动按过期调度刷新仍 TODO |
| Webhook 入口 | ✅ | `WebhookController` `POST /api/webhook/{platform}` | 路由、签名校验、解析、会话落库、异步 `SendReplyAsync` |
| Webhook 签名 | ✅ | `VerifySignatureAsync` | HMAC；缺 `AppSecret` 直接失败 |
| Webhook 解析 | ✅ | `ParseWebhookAsync` | `type=message` 抽 CustomerId / ConversationId / Content；`OpenId=to_shop_id` |
| 多店会话归属 | ✅ | `FindOrCreateSessionAsync` | 按 `ShopId` **或** `OpenId` 匹配 `PlatformConnection` |
| 幂等 | ⚠️ | `IsDuplicateAsync` | 已存在 MsgId 才忽略；空 MsgId 仍放行；无分布式锁 |
| 意图识别 | ✅ | `IntentClassifier` ← `ProcessInboundAiReplyAsync` | classify → AgentRouter / GeneralChat；缺 AI Key 降级 |
| Intent ↔ Agent | ✅ | `OrderAgent.SupportedIntent = OrderQuery` | |
| 订单查询（DB） | ✅ | `OrderAgent` → `IOrderRepository` | |
| 订单查询（平台 API） | ✅ | `GetCustomerOrderAsync(..., platformShopId)` | DB 未命中回源；`ChatContext.PlatformShopId` 来自 webhook `to_shop_id` |
| 自动回复 | ✅ | `SendReplyAsync` | 使用 per-shop token（同上） |
| 转人工 handoff | ⚠️ | `MerchantController.TransferToAgent` | 商户手动；Webhook 无自动升级 / 无停止 AI 闸 |
| 配置 | ⚠️ | `Shopee:AppKey/AppSecret/AccessToken/ShopId/Endpoint` | 缺配置 Warning + skip；appsettings 已 gitignore |

## 分步说明

### 1. OAuth → 店铺绑定

- ✅ `GET /api/merchant/bind/{platform}` 取授权 URL  
- ✅ `POST /api/merchant/bind/callback` → `BindShopAsync`：换 token → 店铺信息 → **按 ShopId+Platform upsert** `PlatformConnection`  
- ⚠️ 前端需把平台 Redirect 带回的 `code` POST 到回调；服务端 **无匿名 GET RedirectUri 落地页**，`state` 未服务端校验  
- ❌ `TokenExpiresAt` / 定时刷新 Job  
- ❌ 多站点 partner（TW/VN/…）切换  
- ⚠️ `GetShopInfoAsync` 仍简化，真实环境可能需先 get_shop_list

### 2. Webhook → 会话

- ✅ 验签、解析、建 `ChatSession`、写 `ChatMessage`  
- ✅ 创建会话时用 `PlatformConnection.ShopId/OpenId == msg.OpenId(to_shop_id)` 找 Seller  
- ✅ AI 上下文带 `PlatformShopId = msg.OpenId`，供查单 / 回复选店  

### 3. Intent → Agent

- ✅ Webhook：`IntentClassifier` → `OrderAgent` / `GeneralChat`  
- ⚠️ `IConversationService.ProcessIncomingMessageAsync` 仍独立，Webhook 未复用  

### 4. Order → Reply

- ✅ DB → 平台 `GetCustomerOrderAsync`（可带 `platformShopId`）  
- ✅ 凭证：`PlatformConnection` → 回退 appsettings（单店演示）  
- ✅ 库 / 配置皆空：Warning，不抛，Webhook 仍 200  

### 5. Handoff

- ✅ 商户 API 转人工  
- ❌ 低置信度 / 敏感词自动 handoff  
- ❌ 转人工后停止 AI `SendReplyAsync` 硬闸  

## 建议验收用例（人工 · Win11 + VS2022）

1. **绑店写库**：登录商户 JWT → `GET /api/merchant/bind/SHOPEE` → 浏览器授权 → 前端把 `code` `POST /api/merchant/bind/callback` `{ "platform":"SHOPEE","code":"..." }` → DB `platform_connections` 有 AccessToken/ShopId；再授权一次应 **更新同行** 而非插入重复。  
2. **per-shop 回复**：清空或注释 appsettings 里 `Shopee:AccessToken`，仅保留 DB 行；推一条真实/签名 webhook（`to_shop_id`=该 ShopId）→ 日志出现 credentials from `PlatformConnection`，尝试 `send_message`。  
3. **config 回退**：删掉/空库连接，只配 `Shopee:AccessToken`+`ShopId` → 同上链路仍可发（演示单店）。  
4. **查单**：买家问订单；`PlatformShopId` 传入后按该店 token 调 order API；无 token 仅 Warning +「暂无订单」友好话术。  
5. 商户转人工后会话 Pending。

本地跑 API（VS2022）：

1. 打开 `Synerixis.sln`，设 `Synerixis.Api` 为启动项目。  
2. 用户机密 / 本地 `appsettings.Development.json`（已 gitignore）填 `Database:ConnectionString` 与 `Shopee:*`（勿提交）。  
3. F5；Swagger 或前端走绑店与 webhook。  
4. 确认 MySQL 表 `platform_connections` 可写；开发环境 `EnsureCreated` 不会改已有表结构。

---

维护：改闭环任一环时同步更新本表。
