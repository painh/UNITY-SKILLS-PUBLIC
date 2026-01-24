#!/usr/bin/env python3
"""
Unity Command Server にJSONコマンドを送信し、実行結果を受け取るスクリプト
WebSocketを使用した同期通信（タイムアウト10秒）

コマンド実行前にUnityをアクティブ化し、実行後に元のウィンドウに戻ります。

対応OS: Windows, macOS
必要なパッケージ:
  pip install websockets
  Windows: pip install pywin32
  macOS: (標準ライブラリのみ)
"""

import asyncio
import websockets
import json
import sys
import os
import time
import platform
import subprocess
import argparse
from abc import ABC, abstractmethod

# サーバー設定
DEFAULT_PORT = 8766
TIMEOUT_SECONDS = 10
MAX_BATCH_COMMANDS = 20

# 現在のOS
CURRENT_OS = platform.system()


class WindowManagerBase(ABC):
    """ウィンドウ管理の基底クラス"""

    def __init__(self):
        self.unity_title = None

    @abstractmethod
    def find_unity_window(self, exact_title: str = None, pid: int = None) -> bool:
        """Unityウィンドウを検索"""
        pass

    @abstractmethod
    def save_current_window(self):
        """現在のフォアグラウンドウィンドウを保存"""
        pass

    @abstractmethod
    def activate_unity(self) -> bool:
        """Unityをアクティブ化"""
        pass

    @abstractmethod
    def restore_original(self) -> bool:
        """元のウィンドウに戻す"""
        pass


class MacWindowManager(WindowManagerBase):
    """macOS用ウィンドウ管理クラス"""

    def __init__(self):
        super().__init__()
        self.original_app = None
        self.unity_found = False
        self.unity_pid = None

    def _run_applescript(self, script: str) -> str:
        """AppleScriptを実行"""
        try:
            result = subprocess.run(
                ["osascript", "-e", script],
                capture_output=True,
                text=True,
                timeout=5
            )
            return result.stdout.strip()
        except Exception as e:
            print(f"⚠ AppleScript error: {e}")
            return ""

    def find_unity_window(self, exact_title: str = None, pid: int = None) -> bool:
        """Unityウィンドウを検索"""
        self.unity_pid = pid

        if pid:
            # PIDが指定されていれば、そのプロセスが存在するか確認
            script = f'''
            tell application "System Events"
                set unityProcs to (every process whose unix id is {pid})
                if (count of unityProcs) > 0 then
                    return "found"
                else
                    return "not_found"
                end if
            end tell
            '''
            result = self._run_applescript(script)
            self.unity_found = (result == "found")
        else:
            # PIDがない場合は従来の方法
            script = '''
            tell application "System Events"
                set unityProcs to (name of every process whose name contains "Unity")
                if (count of unityProcs) > 0 then
                    return "found"
                else
                    return "not_found"
                end if
            end tell
            '''
            result = self._run_applescript(script)
            self.unity_found = (result == "found")

        if self.unity_found:
            self.unity_title = exact_title or "Unity"

        return self.unity_found

    def save_current_window(self):
        """現在のアクティブアプリを保存"""
        script = '''
        tell application "System Events"
            set frontApp to name of first application process whose frontmost is true
            return frontApp
        end tell
        '''
        self.original_app = self._run_applescript(script)

    def activate_unity(self) -> bool:
        """Unityをアクティブ化"""
        if not self.unity_found:
            return False

        if self.unity_pid:
            # PIDを使用して特定のUnityプロセスをアクティブ化
            script = f'''
            tell application "System Events"
                set unityProc to first process whose unix id is {self.unity_pid}
                set frontmost of unityProc to true
            end tell
            '''
        else:
            # 従来の方法
            script = '''
            tell application "Unity" to activate
            '''
        self._run_applescript(script)
        return True

    def restore_original(self) -> bool:
        """元のアプリに戻す"""
        if not self.original_app:
            return False

        script = f'''
        tell application "{self.original_app}" to activate
        '''
        self._run_applescript(script)
        return True


