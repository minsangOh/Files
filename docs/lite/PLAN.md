# Files Lite 기획서

files-community/Files를 포크해 RAM 8GB 사무용 노트북에서 부담 없이 쓰도록 메모리 점유를 줄인다.

## 현재 상태 (2026-11-13)

- 포크 `minsangOh/Files` 생성, `D:\github\Files`에 클론, `upstream` 원격 추가 완료.
- 제품 브랜치 `lite-main` 생성·푸시 완료 (`lite/bootstrap`에서 분기).
- 빌드 도구 설치 완료: .NET SDK 10.0.404, Build Tools 2026 18.11 (`D:\BuildTools2026`). 설치 관리자가 재부팅을 권고했고 아직 재부팅하지 않았다.
- 원본 `Files.App` Debug x64 빌드 성공 (재빌드 약 2분).
- 개발자 모드를 켜고 `FilesDev` 패키지 등록·실행 성공. "실행" 절의 수동 복사 방식으로 정상 기동한다.
- 포크 전용 워크플로 `.github/workflows/lite-package.yml`이 Release x64 서명 패키지를 만든다 ("Release 패키지 설치" 참고).
- 0~4단계 구현 완료 (2026-11-13, `lite-main`). 결과와 판단은 각 단계 절과 "측정 기록"을 본다.
- 목표 대비 (자동 측정, Private 합계): S1 132MB (목표 130), S4 245MB (목표 190, 미달), S5 188MB (목표 150, 부득이 200 이내), S6 0.
- 앱 실행 확인은 자동 시험(탭, 듀얼 패인 복원, 탭 재우기 후 깨우기, 미리보기 패널 표시)만 했다. 사람이 직접 조작한 확인은 아직 없다.

## 대상 PC와 사용 환경

| 항목 | 값 |
|---|---|
| 기종 | 삼성 950XED, Windows 11 Home 26200 |
| CPU | i5-1240P (12코어 16스레드) |
| RAM | 7.6GB. 평소 가용 1GB 미만 (Claude, Chrome, PowerPoint, ChatGPT 등 상시 실행) |
| GPU | Iris Xe 내장 (RAM 공유) |
| 디스크 | C: 여유 49GB, D: 여유 753GB |
| 네트워크 드라이브 | W: (`\\SelimElec-NAS\Selim-Elec\개발팀`) |
| 비교 대상 | explorer.exe 약 180MB (2026-11-13 측정) |

## 결정 사항

- **유지**: 탭, 듀얼 패인, 미리보기(정보) 패널.
- **제거 후보**: 그 밖의 전부. Git 통합, FTP, 홈 위젯, 원격 진단(Sentry), 자연어 검색, 압축 등.
- **메모리 목표** (2026-11-13 사용자와 확정. Release 패키지, Files + Server Private 합계, 4단계까지 마친 최종 목표):

  | 시나리오 | 원본 | 목표 |
  |---|---|---|
  | S1 유휴 | 175MB | 130MB 이하 (약 -25%) |
  | S4 탭 5개 | 281MB | 190MB 이하 (약 -33%) |
  | S5 듀얼 패인 + 미리보기 | 265MB | 150MB 이하 (약 -43%). 부득이하면 200MB 이하 (약 -25%) |
  | S6 창 닫은 뒤 | 0 | 0 유지 |

  WS는 메모리 압박에 따라 크게 흔들려 판정에서 뺀다. 원본 값은 세션 복원 탭이 섞이지 않은 재측정값이다.
- **빌드**: 로컬에는 명령줄 Build Tools만 두고 VS Code로 편집한다. Visual Studio IDE는 설치하지 않는다.

## 원본 구조에서 확인한 사실

직접 확인한 것:

