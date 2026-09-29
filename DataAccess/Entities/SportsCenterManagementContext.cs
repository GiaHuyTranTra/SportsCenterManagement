using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

public partial class SportsCenterManagementContext : DbContext
{
    public SportsCenterManagementContext()
    {
    }

    public SportsCenterManagementContext(DbContextOptions<SportsCenterManagementContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Account> Accounts { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<CenterManager> CenterManagers { get; set; }

    public virtual DbSet<Coach> Coaches { get; set; }

    public virtual DbSet<Member> Members { get; set; }

    public virtual DbSet<Receptionist> Receptionists { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<MembershipPackage> MembershipPackages { get; set; }

    public virtual DbSet<MemberSubscription> MemberSubscriptions { get; set; }

    public virtual DbSet<MembershipInvoice> MembershipInvoices { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("Server=localhost\\SQLEXPRESS01;User Id=sa;Password=12345;Database=SportsCenterManagement;Encrypt=False;");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Account__3214EC07E4264B6E");

            entity.ToTable("Account");

            entity.HasIndex(e => e.Email, "UQ__Account__A9D105343F8562DD").IsUnique();

            entity.Property(e => e.Id)
                .HasMaxLength(400)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Email)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Phone)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Active");

            entity.HasOne(d => d.Role).WithMany(p => p.Accounts)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Account_Role");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AuditLog__3214EC07CCB0C4E6");

            entity.ToTable("AuditLog");

            entity.Property(e => e.Id)
                .HasMaxLength(400)
                .IsUnicode(false);
            entity.Property(e => e.AccountId)
                .HasMaxLength(400)
                .IsUnicode(false);
            entity.Property(e => e.Action)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.EntityId)
                .HasMaxLength(400)
                .IsUnicode(false);
            entity.Property(e => e.EntityType)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.Account).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.AccountId)
                .HasConstraintName("FK_AuditLog_Account");
        });

        modelBuilder.Entity<CenterManager>(entity =>
        {
            entity.HasKey(e => e.AccountId).HasName("PK__CenterMa__349DA5A64807BEB6");

            entity.ToTable("CenterManager");

            entity.Property(e => e.AccountId)
                .HasMaxLength(400)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FullName).HasMaxLength(100);

            entity.HasOne(d => d.Account).WithOne(p => p.CenterManager)
                .HasForeignKey<CenterManager>(d => d.AccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CenterManager_Account");
        });

        modelBuilder.Entity<Coach>(entity =>
        {
            entity.HasKey(e => e.AccountId).HasName("PK__Coach__349DA5A65C9861FD");

            entity.ToTable("Coach");

            entity.Property(e => e.AccountId)
                .HasMaxLength(400)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Specialization).HasMaxLength(255);
            entity.Property(e => e.WorkSchedule).HasMaxLength(500);

            entity.HasOne(d => d.Account).WithOne(p => p.Coach)
                .HasForeignKey<Coach>(d => d.AccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Coach_Account");
        });

        modelBuilder.Entity<Member>(entity =>
        {
            entity.HasKey(e => e.AccountId).HasName("PK__Member__349DA5A67C08C6CC");

            entity.ToTable("Member");

            entity.HasIndex(e => e.MemberCode, "UQ__Member__84CA6377C2C6C2FB").IsUnique();

            entity.Property(e => e.AccountId)
                .HasMaxLength(400)
                .IsUnicode(false);
            entity.Property(e => e.AvatarUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.MemberCode)
                .HasMaxLength(30)
                .IsUnicode(false);

            entity.HasOne(d => d.Account).WithOne(p => p.Member)
                .HasForeignKey<Member>(d => d.AccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Member_Account");
        });

        modelBuilder.Entity<Receptionist>(entity =>
        {
            entity.HasKey(e => e.AccountId).HasName("PK__Receptio__349DA5A6971DB159");

            entity.ToTable("Receptionist");

            entity.Property(e => e.AccountId)
                .HasMaxLength(400)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.WorkShift).HasMaxLength(100);

            entity.HasOne(d => d.Account).WithOne(p => p.Receptionist)
                .HasForeignKey<Receptionist>(d => d.AccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Receptionist_Account");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Role");

            entity.HasIndex(e => e.Name, "UQ__Role__737584F616AD49EB").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).HasMaxLength(50);
        });

        modelBuilder.Entity<MembershipPackage>(entity =>
        {
            entity.ToTable("MembershipPackage");

            entity.HasIndex(e => e.Name, "UQ_MembershipPackage_Name").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql(
                    "(sysutcdatetime())",
                    "DF_MembershipPackage_CreatedAt");

            entity.Property(e => e.IsActive)
                .HasDefaultValue(
                    true,
                    "DF_MembershipPackage_IsActive");

            entity.Property(e => e.Name).HasMaxLength(80);

            entity.Property(e => e.Price)
                .HasColumnType("decimal(18, 0)");
        });

        modelBuilder.Entity<MemberSubscription>(entity =>
        {
            entity.ToTable("MemberSubscription");

            entity.HasIndex(e => new { e.MemberId, e.Status }, "IX_MemberSubscription_MemberId_Status");

            entity.HasIndex(e => e.MemberId, "UQ_MemberSubscription_Pending_Member")
                .IsUnique()
                .HasFilter("([Status]='PENDING_PAYMENT')");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_MemberSubscription_CreatedAt");
            entity.Property(e => e.Kind)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.MemberId)
                .HasMaxLength(400)
                .IsUnicode(false);
            entity.Property(e => e.PackageName).HasMaxLength(80);
            entity.Property(e => e.PackagePrice).HasColumnType("decimal(18, 0)");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("PENDING_PAYMENT", "DF_MemberSubscription_Status");

            entity.HasOne(d => d.Member).WithMany(p => p.MemberSubscriptions)
                .HasForeignKey(d => d.MemberId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MemberSubscription_Member");

            entity.HasOne(d => d.Package).WithMany(p => p.MemberSubscriptions)
                .HasForeignKey(d => d.PackageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MemberSubscription_MembershipPackage");
        });

        modelBuilder.Entity<MembershipInvoice>(entity =>
        {
            entity.ToTable("MembershipInvoice");

            entity.HasIndex(e => new { e.MemberId, e.Status }, "IX_MembershipInvoice_MemberId_Status");

            entity.HasIndex(e => e.InvoiceNumber, "UQ_MembershipInvoice_InvoiceNumber").IsUnique();

            entity.HasIndex(e => e.SubscriptionId, "UQ_MembershipInvoice_SubscriptionId").IsUnique();

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 0)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_MembershipInvoice_CreatedAt");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(400)
                .IsUnicode(false);
            entity.Property(e => e.InvoiceNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.MemberId)
                .HasMaxLength(400)
                .IsUnicode(false);
            entity.Property(e => e.PaidBy)
                .HasMaxLength(400)
                .IsUnicode(false);
            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("PENDING_PAYMENT", "DF_MembershipInvoice_Status");

            entity.HasOne(d => d.CreatedByNavigation).WithMany()
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MembershipInvoice_CreatedBy");

            entity.HasOne(d => d.Member).WithMany(p => p.MembershipInvoices)
                .HasForeignKey(d => d.MemberId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MembershipInvoice_Member");

            entity.HasOne(d => d.PaidByNavigation).WithMany()
                .HasForeignKey(d => d.PaidBy)
                .HasConstraintName("FK_MembershipInvoice_PaidBy");

            entity.HasOne(d => d.Subscription).WithOne(p => p.MembershipInvoice)
                .HasForeignKey<MembershipInvoice>(d => d.SubscriptionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MembershipInvoice_Subscription");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
