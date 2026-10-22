# LvvErp – hướng dẫn cho Claude và người phát triển

ERP nội bộ cho nhà máy. Module hiện có:
- **Danh bạ nhân viên** (kèm đăng nhập, phân quyền, giao diện quản lý tài khoản)
- **Vật tư & Kho**: danh mục, phiếu nhập/xuất/chuyển, tồn theo lô, tồn kho, thẻ kho, cảnh báo, tem QR, import Excel
- **Mua hàng**: nhà cung cấp, đề nghị mua hàng theo bộ phận, đơn đặt hàng duyệt theo hạn mức, in đơn A4, nhận hàng từ đơn, báo cáo

Người dùng cuối là người Việt: giao diện mặc định tiếng Việt, có tiếng Anh.

## Cấu trúc

```
backend/                     ASP.NET Core 10, EF Core 10, PostgreSQL
  src/LvvErp.Domain          Entity, enum, hằng số. Không phụ thuộc gì.
  src/LvvErp.Application     Service, DTO, validator, interface. Phụ thuộc Domain + EF Core (chỉ abstractions/LINQ).
  src/LvvErp.Infrastructure  DbContext, cấu hình bảng, migration, JWT, băm mật khẩu, lưu file, Excel (ClosedXML), sổ kho (SQL).
  src/LvvErp.Api             Controller, middleware, Program.cs.
  tests/LvvErp.UnitTests         Logic thuần (validator, import, Excel, quyền, ghi sổ kho).
  tests/LvvErp.IntegrationTests  API thật + Postgres thật qua Testcontainers (cần Docker).
frontend/                    React 19, Vite, TypeScript strict, Tailwind v4
docker-compose.yml           PostgreSQL 17 (+ pgAdmin với --profile tools)
```

Mỗi module nằm trong `Features/<Module>/` ở cả 4 project backend và `src/features/<module>/` ở frontend.
Module hiện có: `Identity` (đăng nhập, tài khoản), `Employees` (nhân viên, bộ phận, chức danh, import), `Inventory`, `Purchasing` (xem các mục riêng bên dưới). Frontend có thêm `features/users` (quản lý tài khoản) và `features/dashboard`.

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
Nếu API đang chạy (khóa `bin/Debug`), build/test bằng `-c Release` thay vì tắt API của người khác.

## Backend – quy ước

### Luồng một request
Controller (mỏng, chỉ gọi service) → `I<Xxx>Service` trong Application → `IAppDbContext` → Postgres.
- Không dùng MediatR/AutoMapper. Map DTO bằng tay trong service (`ToDto`), projection bằng `Select`.
- DTO là `sealed record`, đặt trong `Features/<Module>/<Xxx>Dtos.cs`. Request tạo/sửa dùng chung một record nếu giống nhau (`SaveEmployeeRequest`).
- Service tự gọi `validator.ValidateAndThrowAsync(request)`; validator là `AbstractValidator<T>`, tự đăng ký theo assembly. Rule cần database (tồn tại, thuộc kho nào…) kiểm tra trong service và ném `ValidationException` với đường dẫn trường (`Lines[2].LotId`).
- Service đăng ký trong `Application/DependencyInjection.cs`; implementation hạ tầng trong `Infrastructure/DependencyInjection.cs`.

### Lỗi → HTTP (`Api/Infrastructure/GlobalExceptionHandler.cs`)
| Exception | HTTP |
|---|---|
| `FluentValidation.ValidationException` | 400 `ValidationProblemDetails`, key = tên property (PascalCase) |
| `NotFoundException` | 404 |
| `ConflictException(message, errorCode, details)` | 409, kèm extension `errorCode` (và `details` nếu có) |
| Vi phạm unique index | 409 |
| Vi phạm khóa ngoại khi **xóa** | 409 `errorCode: InUse` (không cần tự kiểm tra từng bảng tham chiếu) |
| `UnauthorizedException` / `ForbiddenException` | 401 / 403 |

