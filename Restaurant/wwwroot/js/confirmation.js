
// Copy reservation ID to clipboard when clicked
document.querySelector('.id-number').addEventListener('click', function() {
    navigator.clipboard.writeText(this.textContent).then(function() {
        const originalText = document.querySelector('.id-number').textContent;
        document.querySelector('.id-number').textContent = 'Copied!';
        setTimeout(() => {
            document.querySelector('.id-number').textContent = originalText;
        }, 1000);
    });
});