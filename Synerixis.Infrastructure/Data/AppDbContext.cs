using Microsoft.EntityFrameworkCore;
using Synerixis.Domain.Entities;

namespace Synerixis.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Seller> Sellers { get; set; }
        public DbSet<SellerConfig> SellerConfigs { get; set; }
        public DbSet<SellerProduct> SellerProducts { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<PayOrder> PayOrders { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductAttribute> ProductAttributes { get; set; }
        public DbSet<SKU> SKUs { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Brand> Brands { get; set; }

        // 新增实体
        public DbSet<Agent> Agents { get; set; }
        public DbSet<ChatSession> ChatSessions { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<QuickReply> QuickReplies { get; set; }
        public DbSet<AgentStat> AgentStats { get; set; }
        public DbSet<PlatformConnection> PlatformConnections { get; set; }
        public DbSet<DraftMessage> DraftMessages { get; set; }



        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. 统一 Guid 为 binary(16)（放在最前面）
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(Guid) || property.ClrType == typeof(Guid?))
                    {
                        property.SetColumnType("binary(16)");
                    }
                }
            }

            // 2. 所有 string 字段默认 longtext（MySQL 兼容）
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(string) && property.GetMaxLength() == null)
                    {
                        property.SetColumnType("longtext");
                    }
                }
            }

            // 3. 显式配置每个实体（表名 + 关系）
            modelBuilder.Entity<Seller>(entity =>
            {
                entity.ToTable("sellers");
                entity.HasKey(s => s.Id);

                // 明确指定 OpenId 类型和长度（防止被全局 longtext 覆盖）
                entity.Property(s => s.OpenId)
                      .HasColumnType("varchar(128)")
                      .HasMaxLength(128)
                      .IsRequired();

                entity.HasIndex(s => s.OpenId)
                      .IsUnique()
                      .HasDatabaseName("IX_sellers_OpenId");
            });

            modelBuilder.Entity<SellerConfig>(entity =>
            {
                entity.ToTable("seller_configs");
                entity.HasKey(c => c.Id);

                entity.Property(c => c.BusinessHoursStart)
                      .HasColumnType("varchar(8)")
                      .HasMaxLength(8);
                entity.Property(c => c.BusinessHoursEnd)
                      .HasColumnType("varchar(8)")
                      .HasMaxLength(8);

                entity.Property(c => c.OutboundMode)
                      .HasColumnType("varchar(32)")
                      .HasMaxLength(32)
                      .HasDefaultValue("DraftFirst");

                entity.HasOne(c => c.Seller)
                      .WithOne(s => s.Config)
                      .HasForeignKey<SellerConfig>(c => c.SellerId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SellerProduct>(entity =>
            {
                entity.ToTable("seller_products");
                entity.HasKey(p => p.Id);

                entity.HasOne(p => p.Seller)
                      .WithMany(s => s.SellerProducts)
                      .HasForeignKey(p => p.SellerId)
                      .OnDelete(DeleteBehavior.Cascade)
                      .IsRequired();

                entity.HasOne(p => p.Product)
                      .WithMany(prod => prod.SellerProducts)
                      .HasForeignKey(p => p.ProductId)
                      .OnDelete(DeleteBehavior.NoAction)
                      .IsRequired();
            });

            modelBuilder.Entity<Product>(entity =>
            {
                entity.ToTable("products");
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Category)
                      .WithMany(c => c.Products)
                      .HasForeignKey(e => e.CategoryId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Brand)
                      .WithMany(b => b.Products)
                      .HasForeignKey(e => e.BrandId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ProductAttribute>(entity =>
            {
                entity.ToTable("product_attributes");
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Product)
                      .WithMany(p => p.Attributes)
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SKU>(entity =>
            {
                entity.ToTable("skus");
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Product)
                      .WithMany(p => p.SKUs)
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Category>(entity =>
            {
                entity.ToTable("categories");
                entity.HasKey(e => e.Id);

                entity.HasMany(e => e.Products)
                      .WithOne(p => p.Category)
                      .HasForeignKey(p => p.CategoryId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Brand>(entity =>
            {
                entity.ToTable("brands");
                entity.HasKey(e => e.Id);

                entity.HasMany(e => e.Products)
                      .WithOne(p => p.Brand)
                      .HasForeignKey(p => p.BrandId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ChatSession>(entity =>
            {
                entity.ToTable("chat_sessions");
                entity.HasKey(s => s.Id);

                entity.Property(s => s.SessionId)
                      .HasColumnType("varchar(100)")
                      .HasMaxLength(100)
                      .IsRequired();

                entity.Property(s => s.CustomerId)
                      .HasColumnType("varchar(100)")
                      .HasMaxLength(100)
                      .IsRequired();

                entity.HasIndex(s => s.SessionId)
                      .IsUnique()
                      .HasDatabaseName("IX_chat_sessions_SessionId");

                entity.HasIndex(s => s.CustomerId);

                entity.Property(s => s.PlatformConversationId)
                      .HasColumnType("varchar(191)")
                      .HasMaxLength(191);
                entity.Property(s => s.PlatformShopOpenId)
                      .HasColumnType("varchar(128)")
                      .HasMaxLength(128);

                entity.HasOne(s => s.Shop)
                      .WithMany()
                      .HasForeignKey(s => s.ShopId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.AssignedAgent)
                      .WithMany()
                      .HasForeignKey(s => s.AssignedAgentId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Agent>(entity =>
            {
                entity.ToTable("agents");
                entity.HasKey(a => a.Id);

                entity.Property(a => a.Email)
                      .HasColumnType("varchar(255)")
                      .HasMaxLength(255)
                      .IsRequired();

                entity.HasIndex(a => a.Email)
                      .IsUnique()
                      .HasDatabaseName("IX_agents_Email");

                entity.HasOne(a => a.Shop)
                      .WithMany()
                      .HasForeignKey(a => a.ShopId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Order>(entity =>
            {
                entity.ToTable("orders");
                entity.HasKey(o => o.Id);

                entity.Property(o => o.OrderNo)
                      .HasColumnType("varchar(100)")
                      .HasMaxLength(100)
                      .IsRequired();

                // CustomerId 用于索引，需指定 varchar 长度
                entity.Property(o => o.CustomerId)
                      .HasColumnType("varchar(100)")
                      .HasMaxLength(100)
                      .IsRequired();

                entity.HasIndex(o => o.OrderNo)
                      .IsUnique()
                      .HasDatabaseName("IX_orders_OrderNo");

                entity.HasIndex(o => new { o.ShopId, o.CustomerId });

                entity.HasOne(o => o.Shop)
                      .WithMany()
                      .HasForeignKey(o => o.ShopId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<QuickReply>(entity =>
            {
                entity.ToTable("quick_replies");
                entity.HasKey(q => q.Id);

                entity.Property(q => q.Title)
                      .HasColumnType("varchar(200)")
                      .HasMaxLength(200)
                      .IsRequired();

                entity.Property(q => q.Content)
                      .HasColumnType("longtext");
            });

            modelBuilder.Entity<AgentStat>(entity =>
            {
                entity.ToTable("agent_stats");
                entity.HasKey(a => a.Id);

                // 组合唯一索引：AgentId + StatDate
                entity.HasIndex(a => new { a.AgentId, a.StatDate })
                      .IsUnique()
                      .HasDatabaseName("IX_agent_stats_Agent_Date");

                entity.HasOne(a => a.Agent)
                      .WithMany()
                      .HasForeignKey(a => a.AgentId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Conversation>(entity =>
            {
                entity.ToTable("conversations");
                entity.HasKey(c => c.Id);

                entity.HasOne(c => c.Seller)
                      .WithMany(s => s.Conversations)
                      .HasForeignKey(c => c.SellerId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ChatMessage>(entity =>
            {
                entity.ToTable("chat_messages");
                entity.HasKey(m => m.Id);

                entity.HasOne(m => m.ChatSession)
                      .WithMany(c => c.Messages)
                      .HasForeignKey(m => m.ChatSessionId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Seller>(entity =>
            {
                entity.ToTable("sellers");
                entity.HasKey(s => s.Id);

                entity.HasMany(s => s.PlatformConnections)
                      .WithOne(c => c.Seller)
                      .HasForeignKey(c => c.SellerId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(s => s.Conversations)
                      .WithOne(c => c.Seller)
                      .HasForeignKey(c => c.SellerId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<DraftMessage>(entity =>
            {
                entity.ToTable("draft_messages");
                entity.HasKey(d => d.Id);

                entity.Property(d => d.Status)
                      .HasColumnType("varchar(32)")
                      .HasMaxLength(32)
                      .IsRequired();

                entity.Property(d => d.Content)
                      .HasColumnType("longtext");

                entity.HasIndex(d => new { d.ChatSessionId, d.Status });

                entity.HasOne(d => d.ChatSession)
                      .WithMany(s => s.Drafts)
                      .HasForeignKey(d => d.ChatSessionId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<PlatformConnection>(entity =>
            {
                entity.ToTable("platform_connections");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.SellerId)
                      .HasColumnType("binary(16)")
                      .ValueGeneratedOnAdd();

                entity.Property(e => e.Platform)
                      .HasColumnType("varchar(50)")
                      .HasMaxLength(50)
                      .IsRequired();

                entity.Property(e => e.OpenId)
                      .HasColumnType("varchar(128)")
                      .HasMaxLength(128)
                      .IsRequired();

                entity.Property(e => e.ShopId)
                      .HasColumnType("varchar(128)")
                      .HasMaxLength(128);

                entity.Property(e => e.AppKey)
                      .HasColumnType("longtext");

                entity.Property(e => e.AccessToken)
                      .HasColumnType("longtext");

                entity.Property(e => e.RefreshToken)
                      .HasColumnType("varchar(512)");

                entity.Property(e => e.Nickname)
                      .HasColumnType("varchar(128)");

                entity.Property(e => e.AvatarUrl)
                      .HasColumnType("varchar(512)");

                entity.Property(e => e.IsActive)
                      .HasColumnType("bit");

                entity.Property(e => e.CreatedAt)
                      .HasColumnType("datetime2");

                entity.Property(e => e.UpdatedAt)
                      .HasColumnType("datetime2");

                entity.HasOne(e => e.Seller)
                      .WithMany(s => s.PlatformConnections)
                      .HasForeignKey(e => e.SellerId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}