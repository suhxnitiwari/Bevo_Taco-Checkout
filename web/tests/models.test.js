import { test, describe } from 'node:test';
import assert from 'node:assert/strict';
import {
    LineItem, Order, WalkupOrder, CateringOrder, EmptyOrderException, ValidationError,
    formatMoney, roundHalfAwayFromZero, suggestTacos, daysBetween
} from '../js/models.js';
import { toRecord, summarize, nextStatus, toCsv, nextOrderNumber } from '../js/store.js';

const TODAY = new Date(2026, 8, 28); // Sept 28, 2026
const tacos = n => new LineItem('taco-brisket', n);
const burgers = n => new LineItem('burger-classic', n);
const catering = overrides => new CateringOrder({
    customerCode: 'MISA', deliveryFee: 10000, eventDate: '2026-10-05', guestCount: 20, today: TODAY, ...overrides
});

describe('Order (abstract)', () => {
    test('cannot be created directly', () => {
        assert.throws(() => new Order(), TypeError);
    });

    test('throws EmptyOrderException when nothing is ordered', () => {
        assert.throws(() => new WalkupOrder({ lines: [] }).calcTotals(), EmptyOrderException);
        assert.throws(() => new WalkupOrder({ lines: [tacos(0)] }).calcTotals(), EmptyOrderException);
    });

    test('rejects negative quantities', () => {
        assert.throws(() => new WalkupOrder({ lines: [tacos(-1)] }).calcTotals(), ValidationError);
    });

    test('subtotals by category', () => {
        const order = new WalkupOrder({ lines: [tacos(2), burgers(1), new LineItem('drink-topo', 1)] });
        assert.deepEqual(order.categorySubtotals, { tacos: 550, burgers: 450, drinks: 200 });
    });
});

describe('LineItem', () => {
    test('prices add-ons into the unit price', () => {
        const line = new LineItem('burger-hatch', 2, ['double', 'guac']);
        assert.equal(line.unitPrice, 450 + 200 + 75);
        assert.equal(line.lineTotal, 1450);
    });

    test('rejects add-ons that do not fit the item', () => {
        assert.throws(() => new LineItem('taco-pastor', 1, ['double']));
        assert.throws(() => new LineItem('nope', 1));
    });

    test('same item and add-ons share a key regardless of order', () => {
        assert.equal(new LineItem('taco-pastor', 1, ['queso', 'guac']).key, new LineItem('taco-pastor', 3, ['guac', 'queso']).key);
    });
});

describe('WalkupOrder', () => {
    test('matches the original app: 3 tacos + 2 burgers', () => {
        const order = new WalkupOrder({ lines: [tacos(3), burgers(2)] }).calcTotals();
        assert.equal(order.subtotal, 1725);
        assert.equal(order.salesTax, 142);   // 142.3125 -> 142
        assert.equal(order.total, 1867);
        assert.equal(order.totalItems, 5);
    });

    test('rounds tax half away from zero', () => {
        // $2.00 * 8.25% = 16.5 cents -> 17
        const order = new WalkupOrder({ lines: [new LineItem('drink-topo', 1)] }).calcTotals();
        assert.equal(order.salesTax, 17);
    });

    test('adds a tip on the pre-tax subtotal', () => {
        const order = new WalkupOrder({ lines: [burgers(2)], tipPercent: 18 }).calcTotals();
        assert.equal(order.tip, 162);
        assert.equal(order.total, 900 + 74 + 162);
    });

    test('only accepts the offered tip percentages', () => {
        assert.throws(() => new WalkupOrder({ lines: [tacos(1)], tipPercent: 50 }).calcTotals(), ValidationError);
    });

    test('customer name is optional', () => {
        assert.doesNotThrow(() => new WalkupOrder({ lines: [tacos(1)], customerName: '  ' }).calcTotals());
    });
});

