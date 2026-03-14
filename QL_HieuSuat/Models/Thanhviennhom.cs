using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QL_HieuSuat.Models;

[PrimaryKey("Manhom", "Manhanvien")]
[Table("THANHVIENNHOM")]
[Index("Manhanvien", Name = "THANHVIENNHOM2_FK")]
[Index("Manhom", Name = "THANHVIENNHOM_FK")]
public partial class Thanhviennhom
{
    [Key]
    [Column("MANHOM")]
    public int Manhom { get; set; }

    [Key]
    [Column("MANHANVIEN")]
    public int Manhanvien { get; set; }

    [Column("NGAYGIANHAP", TypeName = "datetime")]
    public DateTime? Ngaygianhap { get; set; }

    [Column("VAITROTRONGNHOM")]
    [StringLength(100)]
    public string? Vaitrotrongnhom { get; set; }

    [ForeignKey("Manhanvien")]
    [InverseProperty("Thanhviennhoms")]
    public virtual Nhanvien ManhanvienNavigation { get; set; } = null!;

    [ForeignKey("Manhom")]
    [InverseProperty("Thanhviennhoms")]
    public virtual Nhom ManhomNavigation { get; set; } = null!;
}
