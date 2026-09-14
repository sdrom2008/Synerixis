-- AiUsageLog.IsEstimated：无模型 Usage 时按 chars/4 估算并标记
-- SchemaPatcher 启动时也会幂等补列；本文件供手工执行。
-- MySQL 8：若列已存在会报错，可忽略。
ALTER TABLE `ai_usage_logs`
  ADD COLUMN `IsEstimated` TINYINT(1) NOT NULL DEFAULT 0;
