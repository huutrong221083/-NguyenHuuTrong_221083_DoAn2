using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QL_HieuSuat.Models;

[Table("CONGVIEC")]
[Index("ConMacongviec", Name = "CV_DEQUY_FK")]
[Index("Matrangthai", Name = "CV_THUOC_TRANGTHAI_FK")]
[Index("Maduan", Name = "DA_BAOGOM_CV_FK")]


public partial class Congviec
{
    [Key]
    [Column("MACONGVIEC")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Macongviec { get; set; }

    [Column("MADUAN")]
    public int Maduan { get; set; }

    [Column("MATRANGTHAI")]
    public int Matrangthai { get; set; }

    [Column("CON_MACONGVIEC")]
    public int? ConMacongviec { get; set; }

    [Column("TENCONGVIEC")]
    [StringLength(300)]
    public string? Tencongviec { get; set; }

    [Column("MOTA")]
    public string? Mota { get; set; }

    [Column("HANHOANTHANH", TypeName = "datetime")]
    public DateTime? Hanhoanthanh { get; set; }

    [Column("TRANGTHAI")]
    public int? Trangthai { get; set; }

    [Column("DOUUTIEN")]
    public int? Douutien { get; set; }

    [Column("DOKHO")]
    public int? Dokho { get; set; }

    [Column("DIEMCONGVIEC")]
    public double? Diemcongviec { get; set; }

    [Column("TONGCONGVIECCON")]
    public int? Tongcongvieccon { get; set; }

    [Column("MACONGVIECCHA")]
    public int? Macongvieccha { get; set; }

    [ForeignKey("ConMacongviec")]
    [InverseProperty("InverseConMacongviecNavigation")]
    public virtual Congviec? ConMacongviecNavigation { get; set; }

    [InverseProperty("ConMacongviecNavigation")]
    public virtual ICollection<Congviec> InverseConMacongviecNavigation { get; set; } = new List<Congviec>();

    [ForeignKey("Maduan")]
    [InverseProperty("Congviecs")]
    public virtual Duan MaduanNavigation { get; set; } = null!;

    [ForeignKey("Matrangthai")]
    [InverseProperty("Congviecs")]
    public virtual Trangthaicongviec? MatrangthaiNavigation { get; set; }

    [InverseProperty("MacongviecNavigation")]
    public virtual ICollection<Nhatkycongviec> Nhatkycongviecs { get; set; } = new List<Nhatkycongviec>();

    [InverseProperty("MacongviecNavigation")]
    public virtual ICollection<Phancongcongviec> Phancongcongviecs { get; set; } = new List<Phancongcongviec>();

    [ForeignKey("Macongviec")]
    [InverseProperty("Macongviecs")]
    public virtual ICollection<Tailieu> Matailieus { get; set; } = new List<Tailieu>();

    [InverseProperty("MacongviecNavigation")]
    public virtual ICollection<Binhluan> Binhluans { get; set; } = new List<Binhluan>();
}



