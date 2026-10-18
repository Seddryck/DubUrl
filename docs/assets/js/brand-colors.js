(function () {
  'use strict';

  var page = document.querySelector('.brand-colors');
  if (!page) return;
  var status = page.querySelector('[role="status"]');

  page.addEventListener('click', async function (event) {
    var button = event.target.closest('.brand-color__copy');
    if (!button || !page.contains(button) || button.disabled) return;

    var codeId = button.getAttribute('data-copy-code');
    var value = codeId ? document.getElementById(codeId).textContent : button.getAttribute('data-color');
    var original = button.textContent;
    status.textContent = '';
    button.disabled = true;

    try {
      await navigator.clipboard.writeText(value);
      button.textContent = 'Copied!';
      status.textContent = codeId ? 'Sass example copied.' : value + ' copied.';
      window.setTimeout(function () {
        button.textContent = original;
        button.disabled = false;
      }, 1400);
    } catch (error) {
      button.disabled = false;
      status.textContent = 'Could not copy. Select the text and copy it manually.';
    }
  });
}());
