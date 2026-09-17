-- =============================================================================
-- Synerixis / NexusAI — Full schema reset + demo seed
-- 文件: Migrations/FullReset_nexusai_db.sql
-- 目标库: nexusai_db（用户本地常用名）
-- 注意: Docker Compose 默认库名可能是 synerixis（见 docker-compose.yml / .env.example）
--       请按实际连接串改下面 USE，或先手动 USE 目标库再去掉本脚本的 USE 行。
--
-- 对齐来源（权威）:
--   - Synerixis.Infrastructure/Data/AppDbContext.cs（含 Guid→binary(16)、string→longtext）
--   - Synerixis.Domain/Entities/*
--   - SchemaPatcher.cs 与 Migrations/* 增量列（PlatformMsgId / handoff / SLA 等）
--
-- Guid: 全部 Guid / Guid? 列为 binary(16)（Pomelo Binary16 ≈ UUID_TO_BIN(uuid,0)）
-- 例外: orders.CustomerId / chat_sessions.CustomerId 为 varchar（平台买家字符串 ID）
--       ← 历史 bug: CustomerId 误建成 BINARY 会导致 admin/merchant 字段错误
--
-- 用法（示例）:
--   mysql -h HOST -u USER -p nexusai_db < Migrations/FullReset_nexusai_db.sql
-- 或:
--   mysql -h HOST -u USER -p -e "SOURCE /path/to/FullReset_nexusai_db.sql"
--
-- 演示账号（与 docs/LOCAL_DEMO.md / DevController 一致）:
--   商家手机: 13800138000（库内 +8613800138000），验证码 123456（Dev 固定码，非 PasswordHash）
--   坐席: agent@demo.synerixis.local / Agent123!
--   Admin: admin@test.com / Agent123!
--   密码存 AgentPasswordHasher 格式 sha256:salt:hash（固定 salt，便于 SQL 种子）
-- =============================================================================

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- 若你的库是 Docker 默认 synerixis，请改为: USE `synerixis`;
USE `nexusai_db`;

-- ---------------------------------------------------------------------------
-- 1) DROP（子表 → 父表，FK 安全；IF EXISTS 可重复执行）
-- ---------------------------------------------------------------------------
DROP TABLE IF EXISTS `draft_messages`;
DROP TABLE IF EXISTS `chat_messages`;
DROP TABLE IF EXISTS `agent_stats`;
DROP TABLE IF EXISTS `ai_usage_logs`;
DROP TABLE IF EXISTS `audit_logs`;
DROP TABLE IF EXISTS `processed_webhook_events`;
DROP TABLE IF EXISTS `quick_replies`;
DROP TABLE IF EXISTS `orders`;
DROP TABLE IF EXISTS `chat_sessions`;
DROP TABLE IF EXISTS `agents`;
DROP TABLE IF EXISTS `platform_connections`;
DROP TABLE IF EXISTS `seller_configs`;
DROP TABLE IF EXISTS `seller_products`;
DROP TABLE IF EXISTS `product_attributes`;
DROP TABLE IF EXISTS `skus`;
DROP TABLE IF EXISTS `products`;
DROP TABLE IF EXISTS `categories`;
DROP TABLE IF EXISTS `brands`;
DROP TABLE IF EXISTS `conversations`;
DROP TABLE IF EXISTS `PayOrders`;
DROP TABLE IF EXISTS `system_settings`;
DROP TABLE IF EXISTS `sellers`;

SET FOREIGN_KEY_CHECKS = 1;

-- ---------------------------------------------------------------------------
-- 2) CREATE — 与当前 EF 模型一致（utf8mb4）
-- ---------------------------------------------------------------------------

CREATE TABLE `sellers` (
  `Id` binary(16) NOT NULL,
  `OpenId` varchar(128) NOT NULL,
  `Phone` longtext NULL,
  `Username` longtext NULL,
  `Email` longtext NULL,
  `PasswordHash` longtext NULL,
  `Nickname` longtext NULL,
  `AvatarUrl` longtext NULL,
  `IsActive` tinyint(1) NOT NULL DEFAULT 1,
  `SubscriptionLevel` longtext NOT NULL,
  `FreeQuota` int NULL,
  `SubscriptionEnd` datetime(6) NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `RegisterSource` longtext NULL,
  `LastLoginAt` datetime(6) NULL,
  `LastLoginType` longtext NULL,
  `UpdatedAt` datetime(6) NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_sellers_OpenId` (`OpenId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `seller_configs` (
  `Id` binary(16) NOT NULL,
  `SellerId` binary(16) NULL,
  `ShopName` longtext NOT NULL,
  `ShopLogo` longtext NOT NULL,
  `MainCategory` longtext NOT NULL,
  `TargetCustomerDesc` longtext NOT NULL,
  `DefaultReplyTone` longtext NOT NULL,
  `PreferredLanguage` longtext NOT NULL,
  `EnableAutoMarketingReminder` tinyint(1) NOT NULL DEFAULT 1,
  `MemoryRetentionDays` int NOT NULL DEFAULT 180,
  `EnableAutoReply` tinyint(1) NOT NULL DEFAULT 1,
  `OutboundMode` varchar(32) NOT NULL DEFAULT 'DraftFirst',
  `BusinessHoursStart` varchar(8) NOT NULL DEFAULT '09:00',
  `BusinessHoursEnd` varchar(8) NOT NULL DEFAULT '22:00',
  `ResponseSlaHours` int NOT NULL DEFAULT 12,
  `AlertThresholdHours` longtext NOT NULL,
  `AutoHandoffOnLowConfidence` tinyint(1) NOT NULL DEFAULT 1,
  `HandoffConfidenceThreshold` double NOT NULL DEFAULT 0.45,
  `SensitiveKeywords` longtext NOT NULL,
  `HandoffOutsideBusinessHours` tinyint(1) NOT NULL DEFAULT 1,
  `TimeZoneId` longtext NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_seller_configs_SellerId` (`SellerId`),
  CONSTRAINT `FK_seller_configs_sellers_SellerId`
    FOREIGN KEY (`SellerId`) REFERENCES `sellers` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `brands` (
  `Id` binary(16) NOT NULL,
  `Name` longtext NOT NULL,
  `Logo` longtext NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `categories` (
  `Id` binary(16) NOT NULL,
  `Name` longtext NOT NULL,
  `ParentId` binary(16) NULL,
  `IsLeaf` tinyint(1) NOT NULL DEFAULT 1,
  `SortOrder` int NOT NULL DEFAULT 0,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `products` (
  `Id` binary(16) NOT NULL,
  `ExternalId` longtext NULL,
  `ExternalSource` longtext NULL,
  `Title` longtext NULL,
  `Description` longtext NULL,
  `Price` decimal(65,30) NULL,
  `ImagesJson` longtext NULL,
  `VideosJson` longtext NULL,
  `TagsJson` longtext NULL,
  `CategoryId` binary(16) NOT NULL,
  `BrandId` binary(16) NOT NULL,
  `AttributesJson` longtext NULL,
  `Status` int NOT NULL DEFAULT 1,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_products_CategoryId` (`CategoryId`),
  KEY `IX_products_BrandId` (`BrandId`),
  CONSTRAINT `FK_products_categories_CategoryId`
    FOREIGN KEY (`CategoryId`) REFERENCES `categories` (`Id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_products_brands_BrandId`
    FOREIGN KEY (`BrandId`) REFERENCES `brands` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `product_attributes` (
  `Id` binary(16) NOT NULL,
  `ProductId` binary(16) NOT NULL,
  `Name` longtext NOT NULL,
  `Value` longtext NOT NULL,
  `IsKey` tinyint(1) NOT NULL DEFAULT 0,
  `IsSale` tinyint(1) NOT NULL DEFAULT 0,
  `ParentId` binary(16) NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_product_attributes_ProductId` (`ProductId`),
  CONSTRAINT `FK_product_attributes_products_ProductId`
    FOREIGN KEY (`ProductId`) REFERENCES `products` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `skus` (
  `Id` binary(16) NOT NULL,
  `ProductId` binary(16) NOT NULL,
  `SpecsJson` longtext NOT NULL,
  `Price` decimal(65,30) NOT NULL,
  `Stock` int NOT NULL,
  `ExternalCode` longtext NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_skus_ProductId` (`ProductId`),
  CONSTRAINT `FK_skus_products_ProductId`
    FOREIGN KEY (`ProductId`) REFERENCES `products` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `seller_products` (
  `Id` binary(16) NOT NULL,
  `SellerId` binary(16) NOT NULL,
  `ProductId` binary(16) NOT NULL,
  `CustomPrice` decimal(65,30) NULL,
  `CustomStock` int NULL,
  `ImportedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `Source` longtext NULL,
  `OptimizedTitle` longtext NULL,
  `OptimizedDescription` longtext NULL,
  `OptimizedTagsJson` longtext NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_seller_products_SellerId` (`SellerId`),
  KEY `IX_seller_products_ProductId` (`ProductId`),
  CONSTRAINT `FK_seller_products_sellers_SellerId`
    FOREIGN KEY (`SellerId`) REFERENCES `sellers` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_seller_products_products_ProductId`
    FOREIGN KEY (`ProductId`) REFERENCES `products` (`Id`) ON DELETE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `conversations` (
  `Id` binary(16) NOT NULL,
  `Title` longtext NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `LastActiveAt` datetime(6) NULL,
  `LastViewedAt` datetime(6) NULL,
  `Platform` longtext NOT NULL,
  `SellerId` binary(16) NOT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`Id`),
  KEY `IX_conversations_SellerId` (`SellerId`),
  CONSTRAINT `FK_conversations_sellers_SellerId`
    FOREIGN KEY (`SellerId`) REFERENCES `sellers` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- EF 未 ToTable 覆盖 → 默认表名 PayOrders
CREATE TABLE `PayOrders` (
  `Id` binary(16) NOT NULL,
  `SellerId` binary(16) NOT NULL,
  `OutTradeNo` longtext NOT NULL,
  `Amount` decimal(65,30) NOT NULL,
  `Status` longtext NOT NULL,
  `TransactionId` longtext NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `PaidAt` datetime(6) NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `Channel` longtext NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `platform_connections` (
  `Id` binary(16) NOT NULL,
  `SellerId` binary(16) NOT NULL,
  `Platform` varchar(50) NOT NULL,
  `AppKey` longtext NOT NULL,
  `AccessToken` longtext NOT NULL,
  `RefreshToken` varchar(512) NULL,
  `OpenId` varchar(128) NOT NULL,
  `ShopId` varchar(128) NULL,
  `Nickname` varchar(128) NULL,
  `AvatarUrl` varchar(512) NULL,
  `IsActive` bit(1) NOT NULL DEFAULT b'1',
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NULL,
  `TokenExpiresAt` datetime(6) NULL,
  `LastRefreshAt` datetime(6) NULL,
  `LastRefreshError` varchar(512) NULL,
  `Region` varchar(16) NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_platform_connections_SellerId` (`SellerId`),
  CONSTRAINT `FK_platform_connections_sellers_SellerId`
    FOREIGN KEY (`SellerId`) REFERENCES `sellers` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `agents` (
  `Id` binary(16) NOT NULL,
  `Email` varchar(255) NOT NULL,
  `Phone` longtext NULL,
  `PasswordHash` longtext NOT NULL,
  `Name` longtext NOT NULL,
  `AvatarUrl` longtext NULL,
  `Role` int NOT NULL,
  `IsActive` tinyint(1) NOT NULL DEFAULT 1,
  `IsOnline` tinyint(1) NOT NULL DEFAULT 0,
  `MaxConcurrentSessions` int NOT NULL DEFAULT 5,
  `LastLoginAt` datetime(6) NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NULL,
  `ShopId` binary(16) NOT NULL,
  `CurrentSessionCount` int NOT NULL DEFAULT 0,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_agents_Email` (`Email`),
  KEY `IX_agents_ShopId` (`ShopId`),
  CONSTRAINT `FK_agents_sellers_ShopId`
    FOREIGN KEY (`ShopId`) REFERENCES `sellers` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `chat_sessions` (
  `Id` binary(16) NOT NULL,
  `SessionId` varchar(100) NOT NULL,
  `CustomerId` varchar(100) NOT NULL,
  `CustomerName` longtext NULL,
  `CustomerAvatar` longtext NULL,
  `ShopId` binary(16) NOT NULL,
  `Platform` longtext NOT NULL,
  `Status` int NOT NULL,
  `Priority` int NOT NULL,
  `AssignedAgentId` binary(16) NULL,
  `AssignedAt` datetime(6) NULL,
  `ResolvedAt` datetime(6) NULL,
  `Satisfaction` tinyint unsigned NULL,
  `MessageCount` int NOT NULL DEFAULT 0,
  `AiMessageCount` int NOT NULL DEFAULT 0,
  `AgentMessageCount` int NOT NULL DEFAULT 0,
  `ResponseTime` time(6) NULL,
  `ResolutionTime` time(6) NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `LastActiveAt` datetime(6) NULL,
  `UpdatedAt` datetime(6) NULL,
  `PlatformConversationId` varchar(191) NULL,
  `PlatformShopOpenId` varchar(128) NULL,
  `LastBuyerMessageAt` datetime(6) NULL,
  `PendingHumanHandoff` tinyint(1) NOT NULL DEFAULT 0,
  `HandoffAt` datetime(6) NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_chat_sessions_SessionId` (`SessionId`),
  KEY `IX_chat_sessions_CustomerId` (`CustomerId`),
  KEY `IX_chat_sessions_ShopId` (`ShopId`),
  KEY `IX_chat_sessions_AssignedAgentId` (`AssignedAgentId`),
  CONSTRAINT `FK_chat_sessions_sellers_ShopId`
    FOREIGN KEY (`ShopId`) REFERENCES `sellers` (`Id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_chat_sessions_agents_AssignedAgentId`
    FOREIGN KEY (`AssignedAgentId`) REFERENCES `agents` (`Id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `chat_messages` (
  `Id` binary(16) NOT NULL,
  `ChatSessionId` binary(16) NOT NULL,
  `SenderType` int NOT NULL,
  `SenderId` binary(16) NULL,
  `Content` longtext NOT NULL,
  `MessageType` int NOT NULL DEFAULT 1,
  `Metadata` longtext NULL,
  `PlatformMsgId` longtext NULL,
  `IsRead` tinyint(1) NOT NULL DEFAULT 0,
  `ReadAt` datetime(6) NULL,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_chat_messages_ChatSessionId` (`ChatSessionId`),
  CONSTRAINT `FK_chat_messages_chat_sessions_ChatSessionId`
    FOREIGN KEY (`ChatSessionId`) REFERENCES `chat_sessions` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `draft_messages` (
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
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `orders` (
  `Id` binary(16) NOT NULL,
  `OrderNo` varchar(100) NOT NULL,
  `ExternalOrderId` longtext NULL,
  `ShopId` binary(16) NOT NULL,
  `CustomerId` varchar(100) NOT NULL,
  `CustomerName` longtext NULL,
  `CustomerPhone` longtext NULL,
  `Status` longtext NOT NULL,
  `TotalAmount` decimal(65,30) NOT NULL,
  `PaymentAmount` decimal(65,30) NULL,
  `RefundAmount` decimal(65,30) NULL,
  `LogisticsNo` longtext NULL,
  `LogisticsCompany` longtext NULL,
  `ShippingAddress` longtext NULL,
  `OrderTime` datetime(6) NOT NULL,
  `PaidAt` datetime(6) NULL,
  `ShippedAt` datetime(6) NULL,
  `ReceivedAt` datetime(6) NULL,
  `CompletedAt` datetime(6) NULL,
  `SyncedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NULL,
  `Platform` longtext NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_orders_OrderNo` (`OrderNo`),
  KEY `IX_orders_ShopId_CustomerId` (`ShopId`, `CustomerId`),
  CONSTRAINT `FK_orders_sellers_ShopId`
    FOREIGN KEY (`ShopId`) REFERENCES `sellers` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `quick_replies` (
  `Id` binary(16) NOT NULL,
  `Title` varchar(200) NOT NULL,
  `Content` longtext NOT NULL,
  `Category` int NOT NULL,
  `Keywords` longtext NULL,
  `Scope` int NOT NULL,
  `ShopId` binary(16) NULL,
  `IsActive` tinyint(1) NOT NULL DEFAULT 1,
  `SortOrder` int NOT NULL DEFAULT 0,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_quick_replies_ShopId` (`ShopId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `agent_stats` (
  `Id` binary(16) NOT NULL,
  `AgentId` binary(16) NOT NULL,
  `StatDate` datetime(6) NOT NULL,
  `TotalConversations` int NOT NULL DEFAULT 0,
  `PendingCount` int NOT NULL DEFAULT 0,
  `ActiveCount` int NOT NULL DEFAULT 0,
  `ResolvedCount` int NOT NULL DEFAULT 0,
  `ClosedCount` int NOT NULL DEFAULT 0,
  `TotalResponseTimeSeconds` int NOT NULL DEFAULT 0,
  `AvgResponseTimeSeconds` double NOT NULL DEFAULT 0,
  `AvgFirstResponseTimeSeconds` int NULL,
  `TotalMessages` int NOT NULL DEFAULT 0,
  `AgentMessages` int NOT NULL DEFAULT 0,
  `AiMessages` int NOT NULL DEFAULT 0,
  `SatisfactionCount` int NULL,
  `AvgSatisfaction` double NULL,
  `ResolutionRate` double NOT NULL DEFAULT 0,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_agent_stats_Agent_Date` (`AgentId`, `StatDate`),
  CONSTRAINT `FK_agent_stats_agents_AgentId`
    FOREIGN KEY (`AgentId`) REFERENCES `agents` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `processed_webhook_events` (
  `Id` binary(16) NOT NULL,
  `Platform` varchar(32) NOT NULL,
  `EventKey` varchar(191) NOT NULL,
  `ProcessedAt` datetime(6) NOT NULL,
  `IsWeakKey` tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_processed_webhook_events_Platform_EventKey` (`Platform`, `EventKey`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `ai_usage_logs` (
  `Id` binary(16) NOT NULL,
  `SellerId` binary(16) NOT NULL,
  `SessionId` binary(16) NULL,
  `Model` varchar(64) NOT NULL,
  `PromptTokens` int NOT NULL DEFAULT 0,
  `CompletionTokens` int NOT NULL DEFAULT 0,
  `EstimatedCostUsd` decimal(18,6) NOT NULL DEFAULT 0.000000,
  `CreatedAt` datetime(6) NOT NULL,
  `Purpose` varchar(32) NOT NULL,
  `IsEstimated` tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`Id`),
  KEY `IX_ai_usage_logs_SellerId_CreatedAt` (`SellerId`, `CreatedAt`),
  KEY `IX_ai_usage_logs_CreatedAt` (`CreatedAt`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `audit_logs` (
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
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `system_settings` (
  `Key` varchar(64) NOT NULL,
  `Value` varchar(512) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------------
-- 3) SEED — 固定 Guid（便于文档/排查）；密码 = AgentPasswordHasher("Agent123!")
--    salt=a1b2c3d4e5f60718293a4b5c6d7e8f90
--    hash=SHA256(salt+password) hex lower
-- ---------------------------------------------------------------------------

SET @seller_id  = UUID_TO_BIN('a1000000-0000-4000-8000-000000000001', 0);
SET @config_id  = UUID_TO_BIN('a1000000-0000-4000-8000-000000000002', 0);
SET @conn_id    = UUID_TO_BIN('a1000000-0000-4000-8000-000000000003', 0);
SET @agent_id   = UUID_TO_BIN('a1000000-0000-4000-8000-000000000010', 0);
SET @admin_id   = UUID_TO_BIN('a1000000-0000-4000-8000-000000000011', 0);
SET @sess_draft = UUID_TO_BIN('a1000000-0000-4000-8000-000000000020', 0);
SET @sess_hand  = UUID_TO_BIN('a1000000-0000-4000-8000-000000000021', 0);
SET @sess_norm  = UUID_TO_BIN('a1000000-0000-4000-8000-000000000022', 0);
SET @msg_d1     = UUID_TO_BIN('a1000000-0000-4000-8000-000000000030', 0);
SET @msg_h1     = UUID_TO_BIN('a1000000-0000-4000-8000-000000000031', 0);
SET @msg_h2     = UUID_TO_BIN('a1000000-0000-4000-8000-000000000032', 0);
SET @msg_n1     = UUID_TO_BIN('a1000000-0000-4000-8000-000000000033', 0);
SET @msg_n2     = UUID_TO_BIN('a1000000-0000-4000-8000-000000000034', 0);
SET @draft_id   = UUID_TO_BIN('a1000000-0000-4000-8000-000000000040', 0);
SET @ord1_id    = UUID_TO_BIN('a1000000-0000-4000-8000-000000000050', 0);
SET @ord2_id    = UUID_TO_BIN('a1000000-0000-4000-8000-000000000051', 0);
SET @qr1_id     = UUID_TO_BIN('a1000000-0000-4000-8000-000000000060', 0);
SET @qr2_id     = UUID_TO_BIN('a1000000-0000-4000-8000-000000000061', 0);
SET @qr3_id     = UUID_TO_BIN('a1000000-0000-4000-8000-000000000062', 0);
SET @usage1_id  = UUID_TO_BIN('a1000000-0000-4000-8000-000000000070', 0);
SET @usage2_id  = UUID_TO_BIN('a1000000-0000-4000-8000-000000000071', 0);
SET @audit_id   = UUID_TO_BIN('a1000000-0000-4000-8000-000000000080', 0);

SET @now = UTC_TIMESTAMP(6);
SET @pwd = 'sha256:a1b2c3d4e5f60718293a4b5c6d7e8f90:e54974b55b91388cf047ed958945c21a72d41fbfba27830dc61bd92dc77e2122';
-- SIM shop id: left("SIM-SHOP-" + sellerGuidN, 32)
SET @sim_shop = 'SIM-SHOP-a1000000000040008000';

INSERT INTO `sellers` (
  `Id`, `OpenId`, `Phone`, `Nickname`, `IsActive`, `SubscriptionLevel`, `FreeQuota`,
  `CreatedAt`, `RegisterSource`, `UpdatedAt`
) VALUES (
  @seller_id,
  'phone_a1000000000040008000000000000001',
  '+8613800138000',
  '演示商家 Synerixis',
  1,
  'trial',
  100,
  @now,
  'phone',
  @now
);

INSERT INTO `seller_configs` (
  `Id`, `SellerId`, `ShopName`, `ShopLogo`, `MainCategory`, `TargetCustomerDesc`,
  `DefaultReplyTone`, `PreferredLanguage`, `EnableAutoMarketingReminder`, `MemoryRetentionDays`,
  `EnableAutoReply`, `OutboundMode`, `BusinessHoursStart`, `BusinessHoursEnd`,
  `ResponseSlaHours`, `AlertThresholdHours`,
  `AutoHandoffOnLowConfidence`, `HandoffConfidenceThreshold`, `SensitiveKeywords`,
  `HandoffOutsideBusinessHours`, `TimeZoneId`, `CreatedAt`, `UpdatedAt`
) VALUES (
  @config_id, @seller_id, '演示商家 Synerixis', '', '跨境电商', '本地演示买家',
  'professional', 'zh', 1, 180,
  1, 'DraftFirst', '09:00', '22:00',
  12, '1,3,12',
  1, 0.45, '退款,律师,投诉,police,lawyer,refund,lawsuit,举报,报警,法院,诉讼',
  1, 'Asia/Shanghai', @now, DATE_ADD(@now, INTERVAL 5 SECOND)
);

INSERT INTO `platform_connections` (
  `Id`, `SellerId`, `Platform`, `AppKey`, `AccessToken`, `RefreshToken`,
  `OpenId`, `ShopId`, `Nickname`, `IsActive`, `CreatedAt`, `UpdatedAt`,
  `TokenExpiresAt`, `LastRefreshAt`, `LastRefreshError`, `Region`
) VALUES (
  @conn_id, @seller_id, 'SHOPEE', 'SIM-DEV',
  'sim-access-token-not-for-production', 'sim-refresh-token',
  @sim_shop, @sim_shop, '模拟 Shopee 店', b'1', @now, @now,
  DATE_ADD(@now, INTERVAL 10 YEAR), @now, NULL, 'SG'
);

INSERT INTO `agents` (
  `Id`, `Email`, `PasswordHash`, `Name`, `Role`, `IsActive`, `IsOnline`,
  `MaxConcurrentSessions`, `CreatedAt`, `ShopId`, `CurrentSessionCount`
) VALUES
(
  @agent_id, 'agent@demo.synerixis.local', @pwd, '演示坐席小美',
  1, 1, 0, 5, @now, @seller_id, 0
),
(
  @admin_id, 'admin@test.com', @pwd, 'Admin Agent',
  3, 1, 0, 5, @now, @seller_id, 0
);

INSERT INTO `chat_sessions` (
  `Id`, `SessionId`, `CustomerId`, `CustomerName`, `ShopId`, `Platform`,
  `Status`, `Priority`, `AssignedAgentId`, `AssignedAt`,
  `MessageCount`, `AiMessageCount`, `AgentMessageCount`,
  `CreatedAt`, `LastActiveAt`, `UpdatedAt`,
  `PlatformConversationId`, `PlatformShopOpenId`, `LastBuyerMessageAt`,
  `PendingHumanHandoff`, `HandoffAt`
) VALUES
(
  @sess_draft, 'CS-DEMO-DRAFT-0001', 'demo-buyer-draft', '演示买家·待审草稿',
  @seller_id, 'SHOPEE', 1, 1, NULL, NULL,
  2, 1, 0, @now, @now, @now,
  'demo-conv-demo-buyer-draft', @sim_shop, @now, 0, NULL
),
(
  @sess_hand, 'CS-DEMO-HANDOFF-0001', 'demo-buyer-handoff', '演示买家·转人工',
  @seller_id, 'SHOPEE', 1, 1, NULL, NULL,
  2, 1, 0, @now, @now, @now,
  'demo-conv-demo-buyer-handoff', @sim_shop, @now, 1, @now
),
(
  @sess_norm, 'CS-DEMO-NORMAL-0001', 'demo-buyer-normal', '演示买家·正常咨询',
  @seller_id, 'SHOPEE', 2, 1, @agent_id, @now,
  2, 0, 1, @now, @now, @now,
  'demo-conv-demo-buyer-normal', @sim_shop, @now, 0, NULL
);

INSERT INTO `chat_messages` (
  `Id`, `ChatSessionId`, `SenderType`, `SenderId`, `Content`, `MessageType`,
  `PlatformMsgId`, `IsRead`, `CreatedAt`
) VALUES
(
  @msg_d1, @sess_draft, 1, NULL, '你好，请问这款有货吗？多久能发货？', 1,
  'demo-pm-draft-1', 0, @now
),
(
  @msg_h1, @sess_hand, 1, NULL, '我要退款！已经投诉了，请马上处理！', 1,
  'demo-pm-handoff-1', 0, @now
),
(
  @msg_h2, @sess_hand, 3, NULL, '已触发转人工（演示）：敏感意图/投诉，请坐席接手。', 1,
  NULL, 0, DATE_ADD(@now, INTERVAL 1 SECOND)
),
(
  @msg_n1, @sess_norm, 1, NULL, '订单什么时候到？单号发我一下谢谢。', 1,
  'demo-pm-normal-1', 0, @now
),
(
  @msg_n2, @sess_norm, 2, @agent_id,
  '您好，您的订单 DEMO-ORD-002 已支付，快递单号 YT9876543210123（圆通），预计 2–4 日送达。',
  1, NULL, 0, DATE_ADD(@now, INTERVAL 2 SECOND)
);

INSERT INTO `draft_messages` (
  `Id`, `ChatSessionId`, `Content`, `Status`, `CreatedAt`
) VALUES (
  @draft_id, @sess_draft,
  '您好！该款现货充足，一般付款后 24 小时内发出（演示草稿，请人审后发送）。如需指定物流可告知。',
  'Pending', @now
);

INSERT INTO `orders` (
  `Id`, `OrderNo`, `ExternalOrderId`, `ShopId`, `CustomerId`, `CustomerName`,
  `Status`, `TotalAmount`, `PaymentAmount`, `LogisticsNo`, `LogisticsCompany`,
  `ShippingAddress`, `OrderTime`, `PaidAt`, `ShippedAt`, `SyncedAt`, `UpdatedAt`, `Platform`
) VALUES
(
  @ord1_id, 'DEMO-ORD-001', 'EXT-DEMO-001', @seller_id, 'demo-buyer-draft', '演示买家·待审草稿',
  'Shipped', 129.900000000000000000000000000000, 129.900000000000000000000000000000,
  'SF1432887654321', '顺丰速运', '演示地址·上海市', @now, @now, @now, @now, @now, 'SHOPEE'
),
(
  @ord2_id, 'DEMO-ORD-002', 'EXT-DEMO-002', @seller_id, 'demo-buyer-normal', '演示买家·正常咨询',
  'Paid', 59.000000000000000000000000000000, 59.000000000000000000000000000000,
  'YT9876543210123', '圆通速递', '演示地址·杭州市', @now, @now, NULL, @now, @now, 'SHOPEE'
);

INSERT INTO `quick_replies` (
  `Id`, `Title`, `Content`, `Category`, `Keywords`, `Scope`, `ShopId`, `IsActive`, `SortOrder`, `CreatedAt`, `UpdatedAt`
) VALUES
(
  @qr1_id, '[Demo] 欢迎语', '您好，欢迎光临演示店！请问有什么可以帮您？',
  2, '演示,demo', 2, @seller_id, 1, 0, @now, @now
),
(
  @qr2_id, '[Demo] 查物流', '您的订单已发出，快递单号可在会话右侧订单卡片查看，如需催件请告知。',
  4, '演示,demo', 2, @seller_id, 1, 1, @now, @now
),
(
  @qr3_id, '[Demo] 转人工说明', '已为您转接人工坐席，请稍候，我们会尽快回复。',
  3, '演示,demo', 2, @seller_id, 1, 2, @now, @now
);

INSERT INTO `ai_usage_logs` (
  `Id`, `SellerId`, `SessionId`, `Model`, `PromptTokens`, `CompletionTokens`,
  `EstimatedCostUsd`, `CreatedAt`, `Purpose`, `IsEstimated`
) VALUES
(
  @usage1_id, @seller_id, @sess_draft, 'qwen-max', 320, 180,
  0.002000, DATE_SUB(@now, INTERVAL 30 MINUTE), 'draft', 1
),
(
  @usage2_id, @seller_id, @sess_norm, 'qwen-max', 210, 95,
  0.001000, DATE_SUB(@now, INTERVAL 10 MINUTE), 'classify', 1
);

INSERT INTO `audit_logs` (
  `Id`, `ActorId`, `ActorType`, `Action`, `ResourceType`, `ResourceId`, `DetailJson`, `CreatedAt`, `ShopId`
) VALUES (
  @audit_id, @admin_id, 'Admin', 'admin.login', 'Agent',
  'a1000000-0000-4000-8000-000000000011',
  '{"source":"FullReset_nexusai_db.sql"}', @now, @seller_id
);

INSERT INTO `system_settings` (`Key`, `Value`, `UpdatedAt`) VALUES
('MaintenanceMode', 'false', @now),
('DefaultOutboundMode', 'DraftFirst', @now),
('AllowNewRegistration', 'true', @now);

-- ---------------------------------------------------------------------------
-- 4) 快速校验（可选）
-- ---------------------------------------------------------------------------
-- SELECT COUNT(*) AS table_count FROM information_schema.tables
--   WHERE table_schema = DATABASE() AND table_type = 'BASE TABLE';
-- 期望 22 张表，对应 AppDbContext 22 个 DbSet。

SELECT 'FullReset_nexusai_db complete' AS status,
       (SELECT COUNT(*) FROM sellers) AS sellers,
       (SELECT COUNT(*) FROM agents) AS agents,
       (SELECT COUNT(*) FROM chat_sessions) AS sessions,
       (SELECT COUNT(*) FROM draft_messages) AS drafts,
       (SELECT COUNT(*) FROM orders) AS orders;
