> 보류한 Google OAuth의 과거 설계 참고 문서입니다. 3.4.0 기본 앱에는 아래 화면·인증 기능이 없습니다. 현재 사용법은 루트의 `ICS_CALENDAR_SETUP.md`, 재개 범위는 `ROADMAP.md`를 확인하세요. 아래 내용은 현재 공개 버전의 사용 안내가 아닙니다.

# MathTime 공식 Google OAuth 검증 및 배포

2026-10-02 · D-Day 3 3.4.0 · 개발자용 안내

현재 개인 계정으로 확인한 연결은 기능 시험입니다. 일반 사용자가 JSON 없이 로그인하려면 **MathTime 운영 프로젝트의 데스크톱 OAuth 클라이언트**를 빌드에 포함하고 해당 프로젝트의 공개 서비스 요건을 완료해야 합니다. 소스에 공식 OAuth 정보는 들어 있지 않으며 이 문서 작성으로 Google 검증이 완료되는 것은 아닙니다.

## 1. 운영 프로젝트와 도메인

- Google Cloud Console에서 `mathtime.ai@gmail.com`으로 운영 프로젝트를 관리합니다. 기존 개인 DDay 시험 프로젝트와 별도로 운영용 프로젝트를 준비합니다. 개발·시험·운영 프로젝트 분리는 Google OAuth 정책 요구사항입니다.[1]
- 운영 계정에 필요한 프로젝트 Owner/Editor 권한을 부여하고 지원·개발자 연락처를 실제 수신 가능한 주소로 유지합니다. 앱 사용자가 이 계정으로 로그인하는 것은 아닙니다. 각 사용자는 자신의 Google 계정으로 로그인합니다.
- Google Search Console에서 `mathtime.kr` 소유권을 확인하고 그 소유권을 가진 계정을 운영 프로젝트와 연결합니다.[2]
- Google Calendar API를 사용 설정합니다. OAuth 유형은 **Desktop app**입니다. 웹 앱이나 서비스 계정 유형은 사용하지 않습니다. 데스크톱 앱의 임의 포트 `127.0.0.1` 콜백은 앱이 구성하므로 웹용 Redirect URI/JavaScript origin을 임의로 등록할 필요가 없습니다.[3]

## 2. 홈페이지 파일을 먼저 게시

웹 묶음의 세 파일을 기존 Firebase Hosting 공개 폴더에 반영합니다. `dday.html`은 기존 SPA 본문이며 `dday-privacy.html`, `dday-terms.html`은 로그인 없이 직접 읽을 수 있는 독립 페이지입니다.

| Branding 입력 | 값 |
| --- | --- |
| App name | D-Day 3 |
| User support email / Developer contact | mathtime.ai@gmail.com |
| Application home page | https://mathtime.kr/?page=dday |
| Privacy policy | https://mathtime.kr/dday-privacy.html |
| Terms of service | https://mathtime.kr/dday-terms.html |
| Authorized domain | mathtime.kr |
| Logo | 소스의 Assets/DDay3.png와 같은 앱 아이콘 |

시크릿 창에서 위 URL 세 개를 확인합니다. 개인정보/이용조건 URL이 SPA 첫 화면으로 대체되거나 404가 나오면 실제 정적 파일 위치와 Hosting 설정을 먼저 고칩니다. 홈페이지에는 앱 기능과 개인정보·이용조건 링크가 있어야 합니다.[2]

Release를 아직 게시하지 않았다면 새 다운로드 링크는 자산 게시 전까지 사용할 수 없습니다. 심사 기간에는 홈페이지 기능 설명과 개인정보/이용조건을 먼저 공개하고, 다운로드 영역은 실제 사용 가능한 Release를 가리키도록 유지합니다. 검증을 위해 비공개 시험 빌드나 GitHub Draft Release를 준비할 수 있습니다.

## 3. Audience와 Data Access

외부의 일반 Google 계정 사용자에게 제공하므로 Audience는 **External**로 구성합니다. 개발 시험 중에는 Testing과 테스트 사용자 목록을 사용하고, 일반 공개 전에는 Production/In production 게시 상태로 전환합니다. 게시 상태 변경과 Google의 검증 승인은 별개입니다.

