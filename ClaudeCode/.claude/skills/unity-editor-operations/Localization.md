# Localization Operations

Unity Localization 패키지를 사용한 다국어 지원 시스템 관리 명령어입니다.

**요구 사항:**
- Unity Localization 패키지 설치 필요 (`com.unity.localization`)
- Package Manager에서 설치: Window > Package Manager > Unity Registry > Localization

---

## get_localization_info

로컬라이제이션 시스템 정보를 조회합니다.

### Parameters

없음

### Example

```json
{
  "operation": "get_localization_info",
  "params": {}
}
```

### Response

```
Localization Info:

  Status: Unity Localization package installed
  Settings: Assets/Settings/Localization/LocalizationSettings.asset
  Locales (3):
    - ko: Korean
    - en: English
    - ja: Japanese
```

---

## create_localization_settings

LocalizationSettings 에셋을 생성하고 Locale들을 설정합니다.

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `locales` | string[] | Yes | Locale 코드 배열 (예: ["ko", "en", "ja"]) |
| `default_locale` | string | No | 기본 Locale 코드 (기본값: locales의 첫 번째) |
| `settings_path` | string | No | 설정 파일 경로 (기본값: "Assets/Settings/Localization/LocalizationSettings.asset") |

### Example

```json
{
  "operation": "create_localization_settings",
  "params": {
    "locales": ["ko", "en", "ja"],
    "default_locale": "ko"
  }
}
```

### Response

```
Created LocalizationSettings at: Assets/Settings/Localization/LocalizationSettings.asset
Locales: ko, en, ja
Default locale: ko
```

---

## create_string_table

StringTableCollection을 생성합니다.

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `table_name` | string | Yes | 테이블 이름 (예: "Items", "UI", "Dialogs") |
| `folder_path` | string | No | 저장 폴더 경로 (기본값: "Assets/Localization/StringTables") |

### Example

```json
{
  "operation": "create_string_table",
  "params": {
    "table_name": "Items"
  }
}
```

### Response

```
Created StringTableCollection 'Items' at: Assets/Localization/StringTables/Items.asset
```

---

## list_string_tables

모든 StringTableCollection을 조회합니다.

### Parameters

없음

### Example

```json
{
  "operation": "list_string_tables",
  "params": {}
}
```

### Response

```
StringTableCollections (2):

  - Items
    Path: Assets/Localization/StringTables/Items.asset
    Entries: 50

  - UI
    Path: Assets/Localization/StringTables/UI.asset
    Entries: 120
```

---

## add_string_table_entry

StringTable에 단일 엔트리를 추가합니다.

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `table_name` | string | Yes | 테이블 이름 |
| `entry_key` | string | Yes | 엔트리 키 |
| `values` | object | Yes | Locale별 값 (예: {"ko": "체력 물약", "en": "Health Potion"}) |

### Example

```json
{
  "operation": "add_string_table_entry",
  "params": {
    "table_name": "Items",
    "entry_key": "item_potion_health_name",
    "values": {
      "ko": "체력 물약",
      "en": "Health Potion",
      "ja": "回復薬"
    }
  }
}
```

### Response

```
Added entry 'item_potion_health_name' to 3 locale(s) in table 'Items'
```

---

## add_string_table_entries

StringTable에 여러 엔트리를 한 번에 추가합니다.

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `table_name` | string | Yes | 테이블 이름 |
| `children` | object[] | Yes | 엔트리 배열 [{key, values}, ...] |

### Example

```json
{
  "operation": "add_string_table_entries",
  "params": {
    "table_name": "Items",
    "children": [
      {
        "key": "item_potion_health_name",
        "values": {
          "ko": "체력 물약",
          "en": "Health Potion",
          "ja": "回復薬"
        }
      },
      {
        "key": "item_potion_health_desc",
        "values": {
          "ko": "체력을 50 회복합니다.",
          "en": "Restores 50 HP.",
          "ja": "HPを50回復します。"
        }
      },
      {
        "key": "item_potion_mana_name",
        "values": {
          "ko": "마나 물약",
          "en": "Mana Potion",
          "ja": "マナ薬"
        }
      }
    ]
  }
}
```

### Response

```
Added 9 entries to table 'Items'
```

---

## import_string_table_csv

CSV 파일에서 StringTable로 데이터를 가져옵니다.

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `table_name` | string | Yes | 테이블 이름 |
| `csv_path` | string | Yes | CSV 파일 경로 |

### CSV Format

첫 번째 열은 키, 나머지 열은 Locale 코드입니다:

```csv
key,ko,en,ja
item_sword_name,검,Sword,剣
item_sword_desc,기본 무기입니다.,A basic weapon.,基本の武器です。
item_shield_name,방패,Shield,盾
```

### Example

```json
{
  "operation": "import_string_table_csv",
  "params": {
    "table_name": "Items",
    "csv_path": "Assets/Localization/Data/items.csv"
  }
}
```

### Response

```
Imported 6 entries from CSV to table 'Items'
```

---

## Error Handling

### Package Not Installed

```json
{
  "success": false,
  "error": "Unity Localization package is not installed. Install com.unity.localization via Package Manager."
}
```

### Table Not Found

```json
{
  "success": false,
  "error": "StringTableCollection not found: Items"
}
```

### Settings Already Exists

```json
{
  "success": false,
  "error": "LocalizationSettings already exists at: Assets/Settings/Localization/LocalizationSettings.asset"
}
```

---

## Use Cases

### 1. 초기 설정

```bash
# 1. 로컬라이제이션 설정 생성
python send_message.py '{
  "operation": "create_localization_settings",
  "params": {
    "locales": ["ko", "en", "ja"],
    "default_locale": "ko"
  }
}'

# 2. 문자열 테이블 생성
python send_message.py '{"operation": "create_string_table", "params": {"table_name": "UI"}}'
python send_message.py '{"operation": "create_string_table", "params": {"table_name": "Items"}}'
python send_message.py '{"operation": "create_string_table", "params": {"table_name": "Dialogs"}}'
```

### 2. 아이템 데이터 추가

```bash
# CSV에서 대량 임포트
python send_message.py '{
  "operation": "import_string_table_csv",
  "params": {
    "table_name": "Items",
    "csv_path": "Assets/Localization/Data/items.csv"
  }
}'
```

### 3. UI 문자열 개별 추가

```bash
python send_message.py '{
  "operation": "add_string_table_entry",
  "params": {
    "table_name": "UI",
    "entry_key": "btn_start",
    "values": {"ko": "시작", "en": "Start", "ja": "スタート"}
  }
}'
```

---

## Best Practices

1. **테이블 구성**: 용도별로 테이블 분리 (UI, Items, Dialogs, Enums 등)
2. **키 명명 규칙**: `category_subcategory_type` 형식 사용 (예: `item_potion_health_name`)
3. **CSV 활용**: 대량 데이터는 CSV로 관리하고 임포트
4. **기본 Locale 설정**: 개발 언어를 기본으로 설정하여 빠른 테스트 가능
