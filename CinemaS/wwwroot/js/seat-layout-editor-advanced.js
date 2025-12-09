// ===================================
// SEAT LAYOUT EDITOR - MODE/ACTION SYSTEM
// ===================================
// selectionMode: seat | row | column
// currentAction: normal | vip | couple | broken | delete | restore
// ===================================

let selectionMode = 'seat'; // seat | row | column
let currentAction = 'normal'; // normal | vip | couple | broken | delete | restore
let currentTheaterId = null;
let isSaving = false;

document.addEventListener('DOMContentLoaded', function () {
    const theaterSelect = document.getElementById('theaterSelect');
    if (theaterSelect) {
        currentTheaterId = theaterSelect.value;
        console.log('🎬 Initialized theater ID:', currentTheaterId);

        if (!currentTheaterId) {
            console.warn('⚠️ No theater selected!');
        }
    } else {
        console.error('❌ Theater select element not found!');
    }

    document.querySelectorAll('.seat').forEach(seat => {
        seat.addEventListener('contextmenu', function (e) {
            e.preventDefault();
        });
    });

    initializeModeButtons();
    initializeActionButtons();
    initializeDeleteButtons();
    refreshLastMarkers();

    console.log('📊 Initial state:', {
        theaterId: currentTheaterId,
        selectionMode: selectionMode,
        currentAction: currentAction,
        totalSeats: document.querySelectorAll('.seat').length
    });
});

// ===================================
// INITIALIZE MODE BUTTONS (Chế độ chỉnh sửa)
// ===================================
function initializeModeButtons() {
    const modeButtons = document.querySelectorAll('[data-mode]');
    modeButtons.forEach(btn => {
        btn.addEventListener('click', function () {
            const mode = this.getAttribute('data-mode');
            setSelectionMode(mode);

            // Update button active states
            modeButtons.forEach(b => b.classList.remove('active'));
            this.classList.add('active');
        });
    });
}

function setSelectionMode(mode) {
    // Prevent changing mode when couple action is active
    if (currentAction === 'couple' && mode !== 'seat') {
        showToast('⚠️ Khi chọn "Ghế đôi", chỉ có thể chọn theo ghế!', 'warning');
        return;
    }

    selectionMode = mode;
    const modeText = {
        'seat': '🪑 Chọn theo ghế',
        'row': '📋 Chọn theo hàng',
        'column': '📊 Chọn theo cột'
    };
    showToast(modeText[mode] || 'Đã chọn chế độ', 'info');
    console.log('🔄 Selection mode changed to:', mode);
}

// ===================================
// INITIALIZE ACTION BUTTONS (Hành động)
// ===================================
function initializeActionButtons() {
    const actionButtons = document.querySelectorAll('[data-action]');
    actionButtons.forEach(btn => {
        btn.addEventListener('click', function () {
            const action = this.getAttribute('data-action');
            setCurrentAction(action);

            // Update button active states
            actionButtons.forEach(b => b.classList.remove('active'));
            this.classList.add('active');
        });
    });
}

function setCurrentAction(action) {
    currentAction = action;
    const actionText = {
        'normal': '🪑 Ghế thường',
        'vip': '⭐ Ghế VIP',
        'couple': '👥 Ghế đôi',
        'broken': '⚠️ Ghế hỏng',
        'delete': '🗑️ Bỏ ghế',
        'restore': '♻️ Khôi phục'
    };
    showToast(actionText[action] || 'Đã chọn hành động', 'info');
    console.log('🔄 Current action changed to:', action);

    // Special handling for couple action: force seat mode and disable other modes
    if (action === 'couple') {
        selectionMode = 'seat';
        // Update mode buttons: activate seat, disable row and column
        document.querySelectorAll('[data-mode]').forEach(btn => {
            const mode = btn.getAttribute('data-mode');
            if (mode === 'seat') {
                btn.classList.add('active');
                btn.disabled = false;
            } else {
                btn.classList.remove('active');
                btn.disabled = true;
            }
        });
    } else {
        // Re-enable all mode buttons if not couple
        document.querySelectorAll('[data-mode]').forEach(btn => {
            btn.disabled = false;
            if (btn.getAttribute('data-mode') === selectionMode) {
                btn.classList.add('active');
            }
        });
    }
}

