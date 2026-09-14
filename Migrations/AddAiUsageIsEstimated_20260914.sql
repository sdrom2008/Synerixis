-- AiUsageLog.IsEstimated (MySQL 5.7+/8.0 idempotent)
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
