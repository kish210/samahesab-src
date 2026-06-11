---
name: samahesab-design
description: Use this skill to generate well-branded interfaces and assets for SamaHesab (سماع‌حساب), the Iranian enterprise ERP by Sama Rayaneh Kish — for production WPF work or throwaway prototypes/mocks. Contains brand colors, typography, fonts, the dense ERP shell, and operational screen references. The feel is an operational Iranian ERP (Sepidar / SAP Business One style), NOT a SaaS dashboard.
user-invocable: true
---

Read the `readme.md` file within this skill, then explore the other files.

Key facts:
- Brand colors (logo-derived, use ONLY these): primary #4B5F97, secondary #324A7A, gold accent #B18A5A. 80% neutral / 15% brand / 5% status.
- Font: Vazirmatn. Fully RTL. All money figures are tabular + Persian separators, LTR inside columns.
- This is a DENSE operational ERP, not a SaaS dashboard: high data density, full-gridline tables, keyboard-first (F2/F7/F8/F9), 60→220px sidebar, 56px topbar, MDI document tabs, 26px status bar.
- Foundation: `styles.css` + `components.css`. ERP shell: `screens/erp.css` + `screens/erp-shell.js`.
- Operational screen references live in `screens/*.html`; PNGs in `png/`; the full UX spec in `handoff/ux-prompt.md`.
- Source product repo: github.com/kish210/samahesab (WPF/Telerik/.NET).

If creating visual artifacts (mocks, throwaway prototypes, slides), copy assets out and build static HTML using `screens/erp.css` + `erp-shell.js` as the shell. If working on production WPF code, read the rules here and in `handoff/ux-prompt.md` to design accurately for this brand.

If the user invokes this skill without guidance, ask what they want to build, ask a few questions, and act as an expert ERP designer who outputs HTML artifacts or guides production WPF work.
