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

    // ✅ FIX: Block click on aisle seats
    const isAisle = seatElement.getAttribute('data-is-aisle') === 'true';
    const seatLabel = seatElement.getAttribute('data-seat-label');
    if (isAisle || !seatLabel) {
        // Freeze: do nothing and do not show any toast or feedback
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
    // ✅ FIX: Allow restore action on deleted seats, block other actions
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
            // ✅ FIX: Restore works on both deleted and broken seats
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
            seatTypeId = getSeatTypeId('COUPLE');
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

    // Find all seats in this row
    const seatsInRow = Array.from(document.querySelectorAll(`.seat-row[data-row-label="${rowLabel}"] .seat`));
    if (!seatsInRow.length) {
        showToast('❌ Không tìm thấy hàng', 'error');
        return;
    }

    // ✅ FIX: Filter out aisle seats - only work with non-aisle seats
    const nonAisleSeats = seatsInRow.filter(s => 
        s.getAttribute('data-is-aisle') !== 'true' && s.getAttribute('data-seat-label')
    );

    if (!nonAisleSeats.length) {
        showToast('⚠️ Hàng này chỉ có lối đi, không có ghế để thay đổi', 'warning');
        return;
    }

    // If target is COUPLE, ensure each adjacent pair is valid (skip aisle seats)
    const coupleTypeId = getSeatTypeIdByName('COUPLE');
    const isCoupleTarget = coupleTypeId && seatTypeId === coupleTypeId;

    if (isCoupleTarget) {
        // Verify that there exists valid adjacent partners for seats we would convert
        // Only check among non-aisle seats
        for (const s of nonAisleSeats) {
            const isDeleted = s.getAttribute('data-is-deleted') === 'true';
            const isActive = s.getAttribute('data-is-active') === 'true';
            const pairId = s.getAttribute('data-pair-id');
            if (isDeleted || !isActive || pairId) continue; // skip unsuitable seats

            const rowNumber = s.closest('.seat-row')?.getAttribute('data-row-number');
            const colIndex = parseInt(s.getAttribute('data-column-index'));
            const right = document.querySelector(`[data-row-number="${rowNumber}"][data-column-index="${colIndex + 1}"]`);
            const left = document.querySelector(`[data-row-number="${rowNumber}"][data-column-index="${colIndex - 1}"]`);

            const validRight = right && right.getAttribute('data-is-aisle') !== 'true' && right.getAttribute('data-is-deleted') !== 'true' && right.getAttribute('data-is-active') === 'true' && !right.getAttribute('data-pair-id');
            const validLeft = left && left.getAttribute('data-is-aisle') !== 'true' && left.getAttribute('data-is-deleted') !== 'true' && left.getAttribute('data-is-active') === 'true' && !left.getAttribute('data-pair-id');

            if (!validRight && !validLeft) {
                showToast('❌ Hàng không có ghế hợp lệ để tạo ghế đôi', 'error');
                return;
            }
        }
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
            seatTypeId = getSeatTypeId('COUPLE');
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

    // Find all seats in this column
    const seatsInCol = Array.from(document.querySelectorAll(`.seat[data-column-index="${columnIndex}"]`));
    if (!seatsInCol.length) {
        showToast('❌ Không tìm thấy cột', 'error');
        return;
    }

    // ✅ FIX: Filter out aisle seats - only work with non-aisle seats
    const nonAisleSeats = seatsInCol.filter(s => 
        s.getAttribute('data-is-aisle') !== 'true' && s.getAttribute('data-seat-label')
    );

    if (!nonAisleSeats.length) {
        showToast('⚠️ Cột này chỉ có lối đi, không có ghế để thay đổi', 'warning');
        return;
    }

    const coupleTypeId = getSeatTypeIdByName('COUPLE');
    const isCoupleTarget = coupleTypeId && seatTypeId === coupleTypeId;

    if (isCoupleTarget) {
        for (const s of nonAisleSeats) {
            const isDeleted = s.getAttribute('data-is-deleted') === 'true';
            const isActive = s.getAttribute('data-is-active') === 'true';
            const pairId = s.getAttribute('data-pair-id');
            if (isDeleted || !isActive || pairId) continue;

            const rowNumber = s.closest('.seat-row')?.getAttribute('data-row-number');
            const colIndex = parseInt(s.getAttribute('data-column-index'));
            const right = document.querySelector(`[data-row-number="${rowNumber}"][data-column-index="${colIndex + 1}"]`);
            const left = document.querySelector(`[data-row-number="${rowNumber}"][data-column-index="${colIndex - 1}"]`);

            const validRight = right && right.getAttribute('data-is-aisle') !== 'true' && right.getAttribute('data-is-deleted') !== 'true' && right.getAttribute('data-is-active') === 'true' && !right.getAttribute('data-pair-id');
            const validLeft = left && left.getAttribute('data-is-aisle') !== 'true' && left.getAttribute('data-is-deleted') !== 'true' && left.getAttribute('data-is-active') === 'true' && !left.getAttribute('data-pair-id');

            if (!validRight && !validLeft) {
                showToast('❌ Cột không có ghế hợp lệ để tạo ghế đôi', 'error');
                return;
            }
        }
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

// ✅ FIX: Add alias function for compatibility
function getSeatTypeId(name) {
    return getSeatTypeIdByName(name);
}

// ===================================
// CHANGE SEAT TYPE (normal/vip)
// ===================================
async function changeSeatType(seatId, type) {
    // Guard changeSeatType: prevent changing aisle seats
    const seatElement = document.querySelector(`[data-seat-id="${seatId}"]`);
    if (seatElement) {
        const isAisle = seatElement.getAttribute('data-is-aisle') === 'true' || !seatElement.getAttribute('data-seat-label');
        if (isAisle) {
            showToast('⚠️ Không thể thay đổi loại ghế cho lối đi', 'warning');
            return;
        }
    }

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
    const coupleTypeId = getSeatTypeId('COUPLE');

    if (!coupleTypeId) {
        showToast('❌ Không tìm thấy loại ghế đôi', 'error');
        return;
    }

    // Guard: cannot operate on aisle seats
    const thisIsAisle = seatElement.getAttribute('data-is-aisle') === 'true' || !seatElement.getAttribute('data-seat-label');
    if (thisIsAisle) {
        showToast('⚠️ Không thể thao tác trên lối đi', 'warning');
        return;
    }

    const rowNumber = parseInt(seatElement.getAttribute('data-row-number'));
    const columnIndex = parseInt(seatElement.getAttribute('data-column-index'));

    // Prefer partner on the right, fallback to left
    let partner = document.querySelector(`[data-row-number="${rowNumber}"][data-column-index="${columnIndex + 1}"]`);
    if (partner) {
        const pIsAisle = partner.getAttribute('data-is-aisle') === 'true' || !partner.getAttribute('data-seat-label');
        const pIsDeleted = partner.getAttribute('data-is-deleted') === 'true';
        if (pIsAisle || pIsDeleted) {
            partner = null; // treat as invalid
        }
    }

    if (!partner) {
        partner = document.querySelector(`[data-row-number="${rowNumber}"][data-column-index="${columnIndex - 1}"]`);
        if (partner) {
            const pIsAisle = partner.getAttribute('data-is-aisle') === 'true' || !partner.getAttribute('data-seat-label');
            const pIsDeleted = partner.getAttribute('data-is-deleted') === 'true';
            if (pIsAisle || pIsDeleted) {
                partner = null;
            }
        }
    }

    if (!partner) {
        showToast('❌ Không tìm thấy ghế hợp lệ làm cặp', 'error');
        return;
    }

    // Both seats valid — call API on the clicked seat and let backend update the pair
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
    const pairId = seatElement.getAttribute('data-pair-id');

    // If already broken, make it active again
    const newActive = !currentActive;

    // ✅ FIX: If this is a couple seat, toggle BOTH seats in the pair
    if (pairId && pairId.trim() !== '') {
        const pairedSeats = document.querySelectorAll(`[data-pair-id="${pairId}"]`);
        
        if (pairedSeats.length !== 2) {
            showToast('❌ Không tìm thấy ghế cặp đôi đầy đủ', 'error');
            return;
        }

        // Update both seats via API
        let allSuccess = true;
        const affectedSeats = [];

        for (const seat of pairedSeats) {
            const id = seat.getAttribute('data-seat-id');
            const typeId = seat.getAttribute('data-seat-type-id');
            
            const result = await updateSeatTypeAPI(id, typeId, newActive);
            
            if (!result.success) {
                allSuccess = false;
                break;
            }
            
            if (result.data?.affectedSeats) {
                affectedSeats.push(...result.data.affectedSeats);
            }
        }

        if (allSuccess) {
            showToast(
                newActive ? '✅ Cả 2 ghế đã hoạt động trở lại' : '⚠️ Cả 2 ghế đã đánh dấu hỏng', 
                'success'
            );
        }
    } else {
        // Single seat - normal behavior
        const currentTypeId = seatElement.getAttribute('data-seat-type-id');
        const result = await updateSeatTypeAPI(seatId, currentTypeId, newActive);
        
        if (result.success) {
            showToast(newActive ? '✅ Ghế đã hoạt động' : '⚠️ Ghế đã đánh dấu hỏng', 'success');
        }
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

    // Find all seats in this row
    const seatsInRow = Array.from(document.querySelectorAll(`.seat-row[data-row-label="${rowLabel}"] .seat`));
    if (!seatsInRow.length) {
        showToast('❌ Không tìm thấy hàng', 'error');
        return;
    }

    // ✅ FIX: Filter out aisle seats - only validate non-aisle seats
    const nonAisleSeats = seatsInRow.filter(s => 
        s.getAttribute('data-is-aisle') !== 'true' && s.getAttribute('data-seat-label')
    );

    if (!nonAisleSeats.length) {
        showToast('⚠️ Hàng này chỉ có lối đi, không có ghế để thay đổi', 'warning');
        return;
    }

    // If target is COUPLE, ensure each adjacent pair is valid (skip aisle seats)
    const coupleTypeId = getSeatTypeIdByName('COUPLE');
    const isCoupleTarget = coupleTypeId && seatTypeId === coupleTypeId;

    if (isCoupleTarget) {
        // Verify that there exists valid adjacent partners for seats we would convert
        for (const s of nonAisleSeats) {
            const isDeleted = s.getAttribute('data-is-deleted') === 'true';
            const isActive = s.getAttribute('data-is-active') === 'true';
            const pairId = s.getAttribute('data-pair-id');
            if (isDeleted || !isActive || pairId) continue; // skip unsuitable seats

            const rowNumber = s.closest('.seat-row')?.getAttribute('data-row-number');
            const colIndex = parseInt(s.getAttribute('data-column-index'));
            const right = document.querySelector(`[data-row-number="${rowNumber}"][data-column-index="${colIndex + 1}"]`);
            const left = document.querySelector(`[data-row-number="${rowNumber}"][data-column-index="${colIndex - 1}"]`);

            const validRight = right && right.getAttribute('data-is-aisle') !== 'true' && right.getAttribute('data-is-deleted') !== 'true' && right.getAttribute('data-is-active') === 'true' && !right.getAttribute('data-pair-id');
            const validLeft = left && left.getAttribute('data-is-aisle') !== 'true' && left.getAttribute('data-is-deleted') !== 'true' && left.getAttribute('data-is-active') === 'true' && !left.getAttribute('data-pair-id');

            if (!validRight && !validLeft) {
                showToast('❌ Hàng không có ghế hợp lệ để tạo ghế đôi', 'error');
                return;
            }
        }
    }

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

    const seatsInCol = Array.from(document.querySelectorAll(`.seat[data-column-index="${columnIndex}"]`));
    if (!seatsInCol.length) {
        showToast('❌ Không tìm thấy cột', 'error');
        return;
    }

    // ✅ FIX: Filter out aisle seats - only validate non-aisle seats
    const nonAisleSeats = seatsInCol.filter(s => 
        s.getAttribute('data-is-aisle') !== 'true' && s.getAttribute('data-seat-label')
    );

    if (!nonAisleSeats.length) {
        showToast('⚠️ Cột này chỉ có lối đi, không có ghế để thay đổi', 'warning');
        return;
    }

    const coupleTypeId = getSeatTypeIdByName('COUPLE');
    const isCoupleTarget = coupleTypeId && seatTypeId === coupleTypeId;

    if (isCoupleTarget) {
        for (const s of nonAisleSeats) {
            const isDeleted = s.getAttribute('data-is-deleted') === 'true';
            const isActive = s.getAttribute('data-is-active') === 'true';
            const pairId = s.getAttribute('data-pair-id');
            if (isDeleted || !isActive || pairId) continue;

            const rowNumber = s.closest('.seat-row')?.getAttribute('data-row-number');
            const colIndex = parseInt(s.getAttribute('data-column-index'));
            const right = document.querySelector(`[data-row-number="${rowNumber}"][data-column-index="${colIndex + 1}"]`);
            const left = document.querySelector(`[data-row-number="${rowNumber}"][data-column-index="${colIndex - 1}"]`);

            const validRight = right && right.getAttribute('data-is-aisle') !== 'true' && right.getAttribute('data-is-deleted') !== 'true' && right.getAttribute('data-is-active') === 'true' && !right.getAttribute('data-pair-id');
            const validLeft = left && left.getAttribute('data-is-aisle') !== 'true' && left.getAttribute('data-is-deleted') !== 'true' && left.getAttribute('data-is-active') === 'true' && !left.getAttribute('data-pair-id');

            if (!validRight && !validLeft) {
                showToast('❌ Cột không có ghế hợp lệ để tạo ghế đôi', 'error');
                return;
            }
        }
    }

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
    seatElement.setAttribute('data-is-aisle', seatData.isAisle || false); // ✅ Add IsAisle
    seatElement.setAttribute('data-pair-id', seatData.pairId || '');
    seatElement.setAttribute('data-seat-label', seatData.label || '');

    seatElement.classList.remove('seat-type-normal', 'seat-type-vip', 'seat-type-couple', 'inactive', 'removed-seat', 'aisle-seat-visual');

    // Handle seats with null/empty labels as aisles
    if (!seatData.label) {
        // Use literal double colon for marker
        seatElement.innerHTML = '<span class="aisle-marker">::</span>';
        seatElement.style.pointerEvents = 'none';
        seatElement.style.cursor = 'not-allowed';
        seatElement.title = 'Lối đi';
        seatElement.classList.add('aisle-seat-visual');
    } else if (seatData.isAisle) {
        seatElement.classList.add('aisle-seat-visual');
        seatElement.title = 'Lối đi';
        // ensure marker is literal double colon
        seatElement.innerHTML = '<span class="aisle-marker">::</span>';
        seatElement.style.pointerEvents = 'none';
        seatElement.style.cursor = 'not-allowed';
    } else if (seatData.isDeleted) {
        seatElement.classList.add('removed-seat');
        seatElement.innerHTML = '<span class="removed-icon">✗</span>';
    } else {
        seatElement.classList.remove('removed-seat', 'aisle-seat');
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
        seatElement.title = seatData.label || '';
        seatElement.style.pointerEvents = '';
        seatElement.style.cursor = '';
    }

    // Always apply transition
    seatElement.style.transition = 'transform 0.2s ease';

    // Visual feedback
    seatElement.style.transform = 'scale(1.15)';
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
                     data-is-deleted="${seat.isDeleted}"
                     data-is-aisle="${seat.isAisle || false}"
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
                             data-is-deleted="${seat.isDeleted}"
                             data-is-aisle="${seat.isAisle || false}"
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

    const confirmed = await showConfirmDialog('Xác nhận xóa', ` Bạn có chắc muốn xóa hàng "${rowLabel}"? Thao tác này không thể hoàn tác!`, 'OK', 'Hủy');
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

// ===================================
// AISLE TOGGLE FEATURE
// ===================================

// Initialize aisle toggle buttons on page load
document.addEventListener('DOMContentLoaded', function () {
    initializeAisleToggleButtons();
    markAisleHeadersOnLoad();
});

function initializeAisleToggleButtons() {
    // Attach click handlers to all aisle toggle buttons
    document.querySelectorAll('.aisle-toggle-btn').forEach(btn => {
        btn.addEventListener('click', async function (e) {
            e.stopPropagation();
            e.preventDefault();

            const isColumnHeader = this.closest('.column-number');
            const isRowHeader = this.closest('.row-label');

            if (isColumnHeader) {
                await toggleColumnAisle(isColumnHeader);
            } else if (isRowHeader) {
                await toggleRowAisle(isRowHeader);
            }
        });
    });

    console.log('✅ Aisle toggle buttons initialized');
}

async function toggleRowAisle(rowLabelElement) {
    if (isSaving) {
        showToast('⏳ Đang xử lý...', 'warning');
        return;
    }

    const rowLabel = rowLabelElement.getAttribute('data-row-label');
    const rowElement = rowLabelElement.closest('.seat-row');
    const theaterId = rowElement.getAttribute('data-theater-id');

    if (!rowLabel || !theaterId) {
        showToast('❌ Không xác định được hàng', 'error');
        return;
    }

    isSaving = true;
    const toggleBtn = rowLabelElement.querySelector('.aisle-toggle-btn');
    if (toggleBtn) {
        toggleBtn.classList.add('loading');
    }

    showToast('⏳ Đang chuyển đổi hàng...', 'info');

    try {
        const response = await fetch('/Seats/ToggleRowAisle', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json'
            },
            body: JSON.stringify({
                cinemaTheaterId: theaterId,
                rowIndex: rowLabel
            })
        });

        // ✅ FIX: Check response.ok BEFORE parsing JSON
        if (!response.ok) {
            let errorMessage = `HTTP ${response.status}: ${response.statusText}`;
            try {
                const errorData = await response.json();
                if (errorData.message) {
                    errorMessage = errorData.message;
                }
            } catch {
                // Can't parse JSON error, use HTTP status
            }
            throw new Error(errorMessage);
        }

        const data = await response.json();

        if (!data.success) {
            showToast('❌ ' + (data.message || 'Không thể chuyển đổi hàng'), 'error');
            return;
        }

        // Update UI: Mark header as aisle or remove aisle class
        const allRowLabels = rowElement.querySelectorAll('.row-label');
        if (data.isNowAisle) {
            allRowLabels.forEach(label => label.classList.add('is-aisle'));
        } else {
            allRowLabels.forEach(label => label.classList.remove('is-aisle'));
        }

        // Update affected seats in UI
        if (data.affectedSeats && data.affectedSeats.length > 0) {
            console.log(`✅ ToggleRowAisle: Updating ${data.affectedSeats.length} seats in UI`);

            data.affectedSeats.forEach(seat => {
                updateSeatVisual(seat);
            });
        }

        showToast('Tạo lối đi thành công.', 'success');

    } catch (err) {
        console.error('❌ ToggleRowAisle error:', err);
        showToast('❌ Lỗi: ' + err.message, 'error');
    } finally {
        isSaving = false;
        if (toggleBtn) {
            toggleBtn.classList.remove('loading');
        }
    }
}

async function toggleColumnAisle(columnElement) {
    if (isSaving) {
        showToast('⏳ Đang xử lý...', 'warning');
        return;
    }

    const colIndex = parseInt(columnElement.getAttribute('data-col-index'));
    const theaterId = columnElement.getAttribute('data-theater-id');

    if (!colIndex || !theaterId) {
        showToast('❌ Không xác định được cột', 'error');
        return;
    }

    isSaving = true;
    const toggleBtn = columnElement.querySelector('.aisle-toggle-btn');
    if (toggleBtn) {
        toggleBtn.classList.add('loading');
    }

    showToast('⏳ Đang chuyển đổi cột...', 'info');

    try {
        const response = await fetch('/Seats/ToggleColumnAisle', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json'
            },
            body: JSON.stringify({
                cinemaTheaterId: theaterId,
                columnIndex: colIndex
            })
        });

        // ✅ FIX: Check response.ok BEFORE parsing JSON
        if (!response.ok) {
            // Try to get error message from response
            let errorMessage = `HTTP ${response.status}: ${response.statusText}`;
            try {
                const errorData = await response.json();
                if (errorData.message) {
                    errorMessage = errorData.message;
                }
            } catch {
                // Can't parse JSON error, use HTTP status
            }
            throw new Error(errorMessage);
        }

        const data = await response.json();

        if (!data.success) {
            showToast('❌ ' + (data.message || 'Không thể chuyển đổi cột'), 'error');
            return;
        }

        // Update UI: Mark header as aisle or remove aisle class
        if (data.isNowAisle) {
            columnElement.classList.add('is-aisle');
        } else {
            columnElement.classList.remove('is-aisle');
        }

        // Update affected seats in UI
        if (data.affectedSeats && data.affectedSeats.length > 0) {
            console.log(`✅ ToggleColumnAisle: Updating ${data.affectedSeats.length} seats in UI`);

            data.affectedSeats.forEach(seat => {
                updateSeatVisual(seat);
            });
        }

        showToast('Tạo lối đi thành công.', 'success');

    } catch (err) {
        console.error('❌ ToggleColumnAisle error:', err);
        showToast('❌ Lỗi: ' + err.message, 'error');
    } finally {
        isSaving = false;
        if (toggleBtn) {
            toggleBtn.classList.remove('loading');
        }
    }
}

function markAisleHeadersOnLoad() {
    // ✅ FIX: Mark column headers based on CURRENT DOM state (data-is-aisle), not cached JS state
    const allColumns = document.querySelectorAll('.column-number[data-col-index]');
    allColumns.forEach(colHeader => {
        const colIndex = parseInt(colHeader.getAttribute('data-col-index'));
        const seatsInCol = document.querySelectorAll(`[data-column-index="${colIndex}"]`);

        if (seatsInCol.length > 0) {
            // ✅ RELOAD BUG FIX: Check Is_Aisle flag in data attributes, not just IsDeleted
            const allAisle = Array.from(seatsInCol).every(seat =>
                seat.getAttribute('data-is-aisle') === 'true'
            );

            if (allAisle) {
                colHeader.classList.add('is-aisle');
            } else {
                // ✅ CRITICAL: Remove stale aisle class if not all seats are aisle anymore
                colHeader.classList.remove('is-aisle');
            }
        }
    });

    // ✅ FIX: Mark row headers based on CURRENT DOM state (data-is-deleted for row aisles)
    const allRows = document.querySelectorAll('.seat-row[data-row-label]');
    allRows.forEach(rowElement => {
        const rowLabel = rowElement.getAttribute('data-row-label');
        const seatsInRow = rowElement.querySelectorAll('.seat[data-row-number]');

        if (seatsInRow.length > 0) {
            const allDeleted = Array.from(seatsInRow).every(seat =>
                seat.getAttribute('data-is-deleted') === 'true'
            );

            const rowLabels = rowElement.querySelectorAll('.row-label');
            if (allDeleted) {
                rowLabels.forEach(label => label.classList.add('is-aisle'));
            } else {
                // ✅ CRITICAL: Remove stale aisle class
                rowLabels.forEach(label => label.classList.remove('is-aisle'));
            }
        }
    });

    console.log('✅ Aisle headers marked on page load FROM DOM DATA ONLY');
}
