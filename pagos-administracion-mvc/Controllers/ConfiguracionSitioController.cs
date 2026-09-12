using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using pagos_administracion_mvc.Models;

namespace pagos_administracion_mvc.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ConfiguracionSitioController : Controller
    {
        private readonly AdministracionDbContext _context;

        public ConfiguracionSitioController(AdministracionDbContext context)
        {
            _context = context;
        }

        // GET: ConfiguracionSitio/Edit
        // Es una fila única (Id=1): si por algún motivo no está (DB nueva sin la migración de
        // seed corrida todavía), la creamos con los valores por defecto en vez de dar 404.
        public async Task<IActionResult> Edit()
        {
            var config = await _context.ConfiguracionSitio.FirstOrDefaultAsync(c => c.Id == 1);
            if (config == null)
            {
                config = new ConfiguracionSitio { Id = 1 };
                _context.Add(config);
                await _context.SaveChangesAsync();
            }
            return View(config);
        }

        // POST: ConfiguracionSitio/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([Bind("ColorPrimario,ColorPrimarioOscuro,ColorExito,ColorAdvertencia,NombrePreset,Distrito")] ConfiguracionSitio form)
        {
            if (!ModelState.IsValid)
            {
                form.Id = 1;
                return View(form);
            }

            var config = await _context.ConfiguracionSitio.FirstOrDefaultAsync(c => c.Id == 1)
                ?? new ConfiguracionSitio { Id = 1 };

            config.ColorPrimario = form.ColorPrimario;
            config.ColorPrimarioOscuro = form.ColorPrimarioOscuro;
            config.ColorExito = form.ColorExito;
            config.ColorAdvertencia = form.ColorAdvertencia;
            config.NombrePreset = form.NombrePreset;
            config.Distrito = form.Distrito;
            config.ModificadaPorNombre = User.Identity?.Name;
            config.FechaModificacion = DateTime.Now;

            if (_context.Entry(config).State == EntityState.Detached)
                _context.Add(config);

            await _context.SaveChangesAsync();

            TempData["Resultado"] = "Estilo del sitio actualizado.";
            return RedirectToAction(nameof(Edit));
        }
    }
}
