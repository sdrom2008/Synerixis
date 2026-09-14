# Synerixis Admin Console

运营后台（Vue 3 + Element Plus + Vite + ECharts）。

## 功能（P1b 可用化）

- 登录：`POST /api/auth/agent-login`，**仅 AgentRole.Admin** JWT
- 侧栏：概览 / 商家 / 店铺连接 / 会话监控 / 用量计费 / 系统设置
- 真实只读 API：`/api/admin/dashboard|merchants|shops|sessions|usage|settings`
- **禁止伪造生产指标**；无数据时「—」或空表

## 开发

```bash
# 终端 1：API（默认 http://localhost:5000）
cd Synerixis.Api && dotnet run

# 终端 2：Admin 控制台
cd admin-console
npm install
npm run dev
# http://localhost:3000  （/api 代理到 http://localhost:5000）
```

开发环境可先创建 Admin：

```bash
curl -X POST http://localhost:5000/api/auth/init-agent
# 默认邮箱 admin@test.com / 密码 Agent123!（Role=Admin）
```

再在登录页使用该邮箱密码。

## 构建

```bash
npm run build
```

## 设计约定

- 桌面优先，中文文案
- 主色 `#2563EB`
- 产品定位：工作台 + AI 草稿人审，**不是**全自动 chatbot

详见 `docs/ADMIN_CONSOLE.md`、`docs/PRODUCTIZATION_PLAN.md`。
