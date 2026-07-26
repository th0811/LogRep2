# LogRep2のバージョンアップと更新手順

この文書では、GitHub Releasesを使ってLogRep2の新しいバージョンを公開し、利用者が更新する方法を説明します。

## この仕組みの全体像

日々の変更は`main`ブランチへコミットします。利用者へ安定版として公開できる段階になったら、プロジェクトのバージョンを更新してGitHub Releaseを作成します。

アプリは`main`の最新コミットではなく、正式公開された最新のGitHub Releaseを確認します。現在より新しいバージョンがある場合、起動時に通知してReleaseページを開くか確認します。

## まずはこの手順を追えばOK

開発者は、新しいバージョンを公開するときに次の順番で作業します。

1. 公開する機能や修正が完成していることを確認する。
2. 新しいバージョン番号を決める。
3. `src/LogRep2.App/LogRep2.App.csproj`の`<Version>`を変更する。
4. バージョン変更をコミットする。
5. `Publish-Release.ps1`を実行して配布用ZIPを作る。
6. ZIPを展開し、アプリが起動することを確認する。
7. バージョン更新コミットにGitタグを付けてプッシュする。
8. GitHub Releaseを作成する。
9. 配布用ZIPとSHA-256ファイルをReleaseへ添付する。
10. Releaseを公開し、アプリが更新を検出することを確認する。

利用者は更新通知を受け取ったら、次のどちらかを選びます。

- 自分でビルドする人：通知されたバージョンのGitタグを取得してビルドする。
- ビルドしない人：GitHub Releaseに添付された配布用ZIPをダウンロードして上書き展開する。

## 開発者が行う作業

### 1. バージョン番号を決める

LogRep2では`メジャー.マイナー.パッチ`形式を使います。

| 変更内容 | 変更例 |
| --- | --- |
| バグ修正、小さな改善 | `0.2.0`から`0.2.1` |
| 新機能、まとまった改善 | `0.2.1`から`0.3.0` |
| 初めて正式版として公開 | `0.9.0`から`1.0.0` |
| 互換性のない大きな変更 | `1.4.0`から`2.0.0` |

日々のコミットごとにバージョンを変更する必要はありません。安定版としてReleaseを公開する段階で変更します。

### 2. プロジェクトのバージョンを変更する

`src/LogRep2.App/LogRep2.App.csproj`を開き、`<Version>`を変更します。

```xml
<Version>0.2.0</Version>
```

ここでは先頭に`v`を付けません。

### 3. バージョン変更をコミットする

```powershell
git status
git add src/LogRep2.App/LogRep2.App.csproj
git commit -m "chore: bump version to 0.2.0"
```

`git status`で内容を確認し、意図しない変更を一緒にコミットしないようにします。

### 4. 配布用ZIPを作成する

リポジトリのルートフォルダーで実行します。

```powershell
.\scripts\Publish-Release.ps1 -Version 0.2.0
```

最初に全テストが実行され、失敗した場合はリリース成果物が作成されません。成功すると`artifacts`に次の成果物が作られます。

```text
artifacts/
├─ LogRep2-0.2.0-win-x64/
├─ LogRep2-0.2.0-win-x64.zip
└─ LogRep2-0.2.0-win-x64.zip.sha256
```

ZIPを作業用フォルダーへ展開し、`LogRep2.exe`の存在、起動、バージョン表示、必要な付属ファイルを確認します。

### 5. Gitタグを作成する

バージョン更新コミットへ、先頭に`v`を付けたタグを作成します。

```powershell
git tag -a v0.2.0 -m "LogRep2 v0.2.0"
git push origin main
git push origin v0.2.0
```

次の番号を必ず一致させます。

| 場所 | 記載例 |
| --- | --- |
| プロジェクトの`<Version>` | `0.2.0` |
| Gitタグ | `v0.2.0` |
| GitHub Release | `v0.2.0` |

公開済みタグは別のコミットへ付け替えません。公開後の修正は`v0.2.1`などの新しいバージョンとして公開します。

### 6. GitHub Releaseを作成する

