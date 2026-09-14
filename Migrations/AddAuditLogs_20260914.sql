-- Lightweight audit_logs for key write actions
CREATE TABLE IF NOT EXISTS `audit_logs` (
  `Id` binary(16) NOT NULL,
  `ActorId` binary(16) NULL,
  `ActorType` varchar(32) NOT NULL,
  `Action` varchar(64) NOT NULL,
  `ResourceType` varchar(64) NULL,
  `ResourceId` varchar(64) NULL,
  `DetailJson` longtext NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `ShopId` binary(16) NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_audit_logs_ShopId_CreatedAt` (`ShopId`, `CreatedAt`),
  KEY `IX_audit_logs_CreatedAt` (`CreatedAt`)
) CHARACTER SET utf8mb4;
