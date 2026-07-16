# ActionFit Ice Cream Race (`com.actionfit.icecream-race`)

CatDetective 5인 PVP의 경기 규칙과 기본 조정값을 프로젝트 UI, 게임 이벤트, 저장 시스템에서 분리한 재사용 가능 콘텐츠 코어입니다.

## 주요 기능

- 1라운드 5명/3위 컷, 2라운드 4명/2위 컷, 3라운드 이상 3명/1위 컷
- 봇 완료 시각과 행동 곡선 4종을 함께 평가하는 실시간 순위
- 라운드 연승에 따른 `X1`, `X2`, `X4`, `X10` 보상 로드 포인트 배율
- CatDetective `MCD-1000/5P_PVP@676e6b96dce415977f21121db2ace8c4aaee7fb1` 기준 활성 요일, 라운드, 주문, 머지, 보상 로드 catalog
- schema version이 포함된 JSON 상태와 `IContentStateStore` 기반 영속 저장
- pending 보상 transaction과 `IContentRewardService.GrantOnce` 기반 재실행 복구
- 시간, 난수, 상대 프로필을 교체하는 독립 어댑터 계약

## 기본 사용법

```csharp
using ActionFit.IceCreamRace;

IceCreamRaceEngine race = IceCreamRaceEngine.CreateDefault();
race.Restore();

if (race.TryStartEvent() && race.Matchmake())
{
    race.StartRace();
}

race.AddTokens(10);
race.EvaluateTimeout();
```

`CreateDefault`는 `com.actionfit.content-core`의 PlayerPrefs 상태 저장소와 로컬 멱등 보상 ledger를 사용합니다. 실제 게임 재화는 프로젝트의 `IContentRewardService` 구현을 생성자에 주입하세요.

## 프로젝트 연결 경계

- 주문과 머지 이벤트는 프로젝트 어댑터에서 점수로 변환한 뒤 `AddTokens`를 호출합니다.
- 기존 게임 UI가 매칭 전 이벤트 진입 화면을 열어야 하면 `TryStartEvent`로 현재 스케줄 창만 먼저 시작합니다.
- 화면은 `StateChanged`를 구독하고 `State`, `CurrentRank`, `GetOpponentProgress`를 읽어 표현합니다.
- 서버 시간이나 DevTool 시간이 필요하면 `IIceCreamRaceClock`을 교체합니다.
- 프로젝트 활성 요일과 kill switch는 `IIceCreamRaceSchedulePolicy`로 교체합니다. standalone 기본값은 원본의 월·화입니다.
- 프로젝트 프로필/프레임은 `IIceCreamRaceOpponentProvider`에서 제공합니다.
- 진행 중 이벤트의 catalog를 업데이트할 때는 `IIceCreamRaceCatalogResolver`에 이전 version/revision catalog도 등록해야 합니다. 등록하지 않은 저장값은 조용히 새 밸런스를 적용하지 않고 명시적으로 복구를 중단합니다.
- `IsRewardServiceAvailable`이 `false`이면 보상 버튼을 노출하지 마세요. 도달한 보상이 있을 때 `ClaimRoadRewards`를 호출하면 pending 상태를 쓰기 전에 중단됩니다.
- `IceCreamRaceEngine`은 sealed이며 결과 확정, 상태 전이, 보상 복구 순서는 override할 수 없습니다. 화면 연출은 별도 presentation 계층에서 확장합니다.
- MatchStart 같은 화면이 현재 배율 표시를 완료한 뒤에는 `SaveDisplayedMultiplierStep()`으로 배율 단계만 인정할 수 있습니다. 이 호출은 토큰과 경과시간 표시 기준을 변경하지 않습니다.

## 보상 복구 계약

`ClaimRoadRewards`는 먼저 transaction ID, 목표 claimed 포인트, 보상 snapshot을 저장한 뒤 `GrantOnce`를 호출합니다. transaction ID는 `<contentId>/event/<eventEndUtcTicks>/road/<claimId>` 형식으로 이벤트 인스턴스를 포함합니다. 지급 후 상태 저장 전에 앱이 종료되면 `Restore`가 ledger를 확인하고 claimed 포인트만 확정합니다. 지급 전에 종료되면 저장된 snapshot을 같은 transaction ID로 다시 지급합니다.

프로젝트 보상 구현은 한 transaction의 전체 보상 목록을 원자적으로 처리하고, 동일 transaction ID에 대해 영구적으로 중복 지급하지 않아야 합니다.

일반 토큰 증가는 버퍼 저장소에 맡길 수 있지만 이벤트/레이스 시작, 결과 확정, timeout, 결과 claim, 보상 transaction 전후에는 `IFlushableContentStateStore`를 통해 즉시 내보냅니다. 빈 schedule kill switch는 저장된 orphan 이벤트 진행도와 catalog pin도 정리합니다.

## Unity Menu

- Package root: `Tools > Package > ActionFit Ice Cream Race`
- README: `Tools > Package > ActionFit Ice Cream Race > README`

## 설치

수동 게시 후 다른 프로젝트의 `Packages/manifest.json`에는 다음 Git UPM 주소를 추가합니다.

```json
{
  "dependencies": {
    "com.actionfit.content-core": "https://github.com/ActionFit-Editor/ContentCore.git#0.2.1",
    "com.actionfit.icecream-race": "https://github.com/ActionFit-Editor/IceCreamRace.git#0.1.5"
  }
}
```

## 테스트

Unity Test Framework의 EditMode에서 `com.actionfit.icecream-race.Editor.Tests`를 실행합니다. catalog 동등성, 참가 인원/컷, 실시간 순위, 배율, timeout, serializer, 보상 지급 전·후 재실행 복구를 검증합니다.

## 제외 범위

- 프로젝트 `Main`, `DataStore`, `GameEvents`, UI/Addressables 직접 연결
- 서버 권위 매칭 및 부정행위 방지
- 프로젝트 전용 이미지, Spine, 사운드의 재배포
- Cat Merge Cafe 기존 저장값의 자동 마이그레이션
