# Synerixis AI 智能客服 - 项目结构

**状态**: MVP 后端代码可编译

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

## 下一步建议

- [ ] 补齐 admin-console 管理后台源码（目前为空壳）
- [ ] 实现 TikTokShop webhook 集成
- [ ] 补充 Docker Compose 部署文件
- [ ] 清理 AlipayPaymentProvider nullable 警告
- [ ] 将项目从"电商平台客服"升级为"跨境电商平台智能客服"

---

最后更新：2026-04-24 (RAG index + bug fixes)
GitHub: https://github.com/sdrom2008/my-project
