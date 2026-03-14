using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QL_HieuSuat.Models;

[Table("PHANCONGCONGVIEC")]
[Index("Macongviec", Name = "CV_CO_PHANCONG_FK")]
[Index("Manhom", Name = "NHOM_DUOC_GIAO_VIEC_FK")]
[Index("Manhanvien", Name = "NV_DUOC_GIAO_VIEC_FK")]
[Index("Maphongban", Name = "PB_DUOC_GIAO_VIEC_FK")]
public partial class Phancongcongviec
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("MAPHANCONG")]
    public int Maphancong { get; set; }

    [Column("MACONGVIEC")]
    public int Macongviec { get; set; }

    [Column("MAPHONGBAN")]
    public int? Maphongban { get; set; }

    [Column("MANHOM")]
    public int? Manhom { get; set; }

    [Column("MANHANVIEN")]
    public int? Manhanvien { get; set; }

    [Column("LOAIDOITUONG")]
    [StringLength(50)]
    public string? Loaidoituong { get; set; }

    [Column("NGAYGIAO", TypeName = "datetime")]
    public DateTime? Ngaygiao { get; set; }

    [Column("NGAYBATDAUDUKIEN", TypeName = "datetime")]
    public DateTime? Ngaybatdaudukien { get; set; }

    [Column("NGAYKETTHUCDUKIEN", TypeName = "datetime")]
    public DateTime? Ngayketthucdukien { get; set; }

    [Column("NGAYBATDAUTHUCTE", TypeName = "datetime")]
    public DateTime? Ngaybatdauthucte { get; set; }

    [Column("NGAYKETTHUCTHUCTE", TypeName = "datetime")]
    public DateTime? Ngayketthucthucte { get; set; }

    [ForeignKey("Macongviec")]
    [InverseProperty("Phancongcongviecs")]
    public virtual Congviec MacongviecNavigation { get; set; } = null!;

    [ForeignKey("Manhanvien")]
    [InverseProperty("Phancongcongviecs")]
    public virtual Nhanvien? ManhanvienNavigation { get; set; }

    [ForeignKey("Manhom")]
    [InverseProperty("Phancongcongviecs")]
    public virtual Nhom? ManhomNavigation { get; set; }

    [ForeignKey("Maphongban")]
    [InverseProperty("Phancongcongviecs")]
    public virtual Phongban? MaphongbanNavigation { get; set; }
}
