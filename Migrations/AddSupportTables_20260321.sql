-- =====================================================
-- NexusAI Tech - Support Tables Migration
-- Version: 1.0
-- Date: 2026-03-21
-- Description: 新增客服工作台、会话管理、订单缓存、快捷回复、绩效统计相关表
-- =====================================================

-- 注意：当前数据库使用 MySQL/MariaDB，Guid 存储为 binary(16)
-- 所有 Guid 类型字段在数据库中为 BINARY(16)

-- =====================================================
-- Table: agents (客服账号)
-- =====================================================
CREATE TABLE IF NOT EXISTS `agents` (
  `Id` BINARY(16) NOT NULL,
  `Email` VARCHAR(255) NOT NULL,
  `Phone` VARCHAR(50) NULL,
  `PasswordHash` VARCHAR(255) NOT NULL,
  `Name` VARCHAR(100) NOT NULL,
  `AvatarUrl` VARCHAR(500) NULL,
  `Role` INT NOT NULL DEFAULT 1 COMMENT '1=Agent, 2=Supervisor, 3=Admin',
  `IsActive` BOOLEAN NOT NULL DEFAULT TRUE,
  `IsOnline` BOOLEAN NOT NULL DEFAULT FALSE,
  `MaxConcurrentSessions` INT NOT NULL DEFAULT 5,
  `LastLoginAt` DATETIME NULL,
  `CreatedAt` DATETIME NOT NULL,
  `UpdatedAt` DATETIME NULL,
  `ShopId` BINARY(16) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_agents_Email` (`Email`),
  KEY `IX_agents_ShopId` (`ShopId`),
  CONSTRAINT `FK_agents_shops` FOREIGN KEY (`ShopId`) REFERENCES `sellers` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- Table: chat_sessions (客服会话状态)
-- =====================================================
CREATE TABLE IF NOT EXISTS `chat_sessions` (
  `Id` BINARY(16) NOT NULL,
  `SessionId` VARCHAR(100) NOT NULL,
  `CustomerId` VARCHAR(100) NOT NULL,
  `CustomerName` VARCHAR(200) NULL,
  `CustomerAvatar` VARCHAR(500) NULL,
  `ShopId` BINARY(16) NOT NULL,
  `Platform` VARCHAR(50) NOT NULL COMMENT 'TAOBAO, JD, DOUYIN, OTHER',
  `Status` INT NOT NULL DEFAULT 1 COMMENT '1=Pending, 2=Active, 3=Resolved, 4=Closed',
  `Priority` INT NOT NULL DEFAULT 1 COMMENT '1=Normal, 2=High, 3=Urgent',
  `AssignedAgentId` BINARY(16) NULL,
  `AssignedAt` DATETIME NULL,
  `ResolvedAt` DATETIME NULL,
  `Satisfaction` TINYINT NULL COMMENT '1-5 star rating',
  `MessageCount` INT NOT NULL DEFAULT 0,
  `AiMessageCount` INT NOT NULL DEFAULT 0,
  `AgentMessageCount` INT NOT NULL DEFAULT 0,
  `ResponseTime` INT NULL COMMENT 'Seconds',
  `ResolutionTime` INT NULL COMMENT 'Seconds',
  `CreatedAt` DATETIME NOT NULL,
  `LastActiveAt` DATETIME NULL,
  `UpdatedAt` DATETIME NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_chat_sessions_SessionId` (`SessionId`),
  KEY `IX_chat_sessions_CustomerId` (`CustomerId`),
  KEY `IX_chat_sessions_ShopId` (`ShopId`),
  KEY `IX_chat_sessions_AssignedAgentId` (`AssignedAgentId`),
  KEY `IX_chat_sessions_Status` (`Status`),
  CONSTRAINT `FK_chat_sessions_shops` FOREIGN KEY (`ShopId`) REFERENCES `sellers` (`Id`) ON DELETE RESTRICT,
  CONSTRAINT `FK_chat_sessions_agents` FOREIGN KEY (`AssignedAgentId`) REFERENCES `agents` (`Id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- Table: orders (订单缓存)
-- =====================================================
CREATE TABLE IF NOT EXISTS `orders` (
  `Id` BINARY(16) NOT NULL,
  `OrderNo` VARCHAR(100) NOT NULL,
  `ExternalOrderId` VARCHAR(100) NULL,
  `ShopId` BINARY(16) NOT NULL,
  `CustomerId` BINARY(16) NOT NULL,
  `CustomerName` VARCHAR(200) NULL,
  `CustomerPhone` VARCHAR(50) NULL,
  `Status` VARCHAR(50) NOT NULL DEFAULT 'PendingPayment',
  `TotalAmount` DECIMAL(10,2) NOT NULL DEFAULT 0.00,
  `PaymentAmount` DECIMAL(10,2) NULL,
  `RefundAmount` DECIMAL(10,2) NULL,
  `LogisticsNo` VARCHAR(100) NULL,
  `LogisticsCompany` VARCHAR(100) NULL,
  `ShippingAddress` TEXT NULL,
  `OrderTime` DATETIME NOT NULL,
  `PaidAt` DATETIME NULL,
  `ShippedAt` DATETIME NULL,
  `ReceivedAt` DATETIME NULL,
  `CompletedAt` DATETIME NULL,
  `SyncedAt` DATETIME NOT NULL,
  `UpdatedAt` DATETIME NULL,
  `Note` TEXT NULL,
  `Platform` VARCHAR(50) NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_orders_OrderNo` (`OrderNo`),
  KEY `IX_orders_ShopId` (`ShopId`),
  KEY `IX_orders_CustomerId` (`CustomerId`),
  KEY `IX_orders_ShopId_CustomerId` (`ShopId`, `CustomerId`),
  CONSTRAINT `FK_orders_shops` FOREIGN KEY (`ShopId`) REFERENCES `sellers` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- Table: quick_replies (快捷回复模板)
-- =====================================================
CREATE TABLE IF NOT EXISTS `quick_replies` (
  `Id` BINARY(16) NOT NULL,
  `Title` VARCHAR(200) NOT NULL,
  `Content` LONGTEXT NOT NULL,
  `Category` INT NOT NULL DEFAULT 1 COMMENT '1=General, 2=PreSale, 3=AfterSale, 4=Logistics, 5=Complaint, 6=Payment',
  `Scope` INT NOT NULL DEFAULT 1 COMMENT '1=Global, 2=Shop',
  `ShopId` BINARY(16) NULL,
  `Keywords` TEXT NULL COMMENT 'Comma-separated trigger keywords',
  `IsActive` BOOLEAN NOT NULL DEFAULT TRUE,
  `SortOrder` INT NOT NULL DEFAULT 0,
  `CreatedAt` DATETIME NOT NULL,
  `UpdatedAt` DATETIME NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_quick_replies_ShopId` (`ShopId`),
  KEY `IX_quick_replies_Category` (`Category`),
  KEY `IX_quick_replies_Scope` (`Scope`),
  CONSTRAINT `FK_quick_replies_shops` FOREIGN KEY (`ShopId`) REFERENCES `sellers` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- Table: chat_messages (客服消息记录)
-- =====================================================
CREATE TABLE IF NOT EXISTS `chat_messages` (
  `Id` BINARY(16) NOT NULL,
  `ChatSessionId` BINARY(16) NOT NULL,
  `SenderType` INT NOT NULL COMMENT '1=Customer, 2=Agent, 3=System',
  `SenderId` BINARY(16) NULL COMMENT 'AgentId if SenderType=2',
  `Content` LONGTEXT NOT NULL,
  `MessageType` INT NOT NULL DEFAULT 1 COMMENT '1=Text, 2=Image, 3=File, 4=OrderQuery, 5=LogisticsQuery',
  `Metadata` TEXT NULL COMMENT 'JSON payload for special message types',
  `IsRead` BOOLEAN NOT NULL DEFAULT FALSE,
  `ReadAt` DATETIME NULL,
  `CreatedAt` DATETIME NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_chat_messages_ChatSessionId` (`ChatSessionId`),
  KEY `IX_chat_messages_CreatedAt` (`CreatedAt`),
  CONSTRAINT `FK_chat_messages_chat_sessions` FOREIGN KEY (`ChatSessionId`) REFERENCES `chat_sessions` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- Table: agent_stats (客服绩效统计)
-- =====================================================
CREATE TABLE IF NOT EXISTS `agent_stats` (
  `Id` BINARY(16) NOT NULL,
  `AgentId` BINARY(16) NOT NULL,
  `StatDate` DATE NOT NULL,
  `TotalConversations` INT NOT NULL DEFAULT 0,
  `PendingCount` INT NOT NULL DEFAULT 0,
  `ActiveCount` INT NOT NULL DEFAULT 0,
  `ResolvedCount` INT NOT NULL DEFAULT 0,
  `ClosedCount` INT NOT NULL DEFAULT 0,
  `TotalResponseTimeSeconds` INT NOT NULL DEFAULT 0,
  `AvgResponseTimeSeconds` DOUBLE NOT NULL DEFAULT 0,
  `AvgFirstResponseTimeSeconds` INT NULL,
  `TotalMessages` INT NOT NULL DEFAULT 0,
  `AgentMessages` INT NOT NULL DEFAULT 0,
  `AiMessages` INT NOT NULL DEFAULT 0,
  `SatisfactionCount` INT NULL DEFAULT 0,
  `AvgSatisfaction` DOUBLE NULL,
  `ResolutionRate` DOUBLE NOT NULL DEFAULT 0,
  `CreatedAt` DATETIME NOT NULL,
  `UpdatedAt` DATETIME NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_agent_stats_Agent_Date` (`AgentId`, `StatDate`),
  CONSTRAINT `FK_agent_stats_agents` FOREIGN KEY (`AgentId`) REFERENCES `agents` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- Indexes for existing tables (if needed)
-- =====================================================

-- Add index for ChatMessages to filter by ConversationId faster (already has PK on Id)
-- But maybe we need an index on Timestamp for sorting:
-- CREATE INDEX IF NOT EXISTS `IX_chat_messages_ConversationId_Timestamp` ON `chat_messages` (`ConversationId`, `Timestamp`);

-- =====================================================
-- Sample Data (Optional - for testing)
-- =====================================================

-- Insert a default global quick reply (for testing)
-- INSERT INTO `quick_replies` (`Id`, `Title`, `Content`, `Category`, `Scope`, `IsActive`, `SortOrder`, `CreatedAt`)
-- VALUES (UNHEX(REPLACE(UUID(), '-', '')), '欢迎语', '您好！我是AI客服，请问有什么可以帮您？', 1, 1, TRUE, 0, NOW());

-- =====================================================
-- Migration Complete
-- =====================================================
-- Next steps:
-- 1. Verify the tables are created correctly
-- 2. Update application to use new entities
-- 3. Implement JWT authentication
-- 4. Implement support APIs
-- =====================================================
