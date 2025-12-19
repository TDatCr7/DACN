using CinemaS.Models;
using CinemaS.Models.Email;
using CinemaS.Models.Payments;
using CinemaS.Models.Settings;
using CinemaS.Services;
using CinemaS.VNPAY;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;

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
        options.SignIn.RequireConfirmedAccount = true;
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
        var rank = new MembershipRank
        {
            MembershipRankId = "MR00000001",
            Name = "Basic",
            RequirePoint = 0,
            PointReturnTicket = 0,
            PointReturnCombo = 0,
            PriorityLevel = 1,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        await context.MembershipRanks.AddAsync(rank);
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
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