1. GitHubでリポジトリの`Releases`を開く。
2. `Draft a new release`を押す。
3. `Choose a tag`で作成した`v0.2.0`を選ぶ。
4. タイトルへ`LogRep2 v0.2.0`などと入力する。
5. 新機能、修正、注意事項を本文へ記載する。
6. 配布用ZIPとSHA-256ファイルを添付する。
7. 安定版では`Set as a pre-release`を選択しない。
8. `Publish release`を押す。

Assetsは次のようになります。

```text
Assets
├─ LogRep2-0.2.0-win-x64.zip        ← 一般利用者向け
├─ LogRep2-0.2.0-win-x64.zip.sha256
├─ Source code (zip)                ← GitHubが自動生成
└─ Source code (tar.gz)             ← GitHubが自動生成
```

`Source code (zip)`はビルド済みアプリではありません。Release本文でも、一般利用者は`LogRep2-0.2.0-win-x64.zip`を選ぶよう案内してください。

### 7. 公開後に確認する

- ReleaseがDraftやPrereleaseになっていない。
- タグ、Release、プロジェクトのバージョンが一致している。
- 配布用ZIPをダウンロードできる。
- ZIPからアプリを起動できる。
- 旧バージョンのアプリが新しいReleaseを通知する。
- 通知から正しいReleaseページを開ける。

## 利用者の作業：自分でビルドする場合

この方法にはGitと.NET 8 SDKが必要です。安定版を使う場合は、`main`ではなく通知されたバージョンのGitタグをビルドします。

通知されたバージョンが`v0.2.0`の場合：

```powershell
git fetch --tags
git switch --detach v0.2.0
dotnet build LogRep2.sln -c Release
```

配布版と同じ自己完結型ZIPを作る場合：

```powershell
.\scripts\Publish-Release.ps1 -Version 0.2.0
```

日々の開発を行う`main`へ戻る場合：

```powershell
git switch main
```

`main`の最新版を試す場合は次のように更新できますが、未公開または開発途中の変更を含む可能性があります。

```powershell
git switch main
git pull
dotnet build LogRep2.sln -c Release
```

ローカルでソースを変更している場合は、`git switch`や`git pull`の前に変更をコミットまたは退避してください。

## 利用者の作業：GitHub ReleaseのZIPを使う場合

この方法ではGitや.NET SDKは不要です。

1. LogRep2で収集を停止する。
2. LogRep2を終了し、タスクトレイにも残っていないことを確認する。
3. 更新通知で「はい」を選び、GitHub Releaseを開く。
4. Assetsから`LogRep2-バージョン-win-x64.zip`をダウンロードする。
5. 現在の`LogRep2.settings.json`を別の場所へバックアップする。
6. ダウンロードしたZIPを展開する。
7. 展開したファイルを現在のLogRep2フォルダーへ上書きコピーする。
8. `LogRep2.settings.json`が残っていることを確認する。
9. `LogRep2.exe`を起動する。
10. 収集設定、過去ログ一覧、オーバーレイ位置を確認する。

一般利用者がダウンロードするファイル：

```text
LogRep2-0.2.0-win-x64.zip
```

次のファイルはソースコードなので、ビルドしない利用者向けではありません。

```text
Source code (zip)
Source code (tar.gz)
```

現在のLogRep2は、設定をexeと同じフォルダーの`LogRep2.settings.json`へ保存します。配布ZIPには設定ファイルを含めませんが、操作ミスに備えて更新前にバックアップしてください。

## 開発者向けチェックリスト

- [ ] 公開予定の機能と修正が完成している
- [ ] 新しいバージョン番号を決めた
- [ ] `LogRep2.App.csproj`の`<Version>`を変更した
- [ ] バージョン変更をコミットした
- [ ] `Publish-Release.ps1`が成功した
- [ ] 配布用ZIPを展開して起動を確認した
- [ ] `vX.Y.Z`形式のGitタグを作成した
- [ ] `main`とGitタグをGitHubへプッシュした
- [ ] GitHub Releaseを作成した
- [ ] 配布用ZIPとSHA-256ファイルを添付した
- [ ] Releaseを正式公開した
- [ ] 公開したファイルをダウンロードできる
- [ ] 旧バージョンのアプリで更新通知を確認した