// ===================================
// INITIALIZE DELETE BUTTONS (Row/Column delete)
// ===================================
function initializeDeleteButtons() {
    document.querySelectorAll('.row-label .delete-btn').forEach(btn => {
        btn.removeEventListener('click', onRowDeleteClick);
        btn.addEventListener('click', onRowDeleteClick);
    });

    document.querySelectorAll('.column-number .delete-btn').forEach(btn => {
        btn.removeEventListener('click', onColDeleteClick);
        btn.addEventListener('click', onColDeleteClick);
    });

    refreshLastMarkers();
}

function onRowDeleteClick(e) {
    e.stopPropagation();
    const rowLabel = this.closest('.row-label');
    const rowLabelText = rowLabel.querySelector('.row-label-text').textContent;
    deleteRow(rowLabelText);
}

function onColDeleteClick(e) {
    e.stopPropagation();
    const colNumber = this.closest('.column-number');
    const colIndex = parseInt(colNumber.getAttribute('data-col-index'));
    deleteColumn(colIndex);
}

function refreshLastMarkers() {
    const columnNumbersRow = document.querySelector('.column-numbers');
    if (columnNumbersRow) {
        columnNumbersRow.querySelectorAll('.column-number.last-column').forEach(el => el.classList.remove('last-column'));
        columnNumbersRow.querySelectorAll('.column-number .delete-btn').forEach(btn => btn.style.display = 'none');

        const cols = Array.from(columnNumbersRow.querySelectorAll('.column-number'));
        if (cols.length > 0) {
            const last = cols[cols.length - 1];
            last.classList.add('last-column');
            const del = last.querySelector('.delete-btn');
            if (del) del.style.display = 'inline-block';
        }
    }

    const seatRows = Array.from(document.querySelectorAll('.seat-row'));
    seatRows.forEach(r => {
        r.classList.remove('last-row');
        r.querySelectorAll('.row-label .delete-btn').forEach(btn => btn.style.display = 'none');
    });
    if (seatRows.length > 0) {
        const lastRow = seatRows[seatRows.length - 1];
        lastRow.classList.add('last-row');
        lastRow.querySelectorAll('.row-label .delete-btn').forEach(btn => btn.style.display = 'inline-block');
    }
}

// ===================================
// SEAT CLICK HANDLER - MAIN ENTRY POINT
// ===================================
window.onSeatClick = async function (seatElement) {
    if (isSaving) {
        showToast('⏳ Đang lưu...', 'warning');
        return;
    }

    const seatId = seatElement.getAttribute('data-seat-id');
    const isDeleted = seatElement.getAttribute('data-is-deleted') === 'true';
    const rowNumber = parseInt(seatElement.getAttribute('data-row-number'));
    const columnIndex = parseInt(seatElement.getAttribute('data-column-index'));

    // Get row label from parent
    const seatRow = seatElement.closest('.seat-row');
    const rowLabel = seatRow?.getAttribute('data-row-label') || 
                     seatRow?.querySelector('.row-label-text')?.textContent;

    if (!seatId) {
        showToast('❌ Không thể xác định ghế', 'error');
        return;
    }

    console.log('🖱️ Seat clicked:', { seatId, selectionMode, currentAction, rowLabel, columnIndex, isDeleted });

    // Handle based on selection mode
    switch (selectionMode) {
        case 'seat':
            await handleSeatMode(seatElement, seatId, isDeleted);
            break;
        case 'row':
            await handleRowMode(rowLabel);
            break;
        case 'column':
            await handleColumnMode(columnIndex);
            break;
    }
};

