# Debugging Operations

Debugging operations provide access to Unity Console logs for monitoring and troubleshooting.

## Operations

### logs

Unified command for retrieving console logs with optional filtering.

**Parameters:**
- `filter` (optional): Log filter type
  - `"errors"`: Only error and exception logs
  - `"statistics"` or `"stats"`: Log counts by type
  - (empty/omitted): All recent logs
- `count` (optional): Number of recent logs to retrieve (default: 50)

**Examples:**

Get all recent logs:
```json
{
  "operation": "logs",
  "params": {
    "count": 50
  }
}
```

Get only error logs:
```json
{
  "operation": "logs",
  "params": {
    "filter": "errors",
    "count": 20
  }
}
```

Get log statistics:
```json
{
  "operation": "logs",
  "params": {
    "filter": "statistics"
  }
}
```

**Response Formats:**

All logs:
```
Console Logs (50 entries):

[Log] 12:00:00 - Message text...
[Warning] 12:00:01 - Warning text...
```

Error logs (includes stack traces):
```
Error Logs (5 entries):

[Error] 12:00:00 - Error message...
  Stack Trace: at UnityEngine...
```

Statistics:
```
Console Log Statistics (Total: 150):

  Log:       100
  Warning:   30
  Error:     15
  Exception: 5
  Assert:    0
```

### clear_logs

Clears all stored console logs from the log manager.

**Parameters:** None

**Example:**
```json
{
  "operation": "clear_logs",
  "params": {}
}
```

**Response:**
```
Console logs cleared successfully
```
