# Shopee 闭环缺口清单

链路目标：

**OAuth 绑店 → Webhook 收信 → 意图识别 → 订单查询 → AI 草稿（默认）→ 人审 SendReply → 转人工（handoff）**

状态以当前仓库代码为准（2026-09-14）。`✅` 已有可用实现 · `⚠️` 半成品/演示 · `❌` TODO。

## 总览

| 环节 | 状态 | 主要位置 | 备注 |
|------|------|----------|------|
| OAuth / Token | ✅ | `MerchantController` + `MerchantPlatformService.BindShopAsync` + `ShopeePlatformClient` | 成功回调会 **upsert** `PlatformConnection`；**匿名 GET** `/api/merchant/bind/callback`（及 `/api/oauth/{platform}/callback`）收 code/state 完成绑店并 redirect `merchant-web/shops?bound=1`；state 进程内短存；`GetShopInfo` 仍偏占位 |
| Token 读取 | ✅ | `ShopeePlatformClient.ResolveShopCredentialsAsync` | 回复 / 查单：优先按 shop 读 `PlatformConnection`，库空或异常回退 `Shopee:AccessToken`/`ShopId` |
| Token 过期字段 | ✅ | `PlatformConnection.TokenExpiresAt` + `PlatformTokenRefreshHostedService` | SchemaPatcher 补列；绑店写过期时间；HostedService 约每 45 分钟扫即将过期并刷新 |
| Webhook 入口 | ✅ | `WebhookController` `POST /api/webhook/{platform}` | 路由、签名校验、解析、会话落库、异步 AI；**默认落草稿** |
| Webhook 签名 | ✅ | `VerifySignatureAsync` | HMAC；缺 `AppSecret` 直接失败 |
| Webhook 解析 | ✅ | `ParseWebhookAsync` | `type=message` 抽 CustomerId / ConversationId / Content；`OpenId=to_shop_id` |
| 多店会话归属 | ✅ | `FindOrCreateSessionAsync` | 按 `ShopId` **或** `OpenId` 匹配 `PlatformConnection` |
| 幂等 | ✅ | `ProcessedWebhookEvent` + `TryClaimWebhookEventAsync` | Platform+EventKey 唯一 try-insert；EventKey=MsgId，空则弱键 hash 并 Warning；冲突返回 duplicate，成功后再落消息 |
| 意图识别 | ✅ | `IntentClassifier` ← `ProcessInboundAiReplyAsync` | 规则优先 Logistics/Competitor/Order + LLM 枚举对齐；低置信仍 auto-handoff |
| Intent ↔ Agent | ✅ | Order/Logistics/Competitor/Product → 对应 Agent | `AgentRouter` 按 `SupportedIntent` 映射 |
| 订单查询（DB） | ✅ | `OrderAgent` → `IOrderRepository` | |
| 订单查询（平台 API） | ✅ | `GetCustomerOrderAsync(..., platformShopId)` | DB 未命中回源；`ChatContext.PlatformShopId` 来自 webhook `to_shop_id` |
| 物流查询 | ✅ | `LogisticsAgent` + `ShopeePlatformClient.GetTrackingInfoAsync` | 有 order_sn+tracking 调 `get_tracking_info`；失败/无权限诚实降级（运单+订单状态，**不编造 checkpoint**）；侧栏 `GET .../orders` 附 `logistics` |
| AI 草稿（默认） | ✅ | `DraftMessage` + `OutboundMode=DraftFirst` | 默认不 `SendReply`；`AutoSend` 显式开启才出站 |
| 人审出站 | ✅ | `POST .../draft/approve` 等 | 调用 `SendReplyAsync`（per-shop token） |
| 转人工 handoff | ✅ | `ChatSession.PendingHumanHandoff` + `TransferToAgent` | 硬闸 + **敏感词/低置信自动 handoff**；营业外不 AutoSend（可 handoff） |
| 配置 | ⚠️ | `Shopee:AppKey/AppSecret/AccessToken/ShopId/Endpoint` | 缺配置 Warning + skip；appsettings 已 gitignore |

## 分步说明

### 1. OAuth → 店铺绑定

- ✅ `GET /api/merchant/bind/{platform}` 取授权 URL（state 写入进程短存）  
- ✅ `POST /api/merchant/bind/callback` → `BindShopAsync`：换 token → 店铺信息 → **按 ShopId+Platform upsert** `PlatformConnection`  
- ✅ **匿名 GET** `/api/merchant/bind/callback` / `/api/oauth/{platform}/callback`：收 code/state → BindShop → redirect `Frontends:MerchantWebBaseUrl/shops?bound=1`  
- ✅ `TokenExpiresAt` + `PlatformTokenRefreshHostedService`  
- ❌ 多站点 partner（TW/VN/…）切换  
- ⚠️ `GetShopInfoAsync` 仍简化，真实环境可能需先 get_shop_list

