// DOM Elements
const errorMessage = document.getElementById('errorMessage');
const selectedTableInfo = document.getElementById('selectedTableInfo');
const selectedTableText = document.getElementById('selectedTableText');
const warningModal = document.getElementById('warningModal');
const warningOk = document.getElementById('warningOk');
const svgWrapper = document.getElementById("svg-wrapper");
const makeReservationBtn = document.getElementById('makeReservation');
const timeSelect = document.getElementById('reservation-time');
const dateInput = document.getElementById('reservation-date');
const peopleSelect = document.getElementById('people-count');

// Application State
let tableCapacities = [];
let currentOccupiedTables = [];
let tableButtons = [];
let selectedButton = null;
let selectedTableId = null;

// Table positions for layout
const tablePositions = [
    [200, 120], [320, 120], [440, 120], [140, 230],
    [260, 230], [380, 230], [500, 230], [140, 340],
    [260, 340], [380, 340], [500, 340],
    [700, 100], [820, 100], [940, 100], [700, 225],
    [820, 225], [940, 225], [700, 350], [820, 350], [940, 350]
];

// Utility Functions
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

function validateReservationInputs() {
    const dateVal = dateInput.value;
    const timeVal = timeSelect.value;
    const peopleVal = peopleSelect.value;

    return {
        dateVal,
        timeVal,
        peopleVal,
        isValid: dateVal && timeVal && peopleVal
    };
}

function isTableAvailable(button, peopleCount = null) {
    if (button.classList.contains('occupied')) {
        return false;
    }

    if (peopleCount !== null) {
        const minCapacity = parseInt(button.dataset.minCapacity);
        const maxCapacity = parseInt(button.dataset.maxCapacity);
        return peopleCount >= minCapacity && peopleCount <= maxCapacity;
    }

    return true;
}

function updateTableState(button, peopleCount) {
    // Don't update state for occupied tables
    if (button.classList.contains('occupied')) {
        return;
    }

    const isAvailable = isTableAvailable(button, peopleCount);

    button.classList.toggle('disabled', !isAvailable);
    button.disabled = !isAvailable;

    if (!isAvailable && button.classList.contains('selected')) {
        button.classList.remove('selected');
        if (selectedButton === button) {
            clearSelection();
        }
    }
}

function clearSelection() {
    selectedButton = null;
    selectedTableId = null;
    updateSelectedTableDisplay();
    makeReservationBtn.disabled = true;
}

function selectTable(button, tableId) {
    // Clear previous selection
    tableButtons.forEach(btn => btn.classList.remove('selected'));

    // Set new selection
    button.classList.add('selected');
    selectedButton = button;
    selectedTableId = tableId;

    updateSelectedTableDisplay();
    makeReservationBtn.disabled = false;
}

function updateSelectedTableDisplay() {
    if (selectedTableId && selectedButton && !selectedButton.classList.contains('disabled')) {
        const tableInfo = tableCapacities.find(t => t.tableId === selectedTableId);
        if (tableInfo) {
            selectedTableText.textContent = `Table ${selectedTableId} selected (Capacity: ${tableInfo.minCapacity}-${tableInfo.maxCapacity} people)`;
            selectedTableInfo.style.display = 'block';
            return;
        }
    }
    selectedTableInfo.style.display = 'none';
}

// API Functions
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

// Table Management Functions
function updateTableAvailability() {
    const peopleCount = parseInt(peopleSelect.value);

    tableButtons.forEach((button) => {
        const tableNumber = parseInt(button.dataset.tableId);

        // Reset occupied status
        button.classList.remove('occupied');
        button.disabled = false;

        // Check if table is occupied
        if (currentOccupiedTables.includes(tableNumber)) {
            button.classList.add('occupied');
            button.classList.remove('selected');
            button.disabled = true;

            if (selectedButton === button) {
                clearSelection();
            }
        } else if (!isNaN(peopleCount)) {
            // Update based on people count if valid
            updateTableState(button, peopleCount);
        }
    });

    updateSelectedTableDisplay();
}