Data Access에는 아래 두 범위만 추가합니다. Gmail, Drive, 캘린더 수정 범위나 별도 이메일/프로필 범위는 현재 앱에 필요하지 않습니다. Calendar 읽기 권한을 사용하는 운영 앱은 Console에 표시되는 민감 범위 검증을 완료해야 합니다.[4][5]

| Scope | 제출용 기능 설명 |
| --- | --- |
| `https://www.googleapis.com/auth/calendar.calendarlist.readonly` | The user chooses which of their subscribed and shared calendars to display. D-Day 3 reads calendar IDs and display names from CalendarList; it does not create, edit, or delete calendars. |
| `https://www.googleapis.com/auth/calendar.events.readonly` | D-Day 3 displays event titles and dates from the calendars selected by the user. The user may copy chosen events into local D-Day entries. Only event IDs, titles, status, and start/end dates are requested; Google events are never written, edited, or deleted. |

이 설명은 프로그램의 실제 조회와 일치합니다. 공유받은 캘린더도 읽어야 하므로 사용자 소유 일정만 읽는 더 좁은 범위로 같은 기능을 모두 구현할 수 없습니다. `calendar.readonly` 전체 범위 대신 목록과 일정 읽기 범위를 나누어 사용합니다. Console이 요구하는 각 범위의 필요성 질문에 위 목적과 달력 선택·일정 가져오기 기능을 설명하세요.

## 4. 운영 클라이언트로 심사용 빌드

Clients → Create client → Desktop app에서 운영 클라이언트를 만들고 JSON을 저장소 밖에 보관합니다. Visual Studio Developer Command Prompt에서 소스 루트로 이동한 뒤 실행합니다.

```cmd
build-release.bat "D:\OAuth-private\DDay3-production-desktop.json"
```

