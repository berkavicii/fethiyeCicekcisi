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

window.addEventListener('scroll', function () {
    const nav = document.getElementById('mainNav');
    if (nav) nav.classList.toggle('scrolled', window.scrollY > 30);
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

document.addEventListener('DOMContentLoaded', function () { updateCartBadge(); });
