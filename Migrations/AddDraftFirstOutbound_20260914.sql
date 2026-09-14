-- =====================================================
-- Synerixis — Draft-first outbound (OutboundMode + draft_messages + session reply context)
-- Date: 2026-09-14
-- Note: EnsureCreated will NOT alter existing tables.
-- Default OutboundMode = DraftFirst (human-in-the-loop). AutoSend is opt-in compliance risk.
-- =====================================================

SET @db := DATABASE();

-- seller_configs.OutboundMode
SET @col := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'seller_configs' AND COLUMN_NAME = 'OutboundMode'
);
SET @ddl := IF(@col = 0,
  'ALTER TABLE `seller_configs` ADD COLUMN `OutboundMode` VARCHAR(32) NOT NULL DEFAULT ''DraftFirst'' COMMENT ''DraftFirst|AutoSend''',
  'SELECT ''OutboundMode already present'' AS info');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- chat_sessions platform reply context + timeout wake
SET @col2 := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'chat_sessions' AND COLUMN_NAME = 'PlatformConversationId'
);
SET @ddl2 := IF(@col2 = 0,
  'ALTER TABLE `chat_sessions`
     ADD COLUMN `PlatformConversationId` VARCHAR(191) NULL,
     ADD COLUMN `PlatformShopOpenId` VARCHAR(128) NULL,
     ADD COLUMN `LastBuyerMessageAt` DATETIME(6) NULL',
  'SELECT ''chat_sessions draft columns already present'' AS info');
PREPARE stmt2 FROM @ddl2; EXECUTE stmt2; DEALLOCATE PREPARE stmt2;

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
