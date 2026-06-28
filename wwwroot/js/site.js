document.addEventListener('click', e => {
  const opener = e.target.closest('[data-open]');
  if (opener) document.getElementById(opener.dataset.open)?.classList.add('show');
  if (e.target.matches('[data-close]') || e.target.classList.contains('modal')) e.target.closest('.modal')?.classList.remove('show');
});


// Personal Vault merged scripts
// ── Lucide icons ────────────────────────────────────────────────────────────
if (window.lucide) if (window.lucide) { lucide.createIcons(); }

// ── Theme toggle (persisted) ───────────────────────────────────────────────
const THEME_KEY = 'pv-theme';
const root = document.documentElement;
const saved = localStorage.getItem(THEME_KEY);
if (saved) root.setAttribute('data-theme', saved);

document.querySelectorAll('#themeToggle, #themeToggleInline, .theme-toggle, .theme-toggle-inline').forEach(btn => {
    btn.addEventListener('click', () => {
        const next = root.getAttribute('data-theme') === 'light' ? 'dark' : 'light';
        root.setAttribute('data-theme', next);
        localStorage.setItem(THEME_KEY, next);
    });
});

// ── Sidebar toggle (mobile) ────────────────────────────────────────────────
document.getElementById('sidebarToggle')?.addEventListener('click', () => {
    document.getElementById('sidebar')?.classList.toggle('open');
});

// ── Confirm before delete forms ────────────────────────────────────────────
document.querySelectorAll('form[data-confirm]').forEach(form => {
    form.addEventListener('submit', e => {
        if (!confirm(form.dataset.confirm || 'Are you sure?')) e.preventDefault();
    });
});

// ── Auto-dismiss toast ──────────────────────────────────────────────────────
setTimeout(() => {
    document.querySelectorAll('.toast').forEach(t => {
        t.style.transition = 'opacity .4s';
        t.style.opacity = '0';
        setTimeout(() => t.remove(), 400);
    });
}, 4000);

// ── Image preview on file input ────────────────────────────────────────────
document.querySelectorAll('input[type="file"][data-preview]').forEach(input => {
    input.addEventListener('change', () => {
        const target = document.querySelector(input.dataset.preview);
        if (!target || !input.files?.[0]) return;
        const reader = new FileReader();
        reader.onload = e => { target.src = e.target.result; };
        reader.readAsDataURL(input.files[0]);
    });
});

// ── Personal Vault: template item helper ───────────────────────────────────
const useTemplateToggle = document.getElementById('useTemplateToggle');
const templateFields = document.getElementById('templateFields');
const templateTypeFilter = document.getElementById('templateTypeFilter');
const templateItemSelect = document.getElementById('templateItemSelect');

useTemplateToggle?.addEventListener('change', () => {
    templateFields?.classList.toggle('is-hidden', !useTemplateToggle.checked);
});

templateTypeFilter?.addEventListener('change', () => {
    const typeId = templateTypeFilter.value;
    [...templateItemSelect.options].forEach(option => {
        if (!option.value) return;
        option.hidden = !!typeId && option.dataset.type !== typeId;
    });
    templateItemSelect.value = '';
});

templateItemSelect?.addEventListener('change', () => {
    const option = templateItemSelect.selectedOptions[0];
    if (!option || !option.value) return;

    const setValue = (name, value) => {
        if (value === undefined || value === null || value === '') return;
        const el = document.querySelector(`[name="${name}"]`);
        if (el) el.value = value;
    };

    setValue('Description', option.dataset.description);
    setValue('Brand', option.dataset.brand);
    setValue('Model', option.dataset.model);
    setValue('Manufacturer', option.dataset.manufacturer);
    setValue('CaseType', option.dataset.casetype);
    setValue('MediaFormat', option.dataset.mediaformat);
    setValue('Instruction', option.dataset.instruction);
    setValue('Memory', option.dataset.memory);
    setValue('Owner', option.dataset.owner);
    setValue('ReleaseYear', option.dataset.releaseyear);
    setValue('Tested', option.dataset.tested);
    setValue('Boxed', option.dataset.boxed?.toLowerCase());
    setValue('Sell', option.dataset.sell?.toLowerCase());
    setValue('PurchasedFrom', option.dataset.purchasefrom);
    setValue('WarrantyInfo', option.dataset.warrantyinfo);
    setValue('ManualUrl', option.dataset.manualurl);
    setValue('CustomStatus', option.dataset.customstatus);
    setValue('CategoryId', option.dataset.category);
    setValue('ItemTypeId', option.dataset.itemtype);
    setValue('PlatformId', option.dataset.platform);
    setValue('LocationId', option.dataset.location);
    setValue('Condition', option.dataset.condition);

    const status = option.dataset.status;
    if (status) {
        const statusRadio = document.querySelector(`input[name="Status"][value="${status}"]`);
        if (statusRadio) statusRadio.checked = true;
    }
});
