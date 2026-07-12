// KedWear — site.js

async function updateCartBadge() {
    try {
        const res = await fetch('/sepet/ozet');
        if (!res.ok) return;
        const data = await res.json();
        const count = data.count || 0;
        document.querySelectorAll('#cartBadgeMobile, #cartBadgeDesktop').forEach(el => {
            el.textContent = count;
            el.style.display = count > 0 ? 'flex' : 'none';
        });
    } catch (e) {}
}

function showToast(message, type = 'success') {
    const container = document.getElementById('toastContainer');
    if (!container) return;
    const id = 'toast-' + Date.now();
    const bgClass = type === 'success' ? 'bg-dark' : 'bg-danger';
    container.insertAdjacentHTML('beforeend', `<div id="${id}" class="toast align-items-center text-white ${bgClass} border-0" role="alert"><div class="d-flex"><div class="toast-body">${message}</div><button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button></div></div>`);
    const toastEl = document.getElementById(id);
    const toast = new bootstrap.Toast(toastEl, { delay: 3000 });
    toast.show();
    toastEl.addEventListener('hidden.bs.toast', () => toastEl.remove());
}

document.addEventListener('click', async function (e) {
    const btn = e.target.closest('[data-add-cart]');
    if (!btn) return;
    e.preventDefault();
    const productId = btn.dataset.productId;
    const variantId = btn.dataset.variantId || null;
    const quantity = parseInt(btn.dataset.quantity || '1');
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    const origText = btn.textContent;
    try {
        btn.disabled = true;
        btn.textContent = 'Ekleniyor...';
        const res = await fetch('/sepet/ekle', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token || '' },
            body: JSON.stringify({ productId: parseInt(productId), variantId: variantId ? parseInt(variantId) : null, quantity })
        });
        const data = await res.json();
        if (data.success) { showToast(data.message || 'Ürün sepete eklendi.', 'success'); updateCartBadge(); }
        else showToast(data.message || 'Bir hata oluştu.', 'error');
    } catch (err) { showToast('Bağlantı hatası.', 'error'); }
    finally { btn.disabled = false; btn.textContent = origText; }
});

document.addEventListener('click', function (e) {
    const thumb = e.target.closest('.product-thumb');
    if (!thumb) return;
    const mainImg = document.getElementById('mainProductImage');
    if (mainImg) mainImg.src = thumb.src;
    document.querySelectorAll('.product-thumb').forEach(t => t.classList.remove('active'));
    thumb.classList.add('active');
});

document.addEventListener('click', function (e) {
    const btn = e.target.closest('.variant-btn[data-variant-id]');
    if (!btn) return;
    const group = btn.dataset.variantGroup;
    document.querySelectorAll(`.variant-btn[data-variant-group="${group}"]`).forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
    const input = document.getElementById('selectedVariantId');
    if (input) input.value = btn.dataset.variantId;

    // Size selection carries stock — reflect the selected size's remaining quantity,
    // not the product's total across all sizes (a low single size shouldn't hide
    // behind a healthy total, and vice versa).
    if (group === 'size' && btn.dataset.stock !== undefined) {
        const stock = parseInt(btn.dataset.stock, 10);
        const warning = document.getElementById('stockWarning');
        const warningText = document.getElementById('stockWarningText');
        if (warning && warningText) {
            if (stock > 0 && stock <= 5) {
                warningText.textContent = `Son ${stock} adet!`;
                warning.style.display = '';
            } else {
                warning.style.display = 'none';
            }
        }
    }
});