// ===================================
// HANDLE SEAT MODE (Single seat)
// ===================================
async function handleSeatMode(seatElement, seatId, isDeleted) {
    // For deleted seats, only restore action works
    if (isDeleted && currentAction !== 'restore') {
        showToast('⚠️ Ghế đã bị bỏ. Chọn "Khôi phục" để khôi phục ghế.', 'warning');
        return;
    }

    switch (currentAction) {
        case 'normal':
        case 'vip':
            await changeSeatType(seatId, currentAction);
            break;
        case 'couple':
            await createCoupleSeat(seatElement);
            break;
        case 'broken':
            await toggleBrokenSeat(seatElement, seatId);
            break;
        case 'delete':
            await softDeleteSeat(seatId);
            break;
        case 'restore':
            await restoreSeat(seatId);
            break;
    }
}

// ===================================
// HANDLE ROW MODE (Entire row)
// ===================================
async function handleRowMode(rowLabel) {
    if (!rowLabel) {
        showToast('❌ Không xác định được hàng', 'error');
        return;
    }

    console.log('📋 Row mode - applying action:', currentAction, 'to row:', rowLabel);

    let seatTypeId = '';

    switch (currentAction) {
        case 'normal':
            seatTypeId = getSeatTypeIdByName('NORMAL');
            break;
        case 'vip':
            seatTypeId = getSeatTypeIdByName('VIP');
            break;
        case 'couple':
            seatTypeId = getSeatTypeIdByName('COUPLE');
            break;
        case 'broken':
            seatTypeId = 'BROKEN'; // Special flag
            break;
        case 'delete':
            seatTypeId = 'DELETE'; // Special flag
            break;
        case 'restore':
            seatTypeId = 'RESTORE'; // Special flag
            break;
    }

    if (!seatTypeId) {
        showToast('❌ Không tìm thấy loại ghế', 'error');
        return;
    }

    await updateRowSeatType(rowLabel, seatTypeId);
}

// ===================================
// HANDLE COLUMN MODE (Entire column)
// ===================================
async function handleColumnMode(columnIndex) {
    if (!columnIndex || columnIndex <= 0) {
        showToast('❌ Không xác định được cột', 'error');
        return;
    }

    console.log('📊 Column mode - applying action:', currentAction, 'to column:', columnIndex);

    let seatTypeId = '';

    switch (currentAction) {
        case 'normal':
            seatTypeId = getSeatTypeIdByName('NORMAL');
            break;
        case 'vip':
            seatTypeId = getSeatTypeIdByName('VIP');
            break;
        case 'couple':
            seatTypeId = getSeatTypeIdByName('COUPLE');
            break;
        case 'broken':
            seatTypeId = 'BROKEN'; // Special flag
            break;
        case 'delete':
            seatTypeId = 'DELETE'; // Special flag
            break;
        case 'restore':
            seatTypeId = 'RESTORE'; // Special flag
            break;
    }

    if (!seatTypeId) {
        showToast('❌ Không tìm thấy loại ghế', 'error');
        return;
    }

    await updateColumnSeatType(columnIndex, seatTypeId);
}

// ===================================
// HELPER: Get seat type ID by name
// ===================================
function getSeatTypeIdByName(name) {
    const seatTypeSelector = document.getElementById('seatTypeSelector');
    if (!seatTypeSelector) return null;

    const option = Array.from(seatTypeSelector.options).find(opt =>
        opt.getAttribute('data-name')?.toUpperCase() === name.toUpperCase()
    );
    return option?.value || null;
}

// ===================================
// CHANGE SEAT TYPE (normal/vip)
// ===================================
async function changeSeatType(seatId, type) {
    const seatTypeId = getSeatTypeIdByName(type === 'normal' ? 'NORMAL' : 'VIP');

    if (!seatTypeId) {
        showToast('❌ Không tìm thấy loại ghế', 'error');
        return;
    }

    const result = await updateSeatTypeAPI(seatId, seatTypeId, true);
    if (result.success) {
        showToast(`✅ Đã đổi sang ${type === 'normal' ? 'Ghế thường' : 'Ghế VIP'}`, 'success');
    }
}

