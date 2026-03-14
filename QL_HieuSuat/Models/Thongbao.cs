using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QL_HieuSuat.Models;

[Table("THONGBAO")]
public partial class Thongbao
{
    [Key]
    [Column("MATHONGBAO")]
    public int Mathongbao { get; set; }

    [Column("NOIDUNG")]
    public string? Noidung { get; set; }

    [Column("DADOC")]
    public bool? Dadoc { get; set; }

    [Column("THOIGIAN", TypeName = "datetime")]
    public DateTime? Thoigian { get; set; }

    [ForeignKey("Mathongbao")]
    [InverseProperty("Mathongbaos")]
    public virtual ICollection<Nhanvien> Manhanviens { get; set; } = new List<Nhanvien>();
}
