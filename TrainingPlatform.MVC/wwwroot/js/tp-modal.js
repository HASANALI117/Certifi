// Reusable popup for the dashboard. Elements with [data-tp-modal] open a shared modal that loads its content from the server. It sits under <body> so nothing can clip it.
(function () {
    if (window.TpModal) return;

    function modalEl() { return document.getElementById('tpModal'); }
    function contentEl() { return document.querySelector('[data-tp-modal-content]'); }
    function dialogEl() { return document.querySelector('[data-tp-modal-dialog]'); }

    function bsModal() {
        var el = modalEl();
        if (!el || !window.bootstrap) return null;
        return bootstrap.Modal.getOrCreateInstance(el);
    }

    function toast(type, message) {
        if (window.TpToast && message) window.TpToast.show({ type: type, message: message });
    }

    function rebindScripts(container) {
        container.querySelectorAll('script').forEach(function (oldScript) {
            var s = document.createElement('script');
            for (var i = 0; i < oldScript.attributes.length; i++) {
                s.setAttribute(oldScript.attributes[i].name, oldScript.attributes[i].value);
            }
            s.textContent = oldScript.textContent;
            oldScript.parentNode.replaceChild(s, oldScript);
        });
    }

    function setSize(size) {
        var dlg = dialogEl();
        if (!dlg) return;
        dlg.className = 'modal-dialog modal-dialog-centered modal-dialog-scrollable' + (size ? ' ' + size : '');
    }

    async function open(url, size) {
        var content = contentEl();
        if (!content) return;
        content.innerHTML =
            '<div class="modal-body text-center py-5 text-muted">' +
            '<div class="spinner-border" role="status" aria-hidden="true"></div>' +
            '<div class="mt-2 small">Loadingâ€¦</div></div>';
        setSize(size);
        var m = bsModal();
        if (m) m.show();
        try {
            var resp = await fetch(url, {
                headers: { 'X-Requested-With': 'XMLHttpRequest', 'X-Tp-Modal': 'true', 'Accept': 'text/html' },
                credentials: 'same-origin'
            });
            if (!resp.ok) { close(); window.location.href = url; return; }
            var html = await resp.text();
            content.innerHTML = html;
            rebindScripts(content);
        } catch (e) {
            close();
            window.location.href = url;
        }
    }

    function close() {
        var m = bsModal();
        if (m) m.hide();
    }

    async function submit(form) {
        var content = contentEl();
        var action = form.getAttribute('action') || window.location.pathname;
        var method = (form.getAttribute('method') || 'post').toUpperCase();
        try {
            var resp = await fetch(action, {
                method: method,
                body: new FormData(form),
                headers: { 'X-Requested-With': 'XMLHttpRequest', 'X-Tp-Modal': 'true' },
                credentials: 'same-origin'
            });
            var ct = resp.headers.get('content-type') || '';
            if (ct.indexOf('application/json') !== -1) {
                var data = await resp.json();
                if (data.ok) {
                    // If the server sends back a redirect, go there instead of just closing the popup.
                    if (data.redirect) {
                        window.location.assign(data.redirect);
                        return;
                    }
                    close();
                    toast(data.type || 'Success', data.message || 'Saved.');
                    if (typeof window.dashReload === 'function') window.dashReload();
                } else {
                    toast('Error', data.message || 'Action could not be completed.');
                }
                return;
            }
            // Non-JSON response = re-rendered form with validation errors.
            var html = await resp.text();
            if (content) {
                content.innerHTML = html;
                rebindScripts(content);
            }
        } catch (e) {
            toast('Error', 'Something went wrong. Please try again.');
        }
    }

    document.addEventListener('click', function (e) {
        var trigger = e.target.closest('[data-tp-modal]');
        if (!trigger) return;
        var url = trigger.getAttribute('data-tp-modal-url') || trigger.getAttribute('href');
        if (!url || url.charAt(0) === '#') return;
        e.preventDefault();
        open(url, trigger.getAttribute('data-tp-modal-size'));
    });

    document.addEventListener('submit', function (e) {
        var modal = modalEl();
        if (!modal) return;
        var form = e.target;
        if (form.tagName !== 'FORM' || !modal.contains(form)) return;
        if (form.dataset.tpModalSkip === 'true') return;
        e.preventDefault();
        submit(form);
    });

    window.TpModal = { open: open, close: close };
})();
