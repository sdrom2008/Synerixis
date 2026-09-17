# 本地 MySQL 全量重置（schema drift 修复）

当 admin / merchant 出现字段错误（典型：`orders.CustomerId` 被建成 `BINARY(16)` 而代码期望 `varchar`，或缺 `PlatformMsgId` / handoff / SLA 列）时，用本流程**删表重建**。

权威脚本：[`Migrations/FullReset_nexusai_db.sql`](../Migrations/FullReset_nexusai_db.sql)  
对齐：`AppDbContext` + Domain 实体 + `SchemaPatcher` / 增量 SQL。

## 库名

| 场景 | 库名 |
|------|------|
| 用户本地（`.env.mysql`） | **`nexusai_db`**（脚本默认 `USE nexusai_db`） |
| Docker Compose 默认 | **`synerixis`**（见 `docker-compose.yml` / `.env.example`） |

改脚本首部 `USE`，或先 `USE your_db;` 后注释掉脚本里的 `USE`。

## 步骤

### 1. 备份（建议）

```bash
mysqldump -h HOST -u USER -p nexusai_db > nexusai_db_backup_$(date +%Y%m%d_%H%M%S).sql
```

### 2. 执行全量重置

```bash
# 在仓库根目录
mysql -h HOST -u USER -p nexusai_db < Migrations/FullReset_nexusai_db.sql
```

或交互：

```bash
mysql -h HOST -u USER -p
USE nexusai_db;
SOURCE /absolute/path/to/Synerixis/Migrations/FullReset_nexusai_db.sql;
```

脚本会：`DROP TABLE IF EXISTS`（FK 安全顺序）→ `CREATE` 全表 → 插入演示种子。

### 3. 重启 API

```bash
dotnet run --project Synerixis.Api
```

`EnsureCreated` 在表已存在时不会改结构；本脚本已含全部列，一般无需再跑增量 SQL。  
Development 下仍可调用 `POST /api/dev/seed-demo` 幂等补齐（若你改掉了固定 Guid 种子）。

### 4. 演示账号

| 角色 | 登录 | 凭证 |
|------|------|------|
| 商家 | merchant-web 手机 | `13800138000` / 验证码 `123456`（库内手机 `+8613800138000`） |
| 坐席 | 坐席登录 | `agent@demo.synerixis.local` / `Agent123!` |
| Admin | admin-console | `admin@test.com` / `Agent123!` |

坐席密码为 `AgentPasswordHasher`：`sha256:salt:hash`（脚本内固定 salt，见 SQL 注释）。  
商家**无**本地 PasswordHash，走 Dev 手机验证码。

## 校验清单

```sql
USE nexusai_db;
SHOW TABLES;
-- 期望 22 张，与 AppDbContext DbSet 一一对应：
-- sellers, seller_configs, seller_products, conversations, chat_messages,
-- PayOrders, products, product_attributes, skus, categories, brands,
-- agents, chat_sessions, orders, quick_replies, agent_stats,
-- platform_connections, draft_messages, processed_webhook_events,
-- ai_usage_logs, audit_logs, system_settings

DESCRIBE orders;          -- CustomerId 必须是 varchar，不是 binary
DESCRIBE chat_messages;   -- 须有 PlatformMsgId
DESCRIBE chat_sessions;   -- 须有 PendingHumanHandoff, HandoffAt, PlatformConversationId …
DESCRIBE seller_configs;  -- 须有 OutboundMode, BusinessHours*, ResponseSlaHours, AutoHandoff* …
DESCRIBE platform_connections; -- TokenExpiresAt, LastRefresh*, Region

SELECT Email, Role FROM agents;
SELECT Phone, Nickname FROM sellers;
SELECT COUNT(*) FROM chat_sessions;   -- >= 3
SELECT COUNT(*) FROM draft_messages;  -- >= 1
SELECT COUNT(*) FROM orders;          -- >= 2
```

前端：merchant 收件箱有会话/草稿/订单；admin 概览/商家/用量非空。

## 故意不包含

- **`MarketingCopy`**：实体存在但 **无 DbSet**，EnsureCreated 不建表。
- 其它遗留未映射表：脚本不创建。

## 相关

- [`LOCAL_DEMO.md`](./LOCAL_DEMO.md) — 演示种子与 API
- [`DOCKER.md`](./DOCKER.md) — Compose 库名 `synerixis`
