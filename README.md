📌 XÂY DỰNG NỀN TẢNG QUẢN LÝ HIỆU SUẤT HOẠT ĐỘNG CỦA DOANH NGHIỆP MỘT CÁCH THÔNG MINH DỰA TRÊN WEB VÀ AI
📌 1. Giới thiệu
Smart Performance Management Platform là hệ thống Web hỗ trợ doanh nghiệp quản lý:

👤 Nhân sự
📋 Công việc
📊 Hiệu suất làm việc
Hệ thống tích hợp Trí tuệ nhân tạo (AI) nhằm phân tích dữ liệu, dự báo rủi ro và hỗ trợ nhà quản lý ra quyết định chính xác hơn.

Tự đánh giá hiện trạng
 - Hệ thống đã triển khai được các chức năng cơ bản theo định hướng đề tài như quản lý nhân sự, công việc và theo dõi hiệu suất làm việc.

 - Hệ thống bước đầu tích hợp các chức năng phân tích và dự báo dựa trên dữ liệu, giúp hỗ trợ nhà quản lý trong việc theo dõi hoạt động của doanh nghiệp.

 - Tuy nhiên, một số chức năng phân tích và dự báo vẫn đang ở mức thử nghiệm và cần tiếp tục cải thiện để nâng cao độ chính xác.

🎯 2. Mục tiêu đề tài
Xây dựng hệ thống quản lý doanh nghiệp trên nền tảng Web
Phân tích dữ liệu hoạt động nội bộ
Dự báo tiến độ và nguy cơ trễ hạn công việc
Đánh giá & xếp loại hiệu suất nhân viên
Hỗ trợ quản lý ra quyết định dựa trên dữ liệu

Tự đánh giá hiện trạng:
 - Đề tài đã xây dựng được nền tảng Web phục vụ quản lý các hoạt động cơ bản của doanh nghiệp.
 - Hệ thống đã hỗ trợ thu thập và phân tích dữ liệu nội bộ như KPI nhân viên, tiến độ công việc và dự án.
 - Chức năng dự báo rủi ro trễ hạn và đánh giá hiệu suất đã được triển khai nhưng vẫn cần bổ sung thêm các báo cáo so sánh giữa kết quả dự báo và dữ liệu thực tế để tăng tính thuyết phục.
 - Một số chức năng hỗ trợ ra quyết định dựa trên dữ liệu vẫn đang ở mức cơ bản và có thể mở rộng thêm trong tương lai.

🧠 3. Điểm nổi bật & Tính mới
Khác với hệ thống quản lý truyền thống (chỉ thống kê dữ liệu quá khứ), nền tảng này hướng đến quản lý dự báo (Predictive Management).

🔍 Ứng dụng AI trong hệ thống
Mô hình	Mục đích
📈 Linear Regression	Dự đoán nguy cơ trễ hạn công việc
🌲 Random Forest	Phân loại & xếp hạng hiệu suất nhân viên
✔ So sánh kết quả dự báo và dữ liệu thực tế
✔ Cảnh báo rủi ro sớm cho nhà quản lý

Tự đánh giá hiện trạng:
 - Hệ thống đã bước đầu tích hợp các chức năng dự báo và cảnh báo rủi ro, giúp nâng cao khả năng hỗ trợ quản lý so với các hệ thống quản lý thông thường chỉ dừng ở thống kê dữ liệu.
 - Tuy nhiên, mô hình AI hiện đang sử dụng trong hệ thống chưa hoàn toàn trùng khớp với phần mô tả ban đầu trong đề tài.
 - Việc đánh giá độ chính xác của mô hình và so sánh kết quả dự báo với dữ liệu thực tế vẫn cần được bổ sung thêm để hoàn thiện hơn.

🏗️ 4. Kiến trúc hệ thống
Hệ thống được xây dựng theo mô hình Multi-tier Architecture kết hợp AI Service độc lập:

Client (Web Browser)
        ↓
Frontend (React)
        ↓ REST API
Backend (ASP.NET Core / Node.js)
        ↓
SQL Server Database
        ↓
