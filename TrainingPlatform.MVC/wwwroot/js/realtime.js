// SignalR singleton for the dashboard. Survives AJAX fragment swaps in _DashboardLayout.
(function () {
    if (window.TrainingPlatformRealtime) return;
    if (typeof signalR === 'undefined') {
        console.warn('[realtime] signalR client not loaded');
        return;
    }

    function nowHms() {
        var d = new Date();
        return d.toLocaleTimeString(undefined, { hour12: false });
    }

    function setStatus(state, eventLabel) {
        var pill = document.querySelector('[data-live-pill]');
        if (!pill) return;
        var dot = pill.querySelector('[data-live-dot]');
        var label = pill.querySelector('[data-live-label]');
        var time = pill.querySelector('[data-live-time]');
        if (state) {
            pill.dataset.state = state;
            if (dot) dot.className = 'tp-live-dot tp-live-' + state;
        }
        if (label) label.textContent = eventLabel || pill.dataset.lastLabel || (state === 'connected' ? 'Live' : state);
        if (eventLabel) pill.dataset.lastLabel = eventLabel;
        if (time) time.textContent = nowHms();
    }

    function pushLogRow(type, message) {
        var feed = document.querySelector('[data-live-feed]');
        if (!feed) return;
        var empty = feed.querySelector('[data-live-feed-empty]');
        if (empty) empty.remove();
        var row = document.createElement('div');
        row.className = 'tp-live-feed-row';
        row.innerHTML =
            '<span class="tp-live-feed-time">' + nowHms() + '</span>' +
            '<span class="badge bg-secondary tp-live-feed-type">' + escapeHtml(type) + '</span>' +
            '<span class="tp-live-feed-msg">' + escapeHtml(message) + '</span>';
        feed.insertBefore(row, feed.firstChild);
        while (feed.children.length > 20) feed.removeChild(feed.lastChild);
    }

    var connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/enrollment')
        .withAutomaticReconnect()
        .build();

    connection.onreconnecting(function () { setStatus('reconnecting', 'Reconnecting'); });
    connection.onreconnected(function () { setStatus('connected', 'Live'); });
    connection.onclose(function () { setStatus('offline', 'Offline'); });

    var startPromise = null;
    function start() {
        if (startPromise) return startPromise;
        setStatus('connecting', 'Connecting');
        startPromise = connection.start().then(function () {
            setStatus('connected', 'Live');
        }).catch(function (err) {
            startPromise = null;
            setStatus('offline', 'Offline');
            throw err;
        });
        return startPromise;
    }

    // ---------- Notification UI ----------
    var TYPE_ICON = {
        Enrollment: 'bi-mortarboard-fill',
        Payment: 'bi-cash-coin',
        Assessment: 'bi-clipboard-check-fill',
        Certification: 'bi-patch-check-fill',
        Success: 'bi-check-circle-fill',
        Error: 'bi-exclamation-triangle-fill',
        Info: 'bi-info-circle-fill'
    };
    var TYPE_BG = {
        Enrollment: 'bg-primary',
        Payment: 'bg-success',
        Assessment: 'bg-info text-dark',
        Certification: 'bg-warning text-dark',
        Success: 'bg-success',
        Error: 'bg-danger',
        Info: 'bg-info text-dark'
    };

    function token() {
        var el = document.querySelector('input[name="__RequestVerificationToken"]');
        return el ? el.value : '';
    }

    function setUnread(count) {
        var badge = document.querySelector('[data-notif-badge]');
        if (!badge) return;
        if (count > 0) {
            badge.textContent = count > 99 ? '99+' : String(count);
            badge.classList.remove('d-none');
        } else {
            badge.textContent = '0';
            badge.classList.add('d-none');
        }
    }

    function formatTime(iso) {
        try {
            var d = new Date(iso);
            return d.toLocaleString(undefined, { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' });
        } catch (e) { return ''; }
    }

    function renderRow(n) {
        var icon = TYPE_ICON[n.type] || 'bi-bell-fill';
        var bg = TYPE_BG[n.type] || 'bg-secondary';
        var unreadCls = n.isRead ? '' : 'tp-notif-unread';
        return '' +
            '<a href="#" class="list-group-item list-group-item-action ' + unreadCls + '" data-notif-id="' + n.id + '">' +
                '<div class="d-flex gap-2 align-items-start">' +
                    '<span class="badge ' + bg + ' tp-notif-icon"><i class="bi ' + icon + '"></i></span>' +
                    '<div class="flex-grow-1">' +
                        '<div class="small fw-semibold">' + escapeHtml(n.type || 'Alert') + '</div>' +
                        '<div class="small">' + escapeHtml(n.message || '') + '</div>' +
                        '<div class="text-muted" style="font-size:.7rem">' + formatTime(n.createdAt) + '</div>' +
                    '</div>' +
                '</div>' +
            '</a>';
    }

    function escapeHtml(s) {
        return String(s).replace(/[&<>"']/g, function (c) {
            return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c];
        });
    }

    function renderList(items) {
        var list = document.querySelector('[data-notif-list]');
        if (!list) return;
        if (!items || !items.length) {
            list.innerHTML = '<div class="list-group-item text-muted small text-center py-3">No notifications yet.</div>';
            return;
        }
        list.innerHTML = items.map(renderRow).join('');
    }

    function prependItem(n) {
        var list = document.querySelector('[data-notif-list]');
        if (!list) return;
        if (list.querySelector('.text-muted.text-center')) list.innerHTML = '';
        list.insertAdjacentHTML('afterbegin', renderRow(n));
        // Trim to 15 visible
        while (list.children.length > 15) list.removeChild(list.lastChild);
    }

    function showToast(n) {
        var container = document.querySelector('[data-toast-container]');
        if (!container) return;
        var icon = TYPE_ICON[n.type] || 'bi-bell-fill';
        var bg = TYPE_BG[n.type] || 'bg-secondary';
        var toastEl = document.createElement('div');
        toastEl.className = 'toast align-items-center border-0 mb-2';
        toastEl.setAttribute('role', 'alert');
        toastEl.innerHTML =
            '<div class="d-flex">' +
                '<div class="toast-body d-flex gap-2 align-items-start">' +
                    '<span class="badge ' + bg + '"><i class="bi ' + icon + '"></i></span>' +
                    '<div class="flex-grow-1">' +
                        '<div class="d-flex justify-content-between gap-2">' +
                            '<span class="fw-semibold small">' + escapeHtml(n.type || 'Alert') + '</span>' +
                            '<span class="text-muted" style="font-size:.7rem">' + nowHms() + '</span>' +
                        '</div>' +
                        '<div class="small">' + escapeHtml(n.message || '') + '</div>' +
                    '</div>' +
                '</div>' +
                '<button type="button" class="btn-close me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>' +
            '</div>';
        container.appendChild(toastEl);
        if (window.bootstrap && bootstrap.Toast) {
            var t = new bootstrap.Toast(toastEl, { delay: 6000 });
            t.show();
            toastEl.addEventListener('hidden.bs.toast', function () { toastEl.remove(); });
        } else {
            setTimeout(function () { toastEl.remove(); }, 6000);
        }
    }

    async function loadRecent() {
        try {
            var resp = await fetch('/api/notifications/recent?take=15', {
                headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'application/json' },
                credentials: 'same-origin'
            });
            if (!resp.ok) return;
            var data = await resp.json();
            renderList(data.items);
            setUnread(data.unread);
        } catch (e) { /* ignore */ }
    }

    async function markRead(id) {
        try {
            var body = new FormData();
            if (id) body.append('id', id);
            var t = token();
            var resp = await fetch('/api/notifications/mark-read', {
                method: 'POST',
                body: body,
                headers: { 'X-Requested-With': 'XMLHttpRequest', 'RequestVerificationToken': t },
                credentials: 'same-origin'
            });
            if (!resp.ok) return;
            var data = await resp.json();
            setUnread(data.unread);
        } catch (e) { /* ignore */ }
    }

    // Debounced soft-refresh of the current dashboard fragment.
    // Only fires when the current page opted in via [data-tp-live-page].
    var refreshTimer = null;
    function softRefresh(payload) {
        var marker = document.querySelector('[data-tp-live-page]');
        var src = (payload && payload.source) || 'server';
        setStatus('connected', 'Refresh: ' + src);
        pushLogRow('Refresh', src + (marker ? ' → ' + marker.dataset.tpLivePage : ' (no marker)'));
        if (!marker) return;
        if (typeof window.dashReload !== 'function') return;
        clearTimeout(refreshTimer);
        refreshTimer = setTimeout(function () { window.dashReload(); }, 250);
    }

    connection.on('DashboardRefreshRequested', softRefresh);

    connection.on('EnrollmentUpdated', function (d) {
        setStatus('connected', 'Seat update');
        pushLogRow('Seats', 'Session ' + d.courseSessionId + ' → ' + d.enrolledCount + '/' + d.capacity + (d.isFull ? ' (FULL)' : ''));
    });

    // Hub event
    connection.on('NotificationReceived', function (n) {
        setStatus('connected', (n && n.type) || 'Notification');
        pushLogRow(n && n.type || 'Notif', (n && n.message) || '');
        n = n || {};
        // Normalize: SignalR PascalCase → camelCase already done by client.
        prependItem(n);
        showToast(n);
        var badge = document.querySelector('[data-notif-badge]');
        var current = badge ? parseInt(badge.textContent, 10) : 0;
        if (isNaN(current)) current = 0;
        setUnread(current + 1);
    });

    // Mark-read clicks (delegated, survives AJAX swaps)
    document.addEventListener('click', function (e) {
        var row = e.target.closest('[data-notif-id]');
        if (row && row.classList.contains('tp-notif-unread')) {
            row.classList.remove('tp-notif-unread');
            markRead(parseInt(row.dataset.notifId, 10));
        }
        var clearBtn = e.target.closest('[data-notif-clear]');
        if (clearBtn) {
            e.preventDefault();
            markRead(null);
            document.querySelectorAll('[data-notif-list] .tp-notif-unread').forEach(function (n) {
                n.classList.remove('tp-notif-unread');
            });
            // Dismiss every visible toast popup in one click.
            document.querySelectorAll('[data-toast-container] .toast').forEach(function (t) {
                if (window.bootstrap && bootstrap.Toast) {
                    bootstrap.Toast.getOrCreateInstance(t).hide();
                } else {
                    t.remove();
                }
            });
        }
    });

    window.TrainingPlatformRealtime = {
        connection: connection,
        start: start,
        loadRecent: loadRecent,
        markRead: markRead
    };

    // Public toast helper — used by _DashAlerts.cshtml for TempData flash messages
    // so server-rendered errors look identical to SignalR notifications.
    window.TpToast = {
        show: function (opts) {
            opts = opts || {};
            showToast({
                type: opts.type || 'Notice',
                message: opts.message || ''
            });
        }
    };

    // Auto-start and load initial state once.
    document.addEventListener('DOMContentLoaded', function () {
        start().then(loadRecent).catch(function () { /* already logged */ });
    });

    // Reload recent after AJAX page swaps (in case list markup re-rendered).
    window.addEventListener('tp:navigated', loadRecent);
})();
