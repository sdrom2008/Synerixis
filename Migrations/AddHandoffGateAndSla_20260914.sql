-- =====================================================
-- Synerixis — Handoff hard gate + SLA wake
-- Date: 2026-09-14
-- Note: EnsureCreated will NOT alter existing tables.
-- =====================================================

SET @db := DATABASE();

SET @col := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'chat_sessions' AND COLUMN_NAME = 'PendingHumanHandoff'
);
SET @ddl := IF(@col = 0,
  'ALTER TABLE `chat_sessions`
     ADD COLUMN `PendingHumanHandoff` TINYINT(1) NOT NULL DEFAULT 0 COMMENT ''转人工后停 AI 新草稿'',
     ADD COLUMN `HandoffAt` DATETIME(6) NULL',
  'SELECT ''chat_sessions handoff columns already present'' AS info');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col2 := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'seller_configs' AND COLUMN_NAME = 'ResponseSlaHours'
);
SET @ddl2 := IF(@col2 = 0,
  'ALTER TABLE `seller_configs`
     ADD COLUMN `ResponseSlaHours` INT NOT NULL DEFAULT 12 COMMENT ''建议回复 SLA 小时'',
     ADD COLUMN `AlertThresholdHours` VARCHAR(64) NOT NULL DEFAULT ''1,3,12'' COMMENT ''告警阈值小时逗号分隔''',
  'SELECT ''seller_configs SLA columns already present'' AS info');
PREPARE stmt2 FROM @ddl2; EXECUTE stmt2; DEALLOCATE PREPARE stmt2;
