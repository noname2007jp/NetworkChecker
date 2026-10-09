<!-- File: README.md -->
# NetworkChecker（通信障害チェッカー）

一般利用者向けの通信障害切り分けツールです。
端末 → ルーター → DNS → インターネット → HTTPS サービスの順に段階的に診断し、
障害箇所の「可能性」を推定して表示します。復旧操作や設定変更は一切行いません。

## 特徴

- **単一 EXE（NetworkChecker.exe）のみで動作**（.NET ランタイムのインストール不要）
- ダブルクリックで GUI 起動／ターミナルからの実行で CLI モード（1つの EXE が兼用）
- 診断結果を `result.json` / `report.html` / `logs/` に自動保存
- プライバシー情報（IP・MAC・機名・ユーザー名・内部DNS名）を初期状態でマスク
- 日本語・英語対応（OS の表示言語から自動判定、設定で切替可能）
- 診断結果が外部へ送信されることはありません

## 動作環境

- Windows 10 / 11（x64）
- 管理者権限は不要

## ビルド（GitHub Actions）

- `main` ブランチへの push で自動ビルドされ、Actions の Artifacts から
  `NetworkChecker.exe` をダウンロードできます。
- `v1.0.0` のようなタグを push すると Releases に EXE が添付されます。

## ビルド（ローカル）

.NET 8 SDK が必要です。

    dotnet publish src/NetworkChecker/NetworkChecker.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish

`publish/NetworkChecker.exe`（約 25MB）が生成されます。

## 使い方

### GUI（一般利用者向け）

`NetworkChecker.exe` をダブルクリック →「診断開始」ボタンを押してください。

### コマンド（サポート担当者向け）

ターミナルから実行するとコンソールモードになります。

    .\NetworkChecker.exe --verbose
    .\NetworkChecker.exe --checks dns
    .\NetworkChecker.exe --checks net-004,dns-003
    .\NetworkChecker.exe --output-json result.json
    .\NetworkChecker.exe --lang en

`-Verbose` のような PowerShell 形式の引数も受け付けます。

**注意**: WinExe 形式のため、cmd / PowerShell から直接実行するとプロンプトが
先に返り、出力が前後に表示されることがあります。終了コードを確実に取得するには：

    start /wait "" .\NetworkChecker.exe
    echo %ERRORLEVEL%

### 終了コード

| コード | 意味 |
|---|---|
| 0 | 正常 |
| 1 | 警告または一部失敗 |
| 2 | 通信障害の可能性 |
| 3 | DNS障害の可能性 |
| 4 | 設定エラー |
| 5 | 実行環境エラー |

## 生成ファイル

初回起動時に EXE と同じフォルダへ `config.json` / `targets.json` を展開します
（書き込めない場所では `%LOCALAPPDATA%\NetworkChecker` を使用します）。

| ファイル | 内容 |
|---|---|
| `config.json` | タイムアウト・再試行・機能フラグ等の設定 |
| `targets.json` | 診断対象サービスの一覧 |
| `result.json` | 診断結果（JSON） |
| `report.html` | 診断レポート（ブラウザで表示、マスク解除ボタン付き） |
| `logs/checker-yyyyMMdd.log` | 詳細ログ（logRetentionDays で自動削除） |

## 診断項目

NET-001〜004（アダプター／IP／ゲートウェイ／ルーター疎通）、
DNS-001〜004（使用DNS取得／DNS疎通／名前解決／外部DNS比較）、
WAN-001〜002（外部IP到達／HTTPS接続）、APP-001（指定サービス）、
SYS-001〜002（時刻／プロキシ）の 13 項目を実施します。

## 注意事項

- 本ツールは診断のみを行い、DNS・プロキシ・ルート等の設定を変更しません。
- 総合判定は「可能性」の提示であり、プロバイダ障害の断定には使用できません。
- 障害時は全項目の完了に 30 秒を超える場合があります（タイムアウト設定に依存）。
- report.html のマスク解除はローカルファイル内の表示切替のみです。
  同一のマスク表記（例: 192.168.***.***）に複数の実値がある場合は
  最初の値で表示されます。完全な値は `config.json` で
  `"privacyMode": false` にして再診断してください。