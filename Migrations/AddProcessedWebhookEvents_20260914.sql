-- Webhook 幂等表（SchemaPatcher 也会 Ensure）
CREATE TABLE IF NOT EXISTS `processed_webhook_events` (
  `Id` binary(16) NOT NULL,
  `Platform` varchar(32) NOT NULL,
  `EventKey` varchar(191) NOT NULL,
  `ProcessedAt` datetime(6) NOT NULL,
  `IsWeakKey` tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_processed_webhook_events_Platform_EventKey` (`Platform`, `EventKey`)
) CHARACTER SET utf8mb4;
