# Synerixis Admin Console

平台运营台（Vue 3 + Element Plus + Vite + ECharts）。

## 功能

- 登录：`POST /api/auth/agent-login`，**仅 AgentRole.Admin**；成功进 Dashboard
- 演示：`admin@test.com` / `Agent123!`（先 `seed-demo`）；登录页可一键加载演示数据
- 侧栏：概览 / 商家 / 店铺连接 / 会话监控 / 用量计费 / 审计日志 / 系统设置
- 商家治理：启用禁用、改订阅、详情侧栏（连接/会话）
- 运营开关：MaintenanceMode / DefaultOutboundMode / AllowNewRegistration
- Development：系统设置内「开发工具」可调 `seed-demo`
- **禁止伪造生产指标**；无数据时「—」或空表

## 开发

```bash
# 终端 1：API
cd Synerixis.Api && dotnet run

# 终端 2：Admin 控制台
cd admin-console
npm install
npm run dev
# http://localhost:3000  （/api 代理到 http://127.0.0.1:7092，可用 VITE_API_PROXY_TARGET）
```

```bash
curl -X POST http://127.0.0.1:7092/api/dev/seed-demo
# 再用 admin@test.com / Agent123! 登录
```

## 构建

```bash
npm run build
```

详见 `docs/ADMIN_CONSOLE.md`、`docs/LOCAL_DEMO.md`。
