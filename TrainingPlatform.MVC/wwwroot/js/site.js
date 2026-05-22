(function () {
    if (!window.signalR) return;

    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/enrollment')
        .withAutomaticReconnect()
        .build();

    let startPromise = null;

    function startRealtime() {
        if (connection.state === signalR.HubConnectionState.Connected) {
            return Promise.resolve();
        }

        if (!startPromise) {
            startPromise = connection.start().catch(function (err) {
                startPromise = null;
                console.error(err.toString());
                throw err;
            });
        }

        return startPromise;
    }

    window.TrainingPlatformRealtime = {
        connection: connection,
        start: startRealtime
    };

    if (document.body.dataset.userAuthenticated === 'true') {
        connection.on('NotificationReceived', function (notification) {
            prependNotification(notification);
            showNotificationToast(notification);
        });
    }

    startRealtime().catch(function () {
        // Error is already logged in startRealtime.
    });

    function notificationValue(notification, name) {
        var camelName = name.charAt(0).toLowerCase() + name.slice(1);
        return notification[camelName] ?? notification[name] ?? '';
    }

    function formatNotificationDate(value) {
        var date = new Date(value);
        if (Number.isNaN(date.getTime())) return '';

        return date.toLocaleString([], {
            day: '2-digit',
            month: 'short',
            hour: '2-digit',
            minute: '2-digit'
        });
    }

    function prependNotification(notification) {
        const list = document.querySelector('[data-notification-list]');
        if (!list) return;

        const emptyState = document.querySelector('[data-notification-empty]');
        if (emptyState) emptyState.remove();

        const item = document.createElement('div');
        item.className = 'list-group-item list-group-item-light';

        const meta = document.createElement('div');
        meta.className = 'd-flex justify-content-between gap-2';

        const type = document.createElement('span');
        type.className = 'badge bg-secondary';
        type.textContent = notificationValue(notification, 'Type');

        const createdAt = document.createElement('small');
        createdAt.className = 'text-muted';
        createdAt.textContent = formatNotificationDate(notificationValue(notification, 'CreatedAt'));

        const message = document.createElement('p');
        message.className = 'mb-0 mt-2';
        message.textContent = notificationValue(notification, 'Message');

        meta.append(type, createdAt);
        item.append(meta, message);
        list.prepend(item);

        while (list.children.length > 10) {
            list.lastElementChild.remove();
        }
    }

    function showNotificationToast(notification) {
        const container = document.getElementById('notification-toast-container');
        if (!container || !window.bootstrap?.Toast) return;

        const toast = document.createElement('div');
        toast.className = 'toast bg-white border-0 shadow';
        toast.role = 'alert';
        toast.ariaLive = 'assertive';
        toast.ariaAtomic = 'true';

        const header = document.createElement('div');
        header.className = 'toast-header';

        const title = document.createElement('strong');
        title.className = 'me-auto';
        title.textContent = notificationValue(notification, 'Type') || 'Notification';

        const time = document.createElement('small');
        time.className = 'text-muted';
        time.textContent = 'Now';

        const close = document.createElement('button');
        close.type = 'button';
        close.className = 'btn-close';
        close.setAttribute('data-bs-dismiss', 'toast');
        close.setAttribute('aria-label', 'Close');

        const body = document.createElement('div');
        body.className = 'toast-body';
        body.textContent = notificationValue(notification, 'Message');

        header.append(title, time, close);
        toast.append(header, body);
        container.append(toast);

        const toastInstance = new bootstrap.Toast(toast, { delay: 5000 });
        toast.addEventListener('hidden.bs.toast', function () {
            toast.remove();
        });
        toastInstance.show();
    }
})();
