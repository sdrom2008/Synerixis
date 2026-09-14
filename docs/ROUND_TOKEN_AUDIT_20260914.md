# 本轮摘要：刷新失败重绑 · Admin 审计 · 出站平台 message_id

日期：2026-09-14（Asia/Shanghai）

## 已完成（本轮增量，基于 `3ff2763`）

### 1. 刷新失败 → 重绑引导
- `POST /api/merchant/connections/{id}/refresh` 失败返回 `errorCode=TOKEN_REFRESH_FAILED`、`code=REBIND_REQUIRED`、`rebindRequired=true` 与 `message`。
- `PlatformConnection` 新增 `LastRefreshError` / `LastRefreshAt`（SchemaPatcher + `Migrations/AddPlatformConnectionRefreshError_20260914.sql`）。
- `GET /api/merchant/connections` 暴露 `lastRefreshError` / `lastRefreshAt` / `needsRebind`；过期且刷新失败时 `tokenHint=需重新授权`。
- `merchant-web` `/shops`：刷新失败 `ElMessageBox` 引导「重新绑定」并调 `startBind`；列表展示 `lastRefreshError`。
- 顶栏 Token 徽章：`expired` 且刷新失败时文案「需重新授权」。

### 2. Admin 写操作审计
- `IAuditLogger` 注入 `AdminController` / `AuthController`。
- Admin 登录成功记 `admin.login`。
- 写接口：`PATCH /api/admin/merchants/{id}/active`、`PATCH /api/admin/merchants/{id}/subscription`（记 `admin.merchant.*` / `admin.subscription.update`）。
- admin-console 商家页接上禁用/改订阅。

### 3. 出站真实 platform message_id
- `IPlatformClient.SendReplyAsync` 改为返回 `Task<string?>`。
- Shopee / TikTok 解析响应中的 `message_id`（若有）；写入 `ChatMessage.PlatformMsgId`。
- 无平台 id 时仍写 `outbound:{guid}`，并在客户端注释说明。

### 4. 小抛光
- merchant-web Audit 页支持按 `action` 筛选；API `GET /api/merchant/audit-logs?action=`。
- 本文档更新；PRODUCTIZATION 补一行。

## 明确不做
- 支付生产、APNs/FCM、完整 WebPush 服务、多站点 partner、RegimeTrader、假承运商轨迹。

## 下一轮缺口（建议）
- WebPush / 桌面声音（alerts 已改为 `browserNotifySupported`（无 Push））。
- Shopee/TikTok 沙箱实机验证出站 `message_id` 字段路径。
- Admin 设置页若增加可写配置，补 `admin.sensitive` 审计。
- Partner 多站点 / 支付生产仍不在本阶段。
