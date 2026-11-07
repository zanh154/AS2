# Styles — quy ước chung cho cả nhóm

Toàn bộ giao diện AIVES dùng **một** bộ màu duy nhất. Mục tiêu: mỗi người làm
một module (lịch thi, ngân hàng câu hỏi, chấm điểm, ...) nhưng giao diện nhìn
vẫn như một sản phẩm.

## Cách hoạt động

```
Styles/site.scss  ──(build)──▶  wwwroot/css/site.css  ──▶  _Layout.cshtml
```

`AspNetCore.SassCompiler` biên dịch SCSS **mỗi lần build** (cấu hình trong
`sasscompiler.json`). Không cần cài thêm gì — cứ `dotnet build` hoặc bấm F5
trong Visual Studio. File `wwwroot/css/site.css` là file sinh ra, **không sửa
tay và không commit**.

## Cấu trúc

| File | Nội dung | Khi nào sửa |
|---|---|---|
| `_tokens.scss` | Màu, font, spacing, radius, shadow | Khi cả nhóm thống nhất đổi màu |
| `_mixins.scss` | Hàm dùng chung (`card-surface`, `respond-below`, ...) | Khi thêm pattern dùng lại |
| `_base.scss` | Reset, thẻ HTML gốc, biến CSS `--color-*` | Hiếm khi |
| `_layout.scss` | Header, footer, khung trang, trang đăng nhập | Hiếm khi |
| `_components.scss` | Nút, card, form, alert, badge, dashboard tile | Khi thêm component dùng chung |
| `_exam-schedule.scss` | Riêng của module Lịch thi | — |

## Luật quan trọng

**1. Không hard-code màu.** Không viết `color: #2563eb` trong `.cshtml` hay
trong file SCSS mới. Dùng token:

```scss
@use "tokens" as *;

.my-panel {
  color: $color-heading;
  background: $color-surface;
  border: 1px solid $color-border;
}
```

Nếu buộc phải viết CSS thuần (ví dụ style inline), dùng biến CSS được sinh ra
từ chính các token đó:

```css
.my-panel { background: var(--color-surface); color: var(--color-heading); }
```

**2. Dùng lại component có sẵn trước khi tự viết.** Các class dùng chung:

- `content-card` — khung trắng bo góc cho mọi khối nội dung
- `page-header` + `eyebrow` + `back-link` — phần đầu trang
- `metric-row` / `metric-card` — dãy số liệu tổng quan
- `status-chip`, `status-chip--success|warning|danger|muted` — nhãn trạng thái
- `role-badge--admin|lecturer|student` — nhãn vai trò
- `avatar`, `avatar--lg` — ảnh đại diện chữ cái
- `empty-state` + `empty-icon` — màn hình chưa có dữ liệu
- `dashboard-grid` / `dashboard-tile` — ô điều hướng ở trang chủ mỗi vai trò
- `form-actions`, `info-note`, `field-hint`, `validation-summary` — form
- `btn btn-primary`, `btn-outline-primary`, `btn-outline-secondary`,
  `btn-outline-danger` — nút (Bootstrap đã được nhuộm lại theo token)

**3. Thêm module mới thì thêm 1 file + 1 dòng.**

```scss
// Styles/_grading.scss
@use "tokens" as *;
@use "mixins" as *;

.grading-board { @include card-surface; }
```

```scss
// Styles/site.scss
@use "grading";
```

**4. Responsive dùng mixin, không viết `@media` thủ công.**

```scss
@include respond-below($breakpoint-md) {
  .grading-board { grid-template-columns: 1fr; }
}
```

## Bảng màu

| Token | Giá trị | Dùng cho |
|---|---|---|
| `$color-primary` | `#2563eb` | Nút chính, link, nav đang chọn |
| `$color-primary-dark` | `#1d4ed8` | Trạng thái hover |
| `$color-primary-darker` | `#0f172a` | Header, nền tối |
| `$color-primary-soft` | `#eff6ff` | Nền nhấn nhẹ |
| `$color-accent` | `#0d9488` | Nhấn phụ (eyebrow, chip) |
| `$color-success` / `$color-danger` / `$color-warning` / `$color-info` | — | Trạng thái |
| `$color-heading` / `$color-text` / `$color-muted` | — | Chữ |
| `$color-surface` / `$color-surface-alt` / `$color-background` | — | Nền |
| `$color-border` / `$color-border-strong` | — | Viền |

Đổi màu toàn hệ thống = sửa `_tokens.scss`, build lại. Không cần đụng file nào khác.
