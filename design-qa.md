# Design QA — 연습 화면 규격·타이포그래피 / 레슨 큐브 위치

## 비교 대상

- 원본 시각 증거
  - `C:/Users/사용자/AppData/Local/Temp/codex-clipboard-3216a409-958a-4712-8efb-c66d091afbc2.jpg`
  - `C:/Users/사용자/AppData/Local/Temp/codex-clipboard-23cc77fc-d828-4980-8271-d312ad42bb84.jpg`
- 구현 캡처
  - `C:/workAndroid/3Dcube/design-audit/layout-fix-2026-08-12/practice-after.png`
  - `C:/workAndroid/3Dcube/design-audit/layout-fix-2026-08-12/lesson-after.png`
- 한 화면 비교
  - `C:/workAndroid/3Dcube/design-audit/layout-fix-2026-08-12/practice-comparison.png`
  - `C:/workAndroid/3Dcube/design-audit/layout-fix-2026-08-12/lesson-comparison.png`

## 환경과 정규화

- 기기: Samsung Galaxy A16 (`SM_A165N`), 세로, 다크 테마
- 구현 뷰포트: 1080×2340 px, Android 밀도 450 dpi
- 원본: 1080×2400 px
- 구현: 1080×2340 px
- 비교 정규화: 원본을 1080×2340으로 리샘플링한 뒤 구현 캡처와 1:1 폭으로 나란히 배치했다. 각 비교 파일은 2184×2340 px이다.
- 연습 화면은 원본의 스크램블·수동 회전 상태와 구현의 완성 상태가 다르므로 큐브 스티커 배치와 각도는 비교 대상에서 제외했다. 사용자가 지적한 카드 규격과 하단 글자 크기는 동일한 다크/3×3 상태에서 비교했다.
- 레슨 화면은 양쪽 모두 1단계·1/4 페이지·설명 모드로 맞췄다.

## 비교 이력

### 1차 확인 — blocked

- [P2] 연습 화면의 전개도 카드만 좌우 여백이 10%였고 나머지 주요 카드와 하단 요소는 약 5%라 수직 그리드가 어긋났다.
- [P2] 하단 4분할 도구모음의 18pt 라벨과 작은 아이콘이 버튼 높이에 비해 작아 보였다.
- [P2] 힌트 카드의 20pt 설명이 카드 크기에 비해 작고 시각적 밀도가 낮았다.
- [P1] 레슨 설명 모드의 큐브 중심이 viewport y=0.75에 있어 상단 안내 문구와 겹쳤다.

### 적용한 수정

- 전개도, 힌트, 노테이션 패드, 하단 바를 모두 화면 좌우 5% 공통 그리드에 맞췄다.
- 하단 라벨을 18pt에서 24pt로 높이고 아이콘 점유 영역을 확대했다.
- 힌트 제목은 30pt, 설명은 최대 23pt로 높였다.
- 레슨 설명 모드의 큐브 lift를 0.25에서 0.19로 낮추고 라우터와 화면 내부 설정이 같은 값을 사용하도록 맞췄다.
- PlayMode 회귀 테스트로 공통 그리드, 최소 글자 크기, 큐브 viewport 중심을 고정했다.

### 2차 확인 — passed

- `practice-comparison.png`에서 전개도 카드가 스크램블·힌트·패드·하단 바와 같은 좌우선에 정렬됐다.
- 하단 4개 라벨과 아이콘이 버튼 면적에 맞는 크기로 커졌고 `되돌리기`, `초기화`도 잘림 없이 표시된다.
- 힌트 설명은 두 줄을 유지하면서 원본보다 읽기 쉬운 크기로 표시된다.
- `lesson-comparison.png`에서 큐브가 안내 문구 아래로 내려왔고 코치 카드 위 여백 안에 들어간다. 문구·큐브·카드 사이 겹침이 없다.

## 필수 품질 표면

- 글꼴/타이포그래피: 기존 앱 글꼴과 굵기를 유지했다. 하단 라벨 24pt, 힌트 제목 30pt, 설명 최대 23pt로 조정했고 줄바꿈·잘림이 없다.
- 간격/레이아웃 리듬: 연습 주요 요소의 좌우선을 5% 공통 그리드로 통일했다. 레슨 큐브는 안내와 코치 카드 사이에 배치됐다.
- 색상/토큰: 기존 다크 팔레트, Accent, TextPrimary/TextSecondary와 위험 색상을 그대로 사용해 다른 화면과 일관된다.
- 이미지/에셋 품질: 기존 실제 PNG 아이콘과 3D 큐브 렌더러를 유지했다. 확대된 하단 아이콘에 흐림이나 잘림이 없다.
- 문구/콘텐츠: 기존 한국어 문구와 기능 의미를 유지했다. 자동 힌트가 아니라 직접 조작한다는 안내도 그대로다.

## 상호작용 및 검증

- 연습 화면 진입, 3×3 패드, 하단 도구모음 표시 확인
- 배우기 → 1단계 진입 및 페이지/하단 동작 영역 표시 확인
- PlayMode 집중 테스트: 13/13 통과
- Android 빌드 성공 및 Galaxy A16 덮어 설치 완료
- 설치 후 Unity/AndroidRuntime 예외, NullReference, FATAL 없음