AI Service (Python + Scikit-learn)
🔹 Thành phần chính
Frontend: Hiển thị giao diện, Dashboard, biểu đồ
Backend: Xử lý nghiệp vụ, phân quyền, tích hợp AI
Database: Lưu trữ dữ liệu
AI Service: Phân tích & dự báo

Tự đánh giá hiện trạng:
 - Kiến trúc hệ thống trong phần mô tả ban đầu hướng tới mô hình nhiều tầng với các dịch vụ tách biệt. Tuy nhiên trong quá trình triển khai thực tế, hệ thống hiện được xây dựng chủ yếu theo mô hình ASP.NET Core MVC tích hợp trong một ứng dụng.
 - Phần AI hiện được triển khai trực tiếp trong ứng dụng thông qua thư viện ML.NET thay vì sử dụng một AI service độc lập bằng Python như định hướng ban đầu.
 - Hệ thống vẫn đảm bảo được cấu trúc phân lớp giữa giao diện, xử lý nghiệp vụ và truy cập dữ liệu.

🗄️ 5. Thiết kế cơ sở dữ liệu
CSDL quan hệ sử dụng SQL Server, chuẩn hóa đến 3NF.

👤 Quản lý nhân sự
NguoiDung
PhongBan
KyNang
KyNangNhanVien
📋 Quản lý dự án & công việc
DuAn
CongViec
TaiLieuDuAn
📊 Hiệu suất & AI
DanhGiaHieuSuat
DuBaoAI
🔔 Hệ thống hỗ trợ
ThongBao

Tự đánh giá hiện trạng:
 - Cơ sở dữ liệu được thiết kế theo mô hình quan hệ và sử dụng SQL Server, các bảng dữ liệu cơ bản phục vụ quản lý nhân sự, công việc và hiệu suất đã được xây dựng.
 - Trong quá trình phát triển, tên một số bảng và thực thể trong code có sự thay đổi so với phần mô tả ban đầu trong README.
 - Tuy nhiên về mặt chức năng, cấu trúc dữ liệu vẫn đáp ứng được yêu cầu quản lý của hệ thống.

⚙️ 6. Chức năng chính
🔐 Quản lý người dùng
Đăng nhập / Đăng xuất
Phân quyền (Admin / Quản lý / Nhân viên)
Quản lý thông tin cá nhân
🏢 Quản lý tổ chức
Quản lý phòng ban
Quản lý nhân viên
Gán trưởng phòng
📁 Quản lý dự án & công việc
Tạo dự án
Phân công công việc
Theo dõi tiến độ
Đính kèm tài liệu
📊 Quản lý KPI & hiệu suất
Đánh giá KPI theo tháng
Tổng hợp hiệu suất làm việc
Xếp loại nhân viên tự động
🧠 Chức năng AI
Dự báo trễ hạn công việc
Cảnh báo rủi ro sớm
Phân loại hiệu suất nhân viên
So sánh dự báo và thực tế
📈 Dashboard & Báo cáo
Biểu đồ tiến độ dự án
Biểu đồ KPI
Thống kê theo phòng ban

Tự đánh giá hiện trạng:
 - Hệ thống đã triển khai được hầu hết các chức năng chính như quản lý nhân viên, phòng ban, dự án, công việc và đánh giá KPI.
 - Các chức năng thống kê và dashboard đã hỗ trợ người dùng theo dõi tình hình hoạt động của hệ thống.
 - Một số chức năng như quản lý hồ sơ cá nhân chi tiết hoặc module báo cáo riêng biệt vẫn chưa được tách thành các thành phần độc lập hoàn chỉnh.
 - Hệ thống vẫn có thể tiếp tục được mở rộng thêm các chức năng nâng cao trong các phiên bản tiếp theo.

🧪 7. Công nghệ sử dụng
Thành phần	Công nghệ
Frontend	React
Backend	ASP.NET Core / Node.js
Database	SQL Server
AI	Python, Scikit-learn
API	RESTful API
Deployment	Docker (mô phỏng)