- .NET 10, WinUI 3 (Windows App SDK 2.6.5). `global.json`은 SDK 10.0.403, rollForward latestMajor.
- C++ 프로젝트 3개(Launcher, OpenDialog, SaveDialog)는 툴셋 `v145`, 즉 Visual Studio 2026을 요구한다. CI 러너도 `windows-2025-vs2026`이다.
- `Files.App.csproj`는 C++ 프로젝트를 참조하지 않는다. 참조는 BackgroundTasks, Controls, Storage, Shared, SourceGenerators와 별도 빌드되는 Files.App.Server다.
- 배포 빌드는 Native AOT + Satori GC다 (`PublishWithSatoriGC` SDK, `Properties/PublishProfiles/win-*.pubxml`).
- 이 PC에는 Build Tools 2022 17.14(C++ 워크로드, Windows SDK 26100)만 있고 .NET SDK와 .NET 빌드 워크로드, ATL은 없다.

조사 에이전트가 보고한 것 (줄 번호는 조사 시점 기준이다. 수정 전에 해당 파일을 다시 확인할 것):

- 프로세스가 둘이다. `Files.exe`(UI)와 `Files.App.Server.exe`(드라이브·아이콘·설정·휴지통·컨텍스트 메뉴를 맡는 별도 프로세스). 서버 없이는 기본 탐색이 안 된다.
- 창을 닫아도 서버가 남는다. `CloseToBackground`(배포 빌드 기본 true)와 `ShowSystemTrayIcon`(기본 true)이 제어한다. `src/Files.Shared/Models/BackgroundSettings.cs`, `src/Files.App.Server/ServerHost.cs`.
- 비활성 탭이 통째로 살아 있다. 탭마다 `Frame` + `ShellPanesPage` + `ShellViewModel`(항목 목록, 비트맵, 폴더 감시자)을 유지하고, 탭을 내리거나 재우는 기능이 없다. `UserControls/TabBar/TabBarItem.cs`, `Views/MainPage.xaml`.
- 레이아웃 페이지 캐시: 셸 페이지의 `ItemDisplayFrame`은 기본 CacheSize(10), Grid/Details 페이지는 `NavigationCacheMode="Enabled"`.
- 상한 없는 메모리 캐시: `Services/Storage/IconCacheService.cs`(byte[]와 BitmapImage), `Files.Storage/Windows/Helpers/ShellImage.cs`, `WindowsStorableHelpers.Icon.cs`의 `DllIconCache`.
- `Helpers/BitmapHelper.cs`의 `ToBitmapAsync`는 `decodeSize`가 0이면 원본 크기로 디코딩한다.
- Git은 스위치가 없다. 폴더를 열 때마다 저장소 여부를 검사하고(`Win32StorageEnumerator.cs`), 저장소에 들어가면 자동 fetch를 한다(`BaseShellPage.cs`). `ListedItem`에도 Git 필드가 있다.
- 시작 시 명령 객체 약 170개를 한꺼번에 만든다 (`Data/Commands/Manager/CommandManager.cs`).
- 자연어 검색(`Utils/Storage/Search/NaturalLanguageSearch.cs`)이 Windows AI 모델을 미리 데운다. 설정 `EnableNaturalLanguageSearch` 기본 true.
- WebView2는 릴리스 노트 페이지에서만 쓴다. 업데이트 직후 자동으로 열린다.
- Sentry는 CI 배포 빌드에서만 초기화되지만 패키지와 로거 공급자는 항상 링크된다.
- PDF·Office 미리보기는 셸 미리보기 핸들러(별도 프로세스)를 쓴다. 앱 안의 PDF·HTML 미리보기 코드는 주석 처리된 죽은 코드다.
- GC 설정 파일은 없다. `GCConserveMemory` 같은 조정값을 쓰지 않는다.

## 브랜치와 upstream 동기화

- `main`: upstream 미러. 커밋 금지. `git fetch upstream` 후 fast-forward만 한다.
- `lite-main`: 제품 브랜치. 0단계가 끝나면 `lite/bootstrap`에서 만든다. (`lite`라는 이름은 `lite/…` 브랜치와 충돌해서 쓸 수 없다.)
- `lite/<주제>`: 작업 브랜치. `lite-main`으로 병합한다.
- upstream 병합 충돌을 줄이려고 **기본값 변경 → 지연 로딩·해제 → 삭제** 순으로 진행한다.

## 측정 방법

모든 단계에서 같은 절차로 잰다. 수치는 아래 "측정 기록"에 남긴다.

