-- =====================================================
-- Synerixis — SellerConfig AI settings columns
-- Date: 2026-09-14
-- Note: EnsureCreated will NOT alter existing tables.
-- Run manually on MySQL/MariaDB if SchemaPatcher did not apply.
-- =====================================================

SET @db := DATABASE();

SET @col := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'seller_configs' AND COLUMN_NAME = 'EnableAutoReply'
);

SET @ddl := IF(@col = 0,
  'ALTER TABLE `seller_configs`
     ADD COLUMN `EnableAutoReply` TINYINT(1) NOT NULL DEFAULT 1 COMMENT ''Webhook auto AI reply'',
     ADD COLUMN `BusinessHoursStart` VARCHAR(8) NOT NULL DEFAULT ''09:00'',
     ADD COLUMN `BusinessHoursEnd` VARCHAR(8) NOT NULL DEFAULT ''22:00''',
  'SELECT ''seller_configs AI columns already present'' AS info');

PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
