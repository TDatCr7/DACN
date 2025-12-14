using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CinemaS.Models;
using CinemaS.Models.ViewModels;
using CinemaS.Models.DTOs;

namespace CinemaS.Controllers
{
    [Authorize(Roles = "Admin")]
    public class SeatsController : Controller
    {
        private readonly CinemaContext _context;

        public SeatsController(CinemaContext context)
        {
            _context = context;
        }

        // ===========================
        // GET: Seats/Index - Dynamic Seat Layout Editor
        // ===========================
        public async Task<IActionResult> Index(string? cinemaTheaterId)
        {
            // Load all theaters for dropdown
            ViewBag.CinemaTheaters = await _context.CinemaTheaters
                .Where(ct => ct.Status == 1)
                .OrderBy(ct => ct.Name)
                .ToListAsync();

            // If no theater selected, pick first one
            if (string.IsNullOrEmpty(cinemaTheaterId))
            {
                var firstTheater = await _context.CinemaTheaters
                    .Where(ct => ct.Status == 1)
                    .OrderBy(ct => ct.Name)
                    .FirstOrDefaultAsync();

                if (firstTheater != null)
                    cinemaTheaterId = firstTheater.CinemaTheaterId;
            }

            if (string.IsNullOrEmpty(cinemaTheaterId))
            {
                // No theaters available
                return View(new SeatLayoutEditorVM());
            }

            // Load theater info
            var theater = await _context.CinemaTheaters
                .FirstOrDefaultAsync(ct => ct.CinemaTheaterId == cinemaTheaterId);

            if (theater == null)
                return NotFound();

            var cinemaType = await _context.CinemaTypes
                .FirstOrDefaultAsync(ct => ct.CinemaTypeId == theater.CinemaTypeId);

            // ✅ Load show times for this theater
            var showTimes = await _context.ShowTimes
                .Where(st => st.CinemaTheaterId == cinemaTheaterId)
                .OrderByDescending(st => st.ShowDate)
                .ThenBy(st => st.StartTime)
                .ToListAsync();

            var showTimesList = new List<ShowTimeVM>();
            foreach (var st in showTimes)
            {
                var movie = await _context.Movies.FirstOrDefaultAsync(m => m.MoviesId == st.MoviesId);
                
                // ✅ FIX: Không đếm ghế IsDeleted và IsAisle
                var totalSeats = await _context.Seats.CountAsync(s => s.CinemaTheaterId == st.CinemaTheaterId 
                                                                   && !s.IsDeleted 
                                                                   && !s.IsAisle);
                var bookedSeats = await _context.Tickets.CountAsync(t => t.ShowTimeId == st.ShowTimeId && t.Status == 2);

                showTimesList.Add(new ShowTimeVM
                {
                    ShowTimeId = st.ShowTimeId,
                    MoviesId = st.MoviesId,
                    MovieTitle = movie?.Title,
                    ShowDate = st.ShowDate,
                    StartTime = st.StartTime,
                    EndTime = st.EndTime,
                    TotalSeats = totalSeats,
                    AvailableSeats = totalSeats - bookedSeats,
                    OriginPrice = st.OriginPrice
                });
            }

            ViewBag.ShowTimes = showTimesList;

            // Load all seats for this theater
            var seats = await _context.Seats
                .Where(s => s.CinemaTheaterId == cinemaTheaterId)
                .OrderBy(s => s.RowIndex)
                .ThenBy(s => s.ColumnIndex)
                .ToListAsync();

            var seatTypes = await _context.SeatTypes.ToListAsync();

            // Build ViewModel
            var vm = new SeatLayoutEditorVM
            {
                CinemaTheaterId = cinemaTheaterId,
                TheaterName = theater.Name,
                CinemaTypeName = cinemaType?.Name,
                NumOfRows = theater.NumOfRows ?? 6,
                NumOfColumns = theater.NumOfColumns ?? 14,
                SeatTypeOptions = seatTypes.Select(st => new SeatTypeOption
                {
                    SeatTypeId = st.SeatTypeId,
                    Name = st.Name ?? "Unknown",
                    Price = st.Price
                }).ToList()
            };

            // Organize seats into rows
            var rowGroups = seats.GroupBy(s => s.RowIndex).OrderBy(g => g.Key);
            int rowNum = 0;

            foreach (var rowGroup in rowGroups)
            {
                var rowVM = new SeatRowVM
                {
                    RowLabel = rowGroup.Key ?? "?",
                    RowNumber = rowNum++,
                    Seats = new List<SeatCellVM>()
                };

                foreach (var seat in rowGroup.OrderBy(s => s.ColumnIndex))
                {
                    var st = seatTypes.FirstOrDefault(x => x.SeatTypeId == seat.SeatTypeId);

                    var cellVM = new SeatCellVM
                    {
                        SeatId = seat.SeatId,
                        SeatTypeId = seat.SeatTypeId,
                        SeatTypeName = st?.Name,
                        Label = seat.Label,
                        RowNumber = rowNum - 1,
                        ColumnIndex = seat.ColumnIndex ?? 1,
                        IsActive = seat.IsActive,
                        IsDeleted = seat.IsDeleted,
                        IsAisle = seat.IsAisle, // ✅ Add IsAisle from database
                        PairId = seat.PairId
                    };

                    // Determine if left/right of couple pair
                    if (!string.IsNullOrEmpty(seat.PairId))
                    {
                        var pairSeats = seats.Where(s => s.PairId == seat.PairId).OrderBy(s => s.ColumnIndex).ToList();
                        if (pairSeats.Count == 2)
                        {
                            cellVM.IsLeftOfPair = pairSeats[0].SeatId == seat.SeatId;
                            cellVM.IsRightOfPair = pairSeats[1].SeatId == seat.SeatId;
                        }
                    }

                    rowVM.Seats.Add(cellVM);
                }

                vm.Rows.Add(rowVM);
            }

            return View(vm);
        }