describe('CateringOrder', () => {
    test('charges the delivery fee under $1,000', () => {
        const order = catering({ lines: [tacos(30), burgers(30)] }).calcTotals();
        assert.equal(order.subtotal, 21750);
        assert.equal(order.deliveryFee, 10000);
        assert.equal(order.total, 31750);
        assert.equal(order.deliveryWaivedReason, null);
    });

    test('waives delivery at exactly $1,000', () => {
        const order = catering({ lines: [burgers(200), new LineItem('drink-topo', 50)] }).calcTotals();
        assert.equal(order.subtotal, 100000);
        assert.equal(order.deliveryFee, 0);
        assert.equal(order.deliveryWaivedReason, 'Order of $1,000 or more');
    });

    test('charges delivery just under $1,000', () => {
        // 363 tacos = $998.25
        const order = catering({ lines: [tacos(363)] }).calcTotals();
        assert.equal(order.subtotal, 99825);
        assert.equal(order.deliveryFee, 10000);
    });

    test('waives delivery for preferred customers', () => {
        const order = catering({ lines: [tacos(10)], preferredCustomer: true }).calcTotals();
        assert.equal(order.deliveryFee, 0);
        assert.equal(order.total, 2750);
        assert.equal(order.deliveryWaivedReason, 'Preferred customer');
    });

    for (const [code, ok] of [['AB', true], ['abcd', true], ['A', false], ['ABCDE', false], ['A1', false], ['', false]]) {
        test(`customer code "${code}" is ${ok ? 'valid' : 'invalid'}`, () => {
            const run = () => catering({ customerCode: code, lines: [tacos(1)] }).calcTotals();
            ok ? assert.doesNotThrow(run) : assert.throws(run, ValidationError);
        });
    }

    for (const [fee, ok] of [[0, true], [25000, true], [25001, false], [-1, false]]) {
        test(`delivery fee ${fee} cents is ${ok ? 'valid' : 'invalid'}`, () => {
            const run = () => catering({ deliveryFee: fee, lines: [tacos(1)] }).calcTotals();
            ok ? assert.doesNotThrow(run) : assert.throws(run, ValidationError);
        });
    }

    test('needs two days of notice', () => {
        assert.throws(() => catering({ eventDate: '2026-09-29', lines: [tacos(1)] }).calcTotals(), ValidationError);
        assert.doesNotThrow(() => catering({ eventDate: '2026-09-30', lines: [tacos(1)] }).calcTotals());
    });

    test('needs a guest count', () => {
        assert.throws(() => catering({ guestCount: 0, lines: [tacos(1)] }).calcTotals(), ValidationError);
    });

    test('collects every field error at once', () => {
        try {
            catering({ customerCode: '1', deliveryFee: 99999, guestCount: 0, eventDate: '', lines: [tacos(1)] }).calcTotals();
            assert.fail('expected a ValidationError');
        } catch (e) {
            assert.deepEqual(Object.keys(e.errors).sort(), ['customerCode', 'deliveryFee', 'eventDate', 'guestCount']);
        }
    });
});

describe('helpers', () => {
    test('formatMoney', () => {
        assert.equal(formatMoney(0), '$0.00');
        assert.equal(formatMoney(123456), '$1,234.56');
        assert.equal(formatMoney(-5), '-$0.05');
    });

    test('roundHalfAwayFromZero', () => {
        assert.equal(roundHalfAwayFromZero(2.5), 3);
        assert.equal(roundHalfAwayFromZero(-2.5), -3);
    });

    test('suggestTacos rounds up to a full dozen', () => {
        assert.equal(suggestTacos(10), 36);
        assert.equal(suggestTacos(4), 12);
        assert.equal(suggestTacos(0), 0);
    });

    test('daysBetween counts calendar days', () => {
        assert.equal(daysBetween(TODAY, '2026-09-28'), 0);
        assert.equal(daysBetween(TODAY, '2026-10-01'), 3);
    });
});

describe('order history and analytics', () => {
    const walk = toRecord(new WalkupOrder({ lines: [tacos(3), burgers(2)] }).calcTotals(), { id: 'a', number: 1001 });
    const cater = toRecord(catering({ lines: [tacos(30), burgers(30)] }).calcTotals(), { id: 'b', number: 1002 });

    test('records keep the charges for each order type', () => {
        assert.equal(walk.salesTax, 142);
        assert.equal(walk.deliveryFee, undefined);
        assert.equal(cater.deliveryFee, 10000);
        assert.equal(cater.customer, 'MISA');
        assert.equal(walk.customer, 'Walk-up guest');
    });

    test('summarize totals revenue, average ticket and top items', () => {
        const s = summarize([walk, cater]);
        assert.equal(s.orders, 2);
        assert.equal(s.revenue, 1867 + 31750);
        assert.equal(s.averageTicket, Math.round((1867 + 31750) / 2));
        assert.equal(s.byType.Catering.orders, 1);
        assert.deepEqual(s.topItems.map(t => [t.itemId, t.quantity]), [['taco-brisket', 33], ['burger-classic', 32]]);
    });

    test('statuses move forward and stop at Complete', () => {
        assert.equal(nextStatus('New'), 'Cooking');
        assert.equal(nextStatus('Ready'), 'Complete');
        assert.equal(nextStatus('Complete'), 'Complete');
    });

    test('CSV export escapes commas', () => {
        const csv = toCsv([{ ...walk, customer: 'Doe, Jane' }]);
        assert.match(csv, /"Doe, Jane"/);
        assert.equal(csv.split('\n').length, 2);
    });

    test('order numbers count up from 1001', () => {
        assert.equal(nextOrderNumber([]), 1001);
        assert.equal(nextOrderNumber([walk, cater]), 1003);
    });
});
