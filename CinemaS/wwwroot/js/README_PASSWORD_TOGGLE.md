# Chức Năng Toggle Password (Hiện/Ẩn Mật Khẩu)

## Tổng Quan
Đã thêm chức năng hiển thị/ẩn mật khẩu cho **toàn bộ website** thông qua một component JavaScript tự động.

## Cách Hoạt Động

### 1. File JavaScript (`password-toggle.js`)
- **Vị trí**: `CinemaS/wwwroot/js/password-toggle.js`
- **Chức năng**: Tự động tìm tất cả `input[type="password"]` và thêm nút con mắt để toggle

### 2. Tích Hợp vào Layout
Script đã được thêm vào:
- `Views/Shared/_Layout.cshtml` (cho MVC pages)
- `Views/Shared/_LayoutHeader.cshtml` (cho pages khác)

### 3. Các Trang Có Password Input

Chức năng sẽ tự động hoạt động trên tất cả các trang có input password:

#### Identity Pages (Areas/Identity/Pages/Account/)
✅ **Login.cshtml** - Mật khẩu đăng nhập
✅ **Register.cshtml** - Mật khẩu đăng ký + Xác nhận mật khẩu
✅ **ResetPassword.cshtml** - Mật khẩu mới + Xác nhận mật khẩu
✅ **ChangePassword.cshtml** (Manage) - Mật khẩu cũ + Mật khẩu mới + Xác nhận mật khẩu

#### Bất kỳ trang nào khác
- Component sẽ tự động detect và thêm nút toggle cho mọi password input mới

## Tính Năng

### 🔹 Tự Động Phát Hiện
- Sử dụng `MutationObserver` để detect password inputs được thêm động
- Không cần config thủ công cho từng trang

### 🔹 UI/UX
- Icon con mắt: `fa-eye` (ẩn) ↔ `fa-eye-slash` (hiện)
- Màu sắc phù hợp với dark theme:
  - Màu mặc định: `#9ca3c7` (muted)
  - Hover: `#e5e7f5` (text)
  - Focus: `#3b5ccc` (indigo)
- Nút nằm bên phải input, không che mất text

### 🔹 Accessibility
- Có `aria-label` cho screen readers
- Button type="button" để không trigger form submit
- Tab index tự nhiên

## Cấu Trúc HTML Được Tạo

```html
<div class="password-field-wrapper">
    <input type="password" ... /> <!-- Input gốc -->
    <button type="button" class="password-toggle-btn" aria-label="Hiện/ẩn mật khẩu">
        <i class="fa-regular fa-eye"></i>
    </button>
</div>
```

## CSS Styles

### Wrapper
- `position: relative` để chứa nút toggle
- `display: block` để giữ layout

### Toggle Button
- `position: absolute` ở góc phải
- `right: 12px`, `top: 50%` với `transform: translateY(-50%)`
- `z-index: 1` để nằm trên input

### Input Padding
- Tự động thêm `padding-right: 45px` để text không bị che bởi nút

## Cách Sử Dụng

### Không Cần Làm Gì!
Script tự động chạy khi:
1. DOM loaded (`DOMContentLoaded`)
2. Hoặc khi có password input mới được thêm vào (via MutationObserver)

### Nếu Muốn Tắt Cho Input Cụ Thể
Thêm attribute `data-no-toggle="true"` vào input:
```html
<input type="password" data-no-toggle="true" />
```

**Lưu ý**: Hiện tại chưa implement logic check attribute này, nhưng có thể dễ dàng thêm.

## Icon Font Awesome
Cần có Font Awesome 6.5.0+ để hiển thị icon:
- `fa-regular fa-eye`
- `fa-regular fa-eye-slash`

Đã có sẵn trong `_Layout.cshtml`:
```html
<link rel="stylesheet"
      href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.5.0/css/all.min.css"
      crossorigin="anonymous" />
```

## Testing Checklist

✅ Login page - Password field
✅ Register page - Password + Confirm Password
✅ Reset Password page - New Password + Confirm Password  
✅ Change Password page - Old Password + New Password + Confirm Password
✅ Bất kỳ form mới có password input

## Tương Thích

- ✅ Dark theme
- ✅ Mobile responsive
- ✅ Tất cả trình duyệt hiện đại (Chrome, Firefox, Safari, Edge)
- ✅ Không conflict với Bootstrap hay jQuery

## Bảo Trì

### Khi Thêm Trang Mới Có Password
Không cần làm gì! Script tự động hoạt động.

### Khi Thay Đổi Theme/Colors
Chỉnh sửa CSS trong `password-toggle.js`:
```javascript
const styles = `
    .password-toggle-btn {
        color: #9ca3c7; /* Thay đổi màu ở đây */
        ...
    }
`;
```

## Lợi Ích

1. **Trải Nghiệm Người Dùng Tốt**: Dễ check password trước khi submit
2. **Tự Động Hóa**: Không cần copy-paste code cho mỗi trang
3. **Dễ Bảo Trì**: Chỉ một file JavaScript duy nhất
4. **Nhất Quán**: Tất cả password inputs đều có cùng UX
5. **Accessibility**: Hỗ trợ screen readers

---

**Build Status**: ✅ Build successful
**Version**: 1.0.0
**Last Updated**: 2024
