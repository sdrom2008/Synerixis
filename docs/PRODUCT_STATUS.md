# Synerixis 产品状态总览

> 更新日期：2026-09-14（Asia/Shanghai）。  
> 定位：**跨境多店客服工作台 + AI 辅助起草**（draft-first / human-in-the-loop），不是全自动 chatbot。  
> 细节见 [`PRODUCTIZATION_PLAN.md`](./PRODUCTIZATION_PLAN.md)、[`SHOPEE_CLOSED_LOOP.md`](./SHOPEE_CLOSED_LOOP.md)、[`MARKET_FIT_AND_POSITIONING.md`](./MARKET_FIT_AND_POSITIONING.md)。

## 1. 已完成（代码内可演示 / 可联调）

| 能力 | 说明 |
|------|------|
| 商家工作台 `merchant-web` | 登录、概览、收件箱三栏、草稿审发、转人工、SLA、团队、计费用量、上手清单 |
| 移动端 `frontend` | Dashboard / 店铺 / 收件箱 / AI 设置 / 计费主路径 |
| Admin `admin-console` | 商家/连接/会话/用量/可写运营设置；审计 |
| Shopee OAuth 绑店 | 授权 URL → 回调 upsert `PlatformConnection`；**可按 region 绑店**（SG/TW/VN/…） |
| Shopee 多站点 partner | `Shopee:Partners[]` 或 `Shopee:{Region}:Host/PartnerId/PartnerKey`；兼容单组 AppKey/Secret；无多 key 时默认 SG/全球 |
| Shopee Webhook | 验签（可多 PartnerKey）、幂等、会话入库、draft-first |
| Shopee 订单 / 物流 | `get_order_*` + **`get_tracking_info` 真实轨迹**；失败诚实降级，不编造 checkpoint |
| TikTok Shop 客户端 | Webhook 验签/解析、IM 回复、OAuth 骨架 |
| TikTok 物流轨迹 | **`GET /fulfillment/202309/orders/{order_id}/tracking`**；有数据返回 checkpoints；无权限/失败空轨迹 + warning |
| Token 生命周期 | `TokenExpiresAt` 后台刷新；**过期/即将过期告警 UI**（Shops 顶栏/列表，已在更早 commit）；刷新失败引导重绑 |
| 人工协同 | Handoff 硬闸、敏感词/低置信自动转人工、营业外策略、坐席分配/认领 |
| 运维 | `/health` + `/health/ready`；Maintenance 闸；Webhook 限流（默认 Memory；可选 Redis） |
| AI 用量 | `AiUsageLog` + byPurpose；快捷回复 CRUD |

## 2. 需外部账号 / 沙箱才能验

| 项 | 需要什么 | 仓库内状态 |
|----|----------|------------|
| Shopee Partner 沙箱/生产 | PartnerId/Key、店铺授权、Webhook HTTPS | 客户端与绑店已通；实机见 `SHOPEE_SANDBOX_CHECKLIST.md` |
| Shopee 多站点实机 | 各区独立 partner 应用或同 App 多区 Host | 配置与绑店 region 已支持；需真实 key 填 `Partners` |
| TikTok Shop Open API | ClientKey/Secret、shop_cipher、履约 scope | 轨迹路径已接；无凭证或无权限时诚实降级 |
| TikTok 客服消息 scope | Partner 审批 `seller.customer_service` | 客户端具备；**ISV 门槛高**，见 `TIKTOK_SHOP_PITFALLS.md` / `ISV_APPLICATION_CHECKLIST.md` |
| LLM 真实起草 | DashScope / OpenAI 等 API Key | 未配 key 时意图/起草会降级或跳过 |
| 支付生产对接 | 微信/支付宝商户号、证书、回调域名 | **本阶段不做生产对接**；代码有 Provider 桩 |
| APNs / FCM | 苹果/谷歌推送证书与包名 | **本阶段不做**；alerts 仅应用内 + 可选浏览器 Notification |
| Redis 多实例限流 | `ConnectionStrings:Redis` + `Webhook:RateLimitStore=Redis` | 接口已接 StackExchange Redis；默认仍 Memory |

## 3. 明确不做（本阶段）

- 支付生产对接与对账闭环  
- APNs / FCM / 完整 WebPush 服务  
- RegimeTrader / 无关交易模块  
- **假物流轨迹**（任何平台失败一律空 checkpoints + warning）

## 4. 配置要点（多站点 + 限流）

### Shopee 多站点

```json
"Shopee": {
  "Region": "SG",
  "AppKey": "可选-单组兼容",
  "AppSecret": "可选-单组兼容",
  "Endpoint": "https://partner.shopeemobile.com",
  "RedirectUri": "https://your.api/api/oauth/shopee/callback",
  "Partners": [
    { "Region": "SG", "Host": "https://partner.shopeemobile.com", "PartnerId": "...", "PartnerKey": "..." },
    { "Region": "TW", "Host": "https://partner.tw.shopeemobile.com", "PartnerId": "...", "PartnerKey": "..." }
  ]
}
```

绑店：`GET /api/merchant/bind/shopee?region=TW`；merchant-web「店铺绑定」页有站点下拉。connection 存 `Region`。

### Webhook 限流

```json
"Webhook": { "RateLimitPerMinute": 120, "RateLimitStore": "Memory" },
"ConnectionStrings": { "Redis": "localhost:6379" }
```

- **Memory（默认）**：单实例足够。  
- **Redis**：多实例部署必须；未配 Redis 连接串时自动回退 Memory 并打日志。

## 5. 过时缺口说明（已对齐）

以下条目**不再作为工程缺口**（勿再列入「下一轮必做」）：

- ~~Token 过期告警 UI~~ → 已完成  
- ~~Shopee 真实物流轨迹~~ → 已完成  
- ~~TikTok 轨迹路径~~ → 本轮已接（实机仍需账号）  
- ~~Shopee 多站点 partner 配置/绑店 region~~ → 本轮已完成  
- ~~多实例限流开关~~ → 本轮可选 Redis；默认 Memory  

仍依赖外部的项见第 2 节；明确不做见第 3 节。

