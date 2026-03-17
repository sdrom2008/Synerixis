# MVP 开发实施方案 (第一期)

**版本**: 1.0
**日期**: 2026-03-16
**负责人**: 虾子 (AI Assistant)

---

## 1. 概述

本方案旨在明确 NexusAI Tech 项目第一期 MVP (最小可行产品) 的开发目标和实施步骤。根据项目会议决议，首期 MVP 将聚焦于两大核心功能：“营销文案生成”和“AI 客服”，旨在快速验证产品核心价值，并为后续迭代打下坚实基础。

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
作为一名电商商家，我希望能将AI客服无缝接入我的淘宝和抖店店铺。当有顾客发起咨询时，AI不仅能进行智能对话，还能根据顾客的指令，实时查询并告知订单状态和物流信息。

### 3.2. 技术实施步骤

| 模块 | 层级 | 动作 | 文件/类名 | 备注 |
| :--- | :--- | :--- | :--- | :--- |
| **会话管理** | Application | 新建 | `Interfaces/IConversationService.cs` | 定义会话管理的接口。 |
| | Application | 新建 | `Services/ConversationService.cs` | 实现会话的创建、消息记录、上下文管理等。 |
| **意图识别** | Application | **修改** | `Services/IntentClassifier.cs` | 核心改造点。利用LLM的函数调用能力，识别用户的真实意图（如查询订单、查询物流）。 |
| **任务执行** | Application | 新建 | `Agents/OrderAgent.cs` | 封装所有与“订单查询”相关的业务逻辑。 |
| | Application | 新建 | `Agents/LogisticsAgent.cs` | 封装所有与“物流查询”相关的业务逻辑。 |
| **平台对接** | Infrastructure| 新建 | `Messaging/TaobaoClient.cs` | 实现与淘宝开放平台（千牛）API的认证与通信。 |
| | Infrastructure| 新建 | `Messaging/DouyinClient.cs` | 实现与抖店开放平台API的认证与通信。 |
| **消息入口** | Api | 新建 | `Controllers/WebhookController.cs` | 创建一个Webhook端点，用于接收来自淘宝和抖店平台推送的实时消息。 |

---

## 4. 下一步

请审阅此方案。一旦方案获得批准，开发工作将按照上述步骤，从 **目标一：营销文案生成** 的 `Domain` 层开始，正式进入编码阶段。
