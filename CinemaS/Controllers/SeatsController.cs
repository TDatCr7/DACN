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
                var totalSeats = await _context.Seats.CountAsync(s => s.CinemaTheaterId == st.CinemaTheaterId);
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
                                                && s.ColumnIndex == seat.ColumnIndex + 1);

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
                                                    && s.ColumnIndex == seat.ColumnIndex - 1);

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

                    affectedSeats.Add(new {
                        seatId = seat.SeatId,
                        seatTypeId = seat.SeatTypeId,
                        seatTypeName = seatType.Name,
                        isActive = seat.IsActive,
                        pairId = seat.PairId,
                        label = seat.Label,
                        rowNumber = GetRowNumberFromLabel(seat.RowIndex),
                        columnIndex = seat.ColumnIndex
                    });
                    affectedSeats.Add(new {
                        seatId = adjacentSeat.SeatId,
                        seatTypeId = adjacentSeat.SeatTypeId,
                        seatTypeName = seatType.Name,
                        isActive = adjacentSeat.IsActive,
                        pairId = adjacentSeat.PairId,
                        label = adjacentSeat.Label,
                        rowNumber = GetRowNumberFromLabel(adjacentSeat.RowIndex),
                        columnIndex = adjacentSeat.ColumnIndex
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

                            affectedSeats.Add(new {
                                seatId = ps.SeatId,
                                seatTypeId = ps.SeatTypeId,
                                seatTypeName = normalType.Name,
                                isActive = false,
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

                                affectedSeats.Add(new {
                                    seatId = ps.SeatId,
                                    seatTypeId = ps.SeatTypeId,
                                    seatTypeName = seatType.Name, // ← Use requested type name
                                    isActive = ps.IsActive,
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

                            affectedSeats.Add(new {
                                seatId = seat.SeatId,
                                seatTypeId = seat.SeatTypeId,
                                seatTypeName = seatType.Name,
                                isActive = seat.IsActive,
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

                return Json(new { 
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
        // POST: Seats/AddRow - Add new row at the end
        // ===========================
        [HttpPost]
        public async Task<IActionResult> AddRow([FromBody] AddRowRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.CinemaTheaterId))
                    return Json(new { success = false, message = "Thiếu ID phòng chiếu" });

                // ✅ FIX: Load theater for tracking, but detach it first
                var theater = await _context.CinemaTheaters.FindAsync(request.CinemaTheaterId);
                if (theater == null)
                    return Json(new { success = false, message = "Không tìm thấy phòng chiếu" });

                var numCols = theater.NumOfColumns ?? 14;
                var numRows = theater.NumOfRows ?? 0;
                var newRowNumber = numRows;
                var newRowLabel = GenerateRowLabel(newRowNumber);

                // Get normal seat type
                var normalType = await _context.SeatTypes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(st => st.Name == "NORMAL");
                    
                if (normalType == null)
                    return Json(new { success = false, message = "Không tìm thấy loại ghế NORMAL" });

                // ✅ FIX: Pre-generate all IDs BEFORE adding to context
                var lastSeat = await _context.Seats
                    .AsNoTracking()
                    .OrderByDescending(s => s.SeatId)
                    .FirstOrDefaultAsync();
                    
                int startNum = lastSeat == null || !lastSeat.SeatId.StartsWith("S") ? 1 : int.Parse(lastSeat.SeatId.Substring(1)) + 1;

                // Create new seats
                var newSeats = new List<Seats>();
                var newSeatsData = new List<object>();

                for (int col = 1; col <= numCols; col++)
                {
                    var newSeat = new Seats
                    {
                        SeatId = $"S{startNum:D6}", // Use pre-generated ID
                        SeatTypeId = normalType.SeatTypeId,
                        CinemaTheaterId = request.CinemaTheaterId,
                        RowIndex = newRowLabel,
                        ColumnIndex = col,
                        Label = $"{newRowLabel}{col}",
                        IsActive = true,
                        PairId = null
                    };
                    newSeats.Add(newSeat);

                    newSeatsData.Add(new {
                        seatId = newSeat.SeatId,
                        seatTypeId = newSeat.SeatTypeId,
                        seatTypeName = normalType.Name,
                        rowNumber = newRowNumber,
                        columnIndex = col,
                        label = newSeat.Label,
                        isActive = true,
                        pairId = (string?)null
                    });
                    
                    startNum++;
                }

                _context.Seats.AddRange(newSeats);
                
                // ✅ FIX: Update theater NumOfRows directly (already tracked)
                theater.NumOfRows = numRows + 1;
                
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ AddRow SUCCESS: Added {newSeats.Count} seats for row {newRowLabel}");

                return Json(new { 
                    success = true, 
                    message = $"Đã thêm hàng {newRowLabel}",
                    newRow = new {
                        rowNumber = newRowNumber,
                        rowLabel = newRowLabel,
                        seats = newSeatsData
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ AddRow ERROR: {ex.Message}\n{ex.StackTrace}");
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // ===========================
        // POST: Seats/DeleteRow - Delete entire row
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

                if (theater.NumOfRows <= 1)
                    return Json(new { success = false, message = "Không thể xóa hàng cuối cùng" });

                // ✅ FIX: Chỉ cho phép xóa hàng cuối cùng
                var currentNumRows = theater.NumOfRows ?? 0;
                var lastRowLabel = GenerateRowLabel(currentNumRows - 1);
                
                if (request.RowLabel != lastRowLabel)
                {
                    return Json(new { 
                        success = false, 
                        message = $"⚠️ Chỉ có thể xóa hàng cuối cùng ({lastRowLabel}). Không thể xóa hàng {request.RowLabel}!" 
                    });
                }

                // Get seats in this row
                var seatsInRow = await _context.Seats
                    .Where(s => s.CinemaTheaterId == request.CinemaTheaterId && s.RowIndex == request.RowLabel)
                    .ToListAsync();

                // Check if any are booked
                var seatIds = seatsInRow.Select(s => s.SeatId).ToList();
                var bookedSeats = await _context.Tickets
                    .Where(t => seatIds.Contains(t.SeatId) && t.Status == 2)
                    .Select(t => t.SeatId)
                    .ToListAsync();

                if (bookedSeats.Any())
                    return Json(new { success = false, message = $"Có {bookedSeats.Count} ghế đã được đặt, không thể xóa" });

                // Delete seats
                _context.Seats.RemoveRange(seatsInRow);
                theater.NumOfRows = (theater.NumOfRows ?? 1) - 1;
                _context.Update(theater);
                await _context.SaveChangesAsync();

                return Json(new { 
                    success = true, 
                    message = $"Đã xóa hàng {request.RowLabel}"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // ===========================
        // POST: Seats/AddColumn - Add new column at the end (FIXED)
        // ===========================
        [HttpPost]
        public async Task<IActionResult> AddColumn([FromBody] AddColumnRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.CinemaTheaterId))
                    return Json(new { success = false, message = "Thiếu ID phòng chiếu" });

                // ✅ FIX: Load theater for tracking
                var theater = await _context.CinemaTheaters.FindAsync(request.CinemaTheaterId);
                if (theater == null)
                    return Json(new { success = false, message = "Không tìm thấy phòng chiếu" });

                var numCols = theater.NumOfColumns ?? 0;
                var newColIndex = numCols + 1;

                // ✅ CRITICAL FIX: Update NumOfColumns FIRST before adding seats
                theater.NumOfColumns = newColIndex;
                _context.Update(theater);

                // Get all unique row labels and their row numbers
                var allSeats = await _context.Seats
                    .AsNoTracking()
                    .Where(s => s.CinemaTheaterId == request.CinemaTheaterId)
                    .OrderBy(s => s.RowIndex)
                    .ToListAsync();

                var rowsData = allSeats
                    .GroupBy(s => s.RowIndex)
                    .Select((g, index) => new {
                        RowIndex = g.Key,
                        RowNum = index
                    })
                    .ToList();

                if (!rowsData.Any())
                    return Json(new { success = false, message = "Chưa có hàng nào" });

                // Get normal seat type
                var normalType = await _context.SeatTypes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(st => st.Name == "NORMAL");
                    
                if (normalType == null)
                    return Json(new { success = false, message = "Không tìm thấy loại ghế NORMAL" });

                // Create new seats for each row
                var newSeats = new List<Seats>();
                var newSeatsData = new List<object>();
                var newSeatIds = await GenerateNewSeatIdsAsync(rowsData.Count);
                int idIndex = 0;

                foreach (var row in rowsData)
                {
                    var newSeat = new Seats
                    {
                        SeatId = newSeatIds[idIndex++], // Use pre-generated unique ID
                        SeatTypeId = normalType.SeatTypeId,
                        CinemaTheaterId = request.CinemaTheaterId,
                        RowIndex = row.RowIndex,
                        ColumnIndex = newColIndex,
                        Label = $"{row.RowIndex}{newColIndex}",
                        IsActive = true,
                        PairId = null
                    };
                    newSeats.Add(newSeat);

                    newSeatsData.Add(new {
                        seatId = newSeat.SeatId,
                        seatTypeId = newSeat.SeatTypeId,
                        seatTypeName = normalType.Name,
                        rowNumber = row.RowNum,
                        columnIndex = newColIndex,
                        label = newSeat.Label,
                        isActive = true,
                        pairId = (string?)null
                    });
                }

                _context.Seats.AddRange(newSeats);
                
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ AddColumn SUCCESS: Added {newSeats.Count} seats for column {newColIndex}");

                return Json(new { 
                    success = true, 
                    message = $"Đã thêm cột {newColIndex}",
                    newColIndex = newColIndex,
                    newSeats = newSeatsData
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ AddColumn ERROR: {ex.Message}\n{ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"❌ INNER EXCEPTION: {ex.InnerException.Message}");
                }
                return Json(new { success = false, message = $"Lỗi: {ex.InnerException?.Message ?? ex.Message}" });
            }
        }

        // ===========================
        // POST: Seats/DeleteColumn - Delete column at specified index
        // ===========================
        [HttpPost]
        public async Task<IActionResult> DeleteColumn([FromBody] DeleteColumnRequest request)
        {
            try {
                if (string.IsNullOrEmpty(request.CinemaTheaterId) || request.ColumnIndex <= 0)
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

                var theater = await _context.CinemaTheaters.FindAsync(request.CinemaTheaterId);
                if (theater == null)
                    return Json(new { success = false, message = "Không tìm thấy phòng chiếu" });

                if (theater.NumOfColumns <= 1)
                    return Json(new { success = false, message = "Không thể xóa cột cuối cùng" });

                // ✅ FIX: Chỉ cho phép xóa cột cuối cùng
                var currentNumCols = theater.NumOfColumns ?? 0;
                
                if (request.ColumnIndex != currentNumCols)
                {
                    return Json(new { 
                        success = false, 
                        message = $"⚠️ Chỉ có thể xóa cột cuối cùng ({currentNumCols}). Không thể xóa cột {request.ColumnIndex}!" 
                    });
                }

                // Get seats in this column
                var seatsInColumn = await _context.Seats
                    .Where(s => s.CinemaTheaterId == request.CinemaTheaterId && s.ColumnIndex == request.ColumnIndex)
                    .ToListAsync();

                // Check if any are booked
                var seatIds = seatsInColumn.Select(s => s.SeatId).ToList();
                var bookedSeats = await _context.Tickets
                    .Where(t => seatIds.Contains(t.SeatId) && t.Status == 2)
                    .Select(t => t.SeatId)
                    .ToListAsync();

                if (bookedSeats.Any())
                    return Json(new { success = false, message = $"Có {bookedSeats.Count} ghế đã được đặt, không thể xóa" });

                // Handle unpairing for couple seats
                var normalType = await _context.SeatTypes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(st => st.Name == "NORMAL");
                    
                if (normalType == null)
                    return Json(new { success = false, message = "Không tìm thấy loại ghế NORMAL" });

                var affectedSeats = new List<object>();
                var pairsToUnpair = seatsInColumn
                    .Where(s => !string.IsNullOrEmpty(s.PairId))
                    .Select(s => s.PairId)
                    .Distinct()
                    .ToList();

                foreach (var pairId in pairsToUnpair)
                {
                    var remainingSeats = await _context.Seats
                        .Where(s => s.CinemaTheaterId == request.CinemaTheaterId && s.PairId == pairId && s.ColumnIndex != request.ColumnIndex)
                        .ToListAsync();
                    
                    foreach (var rs in remainingSeats)
                    {
                        rs.PairId = null;
                        rs.SeatTypeId = normalType.SeatTypeId;
                        _context.Update(rs);

                        affectedSeats.Add(new {
                            seatId = rs.SeatId,
                            seatTypeId = rs.SeatTypeId,
                            seatTypeName = normalType.Name,
                            isActive = rs.IsActive,
                            pairId = rs.PairId,
                            label = rs.Label,
                            rowNumber = GetRowNumberFromLabel(rs.RowIndex),
                            columnIndex = rs.ColumnIndex
                        });
                    }
                }

                // Delete seats
                _context.Seats.RemoveRange(seatsInColumn);
                
                // Re-index remaining columns
                var seatsToReindex = await _context.Seats
                    .Where(s => s.CinemaTheaterId == request.CinemaTheaterId && s.ColumnIndex > request.ColumnIndex)
                    .OrderBy(s => s.ColumnIndex)
                    .ToListAsync();

                foreach (var s in seatsToReindex)
                {
                    s.ColumnIndex--;
                    s.Label = $"{s.RowIndex}{s.ColumnIndex}";
                    _context.Update(s);

                    var seatType = await _context.SeatTypes.FindAsync(s.SeatTypeId);
                    affectedSeats.Add(new {
                        seatId = s.SeatId,
                        seatTypeId = s.SeatTypeId,
                        seatTypeName = seatType?.Name ?? "NORMAL",
                        isActive = s.IsActive,
                        pairId = s.PairId,
                        label = s.Label,
                        rowNumber = GetRowNumberFromLabel(s.RowIndex),
                        columnIndex = s.ColumnIndex
                    });
                }
                
                // Update NumOfColumns
                theater.NumOfColumns = (theater.NumOfColumns ?? 1) - 1;
                _context.Update(theater);
                
                await _context.SaveChangesAsync();

                return Json(new { 
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
}
