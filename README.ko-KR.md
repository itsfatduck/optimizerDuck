<div align="center">

<a href="https://optimizerduck.vercel.app/"><img src="./.github/assets/optimizerDuck.png" alt="optimizerDuck 배너" title="optimizerDuck"/></a>

# [optimizerDuck](https://optimizerduck.vercel.app/)

**optimizerDuck는 성능, 개인정보 보호, 단순성에 중점을 둔 무료 오픈소스 Windows 최적화 도구입니다.**

[![Release](https://img.shields.io/github/release/itsfatduck/optimizerDuck?color=fed114&label=Release&style=flat-square)](https://github.com/itsfatduck/optimizerDuck/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/itsfatduck/optimizerDuck/total?label=Downloads&style=flat-square&color=lightgreen)](https://github.com/itsfatduck/optimizerDuck/releases)
[![Stars](https://img.shields.io/endpoint?url=https://api.pinstudios.net/api/badges/stars/itsfatduck/optimizerDuck&style=flat-square)](https://github.com/itsfatduck/optimizerDuck/stargazers)
[![License](https://img.shields.io/badge/License-GPL_v3-red?style=flat-square)](./LICENSE)
[![Discord](https://img.shields.io/discord/1091675679994675240?color=5865f2&label=Discord&style=flat-square)](https://discord.gg/tDUBDCYw9Q)
<br>
[![CI](https://github.com/itsfatduck/optimizerDuck/actions/workflows/ci.yml/badge.svg)](https://github.com/itsfatduck/optimizerDuck/actions/workflows/ci.yml)
[![.NET Latest](https://img.shields.io/badge/.NET_Runtime-Latest-ef99dd?style=flat-square)](https://dotnet.microsoft.com/en-us/download)
[![Supported OS](https://img.shields.io/badge/Supported-Windows_10%2B_x64-0078d4?style=flat-square)](https://www.microsoft.com/en-us/software-download/)

**[시작하기](https://optimizerduck.vercel.app/docs/guides/getting-started) | [작동 원리](https://optimizerduck.vercel.app/docs/guides/how-it-works) | [FAQ](https://optimizerduck.vercel.app/docs/faq/general)**

**[English](README.md) | [Tiếng Việt](README.vi.md) | [繁體中文](README.zh-TW.md) | [简体中文](README.zh-CN.md) | [Русский](README.ru-RU.md) | [Français](README.fr-FR.md) | [한글](README.ko-KR.md)**

<details>
<summary>⭐ 스타 동향</summary>

optimizerDuck이 PC 성능을 개선하는 데 도움이 되었다면, 이 저장소에 스타를 눌러 다른 사람들과 공유해 주세요.
여러분이 눌러주신 스타는 향후 프로젝트를 개선해 나가는 데 큰 원동력이 됩니다.

<a href="https://www.star-history.com/#itsfatduck/optimizerDuck&legend=top-left">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/svg?repos=itsfatduck/optimizerDuck&theme=dark&legend=top-left" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/svg?repos=itsfatduck/optimizerDuck&legend=top-left" />
   <img alt="Star History Chart" src="https://api.star-history.com/svg?repos=itsfatduck/optimizerDuck&legend=top-left" />
 </picture>
</a>

</details>

<img src="./.github/assets/app.png" alt="optimizerDuck 다크 모드" title="optimizerDuck 다크 모드" width="800"/>

</div>

---

## 빠른 시작

1. **[GitHub Releases](https://github.com/itsfatduck/optimizerDuck/releases/latest)**에서 다운로드
2. `.exe` 파일을 직접 실행합니다. 설치가 필요 없습니다.
3. 원하는 최적화를 선택하고 적용한 다음 PC를 다시 시작합니다.

> [!TIP]
> 변경하기 전에 항상 **시스템 복원 지점**을 만드세요.

> [!NOTE]
> 영어, 베트남어, 중국어 번체([@abc0922001](https://github.com/abc0922001) 기여), 중국어 간체([@wcxu21](https://github.com/wcxu21) 기여), 러시아어([@Foodhead](https://github.com/Foodhead) 기여), 프랑스어([@Robocnop](https://github.com/Robocnop) 기여), 한국어([@klfnn](https://github.com/klfnn) 기여)로 사용 가능합니다.
> 새로운 언어 번역에 기여하고 싶으신가요? [CONTRIBUTING.md](./CONTRIBUTING.md)를 참고해 주세요.

---

## optimizerDuck이 하는 일

Windows에는 백그라운드 서비스, 원격 분석, 사전 설치된 앱, 시작 프로그램, 불필요하게 리소스를 낭비하는 예약된 작업 등 사용자에게 굳이 필요 없는 기능들이 많이 포함되어 있습니다. optimizerDuck은 이 모든 것을 한 번에 정리할 수 있는 통합 인터페이스를 제공합니다.

이 도구는 시스템 오버헤드를 줄이고 원치 않는 동작을 차단하기 위해 시스템 맞춤형 조정을 적용합니다. 이와 더불어 현재 실행 중인 항목을 확인하고, 불필요한 항목을 제거하며, 문제가 발생했을 때 변경 사항을 쉽게 되돌릴 수 있는 다양한 관리 도구도 함께 제공합니다.

> [!NOTE]
> 이 도구에서 제공하는 모든 최적화는 수동으로도 적용 가능합니다. optimizerDuck은 단지 번거로운 과정 없이 이러한 최적화를 더욱 쉽고 안전하게 적용할 수 있도록 도와줄 뿐입니다.

### 시스템 최적화

6개 범주에 걸친 30개 이상의 조정으로, 각각에는 각 변경이 무엇을 하는지 정확히 알 수 있도록 명확한 설명과 위험 등급이 있습니다.

| 범주                         | 포함 내용                                                                                                   |
| :--------------------------- | :---------------------------------------------------------------------------------------------------------- |
| **성능**                     | RAM 기반 서비스 호스트 튜닝, 프로세스 우선순위 조정, 키보드 지연 감소, 게임을 위한 멀티미디어 스케줄러 조정 |
| **개인정보 보호**            | Windows 원격 분석, 오류 보고, 광고 ID, 위치 추적, Cortana, Copilot, 콘텐츠 배달 제안 비활성화               |
| **GPU**                      | AMD, NVIDIA, Intel GPU용 공급업체별 레지스트리 조정, 전원 상태, 클록 게이팅, 디스플레이 지연 커버리지       |
| **전원**                     | 절전 모드 및 빠른 시작 비활성화, USB 선택적 일시 중단 해제, 고성능 전원 계획 설치, 전원 절약 비활성화       |
| **사전 설치된 앱 및 서비스** | OEM 앱 재설치 동작 차단 및 200개 이상의 Windows 서비스에 대한 시작 유형 미세 조정                           |
| **사용자 경험**              | 메뉴 표시 지연 제거, 작업표시줄 애니메이션 및 투명도 같은 시각 효과 비활성화로 더 빠른 반응성 제공          |

> [!IMPORTANT]
> 최적화가 많지 않다고 생각하더라도 그것이 비효과적이거나 구식이라고 가정하지 마세요. optimizerDuck은 커뮤니티에서 테스트, 벤치마크 또는 널리 신뢰하는 최적화만 중점을 두고 있습니다. 일부 변경 사항은 즉시 눈에 띄는 차이를 만들지 못할 수도 있지만, 시스템이 시간이 지남에 따라 더 부드럽고 안정적으로 실행되는 데 여전히 도움이 될 수 있습니다.

### 기능 토글

레지스트리를 뒤적거리거나 온라인 가이드를 검색할 필요 없이, 간단하게 Windows 설정을 켜거나 끌 수 있습니다. 4개 섹션으로 정리되어 있습니다:

- **바탕화면**: 아이콘 표시 또는 숨김(내 PC, 휴지통, 네트워크, 사용자 파일, 제어판), 바로가기 화살표 오버레이 제거
- **작업표시줄**: 가운데 또는 왼쪽 정렬, 위젯 토글, 작업 보기 단추, 작업 종료 단추, 시계 초, 시작의 Bing 검색
- **파일 탐색기**: 파일 확장명, 숨김 파일, 클립보드 이력, 간단히 보기, 맞춤형 배열, 항목 확인란, 클래식 상황 메뉴 등
- **게임**: 게임 모드, 게임 바, 백그라운드 녹화, 마우스 가속도, 전체 화면 최적화, 하드웨어 가속 GPU 스케줄링
- **시스템**: 부팅 시 Num Lock 활성화

### 내장 도구

| 도구                  | 기능                                                                                                                      |
| :-------------------- | :------------------------------------------------------------------------------------------------------------------------ |
| **시스템 대시보드**   | CPU, RAM, GPU, 저장소 드라이브, OS 세부 정보를 한 패널에서 확인                                                           |
| **시작 관리자**       | 부팅 시 시작되는 모든 앱 및 작업을 확인하고 켜거나 끄며 파일 위치 열기                                                    |
| **예약된 작업**       | Windows 예약 작업 찾아보기, 실행, 중지, 활성화, 비활성화 또는 삭제                                                        |
| **디스크 정리**       | 임시 파일, 시스템 캐시, Windows 업데이트 잔류물, 프리페치, 썸네일, 휴지통, 크래시 덤프, 이전 Windows 설치를 스캔하고 정리 |
| **사전 설치 앱 제거** | 모든 제거 가능한 AppX 패키지를 위험 배지(안전, 주의, 알 수 없음)와 함께 나열하므로 제거할 항목을 선택할 수 있습니다       |

---

## 안전성

시스템 설정을 변경하면 위험이 따릅니다. optimizerDuck은 역변환성과 사용자 제어를 중심으로 구축되었습니다.

데이터 관행에 대한 자세한 내용은 [개인정보 보호 정책](./PRIVACY.md)을 참고하세요.

- **자동 백업**: 모든 변경 사항은 로컬 폴더에 되돌리기 파일을 작성합니다. 개별 조정을 복원하거나 모든 것을 되돌릴 수 있습니다.
- **한 클릭 되돌리기**: UI에서 한 번의 클릭으로 적용된 최적화를 취소합니다.
- **위험 등급**: 각 조정은 잠재적 영향에 따라 안전, 중간, 위험으로 표시됩니다.
- **기본값 미적용**: 선택할 때까지 아무것도 실행되지 않습니다. 도구는 자동으로 아무것도 활성화하지 않습니다.
- **복원 지점 메시지**: 첫 번째 최적화 전에 앱이 Windows 복원 지점 만들기를 제안합니다.

---

## 기술 세부 사항

- **프레임워크**: .NET 10의 WPF, Fluent 디자인을 위해 WPF UI 라이브러리 사용
- **되돌리기 시스템**: 4가지 되돌리기 단계 유형(레지스트리, 서비스, 예약된 작업, 셸)과 JSON 지속 상태 및 스레드 안전 파일 I/O
- **테마**: 다크(기본값), 라이트, 고대비 모드 및 Mica 백드롭 지원
- **설치 프로그램 없음**: 단일 .exe로 실행되며 설치가 필요 없습니다.
- **백업 시스템**: 모든 변경에 대한 폴더 기반 백업이 한 번의 클릭으로 복원됩니다.
- **검색**: 최적화 및 기능 범주는 리플렉션 + 사용자 지정 속성을 통해 자동으로 검색되며 수동으로 등록할 필요가 없습니다.
- **원격 분석 없음**: 앱은 사용자 데이터를 수집하지 않습니다.

---

## 문서

### [공식 문서](https://optimizerduck.vercel.app/docs/guides/getting-started)

가이드, 최적화 세부 사항, 사용 팁.

---

## 기여하기

버그 보고, 새로운 최적화, 문서 개선, 번역이 모두 환영합니다. [CONTRIBUTING.md](./CONTRIBUTING.md)를 참고해 주세요.

---

## 커뮤니티

> [!TIP]
> 지원, 팁, 다른 사용자 및 기여자와의 논의를 위해 Discord 서버에 참여해 주세요.
>
> <a href="https://discord.gg/tDUBDCYw9Q"><img src="https://discord.com/api/guilds/1091675679994675240/widget.png?style=banner2" alt="Discord 배너 2"/></a>

optimizerDuck이 유용하게 쓰였다면, 다음과 같이 지원해 주실 수 있습니다:

- ⭐ 저장소에 스타를 눌러주세요
- 💬 지원 및 논의를 위해 Discord 채널에 참여해 주세요
- 🐞 GitHub 이슈 탭에서 버그를 제보해 주세요
- 🎁 [여기서](https://optimizerduck.vercel.app/docs/contribute/support-me) 프로젝트를 후원할 수 있습니다

### 링크

- 🌐 [웹사이트](https://optimizerduck.vercel.app/)
- 📖 [문서](https://optimizerduck.vercel.app/docs/guides/getting-started)
- 💬 [Discord](https://discord.gg/tDUBDCYw9Q)
- 🐞 [이슈](https://github.com/itsfatduck/optimizerDuck/issues)

버그 보고, 기능 제안, 번역, 경험 공유 모두 프로젝트에 도움이 됩니다.

---

## 면책 조항

optimizerDuck은 어떠한 보증도 없이 **"있는 그대로"** 제공됩니다.

이 도구와 관련된 타사 소프트웨어나 사용자의 시스템 설정 수정으로 인해 발생할 수 있는 시스템 불안정성, 데이터 손실 등의 문제에 대해 개발자는 어떠한 책임도 지지 않습니다.

변경 사항을 적용하기 전에 항상 **시스템 복원 지점**을 생성해 주세요.

> [!NOTE]
> optimizerDuck은 시스템 설정과 Windows 레지스트리를 수정합니다. 모든 사용 결정과 그로 인한 책임은 사용자 본인에게 있습니다. 설정 변경 전에는 반드시 중요한 데이터를 백업하고 시스템 복원 지점을 만드시길 권장합니다.
>
> 자세한 내용은 [서비스 약관](./TERMS.md), [개인정보 보호 정책](./PRIVACY.md), [면책 조항](./DISCLAIMER.md)을 참고해 주세요.

---

## 라이선스

<div align="center">

<a href="./LICENSE">
<img src=".github/assets/gplv3.png" alt="GPL v3 라이선스" title="GPL v3 라이선스"/>
</a>

**[GPL v3 라이선스](https://www.gnu.org/licenses/gpl-3.0.en.html)**<br>[LICENSE](./LICENSE)를 참고하세요.

</div>

<div align="center">

## 모든 기여자에게 감사합니다

[![Contributors](https://contrib.rocks/image?repo=itsfatduck/optimizerDuck)](https://github.com/itsfatduck/optimizerDuck/graphs/contributors)

</div>
