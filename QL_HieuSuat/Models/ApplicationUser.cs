using Microsoft.AspNetCore.Identity;
using QL_HieuSuat.Models;

public class ApplicationUser : IdentityUser
{
    public int? MaNhanVien { get; set; }   // FK sang bảng NHANVIEN

    public Nhanvien? Nhanvien { get; set; }
}