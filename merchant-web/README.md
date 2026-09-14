# Synerixis 商家桌面工作台（merchant-web）

面向 **PC 浏览器** 的跨境多店客服工作台：统一收件箱、AI 草稿、人工批准发送、SLA 视图入口。

与现有应用关系：

| 目录 | 受众 | 技术 |
|------|------|------|
| `frontend/` | 商家移动端 / HBuilder uni-app | uni-app |
| `merchant-web/`（本目录） | 商家 PC 桌面宽屏 | Vue3 + Vite + Element Plus |
| `admin-console/` | 平台运营 | Vue3 + Element Plus |

**不要**用本目录替换或删除 `frontend/`。

## 技术栈

- Vue 3 + TypeScript + Vite
- Element Plus + Pinia + Vue Router + Axios
- ECharts（已列入依赖，概览页暂未强依赖图表）

品牌主色：`#2563EB`。文案：中文。

## 快速开始

```bash
cd merchant-web
cp .env.example .env   # 可选
npm install
npm run dev            # http://localhost:5174
```

生产构建校验：

```bash
npm run build
```

### API 基址

- 开发默认：浏览器请求同源 `/api`，由 Vite 代理到 `VITE_API_PROXY_TARGET`（默认 `http://localhost:5000`）。
- 可设 `VITE_API_BASE_URL` 指向完整 API 根（如 `https://api.example.com`），此时不再依赖代理。

### 登录

1. **手机号登录**：`POST /api/auth/phone-login`（开发环境验证码可用 `123456`）。
2. **粘贴 Token**：使用已有商家 JWT（与 uni-app 商家端同一鉴权）。

## 页面与对接状态

| 路由 | 说明 | 状态 |
|------|------|------|
| `/login` | 登录 | 已接 phone-login + Token |
| `/inbox` | **主战场**：会话列表 \| 消息+草稿审发 \| 订单侧栏占位 | 会话/消息/草稿 API 已接；侧栏占位 |
| `/overview` | KPI 概览 | 已接 `/api/merchant/dashboard`（无数据为 —） |
| `/shops` | 店铺绑定 | 已接 connections / bind / unbind |
| `/ai-settings` | 语气 / OutboundMode / 营业时段 | 已接 seller profile + config |
| `/billing` | 套餐文案 + 用量 | 用量接 `/api/merchant/usage`；套餐为说明卡 |
| `/team` | 团队 | **占位 stub** |

草稿 API（draft-first，commit `a3dc779`）：

- `GET /api/merchant/sessions`、`.../messages`
- `GET/PUT /api/merchant/sessions/{id}/draft`
- `POST .../draft/approve`、`edit-send`、`discard`
- `GET /api/merchant/drafts`

## 设计原则

- 专业桌面壳：顶栏 + 左侧导航，宽屏三栏收件箱。
- 不伪造 KPI；空状态友好提示。
- Handoff / SLA 深化可与后端并行；本脚手架已展示 `needsResponseBy` / `hoursSinceLastBuyerMsg` 字段。
