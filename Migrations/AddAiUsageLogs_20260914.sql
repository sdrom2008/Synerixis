-- AiUsageLog token 记账表（SchemaPatcher 启动时也会 Ensure）
CREATE TABLE IF NOT EXISTS `ai_usage_logs` (
  `Id` binary(16) NOT NULL,
  `SellerId` binary(16) NOT NULL,
  `SessionId` binary(16) NULL,
  `Model` varchar(64) NOT NULL,
  `PromptTokens` int NOT NULL DEFAULT 0,
  `CompletionTokens` int NOT NULL DEFAULT 0,
  `EstimatedCostUsd` decimal(18,6) NOT NULL DEFAULT 0,
  `CreatedAt` datetime(6) NOT NULL,
  `Purpose` varchar(32) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_ai_usage_logs_SellerId_CreatedAt` (`SellerId`, `CreatedAt`),
  KEY `IX_ai_usage_logs_CreatedAt` (`CreatedAt`)
) CHARACTER SET utf8mb4;
