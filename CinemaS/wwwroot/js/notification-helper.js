/**
 * ===================================
 * NOTIFICATION HELPER - JAVASCRIPT
 * Unified notification system using existing styles
 * ===================================
 */

// Ensure toast functions are available globally
if (typeof window.showToast === 'undefined') {
    console.warn('toast-notification.js not loaded. Loading fallback toast functions.');
    
    window.showToast = function(message, type = 'info', duration = 3000) {
        const toast = document.createElement('div');
        toast.className = `toast-notification toast-${type}`;
        
        let icon = '✓';
        if (type === 'error') icon = '✗';
        else if (type === 'warning') icon = '⚠';
        else if (type === 'info') icon = 'ℹ';
        
        toast.innerHTML = `
            <span class="toast-icon">${icon}</span>
            <span class="toast-message">${message}</span>
        `;
        
        document.body.appendChild(toast);
        
        setTimeout(() => {
            toast.classList.add('show');
        }, 10);
        
        setTimeout(() => {
            toast.classList.remove('show');
            setTimeout(() => {
                if (document.body.contains(toast)) {
                    document.body.removeChild(toast);
                }
            }, 400);
        }, duration);
    };
    
    window.toastSuccess = function(message, duration = 3000) {
        showToast(message, 'success', duration);
    };
    
    window.toastError = function(message, duration = 3000) {
        showToast(message, 'error', duration);
    };
    
    window.toastWarning = function(message, duration = 3000) {
        showToast(message, 'warning', duration);
    };
    
    window.toastInfo = function(message, duration = 3000) {
        showToast(message, 'info', duration);
    };
}

/**
 * Confirmation Modal
 * Reuses booking modal styles
 */
window.showConfirm = function(options) {
    return new Promise((resolve) => {
        const {
            title = 'Xác nhận',
            message = 'Bạn có chắc chắn muốn thực hiện thao tác này?',
            confirmText = 'Xác nhận',
            cancelText = 'Hủy',
            type = 'warning' // 'warning', 'danger', 'info'
        } = options;
        
        // Check if modal already exists
        let overlay = document.getElementById('confirmModalOverlay');
        if (overlay) {
            overlay.remove();
        }
        
        // Create modal overlay
        overlay = document.createElement('div');
        overlay.id = 'confirmModalOverlay';
        overlay.className = 'booking-modal-overlay';
        overlay.style.display = 'flex';
        overlay.style.zIndex = '99999';
        
        let iconHtml = '';
        let iconClass = '';
        
        if (type === 'warning') {
            iconHtml = '<i class="fas fa-exclamation-triangle"></i>';
            iconClass = 'warning';
        } else if (type === 'danger') {
            iconHtml = '<i class="fas fa-times-circle"></i>';
            iconClass = 'danger';
        } else {
            iconHtml = '<i class="fas fa-info-circle"></i>';
            iconClass = 'info';
        }
        
        overlay.innerHTML = `
            <div class="booking-modal" style="max-width: 500px;">
                <div class="booking-modal-header">
                    <div class="confirm-icon ${iconClass}">${iconHtml}</div>
                    <h3>${title}</h3>
                </div>
                <div class="booking-modal-body">
                    <p style="color: #cbd5e1; font-size: 15px; line-height: 1.6; margin: 0;">
                        ${message}
                    </p>
                </div>
                <div class="booking-modal-footer" style="gap: 12px;">
                    <button class="booking-modal-btn booking-modal-btn-cancel" data-action="cancel">
                        <i class="fas fa-times"></i> ${cancelText}
                    </button>
                    <button class="booking-modal-btn booking-modal-btn-confirm" data-action="confirm">
                        <i class="fas fa-check"></i> ${confirmText}
                    </button>
                </div>
            </div>
        `;
        
        document.body.appendChild(overlay);
        
        // Add icon styles
        const style = document.createElement('style');
        style.textContent = `
            .confirm-icon {
                width: 60px;
                height: 60px;
                border-radius: 50%;
                display: flex;
                align-items: center;
                justify-content: center;
                margin: 0 auto 16px;
                font-size: 32px;
            }
            .confirm-icon.warning {
                background: linear-gradient(135deg, rgba(251, 191, 36, 0.2), rgba(245, 158, 11, 0.2));
                color: #fbbf24;
                border: 2px solid rgba(251, 191, 36, 0.4);
            }
            .confirm-icon.danger {
                background: linear-gradient(135deg, rgba(239, 68, 68, 0.2), rgba(220, 38, 38, 0.2));
                color: #ef4444;
                border: 2px solid rgba(239, 68, 68, 0.4);
            }
            .confirm-icon.info {
                background: linear-gradient(135deg, rgba(59, 130, 246, 0.2), rgba(37, 99, 235, 0.2));
                color: #3b82f6;
                border: 2px solid rgba(59, 130, 246, 0.4);
            }
        `;
        document.head.appendChild(style);
        
        // Show modal with animation
        setTimeout(() => {
            overlay.classList.add('show');
        }, 10);
        
        // Handle button clicks
        const handleClick = (e) => {
            const action = e.target.closest('button')?.dataset.action;
            if (action) {
                overlay.classList.remove('show');
                setTimeout(() => {
                    overlay.remove();
                    style.remove();
                }, 300);
                resolve(action === 'confirm');
            }
        };
        
        overlay.addEventListener('click', (e) => {
            if (e.target === overlay) {
                overlay.classList.remove('show');
                setTimeout(() => {
                    overlay.remove();
                    style.remove();
                }, 300);
                resolve(false);
            }
        });
        
        overlay.querySelectorAll('button').forEach(btn => {
            btn.addEventListener('click', handleClick);
        });
        
        // ESC key closes modal
        const escHandler = (e) => {
            if (e.key === 'Escape') {
                overlay.classList.remove('show');
                setTimeout(() => {
                    overlay.remove();
                    style.remove();
                }, 300);
                resolve(false);
                document.removeEventListener('keydown', escHandler);
            }
        };
        document.addEventListener('keydown', escHandler);
    });
};

/**
 * Alert Modal (simple notification)
 */
window.showAlert = function(options) {
    return new Promise((resolve) => {
        const {
            title = 'Thông báo',
            message = '',
            buttonText = 'Đóng',
            type = 'info'
        } = options;
        
        showConfirm({
            title,
            message,
            confirmText: buttonText,
            cancelText: '',
            type
        }).then(() => resolve());
    });
};

/**
 * Replace native alert
 */
window.alertModal = function(message) {
    showAlert({
        title: 'Thông báo',
        message: message,
        type: 'info'
    });
};

/**
 * Replace native confirm
 */
window.confirmModal = function(message) {
    return showConfirm({
        title: 'Xác nhận',
        message: message,
        type: 'warning'
    });
};

// Auto-hide TempData alerts
document.addEventListener('DOMContentLoaded', function() {
    setTimeout(() => {
        document.querySelectorAll('.alert').forEach(a => {
            if (a.style.display !== 'none') {
                a.style.transition = 'opacity 0.5s ease';
                a.style.opacity = '0';
                setTimeout(() => a.style.display = 'none', 500);
            }
        });
    }, 5000);
});