        // ===========================
        // POST: Seats/SaveLayout - Save entire layout (SECOND SAVE)
        // ===========================
        [HttpPost]
        public async Task<IActionResult> SaveLayout([FromBody] SaveSeatLayoutRequest? request)
        {
            // Enhanced validation with detailed logging
            if (request == null)
            {
                Console.WriteLine("❌ SaveLayout: Request is null");
                return Json(new { success = false, message = "Dữ liệu yêu cầu null" });
            }

            if (string.IsNullOrEmpty(request.CinemaTheaterId))
            {
                Console.WriteLine($"❌ SaveLayout: CinemaTheaterId is empty. Request data: Rows={request.NumOfRows}, Cols={request.NumOfColumns}, Seats={request.Seats?.Count ?? 0}");
                return Json(new { success = false, message = "Thiếu ID phòng chiếu" });
            }

            if (request.Seats == null || !request.Seats.Any())
            {
                Console.WriteLine($"❌ SaveLayout: No seats provided for theater {request.CinemaTheaterId}");
                return Json(new { success = false, message = "Không có dữ liệu ghế" });
            }

            Console.WriteLine($"✅ SaveLayout: Processing theater {request.CinemaTheaterId} with {request.Seats.Count} seats");

            try
            {
                var theater = await _context.CinemaTheaters.FindAsync(request.CinemaTheaterId);
                if (theater == null)
                {
                    Console.WriteLine($"❌ Theater not found: {request.CinemaTheaterId}");
                    return Json(new { success = false, message = "Không tìm thấy phòng chiếu" });
                }

                // Update theater dimensions
                theater.NumOfRows = request.NumOfRows;
                theater.NumOfColumns = request.NumOfColumns;
                _context.Update(theater);

                // Load existing seats
                var existingSeats = await _context.Seats
                    .Where(s => s.CinemaTheaterId == request.CinemaTheaterId)
                    .ToListAsync();

                var existingSeatIds = new HashSet<string>(existingSeats.Select(s => s.SeatId));
                var incomingSeatIds = new HashSet<string>(request.Seats.Where(s => !string.IsNullOrEmpty(s.SeatId)).Select(s => s.SeatId!));

                // Find seats to delete (not in incoming data)
                var seatsToDelete = existingSeats.Where(s => !incomingSeatIds.Contains(s.SeatId)).ToList();

                Console.WriteLine($"📊 Existing: {existingSeats.Count}, Incoming: {request.Seats.Count}, ToDelete: {seatsToDelete.Count}");

                // Check if any seats to delete are booked
                var bookedSeatIds = await _context.Tickets
                    .Where(t => seatsToDelete.Select(s => s.SeatId).Contains(t.SeatId) && t.Status == 2)
                    .Select(t => t.SeatId)
                    .ToListAsync();

                if (bookedSeatIds.Any())
                {
                    Console.WriteLine($"❌ Cannot delete booked seats: {string.Join(", ", bookedSeatIds)}");
                    return Json(new { success = false, message = $"Không thể xóa ghế đã được đặt: {string.Join(", ", bookedSeatIds)}" });
                }

                // Soft delete seats
                foreach (var seat in seatsToDelete)
                {
                    _context.Seats.Remove(seat); // Actually remove instead of soft delete
                }

                // Generate row labels dynamically
                var rowLabels = GenerateRowLabels(request.NumOfRows);

                int newSeats = 0, updatedSeats = 0;

                // Process each incoming seat
                foreach (var seatDTO in request.Seats)
                {
                    // Validate seat data
                    if (string.IsNullOrEmpty(seatDTO.SeatTypeId))
                    {
                        Console.WriteLine($"⚠️ Skipping seat with empty SeatTypeId at row {seatDTO.RowNumber}, col {seatDTO.ColumnIndex}");
                        continue;
                    }

                    if (seatDTO.RowNumber < 0 || seatDTO.RowNumber >= rowLabels.Count)
                    {
                        Console.WriteLine($"⚠️ Invalid RowNumber {seatDTO.RowNumber} for seat");
                        continue;
                    }

                    string rowLabel = rowLabels[seatDTO.RowNumber];

                    if (string.IsNullOrEmpty(seatDTO.SeatId))
                    {
                        // NEW SEAT
                        var newSeat = new Seats
                        {
                            SeatId = await GenerateNewSeatIdAsync(),
                            SeatTypeId = seatDTO.SeatTypeId,
                            CinemaTheaterId = request.CinemaTheaterId,
                            RowIndex = rowLabel,
                            ColumnIndex = seatDTO.ColumnIndex,
                            Label = seatDTO.Label ?? $"{rowLabel}{seatDTO.ColumnIndex}",
                            IsActive = seatDTO.IsActive,
                            PairId = string.IsNullOrEmpty(seatDTO.PairId) ? null : seatDTO.PairId
                        };
                        _context.Seats.Add(newSeat);
                        newSeats++;
                    }
                    else
                    {
                        // UPDATE EXISTING SEAT
                        var existingSeat = existingSeats.FirstOrDefault(s => s.SeatId == seatDTO.SeatId);
                        if (existingSeat != null)
                        {
                            existingSeat.SeatTypeId = seatDTO.SeatTypeId;
                            existingSeat.RowIndex = rowLabel;
                            existingSeat.ColumnIndex = seatDTO.ColumnIndex;
                            existingSeat.Label = seatDTO.Label ?? $"{rowLabel}{seatDTO.ColumnIndex}";
                            existingSeat.IsActive = seatDTO.IsActive;
                            existingSeat.PairId = string.IsNullOrEmpty(seatDTO.PairId) ? null : seatDTO.PairId;
                            _context.Update(existingSeat);
                            updatedSeats++;
                        }
                    }
                }

                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ SaveLayout SUCCESS: Added {newSeats}, Updated {updatedSeats}, Deleted {seatsToDelete.Count}");
                return Json(new { success = true, message = "Cập nhật bố cục thành công!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ SaveLayout ERROR: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // ===========================
        // POST: Seats/UpdateSeatType - Update single/couple seat type instantly
        // ===========================
        // ✅ FIX: UPDATE SEAT TYPE API - Return affected seats for UI refresh
        [HttpPost]
        public async Task<IActionResult> UpdateSeatType([FromBody] UpdateSeatTypeRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.SeatId) || string.IsNullOrEmpty(request.SeatTypeId))
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

                var seat = await _context.Seats.FindAsync(request.SeatId);
                if (seat == null)
                    return Json(new { success = false, message = "Không tìm thấy ghế" });

                // Check if seat is booked
                var isBooked = await _context.Tickets.AnyAsync(t => t.SeatId == request.SeatId && t.Status == 2);
                if (isBooked)
                    return Json(new { success = false, message = "Ghế đã được đặt, không thể thay đổi" });

                var seatType = await _context.SeatTypes.FindAsync(request.SeatTypeId);
                if (seatType == null)
                    return Json(new { success = false, message = "Loại ghế không tồn tại" });

                var affectedSeats = new List<object>();

                // Handle couple seat logic
                if (string.Equals(seatType.Name, "COUPLE", StringComparison.OrdinalIgnoreCase))
                {
                    // Check if seat is already part of a couple
                    if (!string.IsNullOrEmpty(seat.PairId))
                    {
                        return Json(new { success = false, message = "Ghế này đã là ghế đôi, không thể tạo thêm" });
                    }

                    Seats? adjacentSeat = null;

                    // Try to find adjacent seat to the right
                    var rightSeat = await _context.Seats
                        .FirstOrDefaultAsync(s => s.CinemaTheaterId == seat.CinemaTheaterId
                                                && s.RowIndex == seat.RowIndex
                                                && s.ColumnIndex == seat.ColumnIndex + 1
                                                && !s.IsDeleted);

                    if (rightSeat != null && string.IsNullOrEmpty(rightSeat.PairId))
                    {
                        var rightBooked = await _context.Tickets.AnyAsync(t => t.SeatId == rightSeat.SeatId && t.Status == 2);
                        if (!rightBooked && rightSeat.IsActive)
                        {
                            adjacentSeat = rightSeat;
                        }
                    }

                    // If right not available, try left
                    if (adjacentSeat == null)
                    {
                        var leftSeat = await _context.Seats
                            .FirstOrDefaultAsync(s => s.CinemaTheaterId == seat.CinemaTheaterId
                                                    && s.RowIndex == seat.RowIndex
                                                    && s.ColumnIndex == seat.ColumnIndex - 1
                                                    && !s.IsDeleted);

                        if (leftSeat != null && string.IsNullOrEmpty(leftSeat.PairId))
                        {
                            var leftBooked = await _context.Tickets.AnyAsync(t => t.SeatId == leftSeat.SeatId && t.Status == 2);
                            if (!leftBooked && leftSeat.IsActive)
                            {
                                adjacentSeat = leftSeat;
                            }
                        }
                    }

                    if (adjacentSeat == null)
                    {
                        return Json(new { success = false, message = "Không có ghế bên cạnh hợp lệ để tạo ghế đôi" });
                    }

                    // ✅ FIX: If either seat was part of another couple, unpair them first (though we checked above, but to be safe)
                    if (!string.IsNullOrEmpty(seat.PairId) || !string.IsNullOrEmpty(adjacentSeat.PairId))
                    {
                        var oldPairIds = new List<string>();
                        if (!string.IsNullOrEmpty(seat.PairId)) oldPairIds.Add(seat.PairId);
                        if (!string.IsNullOrEmpty(adjacentSeat.PairId) && adjacentSeat.PairId != seat.PairId)
                            oldPairIds.Add(adjacentSeat.PairId);

                        foreach (var oldPairId in oldPairIds)
                        {
                            var oldPairSeats = await _context.Seats
                                .Where(s => s.PairId == oldPairId)
                                .ToListAsync();

                            foreach (var ops in oldPairSeats)
                            {
                                ops.PairId = null;
                                _context.Update(ops);
                            }
                        }
                    }

                    // Create new pair
                    var pairId = await GenerateNewPairIdAsync();
                    seat.PairId = pairId;
                    seat.SeatTypeId = request.SeatTypeId;
                    seat.IsActive = request.IsActive;

                    adjacentSeat.PairId = pairId;
                    adjacentSeat.SeatTypeId = request.SeatTypeId;
                    adjacentSeat.IsActive = request.IsActive;

                    _context.Update(seat);
                    _context.Update(adjacentSeat);

                    affectedSeats.Add(new
                    {
                        seatId = seat.SeatId,
                        seatTypeId = seat.SeatTypeId,
                        seatTypeName = seatType.Name,
                        isActive = seat.IsActive,
                        isDeleted = seat.IsDeleted,
                        pairId = seat.PairId,
                        label = seat.Label,
                        rowNumber = GetRowNumberFromLabel(seat.RowIndex),
                        columnIndex = seat.ColumnIndex
                    });
                    affectedSeats.Add(new
                    {
                        seatId = adjacentSeat.SeatId,
                        seatTypeId = adjacentSeat.SeatTypeId,
                        seatTypeName = seatType.Name,
                        isActive = adjacentSeat.IsActive,
                        isDeleted = adjacentSeat.IsDeleted,
                        pairId = adjacentSeat.PairId,
                        label = adjacentSeat.Label,
                        rowNumber = GetRowNumberFromLabel(adjacentSeat.RowIndex),
                        columnIndex = seat.ColumnIndex
                    });
                }
                else
                {
                    // Handle broken seat logic for couple seats
                    if (!string.IsNullOrEmpty(seat.PairId) && !request.IsActive)
                    {
                        // If breaking a couple seat, unpair both and set both to inactive NORMAL
                        var pairedSeats = await _context.Seats
                            .Where(s => s.PairId == seat.PairId)
                            .ToListAsync();

                        var normalType = await _context.SeatTypes
                            .AsNoTracking()
                            .FirstOrDefaultAsync(st => st.Name == "NORMAL");

                        if (normalType == null)
                        {
                            return Json(new { success = false, message = "Không tìm thấy loại ghế NORMAL" });
                        }

                        foreach (var ps in pairedSeats)
                        {
                            ps.PairId = null;
                            ps.SeatTypeId = normalType.SeatTypeId;
                            ps.IsActive = false; // Set inactive
                            _context.Update(ps);

                            affectedSeats.Add(new
                            {
                                seatId = ps.SeatId,
                                seatTypeId = ps.SeatTypeId,
                                seatTypeName = normalType.Name,
                                isActive = false,
                                isDeleted = ps.IsDeleted,
                                pairId = (string?)null,
                                label = ps.Label,
                                rowNumber = GetRowNumberFromLabel(ps.RowIndex),
                                columnIndex = ps.ColumnIndex
                            });
                        }
                    }
                    else
                    {
                        // ✅ FIX: Single seat update - If was part of couple, UNPAIR and update BOTH seats to requested type
                        if (!string.IsNullOrEmpty(seat.PairId))
                        {
                            var pairedSeats = await _context.Seats
                                .Where(s => s.PairId == seat.PairId)
                                .ToListAsync();

                            Console.WriteLine($"🔧 Unpairing {pairedSeats.Count} seats from pair {seat.PairId}");

                            // ✅ KEY FIX: Update ALL paired seats to the NEW type (not NORMAL)
                            foreach (var ps in pairedSeats)
                            {
                                ps.PairId = null; // Remove pair
                                ps.SeatTypeId = request.SeatTypeId; // ← Use requested type, not NORMAL!
                                ps.IsActive = request.IsActive;
                                _context.Update(ps);

                                affectedSeats.Add(new
                                {
                                    seatId = ps.SeatId,
                                    seatTypeId = ps.SeatTypeId,
                                    seatTypeName = seatType.Name, // ← Use requested type name
                                    isActive = ps.IsActive,
                                    isDeleted = ps.IsDeleted,
                                    pairId = (string?)null,
                                    label = ps.Label,
                                    rowNumber = GetRowNumberFromLabel(ps.RowIndex),
                                    columnIndex = ps.ColumnIndex
                                });

                                Console.WriteLine($"  ✅ Updated {ps.SeatId}: Type={ps.SeatTypeId}, PairId=null");
                            }
                        }
                        else
                        {
                            // Simple single seat update (not previously paired)
                            seat.SeatTypeId = request.SeatTypeId;
                            seat.IsActive = request.IsActive;
                            seat.PairId = null;
                            _context.Update(seat);

                            affectedSeats.Add(new
                            {
                                seatId = seat.SeatId,
                                seatTypeId = seat.SeatTypeId,
                                seatTypeName = seatType.Name,
                                isActive = seat.IsActive,
                                isDeleted = seat.IsDeleted,
                                pairId = (string?)null,
                                label = seat.Label,
                                rowNumber = GetRowNumberFromLabel(seat.RowIndex),
                                columnIndex = seat.ColumnIndex
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ UpdateSeatType SUCCESS: {affectedSeats.Count} seats affected");

                return Json(new
                {
                    success = true,
                    message = "Cập nhật thành công",
                    affectedSeats = affectedSeats
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ UpdateSeatType ERROR: {ex.Message}\n{ex.StackTrace}");
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // ===========================
        // POST: Seats/BulkUpdateSeatType - Update multiple seats at once (Row/Column quick select)
        // ===========================
        [HttpPost]
        public async Task<IActionResult> BulkUpdateSeatType([FromBody] BulkUpdateSeatTypeRequest request)
        {
            try
            {
                if (request.SeatIds == null || !request.SeatIds.Any() || string.IsNullOrEmpty(request.SeatTypeId))
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

                var seatType = await _context.SeatTypes.FindAsync(request.SeatTypeId);
                if (seatType == null)
                    return Json(new { success = false, message = "Loại ghế không tồn tại" });

                var seats = await _context.Seats
                    .Where(s => request.SeatIds.Contains(s.SeatId) && !s.IsDeleted)
                    .ToListAsync();

                if (!seats.Any())
                    return Json(new { success = false, message = "Không tìm thấy ghế hợp lệ" });

                // Check for booked seats
                var bookedSeats = await _context.Tickets
                    .Where(t => request.SeatIds.Contains(t.SeatId) && t.Status == 2)
                    .Select(t => t.SeatId)
                    .ToListAsync();

                if (bookedSeats.Any())
                    return Json(new { success = false, message = $"Có {bookedSeats.Count} ghế đã được đặt, không thể thay đổi" });

                var affectedSeats = new List<object>();

                // Handle couple seats: unpair them first if changing to non-couple type
                if (!string.Equals(seatType.Name, "COUPLE", StringComparison.OrdinalIgnoreCase))
                {
                    var pairedSeatIds = seats.Where(s => !string.IsNullOrEmpty(s.PairId)).Select(s => s.PairId).Distinct().ToList();
                    foreach (var pairId in pairedSeatIds)
                    {
                        var pairSeats = await _context.Seats.Where(s => s.PairId == pairId).ToListAsync();
                        foreach (var ps in pairSeats)
                        {
                            ps.PairId = null;
                            _context.Update(ps);
                        }
                    }
                }

                // Update all seats
                foreach (var seat in seats)
                {
                    seat.SeatTypeId = request.SeatTypeId;
                    seat.IsActive = true; // Reset to active when bulk updating
                    seat.PairId = null; // Clear pair when bulk updating
                    _context.Update(seat);

                    affectedSeats.Add(new
                    {
                        seatId = seat.SeatId,
                        seatTypeId = seat.SeatId,
                        seatTypeName = seatType.Name,
                        isActive = seat.IsActive,
                        isDeleted = seat.IsDeleted,
                        pairId = seat.PairId,
                        label = seat.Label,
                        rowNumber = GetRowNumberFromLabel(seat.RowIndex),
                        columnIndex = seat.ColumnIndex
                    });
                }

                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ BulkUpdateSeatType SUCCESS: {affectedSeats.Count} seats updated");

                return Json(new
                {
                    success = true,
                    message = $"Đã cập nhật {affectedSeats.Count} ghế",
                    affectedSeats = affectedSeats
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ BulkUpdateSeatType ERROR: {ex.Message}");
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // ===========================
        // POST: Seats/SoftDeleteSeat - Soft delete seat (set IsDeleted = true)
        // ===========================
        [HttpPost]
        public async Task<IActionResult> SoftDeleteSeat([FromBody] SoftDeleteSeatRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.SeatId))
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

                var seat = await _context.Seats.FindAsync(request.SeatId);
                if (seat == null)
                    return Json(new { success = false, message = "Không tìm thấy ghế" });

                // Check if booked
                var isBooked = await _context.Tickets.AnyAsync(t => t.SeatId == request.SeatId && t.Status == 2);
                if (isBooked)
                    return Json(new { success = false, message = "Ghế đã được đặt, không thể bỏ" });

                var affectedSeats = new List<object>();

                // If part of couple, unpair both
                if (!string.IsNullOrEmpty(seat.PairId))
                {
                    var pairSeats = await _context.Seats.Where(s => s.PairId == seat.PairId).ToListAsync();
                    var normalType = await _context.SeatTypes.AsNoTracking().FirstOrDefaultAsync(st => st.Name == "NORMAL");

                    if (normalType == null)
                        return Json(new { success = false, message = "Không tìm thấy loại ghế NORMAL" });

                    foreach (var ps in pairSeats)
                    {
                        ps.PairId = null;
                        ps.SeatTypeId = normalType.SeatTypeId;
                        ps.IsDeleted = true;
                        // ✅ KHÔNG thay đổi IsActive - giữ nguyên trạng thái hoạt động/hỏng
                        _context.Update(ps);

                        affectedSeats.Add(new
                        {
                            seatId = ps.SeatId,
                            seatTypeId = ps.SeatTypeId,
                            seatTypeName = normalType.Name,
                            isActive = ps.IsActive,
                            isDeleted = ps.IsDeleted,
                            pairId = ps.PairId,
                            label = ps.Label,
                            rowNumber = GetRowNumberFromLabel(ps.RowIndex),
                            columnIndex = ps.ColumnIndex
                        });
                    }
                }
                else
                {
                    seat.IsDeleted = true;
                    // ✅ KHÔNG thay đổi IsActive - giữ nguyên trạng thái hoạt động/hỏng
                    _context.Update(seat);

                    var seatType = await _context.SeatTypes.FindAsync(seat.SeatTypeId);
                    affectedSeats.Add(new
                    {
                        seatId = seat.SeatId,
                        seatTypeId = seat.SeatTypeId,
                        seatTypeName = seatType?.Name ?? "NORMAL",
                        isActive = seat.IsActive,
                        isDeleted = seat.IsDeleted,
                        pairId = seat.PairId,
                        label = seat.Label,
                        rowNumber = GetRowNumberFromLabel(seat.RowIndex),
                        columnIndex = seat.ColumnIndex
                    });
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Ghế đã được bỏ khỏi bố cục",
                    affectedSeats = affectedSeats
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ SoftDeleteSeat ERROR: {ex.Message}");
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // ===========================
        // POST: Seats/RestoreSeat - Restore soft-deleted seat
        // ===========================
        [HttpPost]
        public async Task<IActionResult> RestoreSeat([FromBody] RestoreSeatRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.SeatId))
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

                var seat = await _context.Seats.FindAsync(request.SeatId);
                if (seat == null)
                    return Json(new { success = false, message = "Không tìm thấy ghế" });

                seat.IsDeleted = false;
                seat.IsActive = true;
                _context.Update(seat);
                await _context.SaveChangesAsync();

                var seatType = await _context.SeatTypes.FindAsync(seat.SeatTypeId);

                return Json(new
                {
                    success = true,
                    message = "Ghế đã được khôi phục",
                    seat = new
                    {
                        seatId = seat.SeatId,
                        seatTypeId = seat.SeatTypeId,
                        seatTypeName = seatType?.Name ?? "NORMAL",
                        isActive = seat.IsActive,
                        isDeleted = seat.IsDeleted,
                        pairId = seat.PairId,
                        label = seat.Label,
                        rowNumber = GetRowNumberFromLabel(seat.RowIndex),
                        columnIndex = seat.ColumnIndex
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ RestoreSeat ERROR: {ex.Message}");
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // ===========================
        // POST: Seats/AddRow - Add new row at the end
        // ===========================
        [HttpPost]
        public async Task<IActionResult> AddRow([FromBody] AddRowRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.CinemaTheaterId))
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

                var theater = await _context.CinemaTheaters.FindAsync(request.CinemaTheaterId);
                if (theater == null)
                    return Json(new { success = false, message = "Không tìm thấy phòng chiếu" });

                // ✅ RULE 5: Block AddRow if ANY seat has IsAisle = true
                var hasAisleSeat = await _context.Seats
                    .AnyAsync(s => s.CinemaTheaterId == request.CinemaTheaterId && s.IsAisle);

                if (hasAisleSeat)
                {
                    Console.WriteLine($"❌ AddRow BLOCKED: Theater {request.CinemaTheaterId} contains aisle seats");
                    return Json(new { success = false, message = "Vui lòng bỏ lối đi trước khi thêm hàng hoặc cột" });
                }

                // Get current number of rows and columns
                var currentRows = theater.NumOfRows ?? 0;
                var currentColumns = theater.NumOfColumns ?? 14;

                // Generate new row label
                var newRowLabel = GenerateRowLabel(currentRows);

                // Get default seat type (NORMAL)
                var normalType = await _context.SeatTypes.FirstOrDefaultAsync(st => st.Name == "NORMAL");
                if (normalType == null)
                    return Json(new { success = false, message = "Không tìm thấy loại ghế NORMAL" });

                // Generate new seat IDs
                var newSeatIds = await GenerateNewSeatIdsAsync(currentColumns);

                // Create new seats for this row
                var newSeats = new List<Seats>();
                for (int col = 1; col <= currentColumns; col++)
                {
                    var newSeat = new Seats
                    {
                        SeatId = newSeatIds[col - 1],
                        SeatTypeId = normalType.SeatTypeId,
                        CinemaTheaterId = request.CinemaTheaterId,
                        RowIndex = newRowLabel,
                        ColumnIndex = col,
                        Label = $"{newRowLabel}{col}",
                        IsActive = true,
                        IsDeleted = false,
                        PairId = null
                    };
                    newSeats.Add(newSeat);
                }

                _context.Seats.AddRange(newSeats);

                // Update theater's number of rows
                theater.NumOfRows = currentRows + 1;
                _context.Update(theater);

                await _context.SaveChangesAsync();

                // Return new row data for UI
                var seatsData = newSeats.Select(s => new
                {
                    seatId = s.SeatId,
                    seatTypeId = s.SeatTypeId,
                    seatTypeName = normalType.Name,
                    rowNumber = currentRows,
                    columnIndex = s.ColumnIndex,
                    label = s.Label,
                    isActive = s.IsActive,
                    isDeleted = s.IsDeleted,
                    pairId = s.PairId
                }).ToList();

                return Json(new
                {
                    success = true,
                    message = "Đã thêm hàng mới",
                    newRow = new
                    {
                        rowNumber = currentRows,
                        rowLabel = newRowLabel,
                        seats = seatsData
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ AddRow ERROR: {ex.Message}");
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // ===========================
        // POST: Seats/AddColumn - Add new column at the end
        // ===========================
        [HttpPost]
        public async Task<IActionResult> AddColumn([FromBody] AddColumnRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.CinemaTheaterId))
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

                var theater = await _context.CinemaTheaters.FindAsync(request.CinemaTheaterId);
                if (theater == null)
                    return Json(new { success = false, message = "Không tìm thấy phòng chiếu" });

                // ✅ RULE 5: Block AddColumn if ANY seat has IsAisle = true
                var hasAisleSeat = await _context.Seats
                    .AnyAsync(s => s.CinemaTheaterId == request.CinemaTheaterId && s.IsAisle);

                if (hasAisleSeat)
                {
                    Console.WriteLine($"❌ AddColumn BLOCKED: Theater {request.CinemaTheaterId} contains aisle seats");
                    return Json(new { success = false, message = "Vui lòng bỏ lối đi trước khi thêm hàng hoặc cột" });
                }

                var currentRows = theater.NumOfRows ?? 0;
                var currentColumns = theater.NumOfColumns ?? 14;
                var newColIndex = currentColumns + 1;

                // Get default seat type
                var normalType = await _context.SeatTypes.FirstOrDefaultAsync(st => st.Name == "NORMAL");
                if (normalType == null)
                    return Json(new { success = false, message = "Không tìm thấy loại ghế NORMAL" });

                // Get all row labels
                var rowLabels = GenerateRowLabels(currentRows);

                // Generate new seat IDs
                var newSeatIds = await GenerateNewSeatIdsAsync(currentRows);

                // Create new seats for this column (one per row)
                var newSeats = new List<Seats>();
                for (int rowIdx = 0; rowIdx < currentRows; rowIdx++)
                {
                    var rowLabel = rowLabels[rowIdx];
                    var newSeat = new Seats
                    {
                        SeatId = newSeatIds[rowIdx],
                        SeatTypeId = normalType.SeatTypeId,
                        CinemaTheaterId = request.CinemaTheaterId,
                        RowIndex = rowLabel,
                        ColumnIndex = newColIndex,
                        Label = $"{rowLabel}{newColIndex}",
                        IsActive = true,
                        IsDeleted = false,
                        PairId = null
                    };
                    newSeats.Add(newSeat);
                }

                _context.Seats.AddRange(newSeats);

                // Update theater's number of columns
                theater.NumOfColumns = newColIndex;
                _context.Update(theater);

                await _context.SaveChangesAsync();

                // Return new seats data for UI
                var seatsData = newSeats.Select((s, idx) => new
                {
                    seatId = s.SeatId,
                    seatTypeId = s.SeatTypeId,
                    seatTypeName = normalType.Name,
                    rowNumber = idx,
                    columnIndex = s.ColumnIndex,
                    label = s.Label,
                    isActive = s.IsActive,
                    isDeleted = s.IsDeleted,
                    pairId = s.PairId
                }).ToList();

                return Json(new
                {
                    success = true,
                    message = "Đã thêm cột mới",
                    newColIndex = newColIndex,
                    newSeats = seatsData
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ AddColumn ERROR: {ex.Message}");
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // ===========================
        // POST: Seats/DeleteRow - Delete a row
        // ===========================
        [HttpPost]
        public async Task<IActionResult> DeleteRow([FromBody] DeleteRowRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.CinemaTheaterId) || string.IsNullOrEmpty(request.RowLabel))
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

                var theater = await _context.CinemaTheaters.FindAsync(request.CinemaTheaterId);
                if (theater == null)
                    return Json(new { success = false, message = "Không tìm thấy phòng chiếu" });

                // Get all seats in this row
                var rowSeats = await _context.Seats
                    .Where(s => s.CinemaTheaterId == request.CinemaTheaterId && s.RowIndex == request.RowLabel)
                    .ToListAsync();

                if (!rowSeats.Any())
                    return Json(new { success = false, message = "Không tìm thấy ghế trong hàng này" });

                // Check if any seats are booked
                var seatIds = rowSeats.Select(s => s.SeatId).ToList();
                var bookedCount = await _context.Tickets
                    .Where(t => seatIds.Contains(t.SeatId) && t.Status == 2)
                    .CountAsync();

                if (bookedCount > 0)
                    return Json(new { success = false, message = $"Không thể xóa hàng có {bookedCount} ghế đã được đặt" });

                // Delete all seats in this row
                _context.Seats.RemoveRange(rowSeats);

                // Update theater's number of rows
                if (theater.NumOfRows.HasValue && theater.NumOfRows > 0)
                {
                    theater.NumOfRows = theater.NumOfRows.Value - 1;
                    _context.Update(theater);
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"Đã xóa hàng {request.RowLabel}"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ DeleteRow ERROR: {ex.Message}");
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // ===========================
        // POST: Seats/DeleteColumn - Delete a column
        // ===========================
        [HttpPost]
        public async Task<IActionResult> DeleteColumn([FromBody] DeleteColumnRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.CinemaTheaterId) || request.ColumnIndex <= 0)
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

                var theater = await _context.CinemaTheaters.FindAsync(request.CinemaTheaterId);
                if (theater == null)
                    return Json(new { success = false, message = "Không tìm thấy phòng chiếu" });

                // Get all seats in this column
                var columnSeats = await _context.Seats
                    .Where(s => s.CinemaTheaterId == request.CinemaTheaterId && s.ColumnIndex == request.ColumnIndex)
                    .ToListAsync();

                if (!columnSeats.Any())
                    return Json(new { success = false, message = "Không tìm thấy ghế trong cột này" });

                // Check if any seats are booked
                var seatIds = columnSeats.Select(s => s.SeatId).ToList();
                var bookedCount = await _context.Tickets
                    .Where(t => seatIds.Contains(t.SeatId) && t.Status == 2)
                    .CountAsync();

                if (bookedCount > 0)
                    return Json(new { success = false, message = $"Không thể xóa cột có {bookedCount} ghế đã được đặt" });

                // Get affected seats (couple seats that will be unpaired)
                var affectedSeats = new List<object>();
                var normalType = await _context.SeatTypes.FirstOrDefaultAsync(st => st.Name == "NORMAL");

                foreach (var seat in columnSeats)
                {
                    if (!string.IsNullOrEmpty(seat.PairId))
                    {
                        // Find the other seat in the couple pair
                        var pairedSeats = await _context.Seats.Where(s => s.PairId == seat.PairId && s.SeatId != seat.SeatId).ToListAsync();
                        foreach (var pairedSeat in pairedSeats)
                        {
                            // Unpair the other seat
                            pairedSeat.PairId = null;
                            if (normalType != null)
                                pairedSeat.SeatTypeId = normalType.SeatTypeId;
                        }
                    }
                }

                // Delete all seats in this column
                _context.Seats.RemoveRange(columnSeats);

                // Update remaining seats: shift column index down for columns after deleted one
                var seatsToShift = await _context.Seats
                    .Where(s => s.CinemaTheaterId == request.CinemaTheaterId && s.ColumnIndex > request.ColumnIndex)
                    .ToListAsync();

                foreach (var seat in seatsToShift)
                {
                    seat.ColumnIndex = seat.ColumnIndex - 1;
                    seat.Label = $"{seat.RowIndex}{seat.ColumnIndex}";
                    _context.Update(seat);
                }

                // Update theater's number of columns
                if (theater.NumOfColumns.HasValue && theater.NumOfColumns > 0)
                {
                    theater.NumOfColumns = theater.NumOfColumns.Value - 1;
                    _context.Update(theater);
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"Đã xóa cột {request.ColumnIndex}",
                    affectedSeats = affectedSeats,
                    newNumOfColumns = theater.NumOfColumns
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ DeleteColumn ERROR: {ex.Message}");
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // ===========================
        // POST: Seats/UpdateRowSeatType - Update entire row to a specific seat type
        // ===========================
        [HttpPost]
        public async Task<IActionResult> UpdateRowSeatType([FromBody] UpdateRowSeatTypeRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.CinemaTheaterId) || string.IsNullOrEmpty(request.RowLabel) || string.IsNullOrEmpty(request.SeatTypeId))
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

                // ✅ Handle special action types
                var actionType = request.SeatTypeId.ToUpper();
                
                // ✅ HANDLE DELETE ACTION
                if (actionType == "DELETE")
                {
                    var allRowSeats = await _context.Seats
                        .Where(s => s.CinemaTheaterId == request.CinemaTheaterId && s.RowIndex == request.RowLabel)
                        .ToListAsync();

                    var seatsToDelete = allRowSeats.Where(s => !s.IsDeleted).ToList();

                    var bookedSeatIds = await _context.Tickets
                        .Where(t => seatsToDelete.Select(s => s.SeatId).Contains(t.SeatId) && t.Status == 2)
                        .Select(t => t.SeatId)
                        .ToListAsync();

                    if (bookedSeatIds.Any())
                        return Json(new { success = false, message = $"Có {bookedSeatIds.Count} ghế đã được đặt, không thể xóa" });

                    var affectedSeats = new List<object>();
                    var normalType = await _context.SeatTypes.FirstOrDefaultAsync(st => st.Name == "NORMAL");

                    foreach (var seat in seatsToDelete)
                    {
                        // Unpair if couple
                        if (!string.IsNullOrEmpty(seat.PairId))
                        {
                            var pairSeats = await _context.Seats.Where(s => s.PairId == seat.PairId).ToListAsync();
                            foreach (var ps in pairSeats)
                            {
                                ps.PairId = null;
                                ps.IsDeleted = true;
                                if (normalType != null) ps.SeatTypeId = normalType.SeatTypeId;
                                _context.Update(ps);
                            }
                        }
                        else
                        {
                            seat.IsDeleted = true;
                            _context.Update(seat);
                        }

                        var st = await _context.SeatTypes.FindAsync(seat.SeatTypeId);
                        affectedSeats.Add(new
                        {
                            seatId = seat.SeatId,
                            seatTypeId = seat.SeatTypeId,
                            seatTypeName = st?.Name ?? "NORMAL",
                            isActive = seat.IsActive,
                            isDeleted = true,
                            pairId = (string?)null,
                            label = seat.Label,
                            rowNumber = GetRowNumberFromLabel(seat.RowIndex),
                            columnIndex = seat.ColumnIndex
                        });
                    }

                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = $"Đã xóa {seatsToDelete.Count} ghế trong hàng {request.RowLabel}", affectedSeats });
                }

                // ✅ HANDLE RESTORE ACTION
                if (actionType == "RESTORE")
                {
                    var normalType = await _context.SeatTypes.FirstOrDefaultAsync(st => st.Name == "NORMAL");
                    if (normalType == null)
                        return Json(new { success = false, message = "Không tìm thấy loại ghế NORMAL" });

                    var seatsToRestore = await _context.Seats
                        .Where(s => s.CinemaTheaterId == request.CinemaTheaterId 
                                 && s.RowIndex == request.RowLabel 
                                 && (s.IsDeleted || !s.IsActive))
                        .ToListAsync();

                    var affectedSeats = new List<object>();

                    foreach (var seat in seatsToRestore)
                    {
                        seat.IsDeleted = false;
                        seat.IsActive = true;
                        seat.PairId = null;
                        _context.Update(seat);

                        affectedSeats.Add(new
                        {
                            seatId = seat.SeatId,
                            seatTypeId = seat.SeatId,
                            seatTypeName = (await _context.SeatTypes.FindAsync(seat.SeatTypeId))?.Name ?? "NORMAL",
                            isActive = seat.IsActive,
                            isDeleted = seat.IsDeleted,
                            pairId = seat.PairId,
                            label = seat.Label,
                            rowNumber = GetRowNumberFromLabel(seat.RowIndex),
                            columnIndex = seat.ColumnIndex
                        });
                    }

                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = $"Đã khôi phục {seatsToRestore.Count} ghế trong hàng {request.RowLabel}", affectedSeats });
                }

                // ✅ HANDLE BROKEN ACTION
                if (actionType == "BROKEN")
                {
                    var rowSeats = await _context.Seats
                        .Where(s => s.CinemaTheaterId == request.CinemaTheaterId 
                                 && s.RowIndex == request.RowLabel 
                                 && !s.IsDeleted)
                        .ToListAsync();

                    var bookedSeatIds = await _context.Tickets
                        .Where(t => rowSeats.Select(s => s.SeatId).Contains(t.SeatId) && t.Status == 2)
                        .Select(t => t.SeatId)
                        .ToListAsync();

                    if (bookedSeatIds.Any())
                        return Json(new { success = false, message = $"Có {bookedSeatIds.Count} ghế đã được đặt, không thể thay đổi" });

                    var affectedSeats = new List<object>();
                    var normalType = await _context.SeatTypes.FirstOrDefaultAsync(st => st.Name == "NORMAL");

                    foreach (var seat in rowSeats)
                    {
                        // Unpair if couple
                        if (!string.IsNullOrEmpty(seat.PairId))
                        {
                            var pairSeats = await _context.Seats.Where(s => s.PairId == seat.PairId).ToListAsync();
                            foreach (var ps in pairSeats)
                            {
                                ps.PairId = null;
                                _context.Update(ps);
                            }
                        }

                        seat.IsActive = false;
                        seat.PairId = null;
                        _context.Update(seat);

                        var st = await _context.SeatTypes.FindAsync(seat.SeatTypeId);
                        affectedSeats.Add(new
                        {
                            seatId = seat.SeatId,
                            seatTypeId = seat.SeatTypeId,
                            seatTypeName = st?.Name ?? "NORMAL",
                            isActive = false,
                            isDeleted = seat.IsDeleted,
                            pairId = (string?)null,
                            label = seat.Label,
                            rowNumber = GetRowNumberFromLabel(seat.RowIndex),
                            columnIndex = seat.ColumnIndex
                        });
                    }

                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = $"Đã đánh dấu {rowSeats.Count} ghế hỏng trong hàng {request.RowLabel}", affectedSeats });
                }

                // ✅ HANDLE NORMAL SEAT TYPES (NORMAL, VIP, COUPLE)
                var seatType = await _context.SeatTypes.FindAsync(request.SeatTypeId);
                if (seatType == null)
                    return Json(new { success = false, message = "Loại ghế không tồn tại" });

                var allSeats = await _context.Seats
                    .Where(s => s.CinemaTheaterId == request.CinemaTheaterId 
                             && s.RowIndex == request.RowLabel 
                             && !s.IsDeleted)
                    .ToListAsync();

                if (!allSeats.Any())
                    return Json(new { success = false, message = "Không tìm thấy ghế trong hàng này" });

                // Check for booked seats
                var bookedSeats = await _context.Tickets
                    .Where(t => allSeats.Select(s => s.SeatId).Contains(t.SeatId) && t.Status == 2)
                    .Select(t => t.SeatId)
                    .ToListAsync();

                if (bookedSeats.Any())
                    return Json(new { success = false, message = $"Có {bookedSeats.Count} ghế đã được đặt, không thể thay đổi" });

                var affectedSeatsList = new List<object>();

                // Unpair existing couples first
                var pairedSeatIds = allSeats.Where(s => !string.IsNullOrEmpty(s.PairId)).Select(s => s.PairId).Distinct().ToList();
                foreach (var pairId in pairedSeatIds)
                {
                    var pairSeats = await _context.Seats.Where(s => s.PairId == pairId).ToListAsync();
                    foreach (var ps in pairSeats)
                    {
                        ps.PairId = null;
                        _context.Update(ps);
                    }
                }

                // Update all seats
                foreach (var seat in allSeats)
                {
                    seat.SeatTypeId = request.SeatTypeId;
                    seat.IsActive = true;
                    seat.PairId = null;
                    _context.Update(seat);

                    affectedSeatsList.Add(new
                    {
                        seatId = seat.SeatId,
                        seatTypeId = seat.SeatId,
                        seatTypeName = (await _context.SeatTypes.FindAsync(seat.SeatTypeId))?.Name ?? "NORMAL",
                        isActive = seat.IsActive,
                        isDeleted = seat.IsDeleted,
                        pairId = seat.PairId,
                        label = seat.Label,
                        rowNumber = GetRowNumberFromLabel(seat.RowIndex),
                        columnIndex = seat.ColumnIndex
                    });
                }

                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ UpdateRowSeatType SUCCESS: {affectedSeatsList.Count} seats updated in row {request.RowLabel}");

                return Json(new
                {
                    success = true,
                    message = $"Đã cập nhật {affectedSeatsList.Count} ghế trong hàng {request.RowLabel}",
                    affectedSeats = affectedSeatsList
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ UpdateRowSeatType ERROR: {ex.Message}");
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // ===========================
        // POST: Seats/UpdateColumnSeatType - Update entire column to a specific seat type
        // ===========================
        [HttpPost]
        public async Task<IActionResult> UpdateColumnSeatType([FromBody] UpdateColumnSeatTypeRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.CinemaTheaterId) || request.ColumnIndex <= 0 || string.IsNullOrEmpty(request.SeatTypeId))
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

                // ✅ Handle special action types
                var actionType = request.SeatTypeId.ToUpper();

                // ✅ HANDLE DELETE ACTION
                if (actionType == "DELETE")
                {
                    var allColumnSeats = await _context.Seats
                        .Where(s => s.CinemaTheaterId == request.CinemaTheaterId && s.ColumnIndex == request.ColumnIndex)
                        .ToListAsync();

                    var seatsToDelete = allColumnSeats.Where(s => !s.IsDeleted).ToList();

                    var bookedSeatIds = await _context.Tickets
                        .Where(t => seatsToDelete.Select(s => s.SeatId).Contains(t.SeatId) && t.Status == 2)
                        .Select(t => t.SeatId)
                        .ToListAsync();

                    if (bookedSeatIds.Any())
                        return Json(new { success = false, message = $"Có {bookedSeatIds.Count} ghế đã được đặt, không thể xóa" });

                    var affectedSeats = new List<object>();
                    var normalType = await _context.SeatTypes.FirstOrDefaultAsync(st => st.Name == "NORMAL");

                    foreach (var seat in seatsToDelete)
                    {
                        // Unpair if couple
                        if (!string.IsNullOrEmpty(seat.PairId))
                        {
                            var pairSeats = await _context.Seats.Where(s => s.PairId == seat.PairId).ToListAsync();
                            foreach (var ps in pairSeats)
                            {
                                ps.PairId = null;
                                ps.IsDeleted = true;
                                if (normalType != null) ps.SeatTypeId = normalType.SeatTypeId;
                                _context.Update(ps);
                            }
                        }
                        else
                        {
                            seat.IsDeleted = true;
                            _context.Update(seat);
                        }

                        var st = await _context.SeatTypes.FindAsync(seat.SeatTypeId);
                        affectedSeats.Add(new
                        {
                            seatId = seat.SeatId,
                            seatTypeId = seat.SeatTypeId,
                            seatTypeName = st?.Name ?? "NORMAL",
                            isActive = seat.IsActive,
                            isDeleted = true,
                            pairId = (string?)null,
                            label = seat.Label,
                            rowNumber = GetRowNumberFromLabel(seat.RowIndex),
                            columnIndex = seat.ColumnIndex
                        });
                    }

                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = $"Đã xóa {seatsToDelete.Count} ghế trong cột {request.ColumnIndex}", affectedSeats });
                }

                // ✅ HANDLE RESTORE ACTION
                if (actionType == "RESTORE")
                {
                    var normalType = await _context.SeatTypes.FirstOrDefaultAsync(st => st.Name == "NORMAL");
                    if (normalType == null)
                        return Json(new { success = false, message = "Không tìm thấy loại ghế NORMAL" });

                    var seatsToRestore = await _context.Seats
                        .Where(s => s.CinemaTheaterId == request.CinemaTheaterId 
                                 && s.ColumnIndex == request.ColumnIndex 
                                 && (s.IsDeleted || !s.IsActive))
                        .ToListAsync();

                    var affectedSeats = new List<object>();

                    foreach (var seat in seatsToRestore)
                    {
                        seat.IsDeleted = false;
                        seat.IsActive = true;
                        seat.PairId = null;
                        _context.Update(seat);

                        affectedSeats.Add(new
                        {
                            seatId = seat.SeatId,
                            seatTypeId = seat.SeatId,
                            seatTypeName = (await _context.SeatTypes.FindAsync(seat.SeatTypeId))?.Name ?? "NORMAL",
                            isActive = seat.IsActive,
                            isDeleted = seat.IsDeleted,
                            pairId = seat.PairId,
                            label = seat.Label,
                            rowNumber = GetRowNumberFromLabel(seat.RowIndex),
                            columnIndex = seat.ColumnIndex
                        });
                    }

                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = $"Đã khôi phục {seatsToRestore.Count} ghế trong cột {request.ColumnIndex}", affectedSeats });
                }

                // ✅ HANDLE BROKEN ACTION
                if (actionType == "BROKEN")
                {
                    var columnSeats = await _context.Seats
                        .Where(s => s.CinemaTheaterId == request.CinemaTheaterId 
                                 && s.ColumnIndex == request.ColumnIndex 
                                 && !s.IsDeleted)
                        .ToListAsync();

                    var bookedSeatIds = await _context.Tickets
                        .Where(t => columnSeats.Select(s => s.SeatId).Contains(t.SeatId) && t.Status == 2)
                        .Select(t => t.SeatId)
                        .ToListAsync();

                    if (bookedSeatIds.Any())
                        return Json(new { success = false, message = $"Có {bookedSeatIds.Count} ghế đã được đặt, không thể thay đổi" });

                    var affectedSeats = new List<object>();

                    foreach (var seat in columnSeats)
                    {
                        // Unpair if couple
                        if (!string.IsNullOrEmpty(seat.PairId))
                        {
                            var pairSeats = await _context.Seats.Where(s => s.PairId == seat.PairId).ToListAsync();
                            foreach (var ps in pairSeats)
                            {
                                ps.PairId = null;
                                _context.Update(ps);
                            }
                        }

                        seat.IsActive = false;
                        seat.PairId = null;
                        _context.Update(seat);

                        var st = await _context.SeatTypes.FindAsync(seat.SeatTypeId);
                        affectedSeats.Add(new
                        {
                            seatId = seat.SeatId,
                            seatTypeId = seat.SeatId,
                            seatTypeName = st?.Name ?? "NORMAL",
                            isActive = false,
                            isDeleted = seat.IsDeleted,
                            pairId = (string?)null,
                            label = seat.Label,
                            rowNumber = GetRowNumberFromLabel(seat.RowIndex),
                            columnIndex = seat.ColumnIndex
                        });
                    }

                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = $"Đã đánh dấu {columnSeats.Count} ghế hỏng trong cột {request.ColumnIndex}", affectedSeats });
                }

                // ✅ HANDLE NORMAL SEAT TYPES (NORMAL, VIP, COUPLE)
                var seatType = await _context.SeatTypes.FindAsync(request.SeatTypeId);
                if (seatType == null)
                    return Json(new { success = false, message = "Loại ghế không tồn tại" });

                var allSeats = await _context.Seats
                    .Where(s => s.CinemaTheaterId == request.CinemaTheaterId 
                             && s.ColumnIndex == request.ColumnIndex 
                             && !s.IsDeleted)
                    .ToListAsync();

                if (!allSeats.Any())
                    return Json(new { success = false, message = "Không tìm thấy ghế trong cột này" });

                // Check for booked seats
                var bookedSeats = await _context.Tickets
                    .Where(t => allSeats.Select(s => s.SeatId).Contains(t.SeatId) && t.Status == 2)
                    .Select(t => t.SeatId)
                    .ToListAsync();

                if (bookedSeats.Any())
                    return Json(new { success = false, message = $"Có {bookedSeats.Count} ghế đã được đặt, không thể thay đổi" });

                var affectedSeatsList = new List<object>();

                // Unpair existing couples first
                var pairedSeatIds = allSeats.Where(s => !string.IsNullOrEmpty(s.PairId)).Select(s => s.PairId).Distinct().ToList();
                foreach (var pairId in pairedSeatIds)
                {
                    var pairSeats = await _context.Seats.Where(s => s.PairId == pairId).ToListAsync();
                    foreach (var ps in pairSeats)
                    {
                        ps.PairId = null;
                        _context.Update(ps);
                    }
                }

                // Update all seats
                foreach (var seat in allSeats)
                {
                    seat.SeatTypeId = request.SeatTypeId;
                    seat.IsActive = true;
                    seat.PairId = null;
                    _context.Update(seat);

                    affectedSeatsList.Add(new
                    {
                        seatId = seat.SeatId,
                        seatTypeId = seat.SeatId,
                        seatTypeName = (await _context.SeatTypes.FindAsync(seat.SeatTypeId))?.Name ?? "NORMAL",
                        isActive = seat.IsActive,
                        isDeleted = seat.IsDeleted,
                        pairId = seat.PairId,
                        label = seat.Label,
                        rowNumber = GetRowNumberFromLabel(seat.RowIndex),
                        columnIndex = seat.ColumnIndex
                    });
                }

                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ UpdateColumnSeatType SUCCESS: {affectedSeatsList.Count} seats updated in column {request.ColumnIndex}");

                return Json(new
                {
                    success = true,
                    message = $"Đã cập nhật {affectedSeatsList.Count} ghế trong cột {request.ColumnIndex}",
                    affectedSeats = affectedSeatsList
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ UpdateColumnSeatType ERROR: {ex.Message}");
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // ===========================
        // POST: Seats/ConvertSeatToAisle - Convert seat to aisle (ST004) - NO SHIFTING
        // ===========================
        [HttpPost]
        public async Task<IActionResult> ConvertSeatToAisle([FromBody] ConvertSeatToAisleRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.CinemaTheaterId) || string.IsNullOrEmpty(request.RowLabel) || request.ColumnIndex <= 0)
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

                // Get aisle seat type ST004
                var aisleType = await _context.SeatTypes.FirstOrDefaultAsync(st => st.SeatTypeId == "ST004");
                if (aisleType == null)
                    return Json(new { success = false, message = "Loại ghế AISLE (ST004) chưa tồn tại. Vui lòng thêm vào database!" });

                var affectedSeats = new List<object>();

                // MODE: Row - Convert entire row to aisle
                if (request.Mode == "row")
                {
                    var rowSeats = await _context.Seats
                        .Where(s => s.CinemaTheaterId == request.CinemaTheaterId
                                 && s.RowIndex == request.RowLabel
                                 && !s.IsDeleted
                                 && s.SeatTypeId != "ST004")
                        .ToListAsync();

                    if (!rowSeats.Any())
                        return Json(new { success = false, message = "Không tìm thấy ghế trong hàng này" });

                    // Check booked
                    var bookedSeatIds = await _context.Tickets
                        .Where(t => rowSeats.Select(s => s.SeatId).Contains(t.SeatId) && t.Status == 2)
                        .Select(t => t.SeatId)
                        .ToListAsync();

                    if (bookedSeatIds.Any())
                        return Json(new { success = false, message = $"Có {bookedSeatIds.Count} ghế đã được đặt, không thể tạo lối đi" });

                    // Unpair all couple seats first
                    var pairIds = rowSeats.Where(s => !string.IsNullOrEmpty(s.PairId)).Select(s => s.PairId).Distinct().ToList();
                    foreach (var pairId in pairIds)
                    {
                        var pairSeats = await _context.Seats.Where(s => s.PairId == pairId).ToListAsync();
                        foreach (var ps in pairSeats)
                        {
                            ps.PairId = null;
                            _context.Update(ps);
                        }
                    }

                    // Convert all to aisle
                    foreach (var seat in rowSeats)
                    {
                        seat.SeatTypeId = "ST004";
                        seat.PairId = null;
                        seat.IsActive = true;
                        _context.Update(seat);

                        affectedSeats.Add(new
                        {
                            seatId = seat.SeatId,
                            seatTypeId = seat.SeatId,
                            seatTypeName = aisleType.Name,
                            isActive = seat.IsActive,
                            isDeleted = seat.IsDeleted,
                            pairId = seat.PairId,
                            label = seat.Label,
                            rowNumber = GetRowNumberFromLabel(seat.RowIndex),
                            columnIndex = seat.ColumnIndex
                        });
                    }
                }
                // MODE: Column - Convert entire column to aisle
                else if (request.Mode == "column")
                {
                    var columnSeats = await _context.Seats
                        .Where(s => s.CinemaTheaterId == request.CinemaTheaterId
                                 && s.ColumnIndex == request.ColumnIndex
                                 && !s.IsDeleted
                                 && s.SeatTypeId != "ST004")
                        .ToListAsync();

                    if (!columnSeats.Any())
                        return Json(new { success = false, message = "Không tìm thấy ghế trong cột này" });

                    // Check booked
                    var bookedSeatIds = await _context.Tickets
                        .Where(t => columnSeats.Select(s => s.SeatId).Contains(t.SeatId) && t.Status == 2)
                        .Select(t => t.SeatId)
                        .ToListAsync();

                    if (bookedSeatIds.Any())
                        return Json(new { success = false, message = $"Có {bookedSeatIds.Count} ghế đã được đặt, không thể tạo lối đi" });

                    // Unpair all couple seats first
                    var pairIds = columnSeats.Where(s => !string.IsNullOrEmpty(s.PairId)).Select(s => s.PairId).Distinct().ToList();
                    foreach (var pairId in pairIds)
                    {
                        var pairSeats = await _context.Seats.Where(s => s.PairId == pairId).ToListAsync();
                        foreach (var ps in pairSeats)
                        {
                            ps.PairId = null;
                            _context.Update(ps);
                        }
                    }

                    // Convert all to aisle
                    foreach (var seat in columnSeats)
                    {
                        seat.SeatTypeId = "ST004";
                        seat.PairId = null;
                        seat.IsActive = true;
                        _context.Update(seat);

                        affectedSeats.Add(new
                        {
                            seatId = seat.SeatId,
                            seatTypeId = seat.SeatId,
                            seatTypeName = aisleType.Name,
                            isActive = seat.IsActive,
                            isDeleted = seat.IsDeleted,
                            pairId = seat.PairId,
                            label = seat.Label,
                            rowNumber = GetRowNumberFromLabel(seat.RowIndex),
                            columnIndex = seat.ColumnIndex
                        });
                    }
                }
                // MODE: Single seat
                else
                {
                    var targetSeat = await _context.Seats
                        .FirstOrDefaultAsync(s => s.CinemaTheaterId == request.CinemaTheaterId
                                               && s.RowIndex == request.RowLabel
                                               && s.ColumnIndex == request.ColumnIndex);

                    if (targetSeat == null)
                        return Json(new { success = false, message = "Không tìm thấy ghế" });

                    if (targetSeat.IsDeleted || targetSeat.SeatTypeId == "ST004")
                        return Json(new { success = true, message = "Ghế đã là lối đi hoặc đã bị xóa", affectedSeats = new List<object>() });

                    var isBooked = await _context.Tickets.AnyAsync(t => t.SeatId == targetSeat.SeatId && t.Status == 2);
                    if (isBooked)
                        return Json(new { success = false, message = "Ghế đã được đặt, không thể chuyển thành lối đi" });

                    // Unpair if couple
                    if (!string.IsNullOrEmpty(targetSeat.PairId))
                    {
                        var pairSeats = await _context.Seats.Where(s => s.PairId == targetSeat.PairId).ToListAsync();
                        foreach (var ps in pairSeats)
                        {
                            ps.PairId = null;
                            _context.Update(ps);
                        }
                    }

                    targetSeat.SeatTypeId = "ST004";
                    targetSeat.PairId = null;
                    targetSeat.IsActive = true;
                    _context.Update(targetSeat);

                    affectedSeats.Add(new
                    {
                        seatId = targetSeat.SeatId,
                        seatTypeId = targetSeat.SeatTypeId,
                        seatTypeName = aisleType.Name,
                        isActive = targetSeat.IsActive,
                        isDeleted = targetSeat.IsDeleted,
                        pairId = targetSeat.PairId,
                        label = targetSeat.Label,
                        rowNumber = GetRowNumberFromLabel(targetSeat.RowIndex),
                        columnIndex = targetSeat.ColumnIndex
                    });
                }

                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ ConvertSeatToAisle SUCCESS: {affectedSeats.Count} seats affected");

                return Json(new
                {
                    success = true,
                    message = $"Đã tạo lối đi ({affectedSeats.Count} ghế)",
                    affectedSeats = affectedSeats
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ConvertSeatToAisle ERROR: {ex.Message}\n{ex.StackTrace}");
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // ===========================
        // POST: Seats/RestoreRowFromAisle - Restore row from aisle back to NORMAL
        // ===========================
        [HttpPost]
        public async Task<IActionResult> RestoreRowFromAisle([FromBody] RestoreRowFromAisleRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.CinemaTheaterId) || string.IsNullOrEmpty(request.RowLabel))
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

                var normalType = await _context.SeatTypes.FirstOrDefaultAsync(st => st.Name == "NORMAL");
                if (normalType == null)
                    return Json(new { success = false, message = "Không tìm thấy loại ghế NORMAL" });

                var rowSeats = await _context.Seats
                    .Where(s => s.CinemaTheaterId == request.CinemaTheaterId
                             && s.RowIndex == request.RowLabel
                             && (s.SeatTypeId == "ST004" || s.IsDeleted))
                    .ToListAsync();

                if (!rowSeats.Any())
                    return Json(new { success = false });

                var affectedSeats = new List<object>();

                foreach (var seat in rowSeats)
                {
                    seat.SeatTypeId = normalType.SeatTypeId;
                    seat.IsDeleted = false;
                    seat.IsActive = true;
                    seat.PairId = null;
                    _context.Update(seat);

                    affectedSeats.Add(new
                    {
                        seatId = seat.SeatId,
                        seatTypeId = seat.SeatTypeId,
                        seatTypeName = normalType.Name,
                        isActive = seat.IsActive,
                        isDeleted = seat.IsDeleted,
                        pairId = seat.PairId,
                        label = seat.Label,
                        rowNumber = GetRowNumberFromLabel(seat.RowIndex),
                        columnIndex = seat.ColumnIndex
                    });
                }

                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ RestoreRowFromAisle SUCCESS: {affectedSeats.Count} seats restored");

                return Json(new
                {
                    success = true,
                    message = $"Đã khôi phục {affectedSeats.Count} ghế trong hàng {request.RowLabel}",
                    affectedSeats = affectedSeats
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ RestoreRowFromAisle ERROR: {ex.Message}\n{ex.StackTrace}");
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // ===========================
        // POST: Seats/RestoreColumnFromAisle - Restore column from aisle back to NORMAL
        // ===========================
        [HttpPost]
        public async Task<IActionResult> RestoreColumnFromAisle([FromBody] RestoreColumnFromAisleRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.CinemaTheaterId) || request.ColumnIndex <= 0)
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

                var normalType = await _context.SeatTypes.FirstOrDefaultAsync(st => st.Name == "NORMAL");
                if (normalType == null)
                    return Json(new { success = false, message = "Không tìm thấy loại ghế NORMAL" });

                var columnSeats = await _context.Seats
                    .Where(s => s.CinemaTheaterId == request.CinemaTheaterId
                             && s.ColumnIndex == request.ColumnIndex
                             && (s.SeatTypeId == "ST004" || s.IsDeleted))
                    .ToListAsync();

                if (!columnSeats.Any())
                    return Json(new { success = false });

                var affectedSeats = new List<object>();

                foreach (var seat in columnSeats)
                {
                    seat.SeatTypeId = normalType.SeatTypeId;
                    seat.IsDeleted = false;
                    seat.IsActive = true;
                    seat.PairId = null;
                    _context.Update(seat);

                    affectedSeats.Add(new
                    {
                        seatId = seat.SeatId,
                        seatTypeId = seat.SeatTypeId,
                        seatTypeName = normalType.Name,
                        isActive = seat.IsActive,
                        isDeleted = seat.IsDeleted,
                        pairId = seat.PairId,
                        label = seat.Label,
                        rowNumber = GetRowNumberFromLabel(seat.RowIndex),
                        columnIndex = seat.ColumnIndex
                    });
                }

                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ RestoreColumnFromAisle SUCCESS: {affectedSeats.Count} seats restored");

                return Json(new
                {
                    success = true,
                    message = $"Đã khôi phục {affectedSeats.Count} ghế trong cột {request.ColumnIndex}",
                    affectedSeats = affectedSeats
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ RestoreColumnFromAisle ERROR: {ex.Message}\n{ex.StackTrace}");
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // ===========================
        // POST: Seats/RestoreSeatFromAisle - Restore aisle or deleted seat back to NORMAL
        // ===========================
        [HttpPost]
        public async Task<IActionResult> RestoreSeatFromAisle([FromBody] RestoreSeatRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.SeatId))
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

                var seat = await _context.Seats.FindAsync(request.SeatId);
                if (seat == null)
                    return Json(new { success = false, message = "Không tìm thấy ghế" });

                // Get NORMAL seat type
                var normalType = await _context.SeatTypes.FirstOrDefaultAsync(st => st.Name == "NORMAL");
                if (normalType == null)
                    return Json(new { success = false, message = "Không tìm thấy loại ghế NORMAL" });

                // ✅ FIX: Handle different restore scenarios without confirmation
                if (seat.IsDeleted)
                {
                    // Restore deleted seat
                    seat.IsDeleted = false;
                    seat.IsActive = true;
                    // Keep current type unless it's aisle
                    if (seat.SeatTypeId == "ST004")
                        seat.SeatTypeId = normalType.SeatTypeId;
                }
                else if (seat.SeatTypeId == "ST004")
                {
                    // Convert aisle back to normal - DON'T move seats
                    seat.SeatTypeId = normalType.SeatTypeId;
                    seat.IsActive = true;
                }
                else if (!seat.IsActive)
                {
                    // Restore broken seat
                    seat.IsActive = true;
                }

                // Clear couple pairing on restore to avoid conflicts
                seat.PairId = null;

                _context.Update(seat);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Ghế đã được khôi phục",
                    seat = new
                    {
                        seatId = seat.SeatId,
                        seatTypeId = seat.SeatTypeId,
                        seatTypeName = normalType.Name,
                        isActive = seat.IsActive,
                        isDeleted = seat.IsDeleted,
                        pairId = seat.PairId,
                        label = seat.Label,
                        rowNumber = GetRowNumberFromLabel(seat.RowIndex),
                        columnIndex = seat.ColumnIndex
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ RestoreSeatFromAisle ERROR: {ex.Message}");
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // ===========================
        // POST: Seats/SearchSeats - Tìm kiếm và phân trang ghế
        // ===========================
        [HttpPost]
        public async Task<IActionResult> SearchSeats([FromBody] SearchSeatsRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.CinemaTheaterId))
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

                // Base query
                var query = _context.Seats
                    .Where(s => s.CinemaTheaterId == request.CinemaTheaterId && !s.IsDeleted)
                    .AsQueryable();

                // Filter by row label
                if (!string.IsNullOrEmpty(request.RowLabel))
                {
                    query = query.Where(s => s.RowIndex == request.RowLabel);
                }

                // Filter by column index
                if (request.ColumnIndex > 0)
                {
                    query = query.Where(s => s.ColumnIndex == request.ColumnIndex);
                }

                // Filter by seat type
                if (!string.IsNullOrEmpty(request.SeatTypeId))
                {
                    query = query.Where(s => s.SeatTypeId == request.SeatTypeId);
                }

                // Pagination
                var totalRecords = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalRecords / (double)request.PageSize);

                var seats = await query
                    .OrderBy(s => s.RowIndex).ThenBy(s => s.ColumnIndex)
                    .Skip((request.PageIndex - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    data = seats.Select(s => new
                    {
                        seatId = s.SeatId,
                        seatTypeId = s.SeatTypeId,
                        label = s.Label,
                        rowIndex = s.RowIndex,
                        columnIndex = s.ColumnIndex,
                        isActive = s.IsActive,
                        isDeleted = s.IsDeleted,
                        pairId = s.PairId
                    }),
                    totalRecords = totalRecords,
                    totalPages = totalPages
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ SearchSeats ERROR: {ex.Message}");
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // ===========================
        // POST: Seats/ToggleColumnAisle - Toggle column aisle using IsAisle flag
        // ===========================
        [HttpPost]
        public async Task<IActionResult> ToggleColumnAisle([FromBody] ToggleColumnAisleRequest request)
        {
            try
            {
                // Validate request
                if (string.IsNullOrEmpty(request.CinemaTheaterId) || request.ColumnIndex <= 0)
                {
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ" });
                }

                Console.WriteLine($"🔄 ToggleColumnAisle: Theater={request.CinemaTheaterId}, Column={request.ColumnIndex}");

                // Load ALL seats for this theater (needed for label recalculation)
                var allSeats = await _context.Seats
                    .Where(s => s.CinemaTheaterId == request.CinemaTheaterId)
                    .ToListAsync();

                if (!allSeats.Any())
                {
                    return Json(new { success = false, message = "Không tìm thấy ghế trong phòng chiếu này" });
                }

                // Get seats in the target column
                var columnSeats = allSeats.Where(s => s.ColumnIndex == request.ColumnIndex).ToList();

                if (!columnSeats.Any())
                {
                    return Json(new { success = false, message = $"Không tìm thấy ghế trong cột {request.ColumnIndex}" });
                }

                // Determine current aisle state (if ALL seats in column are aisle, then it's an aisle column)
                var isCurrentlyAisle = columnSeats.All(s => s.IsAisle);

                // ✅ RULE 4: Block creating aisle if column contains ANY double seat (PairId != null)
                if (!isCurrentlyAisle)
                {
                    var hasDoubleSeat = columnSeats.Any(s => !string.IsNullOrEmpty(s.PairId));
                    if (hasDoubleSeat)
                    {
                        Console.WriteLine($"❌ ToggleColumnAisle BLOCKED: Column {request.ColumnIndex} contains double seats");
                        return Json(new { success = false, message = "Không thể tạo lối đi tại cột này vì có ghế đôi" });
                    }
                }

                // Check for booked seats if we're creating an aisle (not removing)
                if (!isCurrentlyAisle)
                {
                    var seatIds = columnSeats.Select(s => s.SeatId).ToList();
                    var bookedCount = await _context.Tickets
                        .Where(t => seatIds.Contains(t.SeatId) && t.Status == 2)
                        .CountAsync();

                    if (bookedCount > 0)
                    {
                        return Json(new { success = false, message = $"Không thể tạo lối đi vì có {bookedCount} ghế đã được đặt" });
                    }
                }

                // Toggle aisle state for the column
                bool newAisleState = !isCurrentlyAisle;

                Console.WriteLine($"📊 Column {request.ColumnIndex}: CurrentAisle={isCurrentlyAisle} → NewAisle={newAisleState}");

                // Update column seats
                foreach (var seat in columnSeats)
                {
                    seat.IsAisle = newAisleState;

                    // If setting as aisle, clear the label and unpair couple seats
                    if (newAisleState)
                    {
                        seat.Label = null;

                        // Unpair if part of couple
                        if (!string.IsNullOrEmpty(seat.PairId))
                        {
                            var pairedSeats = allSeats.Where(s => s.PairId == seat.PairId).ToList();
                            foreach (var ps in pairedSeats)
                            {
                                ps.PairId = null;
                                _context.Update(ps);
                            }
                        }
                    }
                }

                // Recalculate labels for ALL seats in the theater
                RecalculateTheaterLabels(allSeats);

                // Mark all modified entities
                foreach (var seat in allSeats)
                {
                    _context.Entry(seat).State = EntityState.Modified;
                }

                // Save all changes in ONE call
                await _context.SaveChangesAsync();

                // Build response with affected seats data
                var affectedSeats = allSeats.Select(s => new
                {
                    seatId = s.SeatId,
                    seatTypeId = s.SeatTypeId,
                    seatTypeName = _context.SeatTypes.AsNoTracking().FirstOrDefault(st => st.SeatTypeId == s.SeatTypeId)?.Name ?? "NORMAL", // ✅ FIX: Include seatTypeName for UI
                    isActive = s.IsActive,
                    isDeleted = s.IsDeleted,
                    isAisle = s.IsAisle,
                    pairId = s.PairId,
                    label = s.Label,
                    rowNumber = GetRowNumberFromLabel(s.RowIndex),
                    columnIndex = s.ColumnIndex
                }).ToList();

                Console.WriteLine($"✅ ToggleColumnAisle SUCCESS: Column {request.ColumnIndex} is now {(newAisleState ? "AISLE" : "NORMAL")}");

                return Json(new
                {
                    success = true,
                    message = newAisleState
                        ? $"Đã tạo lối đi tại cột {request.ColumnIndex}"
                        : $"Đã khôi phục cột {request.ColumnIndex} thành ghế",
                    isNowAisle = newAisleState,
                    affectedSeats = affectedSeats
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ToggleColumnAisle ERROR: {ex.Message}\n{ex.StackTrace}");
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // ===========================
        // HELPER: Recalculate labels for entire theater
        // Rules:
        // - IsAisle = true → Label = NULL (don't count)
        // - IsDeleted = true → Keep existing label (count toward seat number)
        // - Normal seat → Assign new label = RowIndex + seatCounter
        // ===========================
        private void RecalculateTheaterLabels(List<Seats> allSeats)
        {
            // Group seats by row
            var rowGroups = allSeats
                .GroupBy(s => s.RowIndex)
                .OrderBy(g => g.Key);

            foreach (var rowGroup in rowGroups)
            {
                var rowLabel = rowGroup.Key;
                var seatsInRow = rowGroup.OrderBy(s => s.ColumnIndex).ToList();

                int seatCounter = 1; // Start counting from 1 for each row

                foreach (var seat in seatsInRow)
                {
                    if (seat.IsAisle)
                    {
                        // Aisle seat: Label = NULL, don't increment counter
                        seat.Label = null;
                        // Do NOT increment seatCounter
                    }
                    else if (seat.IsDeleted)
                    {
                        // Deleted seat: KEEP existing label, increment counter
                        // If label was somehow null, assign one
                        if (string.IsNullOrEmpty(seat.Label))
                        {
                            seat.Label = $"{rowLabel}{seatCounter}";
                        }
                        seatCounter++;
                    }
                    else
                    {
                        // Normal active seat: Assign new label
                        seat.Label = $"{rowLabel}{seatCounter}";
                        seatCounter++;
                    }
                }
            }
        }

        // ===========================
        // HELPER METHODS
        // ===========================
        private string GenerateRowLabel(int rowIndex)
        {
            if (rowIndex < 26)
            {
                return ((char)('A' + rowIndex)).ToString();
            }
            else
            {
                // AA, AB, AC, etc.
                int first = (rowIndex / 26) - 1;
                int second = rowIndex % 26;
                return $"{(char)('A' + first)}{(char)('A' + second)}";
            }
        }

        private List<string> GenerateRowLabels(int rowCount)
        {
            var labels = new List<string>();
            for (int i = 0; i < rowCount; i++)
            {
                labels.Add(GenerateRowLabel(i));
            }
            return labels;
        }

        private async Task<string> GenerateNewSeatIdAsync()
        {
            var allSeatIds = await _context.Seats
                .AsNoTracking()
                .Select(s => s.SeatId)
                .ToListAsync();

            int maxNum = 0;
            if (allSeatIds.Any())
            {
                maxNum = allSeatIds
                    .Where(id => id.StartsWith("S") && int.TryParse(id.Substring(1), out _))
                    .Select(id => int.Parse(id.Substring(1)))
                    .DefaultIfEmpty(0)
                    .Max();
            }

            return $"S{(maxNum + 1):D6}";
        }

        private async Task<List<string>> GenerateNewSeatIdsAsync(int count)
        {
            var allSeatIds = await _context.Seats
                .AsNoTracking()
                .Select(s => s.SeatId)
                .ToListAsync();

            int maxNum = 0;
            if (allSeatIds.Any())
            {
                maxNum = allSeatIds
                    .Where(id => id.StartsWith("S") && int.TryParse(id.Substring(1), out _))
                    .Select(id => int.Parse(id.Substring(1)))
                    .DefaultIfEmpty(0)
                    .Max();
            }

            var newIds = new List<string>();
            for (int i = 1; i <= count; i++)
            {
                newIds.Add($"S{(maxNum + i):D6}");
            }
            return newIds;
        }

        private async Task<string> GenerateNewPairIdAsync()
        {
            var last = await _context.Seats
                .Where(s => !string.IsNullOrEmpty(s.PairId))
                .AsNoTracking()
                .OrderByDescending(s => s.PairId)
                .FirstOrDefaultAsync();

            if (last == null || string.IsNullOrEmpty(last.PairId) || !last.PairId.StartsWith("P"))
                return "P00001";

            int num = int.Parse(last.PairId.Substring(1));
            return $"P{(num + 1):D5}";
        }

        // ===========================
        // HELPER: Get row number from label (A=0, B=1, ...)
        // ===========================
        private int GetRowNumberFromLabel(string? rowIndex)
        {
            if (string.IsNullOrEmpty(rowIndex)) return 0;
            if (rowIndex.Length == 1) return rowIndex[0] - 'A';
            if (rowIndex.Length == 2) return ((rowIndex[0] - 'A' + 1) * 26) + (rowIndex[1] - 'A');
            return 0;
        }
    }

    // ===========================
    // REQUEST DTOs
    // ===========================
    public class UpdateSeatTypeRequest
    {
        public string SeatId { get; set; } = default!;
        public string SeatTypeId { get; set; } = default!;
        public bool IsActive { get; set; } = true;
    }

    public class BulkUpdateSeatTypeRequest
    {
        public List<string> SeatIds { get; set; } = new();
        public string SeatTypeId { get; set; } = default!;
    }

    public class SoftDeleteSeatRequest
    {
        public string SeatId { get; set; } = default!;
    }

    public class RestoreSeatRequest
    {
        public string SeatId { get; set; } = default!;
    }

    public class AddRowRequest
    {
        public string CinemaTheaterId { get; set; } = default!;
    }

    public class DeleteRowRequest
    {
        public string CinemaTheaterId { get; set; } = default!;
        public string RowLabel { get; set; } = default!;
    }

    public class AddColumnRequest
    {
        public string CinemaTheaterId { get; set; } = default!;
    }

    public class DeleteColumnRequest
    {
        public string CinemaTheaterId { get; set; } = default!;
        public int ColumnIndex { get; set; }
    }

    public class UpdateRowSeatTypeRequest
    {
        public string CinemaTheaterId { get; set; } = default!;
        public string RowLabel { get; set; } = default!;
        public string SeatTypeId { get; set; } = default!;
    }

    public class UpdateColumnSeatTypeRequest
    {
        public string CinemaTheaterId { get; set; } = default!;
        public int ColumnIndex { get; set; }
        public string SeatTypeId { get; set; } = default!;
    }

    public class ConvertSeatToAisleRequest
    {
        public string CinemaTheaterId { get; set; } = default!;
        public string RowLabel { get; set; } = default!;
        public int ColumnIndex { get; set; }
        public string Mode { get; set; } = "row"; // "row" or "column"
    }

    public class SearchSeatsRequest
    {
        public string CinemaTheaterId { get; set; } = default!;
        public string? RowLabel { get; set; }
        public int ColumnIndex { get; set; }
        public string? SeatTypeId { get; set; }
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class RestoreRowFromAisleRequest
    {
        public string CinemaTheaterId { get; set; } = default!;
        public string RowLabel { get; set; } = default!;
    }

    public class RestoreColumnFromAisleRequest
    {
        public string CinemaTheaterId { get; set; } = default!;
        public int ColumnIndex { get; set; }
    }

    public class ToggleColumnAisleRequest
    {
        public string CinemaTheaterId { get; set; } = default!;
        public int ColumnIndex { get; set; }
    }
}