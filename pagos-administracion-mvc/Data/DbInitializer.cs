using Microsoft.AspNetCore.Identity;
using pagos_administracion_mvc.Models;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Data
{
    public static class DbInitializer
    {
        public static void Initialize(AdministracionDbContext context)
        {
            // Si ya hay alumnos cargados, no volvemos a sembrar
            if (context.Alumnos.Any())
                return;

            var alumnos = new List<Alumno>
            {
                new Alumno { Nombre = "Juan", Apellido = "Pérez", Dni = "40111222", Nivel = NivelEducativo.Primaria, GradoAnio = 3, Turno = Turno.Mañana },
                new Alumno { Nombre = "Sofía", Apellido = "Gómez", Dni = "41222333", Nivel = NivelEducativo.Primaria, GradoAnio = 5, Turno = Turno.Tarde },
                new Alumno { Nombre = "Lucas", Apellido = "Fernández", Dni = "42333444", Nivel = NivelEducativo.Secundaria, GradoAnio = 2, Turno = Turno.Mañana },
            };

            context.Alumnos.AddRange(alumnos);
            context.SaveChanges(); // necesitamos los Id generados antes de armar las cuotas

            const decimal montoMensual = 15000m;
            var anio = DateTime.Now.Year;

            foreach (var alumno in alumnos)
            {
                for (int mes = 1; mes <= 12; mes++)
                {
                    context.Cuotas.Add(new Cuota
                    {
                        AlumnoId = alumno.Id,
                        Mes = mes,
                        Anio = anio,
                        Monto = montoMensual,
                        FechaVencimiento = new DateTime(anio, mes, 10),
                        Estado = EstadoCuota.Pendiente
                    });
                }
            }

            context.SaveChanges();
        }



        public static async Task SeedRolesAdminAsync(IServiceProvider services)
        {
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
            string[] roles = { "Admin", "Familia" };

            foreach (var rol in roles)
            {
                if (!await roleManager.RoleExistsAsync(rol))
                    await roleManager.CreateAsync(new IdentityRole(rol));
            }
            const string adminEmail = "admin@escuela.com";
            if (await userManager.FindByEmailAsync(adminEmail) is null)
            {
                var admin = new IdentityUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(admin, "Admin123!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, "Admin");
                }
            }

        }
    }
}