1. 다른 Files 인스턴스를 모두 끄고 두 프로세스가 끝난 것을 확인한다. 앱은 기본값으로 지난 세션의 탭을 복원하므로(`ContinueLastSessionOnStartUp`), `LocalState\settings\user_settings.json`에서 `LastSessionTabList`와 이전 시나리오에서 바꾼 설정(예: `AlwaysOpenDualPaneInNewTab`, `IsInfoPaneEnabled`)을 지운 뒤 앱을 실행한다.
2. 시나리오별로 30초 기다린 뒤 측정한다.

```powershell
.\tools\lite\Measure-Memory.ps1 -Label "기준 / 유휴"
```

| 시나리오 | 내용 |
|---|---|
| S1 유휴 | 홈 화면에서 탭 1개 |
| S2 로컬 대량 | 파일 수천 개인 로컬 폴더를 자세히 보기로 열기 |
| S3 NAS | `W:\`의 실제 업무 폴더 열기 |
| S4 탭 5개 | 서로 다른 폴더로 탭 5개 |
| S5 듀얼 패인 + 미리보기 | 듀얼 패인에서 이미지·PDF 미리보기 |
| S6 창 닫은 뒤 | 창을 닫고 60초 뒤 남은 프로세스 |

Debug(JIT) 빌드와 Release(Native AOT) 빌드는 점유가 크게 다르다. 목표 판정은 Release 패키지로 하고, Debug 수치는 변경 전후 상대 비교에만 쓴다.

## 단계

### 0단계: 빌드 환경과 기준 측정

1. 빌드 도구 설치 (아래 "빌드 도구 설치" 참고).
2. `Files.App.csproj` Debug x64 빌드 성공 확인.
3. 포크에서 GitHub Actions를 켜고 `ci.yml`로 Release x64 패키지를 받아 설치.
4. 원본 그대로 S1~S6 측정, 기록.
5. 측정값을 보고 사용자와 메모리 목표를 정해 이 문서에 적는다.
6. 패키지 이름·게시자·표시 이름을 바꿔 공식 Files와 나란히 설치되게 한다. **완료**: 신원 `FilesLite`, 표시 이름 "Files Lite". 공식판(FilesStable, FilesPreview, 스토어)과 원래 이름부터 겹치지 않았고, 이제 upstream Dev 빌드(`FilesDev`)와도 겹치지 않는다. 실행 별칭 `files-dev.exe`와 프로토콜 `files-dev:`는 코드 여러 곳이 쓰므로 그대로 뒀다(upstream Dev 빌드와 함께 설치하면 별칭이 겹친다). 게시자는 서명 인증서와 맞춰야 해서 `CN=Files` 그대로다. 라이선스 파일은 그대로 둔다.

### 1단계: 기본값만 바꾸기 (코드 삭제 없음)

완료 (2026-11-13, 브랜치 `lite/stage1-defaults`). 항목별 효과는 원본 Release 패키지에 설정 값을 넣어 S1(새로 띄운 홈, 탭 1개)을 3회씩 재서 판단했다. 원본 3회의 Files Private가 7MB쯤 흔들려 그보다 작은 차이는 효과 없음으로 봤다.

| 항목 | S1 Private 합계 (3회 평균) | 판단 |
|---|---|---|
| 원본 | 174MB | |
| 홈 위젯 4개 끔 | 155MB (-19) | **적용**. 하나씩 끄면 어느 것도 효과가 없었다. 넷 다 꺼야 줄어든다 |
| `ShowSystemTrayIcon` false | 171MB (-3) | 미적용. 오차 범위 |
| `EnableNaturalLanguageSearch` false | 173MB (-1) | 미적용. 모델은 검색창에 문장을 칠 때만 데운다 |
| 사이드바 클라우드·WSL·태그 섹션 끔 | 174MB (0) | 미적용. 이 PC에는 해당 항목이 거의 없다 |
| `CloseToBackground` false | 잴 수 없음 | 미적용. 포크 패키지는 Dev 환경이라 이미 false다 (S6 = 0) |
| 업데이트 확인·릴리스 노트 자동 열기 끔 | 잴 수 없음 | 미적용. Dev 환경에서는 원래 돌지 않는다. 설정 스위치도 없다 |

적용 후 Release 패키지 결과는 "측정 기록"의 1단계 행을 본다.

### 2단계: 살아 있는 메모리 줄이기 (핵심)

완료 (2026-11-13). 판단은 자동 측정(아래 "측정 기록"의 자동 측정 절차)으로 했다.

- **비활성 탭 재우기 (적용)**: `TabBarItem`이 선택 해제 30초 뒤 탭 페이지를 Dispose하고 `NavigationParameter`만 남긴다. 다시 선택하면 그 값으로 다시 연다(`AppModel.TabStripSelectedIndex`에서 깨우고 나머지를 예약). 해제 직후 기존 `AppMemoryHelper.RequestTrim()`을 부른다. 자동 시험에서 잠든 듀얼 패인 탭이 두 창 그대로 돌아오는 것을 확인했다. 스크롤 위치와 선택 항목은 돌아오지 않는다.
  - 효과는 미미했다. S4에서 탭이 잠든 뒤 WS는 크게 줄지만(작업 집합 비우기) Private는 215 → 212MB 정도다. 해제한 페이지가 다른 곳에 참조로 남는지, WinUI 네이티브 힙이 반환하지 않는지는 조사하지 못했다. 다음에 메모리를 더 줄이려면 여기부터 본다.
- 레이아웃 페이지 캐시 축소 (미적용): Grid/Details 페이지 캐시는 형식마다 인스턴스 하나라 이득이 작다고 보고 손대지 않았다.
- 아이콘·썸네일 캐시 상한 (미적용): `IconCacheService`는 확장자·크기·DPI 키, `ShellImage`·`DllIconCache`는 리소스 파일·인덱스·크기 키라 항목 수에 비례해 늘지 않는다.
- `PreloadedIconData` 비우기 (미적용): 값이 `IconCacheService`의 공유 배열 참조라 항목마다 복사본이 없다.
- 미리보기 리소스 해제 (미적용): `ShellPreview`가 Unloaded에서 `UnloadPreview()`를 부르고, 패널을 끌 때도 해제한다. 이미 처리돼 있다.

### 3단계: 기능 제거

완료 (2026-11-13). 실행 중인 Files 프로세스의 모듈을 보면 S1~S5에서 Git(libgit2), Sentry, WebView2, 7z, FTP, Win2D 모두 메모리에 올라오지 않는다. 그래서 제거의 메모리 효과는 측정 시나리오에서 거의 없다. "결정 사항"의 제거 후보라 사용자 지시(2026-11-13, 측정에 매몰되지 말고 나머지를 구현)에 따라 제거했다.

| 대상 | 결과 |
|---|---|
| Sentry | 제거 (`f93e6a36`). 패키지가 AOT 링크에 `gdi32.lib`를 넣어 주고 있어서 `NativeLibrary`로 직접 추가했다 (`4a048ee6`) |
| 자연어 검색 | 제거 (`f93e6a36`). 매니페스트 `systemAIModels` 권한도 뺐다 |
| 릴리스 노트 WebView2, 죽은 HTML/PDF 미리보기 | 제거 (`f93e6a36`) |
| FTP | 제거 (`518514fa`). `ftp://` 경로는 이제 잘못된 경로처럼 실패한다 |
| Git 통합 | 제거 (`e6c34ae0`). 열, 상태 표시줄 브랜치 UI, 대화상자, LibGit2Sharp 포함. 개발 도구 설정의 "Open in IDE: Git repos" 선택지는 저장값 호환 때문에 남았고 이제 버튼이 보이지 않는다 |
| 압축 | 유지. 제거 전 사용자 확인 대상이다 |
| 코드·마크다운 미리보기 | 유지. 제거 전 사용자 확인 대상이다 |
| 홈 위젯 | 유지. 1단계에서 기본 끔이고 설정에서 다시 켤 수 있다 |
| 상태 센터 속도 그래프(Win2D) | 유지. Win2D는 글꼴 썸네일(`FontFileHelper`)에도 쓰여 그래프만 지워서는 패키지를 뺄 수 없다 |

