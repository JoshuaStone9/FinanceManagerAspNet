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

document.getElementById('themeToggle')?.addEventListener('click', () => {
    const next = root.getAttribute('data-theme') === 'light' ? 'dark' : 'light';
    root.setAttribute('data-theme', next);
    localStorage.setItem(THEME_KEY, next);
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
