// Auto-hide messages after 5 seconds
document.addEventListener('DOMContentLoaded', function() {
    setTimeout(function() {
        const alerts = document.querySelectorAll('.alert');
        alerts.forEach(function(alert) {
            alert.style.animation = 'slideIn 0.3s ease reverse';
            setTimeout(() => alert.remove(), 300);
        });
    }, 5000);
});

// Function to update reservation status
async function updateReservationStatus(reservationId, status) {
    // Show confirmation dialog
    const statusText = status === 'checked-in' ? 'check-in' : 'cancel';
    const confirmMessage = `Are you sure you want to ${statusText} this reservation?`;

    if (!confirm(confirmMessage)) {
        return;
    }

    try {
        // Disable the buttons to prevent double-clicking
        const reservationRow = document.getElementById(`reservation-${reservationId}`);
        const buttons = reservationRow.querySelectorAll('.btn-action');
        buttons.forEach(btn => {
            btn.disabled = true;
            btn.style.opacity = '0.6';
        });

        // Send AJAX request to update status
        const response = await fetch('/Home/UpdateReservationStatus', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
            },
            body: JSON.stringify({
                reservationId: reservationId,
                status: status
            })
        });

        const result = await response.json();

        if (result.success) {
            // Animate row removal
            reservationRow.style.transition = 'all 0.5s ease';
            reservationRow.style.opacity = '0';
            reservationRow.style.transform = 'translateX(-100%)';

            setTimeout(() => {
                reservationRow.remove();

                // Check if no reservations left
                const remainingRows = document.querySelectorAll('.reservation-row');
                if (remainingRows.length === 0) {
                    location.reload(); // Reload to show "no reservations" message
                }
            }, 500);

            // Show success message
            showMessage('success', result.message || `Reservation ${statusText} successfully!`);
        } else {
            // Re-enable buttons on error
            buttons.forEach(btn => {
                btn.disabled = false;
                btn.style.opacity = '1';
            });
            showMessage('error', result.message || 'An error occurred while updating the reservation.');
        }
    } catch (error) {
        console.error('Error:', error);
        // Re-enable buttons on error
        const reservationRow = document.getElementById(`reservation-${reservationId}`);
        const buttons = reservationRow.querySelectorAll('.btn-action');
        buttons.forEach(btn => {
            btn.disabled = false;
            btn.style.opacity = '1';
        });
        showMessage('error', 'A network error occurred. Please try again.');
    }
}

// Function to confirm staff deletion
function confirmDeleteStaff(staffId, staffName) {
    window.location.href = `/Home/DeleteStaff/${staffId}`;
}

// Function to show dynamic messages
function showMessage(type, message) {
    const messageContainer = document.querySelector('.message-container');
    const alertDiv = document.createElement('div');
    alertDiv.className = `alert alert-${type === 'success' ? 'success' : 'danger'}`;
    alertDiv.innerHTML = `<strong>${type === 'success' ? 'Success!' : 'Error!'}</strong> ${message}`;

    messageContainer.appendChild(alertDiv);

    // Auto-remove after 5 seconds
    setTimeout(() => {
        alertDiv.style.animation = 'slideIn 0.3s ease reverse';
        setTimeout(() => alertDiv.remove(), 300);
    }, 5000);
}