`.resw` 문자열은 upstream 충돌을 줄이려고 지우지 않았다.

### 4단계: 런타임 조정

완료 (2026-11-13).

- **GC (적용)**: Satori를 빼고 표준 워크스테이션 GC, `ConcurrentGarbageCollection=false`, `System.GC.ConserveMemory=5`를 두 프로젝트(앱, 서버)에 적용했다. 시나리오마다 20~45MB 줄고 반복 측정 편차도 거의 없어졌다. 서버 Private는 약 47 → 34MB.
- **서버 미리 데우기 (적용)**: 서버 시작 시 컨텍스트 메뉴 warm-up과 업데이트 확인 타이머를 시작하지 않는다. Dev 환경에서는 클라이언트의 릴리스 노트 HTTP 확인도 하지 않는다. 첫 우클릭 메뉴가 조금 느려질 수 있다. 서버 Private는 최종 측정에서 약 28MB다. 이 항목만 따로 잰 값은 없다.
- `CommandManager` 지연 생성 (미적용): 명령 객체가 작고 리팩터링 범위가 커서 하지 않았다.

## 빌드 도구 설치

필요한 것:

- .NET SDK 10.0.403 이상
- Visual Studio 2026 Build Tools: .NET 데스크톱 빌드 도구, C++ 빌드 도구, C++ ATL

