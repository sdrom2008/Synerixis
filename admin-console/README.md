# Synerixis Admin Console

运营后台（Vue 3 + Element Plus + Vite + ECharts）。

## 功能壳（P1b 起步）

- 登录页（本地脚手架鉴权桩）
- 侧栏布局：概览 / 商家 / 店铺连接 / 会话监控 / 用量计费 / 系统设置
- 概览页：KPI 卡片（占位「—」）+ ECharts 折线示意（全 0，非生产数据）
- 亮/暗主题切换

## 开发

```bash
cd admin-console
npm install
npm run dev
# http://localhost:3000  （/api 代理到 http://localhost:5000）
```

## 构建

```bash
npm run build
```

## 设计约定

- 桌面优先，中文文案
- 主色 `#2563EB`
- **禁止展示伪造生产指标**；未接 API 时用「—」或「暂无数据」

详见仓库 `docs/PRODUCTIZATION_PLAN.md`。
