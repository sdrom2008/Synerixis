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

1. **Agent DI 注册**：需要恢复 DI 注册并测试 Router 路由
2. **模型文件**：`regime_model.pkl` 用于量化交易，非本项目核心

## 下一步建议

- [ ] 统一 Agent 接口
- [ ] 恢复 Program.cs 中的 DI 注册
- [ ] 补充 ConversationService 中的错误处理与日志
- [ ] 编写部署文档和环境变量说明
- [ ] 将项目从"电商平台客服"升级为"跨境电商平台智能客服"

---

最后更新：2026-04-14
GitHub: https://github.com/sdrom2008/my-project
