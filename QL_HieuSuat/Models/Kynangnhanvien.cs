using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QL_HieuSuat.Models;

[PrimaryKey("Manhanvien", "Makynang")]
[Table("KYNANGNHANVIEN")]
[Index("Makynang", Name = "KYNANGNHANVIEN2_FK")]
[Index("Manhanvien", Name = "KYNANGNHANVIEN_FK")]
public partial class Kynangnhanvien
{
    [Key]
    [Column("MANHANVIEN")]
    public int Manhanvien { get; set; }

    [Key]
    [Column("MAKYNANG")]
    public int Makynang { get; set; }

    [Column("CAPDO")]
    public int? Capdo { get; set; }

    [Column("SODUANDADUNGKYNANGNAY")]
    public int? Soduandadungkynangnay { get; set; }

    [ForeignKey("Makynang")]
    [InverseProperty("Kynangnhanviens")]
    public virtual Kynang MakynangNavigation { get; set; } = null!;

    [ForeignKey("Manhanvien")]
    [InverseProperty("Kynangnhanviens")]
    public virtual Nhanvien ManhanvienNavigation { get; set; } = null!;
}
