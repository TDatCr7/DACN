/**
 * Password Toggle Component
 * Tự động thêm nút hiển thị/ẩn mật khẩu cho tất cả input type="password"
 */
(function () {
    'use strict';

    // CSS styles cho password toggle
    const styles = `
        .password-field-wrapper {
            position: relative;
            display: block;
        }

        .password-toggle-btn {
            position: absolute;
            right: 12px;
            top: 50%;
            transform: translateY(-50%);
            background: transparent;
            border: none;
            color: #9ca3c7;
            cursor: pointer;
            padding: 4px 8px;
            font-size: 16px;
            transition: color 0.2s ease;
            z-index: 1;
            outline: none;
        }

        .password-toggle-btn:hover {
            color: #e5e7f5;
        }

        .password-toggle-btn:focus {
            color: #3b5ccc;
        }

        .password-field-wrapper input[type="password"],
        .password-field-wrapper input[type="text"] {
            padding-right: 45px !important;
        }
    `;

    // Thêm styles vào document
    function injectStyles() {
        const styleElement = document.createElement('style');
        styleElement.textContent = styles;
        document.head.appendChild(styleElement);
    }

    // Tạo nút toggle cho một password input
    function createToggleButton() {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'password-toggle-btn';
        button.setAttribute('aria-label', 'Hiện/ẩn mật khẩu');
        button.innerHTML = '<i class="fa-regular fa-eye"></i>';
        return button;
    }

    // Wrap password input với wrapper và thêm toggle button
    function setupPasswordToggle(input) {
        // Kiểm tra nếu đã được setup rồi thì bỏ qua
        if (input.parentElement?.classList.contains('password-field-wrapper')) {
            return;
        }

        // Tạo wrapper
        const wrapper = document.createElement('div');
        wrapper.className = 'password-field-wrapper';

        // Insert wrapper trước input
        input.parentNode.insertBefore(wrapper, input);
        
        // Move input vào wrapper
        wrapper.appendChild(input);

        // Tạo và thêm toggle button
        const toggleBtn = createToggleButton();
        wrapper.appendChild(toggleBtn);

        // Xử lý click toggle
        toggleBtn.addEventListener('click', function () {
            const isPassword = input.type === 'password';
            input.type = isPassword ? 'text' : 'password';
            
            // Cập nhật icon
            const icon = toggleBtn.querySelector('i');
            if (isPassword) {
                icon.className = 'fa-regular fa-eye-slash';
                toggleBtn.setAttribute('aria-label', 'Ẩn mật khẩu');
            } else {
                icon.className = 'fa-regular fa-eye';
                toggleBtn.setAttribute('aria-label', 'Hiện mật khẩu');
            }
        });
    }

    // Tìm và setup tất cả password inputs
    function initializePasswordToggles() {
        const passwordInputs = document.querySelectorAll('input[type="password"]');
        passwordInputs.forEach(setupPasswordToggle);
    }

    // Observer để detect password inputs được thêm động
    function setupMutationObserver() {
        const observer = new MutationObserver(function (mutations) {
            mutations.forEach(function (mutation) {
                mutation.addedNodes.forEach(function (node) {
                    if (node.nodeType === 1) { // Element node
                        if (node.tagName === 'INPUT' && node.type === 'password') {
                            setupPasswordToggle(node);
                        }
                        // Tìm trong children
                        const passwordInputs = node.querySelectorAll?.('input[type="password"]');
                        passwordInputs?.forEach(setupPasswordToggle);
                    }
                });
            });
        });

        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
    }

    // Initialize khi DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            injectStyles();
            initializePasswordToggles();
            setupMutationObserver();
        });
    } else {
        injectStyles();
        initializePasswordToggles();
        setupMutationObserver();
    }
})();