- Mã lỗi 409 nằm ở `Application/Common/Exceptions/AppExceptions.cs` (`ErrorCodes`): `DuplicateCode`, `DuplicateName`, `DuplicateTaxCode`, `DuplicateUserName`, `EmployeeAlreadyLinked`, `CannotChangeOwnAccess`, `InUse`, `DocumentNotDraft`, `DocumentNotApproved`, `InvalidStatus` (thao tác không hợp với trạng thái hiện tại), `InsufficientStock` (details = danh sách `{lineNo, materialCode, lotNumber, requested, available}`), `TrackLotsLocked`, `NoDepartment`, `RequestHasOrders`, `RequestHasOpenOrders` (details = số các đơn), `OverReceipt` (details = danh sách `{lineNo, materialCode, ordered, received, receiving, allowed}`). Thêm mã mới thì thêm bản dịch `errors.codes.*` ở frontend.
- Mọi lỗi, kể cả 401/403/404 do framework sinh, đều trả `ProblemDetails` (RFC 7807). Message server viết bằng tiếng Anh; frontend dịch theo `errorCode`/status.

### Dữ liệu
- Entity kế thừa `Entity`/`AuditableEntity`. **Id là UUIDv7 do code sinh**; `AppDbContext` khai báo `ValueGeneratedNever` cho mọi entity để EF coi entity con mới (thêm qua navigation) là `Added`, không phải `UPDATE`. UUIDv7 tạo trong cùng mili-giây **không có thứ tự** → không dùng Id để sắp xếp theo thời gian.
- Xóa mềm: implement `ISoftDeletable`. Unique index trên bảng xóa mềm phải có `HasFilter("is_deleted = false")`. Danh mục kho không xóa mềm: mục đã dùng thì chuyển `IsActive = false`.
- Tên bảng/cột snake_case. Enum lưu dạng chuỗi. Số lượng `decimal(18,3)`.
- **Cột tên người dùng sắp xếp (họ tên, tên bộ phận, tên vật tư…) dùng collation ICU `vi_vn`** (`.UseCollation(AppDbContext.VietnameseCollation)`), vì collation mặc định xếp Đ, Ê, Ơ sau Z.
- Độ dài cột khai báo bằng hằng số trên entity (`Material.CodeMaxLength`), dùng chung cho EF config, validator và import.
- Tìm kiếm không dấu: `AppDbFunctions.Unaccent(...)`. So khớp mã/tên trong C#: `TextNormalizer.ToMatchKey`, `LookupMatcher`.
- Dữ liệu danh mục mặc định seed trong `DbInitializer` (chỉ khi bảng trống), không dùng `HasData`.
- Mã nghiệp vụ chuẩn hóa trước khi lưu và so sánh: mã chữ hoa (`NormalizeCode`), tên đăng nhập chữ thường.
- Ngày nghiệp vụ ("hôm nay") lấy từ `BusinessClock` (giờ Việt Nam), không dùng `DateTime.Now`.
- Transaction: dùng `DbTransactionScope.BeginAsync(db)`. Scope tự tham gia transaction đang có, chỉ scope mở ra mới commit, nên service dùng riêng được mà cũng gộp được vào thao tác lớn "có lỗi thì không lưu gì" (import tồn đầu kỳ).
- Khóa dòng: `IRowLocker.LockAsync<TEntity>(ids)` (`SELECT … FOR UPDATE`, theo thứ tự id, tên bảng lấy từ model EF). Số chứng từ (`PN`, `PX`, `PC`, `DN`, `DH`) cấp qua `IStockLedger.NextDocumentNumberAsync`.
- Tiền: VND làm tròn đến đồng (`decimal(18,0)`), đơn giá tối đa 2 số lẻ (`decimal(18,2)`), làm tròn nửa xa số 0 (`PurchasingRules.RoundMoney`; frontend `orderModel.roundMoney` phải cho cùng kết quả).

