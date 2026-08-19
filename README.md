<p align="center">

[![GitHub Release](https://img.shields.io/github/v/release/karpitony/eft-where-am-i?include_prereleases&logo=github)](https://github.com/karpitony/eft-where-am-i/releases/latest)
[![GitHub total downloads](https://img.shields.io/github/downloads/karpitony/eft-where-am-i/total.svg?include_prerelease&logo=github)](https://github.com/karpitony/eft-where-am-i/releases)
[![GitHub License](https://img.shields.io/github/license/karpitony/eft-where-am-i)](./LICENSE)
[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-purple?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/10.0)

</p>

> **[English](README_en.md)** | **한국어 (현재)**

# EFT: Where Am I

타르코프 보조 프로그램 Where Am I 입니다.

게임 내 위치 확인, 맵 자동 전환 등 다양한 편의 기능을 제공합니다.
TarkovHelper의 기능들도 일부 구현되어 있습니다.

> [!WARNING]
> 본 프로그램은 [MIT 라이선스](./LICENSE)로 배포되며, **본 프로그램 사용으로 인한 어떠한 피해(BSG에 의한 제재 등)에 대해서도 책임지지 않습니다.**
> 본 프로그램은 [tarkov-market.com](https://tarkov-market.com/)과 공식적인 관계가 없으며, 맵 데이터와 UI를 활용하고 있습니다.

## 목차

- [주요 기능](#주요-기능)
- [시스템 요구 사항](#시스템-요구-사항)
- [다운로드 및 설치](#다운로드-및-설치)
- [사용 방법](#사용-방법)
- [설정](#설정)
- [기여하기](#기여하기)
- [버그 신고 및 건의](#버그-신고-및-건의)
- [크레딧 및 감사](#크레딧-및-감사)
- [라이선스](#라이선스)

## 주요 기능

### 레이드 시작시 자동으로 해당 맵 열기

레이드 시작을 자동으로 감지하여 해당 맵으로 변경합니다.
이 옵션은 로그폴더를 지정해야만 사용이 가능합니다.
혹, 자동로그폴더 지정이 불가하다면 게임을 껏다 켜보시길 부탁드립니다.

### 자동패닝

위치 마커가 뷰포트의 가장자리(데드존)에 도달하면 맵을 자동으로 이동시켜 마커가 항상 화면 안에 보이도록 합니다. 데드존 비율은 설정에서 조절할 수 있습니다.
50%부터 99%까지 설정 가능하고 50%로 갈수록 중앙에 가깝게 99%에 가까울수록 가장자리에 가깝게 패닝됩니다.

#### 50%일때

<img src="assets/panning_50percent.gif" width="90%">

#### 99%일때

<img src="assets/panning_99percent.gif" width="90%">

### 게임내에서 Ctrl + NumPad의 조합으로 층 이동

게임내에서 Ctrl + NumPad를 이용하여 바로 층 이동이 가능하도록 만들었습니다.
이 기능은 타르코프 헬퍼를 참조하였습니다.
게임이 활성화중일때만 사용가능합니다.

### 퀘스트 자동저장・자동 불러오기

퀘스트를 우클릭해서 지정하면 자동으로 저장됩니다. 다시 우클릭해서 해제하면 저장삭제됩니다.
맵을 바꿨다가 다시 그 맵으로 돌아오면 자동으로 불러오기합니다.
타르코프마켓의 페이지를 그대로 사용하는지라 무료는 3개까지, 유료는 무제한으로 저장가능합니다.

### 패널의 숨김상태를 저장・불러오기

타르코프마켓의 패널의 상태를 저장합니다. 맵을 바꿨다가 다시 그 맵으로 돌아오면 저장되었던 상태로 변경합니다.

    아직 패널 안의 내용(보스, 저격스캐브, 컬티 등)은 저장되지 않습니다. - 추후 업뎃 예정

### 스크린샷 자동삭제

레이드 종료시 자동인식하여 이번 레이드에서 생긴 스크린샷만 휴지통으로 보냅니다.
이전에 찍어둔 스크린샷은 건드리지 않으며, 잘못 지워졌더라도 휴지통에서 되돌릴 수 있습니다.

### 자동 촬영 (주기적으로 스크린샷 찍기)

패널의 **자동 촬영**을 켜면 타르코프가 활성 상태일 때 정해진 간격(3~30초)마다
스크린샷 키를 대신 눌러줍니다. 직접 누르지 않아도 위치가 계속 갱신됩니다.

- 스크린샷 키는 설정 화면에서 바꿀 수 있습니다. (기본값 `Print Screen`)
- 타르코프 창이 활성 상태가 아니면 키를 보내지 않습니다. 다른 프로그램에 입력이 들어가지 않습니다.
- 자동 촬영을 켜면 스크린샷 감지도 함께 켜집니다. (찍기만 하고 읽지 않으면 의미가 없으므로)

> [!WARNING]
> 게임에 입력을 주입하는 기능입니다. BSG 정책상 자동 입력이 문제가 될 소지가 있으므로
> 기본값은 꺼짐이며, 사용 여부는 본인 판단에 따라 결정해 주세요.
> 게임이 주입된 입력을 걸러내는 경우 동작하지 않을 수 있습니다.

### 모바일 레이더 (휴대폰으로 맵 보기)

같은 네트워크의 휴대폰에서 PC의 맵 화면을 그대로 볼 수 있습니다.
듀얼 모니터 대신 폰을 옆에 두고 레이더처럼 쓰는 용도입니다.

1. 패널의 **폰으로 보기**를 켭니다.
2. 패널에 나타난 주소(`http://192.168.x.x:8787/코드/`)를 휴대폰 브라우저에 입력합니다.
   - `복사` 버튼으로 주소를 복사할 수 있고, 설정 화면에서도 확인할 수 있습니다.
3. 한 손가락으로 이동, 두 손가락으로 확대·축소, 화면을 한 번 누르면 UI가 숨겨집니다.
   - `화면 유지`를 켜면 폰 화면이 꺼지지 않습니다.
   - 갱신 주기는 1초 / 2초 / 5초 중에 고를 수 있습니다.

- 처음 켤 때 **Windows 방화벽**이 허용을 물어봅니다. 허용해야 폰에서 접속됩니다.
- 폰이 실제로 보고 있을 때만 화면을 캡처하므로, 안 볼 때는 PC 자원을 쓰지 않습니다.
- 주소 뒤의 접근 코드는 같은 네트워크의 다른 사람이 무심코 들여다보는 것을 막아줍니다.
  코드는 설정 화면에서 다시 발급할 수 있습니다.
- 포트가 겹치면 설정 화면에서 바꿀 수 있습니다. (기본값 `8787`)

## 시스템 요구 사항

| 항목 | 요구 사항 |
|------|-----------|
| OS | Windows 10 1809+ / Windows 11 |
| 런타임 | [.NET 10.0 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0) |
| 브라우저 | [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (대부분 사전 설치됨) |
| 네트워크 | 인터넷 연결 필수 (tarkov-market.com 접속) |
| 디스크 | ~50 MB |

## 다운로드 및 설치

### 다운로드

![github-release-screenshot.png](assets/github-release-screenshot.png)

> **💡 권장 다운로드: `*win-sc-Portable.zip`**
> 백신 오탐(False Positive)을 피하고 별도의 .NET 런타임 설치 없이 바로 실행하려면 **`win-sc-Portable.zip`** 버전을 다운로드하는 것을 강력히 권장합니다.

[Releases](https://github.com/karpitony/eft-where-am-i/releases/latest)에서 최신 버전의 파일을 다운로드하세요.

1. `.zip` 파일을 다운로드합니다.
2. 원하는 위치에 압축을 해제합니다.
3. `eft-where-am-i.exe`를 클릭하여 실행합니다.

<img src="assets/eft-wmi-exe.png" width="700">

### ⚠️ 실행 오류 해결 (.NET 런타임 설치)

프로그램을 실행했을 때 아무 반응이 없거나 시스템 오류가 발생한다면, **.NET 10.0 Desktop Runtime**이 설치되어 있지 않기 때문일 수 있습니다. 다음 안내에 따라 런타임을 설치해 주세요.

1. [.NET 10.0 다운로드 페이지](https://dotnet.microsoft.com/download/dotnet/10.0)로 이동합니다.
2. 화면에서 `앱 실행 - 런타임` 분류 안의 **`.NET 데스크톱 런타임 (Desktop Runtime) 10.0.x`** 항목을 찾습니다.
3. 운영체제 목록의 **`Windows`** 줄에서 설치 관리자 **`x64`**를 클릭하여 다운로드한 뒤 설치합니다.

> 💡 **주의사항** 
> - 페이지 내의 다른 런타임(SDK, ASP.NET Core 등)이나 단순 `.NET 런타임`이 아닙니다. 이 앱은 화면(UI)이 있는 프로그램이므로 꼭 **데스크톱 런타임(Desktop Runtime)**의 **x64** 버전을 다운로드하셔야 합니다.

### 첫 실행

첫 실행 시 EFT 스크린샷 폴더와 로그 폴더 경로를 자동으로 감지합니다. 경로가 올바르지 않은 경우 설정에서 수동으로 변경하거나 `Auto Find` 버튼을 사용하세요.

## 사용 방법

### 기본 사용법

**1. 맵을 선택합니다.**

좌측 상단에서 맵을 선택합니다. 자동 맵 감지를 활성화하면 레이드 진입 시 자동으로 전환됩니다.

<img src="assets/screenshot01.png" width="700">

## 설정

<img src="assets/screenshot02.png" width="700">

| 설정 항목 | 설명 |
|-----------|------|
| 언어 | 한국어 / English |
| 스크린샷 경로 | EFT 스크린샷 폴더 경로 (`Change` / `Auto Find`) |
| 로그 경로 | EFT 게임 로그 폴더 경로 |
| 자동 스크린샷 감지 | 스크린샷 촬영 시 자동 위치 갱신 |
| 자동 맵 감지 | 레이드 진입 시 자동 맵 전환 |
| 자동 패닝 | 데드존 기반 자동 맵 이동 |
| 층 구역 편집기 | 다층 맵의 층 구역 편집 |

### 설정 파일 위치

설정은 `%APPDATA%\eft-where-am-i\settings.json` 에 저장됩니다.

설치 폴더가 아니라 사용자 프로필에 두기 때문에 **업데이트를 해도 설정이 유지**됩니다.
구버전(설치 폴더의 `assets\settings.json`)에서 올라오는 경우 처음 실행할 때 자동으로 옮겨옵니다.

로그는 실행 파일과 같은 폴더의 `app.log` 에 쌓입니다. 문제 제보 시 첨부해 주시면 원인 파악이 훨씬 빠릅니다.

### 사이트 구조가 바뀌어 기능이 멈췄을 때

이 프로그램은 tarkov-market.com 의 버튼과 입력창을 CSS 셀렉터로 찾아 조작합니다.
사이트 마크업이 바뀌면 해당 기능이 멈추는데, 새 버전을 기다리지 않고 직접 고칠 수 있습니다.

`%APPDATA%\eft-where-am-i\selectors.json` 을 만들고 바뀐 셀렉터만 적어주세요. 앱과 함께 배포되는
`assets\selectors.json` 보다 우선 적용됩니다.

```json
{
  "hide_show_panel_button": "...",
  "full_screen_button": "...",
  "where_am_i_button": "...",
  "location_input": "..."
}
```

## 기여하기

PR과 Issue 기여를 환영합니다!

1. 이 저장소를 Fork합니다.
2. 기능 브랜치를 생성합니다. (`git checkout -b feature/my-feature`)
3. 변경 사항을 커밋합니다.
4. Pull Request를 생성합니다.

`floor_db.json`에 새로운 맵의 층 데이터를 추가하는 기여도 매우 환영합니다.

## 버그 신고 및 건의

버그 신고, 기능 건의 등은 [Issues](https://github.com/karpitony/eft-where-am-i/issues)에서 남겨주세요.

## 크레딧 및 감사

- [Tarkov-Market](https://tarkov-market.com/) — 맵 데이터 및 UI
- [Tarkov-Client](https://github.com/byeong1/Tarkov-Client) by byeong1 — 방향 표시기 코드 (MIT License)
- [Freepik - Flaticon](https://www.flaticon.com/free-icons/map) — 맵 아이콘, [서버 아이콘](https://www.flaticon.com/free-icon/server_4227991?term=server&page=1&position=8&origin=search&related_id=4227991)
- [Microsoft WebView2](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) — 브라우저 컴포넌트

## 라이선스

MIT License — Copyright (c) 2024 karpitony, Copyright (c) 2026 supnoel

자세한 내용은 [LICENSE](./LICENSE) 파일을 참조하세요.

> [!CAUTION]
> 본 프로그램은 BSG/Battlestate Games와 무관하며, 사용으로 인한 게임 내 제재 등 어떠한 불이익에 대해서도 개발자는 책임지지 않습니다. 사용에 따른 위험은 본인이 감수해야 합니다.