Tự đánh giá hiện trạng:
  - Trong quá trình phát triển, một số công nghệ đã được điều chỉnh so với kế hoạch ban đầu để phù hợp với điều kiện triển khai của đề tài.
 - Hiện tại hệ thống sử dụng:
        + ASP.NET Core MVC cho backend và giao diện.
        + SQL Server và Entity Framework Core cho quản lý dữ liệu.
        + ML.NET để xây dựng các mô hình dự báo.
        + Bootstrap và Chart.js để xây dựng giao diện và biểu đồ thống kê.
 - Hệ thống đã được triển khai thực tế trên Cloud Server iNet (Windows Server 2022), publish từ môi trường phát triển và vận hành qua IIS.
 - Một số công nghệ như Docker hoặc kiến trúc microservice chưa được triển khai trong phạm vi đề tài.

🧠 8. Mô hình sử dụng
Hệ thống tích hợp các mô hình Machine Learning nhằm hỗ trợ dự báo và đánh giá hiệu suất doanh nghiệp.

📈 8.1. Linear Regression (Hồi quy tuyến tính)
Mục đích:
Dự báo nguy cơ trễ hạn công việc.

Ứng dụng trong hệ thống:

Dự đoán số ngày trễ
Ước lượng xác suất trễ hạn
Hỗ trợ cảnh báo sớm cho nhà quản lý
Lý do lựa chọn:

Dễ triển khai
Dễ giải thích về mặt toán học
Phù hợp với dữ liệu doanh nghiệp SME
🌲 8.2. Random Forest (Rừng ngẫu nhiên)
Mục đích:
Phân loại và xếp loại hiệu suất nhân viên.

Ứng dụng trong hệ thống:

Phân loại nhân viên: Xuất sắc / Tốt / Trung bình / Yếu
Phân tích mức độ ảnh hưởng của các yếu tố KPI
Hỗ trợ đánh giá khách quan dựa trên dữ liệu
Lý do lựa chọn:

Độ chính xác cao
Hạn chế overfitting
Hoạt động tốt với dữ liệu thực tế doanh nghiệp
📊 Tổng quan mô hình AI
Bài toán	Mô hình sử dụng	Mục tiêu
Dự báo trễ hạn	Linear Regression	Ước lượng số ngày trễ
Xếp loại hiệu suất	Random Forest Classifier	Phân loại nhân viên
Các mô hình được triển khai bằng Python và thư viện Scikit-learn, tích hợp vào hệ thống thông qua REST API.

Tự đánh giá hiện trạng:
 - Hệ thống đã tích hợp các mô hình Machine Learning để phục vụ dự báo KPI và rủi ro trễ hạn công việc.
 - Tuy nhiên trong quá trình triển khai thực tế, các mô hình được sử dụng thông qua thư viện ML.NET thay vì Python + Scikit-learn như mô tả ban đầu.
 - Ngoài ra, hệ thống hiện chưa triển khai các báo cáo đánh giá chi tiết độ chính xác của mô hình như MAE hoặc MAPE.
 - Đây là phần có thể tiếp tục được cải thiện và nghiên cứu thêm trong các hướng phát triển tiếp theo.


📊 9. Kết quả đạt được (Dự kiến)
Hoàn thiện hệ thống Web quản lý doanh nghiệp
Tích hợp AI dự báo & đánh giá hiệu suất
Giao diện trực quan, thân thiện
Tạo nền tảng cho nghiên cứu mở rộng

Tự đánh giá hiện trạng:
 - Hệ thống Web quản lý doanh nghiệp đã được xây dựng và có thể vận hành với các chức năng chính như quản lý nhân sự, dự án, công việc và KPI.
 - Hệ thống đã tích hợp các chức năng thống kê và dự báo cơ bản nhằm hỗ trợ quản lý theo dõi hoạt động của doanh nghiệp.
 - Hệ thống đã được đưa vào môi trường chạy online, truy cập qua tên miền HTTPS và hoạt động ổn định trên hạ tầng Windows Server + IIS + SQL Server.
 - Một số chức năng nâng cao vẫn đang trong quá trình hoàn thiện và có thể tiếp tục phát triển thêm sau khi đề tài kết thúc.

