# 제3자 구성요소 안내 · D-Day 3 3.5.1

자체 소스에는 `LICENSE.md`가 적용됩니다. 아래 구성요소에는 각 원 라이선스가 적용되며 자체 소스의 재배포 제한으로 그 권리를 제한하지 않습니다.

| 구성요소 | 고정 버전 | 용도 | 라이선스·동봉 파일 |
| --- | --- | --- | --- |
| Ical.Net | 4.3.1 | ICS 해석·반복 일정·시간대 처리 | MIT · `licenses/Ical.Net-MIT.txt` |
| NodaTime | 3.2.0 | Ical.Net의 시간대 계산 | Apache-2.0 · `licenses/NodaTime-APACHE-2.0.txt`, `licenses/NodaTime-NOTICE.txt` |
| System.Runtime.CompilerServices.Unsafe | 6.0.0 | NodaTime의 런타임 의존성 | MIT · `licenses/Microsoft-MIT.txt` |

첫 빌드에서 NuGet으로 복원하며 실행 시 세 DLL을 EXE와 같은 폴더에 둡니다. 배포 도구는 DLL 및 `licenses` 폴더를 함께 복사합니다. 제3자 소스를 수정하지 않았습니다.

- Ical.Net: https://www.nuget.org/packages/Ical.Net/4.3.1 · https://github.com/ical-org/ical.net
- NodaTime: https://www.nuget.org/packages/NodaTime/3.2.0 · https://github.com/nodatime/nodatime
- Unsafe: https://www.nuget.org/packages/System.Runtime.CompilerServices.Unsafe/6.0.0

## Windows 실행 환경

.NET Framework 4.8, WPF, Windows Forms, System.Drawing 등은 Windows에 설치된 Microsoft 구성요소를 참조합니다. 해당 Microsoft 사용 조건이 적용됩니다. 최신 .NET/WPF 저장소의 MIT 라이선스를 .NET Framework 4.8 전체의 사용 조건으로 대신하지 않습니다. .NET Framework 런타임, Qt/PySide6/Python은 이 소스 묶음에 포함하지 않습니다.

- .NET Framework 설치·배포 안내: https://learn.microsoft.com/en-us/dotnet/framework/deployment/deployment-guide-for-developers

Consolas·Tahoma·맑은 고딕 및 사용자가 선택한 시스템 글꼴은 설치된 이름을 참조하며 글꼴 파일을 포함하지 않습니다. 아이콘은 기존 프로젝트 자체 리소스를 사용합니다.

## 선택적 외부 데이터

한국 공휴일·24절기는 한국천문연구원의 공공데이터포털 특일 정보 API를 사용합니다. 인증키와 실제 데이터셋은 소스에 포함하지 않습니다. 이용 조건과 서비스 가용성은 제공기관 안내를 확인하세요.

- 출처: https://www.data.go.kr/data/15012690/openapi.do

사용자가 가져오는 ICS와 iCal URL은 해당 캘린더의 내보내기/공유 권한 및 이용 조건에 따릅니다. URL 요청은 .NET Framework의 System.Net.Http를 사용하며 새 NuGet 의존성을 추가하지 않습니다. 실제 개인 일정 파일은 배포하지 않습니다. Google API/OAuth SDK는 참조하지 않으며 OAuth 자체 구현은 기본 빌드에서 제외한 로드맵 코드입니다. 과거 v2의 Qt/PySide6/Python 고지는 해당 v2 배포본에 계속 적용됩니다.

## 개발 검사

코어 검사는 선택적으로 설치한 .NET 8 SDK와 그 SDK의 Roslyn 참조를 사용합니다. Windows 검사는 .NET Framework 4.8을 사용하며 ICS 검사 프로젝트는 앱과 같은 NuGet 버전을 복원합니다. 검사 실행 파일·SDK·컴파일러는 소스 ZIP에 넣지 않습니다.