document.addEventListener('click', async function (e) {
    const btn = e.target.closest('.qty-btn');
    if (!btn) return;
    const itemId = btn.dataset.itemId;
    const direction = btn.dataset.direction;
    const valueEl = document.querySelector(`.qty-value[data-item-id="${itemId}"]`);
    if (!valueEl) return;
    let qty = parseInt(valueEl.textContent);
    qty = direction === 'up' ? qty + 1 : Math.max(0, qty - 1);
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    try {
        const res = await fetch(`/sepet/guncelle/${itemId}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token || '' },
            body: JSON.stringify({ quantity: qty })
        });
        const data = await res.json();
        if (data.success) {
            if (qty === 0) { location.reload(); }
            else {
                valueEl.textContent = qty;
                const s = document.getElementById('cartSubtotal');
                const t = document.getElementById('cartTotal');
                const sh = document.getElementById('cartShipping');
                if (s) s.textContent = data.subTotal + ' ₺';
                if (t) t.textContent = data.total + ' ₺';
                if (sh) sh.textContent = parseFloat(data.shipping) === 0 ? 'Ücretsiz' : data.shipping + ' ₺';
                updateCartBadge();
            }
        }
    } catch (err) {}
});

// Scroll reveal (fade-in + slide-up) via IntersectionObserver
function initScrollReveal() {
    const items = document.querySelectorAll('.reveal');
    if (!items.length) return;
    if (!('IntersectionObserver' in window)) {
        items.forEach(el => el.classList.add('revealed'));
        return;
    }
    const io = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('revealed');
                io.unobserve(entry.target);
            }
        });
    }, { threshold: 0.15, rootMargin: '0px 0px -60px 0px' });
    items.forEach(el => io.observe(el));
}

// Animated stat counters, triggered once when scrolled into view
function initCounters() {
    const counters = document.querySelectorAll('[data-counter]');
    if (!counters.length || !('IntersectionObserver' in window)) return;
    const animate = (el) => {
        const target = parseFloat(el.dataset.counter);
        const suffix = el.dataset.counterSuffix || '';
        const duration = 1400;
        const start = performance.now();
        const step = (now) => {
            const progress = Math.min((now - start) / duration, 1);
            const eased = 1 - Math.pow(1 - progress, 3);
            const value = Math.round(target * eased);
            el.textContent = value.toLocaleString('tr-TR') + suffix;
            if (progress < 1) requestAnimationFrame(step);
        };
        requestAnimationFrame(step);
    };
    const io = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                animate(entry.target);
                io.unobserve(entry.target);
            }
        });
    }, { threshold: 0.4 });
    counters.forEach(el => io.observe(el));
}

// Pause the hero crossfade animation whenever it's off-screen or the tab is
// hidden — it's a full-viewport animated background, so left running
// unconditionally it keeps compositing/painting for no visible benefit.
function initHeroAnimationPause() {
    const hero = document.querySelector('.hero-section');
    if (!hero) return;
    const layers = hero.querySelectorAll('.hero-bg-layer');
    if (!layers.length) return;
    const setState = (running) => {
        layers.forEach(l => { l.style.animationPlayState = running ? 'running' : 'paused'; });
    };
    if ('IntersectionObserver' in window) {
        const io = new IntersectionObserver((entries) => {
            entries.forEach(entry => setState(entry.isIntersecting && !document.hidden));
        }, { threshold: 0 });
        io.observe(hero);
    }
    document.addEventListener('visibilitychange', () => {
        if (document.hidden) setState(false);
        else setState(hero.getBoundingClientRect().top < window.innerHeight && hero.getBoundingClientRect().bottom > 0);
    });
}

// Single rAF-throttled scroll handler driving both the navbar background
// swap and the hero parallax — avoids two independent unthrottled listeners
// fighting over the main thread on every scroll tick.
function initScrollEffects() {
    const nav = document.getElementById('mainNav');
    const hero = document.querySelector('.hero-section');
    const heroBg = hero ? hero.querySelector('.hero-bg') : null;
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    let ticking = false;

    const update = () => {
        if (nav) nav.classList.toggle('scrolled', window.scrollY > 30);
        if (heroBg && !reduceMotion) {
            const rect = hero.getBoundingClientRect();
            if (rect.bottom > 0 && rect.top < window.innerHeight) {
                const offset = Math.max(-1, Math.min(1, rect.top / window.innerHeight));
                heroBg.style.transform = `translate3d(0, ${-(offset * 40)}px, 0)`;
            }
        }
        ticking = false;
    };

    window.addEventListener('scroll', () => {
        if (!ticking) { requestAnimationFrame(update); ticking = true; }
    }, { passive: true });
    update();
}

document.addEventListener('DOMContentLoaded', function () {
    updateCartBadge();
    initScrollReveal();
    initCounters();
    initScrollEffects();
    initHeroAnimationPause();
});
