using CinemaS.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CinemaS.Models
{
    public class CinemaContext : IdentityDbContext<AppUser>
    {
        public CinemaContext(DbContextOptions<CinemaContext> options) : base(options) { }

        // DbSets
        public DbSet<Status> Statuses => Set<Status>();
        public DbSet<Genres> Genres => Set<Genres>();
        public DbSet<Provinces> Provinces => Set<Provinces>();
        public DbSet<CinemaTypes> CinemaTypes => Set<CinemaTypes>();
        public DbSet<SeatTypes> SeatTypes => Set<SeatTypes>();
        public DbSet<TicketTypes> TicketTypes => Set<TicketTypes>();
        public DbSet<MembershipRank> MembershipRanks => Set<MembershipRank>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<PaymentMethods> PaymentMethods => Set<PaymentMethods>();
        public DbSet<Participants> Participants => Set<Participants>();
        public DbSet<MovieRole> MovieRoles => Set<MovieRole>();
        public DbSet<Movies> Movies => Set<Movies>();
        public DbSet<MoviesGenres> MoviesGenres => Set<MoviesGenres>();
        public DbSet<MoviesParticipants> MoviesParticipants => Set<MoviesParticipants>();
        public DbSet<MovieTheaters> MovieTheaters => Set<MovieTheaters>();
        public DbSet<CinemaTheaters> CinemaTheaters => Set<CinemaTheaters>();
        public DbSet<Seats> Seats => Set<Seats>();
        public DbSet<ShowTimes> ShowTimes => Set<ShowTimes>();
        public DbSet<Users> Users => Set<Users>();
        public DbSet<Invoices> Invoices => Set<Invoices>();
        public DbSet<Tickets> Tickets => Set<Tickets>();
        public DbSet<Promotion> Promotion => Set<Promotion>();
        public DbSet<PointHistories> PointHistories => Set<PointHistories>();
        public DbSet<SnackTypes> SnackTypes => Set<SnackTypes>();
        public DbSet<Snacks> Snacks => Set<Snacks>();
        public DbSet<DetailBookingSnacks> DetailBookingSnacks => Set<DetailBookingSnacks>();
        public DbSet<UserRole> UserRole => Set<UserRole>();
        public DbSet<RolePermission> RolePermission => Set<RolePermission>();
        public DbSet<PaymentTransactions> PaymentTransactions => Set<PaymentTransactions>();

        public string DefaultMembershipRankId { get; set; } = "MR00000001"; // cấu hình/seed id hợp lệ
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // 1) AppUser vừa được thêm => tạo Users tương ứng
            var newUsers = ChangeTracker.Entries<AppUser>()
                .Where(e => e.State == EntityState.Added)
                .Select(e => e.Entity)
                .ToList();

            foreach (var au in newUsers)
            {
                // Tạo mã User_ID 10 ký tự (VD: "U" + 9 ký tự)
                string userId = "U" + Guid.NewGuid().ToString("N").Substring(0, 9).ToUpper();

                var u = new Users
                {
                    UserId = userId,
                    MembershipRankId = DefaultMembershipRankId,   // PHẢI tồn tại trong bảng Membership_Rank
                    Email = au.Email,
                    PhoneNumber = au.PhoneNumber,
                    FullName = au.FullName,
                    Address = au.Address,
                    Status = 1,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                Users.Add(u);
            }

            // 2) AppUser cập nhật => cập nhật Users (khớp theo Email nếu có)
            var modUsers = ChangeTracker.Entries<AppUser>()
                .Where(e => e.State == EntityState.Modified)
                .Select(e => e.Entity)
                .ToList();

            foreach (var au in modUsers)
            {
                if (!string.IsNullOrEmpty(au.Email))
                {
                    var u = await Users.FirstOrDefaultAsync(x => x.Email == au.Email, cancellationToken);
                    if (u != null)
                    {
                        u.FullName = au.FullName;
                        u.PhoneNumber = au.PhoneNumber;
                        u.Address = au.Address;
                        u.UpdatedAt = DateTime.UtcNow;
                        Entry(u).State = EntityState.Modified;
                    }
                }
            }

            return await base.SaveChangesAsync(cancellationToken);
        }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ===== Khóa chính phức hợp =====
            modelBuilder.Entity<MoviesGenres>().HasKey(x => new { x.MovieGenreId, x.MoviesId, x.GenresId });
            modelBuilder.Entity<MoviesParticipants>().HasKey(x => new { x.MovieParticipantId, x.ParticipantsId, x.MoviesId });
            modelBuilder.Entity<DetailBookingSnacks>().HasKey(x => new { x.SnackId, x.InvoiceId, x.DetailBookingSnackId });
            modelBuilder.Entity<UserRole>().HasKey(x => new { x.RoleId, x.UserId });
            modelBuilder.Entity<RolePermission>().HasKey(x => new { x.PermissionId, x.RoleId });

            // ===== Quan hệ FK cốt lõi =====
            modelBuilder.Entity<Movies>().HasOne<Status>().WithMany().HasForeignKey(m => m.StatusId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MoviesGenres>().HasOne<Movies>().WithMany().HasForeignKey(x => x.MoviesId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<MoviesGenres>().HasOne<Genres>().WithMany().HasForeignKey(x => x.GenresId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MoviesParticipants>().HasOne<MovieRole>().WithMany().HasForeignKey(x => x.MovieRoleId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<MoviesParticipants>().HasOne<Participants>().WithMany().HasForeignKey(x => x.ParticipantsId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<MoviesParticipants>().HasOne<Movies>().WithMany().HasForeignKey(x => x.MoviesId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MovieTheaters>().HasOne<Provinces>().WithMany().HasForeignKey(x => x.ProvinceId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CinemaTheaters>().HasOne<CinemaTypes>().WithMany().HasForeignKey(x => x.CinemaTypeId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<CinemaTheaters>().HasOne<MovieTheaters>().WithMany().HasForeignKey(x => x.MovieTheaterId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Seats>().HasOne<SeatTypes>().WithMany().HasForeignKey(x => x.SeatTypeId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Seats>().HasOne<CinemaTheaters>().WithMany().HasForeignKey(x => x.CinemaTheaterId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ShowTimes>().HasOne<Movies>().WithMany().HasForeignKey(x => x.MoviesId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<ShowTimes>().HasOne<CinemaTheaters>().WithMany().HasForeignKey(x => x.CinemaTheaterId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Invoices>().HasOne<Promotion>().WithMany().HasForeignKey(x => x.PromotionId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Invoices>().HasOne<Users>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Invoices>().HasOne<Users>().WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Invoices>().HasOne<PaymentMethods>().WithMany().HasForeignKey(x => x.PaymentMethodId).OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Tickets>().HasOne<ShowTimes>().WithMany().HasForeignKey(x => x.ShowTimeId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Tickets>().HasOne<TicketTypes>().WithMany().HasForeignKey(x => x.TicketTypeId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Tickets>().HasOne<Seats>().WithMany().HasForeignKey(x => x.SeatId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Tickets>().HasOne<Invoices>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Promotion>().HasOne<Users>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Users>().HasOne<MembershipRank>().WithMany().HasForeignKey(x => x.MembershipRankId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserRole>().HasOne<Users>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<UserRole>().HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RolePermission>().HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<RolePermission>().HasOne<Permission>().WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DetailBookingSnacks>().HasOne<Invoices>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<DetailBookingSnacks>().HasOne<Snacks>().WithMany().HasForeignKey(x => x.SnackId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Snacks>().HasOne<SnackTypes>().WithMany().HasForeignKey(x => x.SnackTypeId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PointHistories>().HasOne<Invoices>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<PointHistories>().HasOne<Users>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PaymentTransactions>().HasOne<Invoices>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<PaymentTransactions>().HasOne<PaymentMethods>().WithMany().HasForeignKey(x => x.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);

            // ===== Unique/Filtered Indexes quan trọng =====
            modelBuilder.Entity<Tickets>().HasIndex(x => new { x.ShowTimeId, x.SeatId }).IsUnique();
            modelBuilder.Entity<Seats>().HasIndex(x => new { x.CinemaTheaterId, x.RowIndex, x.ColumnIndex }).IsUnique();
            modelBuilder.Entity<Seats>().HasIndex(x => new { x.CinemaTheaterId, x.Label }).IsUnique().HasFilter("[Label] IS NOT NULL");
            modelBuilder.Entity<Promotion>().HasIndex(x => x.Code).IsUnique().HasFilter("[Code] IS NOT NULL AND [Status] = 1");
            modelBuilder.Entity<PaymentMethods>().HasIndex(x => x.Code).IsUnique().HasFilter("[Code] IS NOT NULL");
            modelBuilder.Entity<Users>().HasIndex(x => x.Email).IsUnique().HasFilter("[Email] IS NOT NULL");
            modelBuilder.Entity<Genres>().HasIndex(x => x.Name).IsUnique().HasFilter("[Name] IS NOT NULL");
            modelBuilder.Entity<Status>().HasIndex(x => x.Name).IsUnique().HasFilter("[Name] IS NOT NULL");
            modelBuilder.Entity<CinemaTypes>().HasIndex(x => x.Code).IsUnique().HasFilter("[Code] IS NOT NULL");
            modelBuilder.Entity<TicketTypes>().HasIndex(x => x.Name).IsUnique().HasFilter("[Name] IS NOT NULL");
            modelBuilder.Entity<SnackTypes>().HasIndex(x => x.Name).IsUnique().HasFilter("[Name] IS NOT NULL");
            modelBuilder.Entity<Role>().HasIndex(x => x.Name).IsUnique().HasFilter("[Name] IS NOT NULL");
            modelBuilder.Entity<Permission>().HasIndex(x => new { x.Method, x.Url }).IsUnique().HasFilter("[Method] IS NOT NULL AND [Url] IS NOT NULL");
            modelBuilder.Entity<Provinces>().HasIndex(x => x.Name).IsUnique().HasFilter("[Name] IS NOT NULL");
            modelBuilder.Entity<MovieTheaters>().HasIndex(x => new { x.ProvinceId, x.Name }).IsUnique().HasFilter("[Name] IS NOT NULL");
            modelBuilder.Entity<Movies>().HasIndex(x => new { x.Title, x.ReleaseDate }).IsUnique().HasFilter("[Title] IS NOT NULL AND [Release_Date] IS NOT NULL");
            modelBuilder.Entity<MoviesGenres>().HasIndex(x => new { x.MoviesId, x.GenresId }).IsUnique();
            modelBuilder.Entity<MoviesParticipants>().HasIndex(x => new { x.MoviesId, x.ParticipantsId, x.MovieRoleId }).IsUnique();
        }
    }
}
