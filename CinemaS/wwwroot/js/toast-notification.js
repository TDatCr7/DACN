/**
 * ===================================
 * TOAST NOTIFICATION - JAVASCRIPT
 * Hệ thống thông báo toast đẹp mắt
 * ===================================
 */

/**
 * Hiển thị toast notification
 * @param {string} message - Nội dung thông báo
 * @param {string} type - Loại: 'success', 'error', 'warning', 'info'
 * @param {number} duration - Thời gian hiển thị (ms), mặc định 3000
 */
window.showToast = function(message, type = 'info', duration = 3000) {
    // Tạo toast element
  const toast = document.createElement('div');
    toast.className = `toast-notification toast-${type}`;
    
    // Icon theo loại
    let icon = '✓';
    if (type === 'error') icon = '✗';
    else if (type === 'warning') icon = '⚠';
    else if (type === 'info') icon = 'ℹ';
    
    toast.innerHTML = `
        <span class="toast-icon">${icon}</span>
        <span class="toast-message">${message}</span>
    `;
    
    // Thêm vào body
    document.body.appendChild(toast);
    
    // Hiển thị với animation
  setTimeout(() => {
        toast.classList.add('show');
    }, 10);
    
    // Tự động ẩn sau duration
    setTimeout(() => {
        hideToast(toast);
    }, duration);
    
    // Click để đóng
    toast.addEventListener('click', () => {
        hideToast(toast);
    });
}

/**
 * Ẩn toast
 */
function hideToast(toast) {
    toast.classList.remove('show');
    setTimeout(() => {
        if (document.body.contains(toast)) {
 document.body.removeChild(toast);
        }
    }, 400);
}

/**
 * Toast shortcuts
 */
window.toastSuccess = function(message, duration = 3000) {
    showToast(message, 'success', duration);
}

window.toastError = function(message, duration = 3000) {
    showToast(message, 'error', duration);
}

window.toastWarning = function(message, duration = 3000) {
    showToast(message, 'warning', duration);
}

window.toastInfo = function(message, duration = 3000) {
    showToast(message, 'info', duration);
}
