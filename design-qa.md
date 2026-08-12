# Design QA Report

## Result

final result: passed

## Visual truth and implementation evidence

- Source visual truth: `C:\workAndroid\3Dcube\design-audit\camera-flow\selected-source.png`
- Camera implementation: `C:\workAndroid\3Dcube\design-audit\camera-flow\camera-final2.png`
- Practice implementation: `C:\workAndroid\3Dcube\design-audit\camera-flow\practice-final.png`
- Full-view camera comparison: `C:\workAndroid\3Dcube\design-audit\camera-flow\comparison-camera-final.png`
- Full-view practice comparison: `C:\workAndroid\3Dcube\design-audit\camera-flow\comparison-practice-final.png`
- Focused controls comparison: `C:\workAndroid\3Dcube\design-audit\camera-flow\comparison-controls-final.png`
- Captured-face state: `C:\workAndroid\3Dcube\design-audit\camera-flow\camera-captured.png`
- Manual correction state: `C:\workAndroid\3Dcube\design-audit\camera-flow\color-edit.png`

## Viewport and captured state

- Target device: Galaxy A16 (`SM_A165N`), portrait, native 1080 x 2340 physical pixels.
- Source: 1254 x 1254 composite image with approximately 610 x 1220 phone panels. The source panels were normalized to the device aspect without stretching individual UI elements.
- Camera evidence state: first face, 0/6, rear camera active. The live image is black because the connected phone's rear camera was physically covered/face-down during remote verification.
- Practice evidence state: solved and idle with the explanation-only hint placeholder visible.

## Surface review

| Surface | Result | Notes |
| --- | --- | --- |
| Typography | Pass | Korean hierarchy, title weight, helper copy, and button labels match the selected friendly dark style and remain readable at device density. |
| Spacing and layout | Pass | Safe-area header, capture card, 3x3 guide, face progress, sticky actions, notation rows, hint card, and bottom action bar fit 1080 x 2340 without clipping or overlap. |
| Colors and tokens | Pass | Dark surfaces, blue primary actions, muted instructional text, selected-state blue, and destructive red reset are consistent across both flows. |
| Image quality and assets | Pass | Action controls use transparent official Material Design icon assets at device-ready resolution; no emoji, placeholder art, stretched sprites, or raster fringe. |
| Copy and content | Pass | Six-face capture order and orientation prompts are explicit. Hint copy describes the move and leaves execution to the user. |
| Icons | Pass | Shuffle, hint, undo, and reset icons use the expected semantics; reset is visually distinguished as destructive. |
| States and interactions | Pass | Permission, live preview, capture/advance, six-face progress, manual correction, accept-to-practice, shuffle, hint, undo, reset, notation modifiers, and back navigation are wired. |
| Accessibility | Pass | Controls have large touch targets, text/icon redundancy, high contrast, and status text that does not rely on color alone. |
| Viewport behavior | Pass | The Galaxy A16 portrait viewport and safe area were checked on hardware; camera and practice screens do not crop or collide. |

## Interaction and runtime verification

- Android camera permission prompt appeared and `android.permission.CAMERA` is granted.
- The rear `WebCamTexture` opened and supplied frames; the 3x3 guide remains transparent over the feed.
- Capture advanced progress from 0/6 to 1/6 and moved to the next instructed face.
- Manual color correction opened with center stickers locked, six swatches, validation, reset, and apply actions.
- Valid scanned/manual state routes into 3x3 Practice and preserves the entered cube state.
- Shuffle changed the cube. Hint showed notation plus Korean guidance without moving the cube; a pixel comparison of the cube region before and after hint reported zero changed pixels.
- Device log scan reported no Unity exception, fatal error, null reference, or Android runtime crash.
- Automated verification: PlayMode 92/92 passed; EditMode 89/89 passed.

## QA history

1. P1: The first capture overlay filled transparent cells and obscured the camera feed. Removed the cell fill.
2. P2: Practice action icons did not match their semantics closely enough. Replaced them with official shuffle, lightbulb, undo, and restart assets and applied red destructive styling to reset.
3. P1: Unity `Outline` duplicated the transparent cell geometry as white fill on-device. Replaced it with four explicit thin border edges per cell.
4. Re-captured the device states and repeated side-by-side comparisons. No actionable P0, P1, or P2 visual issue remains.

## Residual test gap

- P3/expected: A physical cube could not be held in front of the remotely connected rear camera, so end-to-end recognition accuracy against a real cube was not exercised on hardware. Color reconstruction, noisy sample matching, camera rotation/mirroring, and six-face integration are covered by automated tests; capture progression was verified on-device.
