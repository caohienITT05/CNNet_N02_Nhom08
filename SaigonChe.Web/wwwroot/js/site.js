/* ==========================================================================
   Chè Sài Gòn — interaction layer
   Small, dependency-free behaviours that give the server-rendered Razor
   components the same feel as shadcn/Radix primitives.
   ========================================================================== */
(function () {
    "use strict";

    var root = document.documentElement;
    var reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)");

    /* ----------------------------------------------------------------------
       Theme
       ---------------------------------------------------------------------- */

    var themeMeta = document.querySelector('meta[name="theme-color"]');

    function applyTheme(theme) {
        root.setAttribute("data-theme", theme);
        try {
            localStorage.setItem("csg-theme", theme);
        } catch (error) {
            /* storage can be unavailable in private mode */
        }

        document.querySelectorAll("[data-theme-toggle]").forEach(function (button) {
            var isDark = theme === "dark";
            button.setAttribute("aria-pressed", String(isDark));
            button.setAttribute("title", isDark ? "Chuyển sang giao diện sáng" : "Chuyển sang giao diện tối");
            button.setAttribute("aria-label", isDark ? "Chuyển sang giao diện sáng" : "Chuyển sang giao diện tối");
        });

        if (themeMeta) {
            themeMeta.setAttribute("content", theme === "dark" ? "#0b0b0d" : "#fbf9f6");
        }
    }

    function currentTheme() {
        return root.getAttribute("data-theme") === "dark" ? "dark" : "light";
    }

    document.querySelectorAll("[data-theme-toggle]").forEach(function (button) {
        button.addEventListener("click", function () {
            applyTheme(currentTheme() === "dark" ? "light" : "dark");
        });
    });

    applyTheme(currentTheme());

    /* ----------------------------------------------------------------------
       Header elevation
       ---------------------------------------------------------------------- */

    var header = document.querySelector(".site-header");

    if (header) {
        var updateHeader = function () {
            header.classList.toggle("is-scrolled", window.scrollY > 8);
        };
        updateHeader();
        window.addEventListener("scroll", updateHeader, { passive: true });
    }

    /* ----------------------------------------------------------------------
       Mobile navigation sheet
       ---------------------------------------------------------------------- */

    var sheet = document.getElementById("mobileNav");
    var sheetToggles = document.querySelectorAll("[data-sheet-open]");
    var lastFocused = null;

    function setSheetExpanded(expanded) {
        sheetToggles.forEach(function (toggle) {
            toggle.setAttribute("aria-expanded", String(expanded));
        });
    }

    function openSheet() {
        if (!sheet) return;
        lastFocused = document.activeElement;
        sheet.classList.add("is-open");
        document.body.classList.add("nav-open");
        setSheetExpanded(true);
        var focusTarget = sheet.querySelector("[data-sheet-close]");
        if (focusTarget) {
            window.setTimeout(function () { focusTarget.focus(); }, 30);
        }
    }

    function closeSheet() {
        if (!sheet) return;
        sheet.classList.remove("is-open");
        document.body.classList.remove("nav-open");
        setSheetExpanded(false);
        if (lastFocused && typeof lastFocused.focus === "function") {
            lastFocused.focus();
        }
    }

    sheetToggles.forEach(function (button) {
        button.addEventListener("click", openSheet);
    });

    document.querySelectorAll("[data-sheet-close]").forEach(function (button) {
        button.addEventListener("click", closeSheet);
    });

    if (sheet) {
        sheet.querySelectorAll("a").forEach(function (link) {
            link.addEventListener("click", closeSheet);
        });

        sheet.addEventListener("keydown", function (event) {
            if (event.key === "Escape") {
                closeSheet();
                return;
            }

            if (event.key !== "Tab") return;

            var focusable = sheet.querySelectorAll(
                'a[href], button:not([disabled]), input:not([disabled]), [tabindex]:not([tabindex="-1"])'
            );
            if (!focusable.length) return;

            var first = focusable[0];
            var last = focusable[focusable.length - 1];

            if (event.shiftKey && document.activeElement === first) {
                event.preventDefault();
                last.focus();
            } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first.focus();
            }
        });
    }

    /* ----------------------------------------------------------------------
       User dropdown (Radix-style menu behaviour)
       ---------------------------------------------------------------------- */

    var menus = Array.prototype.slice.call(document.querySelectorAll("[data-user-menu]"));

    function closeMenus(except) {
        menus.forEach(function (menu) {
            if (menu === except) return;
            var toggle = menu.querySelector("[data-user-toggle]");
            var panel = menu.querySelector("[data-user-panel]");
            if (!toggle || !panel) return;
            toggle.setAttribute("aria-expanded", "false");
            panel.hidden = true;
        });
    }

    menus.forEach(function (menu) {
        var toggle = menu.querySelector("[data-user-toggle]");
        var panel = menu.querySelector("[data-user-panel]");
        if (!toggle || !panel) return;

        toggle.addEventListener("click", function (event) {
            event.stopPropagation();
            var isOpen = toggle.getAttribute("aria-expanded") === "true";
            closeMenus(menu);
            toggle.setAttribute("aria-expanded", String(!isOpen));
            panel.hidden = isOpen;
            if (!isOpen) {
                var firstItem = panel.querySelector(".dropdown-item");
                if (firstItem) firstItem.focus();
            }
        });

        menu.addEventListener("keydown", function (event) {
            if (event.key === "Escape" && !panel.hidden) {
                toggle.focus();
                toggle.setAttribute("aria-expanded", "false");
                panel.hidden = true;
            }
        });
    });

    document.addEventListener("click", function (event) {
        if (!event.target.closest("[data-user-menu]")) {
            closeMenus(null);
        }
    });

    /* ----------------------------------------------------------------------
       Admin sidebar (mobile)
       ---------------------------------------------------------------------- */

    var adminSidebar = document.getElementById("adminSidebar");
    var adminBackdrop = document.querySelector("[data-admin-backdrop]");
    var adminToggle = document.querySelector("[data-admin-toggle]");

    function closeAdminNav() {
        if (!adminSidebar) return;
        adminSidebar.classList.remove("is-open");
        if (adminBackdrop) adminBackdrop.classList.remove("is-open");
        if (adminToggle) adminToggle.setAttribute("aria-expanded", "false");
        document.body.classList.remove("nav-open");
    }

    if (adminSidebar && adminToggle) {
        adminToggle.addEventListener("click", function () {
            var isOpen = adminSidebar.classList.toggle("is-open");
            if (adminBackdrop) adminBackdrop.classList.toggle("is-open", isOpen);
            adminToggle.setAttribute("aria-expanded", String(isOpen));
            document.body.classList.toggle("nav-open", isOpen);
        });
    }

    if (adminBackdrop) {
        adminBackdrop.addEventListener("click", closeAdminNav);
    }

    window.addEventListener("keydown", function (event) {
        if (event.key === "Escape") {
            closeMenus(null);
            closeSheet();
            closeAdminNav();
        }
    });

    /* ----------------------------------------------------------------------
       Search trigger + keyboard shortcut
       ---------------------------------------------------------------------- */

    function focusMenuSearch() {
        var search = document.getElementById("menuSearch");
        if (search) {
            var section = document.getElementById("menu");
            if (section) {
                section.scrollIntoView({ behavior: reduceMotion.matches ? "auto" : "smooth", block: "start" });
            }
            window.setTimeout(function () {
                search.focus({ preventScroll: true });
                search.select();
            }, reduceMotion.matches ? 0 : 320);
            return true;
        }
        return false;
    }

    document.querySelectorAll("[data-search-focus]").forEach(function (button) {
        button.addEventListener("click", function () {
            if (!focusMenuSearch() && button.dataset.searchHref) {
                window.location.href = button.dataset.searchHref;
            }
        });
    });

    document.addEventListener("keydown", function (event) {
        var isShortcut = (event.metaKey || event.ctrlKey) && event.key.toLowerCase() === "k";
        if (!isShortcut) return;

        event.preventDefault();
        var trigger = document.querySelector("[data-search-focus]");
        if (trigger) trigger.click();
    });

    /* ----------------------------------------------------------------------
       Menu filtering and search
       ---------------------------------------------------------------------- */

    var filterButtons = document.querySelectorAll(".filter-btn");
    var menuCards = document.querySelectorAll(".menu-card-wrapper");
    var menuSearch = document.getElementById("menuSearch");
    var menuNoResults = document.querySelector(".menu-no-results");
    var menuResultCount = document.getElementById("menuResultCount");
    var selectedFilter = document.querySelector(".filter-btn.active")?.dataset.filter || "all";

    function normalizeSearchText(value) {
        return (value || "")
            .toLocaleLowerCase("vi")
            .normalize("NFD")
            .replace(/[\u0300-\u036f]/g, "")
            .replace(/\u0111/g, "d")
            .trim();
    }

    function updateMenu() {
        var searchTerm = normalizeSearchText(menuSearch ? menuSearch.value : "");
        var visibleCount = 0;

        menuCards.forEach(function (card) {
            var matchesFilter = selectedFilter === "all"
                || (selectedFilter === "featured"
                    ? card.dataset.featured === "featured"
                    : card.dataset.category === selectedFilter);
            var searchableText = normalizeSearchText(card.dataset.search || "");
            var matchesSearch = !searchTerm || searchableText.indexOf(searchTerm) !== -1;
            var isVisible = matchesFilter && matchesSearch;

            card.hidden = !isVisible;
            if (isVisible) visibleCount += 1;
        });

        if (menuNoResults) {
            menuNoResults.hidden = visibleCount > 0 || menuCards.length === 0;
        }

        if (menuResultCount) {
            menuResultCount.textContent = menuCards.length > 0
                ? "Hiển thị " + visibleCount + "/" + menuCards.length + " món"
                : "";
        }
    }

    filterButtons.forEach(function (button) {
        button.addEventListener("click", function () {
            selectedFilter = button.dataset.filter || "all";

            filterButtons.forEach(function (filterButton) {
                var isActive = filterButton === button;
                filterButton.classList.toggle("active", isActive);
                filterButton.setAttribute("aria-pressed", String(isActive));
            });

            updateMenu();
        });
    });

    if (menuSearch) {
        menuSearch.addEventListener("input", updateMenu);
    }

    updateMenu();

    /* ----------------------------------------------------------------------
       Quantity steppers
       ---------------------------------------------------------------------- */

    document.querySelectorAll(".qty-stepper").forEach(function (stepper) {
        var input = stepper.querySelector('input[type="number"]');
        if (!input) return;

        var min = parseInt(input.min, 10) || 1;
        var max = parseInt(input.max, 10) || 99;

        function step(delta) {
            var value = parseInt(input.value, 10) || min;
            value = Math.min(max, Math.max(min, value + delta));
            input.value = String(value);
            input.dispatchEvent(new Event("change", { bubbles: true }));
        }

        var minus = stepper.querySelector("[data-qty-minus]");
        var plus = stepper.querySelector("[data-qty-plus]");

        if (minus) minus.addEventListener("click", function () { step(-1); });
        if (plus) plus.addEventListener("click", function () { step(1); });
    });

    /* ----------------------------------------------------------------------
       Confirmation dialog
       ---------------------------------------------------------------------- */

    var confirmDialog = document.getElementById("confirmDialog");
    var pendingForm = null;

    document.querySelectorAll("form[data-confirm]").forEach(function (form) {
        form.addEventListener("submit", function (event) {
            if (form.dataset.confirmed === "true") return;

            if (!confirmDialog || typeof confirmDialog.showModal !== "function") {
                if (!window.confirm(form.dataset.confirm)) {
                    event.preventDefault();
                }
                return;
            }

            event.preventDefault();
            pendingForm = form;

            var message = confirmDialog.querySelector("[data-confirm-message]");
            var title = confirmDialog.querySelector("[data-confirm-title]");
            if (message) message.textContent = form.dataset.confirm || "Bạn có chắc chắn muốn tiếp tục?";
            if (title) title.textContent = form.dataset.confirmTitle || "Xác nhận thao tác";

            confirmDialog.showModal();
        });
    });

    if (confirmDialog) {
        var cancelButton = confirmDialog.querySelector("[data-confirm-cancel]");
        var acceptButton = confirmDialog.querySelector("[data-confirm-accept]");

        if (cancelButton) {
            cancelButton.addEventListener("click", function () {
                pendingForm = null;
                confirmDialog.close();
            });
        }

        if (acceptButton) {
            acceptButton.addEventListener("click", function () {
                if (!pendingForm) {
                    confirmDialog.close();
                    return;
                }

                var form = pendingForm;
                form.dataset.confirmed = "true";
                pendingForm = null;
                confirmDialog.close();
                form.submit();
            });
        }

        confirmDialog.addEventListener("click", function (event) {
            if (event.target === confirmDialog) {
                pendingForm = null;
                confirmDialog.close();
            }
        });

        confirmDialog.addEventListener("close", function () {
            if (confirmDialog.returnValue !== "confirm") {
                pendingForm = null;
            }
        });
    }

    /* ----------------------------------------------------------------------
       Password visibility
       ---------------------------------------------------------------------- */

    document.querySelectorAll("[data-password-toggle]").forEach(function (button) {
        var targetId = button.getAttribute("data-password-toggle");
        var input = document.getElementById(targetId);
        if (!input) return;

        button.addEventListener("click", function () {
            var isPassword = input.type === "password";
            input.type = isPassword ? "text" : "password";
            button.setAttribute("aria-pressed", String(isPassword));
            button.setAttribute("aria-label", isPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu");

            var eye = button.querySelector(".eye-open");
            var eyeOff = button.querySelector(".eye-off");
            if (eye) eye.hidden = isPassword;
            if (eyeOff) eyeOff.hidden = !isPassword;
        });
    });

    /* ----------------------------------------------------------------------
       Copy coupon code
       ---------------------------------------------------------------------- */

    document.querySelectorAll("[data-copy]").forEach(function (button) {
        button.addEventListener("click", function () {
            var value = button.dataset.copy;
            if (!value) return;

            var done = function () {
                var hint = button.querySelector("[data-copy-label]");
                if (!hint) return;
                var original = hint.textContent;
                hint.textContent = "Đã sao chép";
                window.setTimeout(function () { hint.textContent = original; }, 1800);
            };

            if (navigator.clipboard && navigator.clipboard.writeText) {
                navigator.clipboard.writeText(value).then(done).catch(done);
            } else {
                done();
            }
        });
    });

    /* ----------------------------------------------------------------------
       Reveal on scroll + active section nav
       ---------------------------------------------------------------------- */

    var revealItems = document.querySelectorAll("[data-reveal]");

    if (revealItems.length) {
        if ("IntersectionObserver" in window && !reduceMotion.matches) {
            var revealObserver = new IntersectionObserver(function (entries) {
                entries.forEach(function (entry) {
                    if (!entry.isIntersecting) return;
                    entry.target.classList.add("is-visible");
                    revealObserver.unobserve(entry.target);
                });
            }, { rootMargin: "0px 0px -8% 0px", threshold: 0.08 });

            revealItems.forEach(function (item) {
                revealObserver.observe(item);
            });
        } else {
            revealItems.forEach(function (item) {
                item.classList.add("is-visible");
            });
        }
    }

    var navLinks = document.querySelectorAll("[data-nav-section]");
    var sections = document.querySelectorAll("section[id]");

    if (navLinks.length && sections.length && "IntersectionObserver" in window) {
        var sectionObserver = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (!entry.isIntersecting) return;
                var id = entry.target.id;
                navLinks.forEach(function (link) {
                    link.classList.toggle("active", link.dataset.navSection === id);
                });
            });
        }, { rootMargin: "-45% 0px -50% 0px", threshold: 0 });

        sections.forEach(function (section) {
            sectionObserver.observe(section);
        });
    }
})();
