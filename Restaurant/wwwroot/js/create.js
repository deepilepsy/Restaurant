// Get URL parameters
const urlParams = new URLSearchParams(window.location.search);
const tableId = urlParams.get('tableId');
const date = urlParams.get('date');
const time = urlParams.get('time');
const guests = urlParams.get('guests');

// Display reservation details
if (tableId) {
    document.getElementById('tableNumber').textContent = tableId;
    document.getElementById('hiddenTableId').value = tableId;
}
if (date) {
    const formattedDate = new Date(date).toLocaleDateString('en-US', {
        weekday: 'long',
        year: 'numeric',
        month: 'long',
        day: 'numeric'
    });
    document.getElementById('reservationDate').textContent = formattedDate;
    document.getElementById('hiddenDate').value = date;
}
if (time) {
    document.getElementById('reservationTime').textContent = time;
    document.getElementById('hiddenTime').value = time;
}
if (guests) {
    document.getElementById('guestCount').textContent = guests + (guests === '1' ? ' Guest' : ' Guests');
    document.getElementById('hiddenGuests').value = guests;
}

// Form validation
document.getElementById('reservationForm').addEventListener('submit', function(e) {
    let isValid = true;

    // Clear previous errors
    document.querySelectorAll('.error-message').forEach(error => {
        error.style.display = 'none';
    });

    // Validate first name
    const firstName = document.getElementById('firstName').value.trim();
    if (!firstName) {
        document.getElementById('firstNameError').style.display = 'block';
        isValid = false;
    }

    // Validate last name
    const lastName = document.getElementById('lastName').value.trim();
    if (!lastName) {
        document.getElementById('lastNameError').style.display = 'block';
        isValid = false;
    }

    // Validate phone
    const phone = document.getElementById('phone').value.trim();
    const phoneRegex = /^[\+]?[0-9\s\-\(\)]{10,}$/;
    if (!phone || !phoneRegex.test(phone)) {
        document.getElementById('phoneError').style.display = 'block';
        isValid = false;
    }

    // Validate email if provided
    const email = document.getElementById('email').value.trim();
    if (email) {
        const emailRegex = /^[^\s@@]+@@[^\s@@]+\.[^\s@@]+$/;
        if (!emailRegex.test(email)) {
            document.getElementById('emailError').style.display = 'block';
            isValid = false;
        }
    }

    if (!isValid) {
        e.preventDefault();
        // Scroll to first error
        const firstError = document.querySelector('.error-message[style*="block"]');
        if (firstError) {
            firstError.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
    }
});

// Phone number formatting
document.getElementById('phone').addEventListener('input', function(e) {
    let value = e.target.value.replace(/\D/g, '');
    if (value.startsWith('90')) {
        value = '+' + value;
    } else if (value.startsWith('0')) {
        value = '+90' + value.substring(1);
    } else if (!value.startsWith('+')) {
        value = '+90' + value;
    }
    e.target.value = value;
});

// Real-time validation feedback
document.querySelectorAll('input[required]').forEach(input => {
    input.addEventListener('blur', function() {
        const errorElement = document.getElementById(this.id + 'Error');
        if (errorElement) {
            if (this.value.trim() === '') {
                errorElement.style.display = 'block';
                this.style.borderColor = '#ff6b6b';
            } else {
                errorElement.style.display = 'none';
                this.style.borderColor = '';
            }
        }
    });
});

// Email validation on blur
document.getElementById('email').addEventListener('blur', function() {
    const email = this.value.trim();
    const errorElement = document.getElementById('emailError');
    if (email) {
        const emailRegex = /^[^\s@@]+@@[^\s@@]+\.[^\s@@]+$/;
        if (!emailRegex.test(email)) {
            errorElement.style.display = 'block';
            this.style.borderColor = '#ff6b6b';
        } else {
            errorElement.style.display = 'none';
            this.style.borderColor = '';
        }
    } else {
        errorElement.style.display = 'none';
        this.style.borderColor = '';
    }
});