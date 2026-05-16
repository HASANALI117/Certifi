const EnrollmentUI = {
    updateCounter: function (sessionId, enrolled, capacity, remaining, isFull) {
        const counterEl = document.getElementById('enrollment-counter-' + sessionId);
        if (!counterEl) return;

        if (isFull) {
            counterEl.innerHTML = '<span class="badge bg-danger">Full</span> <small class="text-muted">(' + enrolled + '/' + capacity + ')</small>';
        } else {
            var badgeClass = remaining <= 5 ? 'bg-warning text-dark' : 'bg-success';
            counterEl.innerHTML =
                '<span class="badge ' + badgeClass + '">' +
                remaining + ' spots left</span> ' +
                '<small class="text-muted">(' + enrolled + '/' + capacity + ')</small>';
        }

        const btn = document.getElementById('enroll-btn-' + sessionId);
        if (btn) {
            btn.disabled = isFull;
            if (isFull) {
                btn.textContent = 'Session Full';
                btn.classList.remove('btn-primary');
                btn.classList.add('btn-secondary');
            } else {
                // FIX: Restore button when spots become available
                btn.textContent = 'Enroll Now';
                btn.classList.remove('btn-secondary');
                btn.classList.add('btn-primary');
            }
        }
    }
};