🚀 10. Hướng phát triển
Mở rộng thêm nhiều mô hình AI
Gợi ý phân công công việc thông minh dựa trên kỹ năng
Triển khai thực tế cho doanh nghiệp
Phát triển thành hệ thống ERP mini cho SME

Tự đánh giá hiện trạng:
 - Một số hướng phát triển đã được thử nghiệm bước đầu, chẳng hạn như gợi ý nguồn lực dựa trên kỹ năng nhân viên.
 - Trong tương lai, hệ thống có thể được mở rộng thêm nhiều mô hình AI, bổ sung giám sát vận hành và cải thiện kiến trúc để tối ưu khi triển khai thực tế.

📌 11. Phạm vi đề tài
Áp dụng cho doanh nghiệp vừa và nhỏ (SME)
Dữ liệu mô phỏng phục vụ nghiên cứu & thực nghiệm
Tập trung vào quản lý nhân sự – công việc – hiệu suất

Tự đánh giá hiện trạng:
 - Phạm vi của hệ thống hiện tại tập trung chủ yếu vào quản lý nhân sự, công việc và đánh giá hiệu suất trong doanh nghiệp quy mô vừa và nhỏ.
 - Dữ liệu sử dụng trong hệ thống chủ yếu là dữ liệu mô phỏng phục vụ cho mục đích nghiên cứu và thử nghiệm của đề tài.

📚 12. Mục đích Repository
Repository được xây dựng phục vụ:

📖 Học tập
🎓 Luận văn tốt nghiệp
🔬 Nghiên cứu khoa học
Giảng viên có thể tham khảo:

Kiến trúc hệ thống
Thiết kế cơ sở dữ liệu
Phương pháp tích hợp AI vào quản lý doanh nghiệp
✨ Tổng kết
Đề tài hướng đến việc ứng dụng Công nghệ Web và Trí tuệ nhân tạo để giải quyết bài toán quản lý doanh nghiệp trong thực tiễn, tạo nền tảng cho mô hình quản lý thông minh trong tương lai.

✅ 13. Tổng kết kết quả dự án
Hệ thống đã hoàn thành các kết quả chính sau:

- Xây dựng thành công nền tảng Web quản lý hiệu suất doanh nghiệp theo mô hình ASP.NET Core MVC.
- Triển khai đầy đủ các module nghiệp vụ cốt lõi: quản lý phòng ban, nhân viên, dự án, công việc, phân công và theo dõi tiến độ.
- Tích hợp phân quyền người dùng theo vai trò (Admin, quản lý, nhân viên) và cơ chế xác thực tài khoản.
- Hoàn thiện dashboard và thống kê giúp theo dõi KPI, tiến độ và hiệu quả hoạt động theo phòng ban/nhân viên.
- Tích hợp AI bằng ML.NET để hỗ trợ dự báo rủi ro trễ hạn và phân loại hiệu suất nhân viên.
- Thiết kế và vận hành cơ sở dữ liệu SQL Server đáp ứng dữ liệu nghiệp vụ thực tế.
- Chuẩn hóa và bổ sung bộ script hỗ trợ khởi tạo dữ liệu mẫu, kiểm tra dữ liệu và xác minh quyền truy cập.
- Hoàn thiện giao diện vận hành ổn định, phục vụ tốt mục tiêu học tập, nghiên cứu và triển khai thử nghiệm thực tế.

🌐 14. Hệ thống chạy online
https://qlhieusuat.huutrong.id.vn

 Tài khoản đăng nhập:
 - Admin:
        + Tên đăng nhập: huutrongk24@gmail.com
        + Mật khẩu: Trong@123
 - Quản lý nhân sự:
        + Tên đăng nhập: nam272519@gmail.com
        + Mật khẩu: User20@
 - Trưởng phòng:
        + Tên đăng nhập: nv02demo@gmail.com
        + Mật khẩu: User02@
 - Trưởng nhóm:
        + Tên đăng nhập: l45360979@gmail.com
        + Mật khẩu: User07@
 - Nhân viên:
        + Tên đăng nhập: m71718383@gmail.com
        + Mật khẩu: User10@
