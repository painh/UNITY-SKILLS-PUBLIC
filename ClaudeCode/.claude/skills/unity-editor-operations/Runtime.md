# Runtime Operations

Runtime operations allow executing arbitrary C# code at runtime in Play mode. This uses the open-source RoslynCSharp implementation (based on Microsoft Roslyn compiler).

**Requirements:**
- Unity must be in **Play mode**
- RoslynCSharp plugin must be installed in Assets/Plugins/

---

## execute_code

Execute arbitrary C# code at runtime. The code is compiled and the `Run()` method is called.

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `code` | string | Yes | C# source code to compile and execute |
| `class_name` | string | No | Specific class name to find (auto-detect if omitted) |
| `method` | string | No | Method name to call (default: "Run") |

### Example

```bash
python send_message.py '{"operation":"execute_code","params":{"code":"using UnityEngine; public class Test { public string Run() { Debug.Log(\"Hello!\"); return \"Success\"; } }"}}'
```

### Code Template

```csharp
using UnityEngine;

public class MyScript
{
    public string Run()
    {
        // Your code here
        Debug.Log("Executed!");
        return "Result value";
    }
}
```

### Response

```json
{
  "success": true,
  "result": "Executed Test.Run()\nReturn value: Success"
}
```

---

## attach_script

Compile a MonoBehaviour script and attach it to a GameObject at runtime.

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `code` | string | Yes | MonoBehaviour source code |
| `target` | string | Yes | GameObject path to attach to |
| `class_name` | string | No | Specific MonoBehaviour class name |

### Example

```bash
python send_message.py '{"operation":"attach_script","params":{"target":"Player","code":"using UnityEngine; public class Rotator : MonoBehaviour { void Update() { transform.Rotate(0, 100 * Time.deltaTime, 0); } }"}}'
```

### Code Template

```csharp
using UnityEngine;

public class MyBehaviour : MonoBehaviour
{
    void Start()
    {
        Debug.Log("Script attached!");
    }

    void Update()
    {
        // Called every frame
    }
}
```

### Response

```json
{
  "success": true,
  "result": "Attached Rotator to Player"
}
```

---

## execute_on_object

Execute code with a specific GameObject passed as parameter.

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `code` | string | Yes | C# source code |
| `target` | string | Yes | GameObject path to pass to Run() |
| `class_name` | string | No | Specific class name |
| `method` | string | No | Method name (default: "Run") |

### Example

```bash
python send_message.py '{"operation":"execute_on_object","params":{"target":"Enemy","code":"using UnityEngine; public class Destroyer { public void Run(GameObject obj) { obj.SetActive(false); Debug.Log(obj.name + \" disabled\"); } }"}}'
```

### Code Template

```csharp
using UnityEngine;

public class MyScript
{
    public void Run(GameObject target)
    {
        // target is the GameObject specified in params
        target.transform.position = Vector3.zero;
        Debug.Log("Moved " + target.name);
    }
}
```

### Response

```json
{
  "success": true,
  "result": "Executed Destroyer.Run(Enemy)"
}
```

---

## play_animation

Play an animation state on an Animator component. (Does not require code compilation)

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `target` | string | Yes | GameObject path with Animator |
| `state` | string | Yes | Animation state name to play |
| `layer` | int | No | Animator layer (default: 0) |
| `normalized_time` | float | No | Start time 0-1 (default: 0) |

### Example

```bash
python send_message.py '{"operation":"play_animation","params":{"target":"Character","state":"Run","layer":0}}'
```

### Response

```json
{
  "success": true,
  "result": "Playing 'Run' on Character (layer 0)"
}
```

---

## Error Handling

### Not in Play Mode

```json
{
  "success": false,
  "error": "execute_code requires Play mode. Enter Play mode first."
}
```

### Compilation Error

```json
{
  "success": false,
  "error": "Compilation failed:\n  (5,10): error CS1002: ; expected"
}
```

### Missing Class

```json
{
  "success": false,
  "error": "No user-defined class found in the code. Define a class with a Run() method."
}
```

---

## Best Practices

1. **Always include `using UnityEngine;`** - Required for most Unity APIs
2. **Keep code simple** - Complex code is harder to debug via JSON
3. **Use Debug.Log()** - Output appears in Unity Console
4. **Return values** - Return strings for easy result checking
5. **Test in RoslynCSharp Test window first** - Window > RoslynCSharp Test
