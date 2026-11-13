# Files Lite 기획서

files-community/Files를 포크해 RAM 8GB 사무용 노트북에서 부담 없이 쓰도록 메모리 점유를 줄인다.

## 현재 상태 (2026-11-13)

- 포크 `minsangOh/Files` 생성, `D:\github\Files`에 클론, `upstream` 원격 추가 완료.
- 제품 브랜치 `lite-main` 생성·푸시 완료 (`lite/bootstrap`에서 분기).
- 빌드 도구 설치 완료: .NET SDK 10.0.404, Build Tools 2026 18.11 (`D:\BuildTools2026`). 설치 관리자가 재부팅을 권고했고 아직 재부팅하지 않았다.
- 원본 `Files.App` Debug x64 빌드 성공 (재빌드 약 2분).
- 개발자 모드를 켜고 `FilesDev` 패키지 등록·실행 성공. "실행" 절의 수동 복사 방식으로 정상 기동한다.
- 포크 전용 워크플로 `.github/workflows/lite-package.yml`이 Release x64 서명 패키지를 만든다 ("Release 패키지 설치" 참고).
- 원본 Debug와 Release 패키지의 기준 측정 완료 ("측정 기록" 참고). 메모리 목표 확정("결정 사항"). 다음 작업은 1단계다.

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
6. 패키지 이름·게시자·표시 이름을 바꿔 공식 Files와 나란히 설치되게 한다 (`src/Files.App/Package.appxmanifest`, `.github/scripts/Configure-AppxManifest.ps1`). 라이선스 파일(`LICENSE-MIT`, `LICENSE-MPL`)은 그대로 둔다.

### 1단계: 기본값만 바꾸기 (코드 삭제 없음)

- `CloseToBackground` false, `ShowSystemTrayIcon` false → 창을 닫으면 서버도 종료.
- `EnableNaturalLanguageSearch` false.
- 홈 위젯 기본 끔 (`ShowRecentFilesWidget`, `ShowNetworkLocationsWidget`, `ShowDrivesWidget`, `ShowQuickAccessWidget`).
- 사이드바 `ShowCloudDrivesSection`, `ShowWslSection`, `ShowFileTagsSection` false.
- 업데이트 확인과 릴리스 노트 자동 열기 끔.
- 측정해서 항목별 효과를 기록한다.

### 2단계: 살아 있는 메모리 줄이기 (핵심)

- **비활성 탭 재우기**: 탭이 선택 해제되면 일정 시간 뒤 항목 목록·비트맵·폴더 감시자를 내려놓고 경로와 스크롤 위치만 남긴다. 다시 선택하면 재탐색한다. 탭과 듀얼 패인의 겉보기 동작은 유지한다.
- 레이아웃 페이지 캐시 축소 (`ItemDisplayFrame` CacheSize, `NavigationCacheMode`).
- 아이콘·썸네일 캐시에 상한(LRU)을 둔다.
- 썸네일을 항상 표시 크기로 디코딩한다. `FileImage`가 채워지면 `PreloadedIconData`를 비운다.
- 미리보기 패널: 선택이 바뀌거나 패널을 닫을 때 이전 미리보기 리소스가 해제되는지 확인하고 고친다.

### 3단계: 기능 제거

측정으로 효과가 확인된 것부터 한다. 한 기능이 한 브랜치다.

| 대상 | 범위 | 주의 |
|---|---|---|
| Git 통합 | `LibGit2Service`, `GitHelpers`, `Actions/Git`, Git 열·상태 표시줄, `GitItem` | 참조 범위가 넓다. 열거기와 `ListedItem`까지 닿는다. |
| Sentry | 패키지, `SentryLoggerProvider`, `ActiveSessionTracker`, 호출부 | |
| 자연어 검색 | `NaturalLanguageSearch.cs`와 호출부 | |
| 릴리스 노트 WebView2, 죽은 HTML/PDF 미리보기 | `ReleaseNotesPage`, `HtmlPreview`, `PDFPreview`, `WebViewEnvironment` | |
| FTP | `Ftp*` 저장소 항목, `FluentFTP` | |
| 압축 | `StorageArchiveService`, `Zip*`, `Actions/Content/Archives`, 7z DLL | zip을 폴더처럼 여는 기능과 압축 파일 미리보기도 함께 사라진다. 제거 전에 사용자에게 확인한다. |
| 홈 위젯, 상태 센터 속도 그래프(Win2D) | 위젯 뷰모델, `SpeedGraph` | |
| 코드·마크다운 미리보기 | ColorCode, MarkdownTextBlock | 미리보기 패널은 유지 대상이다. 제거 전에 사용자에게 확인한다. |

### 4단계: 런타임 조정

- `CommandManager`의 명령 객체를 필요할 때 만들도록 바꾼다.
- 서버 시작 시 미리 데우는 작업(컨텍스트 메뉴, 최근 항목, 점프 목록, 업데이트 확인 타이머)을 줄인다.
- GC: Satori GC와 표준 워크스테이션 GC + `GCConserveMemory`를 같은 시나리오로 비교해 낮은 쪽을 고른다.

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

upstream `ci.yml`은 포크에서 돌지 않는다. 모든 잡에 `github.repository_owner == 'files-community'` 조건이 있고 push 트리거가 `main`뿐이다. 그래서 `ci.yml`은 그대로 두고 패키징 단계만 옮긴 `.github/workflows/lite-package.yml`을 쓴다. `lite-main`과 `lite/**` 푸시마다 돌고(약 7분), 결과물 "Files Lite Package (Release, x64)"에 `.msixbundle`과 공개 인증서 `.cer`가 들어 있다.

1. 받기: `gh run download <run-id> --repo minsangOh/Files -D D:\lite-pkg\run-<run-id>`
2. 인증서 신뢰 (사용자가 관리자 PowerShell에서 직접 한다. 실행마다 새 인증서가 만들어지므로 매번 필요하다):
   `Import-Certificate -FilePath <...>\Files.App_<버전>_x64.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople`
3. 같은 신원(`FilesDev`, `CN=Files`)의 Debug 등록이 있으면 지운다: `Get-AppxPackage FilesDev | Remove-AppxPackage`. 앱 설정과 탭 기록도 지워진다.
4. 설치: `Add-AppxPackage -Path <...>\Files.App_<버전>_x64.msixbundle`. Windows App Runtime 2.6.5가 필요하다(이 PC에는 있다).

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

Debug에서 아직 재지 않은 것: S3 단독, S5. Release는 S1~S6 모두 쟀다.