class WindowsWindowManager(WindowManagerBase):
    """Windows用ウィンドウ管理クラス"""

    def __init__(self):
        super().__init__()
        self.unity_hwnd = None
        self.original_hwnd = None

        # Windows用モジュールをインポート
        import win32gui
        import win32con
        import win32api
        import ctypes

        self.win32gui = win32gui
        self.win32con = win32con
        self.win32api = win32api
        self.ctypes = ctypes

    def find_window_by_title(self, title_substring: str) -> list:
        """タイトルに部分文字列を含むウィンドウを検索"""
        result = []

        def callback(hwnd, _):
            if self.win32gui.IsWindowVisible(hwnd):
                title = self.win32gui.GetWindowText(hwnd)
                if title_substring in title:
                    result.append((hwnd, title))
            return True

        self.win32gui.EnumWindows(callback, None)
        return result

    def find_unity_window(self, exact_title: str = None, pid: int = None) -> bool:
        """Unityウィンドウを検索"""
        # TODO: Windows版もPIDでウィンドウを検索するように改善可能
        if exact_title:
            # 完全一致検索
            windows = self.find_window_by_title(exact_title)
            if windows:
                self.unity_hwnd = windows[0][0]
                self.unity_title = windows[0][1]
                return True

        # "Unity" を含むウィンドウを検索
        windows = self.find_window_by_title("Unity")
        for hwnd, title in windows:
            # Unity Editor のウィンドウを特定（タイトルに " - Unity " を含む）
            if " - Unity " in title:
                self.unity_hwnd = hwnd
                self.unity_title = title
                return True

        return False

    def save_current_window(self):
        """現在のフォアグラウンドウィンドウを保存"""
        self.original_hwnd = self.win32gui.GetForegroundWindow()

    def force_foreground_window(self, hwnd: int) -> bool:
        """
        ウィンドウを強制的にフォアグラウンドに
        バックグラウンドプロセスからでも動作する回避策を使用
        """
        if not hwnd or not self.win32gui.IsWindow(hwnd):
            return False

        try:
            # 最小化されている場合は復元
            if self.win32gui.IsIconic(hwnd):
                self.win32gui.ShowWindow(hwnd, self.win32con.SW_RESTORE)
                time.sleep(0.1)

            # 方法1: Altキーを送信してフォアグラウンドロックを解除
            self.win32api.keybd_event(self.win32con.VK_MENU, 0, 0, 0)  # Alt押下
            self.win32api.keybd_event(self.win32con.VK_MENU, 0, self.win32con.KEYEVENTF_KEYUP, 0)  # Alt解放

            # 方法2: スレッドをアタッチしてフォアグラウンドに設定
            foreground_hwnd = self.win32gui.GetForegroundWindow()
            if foreground_hwnd:
                foreground_thread = self.ctypes.windll.user32.GetWindowThreadProcessId(foreground_hwnd, None)
                current_thread = self.ctypes.windll.kernel32.GetCurrentThreadId()

                if foreground_thread != current_thread:
                    self.ctypes.windll.user32.AttachThreadInput(current_thread, foreground_thread, True)
                    self.win32gui.SetForegroundWindow(hwnd)
                    self.ctypes.windll.user32.AttachThreadInput(current_thread, foreground_thread, False)
                else:
                    self.win32gui.SetForegroundWindow(hwnd)
            else:
                self.win32gui.SetForegroundWindow(hwnd)

            return True
        except Exception as e:
            print(f"⚠ Window activation error: {e}")
            return False

    def activate_unity(self) -> bool:
        """Unityをアクティブ化"""
        if not self.unity_hwnd:
            return False
        return self.force_foreground_window(self.unity_hwnd)

    def restore_original(self) -> bool:
        """元のウィンドウに戻す"""
        if not self.original_hwnd:
            return False
        return self.force_foreground_window(self.original_hwnd)


class DummyWindowManager(WindowManagerBase):
    """ウィンドウ管理をスキップするダミークラス（Linux等）"""

    def find_unity_window(self, exact_title: str = None, pid: int = None) -> bool:
        print("⚠ Window management not supported on this OS, skipping...")
        return False

    def save_current_window(self):
        pass

    def activate_unity(self) -> bool:
        return False

    def restore_original(self) -> bool:
        return False


def create_window_manager() -> WindowManagerBase:
    """OSに応じたWindowManagerを作成"""
    if CURRENT_OS == "Windows":
        try:
            return WindowsWindowManager()
        except ImportError:
            print("⚠ pywin32 not installed, window management disabled")
            print("  Install with: pip install pywin32")
            return DummyWindowManager()
    elif CURRENT_OS == "Darwin":  # macOS
        return MacWindowManager()
    else:
        return DummyWindowManager()


def read_port_from_project(project_path: str) -> int:
    """
    Read port number from Unity project's Library/ClaudeAgent/port.txt

    Args:
        project_path: Path to Unity project root

    Returns:
        Port number or None if not found
    """
    port_file = os.path.join(project_path, "Library", "ClaudeAgent", "port.txt")
    if os.path.exists(port_file):
        try:
            with open(port_file, "r") as f:
                port = int(f.read().strip())
                return port
        except (ValueError, IOError) as e:
            print(f"⚠ Failed to read port file: {e}")
    return None


