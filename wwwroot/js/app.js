function getUniqIdValue(prefix = 'task4') {
    const randomValue = window.crypto && window.crypto.randomUUID
        ? window.crypto.randomUUID()
        : `${Date.now()}-${Math.random().toString(36).slice(2)}`;
    return `${prefix}-${randomValue.replace(/[^a-zA-Z0-9_-]/g, '')}`;
}

window.getUniqIdValue = getUniqIdValue;

document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach((element) => {
        new bootstrap.Tooltip(element);
    });

    const bulkForm = document.querySelector('#bulkUserForm');
    if (!bulkForm) {
        return;
    }

    const selectAll = bulkForm.querySelector('.js-select-all');
    const rowCheckboxes = Array.from(bulkForm.querySelectorAll('.js-row-checkbox'));
    const selectionCounter = document.querySelector('.selection-counter');
    const selectionButtons = Array.from(document.querySelectorAll('[data-requires-selection]'));
    const filterInput = document.querySelector('#userFilter');
    const confirmModalElement = document.querySelector('#confirmBulkModal');
    const confirmModal = confirmModalElement ? new bootstrap.Modal(confirmModalElement) : null;
    const confirmTitle = document.querySelector('#confirmBulkTitle');
    const confirmMessage = document.querySelector('#confirmBulkMessage');
    const confirmSubmit = document.querySelector('#confirmBulkSubmit');
    let pendingSubmitter = null;

    const updateSelectionState = () => {
        const selectedCount = rowCheckboxes.filter((checkbox) => checkbox.checked).length;
        if (selectionCounter) {
            selectionCounter.textContent = `${selectedCount} selected`;
        }
        if (selectAll) {
            selectAll.checked = selectedCount > 0 && selectedCount === rowCheckboxes.length;
            selectAll.indeterminate = selectedCount > 0 && selectedCount < rowCheckboxes.length;
        }
        selectionButtons.forEach((button) => {
            button.disabled = selectedCount === 0;
        });
    };

    if (selectAll) {
        selectAll.addEventListener('change', () => {
            rowCheckboxes.forEach((checkbox) => {
                if (!checkbox.closest('tr').hidden) {
                    checkbox.checked = selectAll.checked;
                }
            });
            updateSelectionState();
        });
    }

    rowCheckboxes.forEach((checkbox) => checkbox.addEventListener('change', updateSelectionState));

    document.querySelectorAll('[data-confirm-title]').forEach((button) => {
        button.addEventListener('click', (event) => {
            if (button.disabled || !confirmModal) {
                return;
            }

            event.preventDefault();
            pendingSubmitter = button;
            confirmTitle.textContent = button.dataset.confirmTitle || 'Confirm action';
            confirmMessage.textContent = button.dataset.confirmMessage || 'This action cannot be undone.';
            confirmSubmit.textContent = button.dataset.confirmButton || 'Confirm';
            confirmModal.show();
        });
    });

    if (confirmSubmit) {
        confirmSubmit.addEventListener('click', () => {
            if (!pendingSubmitter) {
                return;
            }

            bulkForm.action = pendingSubmitter.formAction;
            bulkForm.submit();
        });
    }

    if (filterInput) {
        filterInput.addEventListener('input', () => {
            const query = filterInput.value.trim().toLowerCase();
            bulkForm.querySelectorAll('tbody tr[data-user-search]').forEach((row) => {
                const matches = row.dataset.userSearch.toLowerCase().includes(query);
                row.hidden = !matches;
                if (!matches) {
                    const checkbox = row.querySelector('.js-row-checkbox');
                    if (checkbox) {
                        checkbox.checked = false;
                    }
                }
            });
            updateSelectionState();
        });
    }

    updateSelectionState();
});
