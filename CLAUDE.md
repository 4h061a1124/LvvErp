# LvvErp – hướng dẫn cho Claude và người phát triển

ERP nội bộ cho nhà máy. Module hiện có: **Danh bạ nhân viên** (kèm đăng nhập, phân quyền, quản lý tài khoản qua API).
Người dùng cuối là người Việt: giao diện mặc định tiếng Việt, có tiếng Anh.

## Cấu trúc

```
backend/                     ASP.NET Core 10, EF Core 10, PostgreSQL
  src/LvvErp.Domain          Entity, enum, hằng số. Không phụ thuộc gì.
  src/LvvErp.Application     Service, DTO, validator, interface. Phụ thuộc Domain + EF Core (chỉ abstractions/LINQ).
  src/LvvErp.Infrastructure  DbContext, cấu hình bảng, migration, JWT, băm mật khẩu, lưu file, Excel (ClosedXML).
  src/LvvErp.Api             Controller, middleware, Program.cs.
  tests/LvvErp.UnitTests         Logic thuần (validator, import, Excel, quyền).
  tests/LvvErp.IntegrationTests  API thật + Postgres thật qua Testcontainers (cần Docker).
frontend/                    React 19, Vite, TypeScript strict, Tailwind v4
docker-compose.yml           PostgreSQL 17 (+ pgAdmin với --profile tools)
```

Mỗi module nằm trong `Features/<Module>/` ở cả 4 project backend và `src/features/<module>/` ở frontend.
Module hiện có: `Identity` (đăng nhập, tài khoản) và `Employees` (nhân viên, bộ phận, chức danh, import).

## Lệnh thường dùng

```bash
docker compose up -d                                   # Postgres
cd backend
dotnet tool restore                                    # dotnet-ef 10 (tool cục bộ, không dùng bản global)
dotnet build
dotnet test                                            # cần Docker cho IntegrationTests
dotnet run --project src/LvvErp.Api                    # http://localhost:5080, Scalar: /scalar
dotnet dotnet-ef migrations add <Ten> -p src/LvvErp.Infrastructure -s src/LvvErp.Api -o Persistence/Migrations

cd frontend
npm run dev                                            # http://localhost:5173, proxy /api và /files sang :5080
npm run typecheck && npm run lint && npm test && npm run build
```

Khi khởi động, API tự áp migration và seed dữ liệu (`Database:InitializeOnStartup`, mặc định `true`).

## Backend – quy ước

### Luồng một request
Controller (mỏng, chỉ gọi service) → `I<Xxx>Service` trong Application → `IAppDbContext` → Postgres.
- Không dùng MediatR/AutoMapper. Map DTO bằng tay trong service (`ToDto`), projection bằng `Select`.
- DTO là `sealed record`, đặt trong `Features/<Module>/<Xxx>Dtos.cs`. Request tạo/sửa dùng chung một record nếu giống nhau (`SaveEmployeeRequest`).
- Service tự gọi `validator.ValidateAndThrowAsync(request)`; validator là `AbstractValidator<T>`, tự đăng ký theo assembly.
- Service đăng ký trong `Application/DependencyInjection.cs`; implementation hạ tầng trong `Infrastructure/DependencyInjection.cs`.

### Lỗi → HTTP (`Api/Infrastructure/GlobalExceptionHandler.cs`)
| Exception | HTTP |
|---|---|
| `FluentValidation.ValidationException` | 400 `ValidationProblemDetails`, key = tên property (PascalCase) |
| `NotFoundException` | 404 |
| `ConflictException`, vi phạm unique index của Postgres | 409 |
| `UnauthorizedException` | 401 |
| `ForbiddenException` | 403 |

Mọi lỗi, kể cả 401/403/404 do framework sinh, đều trả `ProblemDetails` (RFC 7807). Message server viết bằng tiếng Anh; frontend tự dịch theo status/mã lỗi.

