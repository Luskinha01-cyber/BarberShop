/* ========================================================
   BARBERSHOP - FLUXO DE AGENDAMENTO (5 ETAPAS)
   ======================================================== */

let bookingData = {
    serviceId: null,
    serviceName: "",
    servicePrice: 0,
    serviceDuration: 0,
    barberId: null,
    barberName: "Qualquer profissional disponível",
    date: "",
    formattedDate: "",
    timeSlot: "",
    endTime: "",
    notes: ""
};

let currentStep = 1;

document.addEventListener("DOMContentLoaded", () => {
    initBookingWizard();
});

function initBookingWizard() {
    // Se veio serviço pré-selecionado na URL ou ViewModel
    const preSelected = document.querySelector('.service-card-select.selected');
    if (preSelected) {
        selectService(
            preSelected.dataset.id,
            preSelected.dataset.name,
            preSelected.dataset.price,
            preSelected.dataset.duration
        );
    }

    // Configura data mínima no input (hoje)
    const dateInput = document.getElementById("bookingDate");
    if (dateInput) {
        const today = new Date().toISOString().split("T")[0];
        dateInput.min = today;
        dateInput.value = today;
        bookingData.date = today;
        formatSelectedDate(today);

        dateInput.addEventListener("change", (e) => {
            bookingData.date = e.target.value;
            formatSelectedDate(e.target.value);
            loadAvailableSlots();
        });
    }
}

function selectService(id, name, price, duration) {
    bookingData.serviceId = parseInt(id);
    bookingData.serviceName = name;
    bookingData.servicePrice = parseFloat(price);
    bookingData.serviceDuration = parseInt(duration);

    document.querySelectorAll('.service-card-select').forEach(card => {
        card.classList.remove('selected');
        if (card.dataset.id == id) {
            card.classList.add('selected');
        }
    });

    goToStep(2);
}

function selectBarber(id, name) {
    bookingData.barberId = id ? parseInt(id) : null;
    bookingData.barberName = name || "Qualquer profissional disponível";

    document.querySelectorAll('.barber-card-select').forEach(card => {
        card.classList.remove('selected');
        if ((!id && card.dataset.id === "any") || card.dataset.id == id) {
            card.classList.add('selected');
        }
    });

    goToStep(3);
}

function selectDateNext() {
    const dateInput = document.getElementById("bookingDate");
    if (!dateInput.value) {
        alert("Por favor, selecione uma data para continuar.");
        return;
    }
    bookingData.date = dateInput.value;
    formatSelectedDate(dateInput.value);
    goToStep(4);
    loadAvailableSlots();
}

function formatSelectedDate(dateStr) {
    if (!dateStr) return;
    const parts = dateStr.split("-");
    if (parts.length === 3) {
        bookingData.formattedDate = `${parts[2]}/${parts[1]}/${parts[0]}`;
    }
}

async function loadAvailableSlots() {
    const slotsContainer = document.getElementById("slotsContainer");
    const loadingIndicator = document.getElementById("slotsLoading");
    const noSlotsMessage = document.getElementById("noSlotsMessage");

    slotsContainer.innerHTML = "";
    loadingIndicator.style.display = "block";
    noSlotsMessage.style.display = "none";

    try {
        let url = `/Booking/GetAvailableSlots?date=${bookingData.date}&serviceId=${bookingData.serviceId}`;
        if (bookingData.barberId) {
            url += `&barberId=${bookingData.barberId}`;
        }

        const response = await fetch(url);
        const slots = await response.json();

        loadingIndicator.style.display = "none";

        if (!slots || slots.length === 0) {
            noSlotsMessage.style.display = "block";
            return;
        }

        slots.forEach(slot => {
            const btn = document.createElement("button");
            btn.type = "button";
            btn.className = "slot-btn" + (bookingData.timeSlot === slot.time ? " selected" : "");
            btn.innerHTML = `<span class="slot-time">${slot.formattedTime}</span>` +
                            (slot.assignedBarberName ? `<span class="slot-barber">${slot.assignedBarberName}</span>` : "");
            
            btn.addEventListener("click", () => {
                document.querySelectorAll(".slot-btn").forEach(b => b.classList.remove("selected"));
                btn.classList.add("selected");
                bookingData.timeSlot = slot.time;
                bookingData.endTime = slot.endTime;
                if (!bookingData.barberId && slot.assignedBarberName) {
                    bookingData.barberName = `${slot.assignedBarberName} (Alocado)`;
                }
                goToStep(5);
                updateSummary();
            });

            slotsContainer.appendChild(btn);
        });
    } catch (err) {
        loadingIndicator.style.display = "none";
        noSlotsMessage.style.display = "block";
        noSlotsMessage.innerText = "Erro ao carregar os horários. Tente novamente.";
    }
}