### Phân quyền
- Role: `Admin`, `HR`, `Employee`, `Storekeeper` (thủ kho), `Purchaser` (nhân viên mua hàng), `DepartmentHead` (trưởng bộ phận), `Director` (giám đốc) (`Domain/Features/Identity/Roles.cs`).
- Quyền gán cho role ở **một chỗ duy nhất**: `Application/Common/Security/Permissions.cs`. Tên quyền cũng là tên policy.

| Quyền | Admin | HR | Employee | Thủ kho | Mua hàng | Trưởng BP | Giám đốc |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| `Employees.View`, `Inventory.View` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `Employees.Manage` | ✓ | ✓ | | | | | |
| `Users.Manage`, `Inventory.CancelDocuments`, `Purchasing.ManageSettings` | ✓ | | | | | | |
| `Inventory.ManageCatalog`, `Inventory.EditDocuments`, `Inventory.ApproveDocuments` | ✓ | | | ✓ | | | |
| `Purchasing.CreateRequests` (đề nghị cho bộ phận mình) | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `Purchasing.View` (NCC, mọi đề nghị, đơn, báo cáo) | ✓ | | | ✓ | ✓ | ✓ | ✓ |
| `Purchasing.ManageSuppliers`, `Purchasing.ManageOrders` | ✓ | | | | ✓ | | |
| `Purchasing.ApproveRequests`, `Purchasing.ApproveOrders` (`MoneyApprovals`) | | | | | | ✓ | ✓ |

- **Admin có mọi quyền trừ `Permissions.MoneyApprovals`**: cấu hình hệ thống nhưng không duyệt chứng từ có tiền. Có unit test giữ quy tắc này.
- Quyền chỉ là điều kiện cần. Quy tắc theo người/bộ phận nằm trong service, dùng `IUserScopeProvider` (`UserScope`: userId, userName, roles, employeeId, departmentId; đọc lại từ DB mỗi request) và các hàm thuần `PurchasingRules.CanApproveRequest`/`CanApproveOrder`. Bộ phận của người dùng = bộ phận của nhân viên gắn với tài khoản; chưa gắn thì không lập được đề nghị (409 `NoDepartment`).
- **Không ai duyệt chứng từ do mình lập** (đề nghị: so `RequesterId`; đơn: so `CreatedBy` và `SubmittedBy`).
- DTO chi tiết đề nghị/đơn trả về `actions` (canEdit, canApprove…) tính bằng chính các quy tắc đó; frontend ẩn/hiện nút theo `actions`, không tự suy luận lại.
- Controller dùng `[Authorize(Policy = Permissions.Xxx)]`. Mặc định mọi endpoint yêu cầu đăng nhập; endpoint công khai phải ghi rõ `[AllowAnonymous]`.
- JWT có claim `sstamp`. Đổi mật khẩu, đổi role hoặc khóa tài khoản sẽ đổi security stamp, mọi token cũ bị từ chối ngay.
- Frontend nhận danh sách quyền từ `/api/auth/me`; thêm quyền mới phải cập nhật cả `frontend/src/lib/permissions.ts`.

### API
- Route: `api/<resource>` số nhiều, kebab-case. JSON camelCase, enum dạng chuỗi, ngày dạng `yyyy-MM-dd`.
- Danh sách có phân trang trả `PagedResult<T>` (`items, page, pageSize, totalCount, totalPages`); `pageSize` tối đa 100.
- Sắp xếp: `?sort=name` tăng dần, `?sort=-name` giảm dần. Danh mục chọn trong form: `?activeOnly=true`.
- Upload: `multipart/form-data`, trường `file`. Ảnh qua `PhotoUploader` (kiểm tra magic bytes, tối đa 2 MB).
- File đã lưu phục vụ tại `/files/...` (công khai, tên UUID ngẫu nhiên) qua `IFileStorage`.

