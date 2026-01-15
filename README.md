# Unity Skills for macOS

[Original Repository](https://github.com/manahiyo831/UNITY-SKILLS-PUBLIC)에서 fork하여 **macOS용으로 포팅**한 버전입니다.

## 원본과의 차이점

- **macOS 지원 추가**: `send_message.py`에서 AppleScript를 사용한 윈도우 관리
- **크로스플랫폼 아키텍처**: Windows/macOS 모두 지원
- **Command Server UI 개선**: 로그 선택/복사 기능 추가

## 동작 환경

| 항목 | 요건 |
|------|------|
| OS | **macOS, Windows** |
| Unity | 6.x (6000.x) 권장 |
| Python | 3.10 이상 |

## 설치 (macOS)

### Unity 측
1. `Assets/ClaudeAgent/Editor/` 폴더를 Unity 프로젝트에 복사
2. `Assets/Plugins/` 폴더 전체를 Unity 프로젝트에 복사:
   - `websocket-sharp.dll` - WebSocket 통신
   - `Roslyn/` - Microsoft Roslyn 컴파일러 (런타임 코드 실행용)
   - `RoslynCSharp/` - 오픈소스 RoslynCSharp 호환 API
3. Package Manager에서 필수 패키지 설치:
   - `com.unity.nuget.newtonsoft-json` - JSON 직렬화
   - `com.unity.probuilder` - ProBuilder 메쉬 생성 기능 (계단, 아치, 파이프 등)
4. **Tools → ClaudeAgent → Unity Command Server** 실행

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

## 지원 기능

GameObject, Transform, Component, Material, Scene, Prefab, Light, Camera, UI, Animator, Terrain, ProBuilder, Screenshot, **Runtime 코드 실행** 등 64개의 Unity Editor 조작 지원.

자세한 내용은 [원본 저장소](https://github.com/manahiyo831/UNITY-SKILLS-PUBLIC)를 참고하세요.

## 라이선스

MIT License (원본과 동일)
