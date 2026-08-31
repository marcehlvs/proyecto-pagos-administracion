using MercadoPago.Config;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Services;

var builder = WebApplication.CreateBuilder(args);

// Registro de Servicios
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddDbContext<AdministracionDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DbConnection")));

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
});

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedEmail = false;
    // Política reforzada: antes permitía passwords de 6 caracteres sin mayúsculas ni símbolos.
    // GenerarPasswordTemporal (FamiliasController) ya genera claves de 10 caracteres con
    // las 4 clases de carácter, así que el alta de familias no se ve afectada.
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AdministracionDbContext>();

builder.Services.AddScoped<MercadoPagoService>();
builder.Services.AddHostedService<RevisorVencimientosService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, IdentityEmailSenderAdapter>();

var app = builder.Build();

// Configuración de SDKs y Claves de API
MercadoPagoConfig.AccessToken = builder.Configuration["MercadoPago:AccessToken"];

// Middlewares de HTTP Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

//Seeding / Migraciones de Base de Datos
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AdministracionDbContext>();
        //DbInitializer.Initialize(context);
        await DbInitializer.SeedRolesAdminAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocurrió un error al ejecutar la siembra de la base de datos.");
    }
}

app.Run();