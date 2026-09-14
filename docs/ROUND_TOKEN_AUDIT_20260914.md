# 本轮摘要：Token 告警 UI · 审计日志 · Webhook 小加固

日期：2026-09-14（Asia/Shanghai）

## 已完成

### 1. Token 过期告警 UI
- `GET /api/merchant/connections` 对齐字段：`expiresAt`、`expiresInHours`、`status=ok|expiring|expired|unknown`（**24h** 内过期=expiring）；保留 `tokenStatus`/`tokenExpiresAt` 兼容。
- `merchant-web` `/shops`：状态标签颜色；过期/即将过期醒目警告条 +「立即刷新」。
- Layout 顶栏徽章 + Overview 提示条 → 跳转 `/shops`。
- `GET /api/merchant/alerts` 增加 `type=connection_token` 条目。

### 2. 审计日志（轻量）
- 实体 `AuditLog` + SchemaPatcher + `Migrations/AddAuditLogs_20260914.sql`。
- `IAuditLogger` 记录：绑店/解绑/刷新 token、团队增改禁/重置密码、草稿 approve/reject、转人工、AI 设置变更。
- `GET /api/merchant/audit-logs?take=50`；`GET /api/admin/audit-logs`。
- 前端：merchant-web `/audit`；admin-console `/audit`。

### 3. Webhook 小加固
- **未**把生产 Webhook 合并进 `ConversationService`（后者旁路 draft-first/handoff，已加注释说明生产路径）。
- 出站 `SendReply` 成功后写 `PlatformMsgId=outbound:{msgId}`（Merchant 人审发送 + Webhook AutoSend）。

## 明确不做（本轮 / 下一轮仍不碰）
- 支付生产、APNs/FCM、多站点 partner、RegimeTrader、假承运商轨迹。

## 下一轮缺口（建议）
- 出站 SendReply 若平台返回真实 message_id，写入 PlatformMsgId 替代本地 `outbound:` 前缀。
- Admin 写操作（若后续增加）补 `admin.sensitive` 审计。
- Token 刷新失败自动引导重新 OAuth 绑店。
- WebPush / 桌面声音（alerts 仍 pushStub）。
