// The food truck's menu. Prices are in cents so totals never pick up floating-point error.
// Base taco and burger prices match the ASP.NET app ($2.75 and $4.50).

export const CATEGORIES = [
    { id: 'tacos', name: 'Tacos', blurb: 'Handmade corn or flour tortillas' },
    { id: 'burgers', name: 'Burgers', blurb: 'Half-pound Texas beef on a toasted bun' },
    { id: 'sides', name: 'Sides', blurb: 'Made to share (or not)' },
    { id: 'drinks', name: 'Drinks', blurb: 'Cold, colder, coldest' }
];

export const MENU = [
    { id: 'taco-brisket', category: 'tacos', name: 'Smoked Brisket', desc: '12-hour post oak brisket, pickled onion, salsa verde', price: 275, emoji: '🌮', tags: ['bestseller'] },
    { id: 'taco-pastor', category: 'tacos', name: 'Al Pastor', desc: 'Achiote pork, grilled pineapple, cilantro, onion', price: 275, emoji: '🌮', tags: [] },
    { id: 'taco-migas', category: 'tacos', name: 'Migas', desc: 'Scrambled eggs, tortilla strips, jack cheese, pico', price: 275, emoji: '🍳', tags: ['vegetarian'] },
    { id: 'taco-veggie', category: 'tacos', name: 'Roasted Veggie', desc: 'Poblano, sweet potato, black beans, cotija', price: 275, emoji: '🥑', tags: ['vegetarian'] },
    { id: 'burger-classic', category: 'burgers', name: 'Classic Longhorn', desc: 'American cheese, pickles, onion, house sauce', price: 450, emoji: '🍔', tags: ['bestseller'] },
    { id: 'burger-hatch', category: 'burgers', name: 'Hatch Green Chile', desc: 'Roasted Hatch chiles, pepper jack, lime crema', price: 450, emoji: '🌶️', tags: ['spicy'] },
    { id: 'burger-mushroom', category: 'burgers', name: 'Portobello', desc: 'Grilled portobello cap, swiss, caramelized onion', price: 450, emoji: '🍄', tags: ['vegetarian'] },
    { id: 'side-queso', category: 'sides', name: 'Chips & Queso', desc: 'Fresh chips with Bevo’s white queso', price: 325, emoji: '🧀', tags: [] },
    { id: 'side-elote', category: 'sides', name: 'Street Elote', desc: 'Charred corn, mayo, cotija, chile-lime', price: 350, emoji: '🌽', tags: [] },
    { id: 'side-guac', category: 'sides', name: 'Chips & Guac', desc: 'Smashed to order', price: 400, emoji: '🥑', tags: ['vegetarian'] },
    { id: 'drink-horchata', category: 'drinks', name: 'Horchata', desc: 'Cinnamon rice milk over ice', price: 250, emoji: '🥛', tags: [] },
    { id: 'drink-topo', category: 'drinks', name: 'Topo Chico', desc: 'The official water of Austin', price: 200, emoji: '🫧', tags: [] },
    { id: 'drink-tea', category: 'drinks', name: 'Sweet Tea', desc: 'Brewed every morning', price: 200, emoji: '🧋', tags: [] }
];

// Add-ons a customer can put on an item. Each lists the categories it applies to.
export const ADD_ONS = [
    { id: 'guac', name: 'Add guac', price: 75, categories: ['tacos', 'burgers'] },
    { id: 'queso', name: 'Add queso', price: 50, categories: ['tacos', 'burgers'] },
    { id: 'double', name: 'Double patty', price: 200, categories: ['burgers'] },
    { id: 'flour', name: 'Flour tortilla', price: 0, categories: ['tacos'] },
    { id: 'large', name: 'Large size', price: 100, categories: ['drinks'] }
];

const menuById = new Map(MENU.map(item => [item.id, item]));
const addOnById = new Map(ADD_ONS.map(addOn => [addOn.id, addOn]));

export function findItem(id) {
    return menuById.get(id);
}

export function findAddOn(id) {
    return addOnById.get(id);
}

export function addOnsFor(item) {
    return ADD_ONS.filter(addOn => addOn.categories.includes(item.category));
}
