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
