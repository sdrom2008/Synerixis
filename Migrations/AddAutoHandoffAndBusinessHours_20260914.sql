-- Auto handoff + outside business hours policy (MySQL)
-- Idempotent-ish: run once; SchemaPatcher also ensures columns at startup.

ALTER TABLE `seller_configs`
  ADD COLUMN IF NOT EXISTS `AutoHandoffOnLowConfidence` TINYINT(1) NOT NULL DEFAULT 1,
  ADD COLUMN IF NOT EXISTS `HandoffConfidenceThreshold` DOUBLE NOT NULL DEFAULT 0.45,
  ADD COLUMN IF NOT EXISTS `SensitiveKeywords` VARCHAR(1024) NOT NULL DEFAULT '退款,律师,投诉,police,lawyer,refund,lawsuit,举报,报警,法院,诉讼',
  ADD COLUMN IF NOT EXISTS `HandoffOutsideBusinessHours` TINYINT(1) NOT NULL DEFAULT 1,
  ADD COLUMN IF NOT EXISTS `TimeZoneId` VARCHAR(64) NOT NULL DEFAULT 'Asia/Shanghai';
