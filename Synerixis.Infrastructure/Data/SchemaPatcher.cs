using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Synerixis.Infrastructure.Data
{
    /// <summary>
    /// EnsureCreated 不会 ALTER 已有表。对已知增量列/表做幂等补丁（MySQL/MariaDB/SQLite 尽力而为）。
    /// </summary>
    public static class SchemaPatcher
    {
        public static async Task ApplyAsync(AppDbContext db, ILogger? logger = null)
        {
            try
            {
                var provider = db.Database.ProviderName ?? "";
                if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
                {
                    await TrySqliteAsync(db, logger);
                }
                else
                {
                    await TryMySqlAsync(db, logger);
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex,
                    "[SchemaPatcher] Failed to apply schema patches; run Migrations/*.sql manually");
            }
        }

        private static async Task TryMySqlAsync(AppDbContext db, ILogger? logger)
        {
            // SellerConfig AI + OutboundMode
            const string sellerSql = @"
SET @db := DATABASE();
SET @col := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'seller_configs' AND COLUMN_NAME = 'EnableAutoReply'
);
SET @ddl := IF(@col = 0,
  'ALTER TABLE `seller_configs` ADD COLUMN `EnableAutoReply` TINYINT(1) NOT NULL DEFAULT 1, ADD COLUMN `BusinessHoursStart` VARCHAR(8) NOT NULL DEFAULT ''09:00'', ADD COLUMN `BusinessHoursEnd` VARCHAR(8) NOT NULL DEFAULT ''22:00''',
  'SELECT 1');
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @col2 := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'seller_configs' AND COLUMN_NAME = 'OutboundMode'
);
SET @ddl2 := IF(@col2 = 0,
  'ALTER TABLE `seller_configs` ADD COLUMN `OutboundMode` VARCHAR(32) NOT NULL DEFAULT ''DraftFirst''',
  'SELECT 1');
PREPARE stmt2 FROM @ddl2;
EXECUTE stmt2;
DEALLOCATE PREPARE stmt2;
";
            await db.Database.ExecuteSqlRawAsync(sellerSql);
            logger?.LogInformation("[SchemaPatcher] seller_configs columns ensured (MySQL)");

            const string sessionSql = @"
SET @db := DATABASE();
SET @col := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'chat_sessions' AND COLUMN_NAME = 'PlatformConversationId'
);
SET @ddl := IF(@col = 0,
  'ALTER TABLE `chat_sessions` ADD COLUMN `PlatformConversationId` VARCHAR(191) NULL, ADD COLUMN `PlatformShopOpenId` VARCHAR(128) NULL, ADD COLUMN `LastBuyerMessageAt` DATETIME(6) NULL',
  'SELECT 1');
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
";
            await db.Database.ExecuteSqlRawAsync(sessionSql);
            logger?.LogInformation("[SchemaPatcher] chat_sessions draft-context columns ensured (MySQL)");

            const string handoffSql = @"
SET @db := DATABASE();
SET @col := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'chat_sessions' AND COLUMN_NAME = 'PendingHumanHandoff'
);
SET @ddl := IF(@col = 0,
  'ALTER TABLE `chat_sessions` ADD COLUMN `PendingHumanHandoff` TINYINT(1) NOT NULL DEFAULT 0, ADD COLUMN `HandoffAt` DATETIME(6) NULL',
  'SELECT 1');
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @col2 := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'seller_configs' AND COLUMN_NAME = 'ResponseSlaHours'
);
SET @ddl2 := IF(@col2 = 0,
  'ALTER TABLE `seller_configs` ADD COLUMN `ResponseSlaHours` INT NOT NULL DEFAULT 12, ADD COLUMN `AlertThresholdHours` VARCHAR(64) NOT NULL DEFAULT ''1,3,12''',
  'SELECT 1');
PREPARE stmt2 FROM @ddl2;
EXECUTE stmt2;
DEALLOCATE PREPARE stmt2;
";
            await db.Database.ExecuteSqlRawAsync(handoffSql);
            logger?.LogInformation("[SchemaPatcher] handoff + SLA columns ensured (MySQL)");

            const string autoHandoffSql = @"
