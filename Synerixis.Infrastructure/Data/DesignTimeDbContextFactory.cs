using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace Synerixis.Infrastructure.Data
{
    /// <summary>
    /// Design-time factory for EF Core migrations
    /// </summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            // 确定 API 项目目录的绝对路径
            // 假设当前文件在: my-project/Synerixis.Infrastructure/Data/
            var currentDir = Directory.GetCurrentDirectory();
            var possiblePaths = new[]
            {
                Path.Combine(currentDir, "Synerixis.Api"),  // 如果在 Infrastructure 目录运行
                Path.Combine(currentDir, "my-project", "Synerixis.Api"),  // 如果在 workspace 根目录运行
                Path.Combine(currentDir, "..", "Synerixis.Api"),  // 如果在一个子目录
            };

            var apiPath = possiblePaths.FirstOrDefault(Path.Exists);
            if (apiPath == null)
                throw new DirectoryNotFoundException($"Could not find Synerixis.Api directory. Tried: {string.Join(", ", possiblePaths)}");

            var configuration = new ConfigurationBuilder()
                .SetBasePath(apiPath)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();

            var builder = new DbContextOptionsBuilder<AppDbContext>();

            var provider = configuration["Database:Provider"] ?? "mysql";
            var connectionString = configuration["Database:ConnectionString"];

            if (string.IsNullOrEmpty(connectionString))
                throw new InvalidOperationException("Connection string 'Database:ConnectionString' not found in configuration.");

            if (provider == "mysql")
            {
                builder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), mysql =>
                {
                    mysql.EnableRetryOnFailure();
                });
            }
            else
            {
                throw new NotSupportedException($"Provider '{provider}' is not supported.");
            }

            return new AppDbContext(builder.Options);
        }
    }
}