### Dữ liệu
- Entity kế thừa `AuditableEntity` (Id là UUIDv7, CreatedAt/By, UpdatedAt/By được interceptor tự điền).
- Xóa mềm: implement `ISoftDeletable`. `Remove()` được interceptor đổi thành `IsDeleted = true`; global query filter tự ẩn. Unique index trên bảng xóa mềm phải có `HasFilter("is_deleted = false")`.
- Tên bảng/cột snake_case (EFCore.NamingConventions). Enum lưu dạng chuỗi (`HasConversion<string>()`).
- Độ dài cột khai báo bằng hằng số trên entity (`Employee.CodeMaxLength`), dùng chung cho EF config, validator và import.
- Tìm kiếm không dấu: `AppDbFunctions.Unaccent(...)` (map sang `unaccent()` của Postgres). So khớp mã/tên trong C#: `TextNormalizer.ToMatchKey`.
- Dữ liệu danh mục mặc định seed trong `Infrastructure/Persistence/DbInitializer.cs`, chỉ khi bảng trống. Không seed bằng `HasData`.
- Mã nghiệp vụ (mã NV, tên đăng nhập) được chuẩn hóa trước khi lưu và so sánh: `Employee.NormalizeCode` (chữ hoa), `User.NormalizeUserName` (chữ thường).

### Phân quyền
- Role: `Admin`, `HR`, `Employee` (`Domain/Features/Identity/Roles.cs`).
- Quyền (permission) gán cho role ở **một chỗ duy nhất**: `Application/Common/Security/Permissions.cs`. Tên quyền cũng là tên policy.
- Controller dùng `[Authorize(Policy = Permissions.Xxx)]`. Mặc định mọi endpoint yêu cầu đăng nhập (fallback policy); endpoint công khai phải ghi rõ `[AllowAnonymous]`.
- JWT có claim `sstamp`. Đổi mật khẩu, đổi role hoặc khóa tài khoản sẽ đổi security stamp, và mọi token cũ bị từ chối ngay ở `OnTokenValidated`.
- Frontend nhận danh sách quyền từ `/api/auth/me`; khi thêm quyền mới phải cập nhật cả `frontend/src/lib/permissions.ts`.

### API
- Route: `api/<resource>` số nhiều, kebab-case. JSON camelCase, enum dạng chuỗi.
- Danh sách có phân trang trả `PagedResult<T>` (`items, page, pageSize, totalCount, totalPages`); `pageSize` tối đa 100.
- Sắp xếp: `?sort=fullName` tăng dần, `?sort=-fullName` giảm dần.
- Upload: `multipart/form-data`, trường `file`. Kiểm tra loại file bằng magic bytes (`ImageFormat`), không tin tên file hay Content-Type.
- File đã lưu được phục vụ tại `/files/...` (công khai, tên UUID ngẫu nhiên) qua `IFileStorage`; hiện lưu đĩa cục bộ (`Storage:LocalRootPath`).
- Import Excel: lỗi cấp file → 400; lỗi từng dòng → 200 với `succeeded=false` và danh sách `{ row, column, errorCode, message, value }`. Có một dòng lỗi thì không lưu dòng nào.

## Frontend – quy ước

```
src/app/            router (trang sau đăng nhập lazy-load), providers, layout (Sidebar, Topbar)
src/components/ui/  Button, Field/Input/Select, Dialog, ConfirmDialog, Toast, Menu, Pagination, PageHeader
src/features/<m>/   api.ts (hook TanStack Query), types.ts, schema.ts (zod), components/, <M>Page.tsx
src/lib/            api (axios + token), auth, errors, i18n, permissions, storage, hooks
src/locales/        vi.ts (nguồn), en.ts (bắt buộc cùng cấu trúc: kiểu `Messages`)
```

- Gọi API chỉ qua hook trong `features/<m>/api.ts`. Query key gom theo module (`employeeKeys`); mutation thành công thì invalidate.
- Form: react-hook-form + zod. **Message trong schema zod là key i18n** (`'validation.required'`), hiển thị bằng `t(key)`. Luật validate phải khớp validator backend.
- Lỗi server: `getFieldErrors` (gắn vào trường), `getErrorMessage` (toast). Không hiển thị message tiếng Anh của server khi có bản dịch.
- i18n: mọi chuỗi hiển thị phải nằm trong `locales/vi.ts` và `en.ts`; key được TypeScript kiểm tra. Mặc định `vi`, lựa chọn lưu ở localStorage.
- Hộp thoại có state (form, import): **chỉ mount khi mở** (`{open && <XxxDialog onClose=… />}`), không reset state bằng `useEffect`.
- Form có ô select lấy dữ liệu danh mục: chờ danh mục tải xong rồi mới mount form, nếu không giá trị cũ sẽ không được chọn sẵn.
- Bộ lọc danh sách lưu trên URL (`useSearchParams`), xem `useEmployeeFilters`.
- Trên điện thoại (`< md`): bảng chuyển thành danh sách thẻ; menu trái thành ngăn trượt (`< lg`).
- `localStorage` luôn truy cập qua `lib/storage.ts` (đã bọc try/catch).

