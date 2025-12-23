/**
 * ===================================
 * NOTIFICATION SYSTEM - GLOBAL HANDLER
 * Manages all UI-based notifications
 * ===================================
 */

(function () {
    'use strict';

    // Check for TempData notifications and auto-display
    function initializeNotifications() {
        // Look for notification containers with data attributes
        const notificationContainers = document.querySelectorAll('[data-notification-type]');
        
        notificationContainers.forEach(container => {
            const type = container.getAttribute('data-notification-type');
            const message = container.textContent.trim();
            
            if (message) {
                // Show as toast with appropriate type
                showToastNotification(message, type);
                
                // Remove the container from DOM
                container.remove();
            }
        });

        // Handle model validation errors
        handleValidationErrors();
    }

    /**
     * Display toast notification
     * @param {string} message - Notification message
     * @param {string} type - 'success', 'error', 'warning', 'info'
     * @param {number} duration - Auto-hide duration in ms
     */
    window.showToastNotification = function (message, type = 'info', duration = 5000) {
        // Create container if doesn't exist
        let container = document.getElementById('notification-container');
        if (!container) {
            container = document.createElement('div');
            container.id = 'notification-container';
            container.style.cssText = 'position:fixed;top:92px;right:16px;z-index:9999;width:min(520px,calc(100vw - 32px));';
            document.body.appendChild(container);
        }

        // Create toast element
        const toast = document.createElement('div');
        toast.className = `notification-toast notification-${type}`;
        
        const iconMap = {
            success: 'fa-check-circle',
            error: 'fa-exclamation-circle',
            warning: 'fa-exclamation-triangle',
            info: 'fa-info-circle'
        };

        const icon = iconMap[type] || 'fa-info-circle';
        const titleMap = {
            success: 'Thành công',
            error: 'Lỗi',
            warning: 'Cảnh báo',
            info: 'Thông tin'
        };
        const title = titleMap[type] || 'Thông báo';

        toast.innerHTML = `
            <div class="notification-content">
                <div class="notification-icon">
                    <i class="fa-solid ${icon}"></i>
                </div>
                <div class="notification-text">
                    <div class="notification-title">${title}</div>
                    <div class="notification-message">${message}</div>
                </div>
                <button class="notification-close" aria-label="Đóng">
                    <i class="fa-solid fa-xmark"></i>
                </button>
            </div>
            <div class="notification-progress"></div>
        `;

        // Add to container
        container.appendChild(toast);

        // Trigger animation
        setTimeout(() => {
            toast.classList.add('notification-show');
        }, 10);

        // Close button handler
        const closeBtn = toast.querySelector('.notification-close');
        const closeNotification = () => {
            toast.classList.remove('notification-show');
            setTimeout(() => toast.remove(), 400);
            if (hideTimer) clearTimeout(hideTimer);
        };

        closeBtn.addEventListener('click', closeNotification);

        // Auto-hide
        let hideTimer = setTimeout(() => {
            closeNotification();
        }, duration);

        // Cancel hide on hover
        toast.addEventListener('mouseenter', () => {
            if (hideTimer) clearTimeout(hideTimer);
        });

        toast.addEventListener('mouseleave', () => {
            hideTimer = setTimeout(() => {
                closeNotification();
            }, 1000);
        });

        return toast;
    };

    /**
     * Alias functions for convenience
     */
    window.showSuccessNotification = function (message, duration = 5000) {
        return showToastNotification(message, 'success', duration);
    };

    window.showErrorNotification = function (message, duration = 5000) {
        return showToastNotification(message, 'error', duration);
    };

    window.showWarningNotification = function (message, duration = 5000) {
        return showToastNotification(message, 'warning', duration);
    };

    window.showInfoNotification = function (message, duration = 5000) {
        return showToastNotification(message, 'info', duration);
    };

    /**
     * Show confirmation dialog (modal-based)
     * @param {object} options - { title, message, confirmText, cancelText, onConfirm, onCancel, type }
     */
    window.showConfirmDialog = function (options = {}) {
        return new Promise((resolve) => {
            const {
                title = 'Xác nhận',
                message = 'Bạn có chắc chắn?',
                confirmText = 'Xác nhận',
                cancelText = 'Hủy',
                type = 'info',
                onConfirm = null,
                onCancel = null
            } = options;

            // Create backdrop
            const backdrop = document.createElement('div');
            backdrop.className = 'confirm-backdrop';
            backdrop.style.cssText = `
                position: fixed;
                inset: 0;
                background: rgba(0,0,0,0.6);
                display: flex;
                align-items: center;
                justify-content: center;
                z-index: 10000;
                animation: fadeIn 0.3s ease;
            `;

            // Create dialog
            const dialog = document.createElement('div');
            dialog.className = `confirm-dialog confirm-${type}`;
            dialog.style.cssText = `
                background: rgba(2,6,23,0.98);
                border-radius: 20px;
                border: 1px solid rgba(148,163,255,0.45);
                box-shadow: 0 22px 60px rgba(15,23,42,0.95);
                padding: 28px 32px;
                max-width: 520px;
                width: calc(100% - 40px);
                color: #e9ecf6;
                animation: slideUp 0.3s ease;
            `;

            const iconMap = { success: 'fa-check', error: 'fa-exclamation', warning: 'fa-triangle-exclamation', info: 'fa-info' };
            const icon = iconMap[type] || 'fa-info';

            dialog.innerHTML = `
                <div style="display: flex; align-items: center; margin-bottom: 18px;">
                    <i class="fa-solid ${icon}" style="font-size: 24px; margin-right: 12px; color: ${
                        type === 'success' ? '#22c55e' : type === 'error' ? '#ef4444' : type === 'warning' ? '#f59e0b' : '#3b82f6'
                    };"></i>
                    <h2 style="margin: 0; font-weight: 900; font-size: 20px;">${title}</h2>
                </div>
                <p style="margin: 0 0 22px 0; color: #d1d5db; font-size: 14px;">${message}</p>
                <div style="display: flex; gap: 12px; justify-content: flex-end;">
                    <button class="confirm-cancel-btn" style="
                        padding: 10px 20px;
                        border-radius: 999px;
                        border: 1px solid rgba(148,163,255,0.45);
                        background: transparent;
                        color: #e5e7eb;
                        font-weight: 700;
                        cursor: pointer;
                        transition: all 0.2s ease;
                    ">${cancelText}</button>
                    <button class="confirm-ok-btn" style="
                        padding: 10px 20px;
                        border-radius: 999px;
                        border: none;
                        background: linear-gradient(120deg, #3b5ccc, #7c3aed);
                        color: #fff;
                        font-weight: 700;
                        cursor: pointer;
                        transition: all 0.2s ease;
                    ">${confirmText}</button>
                </div>
            `;

            backdrop.appendChild(dialog);
            document.body.appendChild(backdrop);

            // Add hover effects
            const cancelBtn = dialog.querySelector('.confirm-cancel-btn');
            const okBtn = dialog.querySelector('.confirm-ok-btn');

            cancelBtn.addEventListener('mouseenter', function () {
                this.style.background = 'rgba(148,163,255,0.15)';
            });
            cancelBtn.addEventListener('mouseleave', function () {
                this.style.background = 'transparent';
            });

            okBtn.addEventListener('mouseenter', function () {
                this.style.filter = 'brightness(1.08)';
            });
            okBtn.addEventListener('mouseleave', function () {
                this.style.filter = 'brightness(1)';
            });

            // Close handler
            const closeDialog = (confirmed) => {
                backdrop.style.animation = 'fadeOut 0.3s ease';
                dialog.style.animation = 'slideDown 0.3s ease';
                setTimeout(() => {
                    backdrop.remove();
                    if (confirmed) {
                        if (onConfirm) onConfirm();
                        resolve(true);
                    } else {
                        if (onCancel) onCancel();
                        resolve(false);
                    }
                }, 300);
            };

            okBtn.addEventListener('click', () => closeDialog(true));
            cancelBtn.addEventListener('click', () => closeDialog(false));
            backdrop.addEventListener('click', (e) => {
                if (e.target === backdrop) closeDialog(false);
            });
        });
    };

    /**
     * Handle validation errors from ModelState
     */
    function handleValidationErrors() {
        const validationSummary = document.querySelector('[asp-validation-summary="ModelOnly"]');
        
        if (validationSummary && validationSummary.textContent.trim()) {
            // Get all errors from validation summary
            const errorList = validationSummary.textContent.trim().split('\n').filter(e => e.trim());
            
            if (errorList.length > 0) {
                const message = errorList.length === 1 
                    ? errorList[0].trim() 
                    : `Có ${errorList.length} lỗi cần kiểm tra`;
                
                showErrorNotification(message, 6000);
            }

            // Hide the validation summary
            validationSummary.style.display = 'none';
        }

        // Check for field-level validation errors
        const validationSpans = document.querySelectorAll('.text-danger');
        let errorCount = 0;
        validationSpans.forEach(span => {
            if (span.textContent.trim()) {
                errorCount++;
            }
        });

        if (errorCount > 0 && !validationSummary) {
            showWarningNotification(`Vui lòng kiểm tra ${errorCount} trường thông tin`, 6000);
        }
    }

    /**
     * Intercept form submissions to show loading state
     */
    function initFormHandlers() {
        document.addEventListener('submit', function (e) {
            // Only apply to forms that don't have custom handlers
            const form = e.target;
            if (form.hasAttribute('data-no-notification')) return;

            // Add loading state
            const submitBtn = form.querySelector('button[type="submit"]');
            if (submitBtn && !submitBtn.hasAttribute('data-loading')) {
                const originalText = submitBtn.innerHTML;
                submitBtn.setAttribute('data-loading', 'true');
                submitBtn.disabled = true;
                submitBtn.innerHTML = '<i class="fa-solid fa-spinner fa-spin" style="margin-right: 6px;"></i>Đang xử lý...';

                // Restore after response
                setTimeout(() => {
                    submitBtn.removeAttribute('data-loading');
                    submitBtn.disabled = false;
                    submitBtn.innerHTML = originalText;
                }, 3000);
            }
        }, true);
    }

    // Add animations
    function addAnimations() {
        const style = document.createElement('style');
        style.textContent = `
            @keyframes fadeIn {
                from { opacity: 0; }
                to { opacity: 1; }
            }
            @keyframes fadeOut {
                from { opacity: 1; }
                to { opacity: 0; }
            }
            @keyframes slideUp {
                from { 
                    opacity: 0;
                    transform: translateY(20px);
                }
                to {
                    opacity: 1;
                    transform: translateY(0);
                }
            }
            @keyframes slideDown {
                from {
                    opacity: 1;
                    transform: translateY(0);
                }
                to {
                    opacity: 0;
                    transform: translateY(20px);
                }
            }
        `;
        document.head.appendChild(style);
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            addAnimations();
            initializeNotifications();
            initFormHandlers();
        });
    } else {
        addAnimations();
        initializeNotifications();
        initFormHandlers();
    }
})();
