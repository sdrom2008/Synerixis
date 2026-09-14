# Shopee Partner 沙箱联调清单（可执行）

> 目的：按步骤在 **Shopee Open Platform 沙箱 / 测试店** 走通绑店 → Webhook → 草稿人审 → handoff。  
> **不要求本机已真调通**；缺 Partner 凭证时把本清单当验收剧本。  
> 配套：`docs/SHOPEE_CLOSED_LOOP.md`、`docs/ISV_APPLICATION_CHECKLIST.md`。

## 0. 前置

| # | 步骤 | 预期 | 备注 |
|---|------|------|------|
| 0.1 | 申请/登录 Shopee Open Platform，创建测试 App | 有 PartnerId / PartnerKey | 生产密钥勿提交仓库 |
| 0.2 | 配置 `Shopee:AppKey` / `AppSecret` / `Endpoint`（沙箱） | API 启动无缺配置 Warning | appsettings 已 gitignore |
| 0.3 | 配置 `Frontends:MerchantWebBaseUrl` | OAuth 回调可跳转 `/shops?bound=1` | |
| 0.4 | MySQL 可用；跑 `Migrations/*.sql` 或依赖 SchemaPatcher | 表齐全 | 含 `draft_messages` / `processed_webhook_events` / `system_settings` |
| 0.5 | 启动 API `:5000` + merchant-web | `/health` `/health/ready` 绿 | |

## 1. OAuth 绑店

| # | 步骤 | 预期 |
|---|------|------|
| 1.1 | 商家登录 merchant-web → 店铺绑定 | 看到 Shopee 入口 |
| 1.2 | `GET /api/merchant/bind/SHOPEE` | 返回授权 URL（含 state） |
| 1.3 | 浏览器完成沙箱授权 | 回调 `GET /api/merchant/bind/callback?code=&state=` |
| 1.4 | 检查 DB `platform_connections` | 有 AccessToken / ShopId / TokenExpiresAt；再授权应 upsert 非重复行 |
| 1.5 | UI 显示已绑定 | Token 过期告警路径可后验 |

## 2. Webhook 入站

| # | 步骤 | 预期 |
|---|------|------|
| 2.1 | 在 Partner 控制台配置 Webhook URL → `POST /api/webhook/SHOPEE` | 验签通过 |
| 2.2 | 推送一条 `type=message` 测试事件 | 落 `ChatSession` + `ChatMessage`；`PlatformShopOpenId` 对齐 `to_shop_id` |
| 2.3 | 同一 MsgId 再推一次 | 返回 duplicate；消息不双写 |
| 2.4 | 缺 AppSecret / 坏签名 | 失败且不落业务消息 |

## 3. AI 草稿（DraftFirst）

| # | 步骤 | 预期 |
|---|------|------|
| 3.1 | 默认 `OutboundMode=DraftFirst` | 日志 `Draft saved ... (no SendReply)` |
| 3.2 | merchant-web 收件箱打开会话 | 可见待发草稿；未自动出站 |
| 3.3 | 审发 `POST .../draft/approve` | 调 `SendReplyAsync`；优先用本店 PlatformConnection token |
| 3.4 | 显式改 AutoSend（仅沙箱试） | 才自动 SendReply；生产默认勿开 |

## 4. 订单 / 物流上下文

| # | 步骤 | 预期 |
|---|------|------|
| 4.1 | 买家问订单；本地 Orders 有数据 | Inbox 侧栏展示本地单 |
| 4.2 | 本地空 → 平台 `GetCustomerOrderAsync` | 有摘要或 `warning`；不 500 |
| 4.3 | 物流意图 | 解析运单号；无承运商 API 时不造假轨迹 |

## 5. Handoff + 坐席分配

| # | 步骤 | 预期 |
|---|------|------|
| 5.1 | 收件箱「转人工客服」 | `PendingHumanHandoff=true`；停新 AI 草稿 |
| 5.2 | 敏感词 / 低置信自动 handoff | 同闸；可查审计/日志 |
| 5.3 | `POST /api/merchant/sessions/{id}/assign` `{ agentId }` | 同店坐席；审计 `session.assign` |
| 5.4 | 坐席 `POST .../claim` | 认领自己；审计 `session.claim` |
| 5.5 | 列表筛「未分配 / 分给我」 | 与 handoff 并存 |

## 6. SLA / 告警

| # | 步骤 | 预期 |
|---|------|------|
| 6.1 | `GET /api/merchant/alerts` | 返回 SLA / Token 告警；`browserNotifySupported`，**无 pushStub 暗示 Push** |
| 6.2 | 收件箱声音 / 浏览器 Notification | 仅应用内能力；无 APNs/FCM |

## 7. Admin 运营开关

| # | 步骤 | 预期 |
|---|------|------|
| 7.1 | Admin 登录 → 系统设置 | 可编辑维护模式 / 默认出站 / 允许注册 |
| 7.2 | `PUT /api/admin/settings` | 写入 `system_settings`；审计 `admin.settings.update` |
| 7.3 | 确认响应无密钥字段 | AppKey/Secret/Token 不可读写 |

## 验收签字（人工）

- [ ] 沙箱 App 凭证已配且未入库  
- [ ] 绑店 upsert 成功  
- [ ] Webhook 幂等通过  
- [ ] 草稿人审出站成功（至少 1 条）  
- [ ] handoff + 分配/认领可用  
- [ ] alerts 文案无「已 Push」误导  

**不做（本阶段）**：APNs/FCM、支付生产、多站点 partner、承运商假轨迹。
