document.addEventListener('click', e => {
  const opener = e.target.closest('[data-open]');
  if (opener) document.getElementById(opener.dataset.open)?.classList.add('show');
  if (e.target.matches('[data-close]') || e.target.classList.contains('modal')) e.target.closest('.modal')?.classList.remove('show');
});
