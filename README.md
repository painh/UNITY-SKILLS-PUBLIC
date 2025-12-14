# Unity Editor Operations Skill

[English](README_EN.md)

> ⚠️ **重要な注意事項**
>
> このコードの大部分はAI（Claude）によって生成されています。そのため、以下のリスクがあることをご理解ください：
> - コードに誤りや未検証の動作が含まれている可能性があります
> - AI操作により予期せぬ削除や変更が発生する可能性があります
> - すべての環境・ユースケースでの動作は保証されません
>
> **本ソフトウェアの使用により生じたいかなる損害についても、作者は一切の責任を負いません。自己責任でご使用ください。**

---

Claude CodeからUnity Editorを操作するためのスキルです。WebSocket通信を使用して、GameObjectの作成、マテリアルの設定、シーン管理など60以上の操作をサポートします。

## 動作環境

| 項目 | 要件 |
|------|------|
| OS | **Windows 11のみ** |
| Unity | 6.x (6000.x) 推奨 |
| Render Pipeline | URP対応 |
| Python | 3.10以上 |
| Claude Code | **Pro以上のプラン必須**（Freeプランでは不可） |

> **注意**: 現在Windows 11でのみ動作確認済みです。Mac/Linuxには対応していません。

## なぜSKILLを使うのか？

このプロジェクトはMCP（Model Context Protocol）ではなくSKILLを採用しています。

| 観点 | SKILL | MCP |
|------|-------|-----|
| セットアップ | ファイルをコピーするだけ | サーバー起動・設定ファイル必要 |
| セキュリティ | すべてのコードが公開・可読 | サーバー内部の動作が不透明な場合あり |
| カスタマイズ | ユーザーが自由に変更可能 | サーバー側の変更が必要 |
| コンテキスト効率 | 必要な時だけ読み込み | 常時接続でコンテキスト消費 |
| デバッグ | コード直接確認可能 | サーバーログ確認が必要 |
| 依存関係 | 最小限（Python + WebSocket） | MCPサーバー実装に依存 |

## インストール

### 1. Unity側のセットアップ

#### 1.1 WebSocketSharpのインストール

1. [websocket-sharp](https://github.com/sta/websocket-sharp)からDLLをダウンロード
   - Releasesから`websocket-sharp.dll`を取得
   - または NuGet: `Install-Package WebSocketSharp -Pre`
2. DLLをUnityプロジェクトの`Assets/Plugins/`フォルダに配置

#### 1.2 Editor拡張のインストール

`Assets/ClaudeAgent/Editor/`フォルダをUnityプロジェクトにコピー

#### 1.3 必須パッケージ

Package Managerから以下をインストール:
- `com.unity.nuget.newtonsoft-json` (JSON.NET)

オプション:
- `com.unity.probuilder` (ProBuilder操作を使用する場合)

#### 1.4 Unity Command Serverの起動

Unity Editorで: **Tools → Unity Command Server**

ウィンドウが開いたら「Start Server」をクリック（ポート8766で起動）

### 2. Claude Code側のセットアップ

#### 2.1 Python依存関係

```bash
pip install -r ClaudeCode/requirements.txt
```

または個別にインストール:
```bash
pip install websockets>=12.0
pip install pywin32>=306
```

#### 2.2 スキルの配置

`ClaudeCode/.claude/skills/unity-editor-operations/`フォルダをClaude Codeのスキルディレクトリにコピー

## 使い方

Claude Codeで`unity-editor-operations`スキルを起動してください。
操作方法やコマンドの詳細はClaude Codeに問い合わせることができます。

## 対応操作

| カテゴリ | 操作数 | 主な機能 |
|---------|-------|---------|
| GameObject | 8 | 作成、削除、検索、親子関係 |
| Transform | 6 | 位置、回転、スケール |
| Component | 6 | 追加、削除、プロパティ設定 |
| Material | 6 | 色、シェーダー、テクスチャ |
| Scene | 4 | 開く、保存、作成 |
| Prefab | 4 | 作成、インスタンス化 |
| Light | 2 | ライト作成・設定 |
| Camera | 2 | カメラ作成・設定 |
| UI | 6 | Canvas、Button、Text等 |
| Animator | 8 | アニメーター制御 |
| Terrain | 6 | 地形作成・編集 |
| ProBuilder | 4 | 3Dメッシュ作成 |
| Screenshot | 2 | スクリーンショット撮影 |

詳細は`ClaudeCode/.claude/skills/unity-editor-operations/`内の各カテゴリの.mdファイルを参照してください。

## トラブルシューティング

### 接続エラー

1. Unity Command Serverが起動しているか確認
2. ポート8766が使用可能か確認
3. ファイアウォール設定を確認

### Unityウィンドウがアクティブにならない

- `pywin32`が正しくインストールされているか確認
- Unityウィンドウのタイトルに「 - Unity 」が含まれているか確認

## ライセンス

MIT License

## 依存ライブラリ

- [websocket-sharp](https://github.com/sta/websocket-sharp) - MIT License
- [Newtonsoft.Json](https://www.newtonsoft.com/json) - MIT License
