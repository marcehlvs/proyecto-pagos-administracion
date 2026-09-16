document.addEventListener('DOMContentLoaded', function () {
    // Dropdowns de "Acciones" dentro de <div class="table-responsive">: el div recorta
    // (overflow-x: auto) el menú cuando se abre cerca del borde inferior de la tabla, así que
    // hay que scrollear para ver las últimas opciones. Bootstrap posiciona el menú con Popper
    // en "absolute" por defecto (queda atado al contenedor con overflow); con "fixed" el menú se
    // posiciona respecto a la ventana y ya no lo recorta el contenedor. Se activa acá, en vez de
    // repetir JS por vista, para cualquier botón marcado con data-bs-strategy="fixed" (Cursos,
    // Cuotas, Alumnos, Pagos, Familias, y el que se agregue después).
    document.querySelectorAll('[data-bs-toggle="dropdown"][data-bs-strategy="fixed"]').forEach(function (boton) {
        bootstrap.Dropdown.getOrCreateInstance(boton, {
            popperConfig: function (configPorDefecto) {
                return Object.assign({}, configPorDefecto, { strategy: 'fixed' });
            }
        });
    });

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

// ── Buscador global de alumnos (navbar) ──────────────────────────────────────
document.addEventListener('DOMContentLoaded', function () {
    var input     = document.getElementById('buscadorGlobal');
    var lista     = document.getElementById('buscadorResultados');
    if (!input || !lista) return;   // solo existe para Admin; en otros roles no hace nada

    var timerDebounce  = null;
    var controlAbort   = null;   // AbortController del fetch en vuelo
    var indiceFocusado = -1;     // índice del ítem con foco de teclado (-1 = ninguno)

    // ── helpers ───────────────────────────────────────────────────────────────

    function mostrarLista() { lista.style.display = 'block'; }
    function ocultarLista() {
        lista.style.display = 'none';
        lista.innerHTML = '';
        indiceFocusado = -1;
    }

    function itemsVisibles() {
        return Array.from(lista.querySelectorAll('li[data-id]'));
    }

    function resaltarItem(nuevoIndice) {
        var items = itemsVisibles();
        items.forEach(function (li, i) {
            li.classList.toggle('buscador-foco', i === nuevoIndice);
        });
        indiceFocusado = nuevoIndice;
    }

    function navegarA(id) {
        window.location.href = '/Alumnos/Details/' + id;
    }

    // ── lógica de fetch ───────────────────────────────────────────────────────

    function ejecutarBusqueda(q) {
        // Cancelar fetch anterior si todavía está en vuelo
        if (controlAbort) controlAbort.abort();
        controlAbort = new AbortController();

        fetch('/Alumnos/Buscar?q=' + encodeURIComponent(q), {
            signal: controlAbort.signal
        })
        .then(function (res) {
            if (!res.ok) throw new Error('HTTP ' + res.status);
            return res.json();
        })
        .then(function (alumnos) {
            lista.innerHTML = '';
            indiceFocusado  = -1;

            if (!alumnos.length) {
                var li = document.createElement('li');
                li.className = 'buscador-vacio';
                li.textContent = 'Sin resultados';
                lista.appendChild(li);
                mostrarLista();
                return;
            }

            alumnos.forEach(function (a) {
                var li = document.createElement('li');
                li.dataset.id = a.id;

                // Nombre + DNI en dos líneas para lectura rápida
                var nombre = document.createElement('span');
                nombre.className = 'buscador-nombre';
                nombre.textContent = a.apellido + ', ' + a.nombre;

                var dni = document.createElement('span');
                dni.className = 'buscador-dni';
                dni.textContent = 'DNI ' + a.dni;

                li.appendChild(nombre);
                li.appendChild(dni);

                li.addEventListener('mousedown', function (e) {
                    // mousedown en vez de click: evita que el blur del input
                    // oculte la lista antes de que el click se registre.
                    e.preventDefault();
                    navegarA(a.id);
                });

                lista.appendChild(li);
            });

            mostrarLista();
        })
        .catch(function (err) {
            // AbortError es el caso normal (nueva tecla canceló el fetch anterior).
            // Cualquier otro error de red simplemente oculta la lista en silencio
            // para no romper el navbar.
            if (err.name !== 'AbortError') ocultarLista();
        });
    }

    // ── eventos del input ─────────────────────────────────────────────────────

    input.addEventListener('input', function () {
        var q = input.value.trim();
        clearTimeout(timerDebounce);

        if (q.length < 2) {
            if (controlAbort) controlAbort.abort();
            ocultarLista();
            return;
        }

        // Debounce: espera 250 ms de silencio antes de mandar la petición
        timerDebounce = setTimeout(function () { ejecutarBusqueda(q); }, 250);
    });

    // Navegación con teclado dentro del dropdown
    input.addEventListener('keydown', function (e) {
        var items = itemsVisibles();

        if (e.key === 'Escape') {
            ocultarLista();
            input.blur();
            return;
        }

        if (e.key === 'ArrowDown') {
            e.preventDefault();
            resaltarItem(Math.min(indiceFocusado + 1, items.length - 1));
            return;
        }

        if (e.key === 'ArrowUp') {
            e.preventDefault();
            resaltarItem(Math.max(indiceFocusado - 1, 0));
            return;
        }

        if (e.key === 'Enter' && indiceFocusado >= 0 && items[indiceFocusado]) {
            e.preventDefault();
            navegarA(items[indiceFocusado].dataset.id);
        }
    });

    // Ocultar la lista cuando el input pierde el foco (salvo mousedown en ítem)
    input.addEventListener('blur', function () {
        // Pequeño delay para que el mousedown del ítem se procese primero
        setTimeout(ocultarLista, 150);
    });

    // Click fuera del buscador cierra la lista
    document.addEventListener('click', function (e) {
        if (!input.contains(e.target) && !lista.contains(e.target)) {
            ocultarLista();
        }
    });
});