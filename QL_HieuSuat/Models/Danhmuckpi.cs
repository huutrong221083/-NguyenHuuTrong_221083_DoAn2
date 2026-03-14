using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QL_HieuSuat.Models;

[Table("DANHMUCKPI")]
public partial class Danhmuckpi
{
    [Key]
    [Column("MADOANHMUC")]
    public int Madoanhmuc { get; set; }

    [Column("TENDOANHMUC")]
    [StringLength(50)]
    public string? Tendoanhmuc { get; set; }

    [Column("TRONGSO")]
    public double? Trongso { get; set; }

    [InverseProperty("MadoanhmucNavigation")]
    public virtual ICollection<Ketquakpi> Ketquakpis { get; set; } = new List<Ketquakpi>();
}
