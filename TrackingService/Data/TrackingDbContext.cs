using Microsoft.EntityFrameworkCore;
using TrackingService.Models;

namespace TrackingService.Data;

public class TrackingDbContext : DbContext
{
    public TrackingDbContext(DbContextOptions<TrackingDbContext> options) : base(options) { }

    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentEvent> ShipmentEvents => Set<ShipmentEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.HasKey(e => e.TrackingCode);
            entity.Property(e => e.TrackingCode).HasMaxLength(32);
            entity.Property(e => e.Sender).HasMaxLength(120).IsRequired();
            entity.Property(e => e.Receiver).HasMaxLength(120).IsRequired();
            entity.Property(e => e.Location).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(30).IsRequired();

            entity.HasMany(e => e.History)
                  .WithOne(ev => ev.Shipment)
                  .HasForeignKey(ev => ev.TrackingCode)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ShipmentEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TrackingCode).HasMaxLength(32).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(e => e.Location).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Note).HasMaxLength(255);
        });
    }
}   