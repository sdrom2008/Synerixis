using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Synerixis.Infrastructure.Data
{
    /// <summary>
    /// EnsureCreated 不会 ALTER 已有表。对已知增量列做幂等补丁（MySQL/MariaDB/SQLite 尽力而为）。
    /// </summary>
    public static class SchemaPatcher
    {
        public static async Task ApplyAsync(AppDbContext db, ILogger? logger = null)
        {
            try
            {
                var provider = db.Database.ProviderName ?? "";
                if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
                {
                    await TrySqliteAsync(db, logger);
                }
                else
                {
                    await TryMySqlAsync(db, logger);
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex,
                    "[SchemaPatcher] Failed to apply seller_configs AI columns; run Migrations/AddSellerConfigAiSettings_20260914.sql manually");
            }
        }

        private static async Task TryMySqlAsync(AppDbContext db, ILogger? logger)
        {
            const string sql = @"
SET @db := DATABASE();
SET @col := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'seller_configs' AND COLUMN_NAME = 'EnableAutoReply'
);
SET @ddl := IF(@col = 0,
  'ALTER TABLE `seller_configs` ADD COLUMN `EnableAutoReply` TINYINT(1) NOT NULL DEFAULT 1, ADD COLUMN `BusinessHoursStart` VARCHAR(8) NOT NULL DEFAULT ''09:00'', ADD COLUMN `BusinessHoursEnd` VARCHAR(8) NOT NULL DEFAULT ''22:00''',
  'SELECT 1');
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
";
            await db.Database.ExecuteSqlRawAsync(sql);
            logger?.LogInformation("[SchemaPatcher] seller_configs AI columns ensured (MySQL)");
        }

        private static async Task TrySqliteAsync(AppDbContext db, ILogger? logger)
        {
            foreach (var ddl in new[]
            {
                "ALTER TABLE seller_configs ADD COLUMN EnableAutoReply INTEGER NOT NULL DEFAULT 1",
                "ALTER TABLE seller_configs ADD COLUMN BusinessHoursStart TEXT NOT NULL DEFAULT '09:00'",
                "ALTER TABLE seller_configs ADD COLUMN BusinessHoursEnd TEXT NOT NULL DEFAULT '22:00'"
            })
            {
                try
                {
                    await db.Database.ExecuteSqlRawAsync(ddl);
                }
                catch (Exception ex)
                {
                    logger?.LogDebug(ex, "[SchemaPatcher] SQLite ALTER skipped: {Ddl}", ddl);
                }
            }
        }
    }
}
