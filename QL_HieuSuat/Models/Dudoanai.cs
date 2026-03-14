using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QL_HieuSuat.Models;

[Table("DUDOANAI")]
[Index("Manhanvien", Name = "NV_DUOC_DUDOAN_FK")]
public partial class Dudoanai
{
    [Key]
    [Column("MADUDOAN")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Madudoan { get; set; }

    [Column("MANHANVIEN")]
    public int? Manhanvien { get; set; }

    [Column("THANG")]
    public int? Thang { get; set; }

    [Column("NAM")]
    public int? Nam { get; set; }

    [Column("DIEMDUDOAN")]
    public double? Diemdudoan { get; set; }

    [Column("DEXUATCAITHIEN")]
    public string? Dexuatcaithien { get; set; }

    [Column("THOIGIANDUDOAN", TypeName = "datetime")]
    public DateTime? Thoigiandudoan { get; set; }

    [Column("MADOITUONG")]
    public int? Madoituong { get; set; }

    [Column("XACSUATTREHAN")]
    public double? Xacsuattrehan { get; set; }

    [Column("GIOIYNGUONLUC")]
    public string? Gioiynguonluc { get; set; }

    [ForeignKey("Manhanvien")]
    [InverseProperty("Dudoanais")]
    public virtual Nhanvien ManhanvienNavigation { get; set; } = null!;
}