### Import Excel (`Application/Common/Import`)
- Mỗi import khai báo danh sách `ExcelColumn` (key, tiêu đề tiếng Việt, cột text, dropdown) và `ExcelTemplate`; đọc/ghi file qua `IExcelImportFile` (một implementation ClosedXML cho mọi import). Ô ngày được đọc thành `yyyy-MM-dd`, ô số theo invariant culture.
- Kiểm tra từng dòng bằng một class validator **thuần** (không truy cập DB) để unit test được: `EmployeeImportValidator`, `MaterialImportValidator`, `OpeningBalanceValidator`.
- Đọc giá trị người dùng gõ: `ImportParsing` (số `12,5`/`12.5`, ngày `dd/MM/yyyy`, Có/Không), `LookupMatcher` (khớp mã hoặc tên, không dấu).
- Lỗi cấp file → 400; lỗi từng dòng → 200 với `succeeded=false` và `{ row, column, errorCode, message, value }`. Có một dòng lỗi thì không lưu dòng nào. Cập nhật khi trùng mã: ô tùy chọn để trống giữ nguyên giá trị cũ.

## Module Kho (Inventory)

- **Danh mục:** Đơn vị tính, Nhóm vật tư (`CatalogService<T>` chung), Kho, Vị trí (không bắt buộc), Vật tư (`TrackLots` bật/tắt theo vật tư; không đổi được khi đã có trên phiếu), Lô (tạo khi duyệt phiếu nhập).
- **Chứng từ** (`StockDocument`, 3 loại `Receipt`/`Issue`/`Transfer`): Nháp → Đã duyệt → (Admin) Đã hủy. Chỉ phiếu đã duyệt mới làm thay đổi tồn. Số phiếu `PN|PX|PC-yyyy-00001` cấp khi tạo nháp, không cấp lại sau khi xóa.
- **Sổ kho** `stock_transactions`: **chỉ thêm, không sửa, không xóa**. Duyệt phiếu thêm bút toán; hủy phiếu thêm bút toán đảo (`IsReversal`, ngày hủy).
- **Tồn hiện tại** `stock_balances` theo (vật tư, kho, vị trí, lô): **chỉ ghi qua `IStockLedger`** (SQL nguyên tử: `INSERT … ON CONFLICT` để cộng, `UPDATE … WHERE quantity >= x` để trừ), cùng transaction với sổ kho. `CHECK (quantity >= 0)` là lớp bảo vệ cuối. Không bao giờ sửa bảng này bằng EF change tracking.
- **Ghi đồng thời:** mọi thao tác ghi trên một phiếu (sửa, xóa, duyệt, hủy) khóa dòng phiếu (`LockDocumentAsync` = `SELECT … FOR UPDATE`) **trước** khi đọc trạng thái. Bút toán áp theo thứ tự khóa cố định (`StockPosting.InLockOrder`) để tránh deadlock. Có integration test duyệt song song (đã kiểm chứng: gỡ khóa thì test fail).
- Quy tắc chuyển phiếu thành bút toán là hàm thuần trong `StockPosting`; thẻ kho tính tồn lũy kế bằng `StockCard.Build`.
- **Thẻ kho** sắp xếp theo ngày → `CreatedAt` của lần ghi sổ → phiếu → số dòng → xuất trước nhập.
- **Tồn tối thiểu** so với **tổng tồn mọi kho**.
- **Tem QR:** nội dung QR là mã vật tư (chữ thuần). Trang `/inventory/labels` nhận danh sách tem qua router state; in A4 3×8 (70×36 mm) hoặc tem cuộn 50×30 mm bằng `@page`. Không đổi kích thước tem (giấy tem cắt sẵn).

## Module Mua hàng (Purchasing)