// ===================================
// CREATE COUPLE SEAT
// ===================================
async function createCoupleSeat(seatElement) {
    const seatId = seatElement.getAttribute('data-seat-id');
    const coupleTypeId = getSeatTypeIdByName('COUPLE');

    if (!coupleTypeId) {
        showToast('❌ Không tìm thấy loại ghế đôi', 'error');
        return;
    }

    const result = await updateSeatTypeAPI(seatId, coupleTypeId, true);
    if (result.success) {
        showToast('✅ Đã tạo ghế đôi', 'success');
    }
}

// ===================================
// TOGGLE BROKEN SEAT
// ===================================
async function toggleBrokenSeat(seatElement, seatId) {
    const currentActive = seatElement.getAttribute('data-is-active') === 'true';
    const currentTypeId = seatElement.getAttribute('data-seat-type-id');

    // If already broken, make it active again
    const newActive = !currentActive;

    const result = await updateSeatTypeAPI(seatId, currentTypeId, newActive);
    if (result.success) {
        showToast(newActive ? '✅ Ghế đã hoạt động' : '⚠️ Ghế đã đánh dấu hỏng', 'success');
    }
}

// ===================================
// SOFT DELETE SEAT (No confirmation)
// ===================================
async function softDeleteSeat(seatId) {
    isSaving = true;

    try {
        const response = await fetch('/Seats/SoftDeleteSeat', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ seatId: seatId })
        });

        const data = await response.json();

        if (data.success && data.affectedSeats) {
            data.affectedSeats.forEach(seat => {
                updateSeatVisual(seat);
            });
            showToast('✅ Ghế đã được bỏ khỏi bố cục', 'success');
        } else {
            showToast('❌ ' + (data.message || 'Không thể bỏ ghế'), 'error');
        }
    } catch (err) {
        showToast('❌ Lỗi: ' + err.message, 'error');
    } finally {
        isSaving = false;
    }
}

// ===================================
// RESTORE SEAT
// ===================================
async function restoreSeat(seatId) {
    isSaving = true;

    try {
        const response = await fetch('/Seats/RestoreSeat', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ seatId: seatId })
        });

        const data = await response.json();

        if (data.success && data.seat) {
            updateSeatVisual(data.seat);
            showToast('✅ Ghế đã được khôi phục', 'success');
        } else {
            showToast('❌ ' + (data.message || 'Không thể khôi phục'), 'error');
        }
    } catch (err) {
        showToast('❌ Lỗi: ' + err.message, 'error');
    } finally {
        isSaving = false;
    }
}

// ===================================
// UPDATE ROW SEAT TYPE API
// ===================================
async function updateRowSeatType(rowLabel, seatTypeId) {
    if (isSaving) return;
    isSaving = true;

    try {
        const response = await fetch('/Seats/UpdateRowSeatType', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                cinemaTheaterId: currentTheaterId,
                rowLabel: rowLabel,
                seatTypeId: seatTypeId
            })
        });

        const data = await response.json();

        if (data.success && data.affectedSeats) {
            data.affectedSeats.forEach(seat => {
                updateSeatVisual(seat);
            });
            showToast(data.message || `✅ Đã cập nhật hàng ${rowLabel}`, 'success');
        } else {
            showToast('❌ ' + (data.message || 'Không thể cập nhật hàng'), 'error');
        }
    } catch (err) {
        console.error('❌ UpdateRowSeatType error:', err);
        showToast('❌ Lỗi: ' + err.message, 'error');
    } finally {
        isSaving = false;
    }
}

// ===================================
// UPDATE COLUMN SEAT TYPE API
// ===================================
async function updateColumnSeatType(columnIndex, seatTypeId) {
    if (isSaving) return;
    isSaving = true;

    try {
        const response = await fetch('/Seats/UpdateColumnSeatType', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                cinemaTheaterId: currentTheaterId,
                columnIndex: columnIndex,
                seatTypeId: seatTypeId
            })
        });

        const data = await response.json();

        if (data.success && data.affectedSeats) {
            data.affectedSeats.forEach(seat => {
                updateSeatVisual(seat);
            });
            showToast(data.message || `✅ Đã cập nhật cột ${columnIndex}`, 'success');
        } else {
            showToast('❌ ' + (data.message || 'Không thể cập nhật cột'), 'error');
        }
    } catch (err) {
        console.error('❌ UpdateColumnSeatType error:', err);
        showToast('❌ Lỗi: ' + err.message, 'error');
    } finally {
        isSaving = false;
    }
}

