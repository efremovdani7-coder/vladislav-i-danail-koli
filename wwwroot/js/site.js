// АвтоНадеждност.бг — общ JavaScript

(function () {
    'use strict';

    // ---------- US08: тъмна / светла тема (запазва се в localStorage) ----------
    const themeBtn = document.getElementById('theme-toggle');
    if (themeBtn) {
        themeBtn.addEventListener('click', function () {
            const root = document.documentElement;
            const next = root.getAttribute('data-bs-theme') === 'dark' ? 'light' : 'dark';
            root.setAttribute('data-bs-theme', next);
            try { localStorage.setItem('theme', next); } catch (e) { /* браузърът не позволява */ }
            document.dispatchEvent(new CustomEvent('themechange', { detail: next }));
        });
    }

    // ---------- Автоматично скриване на съобщенията ----------
    document.querySelectorAll('main > .alert-dismissible').forEach(function (alert) {
        setTimeout(function () { bootstrap.Alert.getOrCreateInstance(alert).close(); }, 6000);
    });

    // ---------- US09: търсачка с подсказки (от 2 символа, debounce ~300 ms) ----------
    const input = document.getElementById('site-search');
    const box = document.getElementById('site-search-results');
    if (input && box) {
        let timer = null;
        let active = -1;
        let lastQuery = '';

        const hide = function () { box.classList.add('d-none'); box.innerHTML = ''; active = -1; };

        const render = function (items) {
            box.innerHTML = '';
            active = -1;
            if (!items.length) {
                const empty = document.createElement('div');
                empty.className = 'list-group-item small text-body-secondary';
                empty.textContent = 'Няма намерени марки или модели';
                box.appendChild(empty);
            }
            items.forEach(function (item) {
                const a = document.createElement('a');
                a.className = 'list-group-item list-group-item-action d-flex align-items-center gap-2';
                a.href = item.url;
                const icon = document.createElement('i');
                icon.className = 'bi ' + (item.type === 'brand' ? 'bi-trophy text-warning' : 'bi-car-front text-primary');
                const text = document.createElement('span');
                text.textContent = item.label;
                const tag = document.createElement('small');
                tag.className = 'ms-auto text-body-secondary';
                tag.textContent = item.type === 'brand' ? 'марка' : 'модел';
                a.append(icon, text, tag);
                box.appendChild(a);
            });
            box.classList.remove('d-none');
        };

        input.addEventListener('input', function () {
            const q = input.value.trim();
            clearTimeout(timer);
            if (q.length < 2) { hide(); return; }
            timer = setTimeout(async function () {
                lastQuery = q;
                try {
                    const res = await fetch('/api/catalog/suggest?q=' + encodeURIComponent(q));
                    const items = await res.json();
                    if (q === lastQuery) render(items);
                } catch (e) { hide(); }
            }, 300);
        });

        input.addEventListener('keydown', function (e) {
            const links = box.querySelectorAll('a');
            if (!links.length) return;
            if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
                e.preventDefault();
                active = (active + (e.key === 'ArrowDown' ? 1 : -1) + links.length) % links.length;
                links.forEach(function (l, i) { l.classList.toggle('active-suggestion', i === active); });
            } else if (e.key === 'Enter') {
                e.preventDefault();
                window.location.href = links[Math.max(active, 0)].href;
            } else if (e.key === 'Escape') {
                hide();
            }
        });

        document.addEventListener('click', function (e) {
            if (!box.contains(e.target) && e.target !== input) hide();
        });
    }

    // ---------- Каскадни падащи менюта (марка → модел) ----------
    // <select data-cascade-target="#ModelId" data-cascade-url="/api/catalog/models?brandId=" data-cascade-empty="— Всички модели —">
    document.querySelectorAll('select[data-cascade-target]').forEach(function (source) {
        source.addEventListener('change', async function () {
            const target = document.querySelector(source.dataset.cascadeTarget);
            if (!target) return;
            await window.fillSelect(target, source.value ? source.dataset.cascadeUrl + encodeURIComponent(source.value) : null,
                source.value ? (source.dataset.cascadeEmpty || '— Изберете —') : (source.dataset.cascadeDisabled || '— Първо изберете марка —'));
            target.dispatchEvent(new Event('change', { bubbles: true }));
        });
    });

    /** Зарежда опции в падащо меню от API (елементи {id, name}). */
    window.fillSelect = async function (select, url, placeholder) {
        select.innerHTML = '';
        const first = document.createElement('option');
        first.value = '';
        first.textContent = placeholder;
        select.appendChild(first);
        select.disabled = !url;
        if (!url) return [];
        try {
            const res = await fetch(url);
            const items = await res.json();
            items.forEach(function (item) {
                const opt = document.createElement('option');
                if (typeof item === 'object') {
                    opt.value = item.id;
                    opt.textContent = item.name;
                } else {
                    opt.value = item;
                    opt.textContent = item;
                }
                select.appendChild(opt);
            });
            select.disabled = items.length === 0;
            return items;
        } catch (e) {
            select.disabled = true;
            return [];
        }
    };

    // ---------- US11 / US13: известия и съобщения на живо (SignalR) ----------
    if (window.signalR) {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/chat')
            .withAutomaticReconnect()
            .build();
        window.chatConnection = connection;

        const bump = function (id) {
            const badge = document.getElementById(id);
            if (!badge) return;
            badge.textContent = (parseInt(badge.textContent, 10) || 0) + 1;
            badge.classList.remove('d-none');
        };

        const toast = function (title, text, url) {
            const el = document.getElementById('live-toast');
            if (!el) return;
            document.getElementById('live-toast-title').textContent = title;
            const body = document.getElementById('live-toast-body');
            body.innerHTML = '';
            if (url) {
                const a = document.createElement('a');
                a.href = url;
                a.textContent = text;
                body.appendChild(a);
            } else {
                body.textContent = text;
            }
            bootstrap.Toast.getOrCreateInstance(el).show();
        };

        connection.on('notification', function (n) {
            bump('badge-notifications');
            toast('Известие', n.message, n.url);
        });

        connection.on('receiveMessage', function (m) {
            // ако чатът с този човек е отворен, chat страницата сама показва съобщението
            if (window.activeChatPartnerId === m.senderId) return;
            bump('badge-messages');
            toast('Ново съобщение от ' + m.senderNickname, m.text, '/Chat?userId=' + encodeURIComponent(m.senderId));
        });

        connection.start().catch(function (err) { console.warn('SignalR:', err); });
    }
})();
