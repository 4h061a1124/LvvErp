# LVV ERP

Hệ thống ERP nội bộ cho nhà máy. Phiên bản hiện tại có:

- Đăng nhập (JWT), phân quyền theo role: **Admin**, **HR**, **Employee**, **Thủ kho**
- **Danh bạ nhân viên**: ảnh, mã NV, họ tên, bộ phận, chức danh, email, số điện thoại, trạng thái; tìm kiếm không dấu, lọc, phân trang; thêm/sửa/xóa; nhập từ Excel
- **Vật tư & Kho**:
  - Danh mục đơn vị tính, nhóm vật tư, vật tư (ảnh, quy cách, tồn tối thiểu, quản lý theo lô), kho và vị trí
  - Phiếu nhập, phiếu xuất, phiếu chuyển kho: Nháp → Đã duyệt → Đã hủy (chỉ Admin hủy, ghi bút toán đảo)
  - Tồn kho theo kho/vị trí/lô, thẻ kho, cảnh báo dưới tồn tối thiểu
  - In tem QR (A4 24 tem hoặc tem cuộn 50 × 30 mm), đọc mã bằng máy quét
  - Nhập danh mục vật tư và tồn đầu kỳ từ Excel
- Giao diện tiếng Việt / tiếng Anh, dùng được trên điện thoại

| Phần | Công nghệ |
|---|---|
| Backend | ASP.NET Core 10 Web API, EF Core 10, PostgreSQL 17, FluentValidation, ClosedXML, Serilog |
| Frontend | React 19, Vite, TypeScript, Tailwind CSS v4, TanStack Query, React Hook Form + Zod, i18next |
| Kiểm thử | xUnit, Testcontainers, Vitest, Testing Library |

## Yêu cầu

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org) 22 LTS trở lên (tối thiểu 20.19)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (chạy PostgreSQL và integration test)

## Chạy lần đầu (môi trường dev)

Mở 3 cửa sổ terminal tại thư mục gốc của dự án.

**1. Database**

```bash
docker compose up -d
```

PostgreSQL chạy ở `localhost:5432` (database `lvverp`, user `lvverp`, mật khẩu `lvverp_dev`).
Muốn đổi cổng hoặc mật khẩu: copy `.env.example` thành `.env`, sửa giá trị, rồi cập nhật `ConnectionStrings:Default` của API tương ứng.

**2. Backend**

```bash
cd backend
dotnet tool restore
dotnet run --project src/LvvErp.Api
```

API chạy ở http://localhost:5080. Lần chạy đầu sẽ tự tạo bảng và dữ liệu mẫu (10 bộ phận, 9 chức danh, 10 đơn vị tính, 5 nhóm vật tư, tài khoản admin).
Tài liệu API (Scalar): http://localhost:5080/scalar. Kiểm tra tình trạng hệ thống: http://localhost:5080/health.

**3. Frontend**

```bash
cd frontend
npm install
npm run dev
```

Mở http://localhost:5173 và đăng nhập:

| Tên đăng nhập | Mật khẩu |
|---|---|
| `admin` | `Admin@123` |

> Mật khẩu này chỉ dùng cho môi trường dev (nằm trong `appsettings.Development.json`). Hãy đổi ngay sau khi đăng nhập nếu dùng chung máy chủ.

Tài khoản HR / Employee / Thủ kho (`Storekeeper`) được tạo bằng API `POST /api/users` (cần quyền Admin, thử trực tiếp trên trang Scalar).

### Quyền theo role

| | Admin | HR | Employee | Thủ kho |
|---|:-:|:-:|:-:|:-:|
| Xem danh bạ nhân viên | ✓ | ✓ | ✓ | ✓ |
| Thêm/sửa/xóa, nhập Excel nhân viên | ✓ | ✓ | | |
| Xem danh mục kho, phiếu, tồn kho, thẻ kho | ✓ | ✓ | ✓ | ✓ |
| Sửa danh mục kho, lập và duyệt phiếu, nhập Excel kho | ✓ | | | ✓ |
| Hủy phiếu đã duyệt | ✓ | | | |
| Quản lý tài khoản | ✓ | | | |

## Sử dụng module Kho

**Bắt đầu:** tạo kho (và vị trí nếu kho chia kệ/ô) ở **Danh mục kho → Kho & vị trí**, nhập danh mục vật tư (thêm tay hoặc **Vật tư → Nhập Excel**), rồi nhập tồn đầu kỳ (**Phiếu nhập → Nhập tồn đầu kỳ**). Import tồn đầu kỳ tạo **phiếu nhập nháp** (mỗi kho một phiếu); kiểm tra rồi bấm **Duyệt** để ghi tồn.

