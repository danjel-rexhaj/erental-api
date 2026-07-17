using System;
using System.Collections.Generic;
using ERental.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERental.Infrastructure.Persistence;

public partial class ERentalDbContext : DbContext
{
    public ERentalDbContext()
    {
    }

    public ERentalDbContext(DbContextOptions<ERentalDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Booking> Bookings { get; set; }

    public virtual DbSet<Car> Cars { get; set; }

    public virtual DbSet<CarAvailabilityBlock> CarAvailabilityBlocks { get; set; }

    public virtual DbSet<CarPhoto> CarPhotos { get; set; }

    public virtual DbSet<Company> Companies { get; set; }

    public virtual DbSet<CompanySubscription> CompanySubscriptions { get; set; }

    public virtual DbSet<CompanyVerification> CompanyVerifications { get; set; }

    public virtual DbSet<EmailVerification> EmailVerifications { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<Review> Reviews { get; set; }

    public virtual DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }

    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<PendingRegistration> PendingRegistrations { get; set; }
    public virtual DbSet<Notification> Notifications { get; set; }
    public virtual DbSet<WhatsappVerification> WhatsappVerifications { get; set; }

    // At runtime the app always configures this via appsettings.json / env vars in Program.cs (AddDbContext).
    // This fallback only kicks in for design-time tooling (e.g. `dotnet ef`) run directly against this class,
    // and reads from an env var instead of a hardcoded secret so nothing sensitive lives in source control.
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var connectionString = Environment.GetEnvironmentVariable("ERENTAL_CONNECTION_STRING")
                ?? "Host=localhost;Port=5432;Database=ERental;Username=postgres;Password=CHANGE_ME";
            optionsBuilder.UseNpgsql(connectionString);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(e => e.BookingId).HasName("bookings_pkey");

            entity.ToTable("bookings");

            entity.Property(e => e.BookingId).HasColumnName("booking_id");
            entity.Property(e => e.CarId).HasColumnName("car_id");
            entity.Property(e => e.CmimiTotal)
                .HasPrecision(10, 2)
                .HasColumnName("cmimi_total");
            entity.Property(e => e.DataFillimit).HasColumnName("data_fillimit");
            entity.Property(e => e.DataKrijimit)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("data_krijimit");
            entity.Property(e => e.DataPerfundimit).HasColumnName("data_perfundimit");
            entity.Property(e => e.Statusi)
                .HasMaxLength(20)
                .HasDefaultValueSql("'pending'::character varying")
                .HasColumnName("statusi");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Car).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.CarId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("bookings_car_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("bookings_user_id_fkey");
        });

        modelBuilder.Entity<Car>(entity =>
        {
            entity.HasKey(e => e.CarId).HasName("cars_pkey");

            entity.ToTable("cars");

            entity.HasIndex(e => e.Targa, "cars_targa_key").IsUnique();

            entity.Property(e => e.CarId).HasColumnName("car_id");
            entity.Property(e => e.CmimiDites)
                .HasPrecision(8, 2)
                .HasColumnName("cmimi_dites");
            entity.Property(e => e.CompanyId).HasColumnName("company_id");
            entity.Property(e => e.Karburanti)
                .HasMaxLength(20)
                .HasDefaultValueSql("'diesel'::character varying")
                .HasColumnName("karburanti");
            entity.Property(e => e.Kategoria)
                .HasMaxLength(20)
                .HasDefaultValueSql("'economy'::character varying")
                .HasColumnName("kategoria");
            entity.Property(e => e.Klimatizimi)
                .HasDefaultValue(true)
                .HasColumnName("klimatizimi");
            entity.Property(e => e.Km)
                .HasDefaultValue(0)
                .HasColumnName("km");
            entity.Property(e => e.Marka)
                .HasMaxLength(50)
                .HasColumnName("marka");
            entity.Property(e => e.Modeli)
                .HasMaxLength(50)
                .HasColumnName("modeli");
            entity.Property(e => e.Ngjyra)
                .HasMaxLength(30)
                .HasColumnName("ngjyra");
            entity.Property(e => e.NumriVendeve)
                .HasDefaultValue(5)
                .HasColumnName("numri_vendeve");
            entity.Property(e => e.Statusi)
                .HasMaxLength(20)
                .HasDefaultValueSql("'active'::character varying")
                .HasColumnName("statusi");
            entity.Property(e => e.Targa)
                .HasMaxLength(20)
                .HasColumnName("targa");
            entity.Property(e => e.Transmisioni)
                .HasMaxLength(20)
                .HasDefaultValueSql("'manual'::character varying")
                .HasColumnName("transmisioni");
            entity.Property(e => e.Viti).HasColumnName("viti");
            entity.Property(e => e.Pershkrimi).HasColumnName("pershkrimi");
            entity.Property(e => e.Kubatura).HasColumnName("kubatura");
            entity.Property(e => e.Cilindra).HasColumnName("cilindra");

            entity.HasOne(d => d.Company).WithMany(p => p.Cars)
                .HasForeignKey(d => d.CompanyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("cars_company_id_fkey");
        });

        modelBuilder.Entity<CarAvailabilityBlock>(entity =>
        {
            entity.HasKey(e => e.BlockId).HasName("car_availability_blocks_pkey");

            entity.ToTable("car_availability_blocks");

            entity.Property(e => e.BlockId).HasColumnName("block_id");
            entity.Property(e => e.Arsyeja)
                .HasMaxLength(20)
                .HasDefaultValueSql("'manual_block'::character varying")
                .HasColumnName("arsyeja");
            entity.Property(e => e.CarId).HasColumnName("car_id");
            entity.Property(e => e.DataFillimit).HasColumnName("data_fillimit");
            entity.Property(e => e.DataKrijimit)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("data_krijimit");
            entity.Property(e => e.DataPerfundimit).HasColumnName("data_perfundimit");
            entity.Property(e => e.Shenim)
                .HasMaxLength(255)
                .HasColumnName("shenim");

            entity.HasOne(d => d.Car).WithMany(p => p.CarAvailabilityBlocks)
                .HasForeignKey(d => d.CarId)
                .HasConstraintName("car_availability_blocks_car_id_fkey");
        });

        modelBuilder.Entity<CarPhoto>(entity =>
        {
            entity.HasKey(e => e.PhotoId).HasName("car_photos_pkey");

            entity.ToTable("car_photos");

            entity.Property(e => e.PhotoId).HasColumnName("photo_id");
            entity.Property(e => e.CarId).HasColumnName("car_id");
            entity.Property(e => e.DataNgarkimit)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("data_ngarkimit");
            entity.Property(e => e.EshteKryesore)
                .HasDefaultValue(false)
                .HasColumnName("eshte_kryesore");
            entity.Property(e => e.UrlFotos)
                .HasMaxLength(500)
                .HasColumnName("url_fotos");
            entity.Property(e => e.Kategoria)
                .HasMaxLength(30)
                .HasColumnName("kategoria");

            entity.HasOne(d => d.Car).WithMany(p => p.CarPhotos)
                .HasForeignKey(d => d.CarId)
                .HasConstraintName("car_photos_car_id_fkey");
        });

        modelBuilder.Entity<Company>(entity =>
        {
            entity.HasKey(e => e.CompanyId).HasName("companies_pkey");

            entity.ToTable("companies");

            entity.HasIndex(e => e.Email, "companies_email_key").IsUnique();

            entity.HasIndex(e => e.Nipt, "companies_nipt_key").IsUnique();

            entity.Property(e => e.CompanyId).HasColumnName("company_id");
            entity.Property(e => e.Adresa)
                .HasMaxLength(255)
                .HasColumnName("adresa");
            entity.Property(e => e.BillingModel)
                .HasMaxLength(20)
                .HasDefaultValueSql("'commission'::character varying")
                .HasColumnName("billing_model");
            entity.Property(e => e.CommissionRate)
                .HasPrecision(5, 2)
                .HasDefaultValue(10.00m)
                .HasColumnName("commission_rate");
            entity.Property(e => e.DataRegjistrimit)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("data_regjistrimit");
            entity.Property(e => e.DataVerifikimit)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("data_verifikimit");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .HasColumnName("email");
            entity.Property(e => e.Emri)
                .HasMaxLength(100)
                .HasColumnName("emri");
            entity.Property(e => e.EshteVerifikuar)
                .HasDefaultValue(false)
                .HasColumnName("eshte_verifikuar");
            entity.Property(e => e.Nipt)
                .HasMaxLength(20)
                .HasColumnName("nipt");
            entity.Property(e => e.OwnerUserId).HasColumnName("owner_user_id");
            entity.Property(e => e.Qyteti)
                .HasMaxLength(50)
                .HasColumnName("qyteti");
            entity.Property(e => e.Statusi)
                .HasMaxLength(20)
                .HasDefaultValueSql("'active'::character varying")
                .HasColumnName("statusi");
            entity.Property(e => e.Telefoni)
                .HasMaxLength(20)
                .HasColumnName("telefoni");

            entity.HasOne(d => d.OwnerUser).WithMany(p => p.Companies)
                .HasForeignKey(d => d.OwnerUserId)
                .HasConstraintName("companies_owner_user_id_fkey");
        });

        modelBuilder.Entity<CompanySubscription>(entity =>
        {
            entity.HasKey(e => e.SubscriptionId).HasName("company_subscriptions_pkey");

            entity.ToTable("company_subscriptions");

            entity.Property(e => e.SubscriptionId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("subscription_id");
            entity.Property(e => e.CompanyId).HasColumnName("company_id");
            entity.Property(e => e.DataFillimit).HasColumnName("data_fillimit");
            entity.Property(e => e.DataPerfundimit).HasColumnName("data_perfundimit");
            entity.Property(e => e.PlanId).HasColumnName("plan_id");
            entity.Property(e => e.Statusi)
                .HasMaxLength(20)
                .HasDefaultValueSql("'active'::character varying")
                .HasColumnName("statusi");

            entity.HasOne(d => d.Company).WithMany(p => p.CompanySubscriptions)
                .HasForeignKey(d => d.CompanyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("company_subscriptions_company_id_fkey");

            entity.HasOne(d => d.Plan).WithMany(p => p.CompanySubscriptions)
                .HasForeignKey(d => d.PlanId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("company_subscriptions_plan_id_fkey");
        });

        modelBuilder.Entity<CompanyVerification>(entity =>
        {
            entity.HasKey(e => e.VerificationId).HasName("company_verifications_pkey");

            entity.ToTable("company_verifications");

            entity.Property(e => e.VerificationId).HasColumnName("verification_id");
            entity.Property(e => e.CertifikataUrl)
                .HasMaxLength(500)
                .HasColumnName("certifikata_url");
            entity.Property(e => e.CompanyId).HasColumnName("company_id");
            entity.Property(e => e.DataDorezimit)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("data_dorezimit");
            entity.Property(e => e.DataShqyrtimit)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("data_shqyrtimit");
            entity.Property(e => e.Nipt)
                .HasMaxLength(20)
                .HasColumnName("nipt");
            entity.Property(e => e.ShenimeAdmin).HasColumnName("shenime_admin");
            entity.Property(e => e.Statusi)
                .HasMaxLength(20)
                .HasDefaultValueSql("'pending'::character varying")
                .HasColumnName("statusi");

            entity.HasOne(d => d.Company).WithMany(p => p.CompanyVerifications)
                .HasForeignKey(d => d.CompanyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("company_verifications_company_id_fkey");
        });

        modelBuilder.Entity<EmailVerification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("email_verifications_pkey");

            entity.ToTable("email_verifications");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DataKrijimit)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("data_krijimit");
            entity.Property(e => e.DataSkadimit)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("data_skadimit");
            entity.Property(e => e.Perdorur)
                .HasDefaultValue(false)
                .HasColumnName("perdorur");
            entity.Property(e => e.Token)
                .HasMaxLength(255)
                .HasColumnName("token");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.EmailVerifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("email_verifications_user_id_fkey");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.PaymentId).HasName("payments_pkey");

            entity.ToTable("payments");

            entity.Property(e => e.PaymentId).HasColumnName("payment_id");
            entity.Property(e => e.BookingId).HasColumnName("booking_id");
            entity.Property(e => e.DataPageses)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("data_pageses");
            entity.Property(e => e.Komisioni)
                .HasPrecision(10, 2)
                .HasDefaultValue(0m)
                .HasColumnName("komisioni");
            entity.Property(e => e.MetodaPageses)
                .HasMaxLength(20)
                .HasDefaultValueSql("'card'::character varying")
                .HasColumnName("metoda_pageses");
            entity.Property(e => e.ShumaBiznesit)
                .HasPrecision(10, 2)
                .HasColumnName("shuma_biznesit");
            entity.Property(e => e.ShumaTotale)
                .HasPrecision(10, 2)
                .HasColumnName("shuma_totale");
            entity.Property(e => e.Statusi)
                .HasMaxLength(20)
                .HasDefaultValueSql("'pending'::character varying")
                .HasColumnName("statusi");

            entity.HasOne(d => d.Booking).WithMany(p => p.Payments)
                .HasForeignKey(d => d.BookingId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("payments_booking_id_fkey");
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(e => e.ReviewId).HasName("reviews_pkey");

            entity.ToTable("reviews");

            entity.Property(e => e.ReviewId).HasColumnName("review_id");
            entity.Property(e => e.BookingId).HasColumnName("booking_id");
            entity.Property(e => e.CompanyId).HasColumnName("company_id");
            entity.Property(e => e.Data)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("data");
            entity.Property(e => e.Koment).HasColumnName("koment");
            entity.Property(e => e.Rating).HasColumnName("rating");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Booking).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.BookingId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("reviews_booking_id_fkey");

            entity.HasOne(d => d.Company).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.CompanyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("reviews_company_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("reviews_user_id_fkey");
        });

        modelBuilder.Entity<SubscriptionPlan>(entity =>
        {
            entity.HasKey(e => e.PlanId).HasName("subscription_plans_pkey");

            entity.ToTable("subscription_plans");

            entity.Property(e => e.PlanId).HasColumnName("plan_id");
            entity.Property(e => e.CmimiMujor)
                .HasPrecision(8, 2)
                .HasColumnName("cmimi_mujor");
            entity.Property(e => e.Emri)
                .HasMaxLength(50)
                .HasColumnName("emri");
            entity.Property(e => e.LimitMakinash).HasColumnName("limit_makinash");
            entity.Property(e => e.Pershkrimi).HasColumnName("pershkrimi");
        });




        modelBuilder.Entity<PendingRegistration>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pending_registrations_pkey");

            entity.ToTable("pending_registrations");

            entity.HasIndex(e => e.Email, "pending_registrations_email_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Email).HasMaxLength(100).HasColumnName("email");
            entity.Property(e => e.Emri).HasMaxLength(50).HasColumnName("emri");
            entity.Property(e => e.Mbiemri).HasMaxLength(50).HasColumnName("mbiemri");
            entity.Property(e => e.PasswordHash).HasMaxLength(255).HasColumnName("password_hash");
            entity.Property(e => e.Telefoni).HasMaxLength(20).HasColumnName("telefoni");
            entity.Property(e => e.HasWhatsapp).HasDefaultValue(false).HasColumnName("has_whatsapp");
            entity.Property(e => e.Code).HasMaxLength(10).HasColumnName("code");
            entity.Property(e => e.DataKrijimit)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("data_krijimit");
            entity.Property(e => e.DataSkadimit)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("data_skadimit");
        });


        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("notifications_pkey");
            entity.ToTable("notifications");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Title).HasMaxLength(200).HasColumnName("title");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.IsRead).HasDefaultValue(false).HasColumnName("is_read");
            entity.Property(e => e.DataKrijimit)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("data_krijimit");
            entity.Property(e => e.BookingId).HasColumnName("booking_id");
            entity.Property(e => e.Target).HasMaxLength(20).HasColumnName("target");
        });


        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.DataRegjistrimit)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("data_regjistrimit");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .HasColumnName("email");
            entity.Property(e => e.EmailVerified)
                .HasDefaultValue(false)
                .HasColumnName("email_verified");
            entity.Property(e => e.Emri)
                .HasMaxLength(50)
                .HasColumnName("emri");
            entity.Property(e => e.HasWhatsapp)
                .HasDefaultValue(false)
                .HasColumnName("has_whatsapp");
            entity.Property(e => e.WhatsappVerified)
                .HasDefaultValue(false)
                .HasColumnName("whatsapp_verified");
            entity.Property(e => e.Mbiemri)
                .HasMaxLength(50)
                .HasColumnName("mbiemri");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasColumnName("password_hash");
            entity.Property(e => e.Telefoni)
                .HasMaxLength(20)
                .HasColumnName("telefoni");
        });

        modelBuilder.Entity<WhatsappVerification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("whatsapp_verifications_pkey");
            entity.ToTable("whatsapp_verifications");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Code).HasMaxLength(10).HasColumnName("code");
            entity.Property(e => e.Statusi)
                .HasMaxLength(20)
                .HasDefaultValueSql("'pending'::character varying")
                .HasColumnName("statusi");
            entity.Property(e => e.DataKrijimit)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("data_krijimit");
            entity.Property(e => e.DataShqyrtimit)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("data_shqyrtimit");

            entity.HasOne(d => d.User).WithMany(p => p.WhatsappVerifications)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("whatsapp_verifications_user_id_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
