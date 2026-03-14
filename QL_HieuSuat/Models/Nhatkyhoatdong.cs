using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QL_HieuSuat.Models;

[Table("NHATKYHOATDONG")]
[Index("Manhanvien", Name = "NKHD_CUA_NHANVIEN_FK")]
public partial class Nhatkyhoatdong
{
    [Key]
    [Column("MANHATKYHOATDONG")]
    public int Manhatkyhoatdong { get; set; }

    [Column("MANHANVIEN")]
    public int Manhanvien { get; set; }

    [Column("HANHDONG")]
    public string? Hanhdong { get; set; }

    [Column("THOIGIAN", TypeName = "datetime")]
    public DateTime? Thoigian { get; set; }

    [ForeignKey("Manhanvien")]
    [InverseProperty("Nhatkyhoatdongs")]
    public virtual Nhanvien ManhanvienNavigation { get; set; } = null!;
}