// ===================================
// UPDATE SEAT TYPE API (Single seat)
// ===================================
async function updateSeatTypeAPI(seatId, seatTypeId, isActive) {
    if (isSaving) {
        return { success: false };
    }

    isSaving = true;

    try {
        const response = await fetch('/Seats/UpdateSeatType', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json'
            },
            body: JSON.stringify({
                seatId: seatId,
                seatTypeId: seatTypeId,
                isActive: isActive
            })
        });

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const data = await response.json();

        if (!data.success) {
            showToast('❌ ' + (data.message || 'Không thể cập nhật'), 'error');
            return { success: false };
        }

        if (data.affectedSeats && data.affectedSeats.length > 0) {
            console.log(`✅ UpdateSeatType: Updating ${data.affectedSeats.length} seats in UI`);

            data.affectedSeats.forEach(seat => {
                updateSeatVisual(seat);
            });
        }

        return { success: true, data: data };
    } catch (err) {
        console.error('❌ UpdateSeatType error:', err);
        showToast('❌ Lỗi cập nhật: ' + err.message, 'error');
        return { success: false };
    } finally {
        isSaving = false;
    }
}

// ===================================
// UPDATE SEAT VISUAL
// ===================================
function updateSeatVisual(seatData) {
    const seatElement = document.querySelector(`[data-seat-id="${seatData.seatId}"]`);
    if (!seatElement) return;

    seatElement.setAttribute('data-seat-type-id', seatData.seatTypeId);
    seatElement.setAttribute('data-seat-type-name', seatData.seatTypeName);
    seatElement.setAttribute('data-is-active', seatData.isActive);
    seatElement.setAttribute('data-is-deleted', seatData.isDeleted);
    seatElement.setAttribute('data-pair-id', seatData.pairId || '');
    seatElement.setAttribute('data-seat-label', seatData.label || '');

    seatElement.classList.remove('seat-type-normal', 'seat-type-vip', 'seat-type-couple', 'inactive', 'removed-seat');

    if (seatData.isDeleted) {
        seatElement.classList.add('removed-seat');
        seatElement.innerHTML = '<span class="removed-icon">✗</span>';
    } else {
        const typeClass = 'seat-type-' + (seatData.seatTypeName?.toLowerCase() || 'normal');
        seatElement.classList.add(typeClass);

        if (!seatData.isActive) {
            seatElement.classList.add('inactive');
        }

        let content = '';

        if (seatData.seatTypeName?.toUpperCase() === 'VIP') {
            content += '<span class="seat-badge">V</span>';
        } else if (seatData.seatTypeName?.toUpperCase().includes('COUPLE')) {
            content += '<span class="seat-badge">CP</span>';
        }

        if (!seatData.isActive) {
            content += '<span class="broken-icon">✗</span>';
        }

        content += `<div class="seat-label-small">${seatData.label}</div>`;

        seatElement.innerHTML = content;
    }

    // Visual feedback
    seatElement.style.transform = 'scale(1.15)';
    seatElement.style.transition = 'transform 0.2s ease';
    setTimeout(() => {
        seatElement.style.transform = '';
    }, 200);
}