**Lập phiếu:**
- Chọn kho, rồi thêm dòng bằng cách gõ hoặc **quét mã vật tư** vào ô cuối danh sách (máy quét mã vạch/QR hoạt động như bàn phím: mã + Enter).
- Phiếu nhập: vật tư quản lý theo lô phải ghi số lô; chọn lô đã có thì tự điền ngày sản xuất, hạn dùng.
- Phiếu xuất / chuyển: chọn chỗ lấy hàng trong danh sách vị trí – lô còn tồn (hạn dùng gần nhất xếp đầu).
- **Lưu nháp** không làm thay đổi tồn. **Duyệt** mới ghi tồn; nếu không đủ tồn, hệ thống báo từng dòng thiếu bao nhiêu và không ghi dòng nào.
- Phiếu đã duyệt không sửa được. Nhầm lẫn thì Admin **Hủy phiếu** (bắt buộc ghi lý do); hệ thống ghi bút toán đảo, lịch sử vẫn giữ trên thẻ kho. Chỉ hủy được khi tồn hiện tại còn đủ.

**In tem QR:** ở danh sách vật tư, tick các dòng rồi bấm **In tem**; hoặc mở phiếu nhập đã duyệt và bấm **In tem** (tem có số lô, hạn dùng). Khi in: chọn đúng khổ giấy (A4 hoặc 50 × 30 mm), tỷ lệ 100%, tắt "Đầu trang và chân trang".

**Báo cáo:** **Tồn kho** (lọc theo kho, nhóm, chỉ hiện vật tư dưới tồn tối thiểu; bấm mũi tên để xem chi tiết theo vị trí/lô) và **Thẻ kho** (tồn đầu kỳ, từng phát sinh, tồn cuối kỳ). Trang **Tổng quan** hiện danh sách vật tư dưới tồn tối thiểu và số phiếu nháp chờ duyệt.

Tồn tối thiểu được so với **tổng tồn của mọi kho**.

## Nhập Excel

Mọi file import dùng chung quy tắc: tải **file mẫu** từ màn hình nhập, điền từ dòng 2, không đổi thứ tự/tên cột; danh mục (bộ phận, ĐVT, kho…) chọn từ danh sách hoặc gõ mã/tên, không phân biệt hoa thường hay có dấu; số thập phân ghi `12,5` hoặc `12.5`; ngày ghi `dd/MM/yyyy`. Có bất kỳ dòng nào lỗi thì toàn bộ file không được lưu, màn hình liệt kê lỗi theo dòng và cột.

| File | Màn hình | Cột bắt buộc |
|---|---|---|
| Nhân viên | Danh bạ nhân viên → Nhập Excel | Mã NV, Họ tên, Bộ phận, Chức danh |
| Danh mục vật tư | Vật tư → Nhập Excel | Mã vật tư, Tên, ĐVT, Nhóm (Quản lý lô: Có/Không) |
| Tồn đầu kỳ | Phiếu nhập → Nhập tồn đầu kỳ | Mã vật tư, Kho, Số lượng (+ Số lô với vật tư quản lý lô) |

### Nhân viên

1. Vào **Danh bạ nhân viên → Nhập Excel → Tải file mẫu**.
2. Điền danh sách vào sheet **Nhân viên**, bắt đầu từ dòng 2, không đổi thứ tự hay tên cột.
   - Cột có dấu `*` là bắt buộc: Mã NV, Họ tên, Bộ phận, Chức danh.
   - Bộ phận, Chức danh: chọn từ danh sách hoặc gõ mã/tên (không phân biệt hoa thường, có dấu hay không đều được).
   - Trạng thái: `Đang làm` / `Tạm nghỉ` / `Đã nghỉ`; để trống nghĩa là `Đang làm`.
3. Chọn file và bấm **Nhập dữ liệu**.

Quy tắc:
- Nếu **có bất kỳ dòng nào lỗi, toàn bộ file sẽ không được lưu**. Màn hình sẽ liệt kê lỗi theo từng dòng và cột để sửa.
- Mặc định, mã NV đã tồn tại sẽ bị báo lỗi. Bật **Cập nhật nếu trùng mã NV** để cập nhật nhân viên đó; khi đó các ô Email, Số điện thoại, Trạng thái để trống sẽ giữ nguyên giá trị cũ.
- Tối đa 5.000 dòng và 10 MB mỗi file.