def get_server_uri(port: int = None, project: str = None) -> str:
    """
    Get server URI based on port or project path

    Args:
        port: Explicit port number
        project: Unity project path to read port from

    Returns:
        WebSocket server URI
    """
    if port:
        return f"ws://127.0.0.1:{port}/"

    if project:
        port = read_port_from_project(project)
        if port:
            print(f"📂 Using port {port} from project: {project}")
            return f"ws://127.0.0.1:{port}/"
        else:
            print(f"⚠ Port file not found in project, using default port {DEFAULT_PORT}")

    return f"ws://127.0.0.1:{DEFAULT_PORT}/"


async def get_unity_info(server_uri: str) -> dict:
    """Unity Command ServerからUnity情報（タイトル、PID）を取得"""
    try:
        async with websockets.connect(server_uri) as websocket:
            request = {"message": '{"operation":"get_unity_info","params":{}}'}
            await websocket.send(json.dumps(request))
            response_str = await asyncio.wait_for(websocket.recv(), timeout=5)
            response = json.loads(response_str)
            if response.get("success"):
                result = response.get("result", "")
                # 結果がJSON文字列の場合はパース
                if isinstance(result, str):
                    try:
                        return json.loads(result)
                    except:
                        return {"title": result}
                return result
    except Exception:
        pass
    # フォールバック: 旧APIを試す
    try:
        async with websockets.connect(server_uri) as websocket:
            request = {"message": '{"operation":"get_window_title","params":{}}'}
            await websocket.send(json.dumps(request))
            response_str = await asyncio.wait_for(websocket.recv(), timeout=5)
            response = json.loads(response_str)
            if response.get("success"):
                return {"title": response.get("result", "")}
    except Exception:
        pass
    return None


async def send_command(command: str, window_manager: WindowManagerBase, server_uri: str,
                       no_restore: bool = False, restore_delay: float = 0.3,
                       no_focus: bool = False) -> dict:
    """
    Unity Command ServerにJSONコマンドを送信し、結果を受け取る

    Args:
        command: JSONコマンド文字列
        window_manager: ウィンドウ管理オブジェクト
        server_uri: WebSocket server URI
        no_restore: Trueの場合、元のウィンドウに戻さない
        restore_delay: 元のウィンドウに戻すまでの待機時間（秒）
        no_focus: Trueの場合、Unityをアクティブ化しない（バックグラウンド実行）

    Returns:
        dict: サーバーからの応答（success, result, error, timestamp）
    """
    # no_focusモードではウィンドウ操作をスキップ
    if no_focus:
        print("🔇 No-focus mode: skipping window activation")
    else:
        # 現在のウィンドウを保存
        window_manager.save_current_window()

        # Unityウィンドウを検索（PIDを使用）
        unity_info = await get_unity_info(server_uri)
        if unity_info:
            title = unity_info.get("title")
            pid = unity_info.get("pid")
            window_manager.find_unity_window(title, pid)
        else:
            window_manager.find_unity_window()

        # Unityをアクティブ化
        if window_manager.unity_title:
            print(f"🪟 Activating Unity: {window_manager.unity_title}")
            window_manager.activate_unity()
            time.sleep(0.5)  # ウィンドウ切り替え待機
        else:
            print("⚠ Unity window not found, proceeding anyway")

    try:
        async with websockets.connect(server_uri) as websocket:
            print(f"✓ Connected to {server_uri}")

            # コマンドを送信
            request = {"message": command}
            request_json = json.dumps(request)
            print(f"📤 Sending: {command}")

            await websocket.send(request_json)

            # 結果を待つ（タイムアウト付き）
            print(f"⏳ Waiting for response (timeout: {TIMEOUT_SECONDS}s)...")

            response_str = await asyncio.wait_for(
                websocket.recv(),
                timeout=TIMEOUT_SECONDS
            )

            response = json.loads(response_str)
            return response
    finally:
        # no_focusモードではウィンドウ復元もスキップ
        if no_focus:
            pass  # 何もしない
        elif not no_restore:
            time.sleep(restore_delay)
            print(f"🪟 Restoring original window")
            window_manager.restore_original()
        else:
            print(f"🪟 Keeping Unity in foreground (--no-restore)")