// ===================================
// CONFIRM DIALOG
// ===================================
function showConfirmDialog(title, message, okText = 'OK', cancelText = 'Hủy') {
    return new Promise(resolve => {
        const modal = document.getElementById('confirmModal');
        const backdrop = document.getElementById('confirmBackdrop');
        const titleEl = document.getElementById('confirmModalTitle');
        const msgEl = document.getElementById('confirmModalMessage');
        const okBtn = document.getElementById('confirmOkBtn');
        const cancelBtn = document.getElementById('confirmCancelBtn');

        if (!modal || !backdrop || !okBtn || !cancelBtn || !titleEl || !msgEl) {
            const result = window.confirm(message);
            resolve(result);
            return;
        }

        titleEl.textContent = title || '';
        msgEl.textContent = message || '';
        okBtn.textContent = okText || 'OK';
        cancelBtn.textContent = cancelText || 'Hủy';

        modal.style.display = 'block';
        backdrop.style.display = 'block';

        function cleanup(result) {
            okBtn.removeEventListener('click', onOk);
            cancelBtn.removeEventListener('click', onCancel);
            backdrop.removeEventListener('click', onCancel);
            document.removeEventListener('keydown', onKey);
            modal.style.display = 'none';
            backdrop.style.display = 'none';
            resolve(result);
        }

        function onOk(e) {
            e.preventDefault();
            cleanup(true);
        }
        function onCancel(e) {
            e && e.preventDefault();
            cleanup(false);
        }
        function onKey(e) {
            if (e.key === 'Escape') cleanup(false);
            if (e.key === 'Enter') cleanup(true);
        }

        okBtn.addEventListener('click', onOk);
        cancelBtn.addEventListener('click', onCancel);
        backdrop.addEventListener('click', onCancel);
        document.addEventListener('keydown', onKey);

        okBtn.focus();
    });
}

// ===================================
// ADD ROW/COLUMN
// ===================================
async function addRowAtEnd() {
    if (!currentTheaterId) {
        showToast('❌ Chưa chọn phòng chiếu', 'error');
        return;
    }

    if (isSaving) {
        showToast('⏳ Đang xử lý...', 'warning');
        return;
    }

    isSaving = true;
    showToast('⏳ Đang thêm hàng...', 'info');

    try {
        const response = await fetch('/Seats/AddRow', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json'
            },
            body: JSON.stringify({
                cinemaTheaterId: currentTheaterId
            })
        });

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const data = await response.json();

        if (data.success && data.newRow) {
            console.log('✅ AddRow SUCCESS:', data.newRow);
            const newRowNumber = data.newRow.rowNumber;
            const newRowLabel = data.newRow.rowLabel;
            const seats = data.newRow.seats || [];

            const seatsHTML = seats.map(seat => `
                <div class="seat seat-type-${(seat.seatTypeName || 'normal').toLowerCase()}" 
                     data-seat-id="${seat.seatId}" 
                     data-seat-type-id="${seat.seatTypeId}" 
                     data-seat-type-name="${seat.seatTypeName}" 
                     data-row-number="${seat.rowNumber}" 
                     data-column-index="${seat.columnIndex}" 
                     data-is-active="${seat.isActive}" 
                     data-is-deleted="false"
                     data-seat-label="${seat.label}"
                     data-pair-id="${seat.pairId || ''}" 
                     onclick="onSeatClick(this)">
                    ${seat.seatTypeId === 'ST002' ? '<span class="seat-badge">V</span>' : ''}
                    ${seat.seatTypeName?.toUpperCase().includes('COUPLE') ? '<span class="seat-badge">CP</span>' : ''}
                    ${!seat.isActive ? '<span class="broken-icon">✗</span>' : ''}
                    <div class="seat-label-small">${seat.label}</div>
                </div>
            `).join('');

            const newRowHTML = `
                <div class="seat-row" data-row-number="${newRowNumber}" data-row-label="${newRowLabel}">
                    <div class="row-label" data-row-index="${newRowNumber}" data-row-label="${newRowLabel}">
                        <span class="row-label-text">${newRowLabel}</span>
                        <button class="delete-btn" title="Xóa hàng này">
                            <i class="fas fa-times"></i>
                        </button>
                    </div>
                    <div class="seats-in-row">
                        ${seatsHTML}
                    </div>
                    <div class="row-label" data-row-index="${newRowNumber}" data-row-label="${newRowLabel}">
                        <span class="row-label-text">${newRowLabel}</span>
                        <button class="delete-btn" title="Xóa hàng này">
                            <i class="fas fa-times"></i>
                        </button>
                    </div>
                </div>
            `;

            const addRowSection = document.querySelector('.add-row-section');
            if (addRowSection) {
                addRowSection.insertAdjacentHTML('beforebegin', newRowHTML);
            } else {
                document.querySelector('.seats-grid-container').insertAdjacentHTML('beforeend', newRowHTML);
            }

            initializeDeleteButtons();
            refreshLastMarkers();

            showToast('✅ Đã thêm hàng mới', 'success');
        } else {
            showToast('❌ Không thể thêm hàng', 'error');
        }
    } catch (err) {
        console.error('❌ AddRow error:', err);
        showToast('❌ Lỗi thêm hàng: ' + err.message, 'error');
    } finally {
        isSaving = false;
    }
}

