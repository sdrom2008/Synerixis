# NexusAI Tech (AI营销SaaS平台) - 项目结构总结

**状态**: MVP 后端代码可编译，已推送到 GitHub (commit 5ee5657)

## 目录结构

```
my-project/
├── Synerixis.sln
├── regime_model.pkl                     # AI 模型（放在项目根目录）
├── README.md                            # 项目说明（待补充）
├── PROJECT_STRUCTURE.md                 # 本文档
│
├── Synerixis.Application/               # 应用层（服务、接口、DTO）
│   ├── Agents/                          # Agent 实现（目前未启用）
│   │   ├── LogisticsAgent.cs
│   │   └── OrderAgent.cs
│   ├── DTOs/                            # 数据传输对象
│   │   └── GenerateCopyDto.cs
│   ├── Interfaces/                      # 应用层接口
│   │   ├── Agents/
│   │   │   └── IAgent.cs               # 通用 Agent 接口（IAgent）
│   │   ├── Ai/
│   │   │   └── ILlmClient.cs
│   │   ├── Infrastructure/
│   │   │   └── IECommercePlatformClient.cs
│   │   ├── IConversationRepository.cs  # 已从 Domain 移入
│   │   ├── IConversationService.cs
│   │   ├── IMarketingCopyService.cs
│   └── Services/                        # 业务服务实现
│       ├── ConversationService.cs
│       └── MarketingCopyService.cs
│
├── Synerixis.Domain/                    # 领域层（实体、值对象、仓库接口）
│   ├── Common/
│   │   └── AggregateRoot.cs
│   ├── Entities/
│   │   ├── Conversation.cs
│   │   └── MarketingCopy.cs
│   ├── Enums/
│   │   └── ChatIntent.cs
│   └── Repositories/
│       └── IConversationRepository.cs   # 已移动至此（空接口定义已废弃）
│
├── Synerixis.Infrastructure/            # 基础设施层（数据访问、外部服务）
│   ├── AIServices/
│   │   └── AliyunLlmClient.cs
│   ├── Repositories/
│   │   └── ConversationRepository.cs   # 实现 IConversationRepository
│   ├── Services/
│   │   ├── AgentRouter.cs
│   │   └── ECommercePlatformClient.cs
│   └── Data/
│       └── AppDbContext.cs
│
├── Synerixis.Api/                       # 表示层（Web API）
│   ├── Controllers/
│   │   ├── MarketingController.cs
│   │   └── WebhookController.cs
│   ├── Program.cs
│   └── Synerixis.Api.csproj
```

## 关键接口与实现

| 接口 | 位置 | 实现 | 状态 |
|------|------|------|------|
| `IMarketingCopyService` | `Application/Interfaces/` | `MarketingCopyService` (Infrastructure?) | ✅ 实现存在 |
| `IConversationRepository` | `Application/Interfaces/` | `ConversationRepository` (Infrastructure/Repositories/) | ✅ 实现完成 |
| `IAgent` | `Application/Interfaces/Agents/` | `OrderAgent`, `LogisticsAgent` | ⚠️ 接口不匹配（未注册） |
| `ILlmClient` | `Application/Interfaces/Ai/` | `AliyunLlmClient` | ✅ 存在 |
| `IECommercePlatformClient` | `Application/Interfaces/Infrastructure/` | `ECommercePlatformClient` | ✅ 存在 |

## 已知问题与待办

1. **Agent DI 注册被注释**（`Synerixis.Api/Program.cs`）：
   - `OrderAgent` 和 `LogisticsAgent` 实现的是 `Synerixis.Application.Interfaces.Agents.IAgent`（旧接口）
   - 当前项目使用 `Synerixis.Application.Interfaces.IAgent`（Router 使用）
   - 需要调整 Agent 实现以匹配新接口，或恢复旧接口引用。

2. **项目引用路径**：
   - `IConversationRepository` 已移至 `Application.Interfaces`，Domain 中旧定义应删除（避免混淆）
   - Infrastructure 和 Api 项目的 using 已更新，编译通过。

3. **模型文件**：
   - `regime_model.pkl` 放在项目根目录（用于量化交易），非本项目核心。

4. **API 运行时**：
   - 需配置数据库连接字符串（AppDbContext 使用 MySQL）
   - 需配置 Binance API keys（如需接入电商平台）

## 下一步建议

- [ ] 统一 Agent 接口：让 `OrderAgent`/`LogisticsAgent` 实现 `Synerixis.Application.Interfaces.IAgent`（支持 ChatContext 返回）
- [ ] 恢复 Program.cs 中的 DI 注册，并测试 Router 路由
- [ ] 补充 `ConversationService` 中的错误处理与日志
- [ ] 编写 `README.md` 包含部署步骤、环境变量、数据库迁移
- [ ] 考虑将 `IConversationRepository` 放回 Domain（若 Domain 不应依赖 Application），或保持现状（Application 依赖 Domain 单向）

---

最后更新: 2026-03-17
GitHub: https://github.com/sdrom2008/my-project
