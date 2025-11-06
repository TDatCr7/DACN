/**
 * ===================================
 * SEAT SELECTION - JAVASCRIPT
 * Quản lý logic chọn ghế CGV-style
 * ===================================
 */

// Global variables
let selectedSeats = [];
let totalPrice = 0;

/**
 * Toggle chọn/bỏ chọn ghế
 * ⚠️ Hàm này sẽ được override trong SeatSelection.cshtml
 */
window.toggleSeat = function(element) {
    // Kiểm tra ghế đã đặt
    if (element.classList.contains('booked')) {
        showToast('⚠️ Ghế này đã được đặt!', 'error');
        return;
    }

    const seatIds = element.getAttribute('data-seat-id').split(',');
    const price = parseFloat(element.getAttribute('data-seat-price'));
    const label = element.getAttribute('data-seat-label');
    const isCouple = element.getAttribute('data-is-couple') === 'true';

    // Toggle trạng thái
    if (element.classList.contains('selected')) {
        // ✅ BỎ CHỌN
        element.classList.remove('selected');

        // Ẩn icon user
        if (isCouple) {
    const userIcons = element.querySelector('.user-icons');
            if (userIcons) userIcons.style.display = 'none';
        } else {
            const userIcon = element.querySelector('.user-icon-wrapper');
       if (userIcon) userIcon.style.display = 'none';
        }

        // ✅ XÓA KHỎI DANH SÁCH (Ghế đôi: xóa theo chuỗi ID đầy đủ)
        if (isCouple) {
        const combinedId = seatIds.join(',');
            selectedSeats = selectedSeats.filter(s => s.id !== combinedId);
        } else {
        selectedSeats = selectedSeats.filter(s => !seatIds.includes(s.id));
        }
    } else {
        // ✅ CHỌN GHẾ
   element.classList.add('selected');

      // Hiện icon user
    if (isCouple) {
       const userIcons = element.querySelector('.user-icons');
      if (userIcons) userIcons.style.display = 'flex';
        } else {
const userIcon = element.querySelector('.user-icon-wrapper');
     if (userIcon) userIcon.style.display = 'flex';
        }

        // ✅ THÊM VÀO DANH SÁCH (Ghế đôi: lưu chuỗi ID đầy đủ)
        selectedSeats.push({
  id: seatIds.join(','),
      label: label,
            price: price,
         isCouple: isCouple
        });
    }

    updateSummary();
}

/**
 * Cập nhật thông tin tổng kết
 */
function updateSummary() {
    // ✅ Tính tiền ghế
    const seatTotal = selectedSeats.reduce((sum, seat) => sum + seat.price, 0);
    
    // ✅ Tính tiền snacks (nhân với quantity)
 const snackTotal = typeof selectedSnacks !== 'undefined' 
    ? selectedSnacks.reduce((sum, snack) => sum + (snack.price * (snack.quantity || 1)), 0) 
  : 0;
    
  // ✅ Tổng tiền = ghế + snacks
    totalPrice = seatTotal + snackTotal;

    const countElement = document.getElementById('selectedSeats');
  const priceElement = document.getElementById('totalPrice');
    const btnBook = document.getElementById('btnBook');

    if (countElement) {
   const seatList = selectedSeats.map(s => s.label).join(', ');
  countElement.textContent = seatList || 'Chưa chọn';
  }

    if (priceElement) {
      priceElement.textContent = totalPrice.toLocaleString('vi-VN') + ' đ';
    }

    if (btnBook) {
        btnBook.disabled = selectedSeats.length === 0;
    }
}

/**
 * Đặt vé
 * ⚠️ Hàm này sẽ được override trong SeatSelection.cshtml
 */
async function bookSeats() {
    console.warn('bookSeats() should be overridden in view');
}

/**
 * Refresh trạng thái ghế từ server
 */
async function refreshSeatsStatus() {
    try {
        const showTimeId = document.getElementById('showTimeId')?.value;
        if (!showTimeId) return;

  const response = await fetch(`/Booking/GetSeatsStatus?showTimeId=${showTimeId}`);
        const result = await response.json();

 if (result.success && result.bookedSeats) {
        result.bookedSeats.forEach(seatId => {
            // Tìm ghế trong DOM
       const seatElement = document.querySelector(`[data-seat-id="${seatId}"]`) ||
             document.querySelector(`[data-seat-id*="${seatId}"]`);

    if (seatElement && !seatElement.classList.contains('booked')) {
          // Đánh dấu ghế đã đặt
           seatElement.classList.remove('selected');
   seatElement.classList.add('booked');
     seatElement.onclick = null;

     // Ẩn icon user
     const userIcons = seatElement.querySelectorAll('.user-icon-wrapper, .user-icons');
 userIcons.forEach(icon => icon.style.display = 'none');
        }
      });
        }
} catch (error) {
        console.error('Error refreshing seats:', error);
    }
}

/**
 * Hiển thị/ẩn loading spinner
 */
function showLoading(show) {
    let spinner = document.getElementById('loading-spinner');

    // Tạo spinner nếu chưa có
    if (!spinner) {
        spinner = document.createElement('div');
    spinner.id = 'loading-spinner';
        spinner.innerHTML = `
       <div style="position: fixed; top: 0; left: 0; width: 100%; height: 100%; background: rgba(0,0,0,0.7); display: none; justify-content: center; align-items: center; z-index: 9999;">
      <div style="text-align: center; color: white;">
 <div style="border: 4px solid #f3f3f3; border-top: 4px solid #fbbf24; border-radius: 50%; width: 50px; height: 50px; animation: spin 1s linear infinite; margin: 0 auto;"></div>
     <p style="margin-top: 15px;">Đang xử lý...</p>
           </div>
            </div>
        `;
        document.body.appendChild(spinner);

    // Thêm CSS animation
        const style = document.createElement('style');
        style.textContent = `
        @keyframes spin {
                0% { transform: rotate(0deg); }
                100% { transform: rotate(360deg); }
      }
     `;
        document.head.appendChild(style);
    }

    const spinnerDiv = spinner.querySelector('div');
    if (spinnerDiv) {
        spinnerDiv.style.display = show ? 'flex' : 'none';
    }
}

/**
 * Hiển thị toast notification
 */
function showToast(message, type = 'info') {
    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;
    toast.textContent = message;
    toast.style.cssText = `
 position: fixed;
        top: 20px;
      right: 20px;
    padding: 15px 25px;
        background: ${type === 'success' ? '#10b981' : type === 'error' ? '#ef4444' : '#3b82f6'};
   color: white;
    border-radius: 8px;
        box-shadow: 0 4px 12px rgba(0,0,0,0.3);
        z-index: 10000;
     animation: slideIn 0.3s ease-out;
        max-width: 400px;
        word-wrap: break-word;
    `;
    
  document.body.appendChild(toast);
    
    setTimeout(() => {
   toast.style.animation = 'slideOut 0.3s ease-out';
      setTimeout(() => toast.remove(), 300);
    }, 3000);
}

/**
 * Initialize khi DOM loaded
 */
document.addEventListener('DOMContentLoaded', function() {
    console.log('🎬 Seat Selection initialized');
    
    // Thêm CSS animation
    const style = document.createElement('style');
    style.textContent = `
        @keyframes slideIn {
            from { transform: translateX(100%); opacity: 0; }
            to { transform: translateX(0); opacity: 1; }
        }
      @keyframes slideOut {
       from { transform: translateX(0); opacity: 1; }
  to { transform: translateX(100%); opacity: 0; }
        }
    `;
    document.head.appendChild(style);
});