def validate_batch_command(parsed_json: dict) -> tuple:
    """
    バッチコマンドのバリデーション（Python側で実行）

    Args:
        parsed_json: パース済みのJSONコマンド

    Returns:
        (is_valid, error_message): バリデーション結果
    """
    operation = parsed_json.get("operation", "")

    # バッチコマンドでない場合はスキップ
    if operation != "batch":
        return (True, None)

    params = parsed_json.get("params", {})
    commands = params.get("commands", [])

    # コマンド配列のチェック
    if not isinstance(commands, list):
        return (False, "batch params.commands must be an array")

    if len(commands) == 0:
        return (False, "batch params.commands is empty")

    # コマンド数の上限チェック
    if len(commands) > MAX_BATCH_COMMANDS:
        return (False, f"Too many commands in batch: {len(commands)} (max: {MAX_BATCH_COMMANDS})")

    # ネストしたバッチのチェック
    for i, cmd in enumerate(commands):
        if not isinstance(cmd, dict):
            return (False, f"Command at index {i} is not a valid object")

        if cmd.get("operation") == "batch":
            return (False, f"Nested batch not allowed at index {i}")

    return (True, None)


def format_result(response: dict) -> None:
    """結果を整形して表示"""
    success = response.get("success", False)
    timestamp = response.get("timestamp", "")

    if success:
        print(f"\n✓ Command executed successfully")
    else:
        print(f"\n✗ Command failed")

    if timestamp:
        print(f"   Time: {timestamp}")

    # 結果データがあれば表示
    result = response.get("result")
    if result is not None:
        print(f"\n📋 Result:")
        if isinstance(result, dict) or isinstance(result, list):
            print(json.dumps(result, indent=2, ensure_ascii=False))
        else:
            print(f"   {result}")

    # エラーがあれば表示
    error = response.get("error")
    if error:
        print(f"\n❌ Error: {error}")


def main():
    """メイン処理"""
    parser = argparse.ArgumentParser(
        description="Send JSON commands to Unity Command Server",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Single command (default port)
  python send_message.py '{"operation":"get_scene_hierarchy","params":{}}'

  # With explicit port
  python send_message.py --port 8767 '{"operation":"get_scene_hierarchy","params":{}}'

  # With project path (reads port from Library/ClaudeAgent/port.txt)
  python send_message.py --project /path/to/unity/project '{"operation":"get_scene_hierarchy","params":{}}'

  # Batch command (max 20 commands)
  python send_message.py '{"operation":"batch","params":{"commands":[
    {"operation":"create_primitive","params":{"type":"sphere","name":"Ball","color":"red"}},
    {"operation":"transform","params":{"path":"Ball","position":[0,2,0]}}
  ]}}'
"""
    )
    parser.add_argument("command", help="JSON command to send")
    parser.add_argument("--port", "-p", type=int, help=f"Server port (default: {DEFAULT_PORT})")
    parser.add_argument("--project", "-P", help="Unity project path (reads port from Library/ClaudeAgent/port.txt)")
    parser.add_argument("--no-restore", "-n", action="store_true", help="Don't restore original window after command")
    parser.add_argument("--no-focus", "-f", action="store_true", help="Don't activate Unity window (background execution)")
    parser.add_argument("--restore-delay", "-d", type=float, default=0.3, help="Delay before restoring window (default: 0.3s)")

    args = parser.parse_args()

    # サーバーURIを決定
    server_uri = get_server_uri(port=args.port, project=args.project)

    # JSONの検証
    try:
        parsed_json = json.loads(args.command)
    except json.JSONDecodeError as e:
        print(f"✗ Invalid JSON: {e}")
        sys.exit(1)

    # バッチコマンドのバリデーション（Python側でチェック）
    is_valid, error_msg = validate_batch_command(parsed_json)
    if not is_valid:
        print(f"✗ Batch validation error: {error_msg}")
        sys.exit(1)

    # ウィンドウマネージャー（OS別）
    window_manager = create_window_manager()

    try:
        response = asyncio.run(send_command(
            args.command, window_manager, server_uri,
            no_restore=args.no_restore,
            restore_delay=args.restore_delay,
            no_focus=args.no_focus
        ))
        format_result(response)

        # 成功/失敗に応じた終了コード
        sys.exit(0 if response.get("success", False) else 1)

    except ConnectionRefusedError:
        print("✗ Error: Cannot connect to Unity Command Server")
        print(f"  Make sure Unity Editor is running")
        print(f"  Expected server at: {server_uri}")
        sys.exit(1)
    except asyncio.TimeoutError:
        print(f"✗ Error: Timeout ({TIMEOUT_SECONDS}s) waiting for response")
        print("  The command may still be processing in Unity")
        sys.exit(1)
    except Exception as e:
        print(f"✗ Error: {e}")
        sys.exit(1)


if __name__ == "__main__":
    main()
