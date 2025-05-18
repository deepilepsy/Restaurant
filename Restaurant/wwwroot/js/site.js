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
// Write your JavaScript code.
