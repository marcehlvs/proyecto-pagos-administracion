using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pagos_administracion_mvc.Data;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.ViewComponents
{
    public class PagosPendientesBadgeViewComponent : ViewComponent
    {
        private readonly AdministracionDbContext _context;

        public PagosPendientesBadgeViewComponent(AdministracionDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var cantidad = await _context.Pagos.CountAsync(p => p.Estado == EstadoPago.EnRevision);
            return View(cantidad);
        }
    }
}