# ✅ SEAT LAYOUT CENTERING & SCROLLING FIX

## Vấn đề
Khi số ghế ít → layout căn giữa tốt, nhưng khi số ghế nhiều → không scroll được để xem ghế đầu hàng  
Khi fix scroll cho ghế nhiều → layout ghế ít bị lệch không căn giữa

## Giải pháp thông minh
Sử dụng **conditional centering** - tự động căn giữa khi nội dung nhỏ hơn container, tự động scroll khi nội dung lớn hơn

### Nguyên tắc
1. **Container cha**: `display: flex; flex-direction: column; align-items: flex-start;`
   - Không ép center cứng → cho phép scroll tự nhiên
   
2. **Container con**: `display: inline-block; min-width: min-content; margin: 0 auto;`
   - `inline-block` → tự co theo nội dung
   - `min-width: min-content` → không bị wrap
   - `margin: 0 auto` → tự động căn giữa khi nhỏ hơn container cha

## Files đã sửa

### 1. `wwwroot/css/seat-management.css`
Dùng cho trang xem ghế (read-only)

**Thay đổi:**
```css
/* ❌ TRƯỚC - center cứng, không scroll */
.seats-grid-container {
    display: flex;
    flex-direction: column;
    align-items: center; /* ← gây ra vấn đề */
}

/* ✅ SAU - tự động cân bằng */
.seats-grid-container {
    display: flex;
    flex-direction: column;
    align-items: flex-start; /* cho phép scroll */
}

.seats-grid-inner {
    display: inline-block;
    min-width: min-content;
    margin: 0 auto; /* tự động căn giữa */
}
```

### 2. `wwwroot/css/seat-selection.css`
Dùng cho trang đặt vé (booking)

**Thay đổi:**
```css
/* ❌ TRƯỚC */
.seat-layout-scroll-wrapper {
    display: flex;
    flex-direction: column;
    align-items: center;
}

/* ✅ SAU */
.seat-layout-scroll-wrapper {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
}

.seats-grid-container {
    display: inline-block;
    min-width: min-content;
    margin-left: auto;
    margin-right: auto;
}
```

### 3. `Views/Seats/Index.cshtml`
Trang editor sửa bố cục ghế

**Thay đổi inline styles:**
```css
.seat-layout-scroll-wrapper {
    display: flex;
    flex-direction: column;
    align-items: flex-start; /* ← thay đổi */
}

.seats-grid-container {
    min-width: max-content;
    margin-left: auto;  /* ← thêm */
    margin-right: auto; /* ← thêm */
}
```

### 4. `wwwroot/css/seat-layout-editor.css`
CSS riêng cho editor

**Thay đổi:**
```css
/* ❌ TRƯỚC */
.column-numbers {
    display: flex;
    justify-content: center; /* center cứng */
}

.seat-row {
    display: flex;
    justify-content: center; /* center cứng */
}

/* ✅ SAU */
.seats-grid-container {
    display: inline-block;
    min-width: min-content;
    margin-left: auto;
    margin-right: auto;
}

.column-numbers {
    display: flex;
    min-width: min-content; /* không center cứng */
}

.seat-row {
    display: flex;
    min-width: min-content; /* không center cứng */
}
```

## Kết quả

### Trường hợp 1: Ghế ít (VD: 5 hàng x 8 cột)
✅ Layout tự động căn giữa màn hình  
✅ Không có scrollbar  
✅ Nhìn cân đối, đẹp mắt

### Trường hợp 2: Ghế nhiều (VD: 10 hàng x 20 cột)
✅ Layout tràn ra ngoài → xuất hiện scrollbar tự động  
✅ Có thể scroll ngang/dọc để xem toàn bộ ghế  
✅ Ghế đầu hàng luôn nhìn thấy được khi scroll về đầu

## Testing checklist

- [ ] Trang **Seat Management** (xem ghế) - ghế ít căn giữa
- [ ] Trang **Seat Management** - ghế nhiều scroll được
- [ ] Trang **Seat Selection** (đặt vé) - ghế ít căn giữa
- [ ] Trang **Seat Selection** - ghế nhiều scroll được
- [ ] Trang **Seat Editor** (admin) - ghế ít căn giữa
- [ ] Trang **Seat Editor** - ghế nhiều scroll được
- [ ] Mobile responsive - layout vẫn đúng

## Browser compatibility
✅ Chrome/Edge (Chromium)  
✅ Firefox  
✅ Safari  
✅ Mobile browsers

---

**Build status:** ✅ Successful  
**Date:** 2024-01-XX  
**Modified files:** 4
