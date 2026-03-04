using Microsoft.EntityFrameworkCore;
using YP.Models;

namespace YP.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Role> Roles { get; set; }
        public DbSet<Status> Statuses { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Request> Requests { get; set; }
        public DbSet<Comment> Comments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Request>()
                .HasOne(r => r.Client)
                .WithMany()
                .HasForeignKey(r => r.ClientID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Request>()
                .HasOne(r => r.Master)
                .WithMany()
                .HasForeignKey(r => r.MasterID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Request>()
                .HasOne(r => r.Status)
                .WithMany()
                .HasForeignKey(r => r.StatusID)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
