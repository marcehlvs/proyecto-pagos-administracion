document.addEventListener('DOMContentLoaded', function () {
    var switchEl = document.getElementById('temaSwitch');
    if (!switchEl) return;

    switchEl.checked = document.documentElement.getAttribute('data-bs-theme') === 'dark';

    switchEl.addEventListener('change', function () {
        var nuevoTema = switchEl.checked ? 'dark' : 'light';
        document.documentElement.setAttribute('data-bs-theme', nuevoTema);
        localStorage.setItem('tema', nuevoTema);
    });
});

document.addEventListener('DOMContentLoaded', function () {
    // Filtro de alumnos (Familias Create/Edit)
    var filtroAlumnoTexto = document.getElementById('filtroAlumnoTexto');
    if (filtroAlumnoTexto) {
        var filtroNivel = document.getElementById('filtroNivel');
        var filtroTurno = document.getElementById('filtroTurno');
        var itemsAlumnos = document.querySelectorAll('.alumno-item');

        function aplicarFiltroAlumnos() {
            var texto = filtroAlumnoTexto.value.toLowerCase();
            var nivel = filtroNivel.value;
            var turno = filtroTurno.value;
            itemsAlumnos.forEach(function (item) {
                var matchTexto = item.dataset.nombre.includes(texto);
                var matchNivel = !nivel || item.dataset.nivel === nivel;
                var matchTurno = !turno || item.dataset.turno === turno;
                item.style.display = (matchTexto && matchNivel && matchTurno) ? '' : 'none';
            });
        }

        filtroAlumnoTexto.addEventListener('input', aplicarFiltroAlumnos);
        filtroNivel.addEventListener('change', aplicarFiltroAlumnos);
        filtroTurno.addEventListener('change', aplicarFiltroAlumnos);
    }

    // Filtro de familias (Alumnos Create/Edit)
    var filtroFamiliaTexto = document.getElementById('filtroFamiliaTexto');
    if (filtroFamiliaTexto) {
        var itemsFamilias = document.querySelectorAll('.familia-item');
        filtroFamiliaTexto.addEventListener('input', function () {
            var texto = filtroFamiliaTexto.value.toLowerCase();
            itemsFamilias.forEach(function (item) {
                if (!item.dataset.email) return; // no ocultar "Sin asignar"
                item.style.display = item.dataset.email.includes(texto) ? '' : 'none';
            });
        });
    }
});