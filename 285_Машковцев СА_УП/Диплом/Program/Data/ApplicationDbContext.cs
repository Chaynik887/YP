using Microsoft.EntityFrameworkCore;
using Program.Models;

namespace Program.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Role> Roles { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Service> Services { get; set; }
    public DbSet<Sale> Sales { get; set; }
    public DbSet<SaleItem> SaleItems { get; set; }
    public DbSet<ServiceOrder> ServiceOrders { get; set; }
    public DbSet<ServiceOrderItem> ServiceOrderItems { get; set; }
    public DbSet<Supply> Supplies { get; set; }
    public DbSet<SupplyItem> SupplyItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // настройка таблицы Roles
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(e => e.RoleId);
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.RoleName).HasColumnName("role_name").HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.RoleName).IsUnique();
        });

        // настройка таблицы Users
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Login).HasColumnName("login").HasMaxLength(50).IsRequired();
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
            entity.Property(e => e.FullName).HasColumnName("full_name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Phone).HasColumnName("phone").HasMaxLength(20);
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.HasIndex(e => e.Login).IsUnique();
            entity.HasOne(e => e.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // настройка таблицы Products
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(e => e.ProductId);
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Price).HasColumnName("price").HasColumnType("decimal(10,2)").IsRequired();
            entity.Property(e => e.CostPrice).HasColumnName("cost_price").HasColumnType("decimal(10,2)");
            entity.Property(e => e.StockQuantity).HasColumnName("stock_quantity");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
        });

        // настройка таблицы Services
        modelBuilder.Entity<Service>(entity =>
        {
            entity.ToTable("services");
            entity.HasKey(e => e.ServiceId);
            entity.Property(e => e.ServiceId).HasColumnName("service_id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Price).HasColumnName("price").HasColumnType("decimal(10,2)").IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("is_active");
        });

        // настройка таблицы Sales
        modelBuilder.Entity<Sale>(entity =>
        {
            entity.ToTable("sales");
            entity.HasKey(e => e.SaleId);
            entity.Property(e => e.SaleId).HasColumnName("sale_id");
            entity.Property(e => e.SellerId).HasColumnName("seller_id");
            entity.Property(e => e.SaleDate).HasColumnName("sale_date");
            entity.Property(e => e.TotalAmount).HasColumnName("total_amount").HasColumnType("decimal(10,2)");
            entity.HasOne(e => e.Seller)
                .WithMany()
                .HasForeignKey(e => e.SellerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // настройка таблицы SaleItems
        modelBuilder.Entity<SaleItem>(entity =>
        {
            entity.ToTable("saleitems");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SaleId).HasColumnName("sale_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.PriceAtSale).HasColumnName("price_at_sale").HasColumnType("decimal(10,2)");
            entity.HasOne(e => e.Sale)
                .WithMany(s => s.SaleItems)
                .HasForeignKey(e => e.SaleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // настройка таблицы ServiceOrders
        modelBuilder.Entity<ServiceOrder>(entity =>
        {
            entity.ToTable("serviceorders");
            entity.HasKey(e => e.OrderId);
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.MasterId).HasColumnName("master_id");
            entity.Property(e => e.OrderDate).HasColumnName("order_date");
            entity.Property(e => e.TotalAmount).HasColumnName("total_amount").HasColumnType("decimal(10,2)");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.HasOne(e => e.Master)
                .WithMany()
                .HasForeignKey(e => e.MasterId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // настройка таблицы ServiceOrderItems
        modelBuilder.Entity<ServiceOrderItem>(entity =>
        {
            entity.ToTable("serviceorderitems");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.ServiceId).HasColumnName("service_id");
            entity.Property(e => e.PriceAtOrder).HasColumnName("price_at_order").HasColumnType("decimal(10,2)");
            entity.HasOne(e => e.ServiceOrder)
                .WithMany(so => so.ServiceOrderItems)
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Service)
                .WithMany()
                .HasForeignKey(e => e.ServiceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // настройка таблицы Supplies
        modelBuilder.Entity<Supply>(entity =>
        {
            entity.ToTable("supplies");
            entity.HasKey(e => e.SupplyId);
            entity.Property(e => e.SupplyId).HasColumnName("supply_id");
            entity.Property(e => e.SupplierName).HasColumnName("supplier_name").HasMaxLength(200);
            entity.Property(e => e.ReceivedByUserId).HasColumnName("received_by_user_id");
            entity.Property(e => e.SupplyDate).HasColumnName("supply_date");
            entity.Property(e => e.TotalCost).HasColumnName("total_cost").HasColumnType("decimal(10,2)");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.HasOne(e => e.ReceivedByUser)
                .WithMany()
                .HasForeignKey(e => e.ReceivedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // настройка таблицы SupplyItems
        modelBuilder.Entity<SupplyItem>(entity =>
        {
            entity.ToTable("supplyitems");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SupplyId).HasColumnName("supply_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.CostPriceAtSupply).HasColumnName("cost_price_at_supply").HasColumnType("decimal(10,2)");
            entity.HasOne(e => e.Supply)
                .WithMany(s => s.SupplyItems)
                .HasForeignKey(e => e.SupplyId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