```powershell
winget install --id Microsoft.DotNet.SDK.10 -e --source winget
```

```powershell
winget install --id Microsoft.VisualStudio.BuildTools -e --source winget --override "--installPath D:\BuildTools2026 --add Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools --add Microsoft.VisualStudio.Workload.VCTools --add Microsoft.VisualStudio.Component.VC.ATL --includeRecommended --passive --norestart --nocache --wait"
```

확인되지 않은 점:

- 워크로드 ID는 Build Tools 2022 기준이다. 2026 설치 관리자에서 ID가 달라졌으면 설치 화면에서 직접 고른다.
- `--installPath`로 D:에 설치해도 공용 구성 요소 몇 GB는 C:에 들어간다.
- 기존 Build Tools 2022(17.14)만으로 C# 프로젝트가 빌드되는지는 시험하지 않았다. .NET SDK만 먼저 설치하고 아래 빌드를 돌려 보면 알 수 있다. 되면 2026 설치를 미뤄도 된다.

빌드 (2026-11-13 이 PC에서 성공한 절차, 한 번에 하나씩):

```powershell
$env:PATH = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer;$env:PATH"
$msb = "D:\BuildTools2026\MSBuild\Current\Bin\MSBuild.exe"
& $msb Files.slnx -t:Restore -p:Platform=x64 -p:Configuration=Debug -p:PublishReadyToRun=true -m:1 -v:quiet -clp:ErrorsOnly
& $msb src\Files.App\Files.App.csproj -t:Build -p:Configuration=Debug -p:Platform=x64 -p:AppxBundle=Never -p:GenerateAppxPackageOnBuild=false -m:1 -v:quiet -clp:ErrorsOnly
```

빌드하면서 확인한 점:

- `Files.App.csproj`만 `-restore`하면 실패한다(NETSDK1004). `Files.App.Server`가 프로젝트 참조가 아니라서 복원에서 빠진다. 솔루션 전체를 먼저 복원한다.
- 서버는 Debug에서도 Native AOT로 링크한다. `vswhere.exe`가 PATH에 없으면 링크 단계가 MSB3073으로 실패한다. 위 첫 줄이 그 해결이다.

## 실행

패키지 앱이라 exe를 바로 실행할 수 없고 등록이 필요하다. Windows 설정에서 개발자 모드를 켜야 한다(설정 → 시스템 → 고급 → 개발자용).

명령줄 빌드는 서버 실행 파일을 앱 레이아웃에 넣지 않는다(`PublishFilesAppServerForMsix`는 패키징할 때만 돈다). 직접 복사한 뒤 등록한다. 2026-11-13에 이 절차로 등록과 실행을 확인했다.

