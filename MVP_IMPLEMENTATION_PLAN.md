# MVP 开发实施方案 (第一期)

**版本**: 1.1（CBEC 修订）
**日期**: 2026-09-14（原文 2026-03-16）
**负责人**: 工程团队

> **Phase1 平台纠正**：目标平台为 **Shopee + TikTok Shop**。原文中的淘宝 / 抖店（千牛、抖店开放平台）**已废弃为 Phase1 范围**，仅作历史记录保留在下方表格中并标注 Deprecated。

---

## 1. 概述

本方案明确 Synerixis **跨境电商（CBEC）** 第一期 MVP 的目标与步骤。首期聚焦：店铺消息闭环（Webhook → 意图 → 查单 → 回复 → 转人工），营销文案生成为辅助能力。商业目标与定价见 `docs/BUSINESS_PLAN_CBEC.md`、`docs/PRICING_DRAFT.md`；Shopee 缺口见 `docs/SHOPEE_CLOSED_LOOP.md`。

---

## 2. 目标一：营销文案生成

### 2.1. 用户故事
作为一名电商商家，我希望能输入商品的关键信息（如名称、卖点、关键词），然后系统能为我智能生成多版高质量、吸引人的营销文案，用于商品描述、广告投放等场景。

### 2.2. 技术实施步骤

| 层级 | 动作 | 文件/类名 | 备注 |
| :--- | :--- | :--- | :--- |
| **Domain** | 新建 | `MarketingCopy.cs` | 定义和存储文案结果的核心实体。 |
| **Application**| 新建 | `Interfaces/IMarketingCopyService.cs` | 定义文案生成服务的“契约”（接口）。 |
| **Application**| 新建 | `Services/MarketingCopyService.cs` | 实现文案生成的核心业务逻辑，包括构建Prompt、调用AI模型等。 |
| **Infrastructure**| 完善/新建 | `Ai/LlmClient.cs` | 确保有能够调用大语言模型（如Azure OpenAI）的客户端实现。|
| **Api** | 新建 | `Controllers/MarketingController.cs` | 创建一个HTTP API接口，供前端或其他客户端调用。 |

---

## 3. 目标二：AI 客服 (集成与查询能力)

### 3.1. 用户故事
作为一名 **Shopee / TikTok Shop** 跨境卖家，我希望把 AI 客服接到店铺聊天。买家咨询时，系统能识别意图、查询订单/物流并自动回复；搞不定时转人工。

（历史表述「淘宝和抖店」已废弃，不再作为 Phase1 验收。）

### 3.2. 技术实施步骤

| 模块 | 层级 | 动作 | 文件/类名 | 备注 |
| :--- | :--- | :--- | :--- | :--- |
| **会话管理** | Application | 新建 | `Interfaces/IConversationService.cs` | 定义会话管理的接口。 |
| | Application | 新建 | `Services/ConversationService.cs` | 实现会话的创建、消息记录、上下文管理等。 |
| **意图识别** | Application | **修改** | `Services/IntentClassifier.cs` | 核心改造点。利用LLM的函数调用能力，识别用户的真实意图（如查询订单、查询物流）。 |
| **任务执行** | Application | 新建 | `Agents/OrderAgent.cs` | 封装所有与“订单查询”相关的业务逻辑。 |
| | Application | 新建 | `Agents/LogisticsAgent.cs` | 封装所有与“物流查询”相关的业务逻辑。 |
| **平台对接** | Infrastructure| **已有** | `Clients/ShopeePlatformClient.cs` | Phase1 主路径：签名、Webhook、发信、订单查询。 |
| | Infrastructure| **已有** | `Clients/TikTokShopPlatformClient.cs` | Phase1 第二平台。 |
| | Infrastructure| Deprecated | `Messaging/TaobaoClient.cs`（计划名） | ~~淘宝千牛~~ — **非 Phase1**。 |
| | Infrastructure| Deprecated | `Messaging/DouyinClient.cs`（计划名） | ~~抖店~~ — **非 Phase1**。 |
| **消息入口** | Api | **已有** | `Controllers/WebhookController.cs` | `POST /api/webhook/{platform}`，面向 Shopee/TikTok。 |

---

## 4. 下一步（2026-09 起）

1. 按 `docs/SHOPEE_CLOSED_LOOP.md` 清缺口：Webhook 接 Intent/Agent，OrderAgent 平台回源，OAuth 落库，handoff 闸门。  
2. 营销文案能力可并行，但 **不阻塞** Shopee 闭环验收。  
3. 淘宝/抖店客户端 **不要** 再投入 Phase1 工期。