- **Nhà cung cấp** (`Supplier`): mã duy nhất, mã số thuế duy nhất khi có (`^\d{10}(-\d{3})?$`, unique index có lọc), số ngày công nợ 0–365 (tự điền vào đơn mới). Đã có đơn thì không xóa được (409 `InUse`), chuyển "Ngừng dùng". Import Excel theo quy ước chung.
- **Cài đặt** (`PurchasingSettings`, một dòng, `SingletonId`): hạn mức duyệt của Giám đốc (mặc định 50.000.000, so với tổng **đã gồm VAT**, từ hạn mức trở lên là Giám đốc), phòng mua hàng (seed bộ phận `MH`), thông tin công ty in trên đơn.
- **Đề nghị mua hàng** (`PurchaseRequest`, `DN-yyyy-00001`): lập cho bộ phận của người lập; người không có `Purchasing.View` chỉ thấy đề nghị bộ phận mình (đề nghị bộ phận khác trả 404). Nháp → Chờ duyệt → Đã duyệt / Từ chối (bắt buộc lý do; người lập sửa thì quay về Nháp). Sau khi duyệt, trạng thái **tự tính** theo số đã lên đơn: Đã đặt một phần / Đã đặt đủ (`PurchasingRules.RequestStatusAfterOrdering`, gọi lại mỗi khi đơn được tạo/sửa/hủy/xóa). Hủy chỉ khi chưa lên đơn nào (409 `RequestHasOrders`); Đóng không được khi còn đơn chưa duyệt dùng đề nghị (409 `RequestHasOpenOrders`). Mỗi vật tư một dòng.
- **Đơn đặt hàng** (`PurchaseOrder`, `DH-yyyy-00001`): mọi dòng phải lấy từ dòng đề nghị đã duyệt cùng vật tư; `PurchaseOrderLineSource` ghi dòng đơn lấy bao nhiêu từ dòng đề nghị nào (`PurchasingRules.Allocate`: phân bổ lần lượt, nguồn hết vẫn giữ dòng 0 để biết nguồn gốc). Đơn nháp cũng giữ số lượng của đề nghị. Đặt vượt phần đề nghị còn lại phải có `OverOrderReason`, người duyệt thấy lý do. Mỗi vật tư một dòng. Thành tiền dòng làm tròn đồng, VAT (KCT/0/5/8/10) tính một lần trên tổng tiền hàng. Nháp → Chờ duyệt (cấp duyệt `ApprovalLevel` chốt lúc gửi) → Đã duyệt / Từ chối (sửa được, về Nháp) → Đang nhận / Đã nhận đủ (tự tính) → Đóng (chỉ khi đã nhận một phần); Hủy khi chưa nhận gì, trả số lượng cho đề nghị.
- **Nhận hàng**: phiếu nhập lý do `Purchase` có thể gắn `PurchaseOrderId`; khi đó mọi dòng phải có `PurchaseOrderLineId` cùng vật tư, kho phải là kho nhận của đơn, đơn phải Đã duyệt/Đang nhận. Đã nhận = tổng dòng phiếu **đã duyệt** gắn với dòng đơn. Lúc duyệt phiếu (`IPurchaseReceiving.ApplyApprovalAsync`) chặn nhận quá `đặt × (1 + dung sai%)` (`Material.ReceiptTolerancePercent`, làm tròn xuống 3 số lẻ) với 409 `OverReceipt`, rồi cập nhật trạng thái đơn; hủy phiếu thì tính lại.
- **Ghi đồng thời**: thứ tự khóa cố định để không deadlock — phiếu kho → đơn; đơn → các đề nghị (theo id, một lần cho cả nguồn cũ lẫn mới khi sửa đơn); thao tác trên đề nghị chỉ khóa đề nghị. Có test song song cho sửa hai đơn cùng lấy một đề nghị và duyệt hai phiếu cùng nhận một đơn; đã kiểm chứng gỡ khóa thì test fail.
- **Hộp "Chờ tôi duyệt"** (`api/purchasing/approvals`) là cùng quy tắc duyệt viết thành truy vấn; sửa quy tắc duyệt thì sửa cả hai chỗ.
- Báo cáo: `api/purchasing/reports/summary`, `overdue-lines`; giá gần nhất `api/purchase-orders/last-prices` và lịch sử giá `price-history` chỉ tính đơn đã duyệt trở đi.