```powershell
$out = "src\Files.App\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64"
$pub = "src\Files.App.Server\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\publish"
New-Item -ItemType Directory -Force "$out\Files.App.Server" | Out-Null
Copy-Item "$pub\*" "$out\Files.App.Server\" -Recurse -Force -Exclude *.winmd, *.pdb
Add-AppxPackage -Register (Resolve-Path "$out\AppxManifest.xml")
```

등록되면 시작 메뉴의 "Files - Dev"나 `files-dev.exe <경로>`로 실행한다. 패키지 이름은 `FilesDev`다. 이미 떠 있으면 `files-dev.exe <경로>`는 새 탭을 열고 바로 돌아오지만, 첫 실행일 때는 앱이 끝날 때까지 돌아오지 않는다. 스크립트에서는 `Start-Process`로 띄운다.

Release(Native AOT) 패키징은 컴파일러가 메모리를 많이 쓴다. RAM 7.6GB인 이 PC 대신 포크의 GitHub Actions에서 돌린다. 이 판단은 추정이고 로컬 AOT 빌드는 시험하지 않았다.

### Release 패키지 설치

upstream `ci.yml`은 포크에서 돌지 않는다. 모든 잡에 `github.repository_owner == 'files-community'` 조건이 있고 push 트리거가 `main`뿐이다. 그래서 `ci.yml`은 그대로 두고 패키징 단계만 옮긴 `.github/workflows/lite-package.yml`을 쓴다. `lite-main`과 `lite/**` 푸시마다 돌고(약 10분), 결과물 "Files Lite Package (Release, x64)"에 `.msixbundle`과 공개 인증서 `.cer`가 들어 있다. 새 커밋이 없는 브랜치 푸시와 빈 커밋은 `paths-ignore` 때문에 빌드를 시작하지 않는다.

서명은 고정 인증서(CN=Files, 지문 `DBC4C0B8BA5CC2DE8F8150F973D9D7A14CBF75BE`, 2036-11-13 만료)로 한다. 저장소 시크릿 `LITE_SIGNING_PFX_BASE64`, `LITE_SIGNING_PFX_PASSWORD`에 들어 있고, 워크플로가 비밀번호 없는 임시 PFX로 다시 내보내 서명한다(패키징 작업이 비밀번호 있는 키를 못 읽는다, APPX0105). 원본 PFX와 비밀번호는 이 PC의 `D:\lite-pkg\signing`에만 있다.

1. 받기: `gh run download <run-id> --repo minsangOh/Files -D D:\lite-pkg\run-<run-id>`
2. 인증서 신뢰 (사용자가 관리자 PowerShell에서 직접, 고정 인증서라 한 번만 하면 된다):
   `Import-Certificate -FilePath <...>\Files.App_<버전>_x64.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople`
3. 같은 신원(`FilesLite`, 이전 이름 `FilesDev`)의 등록이 있으면 지운다: `Get-AppxPackage FilesLite | Remove-AppxPackage`. 앱 설정과 탭 기록도 지워진다.
4. 설치: `Add-AppxPackage -Path <...>\Files.App_<버전>_x64.msixbundle`. Windows App Runtime 2.6.5가 필요하다(이 PC에는 있다).

인증서 없이 시험하려면(개발자 모드 필요): 번들 안의 `*_x64.msix`와 scale 리소스 msix를 한 폴더에 풀고 `AppxSignature.p7x`, `AppxBlockMap.xml`, `[Content_Types].xml`, `AppxMetadata`를 뺀 뒤 `Add-AppxPackage -Register <폴더>\AppxManifest.xml`로 등록한다. 2026-11-13 자동 측정은 모두 이 방식으로 설치해 쟀다.

이 패키지는 CD의 환경 치환을 거치지 않아 `AppEnvironment`가 Dev다. 따라서 공식 사이드로드판과 달리 `CloseToBackground` 기본값이 false이고, Sentry·업데이트 확인·릴리스 노트 자동 열기가 돌지 않는다(릴리스 노트 존재 여부 HTTP 확인만 한다).

## 측정 기록

값은 평균이다. 2026-11-13 측정은 모두 가용 RAM 0.3~0.8GB 상태에서 쟀다. Windows가 작업 집합(WS)을 깎는 상황이라 WS는 실제보다 낮게 나왔을 수 있다. 비교에는 Private를 우선 본다.

