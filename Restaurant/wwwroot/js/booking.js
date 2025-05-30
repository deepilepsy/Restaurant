const errorMessage = document.getElementById('errorMessage');
const selectedTableInfo = document.getElementById('selectedTableInfo');
const selectedTableText = document.getElementById('selectedTableText');

// Store table information from the server
let tableCapacities = [];
let currentOccupiedTables = [];

// Initialize date to today
document.getElementById('reservation-date').value = new Date().toISOString().split('T')[0];

const timeSelect = document.getElementById('reservation-time');
timeSelect.addEventListener('change', async () => {
    const selectedTime = timeSelect.value;
    const selectedDate = document.getElementById('reservation-date').value;

    if (selectedDate && selectedTime) {
        await loadOccupiedTables(selectedDate, selectedTime);
        updateTableAvailability();
    }
});

document.getElementById('reservation-date').addEventListener('change', async () => {
    const selectedTime = timeSelect.value;
    const selectedDate = document.getElementById('reservation-date').value;

    if (selectedDate && selectedTime) {
        await loadOccupiedTables(selectedDate, selectedTime);
        updateTableAvailability();
    }
});

async function loadOccupiedTables(date, time) {
    try {
        const response = await fetch(`/Home/GetOccupiedTables?date=${encodeURIComponent(date)}&time=${encodeURIComponent(time)}`);
        const data = await response.json();

        if (data.success) {
            currentOccupiedTables = data.occupiedTables || [];
        } else {
            showError('Error loading table availability: ' + data.message);
            currentOccupiedTables = [];
        }
    } catch (error) {
        console.error('Error loading occupied tables:', error);
        showError('Network error while checking table availability');
        currentOccupiedTables = [];
    }
}

async function loadTableCapacities() {
    try {
        const response = await fetch('/Home/GetTableCapacities');
        const data = await response.json();

        if (data.success) {
            tableCapacities = data.tableCapacities || [];
        } else {
            showError('Error loading table information: ' + data.message);
        }
    } catch (error) {
        console.error('Error loading table capacities:', error);
        showError('Network error while loading table information');
    }
}

function updateTableAvailability() {
    const allTables = document.querySelectorAll('.tables');

    allTables.forEach((table) => {
        const tableNumber = parseInt(table.dataset.tableId);

        // Remove occupied status first
        table.classList.remove('occupied');
        table.disabled = false;

        // Check if table is occupied
        if (currentOccupiedTables.includes(tableNumber)) {
            table.classList.add('occupied');
            table.classList.remove('selected');
            table.disabled = true;
        }
    });

    // Update selected table info if current selection is now occupied
    updateSelectedTableDisplay();
}

const warningModal = document.getElementById('warningModal');
const warningOk = document.getElementById('warningOk');

function showWarning(message) {
    document.getElementById('warningText').textContent = message;
    warningModal.style.display = 'block';
}

function showError(message) {
    errorMessage.textContent = message;
    errorMessage.style.display = 'block';
    setTimeout(() => {
        errorMessage.style.display = 'none';
    }, 5000);
}

warningOk.addEventListener('click', () => {
    warningModal.style.display = 'none';
});

const tableButtons = [];
let selectedButton = null;
let selectedTableId = null;

const svgWrapper = document.getElementById("svg-wrapper");
const makeReservationBtn = document.getElementById('makeReservation');

const tablePositions = [
    [200, 120], [320, 120], [440, 120], [140, 230],
    [260, 230], [380, 230], [500, 230], [140, 340],
    [260, 340], [380, 340], [500, 340],
    [700, 100], [820, 100],[940, 100], [700, 225],
    [820, 225], [940, 225], [700, 350],[820,350],[940,350]
];

function updateSelectedTableDisplay() {
    if (selectedTableId && selectedButton) {
        const tableInfo = tableCapacities.find(t => t.tableId === selectedTableId);
        if (tableInfo) {
            selectedTableText.textContent = `Table ${selectedTableId} selected (Capacity: ${tableInfo.minCapacity}-${tableInfo.maxCapacity} people)`;
            selectedTableInfo.style.display = 'block';
        }
    } else {
        selectedTableInfo.style.display = 'none';
    }
}

