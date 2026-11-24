// US01: форма за добавяне на автомобил — каскадни падащи менюта.
// Марка → Модел → Поколение → Година → Двигател → Скоростна кутия/задвижване.
// При избор на марка се показват само нейните модели, при избор на модел — само неговите поколения и т.н.
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
            [engine, '— Първо изберете поколение —'],
            [variant, '— Първо изберете двигател —']
        ];
        chain.slice(level).forEach(function (pair) { reset(pair[0], pair[1]); });
    };

    const autoPickSingle = function (select) {
        // ако има само една възможност — избира се автоматично
        if (select.options.length === 2) {
            select.selectedIndex = 1;
            select.dispatchEvent(new Event('change'));
        }
    };

    brand.addEventListener('change', async function () {
        clearFrom(0);
        if (!brand.value) return;
        await window.fillSelect(model, '/api/catalog/models?brandId=' + brand.value, '— Изберете модел —');
        model.focus();
    });

    model.addEventListener('change', async function () {
        clearFrom(1);
        if (!model.value) return;
        await window.fillSelect(generation, '/api/catalog/generations?modelId=' + model.value, '— Изберете поколение —');
        autoPickSingle(generation);
    });

    generation.addEventListener('change', async function () {
        clearFrom(2);
        if (!generation.value) return;
        await Promise.all([
            window.fillSelect(year, '/api/catalog/years?generationId=' + generation.value, '— Изберете година —'),
            window.fillSelect(engine, '/api/catalog/engines?generationId=' + generation.value, '— Изберете двигател —')
        ]);
        autoPickSingle(year);
        autoPickSingle(engine);
    });

    engine.addEventListener('change', async function () {
        clearFrom(4);
        if (!engine.value) return;
        await window.fillSelect(variant,
            '/api/catalog/variants?generationId=' + generation.value + '&engine=' + encodeURIComponent(engine.value),
            '— Изберете скоростна кутия —');
        autoPickSingle(variant);
    });

    // Карти за избор на дефекти — показва/скрива описанието
    const defectBoxes = document.querySelectorAll('input[name="SelectedDefects"]');
    const issues = $('issues-group');
    const toggleIssues = function () {
        if (!issues) return;
        const any = Array.prototype.some.call(defectBoxes, function (c) { return c.checked; });
        issues.classList.toggle('d-none', !any && !$('CommonIssues').value);
    };
    defectBoxes.forEach(function (c) { c.addEventListener('change', toggleIssues); });
    toggleIssues();

    // Слайдер за оценката
    const rating = $('ReliabilityRating');
    const ratingValue = $('rating-value');
    if (rating && ratingValue) {
        const show = function () { ratingValue.textContent = rating.value ? rating.value + ' / 10' : '—'; };
        rating.addEventListener('input', show);
        show();
    }
})();
