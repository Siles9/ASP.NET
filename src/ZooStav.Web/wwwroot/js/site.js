(function () {
    'use strict';

    var toast = document.getElementById('zooToast');
    if (toast) {
        setTimeout(function () {
            toast.style.transition = 'opacity .6s ease';
            toast.style.opacity = '0';
            setTimeout(function () { toast.remove(); }, 700);
        }, 6000);
    }

    document.querySelectorAll('.zoo-preset').forEach(function (button) {
        button.addEventListener('click', function () {
            var input = document.querySelector('input[name="Amount"]');
            if (input) {
                input.value = button.dataset.amount;
                input.focus();
            }
        });
    });
})();
