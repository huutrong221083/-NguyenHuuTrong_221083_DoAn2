using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QL_HieuSuat.Models;

[Table("TRANGTHAICONGVIEC")]
public partial class Trangthaicongviec
{
    [Key]
    [Column("MATRANGTHAI")]
    public int Matrangthai { get; set; }

    [Column("TENTRANGTHAI")]
    [StringLength(50)]
    public string? Tentrangthai { get; set; }

    [InverseProperty("MatrangthaiNavigation")]
    public virtual ICollection<Congviec> Congviecs { get; set; } = new List<Congviec>();
}
