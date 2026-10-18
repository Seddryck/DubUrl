// Progressive enhancement: the examples remain readable without clipboard access.
document.querySelectorAll('.workbench-copy').forEach(button => {
  const target = document.getElementById(button.dataset.copyTarget);
  if (!target || !navigator.clipboard?.writeText) return;

  button.hidden = false;
  button.addEventListener('click', async () => {
    const status = document.querySelector('.workbench-status');
    const label = button.querySelector('span');
    const code = target.querySelector('code') || target;
    try {
      await navigator.clipboard.writeText(code.textContent.trim());
      label.textContent = 'Copied';
      status.textContent = button.getAttribute('aria-label') + ': copied to clipboard.';
    } catch {
      label.textContent = 'Select code';
      status.textContent = 'Clipboard access is unavailable. Select and copy the code directly.';
    }
    window.clearTimeout(button.copyTimeout);
    button.copyTimeout = window.setTimeout(() => { label.textContent = 'Copy'; }, 2500);
  });
});
