document.addEventListener('DOMContentLoaded', function () {
    const filterButtons = document.querySelectorAll('.filter-btn');
    const menuCards = document.querySelectorAll('.menu-card-wrapper');
    const menuSearch = document.getElementById('menuSearch');
    const menuNoResults = document.querySelector('.menu-no-results');
    const menuResultCount = document.getElementById('menuResultCount');
    let selectedFilter = document.querySelector('.filter-btn.active')?.dataset.filter ?? 'all';

    const normalizeSearchText = (value) => value
        .toLocaleLowerCase('vi')
        .normalize('NFD')
        .replace(/[\u0300-\u036f]/g, '')
        .replace(/đ/g, 'd')
        .trim();

    const updateMenu = () => {
        const searchTerm = normalizeSearchText(menuSearch?.value ?? '');
        let visibleCount = 0;

        menuCards.forEach((card) => {
            const matchesFilter = selectedFilter === 'all'
                || (selectedFilter === 'featured'
                    ? card.dataset.featured === 'featured'
                    : card.dataset.category === selectedFilter);
            const searchableText = normalizeSearchText(card.dataset.search ?? '');
            const matchesSearch = !searchTerm || searchableText.includes(searchTerm);
            const isVisible = matchesFilter && matchesSearch;

            card.hidden = !isVisible;
            visibleCount += isVisible ? 1 : 0;
        });

        if (menuNoResults) {
            menuNoResults.hidden = visibleCount > 0;
        }

        if (menuResultCount) {
            menuResultCount.textContent = menuCards.length > 0
                ? `Hiển thị ${visibleCount}/${menuCards.length} món`
                : '';
        }
    };

    filterButtons.forEach((button) => {
        button.addEventListener('click', () => {
            selectedFilter = button.dataset.filter ?? 'all';

            filterButtons.forEach((filterButton) => {
                const isActive = filterButton === button;
                filterButton.classList.toggle('active', isActive);
                filterButton.setAttribute('aria-pressed', String(isActive));
            });

            updateMenu();
        });
    });

    menuSearch?.addEventListener('input', updateMenu);
    updateMenu();

    document.querySelectorAll('.favorite-toggle-form').forEach((form) => {
        form.addEventListener('submit', async (event) => {
            event.preventDefault();

            const button = form.querySelector('button[type="submit"]');
            if (!button || button.disabled) {
                return;
            }

            button.disabled = true;
            try {
                const response = await fetch(form.action, {
                    method: 'POST',
                    body: new FormData(form),
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
                const result = await response.json();

                if (result.requiresLogin && result.loginUrl) {
                    window.location.assign(result.loginUrl);
                    return;
                }

                if (!response.ok) {
                    throw new Error(result.message || 'Không thể cập nhật món yêu thích.');
                }

                if (typeof result.isFavorite === 'boolean') {
                    button.classList.toggle('is-favorite', result.isFavorite);
                    button.setAttribute('aria-pressed', String(result.isFavorite));
                    button.setAttribute(
                        'aria-label',
                        result.isFavorite ? 'Bỏ khỏi yêu thích' : 'Thêm vào yêu thích'
                    );
                    button.title = result.isFavorite ? 'Bỏ khỏi yêu thích' : 'Thêm vào yêu thích';
                    const icon = button.querySelector('span');
                    if (icon) {
                        icon.textContent = result.isFavorite ? '♥' : '♡';
                    }
                }
            } catch (error) {
                window.alert(error instanceof Error ? error.message : 'Không thể cập nhật món yêu thích.');
            } finally {
                button.disabled = false;
            }
        });
    });

    document.querySelectorAll('.product-image-input').forEach((input) => {
        input.addEventListener('change', () => {
            const previewSelector = input.dataset.previewTarget;
            const preview = previewSelector ? document.querySelector(previewSelector) : null;
            const file = input.files?.[0];
            if (!(preview instanceof HTMLImageElement)) {
                return;
            }

            if (!file) {
                return;
            }

            if (preview.dataset.objectUrl) {
                URL.revokeObjectURL(preview.dataset.objectUrl);
            }

            preview.dataset.objectUrl = URL.createObjectURL(file);
            preview.src = preview.dataset.objectUrl;
            preview.hidden = false;
        });
    });

    const contactForm = document.querySelector('.contact-form');
    if (contactForm) {
        contactForm.addEventListener('submit', (event) => {
            const fullName = document.getElementById('FullName');
            const contact = document.getElementById('Contact');
            const message = document.getElementById('Message');

            const isValid = fullName.value.trim() && contact.value.trim() && message.value.trim().length >= 10;

            if (!isValid) {
                event.preventDefault();
                alert('Vui lòng nhập đầy đủ thông tin liên hệ và nội dung tối thiểu 10 ký tự.');
            }
        });
    }
});