function updateSummary() {
    document.getElementById("summaryService").innerText = bookingData.serviceName;
    document.getElementById("summaryDuration").innerText = `${bookingData.serviceDuration} minutos`;
    document.getElementById("summaryBarber").innerText = bookingData.barberName;
    document.getElementById("summaryDate").innerText = bookingData.formattedDate;
    document.getElementById("summaryTime").innerText = `${bookingData.timeSlot} até ${bookingData.endTime}`;
    document.getElementById("summaryPrice").innerText = bookingData.servicePrice.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
}

function goToStep(step) {
    if (step > 1 && !bookingData.serviceId) {
        alert("Selecione um serviço primeiro.");
        return;
    }
    if (step > 3 && !bookingData.date) {
        alert("Selecione uma data primeiro.");
        return;
    }
    if (step > 4 && !bookingData.timeSlot) {
        alert("Selecione um horário primeiro.");
        return;
    }

    currentStep = step;

    for (let i = 1; i <= 5; i++) {
        const stepEl = document.getElementById(`step${i}`);
        const navEl = document.getElementById(`stepNav${i}`);
        if (stepEl) {
            stepEl.style.display = (i === step) ? "block" : "none";
        }
        if (navEl) {
            navEl.classList.toggle("active", i === step);
            navEl.classList.toggle("completed", i < step);
        }
    }

    window.scrollTo({ top: 150, behavior: "smooth" });
}

async function confirmBooking() {
    const confirmBtn = document.getElementById("btnConfirmBooking");
    const notesInput = document.getElementById("bookingNotes");

    bookingData.notes = notesInput ? notesInput.value : "";

    if (confirmBtn) {
        confirmBtn.disabled = true;
        confirmBtn.innerHTML = '<span class="spinner-border"></span> Confirmando...';
    }
    hideBookingErrors();

    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

    const payload = {
        serviceId: bookingData.serviceId,
        barberId: bookingData.barberId,
        date: bookingData.date,
        timeSlot: bookingData.timeSlot,
        notes: bookingData.notes
    };

    try {
        const response = await fetch("/Booking/Create", {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken": token || ""
            },
            body: JSON.stringify(payload)
        });

        // Se a requisição foi redirecionada para a tela de Login (sessão expirada)
        if (response.redirected || (response.url && response.url.indexOf("/Account/Login") !== -1)) {
            window.location.href = `/Account/Login?returnUrl=${encodeURIComponent('/Booking')}`;
            return;
        }

        let data = null;
        try {
            data = await response.json();
        } catch (jsonErr) {
            console.warn("A resposta do servidor não é um JSON válido:", jsonErr);
        }

        if (response.ok && data && data.success) {
            window.location.href = data.redirectUrl || `/Booking/Confirmation/${data.appointmentId}`;
        } else {
            resetConfirmButton();
            
            const errorMessage = (data && data.message) 
                ? data.message 
                : (response.status === 401 
                    ? "Sessão expirada. Por favor, acesse sua conta novamente." 
                    : "Não foi possível confirmar o agendamento. Verifique os dados e tente novamente.");
            
            showBookingError(errorMessage);

            // Se for conflito de horário (409), volta para o passo 4 e recarrega os horários
            if (response.status === 409) {
                setTimeout(() => {
                    goToStep(4);
                    loadAvailableSlots();
                }, 2500);
            }
        }
    } catch (err) {
        console.error("Falha ao comunicar com o servidor:", err);
        resetConfirmButton();
        showBookingError("Erro ao processar reserva. Verifique sua conexão e tente novamente.");
    }
}

function resetConfirmButton() {
    const confirmBtn = document.getElementById("btnConfirmBooking");
    if (confirmBtn) {
        confirmBtn.disabled = false;
        confirmBtn.innerHTML = '<i class="bi bi-check2-circle"></i> Confirmar Agendamento';
    }
}

function showBookingError(msg) {
    const alerts = [
        document.getElementById("bookingErrorAlert"),
        document.getElementById("bookingCardErrorAlert")
    ];

    alerts.forEach(alertEl => {
        if (alertEl) {
            alertEl.innerText = msg;
            alertEl.style.display = "block";
        }
    });

    const cardAlert = document.getElementById("bookingCardErrorAlert");
    if (cardAlert) {
        cardAlert.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }
}

function hideBookingErrors() {
    const alerts = [
        document.getElementById("bookingErrorAlert"),
        document.getElementById("bookingCardErrorAlert")
    ];

    alerts.forEach(alertEl => {
        if (alertEl) {
            alertEl.style.display = "none";
            alertEl.innerText = "";
        }
    });
}
