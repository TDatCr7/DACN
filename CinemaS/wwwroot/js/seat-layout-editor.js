// ===================================
// DYNAMIC SEAT LAYOUT EDITOR - INSTANT SAVE & UPDATE
// All changes save immediately and update UI without page reload
// ===================================

let selectModeEnabled = false;
let currentTheaterId = null;
let isSaving = false;
let editMode = 'normal';

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

        // show
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

    initializeEditModeButtons();
    initializeDeleteButtons();
    refreshLastMarkers();

    console.log('📊 Initial state:', {
        theaterId: currentTheaterId,
        editMode: editMode,
        selectMode: selectModeEnabled,
        totalSeats: document.querySelectorAll('.seat').length
    });
});

// ===================================
// INITIALIZE DELETE BUTTONS
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

    // Refresh markers so only last shows
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

// ===================================
// EDIT MODE BUTTONS
// ===================================
function initializeEditModeButtons() {
    const modeButtons = document.querySelectorAll('[data-edit-mode]');
    modeButtons.forEach(btn => {
        btn.addEventListener('click', function () {
            const mode = this.getAttribute('data-edit-mode');
            setEditMode(mode);

            modeButtons.forEach(b => {
                b.classList.remove('active', 'btn-primary');
                b.classList.add('btn-outline-primary');
            });
            this.classList.remove('btn-outline-primary');
            this.classList.add('active', 'btn-primary');
        });
    });
}

function setEditMode(mode) {
    editMode = mode;
    const modeText = {
        'normal': 'Ghế thường',
        'vip': 'Ghế VIP',
        'couple': 'Ghế đôi',
        'broken': 'Ghế hỏng'
    };
}

function toggleSelectMode() {
    selectModeEnabled = !selectModeEnabled;
    const textSpan = document.getElementById('selectModeText');
    const checkboxes = document.querySelectorAll('.seat-checkbox');

    if (selectModeEnabled) {
        textSpan.textContent = 'Chọn nhiều: BẬT';
        textSpan.parentElement.classList.add('btn-success');
        textSpan.parentElement.classList.remove('btn-primary');

        checkboxes.forEach(cb => {
            cb.style.display = 'block';
            cb.style.position = 'absolute';
            cb.style.top = '2px';
            cb.style.left = '2px';
            cb.style.zIndex = '10';
        });

        showToast('📋 Chọn nhiều BẬT', 'info');
    } else {
        textSpan.textContent = 'Chọn nhiều: TẮT';
        textSpan.parentElement.classList.remove('btn-success');
        textSpan.parentElement.classList.add('btn-primary');

        checkboxes.forEach(cb => {
            cb.style.display = 'none';
            cb.checked = false;
        });

        updateSelectedCount();
    }
}

function updateSelectedCount() {
    const selected = document.querySelectorAll('.seat-checkbox:checked');
    const count = selected.length;

    document.getElementById('selectedCountText').textContent = `${count} ghế đã chọn`;

    const bulkButtons = ['btnBulkDelete', 'btnBulkBroken', 'btnBulkCouple'];
    bulkButtons.forEach(btnId => {
        const btn = document.getElementById(btnId);
        if (btn) {
            btn.disabled = count === 0;
        }
    });
}


function onSeatClick(seatElement) {
    if (selectModeEnabled) {
        const checkbox = seatElement.querySelector('.seat-checkbox');
        if (checkbox) {
            checkbox.checked = !checkbox.checked;
            updateSelectedCount();
        }
    } else {
        changeSeatTypeAndSave(seatElement);
    }
}

async function changeSeatTypeAndSave(seatElement) {
    if (isSaving) {
        showToast('⏳ Đang lưu...', 'warning');
        return;
    }

    const seatId = seatElement.getAttribute('data-seat-id');
    if (!seatId) {
        showToast('❌ Không thể xác định ghế', 'error');
        return;
    }

    const seatTypeSelector = document.getElementById('seatTypeSelector');
    let targetSeatTypeId = null;

    switch (editMode) {
        case 'normal':
            targetSeatTypeId = Array.from(seatTypeSelector.options).find(opt =>
                opt.getAttribute('data-name')?.toUpperCase() === 'NORMAL'
            )?.value;
            break;
        case 'vip':
            targetSeatTypeId = Array.from(seatTypeSelector.options).find(opt =>
                opt.getAttribute('data-name')?.toUpperCase() === 'VIP'
            )?.value;
            break;
        case 'couple':
            await createCoupleSeatAndSave(seatElement);
            return;
        case 'broken':
            const currentActive = seatElement.getAttribute('data-is-active') === 'true';
            const isActive = !currentActive;
            const hasPairId = seatElement.getAttribute('data-pair-id');

            if (hasPairId) {
                // If it's a couple seat, unpair by setting to NORMAL
                targetSeatTypeId = Array.from(seatTypeSelector.options).find(opt =>
                    opt.getAttribute('data-name')?.toUpperCase() === 'NORMAL'
                )?.value;
            } else {
                // Keep current type for non-couple seats
                targetSeatTypeId = seatElement.getAttribute('data-seat-type-id');
            }

            const result = await updateSeatTypeAPI(seatId, targetSeatTypeId, isActive);

            if (result.success) {
                showToast(isActive ? '✅ Ghế hoạt động' : '⚠️ Ghế đã hỏng', 'success');
            }
            return;
    }

    if (!targetSeatTypeId) {
        showToast('❌ Không tìm thấy loại ghế', 'error');
        return;
    }

    // ✅ FIX: Let API handle the UI update via affectedSeats response
    const result = await updateSeatTypeAPI(seatId, targetSeatTypeId, true);

    if (result.success) {
        const seatType = Array.from(seatTypeSelector.options).find(opt => opt.value === targetSeatTypeId);
        const seatTypeName = seatType?.getAttribute('data-name') || 'NORMAL';

        // Check if this was a couple seat operation
        const wasCoupleOperation = result.data?.affectedSeats?.length > 1;
    }
}

