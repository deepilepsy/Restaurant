
// Phone number formatting
document.getElementById('TelNo').addEventListener('input', function(e) {
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

// Form validation
document.getElementById('staffForm').addEventListener('submit', function(e) {
    let isValid = true;
    const requiredFields = ['Name', 'Surname', 'Job', 'TelNo'];

    requiredFields.forEach(fieldId => {
        const field = document.getElementById(fieldId);
        const value = field.value.trim();

        if (!value) {
            field.style.borderColor = '#ff6b6b';
            isValid = false;
        } else {
            field.style.borderColor = '';
        }
    });

    // Phone validation
    const phone = document.getElementById('TelNo').value.trim();
    const phoneRegex = /^[\+]?[0-9\s\-\(\)]{10,}$/;
    if (phone && !phoneRegex.test(phone)) {
        document.getElementById('TelNo').style.borderColor = '#ff6b6b';
        isValid = false;
    }

    if (!isValid) {
        e.preventDefault();
        alert('Please fill in all required fields correctly.');
    }
});

// Real-time validation
document.querySelectorAll('input[required], select[required]').forEach(field => {
    field.addEventListener('blur', function() {
        if (this.value.trim() === '') {
            this.style.borderColor = '#ff6b6b';
        } else {
            this.style.borderColor = '';
        }
    });
});

// Auto-hide success/error messages
setTimeout(function() {
    const alerts = document.querySelectorAll('.alert');
    alerts.forEach(function(alert) {
        alert.style.transition = 'opacity 0.5s ease';
        alert.style.opacity = '0';
        setTimeout(() => alert.remove(), 500);
    });
}, 5000);