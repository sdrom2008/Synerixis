-- =====================================================
-- Synerixis — Fix missing chat_messages.ConversationId after FullReset
-- Date: 2026-09-17
-- MySQL 8+ (INFORMATION_SCHEMA + prepared DDL; no MariaDB IF NOT EXISTS)
--
-- Why: Older EF models inferred a shadow FK ConversationId from
-- Conversation.Messages. FullReset_nexusai_db.sql intentionally omits that
-- column (messages belong to chat_sessions only). If you still run a build
-- that SELECTs c0.ConversationId, either:
--   (1) Pull latest (model Ignores Conversation.Messages — preferred), OR
--   (2) Run this script to add a nullable column for interim compatibility.
-- Safe to re-run. Does not require re-running FullReset.
-- =====================================================

SET @db := DATABASE();

SET @col := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = @db
    AND TABLE_NAME = 'chat_messages'
    AND COLUMN_NAME = 'ConversationId'
);
SET @ddl := IF(@col = 0,
  'ALTER TABLE `chat_messages` ADD COLUMN `ConversationId` binary(16) NULL',
  'SELECT ''chat_messages.ConversationId already present'' AS info');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @idx := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
  WHERE TABLE_SCHEMA = @db
    AND TABLE_NAME = 'chat_messages'
    AND INDEX_NAME = 'IX_chat_messages_ConversationId'
);
SET @ddl_idx := IF(@idx = 0,
  'ALTER TABLE `chat_messages` ADD KEY `IX_chat_messages_ConversationId` (`ConversationId`)',
  'SELECT ''IX_chat_messages_ConversationId already present'' AS info');
PREPARE stmt_idx FROM @ddl_idx; EXECUTE stmt_idx; DEALLOCATE PREPARE stmt_idx;

SET @fk := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
  WHERE TABLE_SCHEMA = @db
    AND TABLE_NAME = 'chat_messages'
    AND CONSTRAINT_NAME = 'FK_chat_messages_conversations_ConversationId'
    AND CONSTRAINT_TYPE = 'FOREIGN KEY'
);
SET @ddl_fk := IF(@fk = 0,
  'ALTER TABLE `chat_messages` ADD CONSTRAINT `FK_chat_messages_conversations_ConversationId` FOREIGN KEY (`ConversationId`) REFERENCES `conversations` (`Id`) ON DELETE RESTRICT',
  'SELECT ''FK_chat_messages_conversations_ConversationId already present'' AS info');
PREPARE stmt_fk FROM @ddl_fk; EXECUTE stmt_fk; DEALLOCATE PREPARE stmt_fk;

SELECT ''Fix_ConversationId_after_fullreset done'' AS info;
