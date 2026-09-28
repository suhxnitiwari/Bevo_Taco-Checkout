// Domain model for Bevo's Tacos, ported from the C# models in src/BevosTacos/Models.
// Same design: an abstract Order runs the shared steps (template method) and each
// subclass adds its own charges. All money is integer cents.

import { findItem, findAddOn } from './menu.js';

export const SALES_TAX_RATE_BPS = 825;          // 8.25% in basis points
export const FREE_DELIVERY_THRESHOLD = 100000;  // $1,000.00
export const MAX_DELIVERY_FEE = 25000;          // $250.00
export const CATERING_LEAD_DAYS = 2;            // catering needs 2 days' notice
export const TACOS_PER_GUEST = 3;               // used to suggest catering quantities
export const TIP_OPTIONS = [0, 15, 18, 20];     // percent of subtotal

export class EmptyOrderException extends Error {
    constructor() {
        super('Order must contain at least one item.');
        this.name = 'EmptyOrderException';
    }
}

export class ValidationError extends Error {
    constructor(errors) {
        super(Object.values(errors).join(' '));
        this.name = 'ValidationError';
        this.errors = errors;
    }
}

// Rounds half away from zero, like Math.Round(x, MidpointRounding.AwayFromZero) in C#.
export function roundHalfAwayFromZero(value) {
    return Math.sign(value) * Math.round(Math.abs(value));
}

export function formatMoney(cents) {
    const sign = cents < 0 ? '-' : '';
    const abs = Math.abs(cents);
    return `${sign}$${Math.floor(abs / 100).toLocaleString('en-US')}.${String(abs % 100).padStart(2, '0')}`;
}

// One menu item in the cart, with its add-ons and quantity.
export class LineItem {
    constructor(itemId, quantity = 1, addOnIds = []) {
        const item = findItem(itemId);
        if (!item) throw new Error(`Unknown menu item: ${itemId}`);
        for (const id of addOnIds) {
            const addOn = findAddOn(id);
            if (!addOn || !addOn.categories.includes(item.category)) {
                throw new Error(`Add-on ${id} does not apply to ${item.name}`);
            }
        }
        this.itemId = itemId;
        this.quantity = quantity;
        this.addOnIds = [...addOnIds].sort();
    }

    get item() { return findItem(this.itemId); }
    get addOns() { return this.addOnIds.map(findAddOn); }
    get unitPrice() { return this.item.price + this.addOns.reduce((sum, a) => sum + a.price, 0); }
    get lineTotal() { return this.unitPrice * this.quantity; }

    // Two lines with the same item and add-ons merge in the cart
    get key() { return [this.itemId, ...this.addOnIds].join('+'); }
}

export class Order {
    constructor({ lines = [] } = {}) {
        if (new.target === Order) throw new TypeError('Order is abstract');
        this.lines = lines;
        this.subtotal = 0;
        this.total = 0;
        this.totalItems = 0;
    }

    get customerType() { throw new Error('Subclasses must define customerType'); }

    // Subtotals by category, e.g. { tacos: 1100, burgers: 450 }
    get categorySubtotals() {
        const totals = {};
        for (const line of this.lines) {
            totals[line.item.category] = (totals[line.item.category] ?? 0) + line.lineTotal;
        }
        return totals;
    }

    validate() {
        const errors = {};
        this.lines.forEach((line, i) => {
            if (!Number.isInteger(line.quantity) || line.quantity < 0) {
                errors[`line${i}`] = `Quantity for ${line.item.name} cannot be negative.`;
            }
        });
        Object.assign(errors, this.validateFields());
        return errors;
    }

    // Validates, prices the order, then lets the subclass apply its charges
    calcTotals() {
        const errors = this.validate();
        if (Object.keys(errors).length) throw new ValidationError(errors);

        this.totalItems = this.lines.reduce((sum, l) => sum + l.quantity, 0);
        if (this.totalItems === 0) throw new EmptyOrderException();

        this.subtotal = this.lines.reduce((sum, l) => sum + l.lineTotal, 0);
        this.total = this.calcTotal();
        return this;
    }

