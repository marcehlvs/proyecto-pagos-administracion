// Editor enriquecido (Summernote) + dropzone de archivo, compartido entre
// Views/Tareas/Crear.cshtml (consigna del Docente) y Views/MisTareas/Index.cshtml (entrega del
// Alumno). Requiere que jQuery y Summernote ya estén cargados en la página.

// Bug conocido de summernote-bs5 0.8.20: sus botones con dropdown (color, tamaño de fuente,
// alineación/párrafo, listas) renderizan el atributo viejo data-toggle="dropdown", pero
// Bootstrap 5 solo reacciona a data-bs-toggle="dropdown" (Bootstrap 5 renombró todos los
// data-*). Por eso Bold/Italic/Underline andan bien (son toggles simples, no dependen de esto)
// y todo lo que abre un menú desplegable queda muerto. Ver
// https://github.com/summernote/summernote/issues/4603
function corregirDropdownsBs5(raiz) {
    if (!raiz || !raiz.querySelectorAll) return;
    raiz.querySelectorAll('[data-toggle="dropdown"]').forEach(function (el) {
        el.setAttribute('data-bs-toggle', 'dropdown');
    });
}

function initEditorTareas(textareaSelector) {
    var $el = $(textareaSelector);
    $el.summernote({
        height: 160,
        placeholder: 'Escribí acá...',
        lang: 'es-ES',
        toolbar: [
            ['style', ['bold', 'italic', 'underline', 'strikethrough']],
            ['fontsize', ['fontsize']],
            ['color', ['color']],
            ['para', ['ul', 'ol', 'paragraph']],
            ['insert', ['link']],
        ],
        // Las imágenes/PDF se adjuntan por el dropzone de abajo (quedan como adjunto
        // descargable), no pegadas sueltas adentro del texto.
        disableDragAndDrop: true,
        callbacks: {
            onInit: function () {
                // El toolbar ya está armado en este punto: primer pase sobre todo lo generado.
                corregirDropdownsBs5($el.next('.note-editor')[0]);
            },
        },
    });

    // Algunos paneles de Summernote (ej. "Más colores") se arman recién la primera vez que se
    // abren, no en el onInit — un MutationObserver los agarra apenas se insertan en el DOM, sin
    // depender de que Summernote avise por ningún callback.
    var contenedor = $el.next('.note-editor')[0];
    if (contenedor && window.MutationObserver) {
        new MutationObserver(function (mutaciones) {
            mutaciones.forEach(function (m) {
                m.addedNodes.forEach(function (nodo) {
                    if (nodo.nodeType !== 1) return;
                    if (nodo.matches && nodo.matches('[data-toggle="dropdown"]')) nodo.setAttribute('data-bs-toggle', 'dropdown');
                    corregirDropdownsBs5(nodo);
                });
            });
        }).observe(contenedor, { childList: true, subtree: true });
    }
}

// dropzoneSelector: contenedor con la clase .dropzone-archivo
// inputSelector: el <input type="file"> real, oculto adentro del dropzone
function initDropzoneArchivo(dropzoneSelector, inputSelector) {
    var dropzone = document.querySelector(dropzoneSelector);
    var input = document.querySelector(inputSelector);
    if (!dropzone || !input) return;

    var nombreEl = dropzone.querySelector('[data-nombre-archivo]');
    var textoInicial = nombreEl ? nombreEl.textContent : '';

    function actualizarNombre() {
        if (input.files && input.files.length > 0 && nombreEl) {
            nombreEl.textContent = input.files[0].name;
            dropzone.classList.add('dropzone-con-archivo');
        }
    }

    var linkBuscar = dropzone.querySelector('[data-buscar]');
    if (linkBuscar) {
        linkBuscar.addEventListener('click', function (e) {
            e.preventDefault();
            input.click();
        });
    }
    dropzone.addEventListener('click', function (e) {
        if (e.target === dropzone || e.target.closest('.dropzone-icono')) input.click();
    });

    input.addEventListener('change', actualizarNombre);

    ['dragover', 'dragleave', 'drop'].forEach(function (evt) {
        dropzone.addEventListener(evt, function (e) {
            e.preventDefault();
            e.stopPropagation();
        });
    });
    dropzone.addEventListener('dragover', function () { dropzone.classList.add('dropzone-activo'); });
    dropzone.addEventListener('dragleave', function () { dropzone.classList.remove('dropzone-activo'); });
    dropzone.addEventListener('drop', function (e) {
        dropzone.classList.remove('dropzone-activo');
        if (e.dataTransfer.files.length > 0) {
            input.files = e.dataTransfer.files;
            actualizarNombre();
        }
    });
}