### Phong cách giao diện (bắt buộc)
Giao diện phải trông như phần mềm nghiệp vụ do người làm, **không có "chất AI"**:
- Nền `zinc-100`, bề mặt trắng, viền `zinc-200`; một màu thương hiệu `brand-600` (#1f5c99). Màu trạng thái chỉ dùng cho trạng thái.
- Bo góc nhỏ (`rounded-[3px]`/`[4px]`), phân tách bằng viền mảnh, gần như không có shadow (chỉ ở popup/dialog).
- Font Be Vietnam Pro; chữ 13–14px; bảng dày dữ liệu; tiêu đề cột viết thường, không in hoa giãn chữ.
- Không dùng: gradient, chữ gradient, glassmorphism, emoji, hero/banner chào mừng, số liệu giả, card bo tròn to có shadow, icon trên mọi nút, giao diện mặc định của shadcn.
- Câu chữ ngắn, đúng nghiệp vụ ("Thêm nhân viên", "Nhập Excel"), không giọng quảng cáo.
- Tự viết component nhỏ trong `components/ui` thay vì thêm thư viện UI.

## Kiểm thử
- Backend: unit test cho logic thuần; integration test đi qua HTTP thật (`ApiTestBase`). Dữ liệu test phải **tự tạo và có tên/mã duy nhất** (`UniqueCode()`, `UniqueName()`), vì các test dùng chung một database.
- Frontend: Vitest + Testing Library; test viết với chuỗi tiếng Việt (i18n được khởi tạo trong `src/test/setup.ts`).
- Thêm tính năng thì thêm test ở cả hai phía. Trước khi báo xong: `dotnet test` và `npm run typecheck && npm run lint && npm test && npm run build`.

## Checklist thêm module mới (ví dụ `Warehouse`)
1. Domain: `Features/Warehouse/*.cs` (entity kế thừa `AuditableEntity`, thêm `ISoftDeletable` nếu cần, hằng số độ dài).
2. Application: thêm `DbSet` vào `IAppDbContext`; DTO, validator, service trong `Features/Warehouse/`; quyền mới trong `Permissions.cs` (gán cho role); đăng ký service.
3. Infrastructure: `DbSet` trong `AppDbContext`, `IEntityTypeConfiguration`, seed danh mục (nếu có) trong `DbInitializer`; tạo migration.
4. Api: controller trong `Features/Warehouse/` với `[Authorize(Policy = …)]`.
5. Test: unit + integration (kể cả trường hợp bị 403).
6. Frontend: `features/warehouse/` (types, api, schema, page), route trong `app/router.tsx`, mục menu trong `Sidebar.tsx`, quyền trong `lib/permissions.ts`, chuỗi trong `vi.ts` + `en.ts`.

## Lưu ý đã gặp
- Git Bash trên Windows gửi tiếng Việt qua `curl -d` sai mã hóa (lỗi 400 ở JSON). Ghi body ra file UTF-8 rồi dùng `--data-binary @file`.
- `UseSerilogRequestLogging()` phải đứng trước `UseExceptionHandler()` để log đúng status code cuối cùng.
- `/files` chạy trên nhánh pipeline riêng; file không tồn tại trả 404 ngay, không rơi xuống pipeline yêu cầu đăng nhập.
- Bản global `dotnet-ef` trên máy (9.x) không dùng được với EF Core 10; luôn dùng `dotnet dotnet-ef` (tool cục bộ).

## Chưa làm / hạn chế hiện tại
- Chưa có refresh token (access token 8 giờ, cấu hình `Jwt:AccessTokenMinutes`).
- Quản lý tài khoản mới có API (`/api/users`, chỉ Admin), chưa có giao diện.
- Chưa có giao diện quản lý danh mục Bộ phận/Chức danh (chỉ có dữ liệu seed).
- Token lưu ở localStorage; ảnh nhân viên xem được không cần đăng nhập (tên file không đoán được).
- Lỗi 400/409 không gắn với trường cụ thể đang hiển thị nguyên văn tiếng Anh từ server.