    validateFields() { return {}; }
    calcTotal() { throw new Error('Subclasses must implement calcTotal'); }
}

export class WalkupOrder extends Order {
    constructor({ customerName = '', tipPercent = 0, ...rest } = {}) {
        super(rest);
        this.customerName = customerName.trim();
        this.tipPercent = tipPercent;
        this.salesTax = 0;
        this.tip = 0;
    }

    get customerType() { return 'Walkup'; }

    validateFields() {
        const errors = {};
        if (!TIP_OPTIONS.includes(this.tipPercent)) errors.tipPercent = 'Choose one of the tip options.';
        if (this.customerName.length > 40) errors.customerName = 'Name must be 40 characters or fewer.';
        return errors;
    }

    calcTotal() {
        this.salesTax = roundHalfAwayFromZero(this.subtotal * SALES_TAX_RATE_BPS / 10000);
        this.tip = roundHalfAwayFromZero(this.subtotal * this.tipPercent / 100);
        return this.subtotal + this.salesTax + this.tip;
    }
}

export class CateringOrder extends Order {
    constructor({ customerCode = '', deliveryFee = 0, preferredCustomer = false, eventDate = '', guestCount = 0, today = new Date(), ...rest } = {}) {
        super(rest);
        this.customerCode = customerCode.trim().toUpperCase();
        this.requestedDeliveryFee = deliveryFee;
        this.deliveryFee = deliveryFee;
        this.preferredCustomer = preferredCustomer;
        this.eventDate = eventDate;
        this.guestCount = guestCount;
        this.today = today;
        this.deliveryWaivedReason = null;
    }

    get customerType() { return 'Catering'; }

    validateFields() {
        const errors = {};
        if (!this.customerCode) errors.customerCode = 'Customer code is required.';
        else if (this.customerCode.length < 2 || this.customerCode.length > 4) errors.customerCode = 'Customer code must be between 2 and 4 characters.';
        else if (!/^[A-Z]+$/.test(this.customerCode)) errors.customerCode = 'Customer code must contain letters only.';

        if (!Number.isInteger(this.requestedDeliveryFee) || this.requestedDeliveryFee < 0 || this.requestedDeliveryFee > MAX_DELIVERY_FEE) {
            errors.deliveryFee = 'Delivery fee must be between $0 and $250.';
        }

        if (!Number.isInteger(this.guestCount) || this.guestCount < 1) errors.guestCount = 'Enter how many guests you are expecting.';

        if (!this.eventDate) errors.eventDate = 'Pick a date for your event.';
        else if (daysBetween(this.today, this.eventDate) < CATERING_LEAD_DAYS) {
            errors.eventDate = `Catering needs at least ${CATERING_LEAD_DAYS} days’ notice.`;
        }
        return errors;
    }

    calcTotal() {
        this.deliveryFee = this.requestedDeliveryFee;
        this.deliveryWaivedReason = null;
        if (this.preferredCustomer) this.deliveryWaivedReason = 'Preferred customer';
        else if (this.subtotal >= FREE_DELIVERY_THRESHOLD) this.deliveryWaivedReason = 'Order of $1,000 or more';
        if (this.deliveryWaivedReason) this.deliveryFee = 0;
        return this.subtotal + this.deliveryFee;
    }
}

// Whole calendar days from `from` to the ISO date string `to` (yyyy-mm-dd)
export function daysBetween(from, to) {
    const [y, m, d] = to.split('-').map(Number);
    const start = Date.UTC(from.getFullYear(), from.getMonth(), from.getDate());
    return Math.round((Date.UTC(y, m - 1, d) - start) / 86400000);
}

// How many tacos to order for a party, rounded up to a full dozen
export function suggestTacos(guestCount) {
    if (!(guestCount > 0)) return 0;
    return Math.ceil(guestCount * TACOS_PER_GUEST / 12) * 12;
}
