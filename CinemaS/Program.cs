using CinemaS.Models;
using CinemaS.Models.Email;
using CinemaS.Models.Payments;
using CinemaS.Models.Settings;
using CinemaS.Services;
using CinemaS.VNPAY;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using CinemaS.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Kết nối DB
builder.Services.AddDbContext<CinemaContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CinemaS")));

// Identity
builder.Services
    .AddIdentity<AppUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<CinemaContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

// cấu hình VnPay
builder.Services.Configure<VnPaySettings>(builder.Configuration.GetSection("VnPay"));
builder.Services.AddSingleton<VnPayLibrary>();

// cấu hình EmailSender (dùng Gmail)
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<GmailEmailSender>();
builder.Services.AddTransient<IEmailSender>(sp => sp.GetRequiredService<GmailEmailSender>());
builder.Services.AddTransient<IEmailSenderWithAttachment>(sp => sp.GetRequiredService<GmailEmailSender>());

// cấu hình QR Ticket Service
builder.Services.Configure<QrSettings>(builder.Configuration.GetSection("QrSettings"));
builder.Services.AddScoped<IQrTicketService, QrTicketService>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddCors(options =>
{
    options.AddPolicy("MobileCors", p =>
        p.AllowAnyHeader().AllowAnyMethod().SetIsOriginAllowed(_ => true));
});

builder.Services.AddSingleton<IRegisterOtpStore, RegisterOtpStore>();

var app = builder.Build();

/* Seed roles + admin */
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<CinemaContext>();

    var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

    var roles = new[] { "Admin", "User" };
    foreach (var r in roles)
    {
        if (!await roleMgr.RoleExistsAsync(r))
            await roleMgr.CreateAsync(new IdentityRole(r));
    }

    var adminEmail = "admin@cinemas.local";
    var admin = await userMgr.FindByEmailAsync(adminEmail);
    if (admin == null)
    {
        admin = new AppUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,
            FullName = "Administrator"
        };
        await userMgr.CreateAsync(admin, "Admin@123");
    }
    if (!await userMgr.IsInRoleAsync(admin, "Admin"))
        await userMgr.AddToRoleAsync(admin, "Admin");


    // Seed Membership Rank
    if (!await context.MembershipRanks.AnyAsync())
    {
        var ranks = new List<MembershipRank>
    {
        new MembershipRank
        {
            MembershipRankId = "MR00000001",
            Name = "Thành viên mới",
            RequirePoint = 0,
            MaxPoint = 999,
            TicketDiscountPercent = 0m,
            SnackDiscountPercent = 0m,
            PointMultiplier = 1.0m,
            OnlyNormalSeat = false,
            PriorityLevel = 1,
            Description = "Mua vé giá tiêu chuẩn, không ưu đãi",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new MembershipRank
        {
            MembershipRankId = "MR00000002",
            Name = "Thành viên Đồng",
            RequirePoint = 1000,
            MaxPoint = 2999,
            TicketDiscountPercent = 3m,
            SnackDiscountPercent = 0m,
            PointMultiplier = 1.0m,
            OnlyNormalSeat = true,
            PriorityLevel = 2,
            Description = "Giảm 3% giá vé (chỉ ghế thường)",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new MembershipRank
        {
            MembershipRankId = "MR00000003",
            Name = "Thành viên Bạc",
            RequirePoint = 3000,
            MaxPoint = 5999,
            TicketDiscountPercent = 5m,
            SnackDiscountPercent = 0m,
            PointMultiplier = 1.1m,
            OnlyNormalSeat = false,
            PriorityLevel = 3,
            Description = "Giảm 5% giá vé (mọi loại), nhân điểm x1.1",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new MembershipRank
        {
            MembershipRankId = "MR00000004",
            Name = "Thành viên Vàng",
            RequirePoint = 6000,
            MaxPoint = 9999,
            TicketDiscountPercent = 10m,
            SnackDiscountPercent = 2m,
            PointMultiplier = 1.2m,
            OnlyNormalSeat = false,
            PriorityLevel = 4,
            Description = "Giảm 10% vé, giảm 2% đồ ăn & nước, nhân điểm x1.2",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new MembershipRank
        {
            MembershipRankId = "MR00000005",
            Name = "Thành viên Kim Cương",
            RequirePoint = 10000,
            MaxPoint = 49999,
            TicketDiscountPercent = 15m,
            SnackDiscountPercent = 5m,
            PointMultiplier = 1.3m,
            OnlyNormalSeat = false,
            PriorityLevel = 5,
            Description = "Giảm 15% vé, giảm 5% đồ ăn & nước, nhân điểm x1.3",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new MembershipRank
        {
            MembershipRankId = "MR00000006",
            Name = "Thành viên Pha lê",
            RequirePoint = 50000,
            MaxPoint = 99999,
            TicketDiscountPercent = 30m,
            SnackDiscountPercent = 10m,
            PointMultiplier = 1.5m,
            OnlyNormalSeat = false,
            PriorityLevel = 6,
            Description = "Giảm 30% vé, giảm 10% đồ ăn & nước, nhân điểm x1.5",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }
    };

        await context.MembershipRanks.AddRangeAsync(ranks);
        await context.SaveChangesAsync();
    }


    if (!await context.SeatTypes.AnyAsync())
    {
        var seatTypes = new List<SeatTypes>
    {
        new SeatTypes { SeatTypeId = "ST001", Name = "NORMAL", Price = 75000 },
        new SeatTypes { SeatTypeId = "ST002", Name = "VIP", Price = 120000 },
        new SeatTypes { SeatTypeId = "ST003", Name = "COUPLE", Price = 200000 }
    };

        context.SeatTypes.AddRange(seatTypes);
        await context.SaveChangesAsync();
    }

}

/* pipeline */
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseSession();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();
app.UseCors("MobileCors");

app.UseAuthentication();
app.UseAuthorization();

//app.Use(async (context, next) =>
//{
//    // chỉ xử lý request vào trang root "/"
//    if (context.Request.Path == "/")
//    {
//        var user = context.User;

//        // đã đăng nhập + là Admin -> Admin/Index
//        if (user?.Identity?.IsAuthenticated == true && user.IsInRole("Admin"))
//        {
//            context.Response.Redirect("/Admin/Index");
//            return;
//        }

//        // còn lại -> Home/Index
//        context.Response.Redirect("/Home/Index");
//        return;
//    }

//    await next();
//});

app.MapStaticAssets();
app.MapRazorPages();
app.MapControllers();
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
