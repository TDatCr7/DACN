using System;
using System.Collections.Generic;

namespace CinemaS.Models.ViewModels
{
    public class SnackItemVM
    {
        public string Name { get; set; } = "";
        public int Quantity { get; set; }
        public decimal LineTotal { get; set; }
        public string? Image { get; set; }
        public string? SnackId { get; set; }

    }

    public class SnackInvoiceDetailVM
    {
        public string InvoiceId { get; set; } = "";
        public DateTime? CreatedAt { get; set; }

        // Không hiển thị nữa ở view nhưng giữ lại nếu nơi khác dùng
        public string InvoiceEmail { get; set; } = "";
        public string InvoicePhone { get; set; } = "";

        public List<SnackItemVM> SnackItems { get; set; } = new();

        // ✅ 3 số tiền cần hiển thị
        public decimal OriginalAmount { get; set; }      // tổng tiền (giá gốc)
        public decimal DiscountAmount { get; set; }      // được giảm
        public decimal PayableAmount { get; set; }       // tổng thanh toán
        public double? DiscountPercent { get; set; }   // giảm (%)
        public string? PromotionName { get; set; }     // chương trình
        public string? PaymentMethod { get; set; }


    }


    public class SnackPaymentResultVM
    {
        public bool IsSuccess { get; set; }
        public string OrderId { get; set; } = "";
        
        public SnackInvoiceDetailVM? Detail { get; set; }
    }

    public class SnackInvoiceHistoryVM
    {
        public string InvoiceId { get; set; } = "";
        public DateTime? CreatedAt { get; set; }
        public decimal TotalPrice { get; set; }
        public int Status { get; set; }
    }
}
