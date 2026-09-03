// Progressive enhancement only: the server-rendered page already states aggregate
// availability and the primary CTA's disabled state, so a browser with JS disabled still
// shows a fully honest listing (docs/19-CODING-CONVENTIONS.md "the server remains authoritative").
(function () {
    "use strict";

    function initGallery() {
        var hero = document.querySelector("[data-gallery-hero]");
        var thumbs = document.querySelectorAll("[data-gallery-thumb]");
        if (!hero || thumbs.length === 0) {
            return;
        }

        thumbs.forEach(function (thumb) {
            thumb.addEventListener("click", function () {
                var src = thumb.getAttribute("data-full-src");
                var alt = thumb.getAttribute("data-full-alt") || "";
                if (!src) {
                    return;
                }

                hero.setAttribute("src", src);
                hero.setAttribute("alt", alt);
                thumbs.forEach(function (t) {
                    t.classList.remove("is-active");
                    t.removeAttribute("aria-current");
                });
                thumb.classList.add("is-active");
                thumb.setAttribute("aria-current", "true");
            });
        });
    }

    function initVariantPicker() {
        var root = document.querySelector("[data-variant-picker]");
        var statusEl = document.querySelector("[data-variant-status]");
        var dataEl = document.getElementById("listing-variants-data");
        if (!root || !statusEl || !dataEl) {
            return;
        }

        var variants;
        try {
            variants = JSON.parse(dataEl.textContent);
        } catch (error) {
            return;
        }

        var optionGroups = root.querySelectorAll("[data-option-name]");

        function selectedValues() {
            var selection = {};
            optionGroups.forEach(function (group) {
                var name = group.getAttribute("data-option-name");
                var active = group.querySelector(".is-selected");
                if (active) {
                    selection[name] = active.getAttribute("data-value");
                }
            });
            return selection;
        }

        function matches(variant, selection) {
            return Object.keys(selection).every(function (key) {
                var match = variant.options.find(function (o) { return o.option === key; });
                return match && match.value === selection[key];
            });
        }

        // A single option value is selectable when *some* sellable variant carries it. This is
        // deliberately a per-value test and not a per-combination one: disabling a value
        // because it clashes with the current choice in another group traps the buyer — from a
        // valid Black/M they could never reach a valid White/L, because every White chip would
        // disable itself against the selected size M (faed-commerce-ux "let the buyer move
        // between valid combinations"). Impossible partial combinations are surfaced by the
        // availability line in updateStatus() instead. Mirrors
        // PublicListingDetailView.SellableOptionValueIds, which sets the same state server-side.
        function valueIsSellable(optionName, value) {
            return variants.some(function (variant) {
                return variant.sellable && variant.options.some(function (o) {
                    return o.option === optionName && o.value === value;
                });
            });
        }

        function updateStatus() {
            var selection = selectedValues();
            var complete = Object.keys(selection).length === optionGroups.length;
            statusEl.classList.remove("faed-availability--in-stock", "faed-availability--low", "faed-availability--out");

            if (!complete) {
                statusEl.textContent = optionGroups.length === 0
                    ? ""
                    : "Select " + optionGroups.length + " option" + (optionGroups.length === 1 ? "" : "s") + " to see availability.";
                return;
            }

            var variant = variants.find(function (v) { return matches(v, selection); });
            if (!variant) {
                statusEl.textContent = "That combination is not available.";
                statusEl.classList.add("faed-availability--out");
                return;
            }

            if (!variant.sellable) {
                statusEl.textContent = "Sold out in this combination.";
                statusEl.classList.add("faed-availability--out");
            } else if (variant.quantity <= 3) {
                statusEl.textContent = "Only " + variant.quantity + " left in this combination.";
                statusEl.classList.add("faed-availability--low");
            } else {
                statusEl.textContent = "In stock.";
                statusEl.classList.add("faed-availability--in-stock");
            }
        }

        function refreshDisabledStates() {
            optionGroups.forEach(function (group) {
                var name = group.getAttribute("data-option-name");
                var chips = group.querySelectorAll("[data-value]");

                chips.forEach(function (chip) {
                    var possible = valueIsSellable(name, chip.getAttribute("data-value"));
                    chip.classList.toggle("is-unavailable", !possible);
                    chip.setAttribute("aria-disabled", String(!possible));
                });
            });
        }

        root.addEventListener("click", function (event) {
            var chip = event.target.closest("[data-value]");
            if (!chip || chip.classList.contains("is-unavailable")) {
                return;
            }

            var group = chip.closest("[data-option-name]");
            group.querySelectorAll("[data-value]").forEach(function (c) {
                c.classList.remove("is-selected");
                c.setAttribute("aria-pressed", "false");
            });
            chip.classList.add("is-selected");
            chip.setAttribute("aria-pressed", "true");

            // Disabled state is per-value and selection-independent, so it never needs
            // recomputing after a pick — only the availability line changes.
            updateStatus();
        });

        refreshDisabledStates();
        updateStatus();
    }

    document.addEventListener("DOMContentLoaded", function () {
        initGallery();
        initVariantPicker();
    });
})();
