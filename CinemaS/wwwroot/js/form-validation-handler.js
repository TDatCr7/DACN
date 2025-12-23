/**
 * ===================================
 * FORM VALIDATION HANDLER - GLOBAL
 * Xử lý validation errors từ ASP.NET Core
 * Hiển thị toast thay vì inline errors
 * ===================================
 */

(function () {
    'use strict';

    /**
     * Khởi tạo validation handler cho tất cả forms
     */
    function initializeValidationHandler() {
        // Tìm validation summary
        const validationSummary = document.querySelector('[asp-validation-summary="ModelOnly"]');
        
        if (!validationSummary) return;

        // Ẩn validation summary
        validationSummary.style.display = 'none';

        // Lấy tất cả error messages từ validation summary
        const summaryText = validationSummary.textContent.trim();
        
        // Collect tất cả field-level validation errors
        const errorSpans = document.querySelectorAll('span[class*="text-danger"]');
        const fieldErrors = [];
        let hasFieldErrors = false;

        errorSpans.forEach(span => {
            const text = span.textContent.trim();
            if (text && text.length > 0) {
                fieldErrors.push(text);
                hasFieldErrors = true;
            }
        });

        // Nếu có lỗi, hiển thị toast
        if (summaryText || hasFieldErrors) {
            // Tạo error message
            let errorMessage = '';
            
            if (fieldErrors.length > 0) {
                if (fieldErrors.length === 1) {
                    errorMessage = fieldErrors[0];
                } else {
                    errorMessage = `Có ${fieldErrors.length} lỗi cần kiểm tra:\n${fieldErrors.map(e => '• ' + e).join('\n')}`;
                }
            } else if (summaryText) {
                errorMessage = summaryText;
            }

            // Hiển thị warning toast
            if (errorMessage && typeof showWarningNotification !== 'undefined') {
                setTimeout(() => {
                    showWarningNotification(errorMessage, 8000);
                }, 100);
            }
        }

        // Ẩn tất cả individual error messages
        errorSpans.forEach(span => {
            span.style.display = 'none';
        });
    }

    /**
     * Khởi tạo form submission handler
     */
    function initializeFormSubmissionHandler() {
        const forms = document.querySelectorAll('form[asp-action="Create"], form[asp-action="Edit"]');
        
        forms.forEach(form => {
            form.addEventListener('submit', function (e) {
                // Kiểm tra validation
                const isValid = form.checkValidity();
                
                if (!isValid) {
                    e.preventDefault();
                    
                    // Collect validation errors
                    const inputs = form.querySelectorAll('input[required], select[required], textarea[required]');
                    const errors = [];
                    
                    inputs.forEach(input => {
                        if (!input.value || input.value.trim() === '') {
                            const label = form.querySelector(`label[for="${input.id}"]`);
                            const fieldName = label ? label.textContent.trim() : input.name;
                            errors.push(`${fieldName} không được để trống`);
                        }
                    });
                    
                    // Hiển thị error toast
                    if (errors.length > 0 && typeof showErrorNotification !== 'undefined') {
                        const message = errors.length === 1 
                            ? errors[0] 
                            : `Vui lòng điền đầy đủ thông tin (${errors.length} lỗi)`;
                        showErrorNotification(message, 8000);
                    }
                }
            });
        });
    }

    /**
     * Ẩn tất cả browser validation messages
     */
    function hideNativeValidationUI() {
        const inputs = document.querySelectorAll('input, select, textarea');
        
        inputs.forEach(input => {
            // Prevent browser validation UI
            input.addEventListener('invalid', function (e) {
                e.preventDefault();
            });
        });
    }

    // Initialize on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            hideNativeValidationUI();
            initializeValidationHandler();
            initializeFormSubmissionHandler();
        });
    } else {
        hideNativeValidationUI();
        initializeValidationHandler();
        initializeFormSubmissionHandler();
    }
})();
