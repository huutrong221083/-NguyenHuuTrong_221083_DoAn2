using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QL_HieuSuat.Models;

[Table("NHANVIEN")]
[Index("Maphongban", Name = "PB_QUANLY_NV_FK")]
public partial class Nhanvien
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("MANHANVIEN")]
    public int Manhanvien { get; set; }

    [Column("MAPHONGBAN")]
    public int? Maphongban { get; set; }

    [Column("HOTEN")]
    [StringLength(50)]
    public string? Hoten { get; set; }

    [Column("NGAYSINH")]
    public DateTime? Ngaysinh { get; set; }

    [Column("CCCD")]
    [StringLength(12)]
    [Unicode(false)]
    public string? Cccd { get; set; }

    [Column("DIACHI")]
    public string? Diachi { get; set; }

    [Column("GIOITINH")]
    public string? Gioitinh { get; set; }

    [Column("EMAIL")]
    [StringLength(300)]
    [Unicode(false)]
    public string? Email { get; set; }

    [Column("SDT")]
    [StringLength(10)]
    [Unicode(false)]
    public string? Sdt { get; set; }

    [Column("NGAYVAOLAM")]
    public DateTime? Ngayvaolam { get; set; }

    [Column("TRANGTHAI")]
    public int? Trangthai { get; set; }

    [Column("SONAMKINHNGHIEM")]
    public double? Sonamkinhnghiem { get; set; }

    [Column("DIEMKPI_TICHLUY")]
    public double? DiemkpiTichluy { get; set; }

    [InverseProperty("ManhanvienNavigation")]
    public virtual ICollection<Dudoanai> Dudoanais { get; set; } = new List<Dudoanai>();

    [InverseProperty("ManhanvienNavigation")]
    public virtual ICollection<Ketquakpi> Ketquakpis { get; set; } = new List<Ketquakpi>();

    [InverseProperty("ManhanvienNavigation")]
    public virtual ICollection<Kynangnhanvien> Kynangnhanviens { get; set; } = new List<Kynangnhanvien>();

    [ForeignKey("Maphongban")]
    [InverseProperty("Nhanviens")]
    //public virtual Phongban MaphongbanNavigation { get; set; } = null!;
    public virtual Phongban? MaphongbanNavigation { get; set; }

    [InverseProperty("ManhanvienNavigation")]
    public virtual ICollection<Nhatkyhoatdong> Nhatkyhoatdongs { get; set; } = new List<Nhatkyhoatdong>();

    [InverseProperty("ManhanvienNavigation")]
    public virtual ICollection<Phancongcongviec> Phancongcongviecs { get; set; } = new List<Phancongcongviec>();

    [InverseProperty("ManhanvienNavigation")]
    public virtual ICollection<Thanhviennhom> Thanhviennhoms { get; set; } = new List<Thanhviennhom>();

    [ForeignKey("Manhanvien")]
    [InverseProperty("Manhanviens")]
    public virtual ICollection<Thongbao> Mathongbaos { get; set; } = new List<Thongbao>();
}