SET @db := DATABASE();
SET @col := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'seller_configs' AND COLUMN_NAME = 'AutoHandoffOnLowConfidence'
);
SET @ddl := IF(@col = 0,
  'ALTER TABLE `seller_configs` ADD COLUMN `AutoHandoffOnLowConfidence` TINYINT(1) NOT NULL DEFAULT 1, ADD COLUMN `HandoffConfidenceThreshold` DOUBLE NOT NULL DEFAULT 0.45, ADD COLUMN `SensitiveKeywords` VARCHAR(1024) NOT NULL DEFAULT ''退款,律师,投诉,police,lawyer,refund,lawsuit,举报,报警,法院,诉讼'', ADD COLUMN `HandoffOutsideBusinessHours` TINYINT(1) NOT NULL DEFAULT 1, ADD COLUMN `TimeZoneId` VARCHAR(64) NOT NULL DEFAULT ''Asia/Shanghai''',
  'SELECT 1');
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
";
            await db.Database.ExecuteSqlRawAsync(autoHandoffSql);
            logger?.LogInformation("[SchemaPatcher] auto-handoff + business-hours policy columns ensured (MySQL)");


            const string draftSql = @"
CREATE TABLE IF NOT EXISTS `draft_messages` (
  `Id` binary(16) NOT NULL,
  `ChatSessionId` binary(16) NOT NULL,
  `Content` longtext NOT NULL,
  `Status` varchar(32) NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NULL,
  `SentAt` datetime(6) NULL,
  `SentMessageId` binary(16) NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_draft_messages_ChatSessionId_Status` (`ChatSessionId`, `Status`),
  CONSTRAINT `FK_draft_messages_chat_sessions_ChatSessionId`
    FOREIGN KEY (`ChatSessionId`) REFERENCES `chat_sessions` (`Id`) ON DELETE CASCADE
) CHARACTER SET utf8mb4;
";
            await db.Database.ExecuteSqlRawAsync(draftSql);
            logger?.LogInformation("[SchemaPatcher] draft_messages table ensured (MySQL)");

            const string tokenExpSql = @"
SET @db := DATABASE();
SET @col := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'platform_connections' AND COLUMN_NAME = 'TokenExpiresAt'
);
SET @ddl := IF(@col = 0,
  'ALTER TABLE `platform_connections` ADD COLUMN `TokenExpiresAt` DATETIME(6) NULL',
  'SELECT 1');
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
";
            await db.Database.ExecuteSqlRawAsync(tokenExpSql);
            logger?.LogInformation("[SchemaPatcher] platform_connections.TokenExpiresAt ensured (MySQL)");

            const string webhookEventSql = @"
CREATE TABLE IF NOT EXISTS `processed_webhook_events` (
  `Id` binary(16) NOT NULL,
  `Platform` varchar(32) NOT NULL,
  `EventKey` varchar(191) NOT NULL,
  `ProcessedAt` datetime(6) NOT NULL,
  `IsWeakKey` tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_processed_webhook_events_Platform_EventKey` (`Platform`, `EventKey`)
) CHARACTER SET utf8mb4;
";
            await db.Database.ExecuteSqlRawAsync(webhookEventSql);
            logger?.LogInformation("[SchemaPatcher] processed_webhook_events table ensured (MySQL)");

            const string aiUsageSql = @"
CREATE TABLE IF NOT EXISTS `ai_usage_logs` (
  `Id` binary(16) NOT NULL,
  `SellerId` binary(16) NOT NULL,
  `SessionId` binary(16) NULL,
  `Model` varchar(64) NOT NULL,
  `PromptTokens` int NOT NULL DEFAULT 0,
  `CompletionTokens` int NOT NULL DEFAULT 0,
  `EstimatedCostUsd` decimal(18,6) NOT NULL DEFAULT 0,
  `CreatedAt` datetime(6) NOT NULL,
  `Purpose` varchar(32) NOT NULL,
  `IsEstimated` TINYINT(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`Id`),
  KEY `IX_ai_usage_logs_SellerId_CreatedAt` (`SellerId`, `CreatedAt`),
  KEY `IX_ai_usage_logs_CreatedAt` (`CreatedAt`)
) CHARACTER SET utf8mb4;
";

            await db.Database.ExecuteSqlRawAsync(aiUsageSql);
            logger?.LogInformation("[SchemaPatcher] ai_usage_logs table ensured (MySQL)");

            // IsEstimated 列（幂等）
            const string aiEstimatedSql = @"
