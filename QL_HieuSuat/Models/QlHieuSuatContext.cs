using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace QL_HieuSuat.Models;

public partial class QlHieuSuatContext : DbContext
{
    public QlHieuSuatContext()
    {
    }

    public QlHieuSuatContext(DbContextOptions<QlHieuSuatContext> options)
        : base(options)
    {
    }

    //public virtual DbSet<AspNetRole> AspNetRoles { get; set; }

    //public virtual DbSet<AspNetRoleClaim> AspNetRoleClaims { get; set; }

    //public virtual DbSet<AspNetUser> AspNetUsers { get; set; }

    //public virtual DbSet<AspNetUserClaim> AspNetUserClaims { get; set; }

    //public virtual DbSet<AspNetUserLogin> AspNetUserLogins { get; set; }

    //public virtual DbSet<AspNetUserToken> AspNetUserTokens { get; set; }

    public virtual DbSet<Congviec> Congviecs { get; set; }

    public virtual DbSet<Danhmuckpi> Danhmuckpis { get; set; }

    public virtual DbSet<Duan> Duans { get; set; }

    public virtual DbSet<Dudoanai> Dudoanais { get; set; }

    public virtual DbSet<Ketquakpi> Ketquakpis { get; set; }

    public virtual DbSet<Kynang> Kynangs { get; set; }

    public virtual DbSet<Kynangnhanvien> Kynangnhanviens { get; set; }

    public virtual DbSet<Nhanvien> Nhanviens { get; set; }

    public virtual DbSet<Nhatkycongviec> Nhatkycongviecs { get; set; }

    public virtual DbSet<Nhatkyhoatdong> Nhatkyhoatdongs { get; set; }

    public virtual DbSet<Nhom> Nhoms { get; set; }

    public virtual DbSet<Phancongcongviec> Phancongcongviecs { get; set; }

    public virtual DbSet<Phongban> Phongbans { get; set; }

    public virtual DbSet<Tailieu> Tailieus { get; set; }

    public virtual DbSet<Thanhviennhom> Thanhviennhoms { get; set; }

    public virtual DbSet<Thongbao> Thongbaos { get; set; }

    public virtual DbSet<Trangthaicongviec> Trangthaicongviecs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer("Name=DefaultConnection");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        //modelBuilder.Entity<AspNetRole>(entity =>
        //{
        //    entity.HasIndex(e => e.NormalizedName, "RoleNameIndex")
        //        .IsUnique()
        //        .HasFilter("([NormalizedName] IS NOT NULL)");
        //});

        //modelBuilder.Entity<AspNetUser>(entity =>
        //{
        //    entity.HasIndex(e => e.NormalizedUserName, "UserNameIndex")
        //        .IsUnique()
        //        .HasFilter("([NormalizedUserName] IS NOT NULL)");

        //    entity.HasMany(d => d.Roles).WithMany(p => p.Users)
        //        .UsingEntity<Dictionary<string, object>>(
        //            "AspNetUserRole",
        //            r => r.HasOne<AspNetRole>().WithMany().HasForeignKey("RoleId"),
        //            l => l.HasOne<AspNetUser>().WithMany().HasForeignKey("UserId"),
        //            j =>
        //            {
        //                j.HasKey("UserId", "RoleId");
        //                j.ToTable("AspNetUserRoles");
        //                j.HasIndex(new[] { "RoleId" }, "IX_AspNetUserRoles_RoleId");
        //            });
        //});

