using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QL_HieuSuat.Models;

[Table("NHATKYCONGVIEC")]
[Index("Macongviec", Name = "CV_CO_NHATKY_FK")]
public partial class Nhatkycongviec
{
    [Key]
    [Column("MANHATKYCONGVIEC")]
    public int Manhatkycongviec { get; set; }

    [Column("MACONGVIEC")]
    public int Macongviec { get; set; }

    [Column("PHANTRAMHOANTHANH")]
    public double? Phantramhoanthanh { get; set; }

    [Column("GHICHU")]
    public string? Ghichu { get; set; }

    [Column("NGAYCAPNHAT", TypeName = "datetime")]
    public DateTime? Ngaycapnhat { get; set; }

    [ForeignKey("Macongviec")]
    [InverseProperty("Nhatkycongviecs")]
    public virtual Congviec MacongviecNavigation { get; set; } = null!;
}
