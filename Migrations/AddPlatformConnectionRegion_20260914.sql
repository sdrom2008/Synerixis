-- Shopee 多站点：platform_connections.Region（SG/TW/VN/…）
-- SchemaPatcher 启动时也会幂等补列；本脚本供手工执行。

ALTER TABLE `platform_connections`
  ADD COLUMN `Region` varchar(16) NULL;
