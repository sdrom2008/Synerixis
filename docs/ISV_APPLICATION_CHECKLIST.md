# ISV / Chat API 申请证据清单（Shopee + TikTok Shop）

> 用途：准备 Partner / ISV / Customer Service 类应用材料时的**诚实核对表**。  
> 日期：2026-09-14。以各平台开放平台当时原文为准；本表不替代官方条款。

## 产品定位（对外统一口径）

| 可以说 | 不要说 |
|--------|--------|
| 跨境多店客服工作台 + AI 辅助起草 | 全自动 chatbot / 7×24 无人值守机器人 |
| 人审后发送、统一收件箱、订单上下文草稿 | 自动回复保证计入平台响应率 |
| 超时提醒帮助坐席优先处理 | 用 Chat API 做促销群发 / 广播 |

**默认出站：`OutboundMode=DraftFirst`。** `AutoSend` 仅商户显式开启，文档与 UI 均标注合规风险。

---

## Shopee（优先）

### 申请前自检

- [ ] 应用类型选 **Customer Service / 客服工具**（勿标营销群发）
- [ ] 授权流程：OAuth → `PlatformConnection` 落 token；Webhook URL 可公网验签
- [ ] 演示路径：**进线 → AI 草稿 → 坐席确认 → `send_message`**（截图/录屏）
- [ ] 明确未将 Chat API 用于促销推送、订单批量通知、伪装 bot
- [ ] 了解 Chat API 店铺订单量等准入门槛（以官网为准）
- [ ] `send_message` 内容校验加强后的失败处理（日志 + 商户可见错误）

### 建议附证据

1. 架构图：Webhook → Intent → Draft → Human approve → SendReply  
2. 设置页截图：出站模式默认「草稿优先」+ AutoSend 风险文案  
3. 收件箱「待发送草稿」与会话详情发送操作录屏  
4. 隐私政策 / 数据处理说明（消息存储与保留天数）  
5. 测试店铺与 Partner 沙箱联调记录（见 `SHOPEE_CLOSED_LOOP.md`）

---

## TikTok Shop

### 申请前自检

- [ ] 使用官方客服 / IM 相关 API；阅读店铺所在站点的 CS API 门槛（见 `TIKTOK_SHOP_PITFALLS.md`）
- [ ] **不宣称** AI 自动回复计入官方「响应率 / 服务质量」考核  
- [ ] 默认草稿 + 人审发送；自动发送关闭或仅内测  
- [ ] Webhook 验签、幂等、按店 token  
- [ ] 多国家/站点资质与类目限制已核对

### 建议附证据

1. 与 Shopee 同构的 draft-first 演示（平台换成 TikTok）  
2. 文案审查：营销页无「保证响应率」「无人客服」等表述  
3. 失败降级：无 ConversationId / token 时仅 Warning，不刷接口  

---

## 工程开关（仓库现状）

| 项 | 位置 | 默认 |
|----|------|------|
| OutboundMode | `SellerConfig` / `CustomerService:OutboundMode` | `DraftFirst` |
| 生成草稿 | `EnableAutoReply` | true（生成草稿，不等于自动出站） |
| 人审发送 | `POST /api/merchant/sessions/{id}/draft/approve` | — |
| 迁移 | `Migrations/AddDraftFirstOutbound_20260914.sql` | SchemaPatcher 启动补丁 |

---

## 提交材料禁忌

- 用「自动回了 N 条」当作平台响应率截图去申请  
- 隐藏 AutoSend、对外只展示「智能客服全自动」  
- 将 Chat 通道做成营销触达  

维护：政策变更时先改本清单与 `MARKET_FIT_AND_POSITIONING.md`，再改代码默认行为。
