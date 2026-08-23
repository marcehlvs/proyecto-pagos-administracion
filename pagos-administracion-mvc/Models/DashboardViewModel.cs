using System.ComponentModel.DataAnnotations;

namespace pagos_administracion_mvc.Models

{
    public class DashboardViewModel
    {
        public decimal RecaudacionMes { get; set; }
        public int MesActual { get; set; }
        [Display(Name = "Año Actual")]
        public int AnioActual { get; set; }
        [Display(Name = "Cuotas Pagadas")]
        public int CuotasPagadas { get; set; }
        [Display(Name = "Cuotas Pendientes")]
        public int CuotasPendientes { get; set; }
        [Display(Name = "Cuotas Vencidas")]
        public int CuotasVencidas { get; set; }
        [Display(Name = "Cuotas Parciales")]    
        public int CuotasParciales { get; set; }
        [Display(Name = "Total Cuotas Mes")]
        public int TotalCuotasMes { get; set; }

        public decimal PorcentajeMorosidad { get; set; }
        [Display(Name = "Total Cuotas Historico")]
        public int TotalCuotasHistorico { get; set; }
        [Display(Name = "Total Cuotas Vencidas Historico")]
        public int TotalVencidasHistorico { get; set; }
    }
}
