# Unity Skills for macOS

[Original Repository](https://github.com/manahiyo831/UNITY-SKILLS-PUBLIC)에서 fork하여 **macOS용으로 포팅**한 버전입니다.

## 원본과의 차이점

- **macOS 지원 추가**: `send_message.py`에서 AppleScript를 사용한 윈도우 관리
- **크로스플랫폼 아키텍처**: Windows/macOS 모두 지원
- **Command Server UI 개선**: 로그 선택/복사 기능 추가
- **동적 포트 할당**: 여러 Unity 인스턴스 동시 지원
- **서버 자동 시작**: Unity 시작 시 자동으로 서버 실행 (윈도우 독립적)

## 동작 환경

| 항목 | 요건 |
|------|------|
| OS | **macOS, Windows** |
| Unity | 6.x (6000.x) 권장 |
| Python | 3.10 이상 |

## 설치 (macOS)

### Unity 측
1. `Assets/Plugins/` 폴더 전체를 Unity 프로젝트에 복사:
   - `ClaudeAgent/` - Command Server 및 Editor 기능
   - `websocket-sharp.dll` - WebSocket 통신
   - `Roslyn/` - Microsoft Roslyn 컴파일러 (런타임 코드 실행용)
   - `RoslynCSharp/` - 오픈소스 RoslynCSharp 호환 API
2. Package Manager에서 필수 패키지 설치:
   - `com.unity.nuget.newtonsoft-json` - JSON 직렬화
   - `com.unity.probuilder` - ProBuilder 메쉬 생성 기능 (계단, 아치, 파이프 등)
3. **Tools → Unity Command Server** 실행

### 필수 Unity 패키지

| 패키지 | 패키지 ID | 용도 |
|--------|----------|------|
| Newtonsoft Json | `com.unity.nuget.newtonsoft-json` | JSON 직렬화/역직렬화 |
| ProBuilder | `com.unity.probuilder` | 계단, 아치, 파이프, 문 등 복잡한 메쉬 생성 |

**설치 방법**: Window → Package Manager → Unity Registry에서 검색 후 Install

### Runtime 코드 실행 (선택)
`execute_code`, `attach_script` 등 런타임 C# 코드 실행 기능을 사용하려면:
- `Assets/Plugins/Roslyn/` 및 `Assets/Plugins/RoslynCSharp/` 필수
- Play 모드에서만 동작
- 테스트: **Window → RoslynCSharp Test**

### Python 측
```bash
pip install websockets
```

## 사용법

```bash
python send_message.py '{"operation":"get_scene_hierarchy","params":{}}'
```

## 여러 Unity 인스턴스 지원

Command Server는 **동적 포트 할당**을 지원합니다:

- 기본 포트: `8766`
- 이미 사용 중이면 `8767`, `8768`, ... 순서로 자동 할당
- 포트 번호는 `Library/ClaudeAgent/port.txt`에 저장됨

### 여러 프로젝트 동시 작업

```bash
# 프로젝트 A (포트 8766)
cat /path/to/projectA/Library/ClaudeAgent/port.txt
# 출력: 8766

# 프로젝트 B (포트 8767)
cat /path/to/projectB/Library/ClaudeAgent/port.txt
# 출력: 8767

# 특정 프로젝트에 명령 전송
python send_message.py --port 8767 '{"operation":"get_scene_hierarchy","params":{}}'
```

## 기능 확장

`CommandExecutor`는 **partial class**로 구현되어 있어 확장이 용이합니다:

```csharp
// Assets/Plugins/ClaudeAgent/Editor/CommandExecutor.MyFeature.cs
namespace ClaudeAgent
{
    public partial class CommandExecutor
    {
        private void RegisterMyFeatureCommands()
        {
            RegisterCommand("my_operation", MyOperationHandler);
        }

        private (bool, string) MyOperationHandler(CommandParams p)
        {
            // 구현
            return Success("완료");
        }
    }
}
```

그리고 `CommandExecutor.cs`의 `InitializeCommands()`에 등록:
```csharp
RegisterMyFeatureCommands();
```

## Command Server 윈도우

- **Tools → Unity Command Server**로 열기
- 인스펙터 옆에 도킹 가능 (Unity 레이아웃으로 저장됨)
- 서버는 윈도우와 **독립적으로 실행** (윈도우 닫아도 서버 동작)

## 지원 기능

GameObject, Transform, Component, Material, Scene, Prefab, Light, Camera, UI, Animator, Terrain, ProBuilder, Screenshot, **Runtime 코드 실행** 등 64개의 Unity Editor 조작 지원.

자세한 내용은 [원본 저장소](https://github.com/manahiyo831/UNITY-SKILLS-PUBLIC)를 참고하세요.

## 라이선스

MIT License (원본과 동일)
