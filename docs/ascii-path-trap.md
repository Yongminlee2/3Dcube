# 한글 경로가 안드로이드 빌드를 깨뜨리는 방식

이 PC의 Unity는 `C:\Users\사용자\AppData\Local\Unity\Editors\6000.3.18f1`에 설치돼 있다.
사용자 폴더 이름이 한글이라, **프로젝트 경로가 ASCII여도** 안드로이드 빌드가 두 군데서 깨진다.
둘 다 원인이 겉으로 드러나지 않아 추적에 시간이 든다. 다시 물리지 않도록 남긴다.

## 증상 1 — IL2CPP가 `size_t`를 모른다

```
sysroot/usr/include/bits/pthread_types.h(37,3): error: unknown type name 'size_t'
sysroot/usr/include/string.h(44,92): error: unknown type name 'size_t'; did you mean 'ssize_t'?
```

NDK가 제공하는 시스템 헤더 수백 줄이 한꺼번에 무너진다. NDK가 망가진 것처럼 보이지만 아니다.

**원인** — clang은 자기 실행 파일 경로에서 내장 헤더 폴더(`lib/clang/18/include`)를 유도한다.
경로에 한글이 섞이면 그 계산이 실패해서 해당 폴더가 인클루드 검색 목록에서 통째로 빠진다.
`stddef.h`가 거기 있으므로 `size_t`가 정의되지 않고, 그걸 쓰는 모든 시스템 헤더가 줄줄이 깨진다.

**확인법** — 실패한 clang 명령에 `-v`를 붙여 인클루드 검색 목록을 본다.
`lib/clang/18/include`가 없으면 이 문제다.

## 증상 2 — Gradle prefab 단계에서 "네트워크 경로를 찾지 못했습니다"

```
Error while executing process ...\logs\arm64-v8a\prefab_command.bat
```

`prefab_stderr.txt`를 열면 `네트워크 경로를 찾지 못했습니다`와 함께,
인자마다 앞 세 글자가 잘린 이상한 오류가 이어진다(`lass-path`, `tl`, `utput`…).

**원인** — Gradle이 만드는 `prefab_command.bat`은 UTF-8로 쓰이는데 cmd는 시스템 코드페이지로 읽는다.
JDK 경로의 한글이 깨져 cmd가 그걸 UNC 경로로 오인한다. 첫 줄이 실패하면서
`^` 줄 이음이 무너져 나머지 줄이 제각기 명령으로 해석되고, 그래서 앞 글자가 잘린 것처럼 보인다.

임시 폴더도 같은 문제다 — `--output "C:\Users\사용자\AppData\Local\Temp\agp-prefab-staging..."`.

## 해결

도구 경로를 ASCII 정션으로 가리키고, 임시 폴더와 Gradle 홈을 환경 변수로 바꾼다.

정션 만들기 (PowerShell, 한 번만):

```powershell
$base = "C:\Users\사용자\AppData\Local\Unity\Editors\6000.3.18f1\Editor\Data\PlaybackEngines\AndroidPlayer"
New-Item -ItemType Junction -Path C:\workAndroid\ndk-ascii         -Target "$base\NDK"
New-Item -ItemType Junction -Path C:\workAndroid\sdk-ascii         -Target "$base\SDK"
New-Item -ItemType Junction -Path C:\workAndroid\jdk-ascii         -Target "$base\OpenJDK"
New-Item -ItemType Junction -Path C:\workAndroid\gradle-tool-ascii -Target "$base\Tools\gradle"
```

Unity 쪽 설정은 `Assets/Editor/ProjectSetup.cs`의 `ConfigureAndroidTools()`가 맡는다.
`ProjectSetup.Configure`를 한 번 돌리면 적용된다.

빌드할 때는 환경 변수를 함께 준다:

```
TEMP=C:/workAndroid/tmp-ascii
TMP=C:/workAndroid/tmp-ascii
GRADLE_USER_HOME=C:/workAndroid/gradle-home-ascii
```

## 정션이 통하는 이유

clang은 자기가 **호출된 경로 그대로** 리소스 폴더를 계산한다. 정션으로 들어가면
실제 파일이 한글 경로에 있어도 clang이 보는 문자열은 ASCII라서 계산이 성공한다.
같은 이유로 cmd도 정션 경로 문자열을 깨뜨릴 일이 없다.

(Gradle 사용자 홈은 예외다. Gradle은 실제 경로를 되짚어가므로 정션으로는 안 되고,
`C:\workAndroid\gradle-home-ascii`처럼 **진짜 디렉터리**여야 한다.)
