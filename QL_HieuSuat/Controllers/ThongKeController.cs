using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QL_HieuSuat.AI;
using QL_HieuSuat.Models;

namespace QL_HieuSuat.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ThongKeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ThongKeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index(int? nam, int? thang, int? phongban, int? nhom)
        {

            // =====================
            // Tổng quan
            // =====================

            ViewBag.TongNhanVien = _context.Nhanviens.Count();
            ViewBag.TongDuAn = _context.Duans.Count();
            ViewBag.TongCongViec = _context.Congviecs.Count();
            ViewBag.TongNhom = _context.Nhoms.Count();


            // =====================
            // Trạng thái công việc
            // =====================

            var trangThai = _context.Trangthaicongviecs
                .Select(t => new
                {
                    TenTrangThai = t.Tentrangthai,
                    SoLuong = t.Congviecs.Count()
                }).ToList();

            ViewBag.TrangThaiLabels = trangThai.Select(x => x.TenTrangThai);
            ViewBag.TrangThaiData = trangThai.Select(x => x.SoLuong);


            // =====================
            // KPI theo tháng
            // =====================

                var kpi = _context.Ketquakpis
            .Include(x => x.ManhanvienNavigation)
            .ThenInclude(x => x.MaphongbanNavigation)
            .AsQueryable();

            if (phongban.HasValue)
            {
                kpi = kpi.Where(x => x.ManhanvienNavigation.Maphongban == phongban.Value);
            }

            if (nhom.HasValue)
            {
                kpi = kpi.Where(x => x.ManhanvienNavigation.Thanhviennhoms
                    .Any(t => t.Manhom == nhom));
            }

            if (nam.HasValue)
                kpi = kpi.Where(x => x.Nam == nam.Value);

            if (thang.HasValue)
                kpi = kpi.Where(x => x.Thang == thang.Value);

            var kpiTheoThang = Enumerable.Range(1, 12)
    .Select(th =>
    {
        var data = kpi.Where(x => x.Thang == th);
        return new
        {
            Thang = th,
            Diem = data.Any() ? data.Average(x => x.Diemso) : 0
        };
    }).ToList();



            ViewBag.Thang = kpiTheoThang.Select(x => "T" + x.Thang);
            ViewBag.DiemKPI = kpiTheoThang.Select(x => x.Diem);


            // =====================
            // Báo cáo hiệu suất nhân sự
            // =====================

            var baoCaoNhanVien = kpi
                .AsEnumerable()
                .GroupBy(x => new
                {
                    x.Manhanvien,
                    TenNhanVien = x.ManhanvienNavigation?.Hoten ?? "Chưa cập nhật",
                    PhongBan = x.ManhanvienNavigation?.MaphongbanNavigation?.Tenphongban ?? "Chưa có phòng ban"
                })
                .Select(g => new
                {
                    MaNhanVien = g.Key.Manhanvien,
                    TenNhanVien = g.Key.TenNhanVien,
                    PhongBan = g.Key.PhongBan,
                    DiemTrungBinh = Math.Round(g.Average(x => x.Diemso) ?? 0, 2),
                    DiemGanNhat = Math.Round(g
                        .OrderByDescending(x => x.Nam)
                        .ThenByDescending(x => x.Thang)
                        .Select(x => x.Diemso)
                        .FirstOrDefault() ?? 0, 2),
                    SoKyDanhGia = g.Count()
                })
                .OrderByDescending(x => x.DiemTrungBinh)
                .ToList();

            ViewBag.BaoCaoNhanVien = baoCaoNhanVien;

            var soNhanVienBaoCao = baoCaoNhanVien.Count();
            var soDatMucTot = baoCaoNhanVien.Count(x => x.DiemTrungBinh >= 80);
            var soCanhBao = baoCaoNhanVien.Count(x => x.DiemTrungBinh < 60);
            var tyLeDatMucTot = soNhanVienBaoCao == 0 ? 0d : Math.Round((double)soDatMucTot * 100 / soNhanVienBaoCao, 2);

            ViewBag.BaoCaoSoNhanVien = soNhanVienBaoCao;
            ViewBag.BaoCaoSoDatMucTot = soDatMucTot;
            ViewBag.BaoCaoSoCanhBao = soCanhBao;
            ViewBag.BaoCaoTyLeDatMucTot = tyLeDatMucTot;

            ViewBag.BaoCaoCanhBaoNhanVien = baoCaoNhanVien
                .Where(x => x.DiemTrungBinh < 60)
                .Take(5)
                .ToList();


            // =====================
            // Top nhân viên KPI
            // =====================

            var topNhanVien = baoCaoNhanVien
                .Select(x => new
                {
                    Ten = x.TenNhanVien,
                    Diem = x.DiemTrungBinh
                })
                .OrderByDescending(x => x.Diem)
                .Take(10)
                .ToList();

            ViewBag.TopNVLabels = topNhanVien.Select(x => x.Ten);
            ViewBag.TopNVData = topNhanVien.Select(x => x.Diem);

            // =====================
            // Tiến độ dự án
            // =====================

            var duAnTienDo = _context.Duans
                .Select(d => new
                {
                    TenDuAn = d.Tenduan,
                    TongCV = d.Congviecs.Count(),
                    HoanThanh = d.Congviecs.Count(c => c.Matrangthai == 3)
                }).ToList();

            ViewBag.DuAnTienDo = duAnTienDo;


            // =====================
            // Thống kê kỹ năng
            // =====================

            var kyNang = _context.Kynangs
                .Select(k => new
                {
                    TenKyNang = k.Tenkynang,
                    SoNhanVien = k.Kynangnhanviens.Count()
                }).ToList();

            ViewBag.KyNangLabels = kyNang.Select(x => x.TenKyNang);
            ViewBag.KyNangData = kyNang.Select(x => x.SoNhanVien);



            // =====================
            // Công việc theo phòng ban
            // =====================

            var congViecPhong = _context.Phancongcongviecs
                .Include(x => x.MaphongbanNavigation)
                .GroupBy(x => x.MaphongbanNavigation.Tenphongban)
                .Select(g => new
                {
                    Phong = g.Key,
                    SoLuong = g.Count()
                }).ToList();

            ViewBag.PhongLabels = congViecPhong.Select(x => x.Phong);
            ViewBag.PhongData = congViecPhong.Select(x => x.SoLuong);



            // =====================
            // Công việc theo nhóm
            // =====================

            var congViecNhom = _context.Phancongcongviecs
                .Include(x => x.ManhomNavigation)
                .GroupBy(x => x.ManhomNavigation.Tennhom)
                .Select(g => new
                {
                    Nhom = g.Key,
                    SoLuong = g.Count()
                }).ToList();

            ViewBag.NhomLabels = congViecNhom.Select(x => x.Nhom);
            ViewBag.NhomData = congViecNhom.Select(x => x.SoLuong);


            // =====================
            // Dropdown dữ liệu
            // =====================

            ViewBag.PhongBan = _context.Phongbans.ToList();
            ViewBag.Nhom = _context.Nhoms.ToList();



            // =====================
            // AI KPI TRAINING
            // =====================

            var kpiTraining = _context.Nhanviens
            .Select(n => new KPIModel
            {

                Sonamkinhnghiem =
            n.Ngayvaolam.HasValue
            ? DateTime.Now.Year -
            n.Ngayvaolam.Value.Year
            : 0,

            //    DiemkpiTichluy =
            //n.Ketquakpis.Any()
            //? (float)n.Ketquakpis.Average(x => x.Diemso)
            //: 0,

                SoCongViec =
            n.Phancongcongviecs.Count(),

                DoKhoTrungBinh =
            n.Phancongcongviecs.Any()
            ?
            (float)n.Phancongcongviecs
            .Average(p => p.MacongviecNavigation.Dokho ?? 0)
            : 0,

                DoUuTienTrungBinh =
            n.Phancongcongviecs.Any()
            ?
            (float)n.Phancongcongviecs
            .Average(p => p.MacongviecNavigation.Douutien ?? 0)
            : 0,

                TyLeTreHan =
            n.Phancongcongviecs.Any()
            ?
            (float)n.Phancongcongviecs
            .Count(p =>
            p.Ngayketthucthucte >
            p.Ngayketthucdukien)
            /
            n.Phancongcongviecs.Count()
            : 0,

                KPIThucTe =
            n.Ketquakpis.Any()
            ?
            (float)n.Ketquakpis
            .OrderByDescending(x => x.Nam)
            .ThenByDescending(x => x.Thang)
            .Select(x => x.Diemso)
            .First()
            : 0

            }).ToList();


            // =====================
            // AI KPI PREDICTION
            // =====================

            if (kpiTraining.Count > 5)
            {

                KPIService ai =
                new KPIService();

                ai.Train(kpiTraining);

                var predict =
                ai.Predict(kpiTraining.First());

                ViewBag.AIKPI =
                Math.Round(predict, 2);

            }
            else
            {

                ViewBag.AIKPI = 0;

            }



            return View();
        }

        // Xuat bao cao (Excel/PDF)
        [HttpGet]
        public IActionResult ExportReport(string format, string reportType, int? nam, int? thang, int? phongban, int? nhom)
        {
            var exportFormat = string.IsNullOrWhiteSpace(format) ? "excel" : format.Trim().ToLowerInvariant();
            var loaiBaoCao = string.IsNullOrWhiteSpace(reportType) ? "hieu-suat-nhan-vien" : reportType.Trim().ToLowerInvariant();

            var reportTitle = loaiBaoCao switch
            {
                "hieu-suat-nhan-vien" => "Bao cao hieu suat nhan vien",
                "tien-do-du-an" => "Bao cao tien do du an",
                "trang-thai-cong-viec" => "Bao cao trang thai cong viec",
                "thong-ke-ky-nang" => "Bao cao thong ke ky nang",
                _ => "Bao cao tong quan he thong"
            };

            var rows = BuildReportRows(loaiBaoCao, nam, thang, phongban, nhom);
            var generatedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            var boLoc = BuildFilterText(nam, thang, phongban, nhom);
            var filePrefix = loaiBaoCao.Replace("-", "_");

            if (exportFormat == "pdf")
            {
                var pdf = Document.Create(document =>
                {
                    document.Page(page =>
                    {
                        page.Margin(25);
                        page.Size(PageSizes.A4.Landscape());

                        page.Header().Column(column =>
                        {
                            column.Item().Text(reportTitle).SemiBold().FontSize(16);
                            column.Item().Text($"Ngay xuat: {generatedAt}").FontSize(10).FontColor(Colors.Grey.Darken1);
                            column.Item().Text($"Bo loc: {boLoc}").FontSize(10).FontColor(Colors.Grey.Darken1);
                        });

                        page.Content().PaddingTop(12).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                for (var i = 0; i < rows.Headers.Count; i++)
                                {
                                    columns.RelativeColumn();
                                }
                            });

                            table.Header(header =>
                            {
                                foreach (var head in rows.Headers)
                                {
                                    header.Cell().Element(PdfCellStyle).Background(Colors.Grey.Lighten3).Text(head).SemiBold();
                                }
                            });

                            foreach (var row in rows.Data)
                            {
                                foreach (var cell in row)
                                {
                                    table.Cell().Element(PdfCellStyle).Text(cell ?? string.Empty);
                                }
                            }
                        });

                        page.Footer().AlignRight().Text(text =>
                        {
                            text.Span("Trang ");
                            text.CurrentPageNumber();
                            text.Span("/");
                            text.TotalPages();
                        });
                    });
                }).GeneratePdf();

                return File(pdf, "application/pdf", $"{filePrefix}_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
            }

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("ThongKe");

                worksheet.Cell(1, 1).Value = reportTitle;
                worksheet.Cell(2, 1).Value = $"Ngay xuat: {generatedAt}";
                worksheet.Cell(3, 1).Value = $"Bo loc: {boLoc}";

                for (int i = 0; i < rows.Headers.Count; i++)
                {
                    worksheet.Cell(5, i + 1).Value = rows.Headers[i];
                    worksheet.Cell(5, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(5, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                var startRow = 6;
                for (int r = 0; r < rows.Data.Count; r++)
                {
                    for (int c = 0; c < rows.Data[r].Count; c++)
                    {
                        worksheet.Cell(startRow + r, c + 1).Value = rows.Data[r][c];
                    }
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return File(
                        stream.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"{filePrefix}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
                }
            }
        }

        [HttpGet]
        public IActionResult ExportExcel()
        {
            return ExportReport("excel", "hieu-suat-nhan-vien", null, null, null, null);
        }

        private static IContainer PdfCellStyle(IContainer container)
        {
            return container.Border(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5).PaddingHorizontal(4);
        }

        private string BuildFilterText(int? nam, int? thang, int? phongban, int? nhom)
        {
            var tenPhong = phongban.HasValue
                ? _context.Phongbans.Where(p => p.Maphongban == phongban.Value).Select(p => p.Tenphongban).FirstOrDefault() ?? "Khong xac dinh"
                : "Tat ca";

            var tenNhom = nhom.HasValue
                ? _context.Nhoms.Where(n => n.Manhom == nhom.Value).Select(n => n.Tennhom).FirstOrDefault() ?? "Khong xac dinh"
                : "Tat ca";

            return $"Nam: {(nam.HasValue ? nam.Value.ToString() : "Tat ca")}; Thang: {(thang.HasValue ? thang.Value.ToString() : "Tat ca")}; Phong ban: {tenPhong}; Nhom: {tenNhom}";
        }

        private ReportRows BuildReportRows(string reportType, int? nam, int? thang, int? phongban, int? nhom)
        {
            switch (reportType)
            {
                case "hieu-suat-nhan-vien":
                    {
                        var kpiQuery = _context.Ketquakpis
                            .Include(x => x.ManhanvienNavigation)
                            .ThenInclude(x => x.MaphongbanNavigation)
                            .AsQueryable();

                        if (phongban.HasValue)
                            kpiQuery = kpiQuery.Where(x => x.ManhanvienNavigation.Maphongban == phongban.Value);

                        if (nhom.HasValue)
                            kpiQuery = kpiQuery.Where(x => x.ManhanvienNavigation.Thanhviennhoms.Any(t => t.Manhom == nhom));

                        if (nam.HasValue)
                            kpiQuery = kpiQuery.Where(x => x.Nam == nam.Value);

                        if (thang.HasValue)
                            kpiQuery = kpiQuery.Where(x => x.Thang == thang.Value);

                        var data = kpiQuery
                            .AsEnumerable()
                            .GroupBy(x => new
                            {
                                x.Manhanvien,
                                TenNhanVien = x.ManhanvienNavigation?.Hoten ?? "Chua cap nhat",
                                PhongBan = x.ManhanvienNavigation?.MaphongbanNavigation?.Tenphongban ?? "Chua co phong ban"
                            })
                            .Select(g => new
                            {
                                MaNhanVien = g.Key.Manhanvien,
                                TenNhanVien = g.Key.TenNhanVien,
                                PhongBan = g.Key.PhongBan,
                                DiemTrungBinh = Math.Round(g.Average(x => x.Diemso) ?? 0, 2),
                                DiemGanNhat = Math.Round(g.OrderByDescending(x => x.Nam).ThenByDescending(x => x.Thang).Select(x => x.Diemso).FirstOrDefault() ?? 0, 2),
                                SoKyDanhGia = g.Count()
                            })
                            .OrderByDescending(x => x.DiemTrungBinh)
                            .ToList();

                        var rows = data.Select((x, idx) => new List<string>
                        {
                            (idx + 1).ToString(),
                            x.MaNhanVien.ToString(),
                            x.TenNhanVien,
                            x.PhongBan,
                            x.DiemTrungBinh.ToString("0.##"),
                            x.DiemGanNhat.ToString("0.##"),
                            x.SoKyDanhGia.ToString()
                        }).ToList();

                        return new ReportRows
                        {
                            Headers = new List<string> { "STT", "Ma NV", "Nhan vien", "Phong ban", "KPI TB", "KPI gan nhat", "So ky" },
                            Data = rows
                        };
                    }

                case "tien-do-du-an":
                    {
                        var data = _context.Duans
                            .Select(d => new
                            {
                                MaDuAn = d.Maduan,
                                TenDuAn = d.Tenduan,
                                TongCV = d.Congviecs.Count(),
                                HoanThanh = d.Congviecs.Count(c => c.Matrangthai == 3)
                            })
                            .ToList()
                            .Select(x => new
                            {
                                x.MaDuAn,
                                x.TenDuAn,
                                x.TongCV,
                                x.HoanThanh,
                                TiLe = x.TongCV == 0 ? 0 : Math.Round((double)x.HoanThanh * 100 / x.TongCV, 2)
                            })
                            .OrderByDescending(x => x.TiLe)
                            .ToList();

                        var rows = data.Select((x, idx) => new List<string>
                        {
                            (idx + 1).ToString(),
                            x.MaDuAn.ToString(),
                            x.TenDuAn ?? string.Empty,
                            x.TongCV.ToString(),
                            x.HoanThanh.ToString(),
                            x.TiLe.ToString("0.##") + "%"
                        }).ToList();

                        return new ReportRows
                        {
                            Headers = new List<string> { "STT", "Ma du an", "Ten du an", "Tong cong viec", "Da hoan thanh", "Tien do" },
                            Data = rows
                        };
                    }

                case "trang-thai-cong-viec":
                    {
                        var data = _context.Trangthaicongviecs
                            .Select(t => new
                            {
                                TrangThai = t.Tentrangthai,
                                SoLuong = t.Congviecs.Count()
                            })
                            .OrderByDescending(x => x.SoLuong)
                            .ToList();

                        var tongCongViec = data.Sum(x => x.SoLuong);
                        var rows = data.Select((x, idx) => new List<string>
                        {
                            (idx + 1).ToString(),
                            x.TrangThai ?? string.Empty,
                            x.SoLuong.ToString(),
                            (tongCongViec == 0 ? 0 : Math.Round((double)x.SoLuong * 100 / tongCongViec, 2)).ToString("0.##") + "%"
                        }).ToList();

                        return new ReportRows
                        {
                            Headers = new List<string> { "STT", "Trang thai", "So cong viec", "Ty trong" },
                            Data = rows
                        };
                    }

                case "thong-ke-ky-nang":
                    {
                        var data = _context.Kynangs
                            .Select(k => new
                            {
                                MaKyNang = k.Makynang,
                                TenKyNang = k.Tenkynang,
                                SoNhanVien = k.Kynangnhanviens.Count()
                            })
                            .OrderByDescending(x => x.SoNhanVien)
                            .ToList();

                        var rows = data.Select((x, idx) => new List<string>
                        {
                            (idx + 1).ToString(),
                            x.MaKyNang.ToString(),
                            x.TenKyNang ?? string.Empty,
                            x.SoNhanVien.ToString()
                        }).ToList();

                        return new ReportRows
                        {
                            Headers = new List<string> { "STT", "Ma ky nang", "Ten ky nang", "So nhan vien" },
                            Data = rows
                        };
                    }

                default:
                    {
                        var tongNhanVien = _context.Nhanviens.Count();
                        var tongDuAn = _context.Duans.Count();
                        var tongCongViec = _context.Congviecs.Count();
                        var tongNhom = _context.Nhoms.Count();

                        return new ReportRows
                        {
                            Headers = new List<string> { "Chi so", "Gia tri" },
                            Data = new List<List<string>>
                            {
                                new() { "Tong nhan vien", tongNhanVien.ToString() },
                                new() { "Tong du an", tongDuAn.ToString() },
                                new() { "Tong cong viec", tongCongViec.ToString() },
                                new() { "Tong nhom", tongNhom.ToString() }
                            }
                        };
                    }
            }
        }

        private class ReportRows
        {
            public List<string> Headers { get; set; } = new List<string>();
            public List<List<string>> Data { get; set; } = new List<List<string>>();
        }
    }
}