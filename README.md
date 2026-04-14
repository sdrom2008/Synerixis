# Synerixis AI 智能客服

Monorepo: ASP.NET Core 8 + React

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET Version](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/)

## Synerixis AI 智能客服 - 跨境电商智能客服平台

**Synerixis AI 智能客服** 专注于 AI 驱动的跨境电商智能客服解决方案，通过 agentic 方式和 intent-driven 智能，实现通过自然语言自动完成订单管理、客户支持等任务。

## 项目愿景与使命

Synerixis AI 智能客服 专注于 .NET 生态下的 AI 智能客服开发，核心目标：
- **智能对话**：通过 NLP 和用户意图识别，实现多轮对话，提升客服体验
- **市场定位**：领先跨境电商智能客服解决方案，降低运营成本和提升效率

## 核心能力

- **智能对话管理**：对话流管理，用户输入理解和自动回复
- **订单管理功能**：订单查询，状态更新和物流跟踪
- **自动开票功能**：AI 生成发票，合规性检查和批量处理

项目目标是成为企业级 AI 客服操作系统。

## 技术栈

- **后端**：.NET 8 (ASP.NET Core API), Entity Framework Core
- **前端**：React.js with TypeScript
- **AI 服务**：Azure OpenAI / 阿里云大模型 API
- **数据库**：PostgreSQL / SQL Server
- **部署**：Docker, Kubernetes

## 功能架构

基于 Clean Architecture 确保可维护性和可扩展性。

## 项目结构

```
my-project/
├── Synerixis.sln
├── README.md
├── PROJECT_STRUCTURE.md
│
├── Synerixis.Application/
│   ├── Agents/
│   ├── DTOs/
│   ├── Interfaces/
│   └── Services/
│
├── Synerixis.Domain/
│   ├── Common/
│   ├── Entities/
│   ├── Enums/
│   └── Repositories/
│
├── Synerixis.Infrastructure/
│   ├── AIServices/
│   ├── Repositories/
│   ├── Services/
│   └── Data/
│
└── Synerixis.Api/
    ├── Controllers/
    ├── Program.cs
    └── Synerixis.Api.csproj
```

## 下一步

- [ ] 完善 Agent 路由功能
- [ ] 添加更多的客服场景
- [ ] 优化对话流程
- [ ] 增强错误处理和日志记录

## 贡献

欢迎贡献！请遵循以下步骤：
1. Fork 仓库
2. 创建 feature 分支
3. Commit 变更
4. Push 到分支
5. 打开 Pull Request

## 代码风格

- 遵循 .NET 编码规范
- 使用 EditorConfig

## 许可证

本项目采用 MIT License (LICENSE)。

## 联系我们

- GitHub: sdrom2008
- Email: sdrom2008@qq.com
