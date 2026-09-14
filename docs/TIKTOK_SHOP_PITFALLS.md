# TikTok Shop 门槛与坑（客服 / 消息向）

> 调研日期：2026-09-14。定性结论供产品决策；官方门槛以 Partner Center 当前文档为准，申请前再核对一次。

## 1. 和 Shopee 一样的「平台卡死」风险

| 点 | TikTok Shop | 对我们的含义 |
|----|-------------|--------------|
| 客服消息 API | **受限自定义 scope** `seller.customer_service`，默认不开，要审批 | 不能假设「写了客户端就能发消息」 |
| 规模门槛（第三方常见口径） | 申请材料常要求：**可用的工作台产品**（会话列表/详情/收发）+ **订单/履约/售后上下文**；规模上常见表述为 **≥1000 授权卖家** 或 **≥100 万次 API 调用/天** | 纯原型很难过；Self-developed 卖家自建 App 可能有特批，但不是保证 |
| 响应率考核 | 卖家侧 **24h Response Rate ≥90%**；2026 起业界报道：**自动回复/FAQ/AI 秒回不算人工响应** | 全自动替人值班 **既不合规叙事，也帮不了评分** → 强化「人在环 / draft-first」 |
| 授权生命周期 | Seller 授权与 App scope 审批是两件事；token 可过期/被撤 | 必须有刷新、失效告警、店铺级凭证 |

## 2. 产品与工程坑清单

1. **App 类型选错**：Seller Developer / SI / App Developer；Public App 服务类目选错会卡 scope（常见错误码 105005）。客服能力要选对 Customer Service 类目。
2. **不是普通 TikTok DM**：Customer Service API ≠ 广告 Business Messaging ≠ 普通私信。
3. **Webhook**：需 HTTPS、TLS1.2+、验签、3 秒内空 200、用 `tts_notification_id` / `message_id` 去重；收消息不等于已读。
4. **分页**：消息拉取 `page_size` 上限常为 **10**，靠 `next_page_token`。
5. **shop_cipher**：多店/区域场景必存，和 access_token 绑定。
6. **Token**：access 常见约 7 天级（以实际响应为准）；code 短时单次；刷新失败要可运维。
7. **区域碎片**：US/UK/SEA 等能力与 App 可用性不一致；竞品也按区域拆。
8. **订单 API 相对好拿**，客服 scope 才是硬门槛——可先做「订单/履约只读工作台」，客服收发第二阶段。
9. **Self-developed**：自有店自建可能特批，适合「先用自己店打穿 demo」，不适合当 SaaS 扩量路径。
10. **禁止用 bot 伪装人工**去刷响应率——与我们新定位一致。

## 3. 对 Synerixis 的建议（结合新定位）

- **叙事**：工作台 + AI 起草 + 人点发送；把「保 90% 响应率」写成产品价值（提醒待办、超时预警），而不是「AI 自动刷响应率」。
- **准入策略**：
  1. 自有店 Custom App 打穿订单+（若特批）客服；
  2. 同时攒 ISV 材料：真实聊天 UI、订单/售后联动截图、合规说明；
  3. 规模不够时先卖「代运营工作台 / 人工席位」能力，客服 API 批下来再开自动通道。
- **与 Shopee 对照**：两边都反「裸 chatbot」；TikTok 额外用 **响应率不计自动回复** 把「全自动」从商业上打死。

## 4. 参考链接（需人工再核）

- Partner Center Customer Service API Overview：https://partner.tiktokshop.com/docv2/page/customer-service-api-overview
- 第三方整理的审批清单（非官方）：https://www.unifyport.ai/blog/tiktok-shop-customer-service-api-approval-checklist/
- Response Rate / 自动回复不计分报道：行业文章 2026（以卖家后台规则页为准）

