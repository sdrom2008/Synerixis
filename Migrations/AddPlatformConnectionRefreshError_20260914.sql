-- PlatformConnection: LastRefreshError / LastRefreshAt（Token 刷新失败引导重绑）
-- 幂等：列已存在则跳过（可由 SchemaPatcher 在启动时执行）

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
