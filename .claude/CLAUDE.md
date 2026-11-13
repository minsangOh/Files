# Files Lite 포크 작업 지침

이 저장소는 files-community/Files의 개인 포크(`minsangOh/Files`)다. 목표는 RAM 8GB 사무용 노트북에서 부담 없이 돌아가도록 메모리 점유를 줄이는 것이다.

루트 `CLAUDE.md`는 upstream의 심볼릭 링크라 Windows에서는 "AGENTS.md"라는 글자만 읽힌다. upstream 개발 규칙은 아래 import로 불러온다.

@../AGENTS.md

## 먼저 읽을 것

- 기획·단계·측정 방법: `docs/lite/PLAN.md`
- 작업을 시작하기 전에 PLAN.md의 "현재 상태"와 해당 단계 항목을 확인한다.

## 포크 전용 규칙

- 사용자에게는 한국어로 답한다. 코드 주석과 커밋 메시지는 upstream과 같이 영어로 쓴다.
- `main`은 upstream 미러다. 직접 커밋하지 않는다. 제품 브랜치는 `lite-main`이고 작업 브랜치는 `lite/<주제>`로 만든다.
- upstream 병합 충돌을 줄이는 쪽을 택한다. 순서는 기본값 변경 → 지연 로딩·해제 → 코드 삭제다. 삭제는 측정으로 효과가 확인된 것만 한다.
- 유지 기능은 탭, 듀얼 패인, 미리보기(정보) 패널이다. 이 셋의 동작을 깨는 변경은 하지 않는다.
- 메모리 관련 변경은 전후 수치를 `tools/lite/Measure-Memory.ps1`로 재고 PLAN.md의 측정 기록 표에 남긴다. 측정하지 않은 개선은 개선이라고 쓰지 않는다.
- 이 PC는 RAM 7.6GB다. 빌드 명령은 하나씩만 실행하고(`-m:1` 권장), Release(Native AOT) 패키징은 로컬이 아니라 포크의 GitHub Actions에서 돌린다.
- upstream 저장소로 PR·이슈를 만들지 않는다. `gh` 명령에는 항상 `--repo minsangOh/Files`를 붙인다.