| 날짜 | 커밋 | 빌드 | 시나리오 | Files WS / Private (MB) | Server WS / Private (MB) | 비고 |
|---|---|---|---|---|---|---|
| 2026-11-13 | f6f35e79 (원본) | Debug x64 | S1 유휴 | 276 / 123 | 102 / 48 | 첫 실행 직후, 홈, 탭 1개 |
| 2026-11-13 | f6f35e79 (원본) | Debug x64 | S2 로컬 대량 | 283 / 202 | 97 / 46 | 새로 띄워 `C:\Windows\System32`(4,759개) 열기. 이전 세션 탭이 복원됐는지는 확인하지 못했다 |
| 2026-11-13 | f6f35e79 (원본) | Debug x64 | S4 탭 5개 | 341 / 253 | 82 / 35 | 홈, System32, NAS 폴더(25개), `D:\github`, 문서 |
| 2026-11-13 | f6f35e79 (원본) | Debug x64 | S6 창 닫은 뒤 | 0 | 0 | 60초 뒤 두 프로세스 모두 종료. Dev 빌드는 `CloseToBackground` 기본값이 false다 |
| 2026-11-13 | f6f35e79 (원본) | Release x64 (CI) | S1 유휴 | 175 / 128 | 83 / 47 | 설치 직후 첫 실행, 홈, 탭 1개. 가용 RAM 0.35GB |
| 2026-11-13 | f6f35e79 (원본) | Release x64 (CI) | S2 로컬 대량 | 282 / 182 | 98 / 46 | 새로 띄워 `C:\Windows\System32`(4,759개) 열기 |
| 2026-11-13 | f6f35e79 (원본) | Release x64 (CI) | S3 NAS | 214 / 251 | 89 / 45 | 새로 띄워 NAS의 "ERP 업로드 완료" 폴더 열기, 자세히 보기. 경로와 항목 수는 확인하지 못했다. 시작 메뉴로 띄워 S4의 탭 5개가 복원된 상태였을 수 있다. **무효, 재측정 필요** |
| 2026-11-13 | f6f35e79 (원본) | Release x64 (CI) | S4 탭 5개 | 280 / 236 | 88 / 45 | S2에 이어 `W:\`, `D:\github`, 문서, 홈 탭 추가. 탭 5개는 사용자가 화면으로 확인 |
| 2026-11-13 | f6f35e79 (원본) | Release x64 (CI) | S5 듀얼 패인 + 미리보기 | 241 / 289 | 94 / 53 | 새로 띄워 듀얼 패인, 미리보기 켜고 왼쪽 이미지·오른쪽 PDF 선택(사용자 조작). 새 미리보기 호스트 프로세스는 보이지 않았다. 세션 복원으로 탭 3개(홈, System32, 듀얼 패인)였다. **무효, 재측정 필요** |
| 2026-11-13 | f6f35e79 (원본) | Release x64 (CI) | S6 창 닫은 뒤 | 0 | 0 | 60초 뒤 두 프로세스 모두 종료 (Dev 환경이라 `CloseToBackground` false) |
| 2026-11-13 | f6f35e79 (원본) | Release x64 (CI) | S3 NAS (재측정) | 316 / 214 | 98 / 48 | 세션 초기화 후 탭 1개, NAS 업무 폴더(944개) 자세히 보기. 측정 뒤 설정에 정보 패널이 켜져 있었다 |
| 2026-11-13 | f6f35e79 (원본) | Release x64 (CI) | S5 듀얼 패인 + 미리보기 (재측정) | 260 / 218 | 99 / 47 | 세션 초기화 후 탭 1개, 듀얼 패인(NAS 공용폴더 / 개발팀), 미리보기 켜고 이미지·PDF 선택(사용자 조작) |
| 2026-11-13 | 1단계 (홈 위젯 끔) | Release x64 (CI) | S1 유휴 | 205 / 114 | 96 / 47 | 설치 직후. 합계 161 (원본 175) |
| 2026-11-13 | 1단계 (홈 위젯 끔) | Release x64 (CI) | S2 로컬 대량 | 277 / 180 | 96 / 46 | 합계 226 (원본 228). 경로로 바로 열어 홈을 거치지 않는다 |
| 2026-11-13 | 1단계 (홈 위젯 끔) | Release x64 (CI) | S3 NAS | 271 / 186 | 90 / 51 | 탭 1개, 같은 NAS 폴더(944개), 정보 패널 켜짐. 합계 237 (원본 262). 홈에서 이동한 탭이라 위젯 몫이 빠졌다 |
| 2026-11-13 | 1단계 (홈 위젯 끔) | Release x64 (CI) | S4 탭 5개 | 269 / 228 | 83 / 47 | 원본과 같은 순서. 합계 275 (원본 281) |
| 2026-11-13 | 1단계 (홈 위젯 끔) | Release x64 (CI) | S5 듀얼 패인 + 미리보기 | 243 / 210 | 92 / 46 | 탭 1개, 공용폴더 / 개발팀, 이미지·PDF 선택(사용자 조작). 합계 256 (원본 265) |
| 2026-11-13 | 1단계 (홈 위젯 끔) | Release x64 (CI) | S6 창 닫은 뒤 | 0 | 0 | 60초 뒤 두 프로세스 모두 종료 |
| 2026-11-13 | 1단계 `f9d3d096` | Release x64 (CI) | 자동 S1~S5 | 아래 비고 | 아래 비고 | 자동 측정 2회 평균 합계: S1 163, S2 253, S3 245, S4 301, S5 219. 같은 시나리오인데 회차 간 Files Private가 40~50MB씩 흔들렸다(Satori) |
| 2026-11-13 | 최종 `e6c34ae0` | Release x64 (CI) | S1 유휴 | 165 / 105 | 66 / 28 | 자동 2회 평균. 합계 132 (자동 1단계 163) |
| 2026-11-13 | 최종 `e6c34ae0` | Release x64 (CI) | S2 로컬 대량 | 262 / 173 | 74 / 29 | 합계 202 (자동 1단계 253) |
| 2026-11-13 | 최종 `e6c34ae0` | Release x64 (CI) | S3 NAS | 259 / 167 | 74 / 28 | 경로로 바로 연 NAS 폴더(944개). 합계 195 (자동 1단계 245) |
| 2026-11-13 | 최종 `e6c34ae0` | Release x64 (CI) | S4 탭 5개 | 50 / 217 | 67 / 28 | 마지막 탭 뒤 60초 기다림. 탭 재우기로 WS가 비워진다. 합계 245 (자동 1단계 301) |
| 2026-11-13 | 최종 `e6c34ae0` | Release x64 (CI) | S5 듀얼 패인 + 미리보기 | 258 / 159 | 75 / 29 | 자동(세션 주입). 합계 188 (자동 1단계 219) |
| 2026-11-13 | 최종 `e6c34ae0` | Release x64 (CI) | S6 창 닫은 뒤 | 0 | 0 | 2회 모두 60초 뒤 프로세스 없음 |

Debug에서 아직 재지 않은 것: S3 단독, S5. Release는 S1~S6 모두 쟀다. 1단계 행은 시나리오마다 1회 측정이라 10MB 안쪽 차이는 오차일 수 있다.

**자동 측정 절차** (2026-11-13부터): 사람 조작 없이 S1~S6를 잰다. 매 시나리오 전에 앱을 닫고 `user_settings.json`에서 세션 탭과 시나리오 설정을 지운다. S2·S3는 경로 인수로 바로 연다(홈을 거치지 않는다). S4는 마지막 탭을 연 뒤 60초 기다린다(탭 재우기 30초 이후). S5는 세션에 듀얼 패인 탭(왼쪽 `W:\공용폴더`의 이미지, 오른쪽 `W:\`의 PDF 선택)과 `IsInfoPaneEnabled`를 넣고 띄운다. 사람이 조작한 S3·S5와 조건이 달라 값을 직접 비교하지 않는다. 스크립트는 세션 scratchpad에 있었고 저장소에는 넣지 않았다.