## Frontend – quy ước

```
src/app/            router (trang sau đăng nhập lazy-load), providers, layout (Sidebar, Topbar)
src/components/ui/  Button, Field/Input/Select, Dialog, ConfirmDialog, Toast, Menu, Pagination, PageHeader, AsyncCombobox
src/components/     ImportDialog, ImportErrorTable, PhotoField (dùng chung giữa các module)
src/features/<m>/   api.ts (hook TanStack Query), types.ts, schema.ts (zod), components/, <M>Page.tsx
src/lib/            api, auth, errors, i18n, permissions, storage, hooks, format, text, photo, import, types, useListParams
src/locales/        vi.ts (nguồn), en.ts (bắt buộc cùng cấu trúc: kiểu `Messages`)
```

- Gọi API chỉ qua hook trong `features/<m>/api.ts`. Query key gom theo module; mutation thành công thì invalidate. Mọi thứ thay đổi khi tồn kho biến động nằm dưới `stockKeys.all` (`['inventory','stock']`); duyệt/hủy phiếu invalidate cả `documentKeys.all` và `stockKeys.all`.
- Form: react-hook-form + zod. **Message trong schema zod là key i18n** (`'validation.required'`). Luật validate phải khớp validator backend. Logic form phức tạp đặt trong module thuần để unit test (`documents/documentModel.ts`).
- Lỗi server: `getFieldErrors` (gắn vào trường), `getErrorCode` (409 có mã → gắn vào trường cụ thể), `getErrorMessage` (toast; tự dịch `errors.codes.*`).
- i18n: mọi chuỗi hiển thị nằm trong `locales/vi.ts` và `en.ts`. Số lượng hiển thị bằng `formatQuantity`, ô nhập số dùng `parseQuantity` (nhận `12,5`), ngày hiển thị `formatDate` (`dd/MM/yyyy`).
- Hộp thoại có state: **chỉ mount khi mở** (`{open && <XxxDialog onClose=… />}`), không reset state bằng `useEffect`.
- Form có ô select lấy dữ liệu danh mục: chờ danh mục (và bản ghi) tải xong rồi mới mount form, nếu không giá trị cũ sẽ không được chọn sẵn.
- Bộ lọc danh sách lưu trên URL bằng `useListParams(DEFAULTS)` (`DEFAULTS` là hằng số cấp module).
- Ô chọn có tìm kiếm: `AsyncCombobox`. Enter chạy tìm kiếm ngay và chọn bản ghi khớp chính xác, để **máy quét mã** (gõ mã + Enter) dùng được. Ô không có `<label>` thì truyền `ariaLabel`.
- Trên điện thoại (`< md`): bảng chuyển thành danh sách thẻ; menu trái thành ngăn trượt (`< lg`). Phần không in ra giấy thêm `print:hidden`.
- Tên file không được chỉ khác nhau chữ hoa/thường (Windows): ví dụ `documentModel.ts` cạnh `DocumentForm.tsx`.
- Mua hàng: đề nghị, đơn, hộp duyệt và báo cáo nằm dưới `purchasingKeys.documents`; mọi mutation invalidate cả khóa này. Duyệt/hủy phiếu kho cũng invalidate nó (phiếu nhập từ đơn đổi trạng thái đơn).
- Logic form mua hàng thuần, có test: `requests/requestModel.ts`, `orders/orderModel.ts` (gộp dòng đề nghị, tính tiền, đặt dư, `parsePrice`: tiếng Việt dấu chấm là phân cách nghìn). Số tiền bằng chữ: `lib/moneyWords.ts`.
- Menu trái ẩn mục theo `permission`; mục "Chờ tôi duyệt" hiện số chứng từ đang chờ (gọi lại mỗi phút).
- Ô **disabled** đăng ký bằng react-hook-form sẽ bị bỏ giá trị khi submit. Muốn khóa một trường (kho của phiếu nhập từ đơn), hiện một `Input` chỉ đọc không đăng ký, giữ giá trị trong `defaultValues`.
- Trang in đơn đặt hàng (`/purchasing/orders/:id/print`) luôn in tiếng Việt, khổ A4 bằng `@page`; đơn chưa duyệt in kèm chữ "BẢN NHÁP".

