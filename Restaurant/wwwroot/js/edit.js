// Form validation
document.getElementById('editForm').addEventListener('submit', function(e) {
    let isValid = true;

    // Clear previous errors
    document.querySelectorAll('.error-message').forEach(error => {
        error.style.display = 'none';
    });

    // Validate first name
    const firstName = document.getElementById('firstName').value.trim();
    const onlyLettersRegex = /^[a-zA-Z]+$/;
    if (!onlyLettersRegex.test(firstName)) {
        document.getElementById('firstNameError').style.display = 'block';
        isValid = false;
    } else {
        document.getElementById('firstNameError').style.display = 'none';
    }

    // Validate last name
    const lastName = document.getElementById('lastName').value.trim();
    if (!onlyLettersRegex.test(lastName)) {
        document.getElementById('lastNameError').style.display = 'block';
        isValid = false;
    } else {
        document.getElementById('lastNameError').style.display = 'none';
    }

    // Validate phone 
    const phone = document.getElementById('phone').value.trim();
    const phoneDigitsOnly = phone.replace(/\D/g, ''); // Remove all non-digits

    // Check if it's a valid phone format
    const isValidPhone = (
        phoneDigitsOnly.length === 10 || // 10 digits 
        (phone.startsWith('+90') && phoneDigitsOnly.length === 12) // +90 prefix with 12 total digits
    );

    if (!isValidPhone) {
        document.getElementById('phoneError').style.display = 'block';
        isValid = false;
    } else {
        document.getElementById('phoneError').style.display = 'none';
    }

    // Validate email
    const email = document.getElementById('email').value.trim();
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

    if (!email || !emailRegex.test(email)) {
        document.getElementById('emailError').style.display = 'block';
        isValid = false;
    } else {
        document.getElementById('emailError').style.display = 'none';
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
        value = '+9' + value;
    } else if (value.length === 10 && !value.startsWith('90')) {
        // If more than 10 digits and doesn't start with 90, add +90 prefix
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
        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
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