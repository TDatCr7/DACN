using System;
using System.Collections.Generic;

namespace CinemaS.Models.ViewModels
{
    public class StatisticsDashboardVM
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string GroupBy { get; set; } = "week";
        public int TopN { get; set; } = 10;

        public KpiVM Kpi { get; set; } = new KpiVM();
        public List<RevenueBucketVM> RevenueByPeriod { get; set; } = new();
        public List<TopMovieVM> TopMoviesByTickets { get; set; } = new();
        public List<MovieRevenueVM> MovieRevenue { get; set; } = new();
    }

    public class KpiVM
    {
        public decimal TotalPaid { get; set; }
        public decimal TicketRevenue { get; set; }
        public decimal SnackRevenue { get; set; }
        public int TotalInvoices { get; set; }
        public int TotalTickets { get; set; }
    }

    public class RevenueBucketVM
    {
        public DateTime PeriodStartLocal { get; set; }
        public string Label { get; set; } = "";
        public decimal TicketRevenue { get; set; }
        public decimal SnackRevenue { get; set; }
        public decimal TotalPaid { get; set; }
    }

    public class TopMovieVM
    {
        public int Rank { get; set; }
        public string MovieId { get; set; } = "";
        public string Title { get; set; } = "";
        public int TicketsSold { get; set; }
        public decimal TicketRevenue { get; set; }
    }

    public class MovieRevenueVM
    {
        public string MovieId { get; set; } = "";
        public string Title { get; set; } = "";
        public decimal TicketRevenue { get; set; }
    }
}
