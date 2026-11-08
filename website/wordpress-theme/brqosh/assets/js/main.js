/**
 * Brqosh ERP site behaviour. Progressive enhancement only: every feature here
 * has a working no-JS fallback (links, native <details>, regular form POST).
 */
(function () {
	'use strict';

	var doc = document;

	// Sticky header shadow.
	var header = doc.querySelector('[data-header]');
	if (header) {
		var onScroll = function () { header.classList.toggle('is-scrolled', window.scrollY > 8); };
		onScroll();
		window.addEventListener('scroll', onScroll, { passive: true });
	}

	// Mobile navigation.
	var toggle = doc.querySelector('[data-nav-toggle]');
	var nav = doc.querySelector('[data-nav]');
	if (toggle && nav) {
		var setOpen = function (open) {
			toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
			nav.classList.toggle('is-open', open);
		};
		toggle.addEventListener('click', function () {
			setOpen(toggle.getAttribute('aria-expanded') !== 'true');
		});
		doc.addEventListener('keydown', function (e) {
			if (e.key === 'Escape' && nav.classList.contains('is-open')) { setOpen(false); toggle.focus(); }
		});
		nav.addEventListener('click', function (e) { if (e.target.closest('a')) { setOpen(false); } });
		window.matchMedia('(min-width: 1081px)').addEventListener('change', function (m) { if (m.matches) { setOpen(false); } });
	}

	// Reveal on scroll.
	if ('IntersectionObserver' in window && !window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
		doc.documentElement.classList.add('js-reveal');
		var io = new IntersectionObserver(function (entries) {
			entries.forEach(function (entry) {
				if (entry.isIntersecting) { entry.target.classList.add('is-visible'); io.unobserve(entry.target); }
			});
		}, { rootMargin: '0px 0px -8% 0px' });
		doc.querySelectorAll('.reveal').forEach(function (el, i) {
			el.style.transitionDelay = Math.min(i % 4, 3) * 60 + 'ms';
			io.observe(el);
		});
	}

	// Copy buttons (SHA-256).
	doc.querySelectorAll('[data-copy]').forEach(function (btn) {
		btn.addEventListener('click', function () {
			var src = btn.parentElement.querySelector('[data-copy-source]');
			if (!src || !navigator.clipboard) { return; }
			navigator.clipboard.writeText(src.textContent.trim()).then(function () {
				var label = btn.textContent;
				btn.textContent = 'نُسخ ✓';
				setTimeout(function () { btn.textContent = label; }, 1800);
			});
		});
	});

	// Input helpers for licence fields.
	var serial = doc.getElementById('f-serial');
	if (serial) {
		serial.addEventListener('input', function () {
			var d = serial.value.replace(/[٠-٩]/g, function (c) { return String(c.charCodeAt(0) - 1632); }).replace(/\D/g, '').slice(0, 25);
			serial.value = d.replace(/(\d{5})(?=\d)/g, '$1-');
		});
	}
	var code = doc.getElementById('f-request_code');
	if (code) {
		code.addEventListener('input', function () { code.value = code.value.toUpperCase().replace(/\s+/g, ''); });
	}

	// Lead forms: submit with fetch, show field errors inline.
	doc.querySelectorAll('[data-lead-form]').forEach(function (form) {
		var status = form.querySelector('.form-status');
		var submit = form.querySelector('[data-submit]');

		var clearErrors = function () {
			form.querySelectorAll('.field.has-error').forEach(function (f) { f.classList.remove('has-error'); });
			form.querySelectorAll('.field-error').forEach(function (p) { p.textContent = ''; });
			form.querySelectorAll('[aria-invalid]').forEach(function (el) { el.removeAttribute('aria-invalid'); });
		};
		var showStatus = function (ok, text) {
			status.hidden = false;
			status.className = 'form-status ' + (ok ? 'is-ok' : 'is-err');
			status.textContent = text;
		};
		var showErrors = function (errors) {
			var first = null;
			Object.keys(errors).forEach(function (name) {
				var slot = doc.getElementById('err-' + name);
				var input = form.querySelector('[name="' + name + '"]');
				if (slot) { slot.textContent = errors[name]; }
				if (input) {
					input.setAttribute('aria-invalid', 'true');
					var field = input.closest('.field');
					if (field) { field.classList.add('has-error'); }
					if (!first) { first = input; }
				}
			});
			if (first) { first.focus(); }
		};

		form.addEventListener('submit', function (e) {
			if (!window.fetch || !window.FormData) { return; } // Native POST fallback.
			e.preventDefault();
			clearErrors();

			// Client-side required check mirrors the server; the server stays authoritative.
			var missing = {};
			form.querySelectorAll('[required]').forEach(function (el) {
				var empty = el.type === 'checkbox' ? !el.checked : !el.value.trim();
				if (empty) { missing[el.name] = el.type === 'checkbox' ? 'الموافقة مطلوبة.' : 'هذا الحقل مطلوب.'; }
			});
			if (Object.keys(missing).length) {
				showStatus(false, 'أكمل الحقول المطلوبة.');
				showErrors(missing);
				return;
			}

			submit.disabled = true;
			// form.action is shadowed by the hidden input named "action"; read the attribute.
			fetch(form.getAttribute('action'), {
				method: 'POST',
				body: new FormData(form),
				headers: { 'Accept': 'application/json' },
				credentials: 'same-origin'
			}).then(function (res) {
				return res.json().catch(function () { return { ok: false, message: 'تعذّر الإرسال. حاول مرة أخرى أو تواصل معنا عبر واتساب.' }; });
			}).then(function (data) {
				showStatus(!!data.ok, data.message || '');
				if (data.ok) {
					form.reset();
					form.querySelectorAll('input, select, textarea, button[type="submit"]').forEach(function (el) { if (el.type !== 'hidden') { el.disabled = true; } });
				} else if (data.errors) {
					showErrors(data.errors);
				}
				status.scrollIntoView({ behavior: 'smooth', block: 'center' });
			}).catch(function () {
				showStatus(false, 'تعذّر الاتصال. تحقق من الإنترنت أو تواصل معنا عبر واتساب.');
			}).finally(function () {
				if (!form.querySelector('.form-status.is-ok')) { submit.disabled = false; }
			});
		});
	});
})();
