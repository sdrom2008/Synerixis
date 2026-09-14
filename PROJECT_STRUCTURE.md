# Synerixis — 项目结构

**定位**: CBEC 智能客服（Phase1 = **Shopee + TikTok Shop**；淘宝/抖店已从 Phase1 目标中移除）

**状态**: MVP 后端可编译；闭环缺口见 `docs/SHOPEE_CLOSED_LOOP.md`

## 目录结构

```
my-project/
├── Synerixis.sln
├── README.md
├── PROJECT_STRUCTURE.md
│
├── Synerixis.Application/               # 应用层（服务、接口、DTO）
│   ├── Agents/                          # Agent 实现
│   ├── DTOs/                            # 数据传输对象
│   ├── Interfaces/                      # 应用层接口
│   ├── Repositories/                    # 仓库接口
│   └── Services/                        # 业务服务实现
│
├── Synerixis.Domain/                    # 领域层（实体、值对象、仓库接口）
│   ├── Common/
│   ├── Entities/
│   ├── Enums/
│   └── Repositories/
│
├── Synerixis.Infrastructure/            # 基础设施层（数据访问、外部服务）
│   ├── AIServices/
│   ├── Repositories/
│   ├── Services/
│   └── Data/
│
├── Synerixis.Api/                       # 表示层（Web API）
│   ├── Controllers/
│   ├── Program.cs
│   └── Synerixis.Api.csproj
│
└── frontend/                            # React 前端
    ├── src/
    └── package.json
```

## 关键接口与实现

| 接口 | 位置 | 实现 | 状态 |
|------|------|------|------|
| `IMarketingCopyService` | Application/Interfaces/ | MarketingCopyService | ✅ 实现存在 |
| `IConversationRepository` | Application/Interfaces/ | ConversationRepository (Infrastructure/Repositories/) | ✅ 实现完成 |
| `IAgent` | Application/Interfaces/ | OrderAgent, LogisticsAgent | ⚠️ 需要调整 |
| `ILlmClient` | Application/Interfaces/Ai/ | AliyunLlmClient | ✅ 存在 |
| `IECommercePlatformClient` | Application/Interfaces/Infrastructure/ | ECommercePlatformClient | ✅ 存在 |

## 已知问题与待办

1. **Agent DI 注册**：✅ 已完成 - OrderAgent, LogisticsAgent, ProductOptimizationAgent, CompetitorAnalysisAgent 已注册
2. **CORS 中间件**：✅ 已修复 - AllowAll → AllowSpecific
3. **支付 Provider**：✅ 已启用 - WeChatPayV3Client, WechatPaymentProvider, AlipayPaymentProvider
4. **Nullable 警告**：✅ 已清理 - 从 110 降至 2 条（仅剩 HttpContextFactory 和 CompetitorAnalysisController）
5. **RAG 代码索引**：✅ 已完成 - MySQL 1617 chunks, TF-IDF 5000 维词向量
6. **模型文件**：`regime_model.pkl` 用于量化交易，非本项目核心

## RAG 使用

```bash
# 查询代码库
PYTHONPATH=/home/rich/.local/lib/python3.12/site-packages python3 tools/query_rag.py "你的问题" top_k
```

## Phase1 平台客户端

| 客户端 | 路径 | 说明 |
|--------|------|------|
| `ShopeePlatformClient` | Infrastructure/Clients/ | Webhook、发信、订单查询（v2） |
| `TikTokShopPlatformClient` | Infrastructure/Clients/ | Webhook / 发信骨架 |
| `PlatformClientRouter` | Infrastructure/Clients/ | 按平台名路由 |

淘宝 / 抖店相关命名若仍出现在旧接口注释中，视为历史残留，**不是 Phase1 交付项**。

## 文档

- `docs/BUSINESS_PLAN_CBEC.md` — CBEC 商业计划
- `docs/SHOPEE_CLOSED_LOOP.md` — Shopee 闭环清单
- `docs/PRICING_DRAFT.md` — 定价草案

## 下一步建议

- [ ] 打通 Webhook → IntentClassifier → AgentRouter（替换占位 `GenerateAiReply`）
- [ ] OrderAgent：DB 空时 fallback `IPlatformClient.GetCustomerOrderAsync`
- [ ] 补齐 Shopee OAuth 回调与 token 落库
- [ ] 对齐 TikTokShop 与 Shopee 同一验收清单
- [ ] 补齐 admin-console；补充 Docker Compose
- [ ] 修复 Webhook 幂等判断逻辑

---

最后更新：2026-09-14（CBEC Phase1 叙事）
GitHub: https://github.com/sdrom2008/Synerixis
