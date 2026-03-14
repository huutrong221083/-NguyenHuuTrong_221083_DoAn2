using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QL_HieuSuat.Models;

[Table("TAILIEU")]
public partial class Tailieu
{
    [Key]
    [Column("MATAILIEU")]
    public int Matailieu { get; set; }

    [Column("TENTAILIEU")]
    [StringLength(300)]
    public string? Tentailieu { get; set; }

    [Column("HUONGDAN")]
    public string? Huongdan { get; set; }

    [ForeignKey("Matailieu")]
    [InverseProperty("Matailieus")]
    public virtual ICollection<Congviec> Macongviecs { get; set; } = new List<Congviec>();
}
