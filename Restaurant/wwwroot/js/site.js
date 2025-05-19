// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

if (window.location.pathname.toLowerCase() === "/") {
    window.addEventListener("scroll", function () {
        const header = document.getElementById("site-header");
        if (window.scrollY > 50) {
            header.style.backgroundColor = "rgba(35, 35, 35, 0.9)"; // opaque
        } else {
            header.style.backgroundColor = "rgba(0, 0, 0, 0)"; // transparent
        }
    })
}

const form = document.getElementById("search-form");
const input = document.getElementById("receipt-search");
const icon = document.getElementById("search-icon");

// Toggle search bar when clicking the icon
icon.addEventListener("click", function (e) {
    e.preventDefault();
    form.classList.toggle("active");
    if (form.classList.contains("active")) {
        input.focus();
    }
    icon.classList.toggle("orng");
});

// Collapse when clicking outside
document.addEventListener("click", function (e) {
    if (!form.contains(e.target)) {
        form.classList.remove("active");
        icon.classList.remove("orng");
    }
});

// Write your JavaScript code.
