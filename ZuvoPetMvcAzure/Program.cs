using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.SignalR;
using ZuvoPetMvcAzure.Data;
using ZuvoPetMvcAzure.Helpers;
using ZuvoPetMvcAzure.Repositories;
using ZuvoPetMvcAzure.Hubs;
using ZuvoPetMvcAzure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAntiforgery();
builder.Services.AddSingleton<HelperPathProvider>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<ServiceZuvoPet>();
builder.Services.AddTransient<IRepositoryZuvoPet, RepositoryZuvoPet>();

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    options.EnableEndpointRouting = false;
}).AddSessionStateTempDataProvider();
builder.Services.AddSignalR();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

string connectionString = builder.Configuration.GetConnectionString("ZuvoPet");

builder.Services.AddDbContext<ZuvoPetMvcAzureContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddAuthentication(options =>
{
    options.DefaultSignInScheme =
    CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultAuthenticateScheme =
    CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme =
    CookieAuthenticationDefaults.AuthenticationScheme;
}).AddCookie(
    CookieAuthenticationDefaults.AuthenticationScheme,
    config =>
    {
        config.LoginPath = "/Managed/Login";
        config.AccessDeniedPath = "/Managed/Denied";
        config.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        config.SlidingExpiration = true;
        config.Cookie.HttpOnly = true;
        config.ReturnUrlParameter = "returnUrl";
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Adoptante", policy => policy.RequireRole("Adoptante"));
    options.AddPolicy("Refugio", policy => policy.RequireRole("Refugio"));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Managed/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Managed}/{action=Landing}/{id?}")
    .WithStaticAssets();

app.MapHub<ChatHub>("/chatHub");
app.Run();