스크립트는 앱 식별 정보만 정리해 로컬 `Build\GoogleOAuthClient.json`으로 복사합니다. 다음 빌드에서 이 파일을 EXE 리소스로 포함하고, 확인 후 `artifacts\v3.4.0`에 EXE·포터블 ZIP·체크섬을 만듭니다. 개인 사용자의 액세스/갱신 토큰과 캘린더 선택은 포함하지 않습니다. 공식 리소스가 없는 EXE는 정식 ZIP 패키징에서 거절됩니다. VS에서 빌드할 때도 먼저 아래 명령으로 구성한 후 Release/x64로 **다시 빌드**하세요.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\configure-google-oauth.ps1 -ClientJson "D:\OAuth-private\DDay3-production-desktop.json"
```

원본 JSON과 Build 파일은 Git에 커밋하지 않습니다. 데스크톱 앱에 포함된 OAuth client secret은 서버 비밀키처럼 숨겨 보장할 수 없습니다. 이 앱은 PKCE와 임의 state, 로컬 콜백을 사용하며 사용자의 토큰은 별도로 Windows DPAPI로 저장합니다.[3] 공개 소스를 재빌드하는 사용자는 고급 연결에서 자신의 데스크톱 OAuth 프로젝트를 사용할 수 있습니다.

공식 정보가 없는 소스도 일반 VS 빌드는 가능합니다. 기존 개인 연결은 유지되며 기본 로그인 버튼은 비활성화되고 고급 JSON 연결을 사용할 수 있습니다. 로컬 시계와 직접 입력한 D-Day는 계속 동작합니다.

## 5. Branding과 데이터 접근 검증 제출

1. Branding 정보를 저장하고 **Verify Branding**을 실행합니다.
2. 결과가 Ready to publish가 되면 **Publish branding**으로 게시합니다. Branding과 Data access 검증은 다른 절차입니다.[2]
3. **Verification Center**에서 데이터 접근 검증을 준비하고 요청된 범위 설명과 시연 동영상을 제출합니다. 메뉴 이름은 Console 표시를 따르세요.
4. 홈페이지·개인정보·이용조건 URL과 앱 이름, 클라이언트 ID, 실제 코드의 두 범위가 모두 일치하는지 확인합니다.
5. 지원/개발자 연락처 메일과 Verification Center의 보완 요청에 응답합니다. 제출이나 Production 전환만으로 승인되지 않습니다. 일반 공개는 필요한 검증 승인과 실제 비테스트 계정 로그인 점검 후 진행합니다.

## 6. 심사용 동영상 구성

YouTube에 비공개가 아닌 **일부 공개(Unlisted)** 영상으로 올립니다. 승인 화면을 포함한 흐름을 영어로 보여주고 주소 표시줄을 읽을 수 있게 촬영합니다. 별도 시험 캘린더와 가상의 일정 제목을 사용하세요. 실제 토큰·client secret·개인 일정은 촬영하지 않습니다.[4]

1. 공식 홈페이지의 앱 기능과 개인정보/이용조건 링크를 보여줍니다.
2. 새 Windows 사용자 또는 기존 연결을 해제한 상태의 Release 앱을 실행합니다.
3. Settings → Calendar integration → Google calendar connection → Sign in with Google에 해당하는 실제 버튼 흐름을 보여줍니다. 앱 UI는 현재 한국어이므로 영어 자막으로 메뉴를 설명합니다.
4. 브라우저에서 계정을 선택하고 두 읽기 권한을 허용합니다. OAuth 요청 URL의 client_id가 제출 클라이언트와 같고 요청 범위가 두 개임을 보여줍니다.
5. 자신의 기본 캘린더와 공유/구독 캘린더를 선택하여 저장합니다. 이 화면은 CalendarList 읽기 권한의 용도입니다.
6. 달력 날짜를 눌러 같은 날의 여러 일정 중 일부를 D-Day로 가져옵니다. Google 원본이 바뀌지 않는 것을 보여줍니다. 이 화면은 Events 읽기 권한의 용도입니다.
7. 개인정보 버튼과 연결 해제·Google 권한 철회, 가져온 D-Day가 별도 로컬 복사본임을 보여줍니다.

## 7. 승인 후 GitHub 공개

- 운영 클라이언트와 동일한 설정으로 최종 Release/x64를 빌드합니다. Windows에서 hover 테두리와 로그인/취소/권한 거부/철회/재연결을 점검합니다.
- GitHub 태그는 **3.4.0**입니다. `RELEASE_NOTES.md`를 본문으로 쓰고 `DDay3.exe`, `DDay3-v3.4.0-win-x64.zip`, `DDay3_SHA256.txt`를 첨부합니다. Debug/PDB/사용자 INI/로그/토큰/원본 OAuth JSON은 첨부하지 않습니다.
- 새 Release 자산을 게시한 뒤 홈페이지의 3.4.0 다운로드 링크를 반영합니다. 개인정보·이용조건 페이지는 심사와 배포 이후에도 계속 공개합니다.
- OAuth 프로젝트의 공개 여부를 확인하고 새 계정으로 기본 버튼에서 JSON 없이 로그인되는지 점검합니다. Workspace 계정은 조직 관리자의 앱 접근 정책에 따라 제한될 수 있습니다.
- 앱 검증은 Google 서비스 사용 검증이며 EXE 코드 서명이나 SmartScreen 평판을 대신하지 않습니다. 승인되지 않은 상태를 ‘Google 인증 완료’로 표시하지 마세요.

## 공식 자료

1. [OAuth 2.0 Policies](https://developers.google.com/identity/protocols/oauth2/policies)
2. [Submit for brand verification](https://developers.google.com/identity/protocols/oauth2/production-readiness/brand-verification)
3. [OAuth for native apps](https://developers.google.com/identity/protocols/oauth2/native-app)
4. [Sensitive scope verification](https://developers.google.com/identity/protocols/oauth2/production-readiness/sensitive-scope-verification)
5. [Calendar API scopes](https://developers.google.com/workspace/calendar/api/auth)
6. [Google API Services User Data Policy](https://developers.google.com/terms/api-services-user-data-policy)

검증 상태와 Console 안내가 최종 기준입니다. 심사 완료 날짜나 승인 여부는 이 문서에서 보장하지 않습니다.
