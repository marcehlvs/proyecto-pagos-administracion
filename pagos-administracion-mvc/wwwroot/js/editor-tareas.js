// Editor enriquecido (Summernote) + dropzone de archivo, compartido entre
// Views/Tareas/Crear.cshtml (consigna del Docente) y Views/MisTareas/Index.cshtml (entrega del
// Alumno). Requiere que jQuery y Summernote ya estén cargados en la página.

function initEditorTareas(textareaSelector) {
    $(textareaSelector).summernote({
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
