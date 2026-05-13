using Microsoft.EntityFrameworkCore;
using PowerGuardCoreApi.Models;

public class PowerGuardDbContext : DbContext
{
    public DbSet<Account> Accounts { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<ActivityLog> ActivityLogs { get; set; }
    public DbSet<ArduinoLog> ArduinoLogs { get; set; }
    public DbSet<Room> Rooms { get; set; }
    public DbSet<ValidationAttempt> ValidationAttempts { get; set; }
    public DbSet<BlockedUid> BlockedUids { get; set; }
    public DbSet<Preferences> Preferences { get; set; }
    public DbSet<AccountRoom> AccountRooms { get; set; }

    public PowerGuardDbContext(DbContextOptions<PowerGuardDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Account → RefreshToken (one-to-many, cascade delete)
        modelBuilder.Entity<Account>()
            .HasMany(a => a.RefreshTokens)
            .WithOne(rt => rt.Account)
            .HasForeignKey(rt => rt.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        // Account ↔ Room (many-to-many via explicit join table)
        modelBuilder.Entity<AccountRoom>()
            .HasKey(ar => new { ar.AccountId, ar.RoomId });

        modelBuilder.Entity<AccountRoom>()
            .HasOne(ar => ar.Account)
            .WithMany(a => a.AccountRooms)
            .HasForeignKey(ar => ar.AccountId);

        modelBuilder.Entity<AccountRoom>()
            .HasOne(ar => ar.Room)
            .WithMany(r => r.AccountRooms)
            .HasForeignKey(ar => ar.RoomId);

        // Unique index on Room.DeviceId
        modelBuilder.Entity<Room>()
            .HasIndex(r => r.DeviceId)
            .IsUnique();

        // Unique index on BlockedUid.Uid
        modelBuilder.Entity<BlockedUid>()
            .HasIndex(b => b.Uid)
            .IsUnique();

        // Unique index on Account.Uid (allow null)
        modelBuilder.Entity<Account>()
            .HasIndex(a => a.Uid)
            .IsUnique()
            .HasFilter("[Uid] IS NOT NULL");

        // Preferences → Account (one-to-one)
        modelBuilder.Entity<Preferences>()
            .HasOne(p => p.Account)
            .WithOne()
            .HasForeignKey<Preferences>(p => p.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}