using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QL_HieuSuat.Models;

[Table("NHOM")]
public partial class Nhom
{
    [Key]
    [Column("MANHOM")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Manhom { get; set; }

    [Column("TENNHOM")]
    [StringLength(50)]
    public string? Tennhom { get; set; }

    [Column("NGAYTAO", TypeName = "datetime")]
    public DateTime? Ngaytao { get; set; }

    [InverseProperty("ManhomNavigation")]
    public virtual ICollection<Phancongcongviec> Phancongcongviecs { get; set; } = new List<Phancongcongviec>();

    [InverseProperty("ManhomNavigation")]
    public virtual ICollection<Thanhviennhom> Thanhviennhoms { get; set; } = new List<Thanhviennhom>();
}
