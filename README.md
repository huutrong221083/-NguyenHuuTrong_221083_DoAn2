# XÂY DỰNG HỆ THỐNG WEB QUẢN LÝ HIỆU SUẤT HOẠT ĐỘNG DOANH NGHIỆP TÍCH HỢP AI

## 1. Giới thiệu

Hệ thống là ứng dụng Web phục vụ quản lý hoạt động doanh nghiệp, tập trung vào:

- Quản lý nhân sự
- Quản lý công việc và dự án
- Theo dõi và đánh giá hiệu suất làm việc

Hệ thống đã được triển khai và vận hành thực tế trên môi trường online.

## 2. Kiến trúc hệ thống

Hệ thống được xây dựng theo mô hình ASP.NET Core MVC (Monolithic):

Client (Browser)
-> ASP.NET Core MVC (UI + Business Logic)
-> Entity Framework Core
-> SQL Server

### Đặc điểm

- Không tách microservice
- Không sử dụng AI service riêng
- Tích hợp xử lý AI trực tiếp trong backend

## 3. Cơ sở dữ liệu

Cơ sở dữ liệu được tổ chức theo các nhóm bảng sau:

### Nhóm xác thực và phân quyền

- AspNetUsers
- AspNetRoles
- AspNetUserRoles
- AspNetUserClaims
- AspNetRoleClaims
- AspNetUserLogins
- AspNetUserTokens
- __EFMigrationsHistory

### Nhóm nhân sự

- NHANVIEN
- PHONGBAN

### Nhóm kỹ năng

- KYNANG
- KYNANGNHANVIEN

### Nhóm dự án và công việc

- DUAN
- CONGVIEC
- TRANGTHAICONGVIEC

### Nhóm phân công và tiến độ

- PHANCONGCONGVIEC
- NHATKYCONGVIEC
- NHATKYHOATDONG

### Nhóm tổ chức và nhóm làm việc

- NHOM
- THANHVIENNHOM

### Nhóm tương tác và tài liệu

- BINHLUAN
- TAILIEU
- CV_DINHKEM_TL

### Nhóm KPI và hiệu suất

- DANHMUCKPI
- KETQUAKPI

### Nhóm AI và dự báo

- DUDOANAI

### Nhóm thông báo

- THONGBAO
- THONGBAO_CHO_NV

## 4. Chức năng đã triển khai

### Quản lý người dùng

- Đăng nhập và đăng xuất
- Phân quyền (Admin, Quản lý, Nhân viên)
- Quản lý thông tin cá nhân

### Quản lý tổ chức

- Quản lý phòng ban
- Quản lý nhân viên
- Gán trưởng phòng

### Quản lý dự án và công việc

- Tạo và quản lý dự án
- Phân công công việc
- Theo dõi tiến độ
- Đính kèm tài liệu

### Quản lý KPI và hiệu suất

- Đánh giá KPI theo kỳ
- Tổng hợp hiệu suất
- Xếp loại nhân viên

### Dashboard và thống kê

- Biểu đồ tiến độ dự án
- Biểu đồ KPI
- Thống kê theo phòng ban và nhân viên

### Chức năng AI (ML.NET)

- Dự báo khả năng trễ hạn công việc
- Phân loại hiệu suất nhân viên
- Lưu kết quả dự báo

## 5. Công nghệ sử dụng

| Thành phần | Công nghệ |
| --- | --- |
| Backend + UI | ASP.NET Core MVC |
| Database | SQL Server |
| ORM | Entity Framework Core |
| AI | ML.NET |
| Giao diện | Bootstrap |
| Biểu đồ | Chart.js |
| Triển khai | IIS - Windows Server 2022 |

## 6. Triển khai hệ thống

### Demo

- Link: https://qlhieusuat.huutrong.id.vn

### Tài khoản demo

Admin:

- Email: huutrongk24@gmail.com
- Password: Trong@123

Người dùng:

- nv20@demo.local / User20@
- nv02@demo.local / User02@
- nv07@demo.local / User07@
- nv10@demo.local / User10@

## 7. Kết quả đạt được

- Xây dựng hoàn chỉnh hệ thống Web quản lý doanh nghiệp
- Triển khai đầy đủ các module: Nhân sự, Phòng ban, Dự án, Công việc, KPI
- Tích hợp phân quyền và xác thực người dùng
- Xây dựng dashboard trực quan
- Tích hợp AI bằng ML.NET cho dự báo và phân loại
- Thiết kế và vận hành cơ sở dữ liệu SQL Server
- Triển khai thành công trên server thực tế

## 8. Tổng kết

Hệ thống đã được xây dựng hoàn chỉnh và có thể sử dụng để:

- Quản lý nhân sự và tổ chức
- Theo dõi công việc và tiến độ
- Đánh giá hiệu suất làm việc
- Hỗ trợ dự báo và phân loại bằng AI
