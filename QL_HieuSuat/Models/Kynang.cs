using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QL_HieuSuat.Models;

[Table("KYNANG")]
public partial class Kynang
{
    [Key]
    [Column("MAKYNANG")]
    public int Makynang { get; set; }

    [Column("TENKYNANG")]
    [StringLength(300)]
    public string? Tenkynang { get; set; }

    [InverseProperty("MakynangNavigation")]
    public virtual ICollection<Kynangnhanvien> Kynangnhanviens { get; set; } = new List<Kynangnhanvien>();
}
