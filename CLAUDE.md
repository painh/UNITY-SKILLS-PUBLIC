# Unity Skills Project

Unity Command Server - Claude Code와 Unity Editor를 연동하는 WebSocket 기반 명령 서버

## 프로젝트 구조

```
Assets/Plugins/ClaudeAgent/Editor/     # Unity Editor 플러그인 (C#)
ClaudeCode/.claude/skills/             # Claude 스킬 문서 (MD)
scripts/                               # 빌드 스크립트
.github/workflows/                     # GitHub Actions
```

## 버전 업 및 배포 체크리스트

### 1. 코드 변경 완료 후

1. **버전 번호 업데이트**
   - `Assets/Plugins/ClaudeAgent/Editor/UnityCommandServer.cs`
   - `VersionChecker.CurrentVersion` 상수 수정

2. **스킬 문서 업데이트** (중요!)
   - `ClaudeCode/.claude/skills/unity-editor-operations/` 폴더
   - 새 명령어 추가 시 해당 카테고리 MD 파일 업데이트
   - 새 카테고리 추가 시 새 MD 파일 생성

3. **커밋 및 푸시**
   ```bash
   git add -A
   git commit -m "feat: 변경 내용"
   git push
   ```

### 2. 릴리즈 배포

4. **태그 생성 및 푸시** (GitHub Actions 트리거)
   ```bash
   git tag v0.0.XX
   git push origin v0.0.XX
   ```
   - 태그 푸시 시 GitHub Actions가 자동으로:
     - `.unitypackage` 생성
     - GitHub Release 생성
     - Release notes 자동 생성

### 3. 사용자 스킬 업데이트

5. **Claude 사용자 스코프 스킬 업데이트**
   - `~/.claude/skills/` 또는 사용자 정의 위치의 스킬 파일 업데이트
   - 프로젝트의 `ClaudeCode/.claude/skills/` 내용을 사용자 스코프에 복사

## 명령어 카테고리

| 카테고리 | 파일 | 주요 명령어 |
|---------|------|------------|
| GameObject | GameObject.md | create_primitive, delete_gameobject |
| Transform | Transform.md | transform, set_parent |
| Component | Component.md | add_component, set_component_property |
| Material | Material.md | set_material, create_material |
| Scene | Scene.md | save_scene, load_scene |
| Asset | Asset.md | create_asset, delete_asset |
| Prefab | Prefab.md | create_prefab, instantiate_prefab |
| Animator | Animator.md | set_animator_parameter |
| UI | UI.md | create_canvas, create_button |
| Terrain | Terrain.md | create_terrain, set_terrain_height |
| ProBuilder | ProBuilder.md | create_probuilder_shape |
| Runtime | Runtime.md | execute_code, attach_script |
| Layer | Layer.md | create_layer, set_layer |
| Physics | Physics.md | set_physics_settings, set_layer_collision |
| Input | Input.md | simulate_key, simulate_mouse |
| Screenshot | Screenshot.md | take_screenshot |
| Debugging | Debugging.md | get_logs |

## Feature Permissions

Unity Command Server 윈도우에서 카테고리별 권한 제어 가능:
- Screenshot, Runtime, SceneEdit, AssetEdit, PrefabEdit
- ProBuilder, Terrain, Layer, Physics, Input