// ===================================
// UPDATE SEAT TYPE API - WITH PROPER UI REFRESH
// ===================================
async function updateSeatTypeAPI(seatId, seatTypeId, isActive) {
    if (isSaving) {
        return;
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

        // ✅ FIX: Process affected seats from API response
        if (data.affectedSeats && data.affectedSeats.length > 0) {
            console.log(`✅ UpdateSeatType: Updating ${data.affectedSeats.length} seats in UI`);

            data.affectedSeats.forEach(seat => {
                const seatElement = document.querySelector(`[data-seat-id="${seat.seatId}"]`);
                if (seatElement) {
                    // Update all data attributes
                    seatElement.setAttribute('data-seat-type-id', seat.seatTypeId);
                    seatElement.setAttribute('data-seat-type-name', seat.seatTypeName);
                    seatElement.setAttribute('data-is-active', seat.isActive.toString());
                    seatElement.setAttribute('data-pair-id', seat.pairId || '');

                    // Update visual appearance
                    updateSeatVisual(seatElement, seat.seatTypeId, seat.seatTypeName);

                    // Handle active/inactive state
                    if (seat.isActive) {
                        seatElement.classList.remove('inactive');
                        const brokenIcon = seatElement.querySelector('.broken-icon');
                        if (brokenIcon) brokenIcon.remove();
                    } else {
                        seatElement.classList.add('inactive');
                        if (!seatElement.querySelector('.broken-icon')) {
                            const icon = document.createElement('span');
                            icon.className = 'broken-icon';
                            icon.textContent = '✗';
                            seatElement.insertBefore(icon, seatElement.querySelector('.seat-label-small'));
                        }
                    }
                }
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
// CREATE COUPLE SEAT - SIMPLIFIED WITH API HANDLING
// ===================================
async function createCoupleSeatAndSave(leftSeatElement) {
    const seatId = leftSeatElement.getAttribute('data-seat-id');
    const rowNumber = parseInt(leftSeatElement.getAttribute('data-row-number'));
    const columnIndex = parseInt(leftSeatElement.getAttribute('data-column-index'));

    let adjacentSeatElement = document.querySelector(
        `[data-row-number="${rowNumber}"][data-column-index="${columnIndex + 1}"]`
    );
    if (!adjacentSeatElement) {
        adjacentSeatElement = document.querySelector(
            `[data-row-number="${rowNumber}"][data-column-index="${columnIndex - 1}"]`
        );
    }

    if (!adjacentSeatElement) {
        showToast('❌ Không tìm thấy ghế bên cạnh', 'error');
        return;
    }

    const coupleType = Array.from(document.getElementById('seatTypeSelector').options).find(opt =>
        opt.getAttribute('data-name')?.toUpperCase().includes('COUPLE')
    );

    if (!coupleType) {
        showToast('❌ Không tìm thấy loại ghế ĐÔI', 'error');
        return;
    }

    const result = await updateSeatTypeAPI(seatId, coupleType.value, true);

}

async function unpairCoupleAndSave(seatElement) {
    const pairId = seatElement.getAttribute('data-pair-id');
    if (!pairId) return;

    const pairedSeats = document.querySelectorAll(`[data-pair-id="${pairId}"]`);
    if (pairedSeats.length !== 2) return;

    const seatTypeSelector = document.getElementById('seatTypeSelector');
    const normalType = Array.from(seatTypeSelector.options).find(opt =>
        opt.getAttribute('data-name')?.toUpperCase() === 'NORMAL'
    );

    if (normalType) {
        for (const seat of pairedSeats) {
            const seatId = seat.getAttribute('data-seat-id');
            await updateSeatTypeAPI(seatId, normalType.value, true);

            // ✅ FIX: Clear pair-id and refresh UI
            seat.setAttribute('data-pair-id', '');
            updateSeatVisual(seat, normalType.value, 'NORMAL');
        }
    }

}
function updateSeatVisual(seatElement, typeId, typeName) {
    seatElement.setAttribute('data-seat-type-id', typeId);
    seatElement.setAttribute('data-seat-type-name', typeName);

    // Remove all seat type classes
    seatElement.classList.remove('seat-type-normal', 'seat-type-vip', 'seat-type-couple');

    // Add new seat type class
    const typeClass = 'seat-type-' + (typeName?.toLowerCase() || 'normal');
    seatElement.classList.add(typeClass);

    // Handle badge
    let badge = seatElement.querySelector('.seat-badge');

    if (typeName?.toUpperCase() === 'VIP') {
        if (!badge) {
            badge = document.createElement('span');
            badge.className = 'seat-badge';
            seatElement.insertBefore(badge, seatElement.firstChild);
        }
        badge.textContent = 'V';
    } else if (typeName?.toUpperCase().includes('COUPLE')) {
        if (!badge) {
            badge = document.createElement('span');
            badge.className = 'seat-badge';
            seatElement.insertBefore(badge, seatElement.firstChild);
        }
        badge.textContent = 'CP';
    } else {
        // Remove badge for normal seats
        if (badge) badge.remove();
    }

    // ✅ FIX: Force browser repaint to ensure changes are visible
    seatElement.style.opacity = '0.99';
    setTimeout(() => {
        seatElement.style.opacity = '';
    }, 0);
}

// ===================================
// ADD ROW AT END
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
                     data-pair-id="${seat.pairId || ''}" 
                     onclick="onSeatClick(this)">
                    ${seat.seatTypeId === 'ST002' ? '<span class="seat-badge">V</span>' : ''}
                    ${seat.seatTypeName?.toUpperCase().includes('COUPLE') ? '<span class="seat-badge">CP</span>' : ''}
                    ${!seat.isActive ? '<span class="broken-icon">✗</span>' : ''}
                    <div class="seat-label-small">${seat.label}</div>
                </div>
            `).join('');

            const newRowHTML = `
                <div class="seat-row" data-row-number="${newRowNumber}">
                    <div class="row-label" data-row-index="${newRowNumber}">
                        <span class="row-label-text">${newRowLabel}</span>
                        <button class="btn btn-danger btn-sm delete-btn" title="Xóa hàng này">
                            <i class="fas fa-times"></i>
                        </button>
                    </div>
                    <div class="seats-in-row">
                        ${seatsHTML}
                    </div>
                    <div class="row-label" data-row-index="${newRowNumber}">
                        <span class="row-label-text">${newRowLabel}</span>
                        <button class="btn btn-danger btn-sm delete-btn" title="Xóa hàng này">
                            <i class="fas fa-times"></i>
                        </button>
                    </div>
                </div>
            `;

            // Insert before the add-row-section
            const addRowSection = document.querySelector('.add-row-section');
            if (addRowSection) {
                addRowSection.insertAdjacentHTML('beforebegin', newRowHTML);
            } else {
                // Fallback: append to seats-grid-container
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

            // Add column number header
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

            // Add seats to each row
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
// DELETE ROW - COMPLETE IMPLEMENTATION (uses custom modal)
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

            // Remove row from UI
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

            // Update affected seats (unpaired couple seats)
            if (data.affectedSeats && data.affectedSeats.length > 0) {
                console.log(`✅ DeleteColumn: Updating ${data.affectedSeats.length} unpaired seats in UI`);

                data.affectedSeats.forEach(seat => {
                    const seatElement = document.querySelector(`[data-seat-id="${seat.seatId}"]`);
                    if (seatElement) {
                        seatElement.setAttribute('data-seat-type-id', seat.seatTypeId);
                        seatElement.setAttribute('data-seat-type-name', seat.seatTypeName);
                        seatElement.setAttribute('data-pair-id', seat.pairId || '');
                        seatElement.setAttribute('data-column-index', seat.columnIndex);

                        updateSeatVisual(seatElement, seat.seatTypeId, seat.seatTypeName);
                    }
                });
            }

            // Update column headers
            const columnNumbersRow = document.querySelector('.column-numbers');
            if (columnNumbersRow && data.newNumOfColumns) {
                // Remove all existing column numbers except the add button
                const addBtn = columnNumbersRow.querySelector('.add-column-btn');
                columnNumbersRow.innerHTML = '';

                // Add back the row label space
                const rowLabelSpace = document.createElement('div');
                rowLabelSpace.className = 'row-label-space';
                columnNumbersRow.appendChild(rowLabelSpace);

                // Add new column numbers 1 to newNumOfColumns
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

                // Add back the add button at the end
                columnNumbersRow.appendChild(addBtn);
            }

            // Remove seats from the deleted column
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
