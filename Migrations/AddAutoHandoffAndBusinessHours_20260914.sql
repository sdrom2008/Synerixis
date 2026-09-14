-- Auto handoff + outside business hours policy (MySQL 5.7+/8.0)
-- Compatible with MySQL: no ADD COLUMN IF NOT EXISTS (MariaDB-only).
-- Idempotent via INFORMATION_SCHEMA. SchemaPatcher also ensures at startup.

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
