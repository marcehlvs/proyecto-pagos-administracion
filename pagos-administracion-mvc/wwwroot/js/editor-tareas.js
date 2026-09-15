// Editor enriquecido (TinyMCE, autohospedado vía CDN con licencia GPL — gratis, sin API key ni
// límite de uso) + dropzone de archivo, compartido entre Views/Tareas/Crear.cshtml (consigna del
// Docente) y Views/MisTareas/Index.cshtml (entrega del Alumno). Requiere que tinymce.min.js ya
// esté cargado en la página. Reemplaza a Summernote: sus dropdowns (color/tamaño/alineación)
// quedaban rotos con Bootstrap 5 y el proyecto está casi sin mantenimiento; TinyMCE tiene su
// propia UI (no depende de Bootstrap) y es más parecido a Word/Docs, más conocido para
// Docentes/Alumnos.
function initEditorTareas(textareaSelector) {
    tinymce.init({
        selector: textareaSelector,
        license_key: 'gpl', // autohospedado bajo GPLv2+: sin cuenta, sin API key, sin límite.
        language: 'es',
        language_url: 'https://cdn.jsdelivr.net/npm/tinymce-i18n@26/langs8/es.js',
        height: 220,
        menubar: false,
        statusbar: false,
        placeholder: 'Escribí acá...',
        plugins: 'lists link',
        toolbar: 'bold italic underline strikethrough | fontsize forecolor backcolor | ' +
                 'bullist numlist | alignleft aligncenter alignright alignjustify | link',
        // Las imágenes/PDF se adjuntan por el dropzone de abajo (quedan como adjunto
        // descargable), no pegadas sueltas adentro del texto.
        paste_data_images: false,
    });
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
