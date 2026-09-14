# Shopee 闭环缺口清单

链路目标：

**OAuth 绑店 → Webhook 收信 → 意图识别 → 订单查询 → 自动回复 → 转人工（handoff）**

状态以当前仓库代码为准（2026-09-14）。`✅` 已有可用实现 · `⚠️` 半成品/演示 · `❌` TODO。

## 总览

| 环节 | 状态 | 主要位置 | 备注 |
|------|------|----------|------|
| OAuth / Token | ⚠️ | `ShopeePlatformClient.GetAuthorizationUrlAsync` / `GetAccessTokenAsync` / `GetShopInfoAsync` | 有方法；授权回调 Controller 与 token 持久化到 `PlatformConnection` 未打通；`GetShopInfo` 仍偏占位 |
| Webhook 入口 | ✅ | `WebhookController` `POST /api/webhook/{platform}` | 路由、签名校验、解析、会话落库、异步 `SendReplyAsync` |
| Webhook 签名 | ✅ | `VerifySignatureAsync` | HMAC；缺 `AppSecret` 直接失败 |
| Webhook 解析 | ✅ | `ParseWebhookAsync` | `type=message` 抽 CustomerId / ConversationId / Content |
| 幂等 | ⚠️ | `IsDuplicateAsync` | 有 MsgId 去重意图，但条件判断疑似反了（`!IsDuplicate` 当重复忽略），需修 |
| 意图识别 | ⚠️ | `IntentClassifier` + `ConversationService` | 服务存在；**Webhook 路径未调用**，现用 `GenerateAiReply` 占位话术 |
| Intent ↔ Agent | ✅ | `OrderAgent.SupportedIntent = OrderQuery` | 与 Classifier 统一为 `OrderQuery`（`QueryOrder` 同值别名保留） |
| 订单查询（DB） | ✅ | `OrderAgent` → `IOrderRepository` | B2C 按 shop+customer 查库 |
| 订单查询（平台 API） | ✅ | `OrderAgent` → `IPlatformClientRouter` → `GetCustomerOrderAsync` | DB 未命中时回源；平台 null / DI 缺失时保持友好无单话术 |
| 自动回复 | ✅ | `SendReplyAsync` → `/api/v2/sellerchat/send_message` | 与订单查询同套 HMAC（partnerId+path+timestamp+accessToken+shopId） |
| 转人工 handoff | ⚠️ | `MerchantController.TransferToAgent` + `ChatSession.TransferToAgent` | 商户手动转；Webhook/AI 侧无自动升级策略 |
| 配置 | ⚠️ | `Shopee:AppKey/AppSecret/AccessToken/ShopId/Endpoint` | 缺配置打 Warning 并 skip，不抛 |

## 分步说明

### 1. OAuth → 店铺绑定

- ✅ 客户端侧拼授权 URL、换 token 的雏形  
- ❌ 完整「卖家点击授权 → 回调写库 → 刷新 token」产品流  
- ❌ 多店铺 / 多站点 partner 切换

### 2. Webhook → 会话

- ✅ 验签、解析、建 `ChatSession`、写 `ChatMessage`  
- ⚠️ Shopee 事件类型未像 TikTok 那样枚举分流；非 chat 推送走 default「未处理」  
- ⚠️ 测试接口 `POST /api/webhook/shopee/test` 仅本地模拟，不验签

### 3. Intent → Agent

- ⚠️ 生产 Webhook 未走 `IConversationService.ProcessIncomingMessageAsync`  
- ✅ `OrderQuery` / `QueryOrder` 已统一（权威名 `OrderQuery`，别名同值）

### 4. Order → Reply

- ✅ DB 路径：`OrderAgent`  
- ✅ 平台路径：`GetCustomerOrderAsync`（order_sn 含字母 → detail；否则近期 list + detail 匹配 `buyer_user_id`；返回 `order_sn/status/tracking` 摘要）  
- ✅ **接线完成**：`IOrderRepository` 无数据时，OrderAgent 经 `IPlatformClientRouter` fallback 调用 `GetCustomerOrderAsync`，格式化为买家话术；平台 null / 未注入路由则保留「暂无订单」友好回复

### 5. Handoff

- ✅ 商户 API 可标记转人工  
- ❌ 置信度低 / 敏感词 / 买家怒气自动 handoff  
- ❌ 转人工后停止 AI 自动 `SendReplyAsync` 的硬闸（需在 Webhook 异步回复前读 session 状态）

## 建议验收用例（人工）

1. 配置齐 `Shopee:*`，用真实/沙箱 order_sn 调 `GetCustomerOrderAsync`，得到非 null 摘要。  
2. Webhook 推一条 message，DB 有会话与用户消息，且尝试 `SendReplyAsync`（无 token 时应仅 Warning）。  
3. 商户调用转人工后，会话状态为 Pending/待接入。  
4. ✅ 库空时平台回源查单话术：有摘要 → 买家可读回复；平台 null → 「还没有订单记录」。

---

维护：改闭环任一环时同步更新本表。
