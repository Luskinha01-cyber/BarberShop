using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using BarberShop.Models;

namespace BarberShop.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Barber> Barbers => Set<Barber>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<BarberWorkingHour> BarberWorkingHours => Set<BarberWorkingHour>();
    public DbSet<BarberTimeOff> BarberTimeOffs => Set<BarberTimeOff>();
    public DbSet<BusinessSettings> BusinessSettings => Set<BusinessSettings>();
    public DbSet<WhatsAppNotification> WhatsAppNotifications => Set<WhatsAppNotification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Indices
        builder.Entity<Appointment>()
            .HasIndex(a => new { a.BarberId, a.StartTime, a.EndTime, a.Status });

        builder.Entity<ApplicationUser>()
            .HasIndex(u => u.PhoneNumber)
            .IsUnique(false);

        // Relacionamentos Appointment
        builder.Entity<Appointment>()
            .HasOne(a => a.Client)
            .WithMany(u => u.Appointments)
            .HasForeignKey(a => a.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Appointment>()
            .HasOne(a => a.Barber)
            .WithMany(b => b.Appointments)
            .HasForeignKey(a => a.BarberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Appointment>()
            .HasOne(a => a.Service)
            .WithMany(s => s.Appointments)
            .HasForeignKey(a => a.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Barber WorkingHours e TimeOffs
        builder.Entity<BarberWorkingHour>()
            .HasOne(w => w.Barber)
            .WithMany(b => b.WorkingHours)
            .HasForeignKey(w => w.BarberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<BarberTimeOff>()
            .HasOne(t => t.Barber)
            .WithMany(b => b.TimeOffs)
            .HasForeignKey(t => t.BarberId)
            .OnDelete(DeleteBehavior.Cascade);

        // WhatsApp Notification
        builder.Entity<WhatsAppNotification>()
            .HasOne(n => n.Appointment)
            .WithMany(a => a.WhatsAppNotifications)
            .HasForeignKey(n => n.AppointmentId)
            .OnDelete(DeleteBehavior.SetNull);

        // Precisões decimais
        builder.Entity<Service>()
            .Property(s => s.Price)
            .HasPrecision(18, 2);

        builder.Entity<Appointment>()
            .Property(a => a.Price)
            .HasPrecision(18, 2);

        builder.Entity<Appointment>()
            .Property(a => a.AmountPaid)
            .HasPrecision(18, 2);
    }
}
