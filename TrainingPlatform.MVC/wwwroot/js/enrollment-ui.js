// Updates the live enrollment count on session cards.
(function () {
    if (window.EnrollmentUI) return;

    function el(id) { return document.getElementById(id); }

    function updateCounter(sessionId, enrolledCount, capacity, remainingSpots, isFull) {
        var counter = el('enrollment-counter-' + sessionId);
        if (counter) {
            var badgeClass, label;
            if (isFull) {
                badgeClass = 'bg-danger';
                label = 'Full';
            } else if (remainingSpots <= 5) {
                badgeClass = 'bg-warning text-dark';
                label = remainingSpots + ' spots left';
            } else {
                badgeClass = 'bg-success';
                label = remainingSpots + ' spots left';
            }
            counter.innerHTML =
                '<span class="badge ' + badgeClass + '">' + label + '</span>' +
                '<small class="text-muted d-block">(' + enrolledCount + '/' + capacity + ')</small>';
        }

        var btn = el('enroll-btn-' + sessionId);
        if (btn) {
            btn.disabled = !!isFull;
            btn.classList.toggle('btn-secondary', !!isFull);
            btn.classList.toggle('btn-primary', !isFull);
            btn.textContent = isFull ? 'Session Full' : 'Enroll Now';
        }

        // Generic data-bound elements (used on session detail pages, dashboards, etc.)
        document.querySelectorAll('[data-session-remaining="' + sessionId + '"]').forEach(function (n) {
            n.textContent = remainingSpots;
        });
        document.querySelectorAll('[data-session-enrolled="' + sessionId + '"]').forEach(function (n) {
            n.textContent = enrolledCount;
        });
        document.querySelectorAll('[data-session-capacity="' + sessionId + '"]').forEach(function (n) {
            n.textContent = capacity;
        });
        document.querySelectorAll('[data-session-card="' + sessionId + '"]').forEach(function (card) {
            card.classList.toggle('is-full', !!isFull);
        });
    }

    window.EnrollmentUI = { updateCounter: updateCounter };

    // Auto-wire: if realtime is loaded, subscribe so cards update without per-view JS.
    function subscribe() {
        if (!window.TrainingPlatformRealtime) return;
        var conn = window.TrainingPlatformRealtime.connection;
        if (conn.__enrollmentUIBound) return;
        conn.__enrollmentUIBound = true;
        conn.on('EnrollmentUpdated', function (d) {
            updateCounter(d.courseSessionId, d.enrolledCount, d.capacity, d.remainingSpots, d.isFull);
        });
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', subscribe);
    } else {
        subscribe();
    }
})();