## 남은 사항

- P3: 연습 캡처의 큐브 각도는 사용자 조작 상태에 따라 달라지며 이번 레이아웃 수정 범위가 아니다.

## 3차 확인 — blocked

- 2026-08-12 Galaxy A16 실기기 캡처에서 연습 화면의 `2회`·`넓은 수` 제거, 한 줄 노테이션 패드, 낮아진 하단 바를 확인했다.
- 한 화면 비교: `C:/workAndroid/3Dcube/design-audit/density-audio-2026-08-12/practice-before-after.png`
- A16 구현 캡처: `C:/workAndroid/3Dcube/design-audit/density-audio-2026-08-12/a16-state.png` (1080×2340, 다크, 3×3 연습)
- 새 레슨 위치 보정(읽기 모드 scale 0.68, lift 0.15)을 적용하고 Galaxy A16에 최신 APK를 설치했다.
- PlayMode 94/94, EditMode 89/89, 레슨 위치 집중 테스트 8/8 통과.
- 차단 사유: scrcpy와 에뮬레이터를 사용하지 않는 조건에서 최신 빌드의 `배우기 → 1단계` 화면을 사용자가 A16에서 다시 열어야 최종 실기기 캡처와 시각 비교를 할 수 있다. 현재 A16은 홈 화면에 있다.

## 4차 확인 — passed

- Galaxy A16(`SM-A165N`, 1080×2340)에서 ADB만 사용해 최신 설치본을 직접 이동·캡처했다. `scrcpy`와 에뮬레이터는 사용하지 않았다.
- 배우기 1단계의 큐브는 기존보다 아래로 내려왔고, 코치 설명 카드와 겹치지 않도록 최종 `scale 0.68 / lift 0.16`으로 맞췄다.
- 연습 화면은 `U D L R F B 반시계` 한 줄만 남고, `2회`와 `넓은 수`가 제거되었으며 하단 동작 버튼과 글자 비율에 잘림이 없다.
- 홈 화면에서 `큐브 스킨` 진입점이 바로 보이고, 스킨 화면에서 6개 프리셋과 실시간 큐브 미리보기가 모두 보인다.
- 설정 화면에서 `배경음`과 `효과음`이 독립적으로 표시되고, 나머지 설정 행과 함께 한 화면에서 잘리지 않는다.
- 최종 실기기 캡처:
  - `C:/workAndroid/3Dcube/design-audit/density-audio-2026-08-12/a16-state.png`
  - `C:/workAndroid/3Dcube/design-audit/density-audio-2026-08-12/a16-current.png`
  - `C:/workAndroid/3Dcube/design-audit/density-audio-2026-08-12/a16-home-final.png`
  - `C:/workAndroid/3Dcube/design-audit/density-audio-2026-08-12/a16-skin-final.png`
  - `C:/workAndroid/3Dcube/design-audit/density-audio-2026-08-12/a16-settings-final.png`
- 최종 전후 비교:
  - `C:/workAndroid/3Dcube/design-audit/density-audio-2026-08-12/practice-before-after.png`
  - `C:/workAndroid/3Dcube/design-audit/density-audio-2026-08-12/lesson-before-after-final.png`
- 검증: PlayMode 전체 94/94, EditMode 전체 89/89, 최종 배우기 위치 집중 테스트 8/8 통과. 최신 APK는 Galaxy A16에 설치되어 있다.

## 5차 확인 — 스킨 중앙 정렬·배우기 하단 이동

- Source visual truth:
  - `C:/workAndroid/3Dcube/design-audit/density-audio-2026-08-12/a16-skin-final.png`
  - `C:/workAndroid/3Dcube/design-audit/density-audio-2026-08-12/a16-current.png`
- Revised implementation:
  - `C:/workAndroid/3Dcube/design-audit/density-audio-2026-08-12/a16-skin-centered-final.png`
  - `C:/workAndroid/3Dcube/design-audit/density-audio-2026-08-12/a16-lesson-lowered-final.png`
- Viewport/state: Galaxy A16 `SM_A165N`, 1080×2340, 450dpi, portrait, dark theme. 스킨은 클래식 선택 상태, 배우기는 1단계 1/4 설명 상태다. 모든 이미지는 동일한 실기기 픽셀 크기라 별도 밀도 보정 없이 1:1 비교했다.
- Full-view comparisons:
  - `C:/workAndroid/3Dcube/design-audit/density-audio-2026-08-12/skin-position-before-after.png`
  - `C:/workAndroid/3Dcube/design-audit/density-audio-2026-08-12/lesson-layout-lower-before-after.png`
- Focused evidence: 스킨 미리보기 카드의 실측 중심은 `(539.5, 777.0)`, 최종 큐브의 채색 면 경계 중심도 `(539.5, 777.0)`으로 가로·세로 중심이 일치한다. 별도 확대 비교 없이도 전체 화면에서 큐브와 카드 경계가 충분히 선명해 추가 크롭은 필요하지 않았다.

