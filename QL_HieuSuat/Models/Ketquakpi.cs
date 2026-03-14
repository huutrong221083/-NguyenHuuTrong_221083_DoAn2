using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QL_HieuSuat.Models;

[Table("KETQUAKPI")]
[Index("Madoanhmuc", Name = "KPI_THUOC_DANHMUC_FK")]
[Index("Manhanvien", Name = "NV_CO_KETQUAKPI_FK")]
public partial class Ketquakpi
{
    [Key]
    [Column("MAKETQUA")]
    public int Maketqua { get; set; }

    [Column("MANHANVIEN")]
    public int Manhanvien { get; set; }

    [Column("MADOANHMUC")]
    public int Madoanhmuc { get; set; }

    [Column("DIEMSO")]
    public double? Diemso { get; set; }

    [Column("THANG")]
    public int? Thang { get; set; }

    [Column("NAM")]
    public int? Nam { get; set; }

    [ForeignKey("Madoanhmuc")]
    [InverseProperty("Ketquakpis")]
    public virtual Danhmuckpi MadoanhmucNavigation { get; set; } = null!;

    [ForeignKey("Manhanvien")]
    [InverseProperty("Ketquakpis")]
    public virtual Nhanvien ManhanvienNavigation { get; set; } = null!;
}