### 2. Webhook → 会话

- ✅ 验签、解析、建 `ChatSession`、写 `ChatMessage`
- ✅ 幂等表 `processed_webhook_events`（SchemaPatcher Ensure）  
- ✅ 创建会话时用 `PlatformConnection.ShopId/OpenId == msg.OpenId(to_shop_id)` 找 Seller  
- ✅ AI 上下文带 `PlatformShopId = msg.OpenId`，供查单 / 回复选店  

### 3. Intent → Agent

- ✅ Webhook：`IntentClassifier`（规则优先 LogisticsQuery / CompetitorAnalysis / OrderQuery + LLM）→ `AgentRouter` → Logistics / Competitor / Order / GeneralChat  
- ✅ `LogisticsAgent`：解析运单号 + 订单状态，禁止固定模拟运单号  
- ✅ `GET /api/merchant|admin/usage` 增加 `byPurpose` 分桶；商户计费 / Admin 用量表格展示  
- ✅ `IInboundSessionService`（FindOrCreate + AppendBuyerMessage）Webhook 与 ConversationService 共用；`ProcessIncomingMessageAsync` 已 Obsolete 并转发入库；**AI 草稿/handoff/维护/营业外仍仅 Webhook ProcessInboundAiReply**  

**本轮摘要（2026-09-14）**：Shopee `get_tracking_info` 真实轨迹 + 侧栏 logistics；`IInboundSessionService` 收拢 Webhook/Conversation 入库。  
**下一轮缺口（自动继续）**：TikTok 真实轨迹；Token 过期告警 UI；支付生产网关；APNs；多站点 partner；多实例限流（当前 Webhook 限流为**单机内存**，非 Redis）；幂等审计完善。  

### 4. Order → Reply

- ✅ DB → 平台 `GetCustomerOrderAsync`（可带 `platformShopId`）  
- ✅ 凭证：`PlatformConnection` → 回退 appsettings（单店演示）  
- ✅ 库 / 配置皆空：Warning，不抛，Webhook 仍 200  

### 5. Handoff

- ✅ 商户 API 转人工（`PendingHumanHandoff=true`，与新建会话的 `Status=Pending` 解耦）  
- ✅ 低置信度 / 敏感词自动 handoff（`AutoHandoffOnLowConfidence` / `SensitiveKeywords`）  
- ✅ 营业时间外：不 AutoSend；`HandoffOutsideBusinessHours` 默认转人工 + 系统提示草稿  
- ✅ **硬闸**：转人工后 Webhook **不再生成新 AI 草稿**，并跳过 AutoSend；旧草稿保留（可标 Superseded）仍可人审发送  
- ✅ SLA 唤醒：`needsResponseBy` / `hoursSinceLastBuyerMsg` / `GET /api/merchant/alerts`；收件箱排序按超时升序  

## 建议验收用例（人工 · Win11 + VS2022）

1. **绑店写库**：登录商户 JWT → `GET /api/merchant/bind/SHOPEE` → 浏览器授权 → 平台回调 **GET** `/api/merchant/bind/callback?code=&state=` → 自动 BindShop 并跳转 `/shops?bound=1`；亦可手 POST code。DB `platform_connections` 有 AccessToken/ShopId；再授权一次应 **更新同行** 而非插入重复。  
1b. **Webhook 幂等**：同一 MsgId 第二次 webhook 返回 `duplicate` 且不双写消息。  
2. **草稿优先**：推一条 webhook → 日志 `Draft saved ... (no SendReply)`；商户 `GET /api/merchant/sessions/{id}/draft` → `POST .../draft/approve` 才 `send_message`。
2b. **per-shop 出站**：仅 AutoSend 或人审 approve 时走 `SendReplyAsync`；优先 `PlatformConnection` token。  
3. **config 回退**：删掉/空库连接，只配 `Shopee:AccessToken`+`ShopId` → 同上链路仍可发（演示单店）。  
4. **查单**：买家问订单；`PlatformShopId` 传入后按该店 token 调 order API；无 token 仅 Warning +「暂无订单」友好话术。  
5. 商户转人工后 `PendingHumanHandoff=true`：再推 webhook 日志出现 `Handoff hard-gate`，无新草稿；旧草稿仍可 approve。

本地跑 API（VS2022）：

1. 打开 `Synerixis.sln`，设 `Synerixis.Api` 为启动项目。  
2. 用户机密 / 本地 `appsettings.Development.json`（已 gitignore）填 `Database:ConnectionString` 与 `Shopee:*`（勿提交）。  
3. F5；Swagger 或前端走绑店与 webhook。  
4. 确认 MySQL 表 `platform_connections` 可写；开发环境 `EnsureCreated` 不会改已有表结构。

---

维护：改闭环任一环时同步更新本表。