### Phong cách giao diện (bắt buộc)
Giao diện phải trông như phần mềm nghiệp vụ do người làm, **không có "chất AI"**:
- Nền `zinc-100`, bề mặt trắng, viền `zinc-200`; một màu thương hiệu `brand-600` (#1f5c99). Màu trạng thái chỉ dùng cho trạng thái (chấm màu + chữ).
- Bo góc nhỏ (`rounded-[3px]`/`[4px]`), phân tách bằng viền mảnh, gần như không có shadow (chỉ ở popup/dialog).
- Font Be Vietnam Pro; chữ 13–14px; bảng dày dữ liệu; tiêu đề cột viết thường. Thao tác trong bảng là chữ "Sửa | Xóa", mũi tên sắp xếp là icon `ArrowUp`/`ArrowDown`.
- Không dùng: gradient, chữ gradient, glassmorphism, emoji, hero/banner chào mừng, số liệu giả, card bo tròn to có shadow, icon trên mọi nút, giao diện mặc định của shadcn.
- Câu chữ ngắn, đúng nghiệp vụ ("Thêm vật tư", "Lưu và duyệt"), không giọng quảng cáo.
- Tự viết component nhỏ trong `components/ui` thay vì thêm thư viện UI.

## Kiểm thử
- Backend: unit test cho logic thuần; integration test đi qua HTTP thật (`ApiTestBase`, `InventoryTestBase`). Dữ liệu test phải **tự tạo và có tên/mã duy nhất** (`UniqueCode()`, `UniqueName()`), vì các test dùng chung một database.
- Thay đổi về đồng thời phải có test chạy song song (`Task.WhenAll`), và nên kiểm chứng test thực sự fail khi gỡ phần bảo vệ. Lưu ý: **tạo** chứng từ giữ khóa dòng `document_sequences` tới khi commit, nên hai lần tạo song song vốn đã chạy tuần tự; muốn kiểm chứng một khóa khác thì test bằng thao tác sửa/duyệt.
- Test mua hàng dùng `PurchasingTestBase`: `UserInAsync("SX", Roles.DepartmentHead)` tạo nhân viên + tài khoản gắn bộ phận, `ApprovedRequestAsync(...)` tạo đề nghị đã duyệt.
- Frontend: Vitest + Testing Library; test viết với chuỗi tiếng Việt (i18n khởi tạo trong `src/test/setup.ts`).
- Kiểm tra giao diện thật: chạy API/Vite riêng ở cổng khác (5081/5174) với database riêng (`ConnectionStrings__Default=…lvverp_ui`) để không đụng DB dev, rồi xóa database đó khi xong.
- Trước khi báo xong: `dotnet test` và `npm run typecheck && npm run lint && npm test && npm run build`.

## Checklist thêm module mới
1. Domain: `Features/<M>/*.cs` (entity kế thừa `AuditableEntity`, hằng số độ dài).
2. Application: thêm `DbSet` vào `IAppDbContext`; DTO, validator, service trong `Features/<M>/`; quyền mới trong `Permissions.cs` (gán cho role); đăng ký service.
3. Infrastructure: `DbSet` trong `AppDbContext`, `IEntityTypeConfiguration` (cột tên dùng collation `vi_vn`), seed danh mục (nếu có) trong `DbInitializer`; tạo migration.
4. Api: controller trong `Features/<M>/` với `[Authorize(Policy = …)]`.
5. Test: unit + integration (kể cả trường hợp bị 403).
6. Frontend: `features/<m>/` (types, api, schema, page), route trong `app/router.tsx`, mục menu trong `Sidebar.tsx`, quyền trong `lib/permissions.ts`, chuỗi trong `vi.ts` + `en.ts` (kể cả `errors.codes.*` mới).

## Lưu ý đã gặp
- Git Bash trên Windows gửi tiếng Việt qua `curl -d` sai mã hóa. Ghi body ra file UTF-8 rồi dùng `--data-binary @file`.
- `UseSerilogRequestLogging()` phải đứng trước `UseExceptionHandler()` để log đúng status code cuối cùng.
- `/files` chạy trên nhánh pipeline riêng; file không tồn tại trả 404 ngay.
- Bản global `dotnet-ef` (9.x) không dùng được với EF Core 10; luôn dùng `dotnet dotnet-ef`.
- Khôi phục file bằng `mv file.bak file` giữ mốc thời gian cũ, MSBuild không build lại → test chạy trên bản build cũ. Sau khi khôi phục hãy `touch` file.
- Playwright: `emulateMedia({ media: 'screen' })` ép CSS màn hình cả khi xuất PDF; reset bằng `media: null`. Sau khi bấm link tới trang lazy-load, chờ heading của trang mới rồi mới thao tác, nếu không có thể thao tác nhầm trên trang cũ và hủy lần điều hướng. Chụp ảnh trang có dữ liệu thì chờ dữ liệu hiện ra trước, không chụp lúc "Đang tải...".
- Bash heredoc hoặc `node -e` chứa nhiều dấu nháy/backtick (chuỗi locale) có thể báo `unexpected EOF`. Ghi script ra file bằng công cụ Write rồi chạy `node file.mjs`.
- Sửa file bằng script Node: `s.replace(from, to)` diễn giải `$&`, `` $` ``, `$'` trong chuỗi thay thế (từng làm nhân đôi nửa đầu CLAUDE.md vì nội dung có regex kết thúc bằng `$`). Dùng `s.replace(from, () => to)` hoặc `split/join`.
- Chèn chuỗi vào `vi.ts`/`en.ts` bằng script: anchor phải kết thúc đúng chỗ cần chèn (anchor kéo dài vào object con sẽ chèn nhầm vào trong object đó). `en.ts` có kiểu `Messages` nên tsc bắt thiếu/thừa key, nhưng key ghép động (`` t(`x.${a}Title`) ``) phải tự kiểm.

## Chưa làm / hạn chế hiện tại
- Chưa có refresh token (access token 8 giờ, cấu hình `Jwt:AccessTokenMinutes`).
- Chưa có giao diện danh mục Bộ phận/Chức danh. Danh sách tài khoản không phân trang (lọc ở trình duyệt; số tài khoản nhà máy ít).
- Token lưu ở localStorage; ảnh xem được không cần đăng nhập (tên file không đoán được).
- Kho: chưa có giá trị tồn (đơn giá, thành tiền), kiểm kê, in phiếu theo mẫu 01-VT/02-VT, xuất báo cáo ra Excel.
- Mua hàng: chưa có hóa đơn/thanh toán/công nợ nhà cung cấp, báo giá so sánh nhiều NCC, thông báo email. Dòng đơn chỉ lấy từ đề nghị (không có dòng tự do); phiếu nhập từ đơn phải vào đúng kho nhận của đơn. Hộp chọn dòng đề nghị hiện tối đa 100 dòng (lọc bằng ô tìm). Dung sai nhận dư chưa có trong file import vật tư. Đơn nháp/chờ duyệt vẫn giữ số lượng của đề nghị.
- Ô chọn ngày dùng `<input type="date">`: định dạng hiển thị theo ngôn ngữ trình duyệt/Windows (máy tiếng Anh-Mỹ hiện `mm/dd/yyyy`).
- Trang in tem nhận danh sách qua router state: tải lại trang thì danh sách trống.
