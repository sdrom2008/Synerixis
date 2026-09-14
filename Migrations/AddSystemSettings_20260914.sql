-- SystemSettings: platform ops switches (key-value). Safe writable keys only; no secrets.
CREATE TABLE IF NOT EXISTS `system_settings` (
  `Key` varchar(64) NOT NULL,
  `Value` varchar(512) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Key`)
) CHARACTER SET utf8mb4;
