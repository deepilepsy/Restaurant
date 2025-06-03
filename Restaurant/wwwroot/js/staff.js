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

function updateReservationStatus(reservationId, status) {
    if (confirm(`Are you sure you want to ${status.replace('-', ' ')} this reservation?`)) {
        // Create form data
        const formData = new FormData();
        formData.append('reservationId', reservationId);
        formData.append('status', status);

        fetch('/Home/UpdateReservationStatus', {
            method: 'POST',
            body: formData
        })
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    location.reload(); // Refresh the page to show updated status
                } else {
                    alert('Error: ' + data.message);
                }
            })
            .catch(error => {
                console.error('Error:', error);
                alert('An error occurred while updating the reservation status.');
            });
    }
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