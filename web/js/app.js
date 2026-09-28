// UI for the Bevo's Tacos checkout: a small hash router and one render function per page.

import { CATEGORIES, MENU, addOnsFor, findItem } from './menu.js';
import {
    LineItem, WalkupOrder, CateringOrder, EmptyOrderException, ValidationError,
    formatMoney, suggestTacos, TIP_OPTIONS, FREE_DELIVERY_THRESHOLD, CATERING_LEAD_DAYS,
    SALES_TAX_RATE_BPS, TACOS_PER_GUEST
} from './models.js';
import { STATUSES, toRecord, summarize, toCsv, loadOrders, saveOrders, nextOrderNumber, nextStatus } from './store.js';

const app = document.getElementById('app');
const $ = (sel, root = document) => root.querySelector(sel);
const esc = s => String(s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

let orders = loadOrders();

// One cart per order type so switching tabs doesn't lose anything
const carts = { walkup: [], catering: [] };
const forms = {
    walkup: { customerName: '', tipPercent: 0 },
    catering: { customerCode: '', deliveryFee: '', preferredCustomer: false, eventDate: '', guestCount: '' }
};
const submitted = { walkup: false, catering: false };
const menuState = { walkup: 'tacos', catering: 'tacos' };
const picked = {}; // add-ons chosen on a menu card before it's added: { itemId: Set }

// ---------------- routing ----------------

const routes = [
    [/^#?\/?$/, renderHome, 'home'],
    [/^#\/order\/(walkup|catering)$/, renderOrder, m => m[1]],
    [/^#\/receipt\/(\d+)$/, renderReceipt, null],
    [/^#\/kitchen$/, renderKitchen, 'kitchen'],
    [/^#\/dashboard$/, renderDashboard, 'dashboard'],
    [/^#\/about$/, renderAbout, 'about']
];

function route() {
    const hash = location.hash || '#/';
    for (const [pattern, render, nav] of routes) {
        const match = hash.match(pattern);
        if (match) {
            const active = typeof nav === 'function' ? nav(match) : nav;
            document.querySelectorAll('.nav-links a').forEach(a => a.classList.toggle('active', a.dataset.route === active));
            document.body.dataset.page = active ?? 'receipt';
            render(match);
            updateKitchenBadge();
            $('.nav-links').classList.remove('open');
            $('.nav-toggle').setAttribute('aria-expanded', 'false');
            return;
        }
    }
    location.hash = '#/';
}

window.addEventListener('hashchange', () => { route(); window.scrollTo(0, 0); });
$('.nav-toggle').addEventListener('click', e => {
    const open = $('.nav-links').classList.toggle('open');
    e.currentTarget.setAttribute('aria-expanded', String(open));
});

// ---------------- helpers ----------------

function toast(message) {
    const el = $('#toast');
    el.textContent = message;
    el.classList.add('show');
    clearTimeout(toast.timer);
    toast.timer = setTimeout(() => el.classList.remove('show'), 2200);
}

function updateKitchenBadge() {
    const open = orders.filter(o => o.status !== 'Complete').length;
    const badge = $('#kitchen-count');
    badge.hidden = open === 0;
    badge.textContent = open;
}

function persist() {
    saveOrders(orders);
    updateKitchenBadge();
}

function isoDate(date) {
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
}

function addDays(date, days) {
    const d = new Date(date);
    d.setDate(d.getDate() + days);
    return d;
}

function prettyDate(iso) {
    const [y, m, d] = iso.split('-').map(Number);
    return new Date(y, m - 1, d).toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric', year: 'numeric' });
}

function prettyTime(iso) {
    return new Date(iso).toLocaleString('en-US', { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' });
}

// Dollars typed into a form -> integer cents (NaN if it isn't a valid amount)
function parseDollars(text) {
    const t = String(text).trim();
    if (t === '') return 0;
    if (!/^\d+(\.\d{1,2})?$/.test(t)) return NaN;
    const [whole, frac = ''] = t.split('.');
    return Number(whole) * 100 + Number(frac.padEnd(2, '0'));
}

function buildOrder(type) {
    const lines = carts[type];
    if (type === 'walkup') {
        return new WalkupOrder({ lines, customerName: forms.walkup.customerName, tipPercent: Number(forms.walkup.tipPercent) });
    }
    const f = forms.catering;
    return new CateringOrder({
        lines,
        customerCode: f.customerCode,
        deliveryFee: parseDollars(f.deliveryFee),
        preferredCustomer: f.preferredCustomer,
        eventDate: f.eventDate,
        guestCount: f.guestCount === '' ? 0 : Number(f.guestCount)
    });
}

// Prices the cart as-is for the live summary, ignoring form errors
function preview(type) {
    const order = buildOrder(type);
    order.validateFields = () => ({});
    if (type === 'catering' && !Number.isInteger(order.requestedDeliveryFee)) order.requestedDeliveryFee = 0;
    try {
        return { order: order.calcTotals(), empty: false };
    } catch (e) {
        if (e instanceof EmptyOrderException) return { order, empty: true };
        throw e;
    }
}

function addToCart(type, itemId, qty = 1, addOnIds = []) {
    const line = new LineItem(itemId, qty, addOnIds);
    const existing = carts[type].find(l => l.key === line.key);
    if (existing) existing.quantity += qty;
    else carts[type].push(line);
}

// ---------------- home ----------------

function renderHome() {
    const s = summarize(orders);
    app.innerHTML = `
    <section class="hero hero-banner">
        <div class="card welcome">
            <p class="eyebrow">Welcome to</p>
            <h1 class="headline">Bevo's Tacos!<span class="underline"></span></h1>
            <p class="subtitle">Fresh tacos. Juicy burgers. Great people.</p>
            <p class="lead">Choose an order type to get started:</p>
            <div class="order-types">
                <a class="order-type walkup" href="#/order/walkup">
                    <span class="icon">🌮</span>
                    <span><b>Walk-Up Order</b><small>Quick &amp; easy · 8.25% sales tax</small></span>
                </a>
                <a class="order-type catering" href="#/order/catering">
                    <span class="icon">🎉</span>
                    <span><b>Catering Order</b><small>For your next event · free delivery at $1,000</small></span>
                </a>
            </div>
            <div class="home-links">
                <a href="#/kitchen">👩‍🍳 Kitchen board${s.orders ? ` · ${orders.filter(o => o.status !== 'Complete').length} open` : ''}</a>
                <a href="#/dashboard">📊 Sales dashboard</a>
                <a href="#/about">⚙️ How it works</a>
            </div>
        </div>
    </section>`;
}

// ---------------- ordering ----------------

function renderOrder(match) {
    const type = match[1];
    const cat = menuState[type];
    const isCatering = type === 'catering';

    app.innerHTML = `
    <section class="hero hero-truck">
        <div class="order-layout">
            <div class="card menu-card">
                <div class="order-head">
                    <div>
                        <p class="eyebrow">${isCatering ? 'Catering order' : 'Walk-up order'}</p>
                        <h1 class="page-title">${isCatering ? 'Feed the whole crew' : 'What are we eating?'}</h1>
                    </div>
                    <a class="switch-type" href="#/order/${isCatering ? 'walkup' : 'catering'}">Switch to ${isCatering ? 'walk-up' : 'catering'} →</a>
                </div>
                <div class="tabs" role="tablist" aria-label="Menu categories">
                    ${CATEGORIES.map(c => `<button type="button" role="tab" class="tab${c.id === cat ? ' on' : ''}" aria-selected="${c.id === cat}" data-cat="${c.id}">${esc(c.name)}</button>`).join('')}
                </div>
                <p class="cat-blurb">${esc(CATEGORIES.find(c => c.id === cat).blurb)}</p>
                <div class="menu-grid">
                    ${MENU.filter(m => m.category === cat).map(item => menuItemHtml(item)).join('')}
                </div>
            </div>
            <aside class="card cart-card" aria-label="Your order">
                <div id="cart"></div>
            </aside>
        </div>
    </section>`;

    app.querySelectorAll('.tab').forEach(btn => btn.addEventListener('click', () => {
        menuState[type] = btn.dataset.cat;
        renderOrder(match);
    }));

    app.querySelectorAll('.menu-item').forEach(card => {
        const itemId = card.dataset.id;
        card.querySelectorAll('.chip').forEach(chip => chip.addEventListener('click', () => {
            const set = picked[itemId] ??= new Set();
            set.has(chip.dataset.addon) ? set.delete(chip.dataset.addon) : set.add(chip.dataset.addon);
            chip.classList.toggle('on');
            chip.setAttribute('aria-pressed', String(set.has(chip.dataset.addon)));
            card.querySelector('.price').textContent = formatMoney(new LineItem(itemId, 1, [...set]).unitPrice);
        }));
        card.querySelector('.add').addEventListener('click', () => {
            const qtyInput = card.querySelector('.qty-input');
            const qty = Math.max(1, Math.min(999, parseInt(qtyInput.value, 10) || 1));
            addToCart(type, itemId, qty, [...(picked[itemId] ?? [])]);
            picked[itemId] = new Set();
            card.querySelectorAll('.chip').forEach(c => { c.classList.remove('on'); c.setAttribute('aria-pressed', 'false'); });
            card.querySelector('.price').textContent = formatMoney(findItem(itemId).price);
            qtyInput.value = isCatering ? 12 : 1;
            toast(`Added ${qty} × ${findItem(itemId).name}`);
            renderCart(type);
        });
    });

    renderCart(type);
}

function menuItemHtml(item) {
    const chosen = picked[item.id] ?? new Set();
    const price = new LineItem(item.id, 1, [...chosen]).unitPrice;
    const isCatering = location.hash.includes('catering');
    return `
    <article class="menu-item" data-id="${item.id}">
        <div class="menu-top">
            <span class="emoji" aria-hidden="true">${item.emoji}</span>
            <div>
                <h3>${esc(item.name)}</h3>
                <p>${esc(item.desc)}</p>
                ${item.tags.map(t => `<span class="tag tag-${t}">${t}</span>`).join('')}
            </div>
        </div>
        ${addOnsFor(item).length ? `<div class="chips">${addOnsFor(item).map(a => `
            <button type="button" class="chip${chosen.has(a.id) ? ' on' : ''}" aria-pressed="${chosen.has(a.id)}" data-addon="${a.id}">${esc(a.name)}${a.price ? ` +${formatMoney(a.price)}` : ''}</button>`).join('')}
        </div>` : ''}
        <div class="menu-bottom">
            <span class="price">${formatMoney(price)}</span>
            <label class="qty"><span class="sr-only">Quantity</span><input class="qty-input" type="number" min="1" max="999" value="${isCatering ? 12 : 1}"></label>
            <button type="button" class="btn add">Add</button>
        </div>
    </article>`;
}

function renderCart(type) {
    const isCatering = type === 'catering';
    const lines = carts[type];
    const { order, empty } = preview(type);
    const errors = submitted[type] ? safeErrors(type) : {};
    const f = forms[type];
    const fieldError = name => errors[name] ? `<span class="field-error" id="err-${name}">${esc(errors[name])}</span>` : '';
    const aria = name => errors[name] ? ` aria-invalid="true" aria-describedby="err-${name}"` : '';
    const minDate = isoDate(addDays(new Date(), CATERING_LEAD_DAYS));

    const progress = isCatering ? Math.min(100, Math.round((order.subtotal / FREE_DELIVERY_THRESHOLD) * 100)) : 0;

    $('#cart').innerHTML = `
        <h2 class="cart-title">Your order <span>${lines.reduce((s, l) => s + l.quantity, 0)} items</span></h2>
        ${lines.length ? `<ul class="cart-lines">
            ${lines.map((l, i) => `
            <li>
                <div class="line-info">
                    <b>${esc(l.item.name)}</b>
                    ${l.addOns.length ? `<small>${l.addOns.map(a => esc(a.name)).join(', ')}</small>` : ''}
                    <small>${formatMoney(l.unitPrice)} each</small>
                </div>
                <div class="stepper" aria-label="Quantity for ${esc(l.item.name)}">
                    <button type="button" data-line="${i}" data-step="-1" aria-label="One fewer">−</button>
                    <span>${l.quantity}</span>
                    <button type="button" data-line="${i}" data-step="1" aria-label="One more">+</button>
                </div>
                <span class="line-total">${formatMoney(l.lineTotal)}</span>
            </li>`).join('')}
        </ul>` : `<p class="empty-cart">Your cart is empty. Add something from the menu.</p>`}

        ${isCatering ? `
        <div class="delivery-meter" aria-label="Progress toward free delivery">
            <div class="meter"><span style="width:${progress}%"></span></div>
            <small>${f.preferredCustomer ? '⭐ Preferred customers always get free delivery.'
                : order.subtotal >= FREE_DELIVERY_THRESHOLD ? '🎉 Free delivery unlocked!'
                : `${formatMoney(FREE_DELIVERY_THRESHOLD - order.subtotal)} more for free delivery`}</small>
        </div>` : ''}

        <form id="checkout" novalidate>
            ${errors.form ? `<div class="form-error" role="alert">${esc(errors.form)}</div>` : ''}
            ${isCatering ? `
            <div class="grid-2">
                <label>Customer code
                    <input name="customerCode" maxlength="4" autocomplete="off" placeholder="e.g. MISA" value="${esc(f.customerCode)}"${aria('customerCode')}>
                    ${fieldError('customerCode')}
                </label>
                <label>Delivery fee ($)
                    <input name="deliveryFee" inputmode="decimal" placeholder="0.00" value="${esc(f.deliveryFee)}"${aria('deliveryFee')}>
                    ${fieldError('deliveryFee')}
                </label>
                <label>Event date
                    <input name="eventDate" type="date" min="${minDate}" value="${esc(f.eventDate)}"${aria('eventDate')}>
                    ${fieldError('eventDate')}
                </label>
                <label>Guests
                    <input name="guestCount" type="number" min="1" placeholder="How many?" value="${esc(f.guestCount)}"${aria('guestCount')}>
                    ${fieldError('guestCount')}
                </label>
            </div>
            ${Number(f.guestCount) > 0 ? `<button type="button" class="suggest" id="suggest">💡 Plan for ${f.guestCount} guests: add ${suggestTacos(Number(f.guestCount))} tacos (${TACOS_PER_GUEST} per guest)</button>` : ''}
            <label class="check"><input type="checkbox" name="preferredCustomer"${f.preferredCustomer ? ' checked' : ''}> Preferred customer</label>
            ` : `
            <label>Name for the order <small>(optional)</small>
                <input name="customerName" maxlength="40" autocomplete="given-name" placeholder="We'll call it out" value="${esc(f.customerName)}"${aria('customerName')}>
                ${fieldError('customerName')}
            </label>
            <fieldset class="tips">
                <legend>Tip the crew</legend>
                ${TIP_OPTIONS.map(p => `<label class="tip${Number(f.tipPercent) === p ? ' on' : ''}"><input type="radio" name="tipPercent" value="${p}"${Number(f.tipPercent) === p ? ' checked' : ''}>${p ? `${p}%` : 'None'}</label>`).join('')}
            </fieldset>`}

            <dl class="totals">
                <div><dt>Subtotal</dt><dd>${formatMoney(order.subtotal)}</dd></div>
                ${isCatering ? `
                <div><dt>Delivery</dt><dd>${order.deliveryWaivedReason && !empty ? `<s>${formatMoney(order.requestedDeliveryFee)}</s> Free` : formatMoney(order.deliveryFee)}</dd></div>
                ` : `
                <div><dt>Sales tax (${(SALES_TAX_RATE_BPS / 100).toFixed(2)}%)</dt><dd>${formatMoney(order.salesTax)}</dd></div>
                ${order.tip ? `<div><dt>Tip</dt><dd>${formatMoney(order.tip)}</dd></div>` : ''}`}
                <div class="grand"><dt>Total</dt><dd>${formatMoney(empty ? 0 : order.total)}</dd></div>
            </dl>
            <button class="btn btn-big" type="submit">Place ${isCatering ? 'catering' : 'walk-up'} order</button>
            ${lines.length ? `<button class="link-btn" type="button" id="clear-cart">Clear cart</button>` : ''}
        </form>`;

    $('#cart').querySelectorAll('.stepper button').forEach(btn => btn.addEventListener('click', () => {
        const line = carts[type][Number(btn.dataset.line)];
        line.quantity += Number(btn.dataset.step);
        if (line.quantity <= 0) carts[type].splice(Number(btn.dataset.line), 1);
        renderCart(type);
    }));

    $('#clear-cart')?.addEventListener('click', () => { carts[type] = []; renderCart(type); });

    $('#suggest')?.addEventListener('click', () => {
        const wanted = suggestTacos(Number(forms.catering.guestCount));
        const have = carts.catering.filter(l => l.item.category === 'tacos').reduce((s, l) => s + l.quantity, 0);
        if (have >= wanted) { toast(`You already have ${have} tacos. That's plenty!`); return; }
        // Split the extra tacos across the menu so the party gets variety
        const tacoIds = MENU.filter(m => m.category === 'tacos').map(m => m.id);
        const extra = wanted - have;
        const each = Math.floor(extra / tacoIds.length / 12) * 12;
        let remaining = extra;
        tacoIds.forEach((id, i) => {
            const qty = i === tacoIds.length - 1 ? remaining : each;
            if (qty > 0) addToCart('catering', id, qty);
            remaining -= qty;
        });
        toast(`Added ${extra} tacos across the menu`);
        renderCart(type);
    });

    const form = $('#checkout');
    // Keep form values in state and re-render the totals as the customer types
    form.addEventListener('input', e => {
        const el = e.target;
        if (!el.name) return;
        forms[type][el.name] = el.type === 'checkbox' ? el.checked : el.value;
        if (el.type === 'checkbox' || el.type === 'radio' || el.name === 'guestCount' || el.name === 'deliveryFee' || submitted[type]) {
            const focusName = el.type === 'text' || el.type === 'number' || el.inputMode ? el.name : null;
            const caret = focusName && el.selectionStart;
            renderCart(type);
            if (focusName) {
                const again = $(`#checkout [name="${focusName}"]`);
                again.focus();
                try { again.setSelectionRange(caret, caret); } catch { /* number inputs don't support it */ }
            }
        }
    });

    form.addEventListener('submit', e => {
        e.preventDefault();
        submitted[type] = true;
        try {
            const priced = buildOrder(type).calcTotals();
            const number = nextOrderNumber(orders);
            orders = [toRecord(priced, { id: crypto.randomUUID?.() ?? String(Date.now()), number }), ...orders];
            persist();
            carts[type] = [];
            submitted[type] = false;
            forms[type] = type === 'walkup'
                ? { customerName: '', tipPercent: 0 }
                : { customerCode: '', deliveryFee: '', preferredCustomer: false, eventDate: '', guestCount: '' };
            location.hash = `#/receipt/${number}`;
        } catch (err) {
            if (!(err instanceof ValidationError || err instanceof EmptyOrderException)) throw err;
            renderCart(type);
            ($('#checkout [aria-invalid="true"]') ?? $('#checkout .form-error'))?.focus?.();
            $('.cart-card').scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    });
}

function safeErrors(type) {
    const order = buildOrder(type);
    const errors = order.validate();
    if (!order.lines.some(l => l.quantity > 0)) errors.form = new EmptyOrderException().message;
    return errors;
}

// ---------------- receipt ----------------

function renderReceipt(match) {
    const r = orders.find(o => o.number === Number(match[1]));
    if (!r) {
        app.innerHTML = `<section class="hero"><div class="card narrow"><h1 class="page-title">Order not found</h1><p>This order isn't saved in this browser.</p><a class="btn" href="#/">Back to home</a></div></section>`;
        return;
    }
    const isCatering = r.type === 'Catering';
    app.innerHTML = `
    <section class="hero">
        <div class="card receipt">
            <div class="receipt-head">
                <div>
                    <p class="eyebrow">Order #${r.number} · ${esc(r.type === 'Walkup' ? 'Walk-up' : 'Catering')}</p>
                    <h1 class="headline green">Order Summary<span class="underline"></span></h1>
                </div>
                <span class="status status-${r.status.toLowerCase()}">${r.status}</span>
            </div>
            <dl class="receipt-meta">
                <div><dt>${isCatering ? 'Customer code' : 'Name'}</dt><dd>${esc(r.customer)}</dd></div>
                <div><dt>Placed</dt><dd>${prettyTime(r.placedAt)}</dd></div>
                ${isCatering ? `
                <div><dt>Event</dt><dd>${prettyDate(r.eventDate)}</dd></div>
                <div><dt>Guests</dt><dd>${r.guestCount}</dd></div>
                <div><dt>Preferred customer</dt><dd>${r.preferredCustomer ? 'Yes' : 'No'}</dd></div>` : ''}
                <div><dt>Total items</dt><dd>${r.totalItems}</dd></div>
            </dl>
            <table class="receipt-lines">
                <thead><tr><th>Item</th><th>Qty</th><th>Each</th><th>Total</th></tr></thead>
                <tbody>
                ${r.lines.map(l => `<tr><td>${esc(l.name)}${l.addOns.length ? `<small>${l.addOns.map(esc).join(', ')}</small>` : ''}</td><td>${l.quantity}</td><td>${formatMoney(l.unitPrice)}</td><td>${formatMoney(l.lineTotal)}</td></tr>`).join('')}
                </tbody>
            </table>
            <dl class="totals">
                <div><dt>Subtotal</dt><dd>${formatMoney(r.subtotal)}</dd></div>
                ${isCatering ? `<div><dt>Delivery fee${r.deliveryWaivedReason ? ` <small>(waived: ${esc(r.deliveryWaivedReason.toLowerCase())})</small>` : ''}</dt><dd>${r.deliveryWaivedReason ? `<s>${formatMoney(r.requestedDeliveryFee)}</s> ` : ''}${formatMoney(r.deliveryFee)}</dd></div>`
                    : `<div><dt>Sales tax (8.25%)</dt><dd>${formatMoney(r.salesTax)}</dd></div>${r.tip ? `<div><dt>Tip (${r.tipPercent}%)</dt><dd>${formatMoney(r.tip)}</dd></div>` : ''}`}
                <div class="grand"><dt>Total</dt><dd>${formatMoney(r.total)}</dd></div>
            </dl>
            <div class="receipt-actions no-print">
                <button class="btn" type="button" onclick="window.print()">🖨️ Print receipt</button>
                <a class="btn ghost" href="#/kitchen">Track in kitchen →</a>
                <a class="btn ghost" href="#/order/${isCatering ? 'catering' : 'walkup'}">New order</a>
            </div>
        </div>
    </section>`;
}

// ---------------- kitchen board ----------------

function renderKitchen() {
    const cols = STATUSES.map(status => {
        const list = orders.filter(o => o.status === status);
        return `
        <section class="kcol" aria-label="${status}">
            <h2>${status} <span>${list.length}</span></h2>
            ${list.length ? list.map(o => `
            <article class="kcard ${o.type.toLowerCase()}">
                <header><b>#${o.number}</b><span class="pill">${o.type === 'Walkup' ? 'Walk-up' : 'Catering'}</span></header>
                <p class="kname">${esc(o.customer)}</p>
                <ul>${o.lines.slice(0, 4).map(l => `<li>${l.quantity} × ${esc(l.name)}${l.addOns.length ? ` <small>(${l.addOns.map(esc).join(', ')})</small>` : ''}</li>`).join('')}${o.lines.length > 4 ? `<li><small>+${o.lines.length - 4} more</small></li>` : ''}</ul>
                <footer>
                    <small>${o.type === 'Catering' ? `📅 ${prettyDate(o.eventDate)}` : `🕒 ${prettyTime(o.placedAt)}`}</small>
                    <span>
                        <a href="#/receipt/${o.number}" class="mini">Receipt</a>
                        ${status !== 'Complete' ? `<button type="button" class="mini solid" data-advance="${o.number}">${nextStatus(status) === 'Complete' ? 'Complete ✓' : `→ ${nextStatus(status)}`}</button>` : ''}
                    </span>
                </footer>
            </article>`).join('') : `<p class="kempty">Nothing here.</p>`}
        </section>`;
    }).join('');

    app.innerHTML = `
    <section class="page">
        <div class="page-head">
            <div>
                <p class="eyebrow">Behind the counter</p>
                <h1 class="page-title">Kitchen board</h1>
                <p class="muted">Every order placed in this browser, moving from New to Complete.</p>
            </div>
            <button class="btn${orders.length ? ' ghost' : ''}" type="button" id="seed">Load sample orders</button>
        </div>
        <div class="kboard">${cols}</div>
    </section>`;

    app.querySelectorAll('[data-advance]').forEach(btn => btn.addEventListener('click', () => {
        const o = orders.find(x => x.number === Number(btn.dataset.advance));
        o.status = nextStatus(o.status);
        persist();
        renderKitchen();
    }));
    $('#seed')?.addEventListener('click', () => { seedOrders(); renderKitchen(); });
}

// ---------------- dashboard ----------------

function renderDashboard() {
    const s = summarize(orders);
    const maxItem = Math.max(1, ...s.topItems.map(t => t.quantity));
    const maxCat = Math.max(1, ...Object.values(s.categoryRevenue));
    const walkShare = s.revenue ? Math.round(s.byType.Walkup.revenue / s.revenue * 100) : 0;

    app.innerHTML = `
    <section class="page">
        <div class="page-head">
            <div>
                <p class="eyebrow">Manager view</p>
                <h1 class="page-title">Sales dashboard</h1>
                <p class="muted">Calculated live from the order history in this browser.</p>
            </div>
            <div class="head-actions">
                <button class="btn${orders.length ? ' ghost' : ''}" type="button" id="seed">Load sample orders</button>
                ${orders.length ? `<button class="btn ghost" type="button" id="csv">⬇ Export CSV</button><button class="btn ghost" type="button" id="reset">Clear history</button>` : ''}
            </div>
        </div>

        ${orders.length ? `
        <div class="kpis">
            <div class="kpi"><span>Revenue</span><b>${formatMoney(s.revenue)}</b></div>
            <div class="kpi"><span>Orders</span><b>${s.orders}</b></div>
            <div class="kpi"><span>Average ticket</span><b>${formatMoney(s.averageTicket)}</b></div>
            <div class="kpi"><span>Items sold</span><b>${s.itemsSold.toLocaleString('en-US')}</b></div>
            <div class="kpi"><span>Sales tax collected</span><b>${formatMoney(s.salesTaxCollected)}</b></div>
            <div class="kpi"><span>Free deliveries</span><b>${s.deliveryWaived}</b></div>
        </div>

        <div class="dash-grid">
            <div class="card panel">
                <h2>Revenue by order type</h2>
                <div class="split" role="img" aria-label="Walk-up ${walkShare}%, catering ${100 - walkShare}%">
                    <span class="split-walkup" style="width:${walkShare}%"></span><span class="split-catering" style="width:${100 - walkShare}%"></span>
                </div>
                <dl class="legend">
                    <div><dt><i class="dot walkup"></i>Walk-up</dt><dd>${formatMoney(s.byType.Walkup.revenue)} · ${s.byType.Walkup.orders} orders</dd></div>
                    <div><dt><i class="dot catering"></i>Catering</dt><dd>${formatMoney(s.byType.Catering.revenue)} · ${s.byType.Catering.orders} orders</dd></div>
                </dl>
            </div>
            <div class="card panel">
                <h2>Top sellers</h2>
                <ul class="bars">${s.topItems.map(t => `<li><span>${esc(t.name)}</span><div class="bar"><i style="width:${t.quantity / maxItem * 100}%"></i></div><b>${t.quantity}</b></li>`).join('')}</ul>
            </div>
            <div class="card panel">
                <h2>Food sales by category</h2>
                <ul class="bars">${CATEGORIES.map(c => `<li><span>${c.name}</span><div class="bar"><i class="alt" style="width:${s.categoryRevenue[c.id] / maxCat * 100}%"></i></div><b>${formatMoney(s.categoryRevenue[c.id])}</b></li>`).join('')}</ul>
            </div>
        </div>

        <div class="card panel">
            <h2>Order history</h2>
            <div class="table-wrap">
            <table class="history">
                <thead><tr><th>#</th><th>Placed</th><th>Type</th><th>Customer</th><th class="num">Items</th><th class="num">Total</th><th>Status</th></tr></thead>
                <tbody>${orders.map(o => `<tr><td><a href="#/receipt/${o.number}">${o.number}</a></td><td>${prettyTime(o.placedAt)}</td><td>${o.type === 'Walkup' ? 'Walk-up' : 'Catering'}</td><td>${esc(o.customer)}</td><td class="num">${o.totalItems}</td><td class="num">${formatMoney(o.total)}</td><td><span class="status status-${o.status.toLowerCase()}">${o.status}</span></td></tr>`).join('')}</tbody>
            </table>
            </div>
        </div>` : `<div class="card empty-state"><p>No orders yet. Place one, or load a day of sample orders to see the dashboard fill in.</p></div>`}
    </section>`;

    $('#seed')?.addEventListener('click', () => { seedOrders(); renderDashboard(); });
    $('#reset')?.addEventListener('click', () => {
        if (!confirm('Clear every order saved in this browser?')) return;
        orders = [];
        persist();
        renderDashboard();
    });
    $('#csv')?.addEventListener('click', () => {
        const blob = new Blob([toCsv(orders)], { type: 'text/csv' });
        const a = Object.assign(document.createElement('a'), { href: URL.createObjectURL(blob), download: 'bevos-tacos-orders.csv' });
        a.click();
        URL.revokeObjectURL(a.href);
    });
}

// A realistic day of orders, priced by the real models so the numbers are consistent
function seedOrders() {
    const now = new Date();
    const at = (hoursAgo) => new Date(now.getTime() - hoursAgo * 3600000).toISOString();
    const eventDate = isoDate(addDays(now, 5));
    const L = (id, q, a = []) => new LineItem(id, q, a);
    const samples = [
        [new WalkupOrder({ customerName: 'Maya', tipPercent: 18, lines: [L('taco-brisket', 2), L('taco-pastor', 1, ['guac']), L('drink-horchata', 1)] }), 6.5, 'Complete'],
        [new WalkupOrder({ customerName: 'Jordan', lines: [L('burger-hatch', 1, ['double']), L('side-queso', 1), L('drink-topo', 1)] }), 5.8, 'Complete'],
        [new CateringOrder({ customerCode: 'MISA', deliveryFee: 10000, eventDate, guestCount: 40, lines: [L('taco-brisket', 48), L('taco-migas', 36), L('burger-classic', 30), L('side-guac', 6)] }), 5, 'Cooking'],
        [new WalkupOrder({ customerName: 'Priya', tipPercent: 20, lines: [L('taco-veggie', 3, ['queso']), L('side-elote', 1)] }), 4.2, 'Complete'],
        [new CateringOrder({ customerCode: 'UTX', deliveryFee: 15000, preferredCustomer: true, eventDate, guestCount: 25, lines: [L('taco-pastor', 36), L('taco-veggie', 24), L('drink-tea', 25)] }), 3.1, 'New'],
        [new WalkupOrder({ lines: [L('burger-classic', 2), L('drink-tea', 2, ['large'])] }), 2.4, 'Ready'],
        [new CateringOrder({ customerCode: 'BEVO', deliveryFee: 20000, eventDate, guestCount: 120, lines: [L('taco-brisket', 144), L('taco-pastor', 120), L('burger-classic', 60, ['queso']), L('side-queso', 20)] }), 1.5, 'New'],
        [new WalkupOrder({ customerName: 'Sam', tipPercent: 15, lines: [L('taco-brisket', 4, ['flour']), L('drink-topo', 2)] }), 0.6, 'Cooking'],
        [new WalkupOrder({ customerName: 'Alex', lines: [L('burger-mushroom', 1), L('side-elote', 1)] }), 0.2, 'New']
    ];
    let number = nextOrderNumber(orders);
    const records = samples.map(([order, hoursAgo, status]) => {
        order.today = now;
        const r = toRecord(order.calcTotals(), { id: `seed-${number}`, number: number++, placedAt: at(hoursAgo) });
        r.status = status;
        return r;
    });
    orders = [...records.reverse(), ...orders];
    persist();
    toast(`Loaded ${records.length} sample orders`);
}

// ---------------- about ----------------

function renderAbout() {
    app.innerHTML = `
    <section class="page about">
        <p class="eyebrow">How it works</p>
        <h1 class="page-title">One set of rules, two kinds of customers.</h1>
        <p class="muted lead-wide">Bevo's Tacos started as an ASP.NET Core MVC app in C#. This live version ports the same domain model to JavaScript so it can run on GitHub Pages with no server. It keeps the original design and adds a full menu, a kitchen board and a sales dashboard.</p>

        <div class="about-grid">
            <div class="card panel">
                <h2>The class design</h2>
<pre class="code">Order (abstract)
│  calcTotals()      validate → count items
│                    (EmptyOrderException if none)
│                    → subtotal → calcTotal()
│  calcTotal()       each subclass adds its charges
│
├── WalkupOrder      + 8.25% sales tax
│                    + optional tip (0/15/18/20%)
│
└── CateringOrder    + delivery fee ($0–$250)
                     waived if preferred or ≥ $1,000</pre>
                <p>The base class runs the steps every order shares and leaves one step for each subclass (the template method pattern).</p>
            </div>
            <div class="card panel">
                <h2>Business rules</h2>
                <table class="rules">
                    <tr><th>Tacos / burgers</th><td>$2.75 / $4.50 base, add-ons priced per item</td></tr>
                    <tr><th>Sales tax</th><td>8.25% of the subtotal, rounded half away from zero to the cent</td></tr>
                    <tr><th>Customer code</th><td>2–4 letters, required for catering</td></tr>
                    <tr><th>Delivery fee</th><td>$0 to $250, waived for preferred customers and orders of $1,000+</td></tr>
                    <tr><th>Event date</th><td>At least ${CATERING_LEAD_DAYS} days out</td></tr>
                    <tr><th>Empty orders</th><td>Rejected with an EmptyOrderException</td></tr>
                </table>
            </div>
            <div class="card panel">
                <h2>Engineering choices</h2>
                <ul class="ticks">
                    <li>All money is stored as whole cents, so totals never pick up floating-point error.</li>
                    <li>Validation collects every field error at once and shows each one next to its field.</li>
                    <li>38 unit tests run with Node's built-in test runner on every push, next to the 24 xUnit tests for the C# version.</li>
                    <li>GitHub Actions runs the tests, then publishes the site to GitHub Pages.</li>
                    <li>No frameworks, just ES modules, and orders are saved in the browser.</li>
                </ul>
            </div>
        </div>
    </section>`;
}

route();
