using CinemaS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Kết nối đến DB CinemaS
builder.Services.AddDbContext<CinemaContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CinemaS")));

// Identity dùng CinemaContext
builder.Services
    .AddIdentity<AppUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = true; // bật confirm email nếu muốn
        options.Password.RequireDigit = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<CinemaContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

/* --- Seeding Role + Admin (khuyên dùng) --- */
using (var scope = app.Services.CreateScope())
{
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
        admin = new AppUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true, FullName = "Administrator" };
        await userMgr.CreateAsync(admin, "Admin@123"); // đổi khi production
    }
    if (!await userMgr.IsInRoleAsync(admin, "Admin"))
        await userMgr.AddToRoleAsync(admin, "Admin");
    if (!await userMgr.IsInRoleAsync(admin, "User"))
        await userMgr.AddToRoleAsync(admin, "User");
}

/* --- pipeline --- */
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

app.MapStaticAssets(); // nếu dùng
app.MapRazorPages();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Movies}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
