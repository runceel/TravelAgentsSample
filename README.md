# TravelAgentsSample

[JAZUG 14 周年イベント](https://jazug.connpass.com/event/327273/)の「Semantic KernelのAgent機能試してみた！」でデモをしたコードです。

## ローカルでの実行方法

### 事前準備

1. Azure に Azure OpenAI Service をデプロイしてください。
2. gpt-4o のモデルを gpt-4o という名前でデプロイしてください。
3. 作成した Azure OpenAI Service に対して Azure CLI や Azure Developer CLI などでサインインしているユーザーに「Cognitive Services OpenAI ユーザー」のロールを割り当ててください。

### デモ プログラムの実行

1. このリポジトリをクローンしてください。
2. `TravelAgentsSample.sln` を Visual Stduio 2022 で開いてください
3. `TravelAgentsSample.AppHost` プロジェクトを右クリックしてユーザーシークレットの管理を選択して以下のように、作成した Azure OpenAI Service のエンドポイントを設定してください。
   ```json
    {
        "ConnectionStrings": {
            "openai": "https://<<リソース名>>.openai.azure.com/"
        }
    }
    ```
4. `TravelAgentsSample.AppHost` プロジェクトをデバッグ実行してください。

### 実行後の操作方法

.NET Aspire のダッシュボードが表示されるので `TravelAgentsSample` のエンドポイントをクリックしてください。
デモアプリの画面が表示されるので、テキストボックスに「東京に住んでいるのですが、2泊3日で広島に行きたいです。」など旅行のプランを立てるための基本情報を入力して「問い合わせる」ボタンをクリックしてください。

## エージェントのコード

- `TravelAgentsSample` プロジェクト
  - Blazor Web App プロジェクト。デモアプリの画面が含まれています。
    - `Program.cs` で `Kernel` などのセットアップを行っています。
    - `Components/Pages/Home.razor` と `Home.razor.cs` でエージェントの呼び出しと結果の表示を行っています。
- `TravelAgentsSample.Agents` プロジェクト
  - Agent を定義しています。
    - `AgentFactory.cs` で各種エージェントのプロンプトの定義やエージェントの生成と `AgentGroupChat` の作成を行っています。
    - `HiroshimaDialectPlugin.cs` で広島弁のプラグインを定義しています。
- `TravelAgentsSample.AppHost` プロジェクト
  - .NET Aspire のアプリホストプロジェクト。このプロジェクトをスタートアッププロジェクトに設定して起動してください。
- `TravelAgentsSample.ServiceDefaults` プロジェクト
  - .NET Aspire のサービスデフォルトプロジェクト。OTel の構成などのコードが含まれています。