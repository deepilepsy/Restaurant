// Toggle password visibility

document.getElementById('togglePassword').addEventListener('click', function() {
    const passwordField = document.getElementById('password');
    const toggleIcon = this.querySelector('i');

    if (passwordField.type === 'password') {
        passwordField.type = 'text';
        toggleIcon.classList.remove('fa-eye');
        toggleIcon.classList.add('fa-eye-slash');
    } else {
        passwordField.type = 'password';
        toggleIcon.classList.remove('fa-eye-slash');
        toggleIcon.classList.add('fa-eye');
    }
});

// Form validation and submission
document.getElementById('createUserForm').addEventListener('submit', function(e) {
    const username = document.getElementById('username').value.trim();
    const password = document.getElementById('password').value.trim();
    const type = document.getElementById('type').value;

    // Basic validation
    if (!username || !password || !type) {
        e.preventDefault();
        showError('All fields are required.');
        return;
    }

    if (username.length < 3) {
        e.preventDefault();
        showError('Username must be at least 3 characters long.');
        return;
    }

    if (password.length < 4) {
        e.preventDefault();
        showError('Password must be at least 4 characters long.');
        return;
    }
});
// Add subtle animations
document.querySelectorAll('.form-control, .form-select').forEach(input => {
    input.addEventListener('focus', function() {
        this.style.transform = 'scale(1.02)';
    });

    input.addEventListener('blur', function() {
        this.style.transform = 'scale(1)';
    });
});