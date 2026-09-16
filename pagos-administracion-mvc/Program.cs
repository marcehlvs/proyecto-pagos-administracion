using MercadoPago.Config;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Services;
using QuestPDF.Infrastructure;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
QuestPDF.Settings.License = LicenseType.Community;

// Registro de Servicios
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
// Necesario para que AdministracionDbContext pueda resolver el usuario HTTP actual
// en SaveChangesAsync y atribuir cada AuditLog al autor correcto.
builder.Services.AddHttpContextAccessor();

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
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ConversacionAsistenteStore>();
builder.Services.AddScoped<PagoIniciadorService>();
builder.Services.AddScoped<MercadoPagoService>();
builder.Services.AddScoped<NotaCalculadora>();
builder.Services.AddScoped<BoletinService>();
builder.Services.AddScoped<IBoletinPlantilla, BoletinPlantillaPredeterminada>();
// Plantilla RITE (Fase 1, ver Services/BoletinPlantillaRite.cs): se registra por su tipo
// concreto además de la interfaz de arriba, para poder tener las dos disponibles a la vez y que
// BoletinesController elija cuál generar.
builder.Services.AddScoped<BoletinPlantillaRite>();
builder.Services.AddScoped<AsistenteService>();
builder.Services.AddHostedService<RevisorVencimientosService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, IdentityEmailSenderAdapter>();

// Rate limiting: protección del endpoint de webhook de Mercado Pago contra abuso.
// Límite: 60 requests/minuto por IP (el tráfico legítimo de MP es mucho menor).
// Las peticiones en cola (hasta 2) se procesan en orden; el resto recibe 429.
builder.Services.AddRateLimiter(options =>
{
    options.AddSlidingWindowLimiter("webhook-mp", limiterOptions =>
    {
        limiterOptions.PermitLimit         = 60;
        limiterOptions.Window              = TimeSpan.FromMinutes(1);
        limiterOptions.SegmentsPerWindow   = 6;  // ventanas de 10 seg
        limiterOptions.QueueLimit          = 2;
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

// Configuración de SDKs y Claves de API
// Falla rápido al arrancar si el token no está configurado: es mejor que una excepción
// críptica en el primer intento de pago en producción.
MercadoPagoConfig.AccessToken = builder.Configuration["MercadoPago:AccessToken"]
    ?? throw new InvalidOperationException(
        "MercadoPago:AccessToken no está configurado. " +
        "Usá dotnet user-secrets en desarrollo o la variable de entorno " +
        "MercadoPago__AccessToken en producción.");

// Middlewares de HTTP Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Security headers: aplicados a todas las respuestas HTTP.
// - X-Frame-Options: previene clickjacking (la app no puede ser embebida en iframes de otros dominios).
// - X-Content-Type-Options: impide que el browser reinterprete la extensión/Content-Type del archivo.
// - Referrer-Policy: no filtra la URL de la app a sitios externos en el header Referer.
// - Content-Security-Policy: política restrictiva; ajustar si se agregan fuentes CDN adicionales.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Frame-Options"]           = "DENY";
    context.Response.Headers["X-Content-Type-Options"]    = "nosniff";
    context.Response.Headers["Referrer-Policy"]           = "strict-origin-when-cross-origin";
    context.Response.Headers["Content-Security-Policy"]   =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://sdk.mercadopago.com; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data:; " +
        "frame-src https://sdk.mercadopago.com; " +
        "connect-src 'self';";
    await next();
});

app.UseRouting();
app.UseRateLimiter();

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