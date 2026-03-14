using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QL_HieuSuat.Models;

[Table("PHONGBAN")]
public partial class Phongban
{
    [Key]
    [Column("MAPHONGBAN")]
    public int Maphongban { get; set; }

    [Column("TENPHONGBAN")]
    [StringLength(50)]
    public string? Tenphongban { get; set; }

    [Column("MOTA")]
    public string? Mota { get; set; }

    [Column("MATRUONGPHONG")]
    public int? Matruongphong { get; set; }

    [ForeignKey("Matruongphong")]
    public virtual Nhanvien? MatruongphongNavigation { get; set; }

    [InverseProperty("MaphongbanNavigation")]
    public virtual ICollection<Nhanvien> Nhanviens { get; set; } = new List<Nhanvien>();

    [InverseProperty("MaphongbanNavigation")]
    public virtual ICollection<Phancongcongviec> Phancongcongviecs { get; set; } = new List<Phancongcongviec>();
}
