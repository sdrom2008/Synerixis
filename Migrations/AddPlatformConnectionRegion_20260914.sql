-- Shopee multi-region: platform_connections.Region (MySQL 5.7+/8.0 idempotent)
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