// Create table buttons dynamically
async function initializeTables() {
    await loadTableCapacities();

    for (let i = 1; i <= 20; i++) {
        const [x, y] = tablePositions[i-1];
        const button = document.createElement("button");
        button.className = "tables";
        button.innerText = i;
        button.dataset.tableId = i;

        // Find capacity from database data
        const tableInfo = tableCapacities.find(t => t.tableId === i);
        if (tableInfo) {
            button.dataset.minCapacity = tableInfo.minCapacity;
            button.dataset.maxCapacity = tableInfo.maxCapacity;
        } else {
            // Fallback to default values
            button.dataset.minCapacity = 1;
            button.dataset.maxCapacity = 4;
        }

        button.style.left = `${x}px`;
        button.style.top = `${y}px`;

        button.addEventListener("click", () => {
            const dateVal = document.getElementById("reservation-date").value;
            const timeVal = document.getElementById("reservation-time").value;
            const peopleVal = document.getElementById("people-count").value;

            if (!dateVal || !timeVal || !peopleVal) {
                showWarning("Please enter the date, time and the number of people!");
                return;
            }

            if (button.classList.contains("disabled") || button.classList.contains("occupied")) {
                if (button.classList.contains("occupied")) {
                    showWarning("This table is already reserved for the selected time.");
                } else {
                    showWarning("This table doesn't have sufficient capacity for your party size.");
                }
                return;
            }

            // Direct selection without confirmation
            // Clear all other selected tables
            tableButtons.forEach(btn => btn.classList.remove('selected'));

            // Select this table
            button.classList.add('selected');
            selectedButton = button;
            selectedTableId = i;

            // Update display and enable button
            updateSelectedTableDisplay();
            makeReservationBtn.disabled = false;
        });

        tableButtons.push(button);
        svgWrapper.appendChild(button);
    }
}

makeReservationBtn.addEventListener('click', () => {
    const dateVal = document.getElementById("reservation-date").value;
    const timeVal = document.getElementById("reservation-time").value;
    const peopleVal = document.getElementById("people-count").value;

    if (selectedTableId && dateVal && timeVal && peopleVal) {
        // Redirect to reservation form with parameters
        const params = new URLSearchParams({
            tableId: selectedTableId,
            date: dateVal,
            time: timeVal,
            guests: peopleVal
        });

        // Debug: Log the URL being generated
        console.log('Redirecting to:', `/Reservation/Create?${params.toString()}`);

        // Try the redirect
        try {
            window.location.href = `/Reservation/Create?${params.toString()}`;
        } catch (error) {
            console.error('Redirect error:', error);
            alert('Error redirecting to reservation form. Please try again.');
        }
    }
});

const peopleSelect = document.getElementById('people-count');
peopleSelect.addEventListener('input', () => {
    const selectedPeople = parseInt(peopleSelect.value);

    tableButtons.forEach(button => {
        const minCapacity = parseInt(button.dataset.minCapacity);
        const maxCapacity = parseInt(button.dataset.maxCapacity);

        if (button.classList.contains('occupied')) {
            button.classList.add('disabled');
            button.disabled = true;
            button.classList.remove('selected');
            return;
        }

        if (isNaN(selectedPeople)) {
            button.classList.remove('disabled');
            button.disabled = false;
        } else if (selectedPeople >= minCapacity && selectedPeople <= maxCapacity) {
            button.classList.remove('disabled');
            button.disabled = false;
        } else {
            button.classList.add('disabled');
            button.disabled = true;
            button.classList.remove('selected');
        }
    });

    // Reset selection if currently selected table is now disabled
    if (selectedButton && selectedButton.classList.contains('disabled')) {
        selectedButton = null;
        selectedTableId = null;
        updateSelectedTableDisplay();
    }

    // Reset make reservation button if no valid table is selected
    const hasValidSelection = tableButtons.some(btn =>
        btn.classList.contains('selected') && !btn.classList.contains('disabled')
    );
    makeReservationBtn.disabled = !hasValidSelection;
});

// Initialize the page
document.addEventListener('DOMContentLoaded', function() {
    initializeTables();
});