## Chạy test

```bash
# Backend (Docker phải đang chạy: integration test tự bật một Postgres tạm)
cd backend
dotnet test

# Frontend
cd frontend
npm run typecheck
npm run lint
npm test
```

## Migration database

```bash
cd backend
dotnet dotnet-ef migrations add TenMigration -p src/LvvErp.Infrastructure -s src/LvvErp.Api -o Persistence/Migrations
```

Migration được áp tự động khi API khởi động. Có thể tắt bằng `Database:InitializeOnStartup=false` và áp thủ công:

```bash
dotnet dotnet-ef database update -p src/LvvErp.Infrastructure -s src/LvvErp.Api
```

## Cấu hình

Cấu hình nằm trong `backend/src/LvvErp.Api/appsettings*.json`. Có thể ghi đè bằng biến môi trường, dùng `__` thay cho dấu `:` (ví dụ `Jwt__SigningKey`), hoặc bằng `dotnet user-secrets` khi dev.

| Khóa | Mặc định | Ghi chú |
|---|---|---|
| `ConnectionStrings:Default` | (dev: Postgres local) | **Bắt buộc** ở production |
| `Jwt:SigningKey` | (dev có sẵn) | **Bắt buộc** ở production, tối thiểu 32 ký tự; API không khởi động nếu thiếu |
| `Jwt:AccessTokenMinutes` | `480` | Thời gian hiệu lực của phiên đăng nhập |
| `Seed:AdminUserName` / `Seed:AdminPassword` | `admin` / (dev: `Admin@123`) | Chỉ dùng khi chưa có tài khoản nào |
| `Database:InitializeOnStartup` | `true` | Tự áp migration và seed khi khởi động |
| `Storage:LocalRootPath` | `uploads` | Thư mục lưu ảnh (đường dẫn tuyệt đối hoặc tương đối với thư mục API) |
| `Cors:AllowedOrigins` | dev: `http://localhost:5173` | Chỉ cần khi frontend và API khác domain |
| `RateLimiting:LoginPermitPerMinute` | `10` | Số lần đăng nhập tối đa mỗi phút cho mỗi IP |

Log được ghi ra console và `logs/lvverp-<ngày>.log` (giữ 30 ngày).

## Triển khai (gợi ý)

1. Build frontend: `cd frontend && npm ci && npm run build`, kết quả nằm trong `frontend/dist`.
2. Publish API: `cd backend && dotnet publish src/LvvErp.Api -c Release -o out`.
3. Đặt reverse proxy (Nginx/IIS) phục vụ `frontend/dist`, chuyển `/api` và `/files` sang API. Các đường dẫn khác trả về `index.html` (ứng dụng một trang).
4. Đặt biến môi trường: `ConnectionStrings__Default`, `Jwt__SigningKey`, `Seed__AdminPassword`, `Storage__LocalRootPath` (thư mục có sao lưu).
5. Sao lưu định kỳ database và thư mục ảnh.

## Cấu trúc thư mục

```
backend/     API .NET (Domain / Application / Infrastructure / Api) và test
frontend/    Ứng dụng React
docker-compose.yml
CLAUDE.md    Kiến trúc và quy ước code (đọc trước khi phát triển)
```

## Xử lý sự cố

| Hiện tượng | Cách xử lý |
|---|---|
| `docker compose up` báo cổng 5432 đã dùng | Đặt `POSTGRES_PORT=5433` trong `.env` và sửa `Port=5433` trong connection string |
| API báo `Jwt:SigningKey must be at least 32 bytes` | Đặt khóa ký JWT (biến `Jwt__SigningKey`) |
| Integration test lỗi khi khởi tạo | Kiểm tra Docker Desktop đang chạy |
| Frontend báo "Không kết nối được máy chủ" | Kiểm tra API đang chạy ở cổng 5080 |
| Trang kho báo lỗi sau khi cập nhật code | Khởi động lại API để áp migration mới |
| Tem in bị lệch hoặc thu nhỏ | Trong hộp thoại in: đúng khổ giấy, tỷ lệ 100% (không "Fit to page"), lề "None", tắt đầu/chân trang |
| Ô ngày hiện `mm/dd/yyyy` | Định dạng theo ngôn ngữ trình duyệt/Windows; đặt ngôn ngữ Tiếng Việt để hiện `dd/mm/yyyy` |
| Xóa sạch dữ liệu dev | `docker compose down -v` rồi chạy lại API |
