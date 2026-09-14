# 제3자 구성요소 안내 · D-Day 3 v3.1.4

이 프로젝트는 외부 애플리케이션 NuGet 패키지를 참조하지 않습니다. 배포 ZIP에는 자체 실행 파일·사용 안내·라이선스만 포함하며 .NET Framework 런타임이나 Qt/PySide6/Python 라이브러리는 포함하지 않습니다.

## 실행 환경

.NET Framework 4.8, WPF, Windows Forms, System.Drawing 등은 Windows에 설치된 Microsoft 구성요소를 참조합니다. 해당 Microsoft 사용 조건이 적용됩니다. 최신 .NET/WPF 저장소의 MIT 라이선스를 .NET Framework 4.8 전체의 사용 조건으로 대신하지 않습니다.

- 설치·배포 안내: https://learn.microsoft.com/en-us/dotnet/framework/deployment/deployment-guide-for-developers
- 공식 .NET Framework 다운로드: https://dotnet.microsoft.com/en-us/download/dotnet-framework

## 글꼴과 리소스

Tahoma·맑은 고딕 및 사용자가 선택한 시스템 글꼴은 설치된 이름을 참조하며 글꼴 파일은 포함하지 않습니다. 아이콘은 프로젝트의 기존 자체 리소스를 사용합니다.

## 선택적 외부 데이터

공휴일·24절기 옵션은 한국천문연구원이 공공데이터포털에서 제공하는 특일 정보 API를 조회합니다.

- 출처: https://www.data.go.kr/data/15012690/openapi.do
- 데이터 형식: XML. 공휴일 날짜·명칭·공공기관 휴일 여부 및 24절기 날짜·명칭 사용.
- 공식 페이지 확인일: 2026-09-14. 당시 무료·이용허락범위 제한 없음으로 안내되며 활용신청과 인증키가 필요함.
- 앱에는 인증키와 실제 공휴일·절기 데이터셋을 포함하지 않음. 사용자가 받은 자료는 개인 설정 폴더에 캐시함.
- 향후 API 이용 조건·서비스 가용성·데이터 갱신 여부는 제공기관 안내를 확인해야 함.

## 개발 검사 도구

`tests`는 선택적으로 설치한 .NET 8 SDK와 SDK에 들어 있는 Roslyn 참조를 사용합니다. 테스트 실행 파일·SDK·컴파일러는 정식 앱에 포함하지 않습니다. 개발 도구는 각 제공자의 조건을 따릅니다.

기존 v2의 Qt/PySide6/Python 관련 고지는 해당 v2 배포본에 계속 적용됩니다.
