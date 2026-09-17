-- CBEC i18n: seller supported language list (working language = PreferredLanguage)
SET @db := DATABASE();
SET @col := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'seller_configs' AND COLUMN_NAME = 'SupportedLanguages'
);
SET @ddl := IF(@col = 0,
  'ALTER TABLE `seller_configs` ADD COLUMN `SupportedLanguages` VARCHAR(64) NOT NULL DEFAULT ''ID,TH,VN,EN,ZH''',
  'SELECT 1');
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
