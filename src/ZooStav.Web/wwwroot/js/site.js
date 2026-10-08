/* ============================================================
   ZooStav — клиентские скрипты страницы животного
   ============================================================ */
(function () {
    'use strict';

    // ---------- Автоскрытие уведомления ----------
    var toast = document.getElementById('zooToast');
    if (toast) {
        setTimeout(function () {
            toast.style.transition = 'opacity .6s ease';
            toast.style.opacity = '0';
            setTimeout(function () { toast.remove(); }, 700);
        }, 6000);
    }

    // ---------- Быстрый выбор суммы доната ----------
    document.querySelectorAll('.zoo-preset').forEach(function (button) {
        button.addEventListener('click', function () {
            var input = document.querySelector('input[name="Amount"]');
            if (input) {
                input.value = button.dataset.amount;
                input.focus();
            }
        });
    });

    // ---------- Живые обновления дневника (SignalR) ----------
    var config = window.zooLive;
    if (!config || typeof signalR === 'undefined') {
        setLiveStatus('нет соединения (скрипт SignalR не загружен)');
        return;
    }

    function setLiveStatus(text) {
        var el = document.getElementById('liveStatus');
        if (el) { el.textContent = 'Живые обновления: ' + text; }
    }

    var connection = new signalR.HubConnectionBuilder()
        .withUrl(config.hubUrl)
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    connection.on('diaryEntryCreated', function (entry) {
        var feed = document.getElementById('liveFeed');
        if (feed) {
            var li = document.createElement('li');
            li.className = 'zoo-feed__new';
            li.innerHTML = '<span class="zoo-tag zoo-tag--sm">' + entry.type + '</span>' +
                '<strong>' + entry.title + '</strong>' +
                '<small>' + new Date(entry.occurredAtUtc).toLocaleString('ru-RU') + ' · API</small>';
            feed.prepend(li);
            while (feed.children.length > 8) { feed.removeChild(feed.lastChild); }
        }

        var timeline = document.getElementById('diaryTimeline');
        if (timeline) {
            var item = document.createElement('li');
            item.className = 'zoo-timeline__item zoo-feed__new';
            item.innerHTML = '<div class="zoo-timeline__icon">🆕</div><div class="zoo-timeline__body">' +
                '<div class="zoo-timeline__meta"><span class="zoo-tag">' + entry.type + '</span>' +
                '<time>' + new Date(entry.occurredAtUtc).toLocaleString('ru-RU') + '</time>' +
                '<span class="zoo-muted">· только что добавлено</span></div>' +
                '<span class="zoo-timeline__title">' + entry.title + '</span></div>';
            timeline.prepend(item);
        }
    });

    connection.start()
        .then(function () {
            setLiveStatus('подключено');
            if (config.animalSlug) { return connection.invoke('JoinAnimal', config.animalSlug); }
            return null;
        })
        .catch(function (err) {
            console.warn('SignalR недоступен:', err);
            setLiveStatus('недоступно (страница работает и без онлайн-обновлений)');
        });

    connection.onreconnecting(function () { setLiveStatus('переподключение…'); });
    connection.onreconnected(function () { setLiveStatus('подключено'); });
    connection.onclose(function () { setLiveStatus('соединение закрыто'); });
})();
