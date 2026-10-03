# 기존 저장소에 3.5.0 반영

GitHub 공개용 ZIP의 `MyDDayWidget-Source` 폴더 **안의 파일**을 기존 로컬 저장소에 복사합니다. 기존 Git 기록을 유지하고 새 저장소 생성 메뉴를 사용하지 않습니다.

## Visual Studio

1. 기존 로컬 저장소와 `DDay3.sln`을 엽니다. 작업 중인 변경은 먼저 보존합니다.
2. 공개용 파일을 덮어쓰고 아래 이전 파일 정리를 수행합니다. ZIP 자체를 저장소 안에 넣지 않습니다.
3. Git 변경 내용에서 수정/추가/삭제를 검토하고 `Release D-Day 3 3.5.0`으로 커밋합니다.
4. Fetch/Pull로 원격 변경을 확인하고 필요하면 충돌을 해결한 뒤 기존 `origin`으로 Push합니다.
5. 로컬에서 `build-release.bat`를 실행하고 생성한 ZIP의 실행·URL 구독·갱신·달력/D-Day를 확인합니다.
6. GitHub Releases에서 태그 **3.5.0**을 최신 커밋에 만들고 `RELEASE_NOTES.md`를 본문으로 사용합니다. 태그에 `v`를 붙이지 않습니다.
7. `artifacts/v3.5.0`의 `DDay3-v3.5.0-win-x64.zip`, `DDay3_SHA256.txt`, 필요하면 `DDay3.exe`를 첨부합니다. 실행 안내는 항상 전체 포터블 ZIP을 기준으로 합니다.
8. Release 게시와 자산 다운로드를 확인한 뒤 MathTime의 `dday.html`을 교체합니다. 페이지의 파일명과 Release 자산 이름을 정확히 맞춥니다.

## 이전 공개 파일 정리

덮어쓰기만 하면 이번 묶음에서 제외한 옛 파일이 남을 수 있습니다. 기존 로컬 저장소에 있는 아래 **추적 중인** 파일을 검토해 삭제도 커밋합니다.

- 루트의 옛 `GOOGLE_CALENDAR_SETUP.md`, `GOOGLE_OAUTH_RELEASE.md`: 보관 안내는 `Roadmap/GoogleOAuth/`로 이전했습니다.
- 옛 `web/` 또는 루트의 구버전 D-Day HTML이 저장소에 있다면 별도 웹사이트에서 관리할지 검토해 정리합니다. 이번 소스 묶음에는 웹페이지가 없습니다.
- 개인 일정·주소·INI·인증키·OAuth JSON·로그·캐시·bin/obj·압축 ZIP·내부 검증 메모가 이미 추적 중이면 공개 대상에서 제외합니다. `.gitignore`는 이미 추적한 파일을 제거하지 않습니다.

기존 폴더 전체나 Git 이력을 삭제하지 마세요. 인증 정보가 과거 커밋에 들어갔으면 현재 파일 삭제만으로 과거 내용이 사라지지 않습니다. 해당 비밀 정보의 폐기/재발급과 기록 정리를 별도로 처리해야 합니다.

## 원격 확인과 명령줄

아래 명령은 **기존 저장소 폴더**에서 실행합니다. 현재 브랜치와 원격을 먼저 확인하고 저장소 생성이나 강제 푸시를 하지 않습니다.

```cmd
git status
git branch --show-current
git remote -v
```

`origin`이 없다면 한 번만 추가합니다. 이미 있으면 현재 주소를 확인한 후 필요한 경우 `git remote set-url origin ...`으로 수정합니다.

```cmd
git remote add origin https://github.com/lottoria-dev/MyDDayWidget-Source.git
```

현재 브랜치가 `main`일 때 다음 순서로 반영합니다. 다른 브랜치라면 실제 브랜치 이름을 사용하세요.

```cmd
git add -A
git diff --cached --stat
git commit -m "Release D-Day 3 3.5.0"
git pull --rebase origin main
git push -u origin main
```

원격에서 해당 브랜치가 아직 없으면 Pull은 생략합니다. Rebase 충돌이 나면 해결하고 `git rebase --continue`를 완료한 뒤 Push합니다. 이미 게시한 3.5.0 태그를 바꾸지 않고 이후 소규모 개선은 3.5.1로 진행합니다.

URL 구독은 OAuth JSON·Google 인증 심사와 무관하게 동작합니다. 보류한 Google 코드는 기본 빌드에서 제외하며, 실제 구독 주소와 개인 자료를 Git에 넣지 않습니다.