async function addColumnAtEnd() {
    if (!currentTheaterId) {
        showToast('❌ Chưa chọn phòng chiếu', 'error');
        return;
    }

    if (isSaving) {
        showToast('⏳ Đang xử lý...', 'warning');
        return;
    }

    isSaving = true;
    showToast('⏳ Đang thêm cột...', 'info');

    try {
        const response = await fetch('/Seats/AddColumn', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json'
            },
            body: JSON.stringify({
                cinemaTheaterId: currentTheaterId
            })
        });

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const data = await response.json();

        if (data.success && data.newSeats) {
            console.log('✅ AddColumn SUCCESS:', data);

            const columnNumbersRow = document.querySelector('.column-numbers');
            const addColumnBtn = columnNumbersRow.querySelector('.add-column-btn');

            const newColHTML = `
                <div class="column-number" data-col-index="${data.newColIndex}">
                    <span class="col-label-text">${data.newColIndex}</span>
                    <button class="delete-btn" title="Xóa cột ${data.newColIndex}">
                        <i class="fas fa-times"></i>
                    </button>
                </div>
            `;
            addColumnBtn.insertAdjacentHTML('beforebegin', newColHTML);

            data.newSeats.forEach(seat => {
                const rowElement = document.querySelector(`[data-row-number="${seat.rowNumber}"]`);
                if (rowElement) {
                    const seatsInRow = rowElement.querySelector('.seats-in-row');
                    const seatHTML = `
                        <div class="seat seat-type-${(seat.seatTypeName || 'normal').toLowerCase()}" 
                             data-seat-id="${seat.seatId}" 
                             data-seat-type-id="${seat.seatTypeId}" 
                             data-seat-type-name="${seat.seatTypeName}" 
                             data-row-number="${seat.rowNumber}" 
                             data-column-index="${seat.columnIndex}" 
                             data-is-active="${seat.isActive}" 
                             data-is-deleted="false"
                             data-seat-label="${seat.label}"
                             data-pair-id="${seat.pairId || ''}" 
                             onclick="onSeatClick(this)">
                            ${seat.seatTypeId === 'ST002' ? '<span class="seat-badge">V</span>' : ''}
                            ${seat.seatTypeName?.toUpperCase().includes('COUPLE') ? '<span class="seat-badge">CP</span>' : ''}
                            ${!seat.isActive ? '<span class="broken-icon">✗</span>' : ''}
                            <div class="seat-label-small">${seat.label}</div>
                        </div>
                    `;
                    seatsInRow.insertAdjacentHTML('beforeend', seatHTML);
                }
            });

            initializeDeleteButtons();
            refreshLastMarkers();
            showToast('✅ Đã thêm cột mới', 'success');
        } else {
            showToast('❌ ' + (data.message || 'Không thể thêm cột'), 'error');
        }
    } catch (err) {
        console.error('❌ AddColumn error:', err);
        showToast('❌ Lỗi thêm cột: ' + err.message, 'error');
    } finally {
        isSaving = false;
    }
}

