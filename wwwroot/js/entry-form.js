// US01: форма за добавяне на автомобил — каскадни падащи менюта.
// Марка → Модел → Поколение → Година → Двигател → Скоростна кутия/задвижване.
// При избор на марка се показват само нейните модели, при избор на модел — само неговите поколения,
// при избор на година — само двигателите, предлагани през тази година, и т.н.
(function () {
    'use strict';

    const $ = function (id) { return document.getElementById(id); };
    const brand = $('BrandId');
    const model = $('ModelId');
    const generation = $('GenerationId');
    const year = $('Year');
    const engine = $('EngineName');
    const variant = $('CarEngineId');
    if (!brand) return;

    const reset = function (select, text) {
        select.innerHTML = '';
        const opt = document.createElement('option');
        opt.value = '';
        opt.textContent = text;
        select.appendChild(opt);
        select.disabled = true;
    };

    const clearFrom = function (level) {
        const chain = [
            [model, '— Първо изберете марка —'],
            [generation, '— Първо изберете модел —'],
            [year, '— Първо изберете поколение —'],
            [engine, '— Първо изберете година —'],
            [variant, '— Първо изберете двигател —']
        ];
        chain.slice(level).forEach(function (pair) { reset(pair[0], pair[1]); });
    };

    const pickSingle = function (select) {
        if (select.options.length === 2) { select.selectedIndex = 1; return true; }
        return false;
    };

    // Всяка стъпка връща Promise, за да може VIN попълването да ги извиква последователно
    async function loadModels() {
        clearFrom(0);
        if (!brand.value) return;
        await window.fillSelect(model, '/api/catalog/models?brandId=' + brand.value, '— Изберете модел —');
    }
    async function loadGenerations() {
        clearFrom(1);
        if (!model.value) return;
        await window.fillSelect(generation, '/api/catalog/generations?modelId=' + model.value, '— Изберете поколение —');
    }
    async function loadYears() {
        clearFrom(2);
        if (!generation.value) return;
        await window.fillSelect(year, '/api/catalog/years?generationId=' + generation.value, '— Изберете година —');
    }
    async function loadEngines() {
        clearFrom(3);
        if (!generation.value || !year.value) return;
        await window.fillSelect(engine, '/api/catalog/engines?generationId=' + generation.value + '&year=' + year.value, '— Изберете двигател —');
    }
    async function loadVariants() {
        clearFrom(4);
        if (!engine.value) return;
        await window.fillSelect(variant,
            '/api/catalog/variants?generationId=' + generation.value + '&year=' + year.value + '&engine=' + encodeURIComponent(engine.value),
            '— Изберете скоростна кутия —');
    }

    brand.addEventListener('change', async function () { await loadModels(); model.focus(); });
    model.addEventListener('change', async function () { await loadGenerations(); if (pickSingle(generation)) generation.dispatchEvent(new Event('change')); });
    generation.addEventListener('change', async function () { await loadYears(); if (pickSingle(year)) year.dispatchEvent(new Event('change')); });
    year.addEventListener('change', async function () { await loadEngines(); if (pickSingle(engine)) engine.dispatchEvent(new Event('change')); });
    engine.addEventListener('change', async function () { await loadVariants(); pickSingle(variant); });

    // ---------- Попълване по VIN (публична база NHTSA vPIC) ----------
    const vinInput = $('vin-input');
    const vinButton = $('vin-button');
    const vinResult = $('vin-result');

    const setValue = function (select, value) {
        if (value === null || value === undefined) return false;
        const v = String(value);
        if (!Array.prototype.some.call(select.options, function (o) { return o.value === v; })) return false;
        select.value = v;
        return true;
    };

    const showVin = function (html, css) {
        vinResult.className = 'small mt-2 ' + css;
        vinResult.innerHTML = '';
        vinResult.append(html);
    };

    async function decodeVin() {
        const vin = vinInput.value.trim();
        if (vin.length < 11) { showVin('Въведете целия VIN номер (17 символа).', 'text-danger'); return; }
        vinButton.disabled = true;
        showVin('Търсене в публичната база NHTSA...', 'text-body-secondary');
        try {
            const res = await fetch('/api/catalog/vin?vin=' + encodeURIComponent(vin));
            const data = await res.json();
            if (!data.ok) { showVin(data.error || 'VIN номерът не беше разпознат.', 'text-danger'); return; }

            const found = [data.make, data.model, data.year, data.displacementL ? data.displacementL + ' L' : null, data.fuel, data.transmission]
                .filter(Boolean).join(' · ');
            let filled = 0;
            if (setValue(brand, data.brandId)) {
                filled++;
                await loadModels();
                if (setValue(model, data.modelId)) {
                    filled++;
                    await loadGenerations();
                    if (setValue(generation, data.generationId)) {
                        filled++;
                        await loadYears();
                        if (setValue(year, data.year)) {
                            filled++;
                            await loadEngines();
                            if (setValue(engine, data.engineName)) {
                                filled++;
                                await loadVariants();
                                if (!setValue(variant, data.carEngineId)) pickSingle(variant);
                            }
                        }
                    }
                }
            }
            const msg = 'Разпознат: ' + found + '. ' + (filled >= 4
                ? 'Полетата са попълнени — проверете ги и изберете останалото.'
                : filled > 0 ? 'Попълнено е каквото има в каталога — изберете останалото ръчно.'
                    : 'Тази кола я няма в каталога — изберете я ръчно.');
            showVin(msg, filled > 0 ? 'text-success' : 'text-warning');
        } catch (e) {
            showVin('Грешка при връзката с публичната база.', 'text-danger');
        } finally {
            vinButton.disabled = false;
        }
    }

    if (vinButton) {
        vinButton.addEventListener('click', decodeVin);
        vinInput.addEventListener('keydown', function (e) { if (e.key === 'Enter') { e.preventDefault(); decodeVin(); } });
    }
})();
