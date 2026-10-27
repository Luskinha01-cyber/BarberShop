/* ========================================================
   BARBERSHOP - JAVASCRIPT GLOBAL
   ======================================================== */

document.addEventListener("DOMContentLoaded", () => {
    // Máscara de Telefone Automática (XX) XXXXX-XXXX
    const phoneInputs = document.querySelectorAll('input[type="tel"], input[name*="Phone"], input[name*="Identifier"]');
    phoneInputs.forEach(input => {
        input.addEventListener("input", (e) => {
            // Só aplica máscara se não for um e-mail sendo digitado
            if (e.target.value.includes("@")) return;

            let value = e.target.value.replace(/\D/g, "");
            if (value.startsWith("55") && value.length > 11) {
                value = value.substring(2);
            }
            if (value.length > 11) {
                value = value.substring(0, 11);
            }

            if (value.length > 6) {
                e.target.value = `(${value.substring(0, 2)}) ${value.substring(2, 7)}-${value.substring(7)}`;
            } else if (value.length > 2) {
                e.target.value = `(${value.substring(0, 2)}) ${value.substring(2)}`;
            } else if (value.length > 0) {
                e.target.value = `(${value}`;
            }
        });
    });

    // Auto-dismiss alertas após 6 segundos
    const alerts = document.querySelectorAll(".alert-box");
    if (alerts.length > 0) {
        setTimeout(() => {
            alerts.forEach(alert => {
                alert.style.transition = "opacity 0.5s ease";
                alert.style.opacity = "0";
                setTimeout(() => alert.remove(), 500);
            });
        }, 6000);
    }
});

// Helper para confirmações destrutivas com mensagem amigável
function confirmAction(message) {
    return confirm(message || "Tem certeza que deseja prosseguir com esta ação?");
}