SET @db := DATABASE();
SET @col := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'ai_usage_logs' AND COLUMN_NAME = 'IsEstimated'
);
SET @ddl := IF(@col = 0,
  'ALTER TABLE `ai_usage_logs` ADD COLUMN `IsEstimated` TINYINT(1) NOT NULL DEFAULT 0',
  'SELECT 1');
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
";
            await db.Database.ExecuteSqlRawAsync(aiEstimatedSql);
            logger?.LogInformation("[SchemaPatcher] ai_usage_logs.IsEstimated ensured (MySQL)");

            const string auditSql = @"
CREATE TABLE IF NOT EXISTS `audit_logs` (
  `Id` binary(16) NOT NULL,
  `ActorId` binary(16) NULL,
  `ActorType` varchar(32) NOT NULL,
  `Action` varchar(64) NOT NULL,
  `ResourceType` varchar(64) NULL,
  `ResourceId` varchar(64) NULL,
  `DetailJson` longtext NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `ShopId` binary(16) NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_audit_logs_ShopId_CreatedAt` (`ShopId`, `CreatedAt`),
  KEY `IX_audit_logs_CreatedAt` (`CreatedAt`)
) CHARACTER SET utf8mb4;
";
            await db.Database.ExecuteSqlRawAsync(auditSql);
            logger?.LogInformation("[SchemaPatcher] audit_logs table ensured (MySQL)");

            const string refreshErrSql = @"
SET @db := DATABASE();
SET @col := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'platform_connections' AND COLUMN_NAME = 'LastRefreshError'
);
SET @ddl := IF(@col = 0,
  'ALTER TABLE `platform_connections` ADD COLUMN `LastRefreshError` varchar(512) NULL',
  'SELECT 1');
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
SET @col2 := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'platform_connections' AND COLUMN_NAME = 'LastRefreshAt'
);
SET @ddl2 := IF(@col2 = 0,
  'ALTER TABLE `platform_connections` ADD COLUMN `LastRefreshAt` DATETIME(6) NULL',
  'SELECT 1');
PREPARE stmt2 FROM @ddl2;
EXECUTE stmt2;
DEALLOCATE PREPARE stmt2;
";
            await db.Database.ExecuteSqlRawAsync(refreshErrSql);
            logger?.LogInformation("[SchemaPatcher] platform_connections.LastRefreshError/At ensured (MySQL)");

            const string regionSql = @"
SET @db := DATABASE();
SET @col := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'platform_connections' AND COLUMN_NAME = 'Region'
);
SET @ddl := IF(@col = 0,
  'ALTER TABLE `platform_connections` ADD COLUMN `Region` varchar(16) NULL',
  'SELECT 1');
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
";
            await db.Database.ExecuteSqlRawAsync(regionSql);
            logger?.LogInformation("[SchemaPatcher] platform_connections.Region ensured (MySQL)");

            const string settingsSql = @"
CREATE TABLE IF NOT EXISTS `system_settings` (
  `Key` varchar(64) NOT NULL,
  `Value` varchar(512) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Key`)
) CHARACTER SET utf8mb4;
";
            await db.Database.ExecuteSqlRawAsync(settingsSql);
            logger?.LogInformation("[SchemaPatcher] system_settings table ensured (MySQL)");

        }

        private static async Task TrySqliteAsync(AppDbContext db, ILogger? logger)
        {
            foreach (var ddl in new[]
            {
                "ALTER TABLE seller_configs ADD COLUMN EnableAutoReply INTEGER NOT NULL DEFAULT 1",
                "ALTER TABLE seller_configs ADD COLUMN BusinessHoursStart TEXT NOT NULL DEFAULT '09:00'",
                "ALTER TABLE seller_configs ADD COLUMN BusinessHoursEnd TEXT NOT NULL DEFAULT '22:00'",
                "ALTER TABLE seller_configs ADD COLUMN OutboundMode TEXT NOT NULL DEFAULT 'DraftFirst'",
                "ALTER TABLE seller_configs ADD COLUMN ResponseSlaHours INTEGER NOT NULL DEFAULT 12",
                "ALTER TABLE seller_configs ADD COLUMN AlertThresholdHours TEXT NOT NULL DEFAULT '1,3,12'",
                "ALTER TABLE seller_configs ADD COLUMN AutoHandoffOnLowConfidence INTEGER NOT NULL DEFAULT 1",
                "ALTER TABLE seller_configs ADD COLUMN HandoffConfidenceThreshold REAL NOT NULL DEFAULT 0.45",
                "ALTER TABLE seller_configs ADD COLUMN SensitiveKeywords TEXT NOT NULL DEFAULT '退款,律师,投诉,police,lawyer,refund,lawsuit,举报,报警,法院,诉讼'",
                "ALTER TABLE seller_configs ADD COLUMN HandoffOutsideBusinessHours INTEGER NOT NULL DEFAULT 1",
                "ALTER TABLE seller_configs ADD COLUMN TimeZoneId TEXT NOT NULL DEFAULT 'Asia/Shanghai'",
                "ALTER TABLE chat_sessions ADD COLUMN PlatformConversationId TEXT NULL",
                "ALTER TABLE chat_sessions ADD COLUMN PlatformShopOpenId TEXT NULL",
                "ALTER TABLE chat_sessions ADD COLUMN LastBuyerMessageAt TEXT NULL",
                "ALTER TABLE chat_sessions ADD COLUMN PendingHumanHandoff INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE chat_sessions ADD COLUMN HandoffAt TEXT NULL",
                "ALTER TABLE platform_connections ADD COLUMN TokenExpiresAt TEXT NULL",
                "ALTER TABLE platform_connections ADD COLUMN LastRefreshError TEXT NULL",
                "ALTER TABLE platform_connections ADD COLUMN LastRefreshAt TEXT NULL",
                "ALTER TABLE platform_connections ADD COLUMN Region TEXT NULL",
                @"CREATE TABLE IF NOT EXISTS draft_messages (
                    Id BLOB NOT NULL PRIMARY KEY,
                    ChatSessionId BLOB NOT NULL,
                    Content TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NULL,
                    SentAt TEXT NULL,
                    SentMessageId BLOB NULL
                )",
                @"CREATE TABLE IF NOT EXISTS processed_webhook_events (
                    Id BLOB NOT NULL PRIMARY KEY,
                    Platform TEXT NOT NULL,
                    EventKey TEXT NOT NULL,
                    ProcessedAt TEXT NOT NULL,
                    IsWeakKey INTEGER NOT NULL DEFAULT 0
                )",
                @"CREATE TABLE IF NOT EXISTS ai_usage_logs (
                    Id BLOB NOT NULL PRIMARY KEY,
                    SellerId BLOB NOT NULL,
                    SessionId BLOB NULL,
                    Model TEXT NOT NULL,
                    PromptTokens INTEGER NOT NULL DEFAULT 0,
                    CompletionTokens INTEGER NOT NULL DEFAULT 0,
                    EstimatedCostUsd TEXT NOT NULL DEFAULT '0',
                    CreatedAt TEXT NOT NULL,
                    Purpose TEXT NOT NULL,
                    IsEstimated INTEGER NOT NULL DEFAULT 0
                )",
                @"CREATE INDEX IF NOT EXISTS IX_ai_usage_logs_SellerId_CreatedAt ON ai_usage_logs (SellerId, CreatedAt)",
                "ALTER TABLE ai_usage_logs ADD COLUMN IsEstimated INTEGER NOT NULL DEFAULT 0",
                @"CREATE UNIQUE INDEX IF NOT EXISTS IX_processed_webhook_events_Platform_EventKey ON processed_webhook_events (Platform, EventKey)",
                @"CREATE TABLE IF NOT EXISTS audit_logs (
                    Id BLOB NOT NULL PRIMARY KEY,
                    ActorId BLOB NULL,
                    ActorType TEXT NOT NULL,
                    Action TEXT NOT NULL,
                    ResourceType TEXT NULL,
                    ResourceId TEXT NULL,
                    DetailJson TEXT NULL,
                    CreatedAt TEXT NOT NULL,
                    ShopId BLOB NULL
                )",
                @"CREATE INDEX IF NOT EXISTS IX_audit_logs_ShopId_CreatedAt ON audit_logs (ShopId, CreatedAt)",
                @"CREATE INDEX IF NOT EXISTS IX_audit_logs_CreatedAt ON audit_logs (CreatedAt)",
                @"CREATE TABLE IF NOT EXISTS system_settings (
                    ""Key"" TEXT NOT NULL PRIMARY KEY,
                    Value TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                )"

            })
            {
                try
                {
                    await db.Database.ExecuteSqlRawAsync(ddl);
                }
                catch (Exception ex)
                {
                    logger?.LogDebug(ex, "[SchemaPatcher] SQLite ALTER skipped: {Ddl}", ddl);
                }
            }
        }
    }
}