        modelBuilder.Entity<Congviec>(entity =>
        {
            entity.Property(e => e.Macongviec).ValueGeneratedNever();

            entity.HasOne(d => d.ConMacongviecNavigation).WithMany(p => p.InverseConMacongviecNavigation).HasConstraintName("FK_CONGVIEC_CV_DEQUY_CONGVIEC");

            entity.HasOne(d => d.MaduanNavigation).WithMany(p => p.Congviecs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CONGVIEC_DA_BAOGOM_DUAN");

            entity.HasOne(d => d.MatrangthaiNavigation).WithMany(p => p.Congviecs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CONGVIEC_CV_THUOC__TRANGTHA");

            entity.HasMany(d => d.Matailieus).WithMany(p => p.Macongviecs)
                .UsingEntity<Dictionary<string, object>>(
                    "CvDinhkemTl",
                    r => r.HasOne<Tailieu>().WithMany()
                        .HasForeignKey("Matailieu")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_CV_DINHK_CV_DINHKE_TAILIEU"),
                    l => l.HasOne<Congviec>().WithMany()
                        .HasForeignKey("Macongviec")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_CV_DINHK_CV_DINHKE_CONGVIEC"),
                    j =>
                    {
                        j.HasKey("Macongviec", "Matailieu");
                        j.ToTable("CV_DINHKEM_TL");
                        j.HasIndex(new[] { "Matailieu" }, "CV_DINHKEM_TL2_FK");
                        j.HasIndex(new[] { "Macongviec" }, "CV_DINHKEM_TL_FK");
                        j.IndexerProperty<int>("Macongviec").HasColumnName("MACONGVIEC");
                        j.IndexerProperty<int>("Matailieu").HasColumnName("MATAILIEU");
                    });
        });

        modelBuilder.Entity<Danhmuckpi>(entity =>
        {
            entity.Property(e => e.Madoanhmuc).ValueGeneratedNever();
        });

        modelBuilder.Entity<Duan>(entity =>
        {
            entity.Property(e => e.Maduan).ValueGeneratedNever();
        });

        modelBuilder.Entity<Dudoanai>(entity =>
        {
            entity.Property(e => e.Madudoan).ValueGeneratedNever();

            entity.HasOne(d => d.ManhanvienNavigation).WithMany(p => p.Dudoanais)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DUDOANAI_NV_DUOC_D_NHANVIEN");
        });

        modelBuilder.Entity<Ketquakpi>(entity =>
        {
            entity.Property(e => e.Maketqua).ValueGeneratedNever();

            entity.HasOne(d => d.MadoanhmucNavigation).WithMany(p => p.Ketquakpis)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_KETQUAKP_KPI_THUOC_DANHMUCK");

            entity.HasOne(d => d.ManhanvienNavigation).WithMany(p => p.Ketquakpis)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_KETQUAKP_NV_CO_KET_NHANVIEN");
        });

        modelBuilder.Entity<Kynang>(entity =>
        {
            entity.Property(e => e.Makynang).ValueGeneratedNever();
        });

        modelBuilder.Entity<Kynangnhanvien>(entity =>
        {
            entity.HasOne(d => d.MakynangNavigation).WithMany(p => p.Kynangnhanviens)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_KYNANGNH_KYNANGNHA_KYNANG");

            entity.HasOne(d => d.ManhanvienNavigation).WithMany(p => p.Kynangnhanviens)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_KYNANGNH_KYNANGNHA_NHANVIEN");
        });

        modelBuilder.Entity<Nhanvien>(entity =>
        {
            entity.Property(e => e.Manhanvien).ValueGeneratedNever();

            entity.HasOne(d => d.MaphongbanNavigation).WithMany(p => p.Nhanviens)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_NHANVIEN_PB_QUANLY_PHONGBAN");
        });

        modelBuilder.Entity<Nhatkycongviec>(entity =>
        {
            entity.Property(e => e.Manhatkycongviec).ValueGeneratedNever();

            entity.HasOne(d => d.MacongviecNavigation).WithMany(p => p.Nhatkycongviecs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_NHATKYCO_CV_CO_NHA_CONGVIEC");
        });

        modelBuilder.Entity<Nhatkyhoatdong>(entity =>
        {
            entity.Property(e => e.Manhatkyhoatdong).ValueGeneratedNever();

            entity.HasOne(d => d.ManhanvienNavigation).WithMany(p => p.Nhatkyhoatdongs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_NHATKYHO_NKHD_CUA__NHANVIEN");
        });

        modelBuilder.Entity<Nhom>(entity =>
        {
            entity.Property(e => e.Manhom).ValueGeneratedNever();
        });

        modelBuilder.Entity<Phancongcongviec>(entity =>
        {
            entity.Property(e => e.Maphancong).ValueGeneratedNever();

            entity.HasOne(d => d.MacongviecNavigation).WithMany(p => p.Phancongcongviecs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PHANCONG_CV_CO_PHA_CONGVIEC");

            entity.HasOne(d => d.ManhanvienNavigation).WithMany(p => p.Phancongcongviecs).HasConstraintName("FK_PHANCONG_NV_DUOC_G_NHANVIEN");

            entity.HasOne(d => d.ManhomNavigation).WithMany(p => p.Phancongcongviecs).HasConstraintName("FK_PHANCONG_NHOM_DUOC_NHOM");

            entity.HasOne(d => d.MaphongbanNavigation).WithMany(p => p.Phancongcongviecs).HasConstraintName("FK_PHANCONG_PB_DUOC_G_PHONGBAN");
        });

        modelBuilder.Entity<Phongban>(entity =>
        {
            entity.Property(e => e.Maphongban).ValueGeneratedNever();
        });

        modelBuilder.Entity<Tailieu>(entity =>
        {
            entity.Property(e => e.Matailieu).ValueGeneratedNever();
        });

        modelBuilder.Entity<Thanhviennhom>(entity =>
        {
            entity.HasOne(d => d.ManhanvienNavigation).WithMany(p => p.Thanhviennhoms)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_THANHVIE_THANHVIEN_NHANVIEN");

            entity.HasOne(d => d.ManhomNavigation).WithMany(p => p.Thanhviennhoms)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_THANHVIE_THANHVIEN_NHOM");
        });

        modelBuilder.Entity<Thongbao>(entity =>
        {
            entity.Property(e => e.Mathongbao).ValueGeneratedNever();

            entity.HasMany(d => d.Manhanviens).WithMany(p => p.Mathongbaos)
                .UsingEntity<Dictionary<string, object>>(
                    "ThongbaoChoNv",
                    r => r.HasOne<Nhanvien>().WithMany()
                        .HasForeignKey("Manhanvien")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_THONGBAO_THONGBAO__NHANVIEN"),
                    l => l.HasOne<Thongbao>().WithMany()
                        .HasForeignKey("Mathongbao")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_THONGBAO_THONGBAO__THONGBAO"),
                    j =>
                    {
                        j.HasKey("Mathongbao", "Manhanvien");
                        j.ToTable("THONGBAO_CHO_NV");
                        j.HasIndex(new[] { "Manhanvien" }, "THONGBAO_CHO_NV2_FK");
                        j.HasIndex(new[] { "Mathongbao" }, "THONGBAO_CHO_NV_FK");
                        j.IndexerProperty<int>("Mathongbao").HasColumnName("MATHONGBAO");
                        j.IndexerProperty<int>("Manhanvien").HasColumnName("MANHANVIEN");
                    });
        });

        modelBuilder.Entity<Trangthaicongviec>(entity =>
        {
            entity.Property(e => e.Matrangthai).ValueGeneratedNever();
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
