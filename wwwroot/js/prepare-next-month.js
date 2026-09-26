(() => {
    const form = document.getElementById('prepare-next-month');
    if (!form) return;
    const choices = [...form.querySelectorAll('input[name="selectedEntries"]')];
    const submit = document.getElementById('prepare-submit');
    const currency = new Intl.NumberFormat('en-GB', { style: 'currency', currency: 'GBP' });
    const update = () => {
        const selected = choices.filter(input => input.checked);
        const total = income => selected.filter(input => (input.dataset.source === 'income') === income)
            .reduce((sum, input) => sum + Math.round(Number(input.dataset.amount) * 100), 0) / 100;
        document.getElementById('prepare-count').textContent = selected.length;
        document.getElementById('prepare-income').textContent = currency.format(total(true));
        document.getElementById('prepare-allocations').textContent = currency.format(total(false));
        submit.textContent = selected.length ? `Prepare ${submit.dataset.month}` : 'Continue without adding entries';
    };
    form.addEventListener('change', update);
    form.querySelectorAll('[data-prepare-select]').forEach(button => {
        button.addEventListener('click', () => {
            button.closest('fieldset').querySelectorAll('input[name="selectedEntries"]').forEach(input => {
                input.checked = button.dataset.prepareSelect === 'all';
            });
            update();
        });
    });
    form.addEventListener('submit', () => { submit.disabled = true; });
    window.addEventListener('pageshow', () => { submit.disabled = false; update(); });
    update();
})();