function createTableButton(tableId) {
    const [x, y] = tablePositions[tableId - 1];
    const button = document.createElement("button");

    button.className = "tables";
    button.innerText = tableId;
    button.dataset.tableId = tableId;

    // Set capacity from database data or defaults
    const tableInfo = tableCapacities.find(t => t.tableId === tableId);
    if (tableInfo) {
        button.dataset.minCapacity = tableInfo.minCapacity;
        button.dataset.maxCapacity = tableInfo.maxCapacity;
    } else {
        button.dataset.minCapacity = 1;
        button.dataset.maxCapacity = 4;
    }

    button.style.left = `${x}px`;
    button.style.top = `${y}px`;

    button.addEventListener("click", () => handleTableClick(button, tableId));

    return button;
}

function handleTableClick(button, tableId) {
    const { isValid } = validateReservationInputs();

    if (!isValid) {
        showWarning("Please enter the date, time and the number of people!");
        return;
    }

    if (button.classList.contains("disabled") || button.classList.contains("occupied")) {
        const message = button.classList.contains("occupied")
            ? "This table is already reserved for the selected time."
            : "This table doesn't have sufficient capacity for your party size.";
        showWarning(message);
        return;
    }

    selectTable(button, tableId);
}

// Event Handlers
async function handleDateTimeChange() {
    const selectedTime = timeSelect.value;
    const selectedDate = dateInput.value;

    if (selectedDate && selectedTime) {
        await loadOccupiedTables(selectedDate, selectedTime);
        updateTableAvailability();
    }
}

function handlePeopleCountChange() {
    const selectedPeople = parseInt(peopleSelect.value);

    if (isNaN(selectedPeople)) {
        // Reset all tables to available (except occupied)
        tableButtons.forEach(button => {
            if (!button.classList.contains('occupied')) {
                button.classList.remove('disabled');
                button.disabled = false;
            }
        });
    } else {
        // Update table states based on capacity (but preserve occupied status)
        tableButtons.forEach(button => {
            updateTableState(button, selectedPeople);
        });
    }

    // Check if current selection is still valid
    const hasValidSelection = selectedButton &&
        !selectedButton.classList.contains('disabled') &&
        !selectedButton.classList.contains('occupied');

    if (!hasValidSelection) {
        clearSelection();
    }
}

function handleMakeReservation() {
    const { dateVal, timeVal, peopleVal, isValid } = validateReservationInputs();

    if (selectedTableId && isValid) {
        const params = new URLSearchParams({
            tableId: selectedTableId,
            date: dateVal,
            time: timeVal,
            guests: peopleVal
        });

        console.log('Redirecting to:', `/Reservation/Create?${params.toString()}`);

        try {
            window.location.href = `/Reservation/Create?${params.toString()}`;
        } catch (error) {
            console.error('Redirect error:', error);
            showError('Error redirecting to reservation form. Please try again.');
        }
    }
}

// Initialization Functions
async function initializeTables() {
    await loadTableCapacities();

    for (let i = 1; i <= 20; i++) {
        const button = createTableButton(i);
        tableButtons.push(button);
        svgWrapper.appendChild(button);
    }
}

function initializeEventListeners() {
    // Date/time change handlers
    timeSelect.addEventListener('change', handleDateTimeChange);
    dateInput.addEventListener('change', handleDateTimeChange);

    // People count handler
    peopleSelect.addEventListener('input', handlePeopleCountChange);

    // Make reservation handler
    makeReservationBtn.addEventListener('click', handleMakeReservation);

    // Warning modal handler
    warningOk.addEventListener('click', () => {
        warningModal.style.display = 'none';
    });
}

function initializePage() {
    // Set today's date as default
    dateInput.value = new Date().toISOString().split('T')[0];

    // Initialize event listeners
    initializeEventListeners();

    // Initialize tables
    initializeTables();
}

// Start the application
document.addEventListener('DOMContentLoaded', initializePage);