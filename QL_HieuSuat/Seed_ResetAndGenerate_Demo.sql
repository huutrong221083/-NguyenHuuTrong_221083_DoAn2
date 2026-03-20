USE [QuanLy_HieuSuat];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRAN;

    DECLARE @Now DATETIME = GETDATE();
    DECLARE @Today DATE = CAST(GETDATE() AS DATE);
    DECLARE @StartDate DATE = '2024-01-01';

    DECLARE @TT_ChuaBatDau INT = (
        SELECT TOP 1 MATRANGTHAI
        FROM dbo.TRANGTHAICONGVIEC
        WHERE TENTRANGTHAI = N'Chưa bắt đầu'
    );
    DECLARE @TT_DangLam INT = (
        SELECT TOP 1 MATRANGTHAI
        FROM dbo.TRANGTHAICONGVIEC
        WHERE TENTRANGTHAI = N'Đang thực hiện'
    );
    DECLARE @TT_HoanThanh INT = (
        SELECT TOP 1 MATRANGTHAI
        FROM dbo.TRANGTHAICONGVIEC
        WHERE TENTRANGTHAI = N'Hoàn thành'
    );

    SET @TT_ChuaBatDau = ISNULL(@TT_ChuaBatDau, 1);
    SET @TT_DangLam = ISNULL(@TT_DangLam, 2);
    SET @TT_HoanThanh = ISNULL(@TT_HoanThanh, 3);

    /*
      1) GIU DU LIEU ASP.NET, GIU NHANVIEN #1 + TRANGTHAICONGVIEC
         XOA TOAN BO DU LIEU NGHIEP VU CON LAI
    */

    UPDATE dbo.AspNetUsers
    SET MaNhanVien = NULL
    WHERE MaNhanVien IS NOT NULL
      AND MaNhanVien <> 1;

    IF COL_LENGTH('dbo.PHONGBAN', 'MATRUONGPHONG') IS NOT NULL
    BEGIN
        UPDATE dbo.PHONGBAN
        SET MATRUONGPHONG = NULL;
    END;

    DELETE FROM dbo.CV_DINHKEM_TL;
    DELETE FROM dbo.THONGBAO_CHO_NV;
    DELETE FROM dbo.THANHVIENNHOM;
    DELETE FROM dbo.KYNANGNHANVIEN;
    DELETE FROM dbo.BINHLUAN;
    DELETE FROM dbo.NHATKYCONGVIEC;
    DELETE FROM dbo.PHANCONGCONGVIEC;
    DELETE FROM dbo.CONGVIEC;
    DELETE FROM dbo.THONGBAO;
    DELETE FROM dbo.TAILIEU;
    DELETE FROM dbo.DUDOANAI;
    DELETE FROM dbo.KETQUAKPI;
    DELETE FROM dbo.NHATKYHOATDONG;
    DELETE FROM dbo.NHOM;
    DELETE FROM dbo.DUAN;
    DELETE FROM dbo.DANHMUCKPI;
    DELETE FROM dbo.KYNANG;

    DELETE FROM dbo.NHANVIEN
    WHERE MANHANVIEN <> 1;

    UPDATE dbo.NHANVIEN
    SET MAPHONGBAN = NULL
    WHERE MANHANVIEN = 1;

    DELETE FROM dbo.PHONGBAN;

    /*
      2) RESEED IDENTITY
    */

    DBCC CHECKIDENT ('dbo.BINHLUAN', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.CONGVIEC', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.DUAN', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.DUDOANAI', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.NHANVIEN', RESEED, 1) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.NHATKYCONGVIEC', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.NHOM', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.PHANCONGCONGVIEC', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.PHONGBAN', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.TAILIEU', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.THONGBAO', RESEED, 0) WITH NO_INFOMSGS;

    /*
      3) SEED PHONGBAN
    */

    INSERT INTO dbo.PHONGBAN (TENPHONGBAN, MOTA, MATRUONGPHONG)
    VALUES
    (N'Ban Điều hành', N'Điều hành tổng thể và quản trị chiến lược', NULL),
    (N'Phòng Kỹ thuật nền tảng', N'Phát triển hệ thống backend và tích hợp', NULL),
    (N'Phòng Sản phẩm', N'Quản lý sản phẩm và yêu cầu nghiệp vụ', NULL),
    (N'Phòng Dữ liệu và AI', N'Xây dựng pipeline dữ liệu và mô hình dự báo', NULL),
    (N'Phòng Kiểm thử chất lượng', N'Đảm bảo chất lượng, test tự động và thủ công', NULL),
    (N'Phòng Vận hành', N'Vận hành hệ thống, hỗ trợ và theo dõi sự cố', NULL);

    DECLARE @PB_DH INT = (SELECT TOP 1 MAPHONGBAN FROM dbo.PHONGBAN WHERE TENPHONGBAN = N'Ban Điều hành');
    DECLARE @PB_KT INT = (SELECT TOP 1 MAPHONGBAN FROM dbo.PHONGBAN WHERE TENPHONGBAN = N'Phòng Kỹ thuật nền tảng');
    DECLARE @PB_SP INT = (SELECT TOP 1 MAPHONGBAN FROM dbo.PHONGBAN WHERE TENPHONGBAN = N'Phòng Sản phẩm');
    DECLARE @PB_AI INT = (SELECT TOP 1 MAPHONGBAN FROM dbo.PHONGBAN WHERE TENPHONGBAN = N'Phòng Dữ liệu và AI');
    DECLARE @PB_QA INT = (SELECT TOP 1 MAPHONGBAN FROM dbo.PHONGBAN WHERE TENPHONGBAN = N'Phòng Kiểm thử chất lượng');
    DECLARE @PB_VH INT = (SELECT TOP 1 MAPHONGBAN FROM dbo.PHONGBAN WHERE TENPHONGBAN = N'Phòng Vận hành');

    UPDATE dbo.NHANVIEN
    SET MAPHONGBAN = @PB_DH,
        TRANGTHAI = 1,
        SONAMKINHNGHIEM = ISNULL(SONAMKINHNGHIEM, 12),
        DIEMKPI_TICHLUY = ISNULL(DIEMKPI_TICHLUY, 88)
    WHERE MANHANVIEN = 1;

    /*
      4) SEED NHANVIEN (49 nguoi moi + 1 nguoi giu lai)
    */

    ;WITH NamePool AS
    (
        SELECT TOP (49)
            ROW_NUMBER() OVER (ORDER BY a.Ho, b.TenDem, c.Ten) AS rn,
            CONCAT(a.Ho, N' ', b.TenDem, N' ', c.Ten) AS HoTen
        FROM (VALUES
            (N'Nguyễn'), (N'Trần'), (N'Lê'), (N'Phạm'), (N'Hoàng'),
            (N'Vũ'), (N'Phan'), (N'Đặng'), (N'Bùi'), (N'Đỗ')
        ) a(Ho)
        CROSS JOIN (VALUES
            (N'Gia'), (N'Minh'), (N'Quốc'), (N'Bảo'), (N'Thanh'),
            (N'Ngọc'), (N'Thùy'), (N'Anh'), (N'Hoài'), (N'Khánh')
        ) b(TenDem)
        CROSS JOIN (VALUES
            (N'An'), (N'Bình'), (N'Châu'), (N'Đức'), (N'Hà'),
            (N'Hiếu'), (N'Hùng'), (N'Khánh'), (N'Linh'), (N'Nam'),
            (N'Phúc'), (N'Phong'), (N'Quân'), (N'Trang'), (N'Trung')
        ) c(Ten)
    )
    INSERT INTO dbo.NHANVIEN
    (
        MAPHONGBAN, HOTEN, NGAYSINH, CCCD, DIACHI, GIOITINH, EMAIL, SDT,
        NGAYVAOLAM, TRANGTHAI, SONAMKINHNGHIEM, DIEMKPI_TICHLUY
    )
    SELECT
        CASE (rn % 6)
            WHEN 1 THEN @PB_KT
            WHEN 2 THEN @PB_SP
            WHEN 3 THEN @PB_AI
            WHEN 4 THEN @PB_QA
            WHEN 5 THEN @PB_VH
            ELSE @PB_DH
        END AS MAPHONGBAN,
        HoTen,
        DATEADD(DAY, -1 * (9000 + rn * 37), @Today) AS NGAYSINH,
        RIGHT(CONCAT('100000000000', CAST(200 + rn AS VARCHAR(10))), 12) AS CCCD,
        CASE (rn % 5)
            WHEN 0 THEN N'Hà Nội'
            WHEN 1 THEN N'TP Hồ Chí Minh'
            WHEN 2 THEN N'Đà Nẵng'
            WHEN 3 THEN N'Cần Thơ'
            ELSE N'Hải Phòng'
        END AS DIACHI,
        CASE WHEN rn % 2 = 0 THEN N'Nam' ELSE N'Nu' END AS GIOITINH,
        CONCAT('nv', RIGHT(CONCAT('00', rn + 1), 2), '@demo.local') AS EMAIL,
        RIGHT(CONCAT('0900000000', CAST(200 + rn AS VARCHAR(10))), 10) AS SDT,
        DATEADD(DAY, -1 * (240 + rn * 11), @Today) AS NGAYVAOLAM,
        1 AS TRANGTHAI,
        CAST(1 + (rn % 11) AS FLOAT) AS SONAMKINHNGHIEM,
        CAST(55 + (rn % 36) AS FLOAT) AS DIEMKPI_TICHLUY
    FROM NamePool;

    UPDATE n
    SET n.DIEMKPI_TICHLUY = CASE v.rn
        WHEN 1 THEN 46
        WHEN 2 THEN 49
        WHEN 3 THEN 52
        WHEN 4 THEN 54
        ELSE n.DIEMKPI_TICHLUY
    END
    FROM dbo.NHANVIEN n
    JOIN
    (
        SELECT TOP (4)
            MANHANVIEN,
            ROW_NUMBER() OVER (ORDER BY MANHANVIEN DESC) AS rn
        FROM dbo.NHANVIEN
        WHERE MANHANVIEN <> 1
        ORDER BY MANHANVIEN DESC
    ) v ON v.MANHANVIEN = n.MANHANVIEN;

    DECLARE @HeadKT INT = (SELECT TOP 1 MANHANVIEN FROM dbo.NHANVIEN WHERE MAPHONGBAN = @PB_KT ORDER BY MANHANVIEN);
    DECLARE @HeadSP INT = (SELECT TOP 1 MANHANVIEN FROM dbo.NHANVIEN WHERE MAPHONGBAN = @PB_SP ORDER BY MANHANVIEN);
    DECLARE @HeadAI INT = (SELECT TOP 1 MANHANVIEN FROM dbo.NHANVIEN WHERE MAPHONGBAN = @PB_AI ORDER BY MANHANVIEN);
    DECLARE @HeadQA INT = (SELECT TOP 1 MANHANVIEN FROM dbo.NHANVIEN WHERE MAPHONGBAN = @PB_QA ORDER BY MANHANVIEN);
    DECLARE @HeadVH INT = (SELECT TOP 1 MANHANVIEN FROM dbo.NHANVIEN WHERE MAPHONGBAN = @PB_VH ORDER BY MANHANVIEN);

    UPDATE dbo.PHONGBAN
    SET MATRUONGPHONG = CASE MAPHONGBAN
        WHEN @PB_DH THEN 1
        WHEN @PB_KT THEN @HeadKT
        WHEN @PB_SP THEN @HeadSP
        WHEN @PB_AI THEN @HeadAI
        WHEN @PB_QA THEN @HeadQA
        WHEN @PB_VH THEN @HeadVH
    END;

    /*
      5) NHOM + THANHVIENNHOM
    */

    INSERT INTO dbo.NHOM (TENNHOM, NGAYTAO)
    VALUES
    (N'Backend Core', DATEADD(DAY, -680, @Now)),
    (N'Tích hợp API', DATEADD(DAY, -640, @Now)),
    (N'Frontend Web', DATEADD(DAY, -610, @Now)),
    (N'Mobile App', DATEADD(DAY, -600, @Now)),
    (N'Khám phá sản phẩm', DATEADD(DAY, -570, @Now)),
    (N'Kỹ thuật dữ liệu', DATEADD(DAY, -560, @Now)),
    (N'Dự báo AI', DATEADD(DAY, -540, @Now)),
    (N'QA tự động', DATEADD(DAY, -520, @Now)),
    (N'QA Manual', DATEADD(DAY, -500, @Now)),
    (N'DevOps SRE', DATEADD(DAY, -480, @Now)),
    (N'Vận hành khách hàng', DATEADD(DAY, -450, @Now)),
    (N'Bảo mật hệ thống', DATEADD(DAY, -430, @Now));

    ;WITH TeamList AS
    (
        SELECT MANHOM, ROW_NUMBER() OVER (ORDER BY MANHOM) AS TeamNo
        FROM dbo.NHOM
    ),
    Emp AS
    (
        SELECT MANHANVIEN, ROW_NUMBER() OVER (ORDER BY MANHANVIEN) AS EmpNo
        FROM dbo.NHANVIEN
    )
    INSERT INTO dbo.THANHVIENNHOM (MANHOM, MANHANVIEN, NGAYGIANHAP, VAITROTRONGNHOM)
    SELECT
        t.MANHOM,
        e.MANHANVIEN,
        DATEADD(DAY, -1 * (420 - (e.EmpNo % 120)), @Now) AS NGAYGIANHAP,
        CASE WHEN (e.EmpNo % 7 = 0) THEN N'Trưởng nhóm' ELSE N'Thành viên' END AS VAITROTRONGNHOM
    FROM TeamList t
    JOIN Emp e
      ON ((e.EmpNo - 1) % 12) + 1 = t.TeamNo
    WHERE NOT (t.TeamNo = 1 AND e.MANHANVIEN = 1); -- tranh duplicate truong hop admin

    IF NOT EXISTS (SELECT 1 FROM dbo.THANHVIENNHOM WHERE MANHOM = 1 AND MANHANVIEN = 1)
    BEGIN
        INSERT INTO dbo.THANHVIENNHOM (MANHOM, MANHANVIEN, NGAYGIANHAP, VAITROTRONGNHOM)
        VALUES (1, 1, DATEADD(DAY, -730, @Now), N'Trưởng nhóm');
    END;

    /*
      6) KYNANG + KYNANGNHANVIEN
    */

    INSERT INTO dbo.KYNANG (MAKYNANG, TENKYNANG)
    VALUES
    (1, N'SQL Server'),
    (2, N'ASP.NET Core'),
    (3, N'Entity Framework'),
    (4, N'Frontend MVC'),
    (5, N'DevOps CI/CD'),
    (6, N'Test Automation'),
    (7, N'Phân tích nghiệp vụ'),
    (8, N'Python Data'),
    (9, N'Machine Learning'),
    (10, N'Bảo mật ứng dụng'),
    (11, N'Giao tiếp và phối hợp'),
    (12, N'Quản lý dự án');

    ;WITH Emp AS
    (
        SELECT MANHANVIEN, ROW_NUMBER() OVER (ORDER BY MANHANVIEN) AS rn
        FROM dbo.NHANVIEN
    ),
    SkillPick AS
    (
        SELECT MANHANVIEN, ((rn - 1) % 12) + 1 AS MAKYNANG, 2 + (rn % 4) AS CAPDO, 1 + (rn % 7) AS SoDuAn
        FROM Emp
        UNION ALL
        SELECT MANHANVIEN, ((rn + 3) % 12) + 1, 2 + ((rn + 1) % 4), 1 + ((rn + 2) % 7)
        FROM Emp
        UNION ALL
        SELECT MANHANVIEN, ((rn + 6) % 12) + 1, 2 + ((rn + 2) % 4), 1 + ((rn + 4) % 7)
        FROM Emp
    )
    INSERT INTO dbo.KYNANGNHANVIEN (MANHANVIEN, MAKYNANG, CAPDO, SODUANDADUNGKYNANGNAY)
    SELECT DISTINCT MANHANVIEN, MAKYNANG, CAPDO, SoDuAn
    FROM SkillPick;

    /*
      7) DANHMUCKPI + KETQUAKPI (tu 2024 den hien tai)
    */

    INSERT INTO dbo.DANHMUCKPI (MADOANHMUC, TENDOANHMUC, TRONGSO)
    VALUES
    (1, N'Tiến độ hoàn thành', 0.30),
    (2, N'Chất lượng đầu ra', 0.25),
    (3, N'Kỷ luật và phản hồi', 0.15),
    (4, N'Đóng góp đội nhóm', 0.10),
    (5, N'Hiệu quả sử dụng tài nguyên', 0.10),
    (6, N'Sáng kiến cải tiến', 0.10);

    ;WITH Months AS
    (
        SELECT CAST('2024-01-01' AS DATE) AS MonthStart
        UNION ALL
        SELECT DATEADD(MONTH, 1, MonthStart)
        FROM Months
        WHERE DATEADD(MONTH, 1, MonthStart) <= DATEFROMPARTS(YEAR(@Today), MONTH(@Today), 1)
    ),
    Emp AS
    (
        SELECT MANHANVIEN, ROW_NUMBER() OVER (ORDER BY MANHANVIEN) AS rn
        FROM dbo.NHANVIEN
    ),
    Cat AS
    (
        SELECT MADOANHMUC
        FROM dbo.DANHMUCKPI
    ),
    KPIData AS
    (
        SELECT
            ROW_NUMBER() OVER (ORDER BY e.MANHANVIEN, m.MonthStart, c.MADOANHMUC) AS Seq,
            e.MANHANVIEN,
            c.MADOANHMUC,
            MONTH(m.MonthStart) AS Thang,
            YEAR(m.MonthStart) AS Nam,
            CAST(60 + ((e.rn * 7 + c.MADOANHMUC * 5 + MONTH(m.MonthStart) * 3) % 36) AS FLOAT) AS DiemSo
        FROM Emp e
        CROSS JOIN Months m
        CROSS JOIN Cat c
    )
    INSERT INTO dbo.KETQUAKPI (MAKETQUA, MANHANVIEN, MADOANHMUC, DIEMSO, THANG, NAM)
    SELECT Seq, MANHANVIEN, MADOANHMUC, DiemSo, Thang, Nam
    FROM KPIData
    OPTION (MAXRECURSION 0);

    /*
      8) DUAN: 20 du an (hoan thanh dung/truoc han, dang tre han, dang thuc hien)
    */

    ;WITH N AS
    (
        SELECT TOP (20) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
        FROM sys.all_objects
    )
    INSERT INTO dbo.DUAN (TENDUAN, MOTA, NGAYBATDAU, NGAYKETTHUC, TRANGTHAI)
    SELECT
        CONCAT(N'Dự án Demo ', RIGHT(CONCAT('00', n), 2)) AS TENDUAN,
        CASE
            WHEN n <= 8 THEN N'Dự án đã hoàn thành, dữ liệu phục vụ so sánh trước hạn và đúng hạn'
            WHEN n <= 14 THEN N'Dự án đang trễ hạn, cần ưu tiên xử lý nghẽn'
            ELSE N'Dự án đang triển khai trong kế hoạch'
        END AS MOTA,
        DATEADD(DAY, n * 35, @StartDate) AS NGAYBATDAU,
        CASE
            WHEN n <= 8 THEN DATEADD(DAY, 160 + n * 10, DATEADD(DAY, n * 35, @StartDate))
            WHEN n <= 14 THEN DATEADD(DAY, -1 * (10 + n), @Today)
            ELSE DATEADD(DAY, 35 + n * 3, @Today)
        END AS NGAYKETTHUC,
        CASE
            WHEN n <= 8 THEN 3
            ELSE 2
        END AS TRANGTHAI
    FROM N;

    /*
      9) CONGVIEC: 500 task
         - hoan thanh ~50%
         - dang tre han ~30%
         - truoc/chua den han ~20%
    */

    IF OBJECT_ID('tempdb..#TaskSeed') IS NOT NULL DROP TABLE #TaskSeed;

    CREATE TABLE #TaskSeed
    (
        rn INT NOT NULL PRIMARY KEY,
        MADUAN INT NOT NULL,
        TENCONGVIEC NVARCHAR(300) NOT NULL,
        MOTA NVARCHAR(MAX) NULL,
        HANHOANTHANH DATETIME NULL,
        MATRANGTHAI INT NOT NULL,
        TRANGTHAI INT NOT NULL,
        DOUUTIEN INT NULL,
        DOKHO INT NULL,
        DIEMCONGVIEC FLOAT NULL,
        TONGCONGVIECCON INT NULL,
        MACONGVIECCHA INT NULL
    );

    ;WITH P AS
    (
        SELECT MADUAN, ROW_NUMBER() OVER (ORDER BY MADUAN) AS pno
        FROM dbo.DUAN
    ),
    N AS
    (
        SELECT TOP (500) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
        FROM sys.all_objects a
        CROSS JOIN sys.all_objects b
    )
    INSERT INTO #TaskSeed
    (
        rn, MADUAN, TENCONGVIEC, MOTA, HANHOANTHANH,
        MATRANGTHAI, TRANGTHAI, DOUUTIEN, DOKHO, DIEMCONGVIEC, TONGCONGVIECCON, MACONGVIECCHA
    )
    SELECT
        n.n AS rn,
        p.MADUAN,
        CONCAT(N'Task P', RIGHT(CONCAT('00', p.pno), 2), N'-', RIGHT(CONCAT('000', n.n), 3)) AS TENCONGVIEC,
        CASE
            WHEN n.n % 10 <= 4 THEN N'Công việc đã hoàn tất, có dữ liệu để đánh giá đúng hạn/trước hạn/trễ hạn'
            WHEN n.n % 10 <= 7 THEN N'Công việc đang xử lý và đã quá hạn cảnh báo'
            ELSE N'Công việc sắp tới hoặc mới khởi tạo trong chu kỳ hiện tại'
        END AS MOTA,
        CASE
            WHEN n.n % 10 <= 4 THEN DATEADD(DAY, -1 * (n.n % 70), @Today)
            WHEN n.n % 10 <= 7 THEN DATEADD(DAY, -1 * (5 + (n.n % 45)), @Today)
            ELSE DATEADD(DAY, 5 + (n.n % 70), @Today)
        END AS HANHOANTHANH,
        CASE
            WHEN n.n % 10 <= 4 THEN @TT_HoanThanh
            WHEN n.n % 10 <= 7 THEN @TT_DangLam
            ELSE @TT_ChuaBatDau
        END AS MATRANGTHAI,
        CASE
            WHEN n.n % 10 <= 4 THEN @TT_HoanThanh
            WHEN n.n % 10 <= 7 THEN @TT_DangLam
            ELSE @TT_ChuaBatDau
        END AS TRANGTHAI,
        1 + (n.n % 5) AS DOUUTIEN,
        1 + (n.n % 5) AS DOKHO,
        CAST(40 + (n.n % 61) AS FLOAT) AS DIEMCONGVIEC,
        0 AS TONGCONGVIECCON,
        NULL AS MACONGVIECCHA
    FROM N n
    JOIN P p
      ON ((n.n - 1) % 20) + 1 = p.pno;

    INSERT INTO dbo.CONGVIEC
    (
        MADUAN, MATRANGTHAI, CON_MACONGVIEC, TENCONGVIEC, MOTA, HANHOANTHANH,
        TRANGTHAI, DOUUTIEN, DOKHO, DIEMCONGVIEC, TONGCONGVIECCON, MACONGVIECCHA
    )
    SELECT
        MADUAN, MATRANGTHAI, NULL, TENCONGVIEC, MOTA, HANHOANTHANH,
        TRANGTHAI, DOUUTIEN, DOKHO, DIEMCONGVIEC, TONGCONGVIECCON, MACONGVIECCHA
    FROM #TaskSeed
    ORDER BY rn;

    IF OBJECT_ID('tempdb..#TaskMap') IS NOT NULL DROP TABLE #TaskMap;

    SELECT
        t.rn,
        c.MACONGVIEC,
        t.MADUAN,
        t.HANHOANTHANH,
        t.MATRANGTHAI
    INTO #TaskMap
    FROM #TaskSeed t
    JOIN dbo.CONGVIEC c ON c.TENCONGVIEC = t.TENCONGVIEC;

    /*
      10) PHANCONGCONGVIEC
    */

    ;WITH Emp AS
    (
        SELECT MANHANVIEN, MAPHONGBAN, ROW_NUMBER() OVER (ORDER BY MANHANVIEN) AS eno
        FROM dbo.NHANVIEN
    ),
    Team AS
    (
        SELECT MANHOM, ROW_NUMBER() OVER (ORDER BY MANHOM) AS tno
        FROM dbo.NHOM
    )
    INSERT INTO dbo.PHANCONGCONGVIEC
    (
        MACONGVIEC, MAPHONGBAN, MANHOM, MANHANVIEN, LOAIDOITUONG,
        NGAYGIAO, NGAYBATDAUDUKIEN, NGAYKETTHUCDUKIEN, NGAYBATDAUTHUCTE, NGAYKETTHUCTHUCTE
    )
    SELECT
        tm.MACONGVIEC,
        e.MAPHONGBAN,
        t.MANHOM,
        e.MANHANVIEN,
        N'Nhân viên',
        DATEADD(DAY, -1 * (3 + (tm.rn % 9)), tm.HANHOANTHANH) AS NGAYGIAO,
        DATEADD(DAY, -1 * (10 + (tm.rn % 6)), tm.HANHOANTHANH) AS NGAYBATDAUDUKIEN,
        tm.HANHOANTHANH AS NGAYKETTHUCDUKIEN,
        DATEADD(DAY, -1 * (9 + (tm.rn % 5)), tm.HANHOANTHANH) AS NGAYBATDAUTHUCTE,
        CASE
            WHEN tm.MATRANGTHAI = @TT_HoanThanh THEN
                CASE
                    WHEN tm.rn % 6 IN (0, 1) THEN DATEADD(DAY, -2, tm.HANHOANTHANH) -- trước hạn
                    WHEN tm.rn % 6 IN (2, 3) THEN tm.HANHOANTHANH                    -- đúng hạn
                    ELSE DATEADD(DAY, 2 + (tm.rn % 4), tm.HANHOANTHANH)              -- trễ hạn
                END
            ELSE NULL
        END AS NGAYKETTHUCTHUCTE
    FROM #TaskMap tm
    JOIN Emp e ON ((tm.rn - 1) % (SELECT COUNT(*) FROM dbo.NHANVIEN)) + 1 = e.eno
    JOIN Team t ON ((tm.rn - 1) % (SELECT COUNT(*) FROM dbo.NHOM)) + 1 = t.tno;

    IF OBJECT_ID('tempdb..#NhanVienCanhBao') IS NOT NULL DROP TABLE #NhanVienCanhBao;

    ;WITH Perf AS
    (
        SELECT
            pc.MANHANVIEN,
            COUNT(*) AS TongTask,
            SUM(CASE WHEN pc.NGAYKETTHUCTHUCTE IS NOT NULL THEN 1 ELSE 0 END) AS SoTaskDaXong,
            SUM(CASE WHEN pc.NGAYKETTHUCTHUCTE IS NOT NULL AND CAST(pc.NGAYKETTHUCTHUCTE AS DATE) < CAST(pc.NGAYKETTHUCDUKIEN AS DATE) THEN 1 ELSE 0 END) AS SoTaskSomHan,
            SUM(CASE WHEN pc.NGAYKETTHUCTHUCTE IS NOT NULL AND CAST(pc.NGAYKETTHUCTHUCTE AS DATE) = CAST(pc.NGAYKETTHUCDUKIEN AS DATE) THEN 1 ELSE 0 END) AS SoTaskDungHan,
            SUM(CASE WHEN pc.NGAYKETTHUCTHUCTE IS NOT NULL AND CAST(pc.NGAYKETTHUCTHUCTE AS DATE) > CAST(pc.NGAYKETTHUCDUKIEN AS DATE) THEN 1 ELSE 0 END) AS SoTaskTreHan
        FROM dbo.PHANCONGCONGVIEC pc
        GROUP BY pc.MANHANVIEN
    ),
    Calc AS
    (
        SELECT
            MANHANVIEN,
            TongTask,
            SoTaskDaXong,
            SoTaskSomHan,
            SoTaskDungHan,
            SoTaskTreHan,
            CAST(100.0 * SoTaskDaXong / NULLIF(TongTask, 0) AS DECIMAL(5,2)) AS TyLeHoanThanh
        FROM Perf
    )
    SELECT TOP (6)
        c.MANHANVIEN,
        c.TongTask,
        c.SoTaskDaXong,
        c.SoTaskSomHan,
        c.SoTaskDungHan,
        c.SoTaskTreHan,
        c.TyLeHoanThanh
    INTO #NhanVienCanhBao
    FROM Calc c
    WHERE c.TyLeHoanThanh < 65
    ORDER BY c.TyLeHoanThanh ASC, c.SoTaskTreHan DESC;

    UPDATE nv
    SET nv.DIEMKPI_TICHLUY =
        CASE
            WHEN cb.TyLeHoanThanh < 45 THEN 42
            WHEN cb.TyLeHoanThanh < 55 THEN 48
            ELSE 54
        END
    FROM dbo.NHANVIEN nv
    JOIN #NhanVienCanhBao cb ON cb.MANHANVIEN = nv.MANHANVIEN;

    /*
      11) NHATKYCONGVIEC
    */

    INSERT INTO dbo.NHATKYCONGVIEC (MACONGVIEC, PHANTRAMHOANTHANH, GHICHU, NGAYCAPNHAT)
    SELECT
        tm.MACONGVIEC,
        CASE
            WHEN tm.MATRANGTHAI = @TT_HoanThanh THEN 100
            WHEN tm.MATRANGTHAI = @TT_DangLam THEN 35 + (tm.rn % 45)
            ELSE 0
        END,
        CASE
            WHEN tm.MATRANGTHAI = @TT_HoanThanh THEN N'Đã cập nhật hoàn tất và nghiệm thu.'
            WHEN tm.MATRANGTHAI = @TT_DangLam THEN N'Đang xử lý, cần theo dõi mốc tiến độ.'
            ELSE N'Chưa bắt đầu, đã xếp lịch nguồn lực.'
        END,
        DATEADD(DAY, -1 * (tm.rn % 30), @Now)
    FROM #TaskMap tm;

    INSERT INTO dbo.NHATKYCONGVIEC (MACONGVIEC, PHANTRAMHOANTHANH, GHICHU, NGAYCAPNHAT)
    SELECT
        tm.MACONGVIEC,
        CASE
            WHEN tm.MATRANGTHAI = @TT_HoanThanh THEN 100
            WHEN tm.MATRANGTHAI = @TT_DangLam THEN 55 + (tm.rn % 35)
            ELSE 10
        END,
        CASE
            WHEN tm.MATRANGTHAI = @TT_HoanThanh THEN N'Hồ sơ đã đóng và lưu kết quả.'
            WHEN tm.MATRANGTHAI = @TT_DangLam THEN N'Đã giải quyết một phần blocker kỹ thuật.'
            ELSE N'Chờ duyệt ưu tiên và phân công chi tiết.'
        END,
        DATEADD(DAY, -1 * (tm.rn % 12), @Now)
    FROM #TaskMap tm
    WHERE tm.rn % 2 = 0;

    /*
      12) BINHLUAN
    */

    ;WITH Cmt AS
    (
        SELECT TOP (300) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
        FROM sys.all_objects
    ),
    Emp AS
    (
        SELECT MANHANVIEN, ROW_NUMBER() OVER (ORDER BY MANHANVIEN) AS eno
        FROM dbo.NHANVIEN
    )
    INSERT INTO dbo.BINHLUAN (MACONGVIEC, MANHANVIEN, NOIDUNG, NGAYTAO)
    SELECT
        tm.MACONGVIEC,
        e.MANHANVIEN,
        CASE (c.n % 6)
            WHEN 0 THEN N'Đã tiếp nhận và đang xử lý theo kế hoạch.'
            WHEN 1 THEN N'Cần xác nhận thêm dữ liệu đầu vào để chốt giải pháp.'
            WHEN 2 THEN N'Đã cập nhật bản nháp, chờ review từ trưởng nhóm.'
            WHEN 3 THEN N'Gặp vướng API ngoài, đã mở ticket hỗ trợ.'
            WHEN 4 THEN N'Dự kiến xong trước 1-2 ngày nếu không phát sinh thêm.'
            ELSE N'Đã cập nhật tài liệu và gửi thông tin cho các bên liên quan.'
        END,
        DATEADD(HOUR, -1 * c.n, @Now)
    FROM Cmt c
    JOIN #TaskMap tm ON ((c.n - 1) % 500) + 1 = tm.rn
    JOIN Emp e ON ((c.n - 1) % (SELECT COUNT(*) FROM dbo.NHANVIEN)) + 1 = e.eno;

    /*
      13) TAILIEU + CV_DINHKEM_TL
    */

    ;WITH Doc AS
    (
        SELECT TOP (120) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
        FROM sys.all_objects
    )
    INSERT INTO dbo.TAILIEU (TENTAILIEU, HUONGDAN)
    SELECT
        CONCAT(N'Tài liệu hướng dẫn ', RIGHT(CONCAT('000', n), 3)),
        N'Mô tả quy trình, checklist và hướng dẫn thao tác chuẩn cho task tương ứng.'
    FROM Doc;

    ;WITH D AS
    (
        SELECT MATAILIEU, ROW_NUMBER() OVER (ORDER BY MATAILIEU) AS dno
        FROM dbo.TAILIEU
    )
    INSERT INTO dbo.CV_DINHKEM_TL (MACONGVIEC, MATAILIEU)
    SELECT
        tm.MACONGVIEC,
        d.MATAILIEU
    FROM #TaskMap tm
    JOIN D d ON ((tm.rn - 1) % 120) + 1 = d.dno
    WHERE tm.rn % 3 = 0;

    /*
      14) THONGBAO + THONGBAO_CHO_NV
    */

    ;WITH Tb AS
    (
        SELECT TOP (140) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
        FROM sys.all_objects
    )
    INSERT INTO dbo.THONGBAO (NOIDUNG, DADOC, THOIGIAN)
    SELECT
        CASE
            WHEN n % 4 = 0 THEN N'Cảnh báo tiến độ: Có task sắp quá hạn trong 72 giờ.'
            WHEN n % 4 = 1 THEN N'Cập nhật dự án: Đã có bản phát hành mới trên môi trường test.'
            WHEN n % 4 = 2 THEN N'Nhắc nhở KPI: Hoàn tất chấm điểm tháng trước 17h Thứ Sáu.'
            ELSE N'Thông báo hệ thống: Đã đồng bộ dữ liệu AI thành công.'
        END,
        CASE WHEN n % 3 = 0 THEN 1 ELSE 0 END,
        DATEADD(HOUR, -1 * (n * 6), @Now)
    FROM Tb;

    INSERT INTO dbo.THONGBAO (NOIDUNG, DADOC, THOIGIAN)
    SELECT
        CONCAT(
            N'Cảnh báo hiệu suất: Tỷ lệ hoàn thành của nhân viên ',
            nv.HOTEN,
            N' chỉ đạt ',
            FORMAT(cb.TyLeHoanThanh, 'N2'),
            N'%. Cần ưu tiên hỗ trợ để cải thiện tiến độ.'
        ) AS NOIDUNG,
        0,
        DATEADD(MINUTE, -1 * ROW_NUMBER() OVER (ORDER BY cb.TyLeHoanThanh ASC), @Now)
    FROM #NhanVienCanhBao cb
    JOIN dbo.NHANVIEN nv ON nv.MANHANVIEN = cb.MANHANVIEN;

    ;WITH TbMap AS
    (
        SELECT MATHONGBAO, ROW_NUMBER() OVER (ORDER BY MATHONGBAO) AS tno
        FROM dbo.THONGBAO
        WHERE NOIDUNG NOT LIKE N'Cảnh báo hiệu suất: Tỷ lệ hoàn thành của nhân viên%'
    ),
    Emp AS
    (
        SELECT MANHANVIEN, ROW_NUMBER() OVER (ORDER BY MANHANVIEN) AS eno
        FROM dbo.NHANVIEN
    )
    INSERT INTO dbo.THONGBAO_CHO_NV (MATHONGBAO, MANHANVIEN)
    SELECT
        t.MATHONGBAO,
        e.MANHANVIEN
    FROM TbMap t
    JOIN Emp e ON ((t.tno - 1) % (SELECT COUNT(*) FROM dbo.NHANVIEN)) + 1 = e.eno;

    ;WITH CanhBaoTB AS
    (
        SELECT
            t.MATHONGBAO,
            ROW_NUMBER() OVER (ORDER BY t.MATHONGBAO DESC) AS rn
        FROM dbo.THONGBAO t
        WHERE t.NOIDUNG LIKE N'Cảnh báo hiệu suất: Tỷ lệ hoàn thành của nhân viên%'
    ),
    CanhBaoNV AS
    (
        SELECT
            cb.MANHANVIEN,
            ROW_NUMBER() OVER (ORDER BY cb.TyLeHoanThanh ASC, cb.MANHANVIEN) AS rn
        FROM #NhanVienCanhBao cb
    )
    INSERT INTO dbo.THONGBAO_CHO_NV (MATHONGBAO, MANHANVIEN)
    SELECT t.MATHONGBAO, n.MANHANVIEN
    FROM CanhBaoTB t
        JOIN CanhBaoNV n ON n.rn = t.rn
        WHERE NOT EXISTS
        (
                SELECT 1
                FROM dbo.THONGBAO_CHO_NV x
                WHERE x.MATHONGBAO = t.MATHONGBAO
                    AND x.MANHANVIEN = n.MANHANVIEN
        );

    /*
      15) DUDOANAI
    */

    ;WITH Proj AS
    (
        SELECT MADUAN, ROW_NUMBER() OVER (ORDER BY MADUAN) AS pno
        FROM dbo.DUAN
    ),
    Emp AS
    (
        SELECT MANHANVIEN, ROW_NUMBER() OVER (ORDER BY MANHANVIEN) AS eno
        FROM dbo.NHANVIEN
    ),
    N AS
    (
        SELECT TOP (160) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
        FROM sys.all_objects
    )
    INSERT INTO dbo.DUDOANAI
    (
        MANHANVIEN, THANG, NAM, DIEMDUDOAN, DEXUATCAITHIEN, THOIGIANDUDOAN,
        MADOITUONG, XACSUATTREHAN, GIOIYNGUONLUC
    )
    SELECT
        e.MANHANVIEN,
        MONTH(DATEADD(MONTH, -1 * (n.n % 24), @Now)) AS THANG,
        YEAR(DATEADD(MONTH, -1 * (n.n % 24), @Now)) AS NAM,
        CAST(58 + (n.n % 35) AS FLOAT) AS DIEMDUDOAN,
        CASE
            WHEN n.n % 3 = 0 THEN N'Tăng review liên phòng, bổ sung checkpoint giữa kỳ.'
            WHEN n.n % 3 = 1 THEN N'Ưu tiên giảm blocker ngoài phạm vi và chốt scope sớm.'
            ELSE N'Cần bổ sung nguồn lực QA và tự động hóa test hồi quy.'
        END,
        DATEADD(DAY, -1 * (n.n % 220), @Now),
        p.MADUAN,
        CAST((n.n % 100) / 100.0 AS FLOAT),
        CASE
            WHEN n.n % 4 = 0 THEN N'1 backend senior, 1 QA automation, 1 PM part-time'
            WHEN n.n % 4 = 1 THEN N'1 data engineer, 1 AI engineer, 1 tester'
            WHEN n.n % 4 = 2 THEN N'2 fullstack, 1 business analyst'
            ELSE N'1 devops, 1 backend, 1 QA manual'
        END
    FROM N
    JOIN Proj p ON ((n.n - 1) % 20) + 1 = p.pno
    JOIN Emp e ON ((n.n - 1) % (SELECT COUNT(*) FROM dbo.NHANVIEN)) + 1 = e.eno;

    /*
      16) NHATKYHOATDONG
    */

    ;WITH N AS
    (
        SELECT TOP (320) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
        FROM sys.all_objects
    ),
    Emp AS
    (
        SELECT MANHANVIEN, ROW_NUMBER() OVER (ORDER BY MANHANVIEN) AS eno
        FROM dbo.NHANVIEN
    )
    INSERT INTO dbo.NHATKYHOATDONG (MANHATKYHOATDONG, MANHANVIEN, HANHDONG, THOIGIAN)
    SELECT
        n.n AS MANHATKYHOATDONG,
        e.MANHANVIEN,
        CASE (n.n % 6)
            WHEN 0 THEN N'Đăng nhập hệ thống và cập nhật task được giao'
            WHEN 1 THEN N'Tạo mới bình luận và báo cáo blocker'
            WHEN 2 THEN N'Cập nhật tiến độ và tài liệu hướng dẫn'
            WHEN 3 THEN N'Ghi nhận kết quả test và đánh dấu cần xử lý'
            WHEN 4 THEN N'Nhận thông báo KPI và xác nhận đã đọc'
            ELSE N'Đồng bộ thông tin dự án với bảng điều khiển AI'
        END,
        DATEADD(MINUTE, -1 * (n.n * 20), @Now)
    FROM N
    JOIN Emp e ON ((n.n - 1) % (SELECT COUNT(*) FROM dbo.NHANVIEN)) + 1 = e.eno;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRAN;

    THROW;
END CATCH;
GO

PRINT N'Hoàn tất reset + seed demo lớn (giữ NHANVIEN #1, giữ TRANGTHAICONGVIEC, giữ bảng ASP.Net).';
GO