### Comparison history

- [P2] 스킨 큐브가 미리보기 카드 중심보다 약 188px 위에 있었다. 첫 보정으로 117px 내린 뒤에도 71px 위에 남아 있어 최종적으로 카드 중심까지 추가 이동했다.
- [P2] 배우기 화면에서 큐브·설명 카드·페이지 버튼 묶음이 위쪽에 몰려 있었다. 큐브 중심을 약 3.5% 내리고, 설명 카드·페이지 버튼·공식 영역을 약 5% 내렸다. 하단 고정 동작 버튼도 안전 영역 안에서 2% 내려 세로 리듬을 맞췄다.
- Post-fix evidence: 스킨 큐브는 카드 정중앙이며, 배우기 큐브와 설명 카드 사이에는 여백이 남고 설명·페이지 버튼·공식 카드가 서로 겹치거나 잘리지 않는다.

### Required fidelity surfaces

- Fonts and typography: 기존 한글 글꼴·크기·굵기·줄바꿈을 유지했고 잘림이나 새 줄바꿈 변화가 없다.
- Spacing and layout rhythm: 스킨은 카드 정중앙 정렬, 배우기는 상단 안내 → 큐브 → 설명 → 페이지 → 공식 → 하단 동작 순서가 더 고르게 분산된다.
- Colors and visual tokens: 다크 배경, Surface 카드, Accent·TextPrimary·TextSecondary 토큰을 그대로 유지했다.
- Image quality and assets: 기존 실제 3D 큐브 렌더러와 PNG 아이콘을 그대로 사용해 선명도·마스킹·비율 변화가 없다.
- Copy and content: 모든 기존 한국어 문구와 현재 단계 정보가 유지된다.

- Primary interactions checked: 홈 → 큐브 스킨, 스킨 뒤로가기, 홈 → 배우기 → 1단계 진입. ADB로 Galaxy A16에서 직접 실행했다.
- Runtime verification: 관련 PlayMode 16/16 통과, 최신 APK 설치 완료, 화면 잘림·겹침·치명 오류 없음.

final result: passed

## Iteration 6 - square camera capture and lesson notation controls

- Device and viewport: Samsung Galaxy A16 (`SM_A165N`), 1080x2340, portrait, dark theme. Validation used ADB only; no emulator or screen-mirroring process was used.
- Source visual truth:
  - `C:/workAndroid/3Dcube/design-audit/density-audio-2026-08-12/a16-real-cube-before.png`
  - `C:/workAndroid/3Dcube/design-audit/density-audio-2026-08-12/a16-lesson-lowered-final.png`
- Revised implementation:
  - `C:/workAndroid/3Dcube/design-audit/density-audio-2026-08-12/a16-real-cube-square-final.png`
  - `C:/workAndroid/3Dcube/design-audit/density-audio-2026-08-12/a16-lesson-reading-current.png`
  - `C:/workAndroid/3Dcube/design-audit/density-audio-2026-08-12/a16-lesson-pad-final.png`
  - `C:/workAndroid/3Dcube/design-audit/density-audio-2026-08-12/a16-lesson-r-moved.png`
- Combined comparison inputs opened and reviewed:
  - `C:/workAndroid/3Dcube/design-audit/density-audio-2026-08-12/camera-square-before-after.png`
  - `C:/workAndroid/3Dcube/design-audit/density-audio-2026-08-12/lesson-reading-before-after-pad-change.png`

### Comparison history

- [P1] The original camera guide stretched each cube sticker cell vertically, so a square physical cube face could not align with the overlay. Fixed by using a centered 1:1 camera preview crop and a 1:1 detection grid whose sample coordinates use the same crop.
- [P2] Lesson practice had no direct face-turn controls. Added `U D L R F B` plus the existing counter-clockwise modifier in practice state only, preserving the quieter reading state.
- Post-fix evidence: the camera comparison shows nine visually square cells inside a square preview; the lesson reading comparison has no layout regression; the practice capture shows all seven controls with clear spacing above the coach status and bottom actions.

### Required fidelity surfaces

- Fonts and typography: existing Korean type scale and weights are preserved; notation labels are large enough for the control height and remain legible on A16.
- Spacing and layout rhythm: camera grid is centered with equal horizontal and vertical proportions; lesson notation controls occupy their own row without collision or clipping.
- Colors and visual tokens: existing dark palette, surface, border, accent, and secondary text tokens are reused.
- Image quality and assets: existing cube art and real 3D renderer are preserved; the camera texture is center-cropped rather than stretched.
- Copy and content: existing Korean guidance remains unchanged; notation uses the standard `U D L R F B` labels requested by the user.

- Primary interactions checked on device: open real-cube capture; return Home; open Learn; open stage 1; enter Practice; press `R`. The final `R` capture confirms the cube state visibly changes.
- Automated verification: focused PlayMode suite passed 22/22. Latest APK built and installed successfully on the connected Galaxy A16. ADB log scan found no FATAL/Unity exception signature.

final result: passed
