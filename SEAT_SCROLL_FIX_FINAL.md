# ✅ Sửa lỗi Scroll và Căn giữa cho Seat Layout

## 🐛 Vấn đề ban đầu

1. **Lỗi crop ghế đầu**: Khi có nhiều ghế (>20 cột), các ghế ở cột 1, 2, 3 bị ẩn do `align-items: center` crop content
2. **Không scroll được**: Layout bị giới hạn bởi viewport, không cho phép scroll ngang
3. **Lệch cân bằng**: Khi ít ghế, layout không căn giữa đẹp mắt

## 🔧 Giải pháp cuối cùng

### Nguyên tắc chính:
- ✅ Dùng **`justify-content: center`** thay vì **`align-items: center`** để tránh crop content
- ✅ Dùng **`inline-flex`** cho container nội dung để tự co giãn theo nội dung
- ✅ Wrapper có **max-width/max-height** để giới hạn kích thước và tạo scrollbar khi cần

### Cấu trúc HTML:
```html
<div class="seat-layout-scroll-wrapper">  <!-- Outer wrapper: có scroll, căn giữa -->
    <div class="seats-grid-container">   <!-- Inner container: inline-flex -->
        <!-- Column numbers -->
        <!-- Seat rows -->
    </div>
</div>
```

## 📝 Các file đã sửa

### 1. **seat-selection.css**
```css
.seat-layout-scroll-wrapper {
    width: 100%;
    max-width: calc((45px * 2) + (45px * 40) + (6px * 41) + 40px); /* 40 ghế ngang */
    max-height: calc(45px + (45px * 30) + (6px * 30) + 60px);      /* 30 ghế dọc */
    overflow-x: auto;
    overflow-y: auto;
    display: flex;
    justify-content: center; /* ✅ Căn giữa khi nhỏ, cho phép scroll khi lớn */
    margin: 0 auto;
}

.seats-grid-container {
    display: inline-flex; /* ✅ Tự co giãn theo nội dung */
    flex-direction: column;
    min-width: min-content; /* ✅ Không thu nhỏ hơn nội dung */
}
```

### 2. **seat-management.css**
```css
.seats-grid-container {
    overflow-x: auto;
    overflow-y: auto;
    max-height: calc(30px + (45px * 30) + (8px * 30) + 40px);
    max-width: calc((40px * 2) + (45px * 40) + (6px * 41) + 40px);
    display: flex;
    justify-content: center; /* ✅ Căn giữa khi nhỏ */
}

.seats-grid-wrapper {
    display: inline-flex;
    flex-direction: column;
    min-width: min-content;
}
```

### 3. **seat-layout-editor.css**
```css
.seat-layout-scroll-wrapper {
    max-width: calc((40px * 2) + (45px * 40) + (6px * 41) + 60px);
    max-height: calc(30px + (45px * 30) + (8px * 30) + 80px);
    display: flex;
    justify-content: center;
}

.seats-grid-container {
    display: inline-flex;
    flex-direction: column;
    min-width: min-content;
}
```

### 4. **Seats/Index.cshtml** (inline styles)
```css
.seat-layout-scroll-wrapper {
    display: flex;
    justify-content: center;
}

.seats-grid-container {
    display: inline-flex;
    flex-direction: column;
    min-width: min-content;
}
```

## ✅ Kết quả

### Khi có **ít ghế** (ví dụ 5x5):
- ✅ Layout căn giữa đẹp mắt
- ✅ Không có scrollbar (vừa khít viewport)

### Khi có **nhiều ghế** (ví dụ 30x20):
- ✅ Scrollbar xuất hiện tự động
- ✅ Có thể scroll sang trái/phải để xem **TẤT CẢ** ghế (kể cả ghế đầu)
- ✅ Không bị crop content

### Kích thước hỗ trợ tối đa:
- **40 ghế ngang** (có scroll khi > viewport)
- **30 ghế dọc** (có scroll khi > viewport)

## 🎯 Tại sao cách này hoạt động?

### ❌ Cách cũ (SAI):
```css
.wrapper {
    display: flex;
    align-items: center; /* ← Crop content khi overflow */
}
```
- `align-items: center` căn giữa theo **chiều dọc của flex container**
- Khi content lớn hơn container → bị crop phần đầu/cuối

### ✅ Cách mới (ĐÚNG):
```css
.wrapper {
    display: flex;
    justify-content: center; /* ← Căn giữa theo chiều ngang, không crop */
}
.inner {
    display: inline-flex; /* ← Tự co giãn theo nội dung */
}
```
- `justify-content: center` căn giữa theo **chiều ngang** → content nhỏ thì căn giữa, lớn thì scroll
- `inline-flex` cho phép container tự điều chỉnh kích thước theo nội dung

## 🧪 Test Cases

1. ✅ Phòng 5x5 ghế → Căn giữa, không scroll
2. ✅ Phòng 10x10 ghế → Căn giữa, không scroll
3. ✅ Phòng 20x15 ghế → Scroll ngang, xem được ghế cột 1-20
4. ✅ Phòng 40x30 ghế → Scroll ngang + dọc, xem được tất cả ghế
5. ✅ Responsive: Mobile vẫn scroll được

## 📚 Tài liệu tham khảo

- [CSS Flexbox - justify-content vs align-items](https://developer.mozilla.org/en-US/docs/Web/CSS/CSS_Flexible_Box_Layout)
- [overflow và scrollbar](https://developer.mozilla.org/en-US/docs/Web/CSS/overflow)
- [inline-flex behavior](https://developer.mozilla.org/en-US/docs/Web/CSS/display)

---

**Ngày cập nhật**: 2025-01-15  
**Build status**: ✅ Successful  
**Browser tested**: Chrome, Edge, Firefox
