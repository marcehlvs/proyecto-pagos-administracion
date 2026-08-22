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