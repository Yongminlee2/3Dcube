# Design QA — 3D Cube mobile redesign

## Target and normalization

- Visual source of truth: `C:\Users\사용자\.codex\generated_images\019fdc3d-43c5-7b92-a0ec-a0b6f6883723\exec-840bb912-b439-456d-8771-37cee7f52bab.png`
- Release implementation capture: `C:\workAndroid\3Dcube\design-audit\after-pass2\dark\home-release-final.png`
- Final installed-build capture: `C:\workAndroid\3Dcube\design-audit\after-pass2\dark\final-device-state.png` (SHA-256 matches the release comparison capture exactly)
- Physical device: Samsung Galaxy A16 (`SM-A165N`, serial `RF9Y101ZZPB`)
- Device viewport: `1080 × 2340`, density `450 dpi`
- Unity layout: portrait Canvas Scaler with Android safe-area handling
- Compared state: Home, dark theme, Classic skin, 3×3 selected
- Reference size: `853 × 1844`
- Implementation normalization: crop three pixels from the top and bottom of the `1080 × 2340` device capture, then Lanczos-resize the resulting `1080 × 2334` image to `853 × 1844`. No device frame or browser chrome is present.

## Combined visual evidence

- Source + release Home, full view: `C:\workAndroid\3Dcube\design-audit\comparisons\home-release-full-comparison.png`
- Source + release Home, hero focus: `C:\workAndroid\3Dcube\design-audit\comparisons\home-release-hero-comparison.png`
- Source + release Home, selector/actions focus: `C:\workAndroid\3Dcube\design-audit\comparisons\home-release-actions-comparison.png`
- Normalized release Home: `C:\workAndroid\3Dcube\design-audit\comparisons\home-release-final-normalized.png`
- All redesigned dark routes: `C:\workAndroid\3Dcube\design-audit\comparisons\dark-all-routes-release-contact-sheet.png`
- Key light-theme routes: `C:\workAndroid\3Dcube\design-audit\comparisons\light-key-routes-release-contact-sheet.png`
- Skin screen before/after: `C:\workAndroid\3Dcube\design-audit\comparisons\skins-before-after-comparison.png`
- Rotation-exit → Skin re-entry smoke: `C:\workAndroid\3Dcube\design-audit\after-pass2\dark\lifecycle-skin-smoke.png`
- Practice before/after: `C:\workAndroid\3Dcube\design-audit\comparisons\practice-before-after-comparison.png`
- Records before/after: `C:\workAndroid\3Dcube\design-audit\comparisons\records-before-after-comparison.png`
- Library before/after: `C:\workAndroid\3Dcube\design-audit\comparisons\library-before-after-comparison.png`

## States inspected on the device

- Dark: Home, Practice expanded, Practice collapsed, Records, Learn, Lesson, Algorithm Library, Color Input, Settings, and Skins.
- Light: Home, Settings, Records, and Color Input.
- Interaction: home navigation, Practice launch/back, net expand/collapse, Learn/Lesson/Library routing, Settings theme rebuild, Skins routing and selection list, Records routing, and Color Input routing.
- Theme behavior: switching Dark ↔ Light keeps Settings active and updates all shared visual tokens; the release is returned to Dark mode afterward.

## Final findings

No actionable P0, P1, or P2 visual findings remain.

- Typography: Korean labels use a consistent bold/regular hierarchy and remain readable without clipping. Headers, card titles, helper copy, timer text, notation, and compact controls are visually distinct.
- Spacing: all screens respect the Galaxy safe area. Cards, cube previews, lists, fixed actions, and footnotes occupy separate bands with no collisions. Scrollable content has usable row heights and the final skin row is fully visible.
- Color and theme: the dark implementation preserves the source's near-black canvas, cobalt actions, cool raised surfaces, muted secondary copy, and restrained borders. The light palette uses the same hierarchy on a cool off-white canvas without losing control boundaries.
- Imagery: the Home hero and 2×2/3×3/4×4 selector use dedicated transparent bitmap assets with the correct visible sticker counts. The blue CTA and selected segment use clean rounded gradient assets without black fringe or square-corner artifacts.
- Icons: real Tabler-style icon assets are used for navigation and utility actions. No emoji, text-symbol, or placeholder icons remain.
- Copy and hierarchy: the release follows the reference sequence—header, coach hero, size choice, primary Practice action, Learn card, and utility cards—and extends the same design language across every menu requested by the user.
- Interaction: primary actions, segmented choices, cards, swatches, and bottom action bars have practical phone-sized tap targets. Expanded and collapsed Practice states were both visually verified.

