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
2. `websocket-sharp.dll`을 `Assets/Plugins/`에 배치
3. Package Manager에서 `com.unity.nuget.newtonsoft-json` 설치
4. **Tools → ClaudeAgent → Unity Command Server** 실행

### Python 측
```bash
pip install websockets
```

## 사용법

```bash
python send_message.py '{"operation":"get_scene_hierarchy","params":{}}'
```

## 지원 기능

GameObject, Transform, Component, Material, Scene, Prefab, Light, Camera, UI, Animator, Terrain, ProBuilder, Screenshot 등 60개 이상의 Unity Editor 조작 지원.

자세한 내용은 [원본 저장소](https://github.com/manahiyo831/UNITY-SKILLS-PUBLIC)를 참고하세요.

## 라이선스

MIT License (원본과 동일)
