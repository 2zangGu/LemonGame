# 레몬 퍼즐 (Lemon Puzzle)

사과게임(Fruit Box) 변형 - 드래그로 사각형을 그려 그 안 숫자의 합이 가운데 뜬 **목표 숫자**와 같으면
레몬이 사라집니다. 목표 숫자는 항상 **7~15 범위 안에서, 지금 보드로 실제로 만들 수 있는 값**만
등장하도록 보장되어 있고(보드가 얼마 안 남으면 범위를 벗어난 값도 나올 수 있어요), 사과/레몬 **한 칸만
선택해서는 지울 수 없습니다**(최소 2칸).

웹 프로토타입에서 확인한 규칙을 그대로 C#으로 옮긴 버전이에요.

## 바로 시작하기 (3단계)

1. **Lemon.png 임포트 설정**: `Assets/LemonGame/Art/Lemon.png`를 선택하고 Inspector에서
   Texture Type을 **Sprite (2D and UI)** 로 바꾼 뒤 Apply를 눌러주세요. (새 PNG를 프로젝트에
   추가하면 Unity가 기본적으로 Texture 타입으로 임포트하기 때문에 한 번 바꿔줘야 해요.)
2. 작업할 씬을 열어주세요 (예: `Assets/Scenes/SampleScene.unity`, 비어있어도 상관없어요).
3. 상단 메뉴에서 **Tools > Lemon Puzzle > Setup Scene (Create UI + Wire Managers)** 를 실행하세요.
   Canvas, EventSystem, 보드 UI, 레몬 셀 프리팹, 시작/게임오버 화면, HUD, 매니저까지 전부 자동으로
   배치되고 서로 연결됩니다. 완료되면 Play를 눌러 바로 플레이해볼 수 있어요. (Ctrl+S로 씬 저장하는 것도
   잊지 마세요!)

메뉴를 다시 실행하면 이미 BoardPanel이 있다는 안내와 함께 중복 생성을 막아줘요. 처음부터 다시 배치하고
싶으면 Canvas 아래에 생성된 오브젝트들을 지우고 다시 실행하면 됩니다.

## 스크립트 구성

| 파일 | 역할 |
|---|---|
| `Scripts/BoardModel.cs` | 순수 C# 데이터 모델. 격자 생성, 접두합(prefix sum) 기반 "항상 풀 수 있는 목표 숫자" 계산, 선택 영역 합/개수 계산. Unity UI에 의존하지 않아 유닛 테스트하기 쉬워요. |
| `Scripts/GameManager.cs` | 점수/타이머/목표 숫자/게임 상태를 관리하고 이벤트로 뷰에 알려줍니다. 드래그 선택 결과를 검증하는 `SubmitSelection`이 핵심입니다. |
| `Scripts/BoardController.cs` | 포인터 드래그(`IPointerDownHandler` / `IDragHandler` / `IPointerUpHandler`)로 사각형 선택을 처리하고, 셀 뷰/선택 박스/실시간 합계 텍스트를 갱신합니다. |
| `Scripts/LemonCellView.cs` | 셀 한 칸의 뷰. 값 표시, 선택 하이라이트, 제거 애니메이션. |
| `Scripts/TargetBadgeView.cs` | 목표 숫자 배지 뷰 (레몬 모양 이미지 + 숫자). 성공 시 pop, 실패 시 shake 애니메이션. |
| `Scripts/HUDController.cs` | 점수/타이머/최고 점수(PlayerPrefs) 표시, 시작/게임오버 화면, 토스트 메시지. |
| `Editor/LemonPuzzleSceneSetup.cs` | 씬을 자동으로 구성하는 에디터 메뉴 스크립트. |

## 규칙 요약

- 격자 17 x 10, 숫자 1~9
- 드래그로 사각형을 그리면 그 안의 **레몬(빈 칸 제외)** 들이 선택됨
- 선택한 숫자 합이 목표 숫자와 같고, **선택된 칸이 2개 이상**이면 클리어 + 점수(칸 수 x 10점)
- 클리어 직후 새 목표 숫자가 뜨는데, 이 숫자는 **지금 보드에서 실제로 만들 수 있는 합**만 나옵니다
  (기본적으로 7~15 범위에서 고르고, 그 범위 안에서 더 이상 못 만들면 범위 밖이라도 실제로 만들 수
  있는 합으로 내려갑니다 - 예: 2와 3만 남으면 목표는 5)
- 레몬이 1개 이하로 남으면(더 이상 2칸 조합이 불가능하면) 보드를 새로 채우고 보너스 500점 지급
- 제한시간 120초, 시간 종료 시 게임오버 화면과 최종 점수 표시 (최고 점수는 기기에 저장)

## 커스터마이징 팁

- 난이도/타이머/점수 배점은 `LemonGameManager` 오브젝트의 `GameManager` 컴포넌트 Inspector에서
  바로 조절할 수 있어요 (격자 크기, 목표 숫자 범위, 최소 클리어 칸 수, 제한시간, 점수 등).
- 지금은 프로젝트에 별도 패키지 의존성이 없도록 기본 UI `Text`를 사용했어요. 더 예쁜 폰트/렌더링을
  원하면 TextMeshPro로 바꿔도 로직은 그대로 동작합니다 (Text 참조만 TMP_Text로 교체).
- `Lemon.png`는 절차적으로 생성한 플레이스홀더 아트예요. 실제 레몬 일러스트로 교체하려면 같은 파일을
  덮어쓰거나, 셀 프리팹(`Assets/LemonGame/Prefabs/LemonCell.prefab`)과 `TargetBadge`의 Image
  Sprite만 새 스프라이트로 바꿔주면 됩니다.
- 새 Input System 패키지가 설치된 프로젝트라면 EventSystem에 자동으로
  `InputSystemUIInputModule`을 붙이고, 없으면 기존 `StandaloneInputModule`을 붙입니다.