## Iteration history

### 1. Incomplete route coverage and cube collisions

- Finding: the earlier implementation applied the new style mainly to Home, Practice, Learn, and Settings. Records, Lesson, Library, Color Input, and Skins still used legacy layouts; the Library cube collided with algorithm cards.
- Fix: introduced the shared Friendly Cube Coach tokens/components across every route, then separated each shared 3D preview from its content region. Library and Skins use bounded preview cards and smaller route-specific cube presentation scales.
- Evidence: the dark all-route contact sheet and Library before/after comparison.

### 2. Home asset fidelity

- Finding: an intermediate Home build had dark contamination around the CTA and selected selector, then square corners from a Unity Mask workaround.
- Fix: generated clean cobalt/electric-blue gradients, applied deterministic alpha-rounded finishing, removed the Mask, and rebuilt the APK.
- Evidence: the final full and action-focused source comparisons. Corners are transparent and visibly rounded on the physical device.

### 3. Records empty-state collapse

- Finding: the empty list card ignored its requested height because the vertical layout group did not control child height.
- Fix: enabled controlled child heights in the shared scroll-list component, preserving each row's `LayoutElement` height.
- Evidence: `records-before-after-comparison.png` and the final dark/light Records captures.

### 4. Practice net hierarchy

- Finding: the fold control appeared detached from the net card and the collapsed state left unnecessary space.
- Fix: integrated the fold/expand affordance into the card header and made the card shrink when collapsed.
- Evidence: `practice-before-after-comparison.png` and `practice-collapsed-release-final2.png`.

### 5. Skin preview and final-row clipping

- Finding: the guide copy overlapped the 3D cube and the Wood preset's lower rounded edge was clipped.
- Fix: moved the guide to the preview footer, reduced the preview cube scale, and tuned row height so all six presets fit with complete rounded edges.
- Evidence: `skins-before-after-comparison.png` and `skins-release-final2.png`.

### 6. Source parity review

- Finding: the combined reference/release comparison showed the production header and hero using slightly more safe-area breathing room, while the concept uses a stronger blue halo and filled play/cube utility art.
- Decision: retain the production safe-area spacing and real icon set. The missing decorative halo/dots and outline-vs-filled icon details are accepted P3 stylistic differences; they do not change hierarchy, comprehension, responsiveness, or the core visual direction.

### 7. Navigation re-entry safety

- Finding: leaving a cube screen during an active turn could stop Unity's rotation coroutine with a stale handle, and Records deletion confirmation could remain armed after navigating away.
- Fix: the router now finishes the shared rotator before hiding `CubeRoot`; Records cancels destructive confirmation on disable. Regression tests cover both leave-and-return paths.
- Result: later Practice/Skin entries cannot inherit a half-turned pivot, and returning to Records always requires two fresh delete taps.

## Release checks

- [x] Final APK built successfully
- [x] Final APK installed on the explicitly selected Galaxy A16
- [x] Dark and Light themes visually verified
- [x] Every application route visually inspected in Dark mode
- [x] Key routes visually inspected in Light mode
- [x] Home compared against the reference in a single combined image
- [x] Post-fix Skin and Practice states recaptured
- [x] Rotation-in-progress exit and aligned Skin re-entry verified on the Galaxy A16
- [x] PlayMode regression suite passed: 87 / 87
- [x] EditMode regression suite passed: 85 / 85

final result: passed
