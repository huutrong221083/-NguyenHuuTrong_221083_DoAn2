using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QL_HieuSuat.Models
{
    [Table("BINHLUAN")]
    public class Binhluan
    {
        [Key]
        [Column("MABINHLUAN")]
        public int Mabinhluan { get; set; }

        [Column("MACONGVIEC")]
        public int Macongviec { get; set; }

        [Column("MANHANVIEN")]
        public int Manhanvien { get; set; }

        [Column("NOIDUNG")]
        public string? Noidung { get; set; }

        [Column("NGAYTAO")]
        public DateTime Ngaytao { get; set; }

        // Navigation tới Congviec
        [ForeignKey("Macongviec")]
        public Congviec MacongviecNavigation { get; set; }

        // Navigation tới Nhanvien
        [ForeignKey("Manhanvien")]
        public Nhanvien ManhanvienNavigation { get; set; }
    }
}