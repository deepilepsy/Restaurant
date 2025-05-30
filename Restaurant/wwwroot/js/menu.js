// Smooth scrolling for highlight cards
document.querySelectorAll('.highlight-card').forEach(card => {
    card.addEventListener('click', function() {
        const target = this.getAttribute('data-target');
        if (target) {
            document.querySelector(target).scrollIntoView({
                behavior: 'smooth'
            });
        }
    });
});

// Scroll to top button functionality
window.addEventListener('scroll', function() {
    const scrollBtn = document.getElementById('scrollTopBtn');
    if (window.pageYOffset > 300) {
        scrollBtn.style.display = 'block';
    } else {
        scrollBtn.style.display = 'none';
    }
});

function scrollToTop() {
    window.scrollTo({
        top: 0,
        behavior: 'smooth'
    });
}

// Add hover effects to menu items
document.querySelectorAll('.menu-item').forEach(item => {
    item.addEventListener('mouseenter', function() {
        this.style.backgroundColor = '#f8f9fa';
        this.style.transform = 'translateX(5px)';
        this.style.transition = 'all 0.3s ease';
    });

    item.addEventListener('mouseleave', function() {
        this.style.backgroundColor = 'transparent';
        this.style.transform = 'translateX(0)';
    });
});

// Add animation to cards on scroll
const observerOptions = {
    threshold: 0.1,
    rootMargin: '0px 0px -50px 0px'
};

const observer = new IntersectionObserver(function(entries) {
    entries.forEach(entry => {
        if (entry.isIntersecting) {
            entry.target.style.opacity = '1';
            entry.target.style.transform = 'translateY(0)';
        }
    });
}, observerOptions);

// Observe all cards and sections
document.querySelectorAll('.highlight-card, .menu-section, .info-banner').forEach(el => {
    el.style.opacity = '0';
    el.style.transform = 'translateY(30px)';
    el.style.transition = 'opacity 0.6s ease, transform 0.6s ease';
    observer.observe(el);
});