// ===================================
// DELETE ROW/COLUMN
// ===================================
async function deleteRow(rowLabel) {
    if (!currentTheaterId) {
        showToast('❌ Chưa chọn phòng chiếu', 'error');
        return;
    }

    const confirmed = await showConfirmDialog('Xác nhận xóa', `⚠️ Bạn có chắc muốn xóa hàng "${rowLabel}"? Thao tác này không thể hoàn tác!`, 'OK', 'Hủy');
    if (!confirmed) {
        return;
    }

    if (isSaving) {
        showToast('⏳ Đang xử lý...', 'warning');
        return;
    }

    isSaving = true;
    showToast('⏳ Đang xóa hàng...', 'info');

    try {
        const response = await fetch('/Seats/DeleteRow', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json'
            },
            body: JSON.stringify({
                cinemaTheaterId: currentTheaterId,
                rowLabel: rowLabel
            })
        });

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const data = await response.json();

        if (data.success) {
            console.log('✅ DeleteRow SUCCESS');

            const rowElement = Array.from(document.querySelectorAll('.seat-row')).find(row => {
                const labelText = row.querySelector('.row-label-text')?.textContent;
                return labelText === rowLabel;
            });

            if (rowElement) {
                rowElement.remove();
            }

            initializeDeleteButtons();
            refreshLastMarkers();

            showToast('✅ Đã xóa hàng ' + rowLabel, 'success');
        } else {
            showToast('❌ ' + (data.message || 'Không thể xóa hàng'), 'error');
        }
    } catch (err) {
        console.error('❌ DeleteRow error:', err);
        showToast('❌ Lỗi xóa hàng: ' + err.message, 'error');
    } finally {
        isSaving = false;
    }
}

async function deleteColumn(colIndex) {
    if (!currentTheaterId) {
        showToast('❌ Chưa chọn phòng chiếu', 'error');
        return;
    }

    const confirmed = await showConfirmDialog('Xác nhận xóa', `⚠️ Bạn có chắc muốn xóa cột ${colIndex}? Thao tác này không thể hoàn tác!`, 'OK', 'Hủy');
    if (!confirmed) {
        return;
    }

    if (isSaving) {
        showToast('⏳ Đang xử lý...', 'warning');
        return;
    }

    isSaving = true;
    showToast('⏳ Đang xóa cột...', 'info');

    try {
        const response = await fetch('/Seats/DeleteColumn', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json'
            },
            body: JSON.stringify({
                cinemaTheaterId: currentTheaterId,
                columnIndex: colIndex
            })
        });

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const data = await response.json();

        if (data.success) {
            console.log('✅ DeleteColumn SUCCESS');

            if (data.affectedSeats && data.affectedSeats.length > 0) {
                console.log(`✅ DeleteColumn: Updating ${data.affectedSeats.length} unpaired seats in UI`);

                data.affectedSeats.forEach(seat => {
                    updateSeatVisual(seat);
                });
            }

            const columnNumbersRow = document.querySelector('.column-numbers');
            if (columnNumbersRow && data.newNumOfColumns) {
                const addBtn = columnNumbersRow.querySelector('.add-column-btn');
                columnNumbersRow.innerHTML = '';

                const rowLabelSpace = document.createElement('div');
                rowLabelSpace.className = 'row-label-space';
                columnNumbersRow.appendChild(rowLabelSpace);

                for (let i = 1; i <= data.newNumOfColumns; i++) {
                    const colHTML = `
                        <div class="column-number" data-col-index="${i}">
                            <span class="col-label-text">${i}</span>
                            <button class="delete-btn" title="Xóa cột ${i}">
                                <i class="fas fa-times"></i>
                            </button>
                        </div>
                    `;
                    columnNumbersRow.insertAdjacentHTML('beforeend', colHTML);
                }

                columnNumbersRow.appendChild(addBtn);
            }

            document.querySelectorAll(`[data-column-index="${colIndex}"]`).forEach(seat => {
                seat.remove();
            });

            initializeDeleteButtons();
            refreshLastMarkers();
            showToast('✅ Đã xóa cột ' + colIndex, 'success');
        } else {
            showToast('❌ ' + (data.message || 'Không thể xóa cột'), 'error');
        }
    } catch (err) {
        console.error('❌ DeleteColumn error:', err);
        showToast('❌ Lỗi xóa cột: ' + err.message, 'error');
    } finally {
        isSaving = false;
    }
}
