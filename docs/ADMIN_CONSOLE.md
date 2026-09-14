# Admin 控制台说明

## 定位

运营侧只读监控：商家、店铺连接、会话、用量 KPI。  
**不**替代商家工作台（`merchant-web/`）的人审发信。

## 端口与代理

| 服务 | 默认地址 |
|------|----------|
| Admin Vite | `http://localhost:3000` |
| API | `http://localhost:5000` |
| Vite proxy | `/api` → `http://localhost:5000` |

可选环境变量：`VITE_API_BASE_URL`（直连 API，不走代理）。

## 鉴权

1. Development：`POST /api/auth/init-agent` 创建 `admin@test.com` / `Agent123!`（Admin）
2. 登录页调用 `POST /api/auth/agent-login`，校验 `role === Admin`
3. Token 存 `localStorage.sx_admin_token`；路由守卫拦截未登录
4. 后端 `[Authorize(Roles = "Admin")]` 保护 `/api/admin/*`

## API 一览

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/admin/dashboard` | 商家数、连接店铺、今日会话、待手审草稿、待人工、SLA overdue |
| GET | `/api/admin/merchants?page=&pageSize=&q=` | 商家分页 |
| GET | `/api/admin/shops` 或 `/connections` | 店铺连接列表 |
| GET | `/api/admin/sessions?take=` | 最近会话只读 |
| GET | `/api/admin/usage` | 全站用量汇总 |
| GET | `/api/admin/settings` | 只读配置说明 |

无数据时返回空列表 / 0，前端不编造指标。
