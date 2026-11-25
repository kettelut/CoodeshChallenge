(() => {
    const form = document.getElementById("order-form");
    const button = document.getElementById("submit-order");
    const pending = document.getElementById("pending");
    const clear = document.getElementById("clear-form");
    if (!form || !button || !pending) return;

    let submitting = false;
    form.addEventListener("submit", (event) => {
        if (submitting) {
            event.preventDefault();
            return;
        }
        submitting = true;
        button.disabled = true;
        if (clear) clear.setAttribute("aria-disabled", "true");
        pending.hidden = false;
        form.setAttribute("aria-busy", "true");
    });
    if (clear) clear.addEventListener("click", (event) => {
        if (submitting) event.preventDefault();
    });
})();
