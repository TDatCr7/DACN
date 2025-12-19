using System;
using System.Collections.Generic;

namespace CinemaS.Models.ViewModels
{
    /// <summary>
    /// Data for QR code content (used for encryption)
    /// </summary>
    public class QrTicketPayload
    {
        public string TicketId { get; set; } = string.Empty;
        public string InvoiceId { get; set; } = string.Empty;
        public string MovieId { get; set; } = string.Empty;
        public string ShowTimeId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Full QR content displayed in QR code (readable + encrypted)
    /// </summary>
    public class QrTicketContent
    {
        public string InvoiceId { get; set; } = string.Empty;
        public string MovieName { get; set; } = string.Empty;
        public string Seats { get; set; } = string.Empty;
        public string CipherText { get; set; } = string.Empty;

        /// <summary>
        /// Format: InvoiceId|MovieName|Seats|CipherText
        /// </summary>
        public string ToQrString()
        {
            return $"{InvoiceId}|{MovieName}|{Seats}|{CipherText}";
        }

        public static QrTicketContent? FromQrString(string qrString)
        {
            if (string.IsNullOrWhiteSpace(qrString))
                return null;

            var parts = qrString.Split('|');
            if (parts.Length < 4)
                return null;

            return new QrTicketContent
            {
                InvoiceId = parts[0],
                MovieName = parts[1],
                Seats = parts[2],
                CipherText = string.Join("|", parts.Skip(3)) // CipherText might contain |
            };
        }
    }

    /// <summary>
    /// Result of QR validation
    /// </summary>
    public class QrValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }

        // Ticket information (if valid)
        public string? InvoiceId { get; set; }
        public string? TicketId { get; set; }
        public string? MovieTitle { get; set; }
        public string? MoviePoster { get; set; }
        public List<string>? SeatLabels { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
        public string? CustomerPhone { get; set; }
        public DateTime? BookingDate { get; set; }
        public DateTime? ShowDate { get; set; }
        public DateTime? ShowTime { get; set; }
        public string? TheaterName { get; set; }
        public string? RoomName { get; set; }
        public decimal? TotalPrice { get; set; }
        public int? TicketCount { get; set; }
        public byte? TicketStatus { get; set; }

        public static QrValidationResult Invalid(string message)
        {
            return new QrValidationResult
            {
                IsValid = false,
                ErrorMessage = message
            };
        }
    }

    /// <summary>
    /// Data for PDF ticket generation
    /// </summary>
    public class TicketPdfData
    {
        public string InvoiceId { get; set; } = string.Empty;
        public string MovieTitle { get; set; } = string.Empty;
        public string? MoviePoster { get; set; }
        public List<string> SeatLabels { get; set; } = new();
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerEmail { get; set; }
        public DateTime? BookingDate { get; set; }
        public DateTime? ShowDate { get; set; }
        public DateTime? ShowTime { get; set; }
        public string TheaterName { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public byte[] QrImageBytes { get; set; } = Array.Empty<byte>();
    }
}
