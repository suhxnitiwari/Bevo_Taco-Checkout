// Order history, kitchen statuses and sales analytics.
// Orders are saved in the browser (localStorage) so the demo works with no server.

import { MENU, CATEGORIES } from './menu.js';

export const STATUSES = ['New', 'Cooking', 'Ready', 'Complete'];
const STORAGE_KEY = 'bevos-tacos-orders-v1';

// Turns a priced Order into a plain record we can save and show on a receipt
export function toRecord(order, { id, number, placedAt = new Date().toISOString() }) {
    const record = {
        id,
        number,
        placedAt,
        status: 'New',
        type: order.customerType,
        customer: order.customerType === 'Walkup' ? (order.customerName || 'Walk-up guest') : order.customerCode,
        lines: order.lines.filter(l => l.quantity > 0).map(l => ({
            itemId: l.itemId,
            name: l.item.name,
            category: l.item.category,
            quantity: l.quantity,
            addOns: l.addOns.map(a => a.name),
            unitPrice: l.unitPrice,
            lineTotal: l.lineTotal
        })),
        totalItems: order.totalItems,
        subtotal: order.subtotal,
        total: order.total
    };
    if (order.customerType === 'Walkup') {
        Object.assign(record, { salesTax: order.salesTax, tip: order.tip, tipPercent: order.tipPercent });
    } else {
        Object.assign(record, {
            deliveryFee: order.deliveryFee,
            requestedDeliveryFee: order.requestedDeliveryFee,
            deliveryWaivedReason: order.deliveryWaivedReason,
            preferredCustomer: order.preferredCustomer,
            eventDate: order.eventDate,
            guestCount: order.guestCount
        });
    }
    return record;
}

export function nextStatus(status) {
    const i = STATUSES.indexOf(status);
    return i >= 0 && i < STATUSES.length - 1 ? STATUSES[i + 1] : status;
}

export function summarize(records) {
    const orders = records.length;
    const revenue = records.reduce((s, r) => s + r.total, 0);
    const itemsSold = records.reduce((s, r) => s + r.totalItems, 0);

    const byType = { Walkup: { orders: 0, revenue: 0 }, Catering: { orders: 0, revenue: 0 } };
    for (const r of records) {
        byType[r.type].orders += 1;
        byType[r.type].revenue += r.total;
    }

    const itemQty = new Map();
    const categoryRevenue = Object.fromEntries(CATEGORIES.map(c => [c.id, 0]));
    for (const r of records) {
        for (const l of r.lines) {
            itemQty.set(l.itemId, (itemQty.get(l.itemId) ?? 0) + l.quantity);
            categoryRevenue[l.category] += l.lineTotal;
        }
    }
    const topItems = [...itemQty.entries()]
        .map(([itemId, quantity]) => ({ itemId, name: MENU.find(m => m.id === itemId)?.name ?? itemId, quantity }))
        .sort((a, b) => b.quantity - a.quantity || a.name.localeCompare(b.name))
        .slice(0, 5);

    return {
        orders,
        revenue,
        itemsSold,
        averageTicket: orders ? Math.round(revenue / orders) : 0,
        salesTaxCollected: records.reduce((s, r) => s + (r.salesTax ?? 0), 0),
        deliveryWaived: records.filter(r => r.deliveryWaivedReason).length,
        byType,
        topItems,
        categoryRevenue
    };
}

export function toCsv(records) {
    const header = ['Order', 'Placed', 'Type', 'Customer', 'Items', 'Subtotal', 'Tax', 'Tip', 'Delivery', 'Total', 'Status'];
    const money = c => (c == null ? '' : (c / 100).toFixed(2));
    const esc = v => /[",\n]/.test(String(v)) ? `"${String(v).replace(/"/g, '""')}"` : String(v);
    const rows = records.map(r => [r.number, r.placedAt, r.type, r.customer, r.totalItems, money(r.subtotal), money(r.salesTax), money(r.tip), money(r.deliveryFee), money(r.total), r.status]);
    return [header, ...rows].map(row => row.map(esc).join(',')).join('\n');
}

// ---- persistence (guarded: storage can be unavailable in private windows) ----

export function loadOrders() {
    try {
        const raw = localStorage.getItem(STORAGE_KEY);
        return raw ? JSON.parse(raw) : [];
    } catch {
        return memoryOrders;
    }
}

let memoryOrders = [];

export function saveOrders(records) {
    memoryOrders = records;
    try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(records));
    } catch { /* keep the in-memory copy */ }
}

export function nextOrderNumber(records) {
    return records.reduce((max, r) => Math.max(max, r.number), 1000) + 1;
}
