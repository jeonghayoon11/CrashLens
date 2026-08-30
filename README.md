# CrashLens

> Windows 애플리케이션 충돌 로그를 빠르게 확인하는 데스크톱 분석 도구

## 다운로드

**[최신 버전 다운로드 — GitHub Releases](https://github.com/jeonghayoon11/CrashLens/releases)**

첫 릴리스를 준비 중입니다. 릴리스가 게시되면 위 페이지에서 `CrashLens.App.exe`를 내려받아 실행하세요. 일반 사용자는 Visual Studio나 .NET 런타임을 설치할 필요가 없습니다.

> Windows SmartScreen 경고가 표시될 수 있습니다. 코드 서명이 아직 적용되지 않은 초기 공개 버전에서는 `추가 정보` → `실행`을 선택해야 할 수 있습니다.

## CrashLens란?

CrashLens는 Windows 이벤트 뷰어를 직접 뒤지지 않고도 최근의 애플리케이션 충돌, 멈춤, Windows 오류 보고를 분석할 수 있도록 돕는 도구입니다. 개발자와 기술 지원 담당자가 문제 발생 시점과 오류 정보를 빠르게 확인하는 데 초점을 맞춥니다.

## 주요 기능

- Application 로그의 충돌 관련 이벤트 검색
  - 이벤트 ID 1000: Application Error
  - 이벤트 ID 1001: Windows Error Reporting
  - 이벤트 ID 1002: Application Hang
- 실행 파일별 충돌 목록과 상세 정보 표시
- 오류 모듈, 예외 코드, 프로세스 ID, 원본 이벤트 메시지 확인
- 대표 예외 코드 해석
  - `0xc0000005`: Access Violation
  - `0xc0000409`: Stack Buffer Overrun
  - `0xe0434352`: .NET Exception
- 원본 이벤트/XML/파싱 정보/보고서 미리보기 탭
- JSON, Markdown, 일반 텍스트 보고서 내보내기 구조
- 개인정보가 포함될 수 있는 사용자 경로 마스킹 구조

## 화면 구성

- 상단: 메뉴와 새로 고침, 기간, 검색, 필터, 내보내기 도구 모음
- 왼쪽: 최근 충돌 목록
- 가운데: 애플리케이션, 예외, 모듈, 추정 원인 등 구조화된 상세 정보
- 하단: 원본 이벤트, XML, 파싱 필드, 보고서 미리보기

## 개발 및 빌드

개발 환경에서는 Windows, .NET SDK 및 WinUI 3 개발 도구가 필요합니다.

```powershell
dotnet restore
dotnet build CrashLens.sln
dotnet run --project src/CrashLens.App
```

## 릴리스 만드는 방법

`main` 브랜치에서 버전 태그를 push하면 GitHub Actions가 Windows EXE를 만들고 GitHub Release에 자동 첨부합니다.

```powershell
git tag v0.1.0
git push origin v0.1.0
```

빌드가 성공하면 [Releases 페이지](https://github.com/jeonghayoon11/CrashLens/releases)에 새 버전과 `CrashLens.App.exe`가 표시됩니다.

## 프로젝트 구조

| 프로젝트 | 역할 |
|---|---|
| `CrashLens.App` | WinUI 3 화면, MVVM 뷰 모델, 명령, 샘플 데이터 |
| `CrashLens.Core` | 도메인 모델, 이벤트 파싱, 예외 코드 해석, 분석 로직 |
| `CrashLens.Infrastructure` | Windows 이벤트 로그 접근과 보고서 내보내기 |
| `CrashLens.Cli` | 최근 충돌 목록을 출력하는 명령줄 도구 |

## 현재 상태와 로드맵

현재는 MVP 단계입니다. UI, 샘플 데이터, 이벤트 로그 읽기 구조와 보고서 형식이 포함되어 있습니다.

- 파일 저장 대화 상자로 내보내기 연결
- 기간·검색·정렬·컨텍스트 메뉴 완성
- Windows 오류 보고 및 관련 이벤트 상관 분석
- 익명화된 이벤트 예제로 파서 테스트 추가
- 코드 서명 및 설치 프로그램 제공

## 라이선스

이 프로젝트는 [MIT License](LICENSE)로 배포됩니다.
