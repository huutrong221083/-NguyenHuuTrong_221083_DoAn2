using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QL_HieuSuat.Models;

[Table("DUAN")]
public partial class Duan
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("MADUAN")]
    public int Maduan { get; set; }

    [Column("TENDUAN")]
    [StringLength(300)]
    public string? Tenduan { get; set; }

    [Column("MOTA")]
    public string? Mota { get; set; }

    [Column("NGAYBATDAU", TypeName = "datetime")]
    public DateTime? Ngaybatdau { get; set; }

    [Column("NGAYKETTHUC", TypeName = "datetime")]
    public DateTime? Ngayketthuc { get; set; }

    [Column("TRANGTHAI")]
    public int? Trangthai { get; set; }

    [InverseProperty("MaduanNavigation")]
    public virtual ICollection<Congviec> Congviecs { get; set; } = new List<Congviec>();
}
