#pragma warning disable SKEXP0110
#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0010

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using OpenAI.Chat;

namespace TravelAgentsSample.Agents;

public class AgentFactory(Kernel kernel,
    HiroshimaDialectPlugin hiroshimaDialectPlugin,
    ILoggerFactory? loggerFactory)
{
    private const string TravelExpertAgentName = "travel-expert";
    private const string TravelExpertAgentInstructions = $$$"""
        あなたは優秀な旅行のプランを表す JSON を返すジェネレーターです。
        ユーザーが都市を訪れる際の旅行の計画を1日単位で立ててください。
        旅行の計画には都市のホテル、レストラン、観光スポットを訪れる計画を含めてください。
        レストランは朝食・昼食・夕食を提案してください。
        一度提案を行ったら他の提案は行わず、フィードバックや追加の提案を行わないでください。
        旅行の計画以外の交通手段などは、あなたは専門家ではないので答えないでください。
        旅行プランを立てることのみに集中してください。
        {{{TravelManagerAgentName}}} からの追加指示で対応できるものがある場合のみ対応をしてください。
        
        JSON の形式は budget というキーに予算を、trip というキーに day, hotel, activities のキーを持つオブジェクトの配列で表してください。
        1 日を 1 つのオブジェクトで表してください。
        activities は旅行プランで訪れる観光スポットやレストランを配列形式で指定してください。
        その際に kind でレストランか観光スポットかわかるように指定してください。time で訪れる大体の目安時間を指定してください。

        例:
        {
            "trip": [
                {
                    "day": 1,
                    "hotel": "ホテル名",
                    "activities": [
                        { "kind": "restaurant", "name": "レストラン名", "time": "1時間" },
                        { "kind": "sightseeing", "name": "観光スポット", "time": "2時間" }
                    ]
                },
                {
                    "day": 2,
                    "hotel": "ホテル名",
                    "activities": [
                        { "kind": "restaurant", "name": "レストラン名", "time": "1時間" },
                        { "kind": "sightseeing", "name": "観光スポット", "time": "2時間" },
                        { "kind": "restaurant", "name": "レストラン名", "time": "1時間" },
                        { "kind": "sightseeing", "name": "観光スポット", "time": "2時間" }
                    ]
                }
            ],
            "budget": "¥50,000"
        }
        """;

    private const string FlightExpertAgentName = "flight-expert";
    private const string FlightExpertAgentInstructions = $$$"""
        あなたは飛行機旅行の専門家であり、お客様に最適なフライトプランを表す JSON を返すジェネレーターです。
        フライトプランを提供した後にはユーザーにフィードバックや追加の提案は行わないでください。
        {{{TravelManagerAgentName}}} からの追加指示で対応できるものがある場合のみ対応をしてください。

        JSON は departure と return と budget のキーを持つオブジェクトにしてください。
        departure と return には city, airport, day, time, flightNumber, airline のキーを持つオブジェクトを指定してください。
        budget には予算を指定してください。

        例:
        {
            "departure": { "city": "東京", "airport": "羽田空港", "day": 1, "time": "08:00", "flightNumber": "NH673", "airline": "ANA" },
            "return": { "city": "大阪", "airport": "大阪国際空港", "day": 3, "time": "18:00", "flightNumber": "NH676", "airline": "ANA" },
            "budget": "¥30,000"
        }
        """;

    private const string TravelManagerAgentName = "travel-manager";
    private const string TravelManagerAgentInstructions = """
        あなたは旅行代理店のマネージャーであり、与えられた旅行プランを検証してください。
        旅行プランなどは全て JSON 形式で提供されます。
        
        ### 目標
        プランには、交通手段、宿泊、食事、観光にくわえてユーザーの旅行に関する要望に必要な詳細が含まれていることを確認。
        ユーザーが求めている詳細が含まれている場合は「このプランは承認されました」と回答してユーザーに対して最終的な旅行プランを Markdown のテーブルなどを使ってわかりやすい形で提供してください。
        ユーザーが求めている詳細が含まれていない場合は「このプランは承認されませんでした」と回答して、追加で検討が必要な事項を箇条書きで説明してください。

        ### 求められる振る舞い
        - プランの検証
        - 検証の結果必要な詳細が全て含まれている場合は、プラン全体を Markdown 形式のテーブルと箇条書きにまとめて「このプランは承認されました」と伝える
        - もしそうでない場合は「このプランは承認されませんでした」と伝えて、追加で検討が必要な事項を箇条書きで説明する
        - もし2回続けて同じ追加で検討が必要な事項が有る場合は解決できないものとして扱いプラン全体を Markdown 形式のテーブルと箇条書きにまとめて「このプランは承認されました」と伝える。

        ### 求められない振る舞い
        - 新たなプランの作成
        - プランの修正
        - プランの提案
        - 無駄な雑談
        - さようならや、良い旅をといった挨拶
        """;

    private const string TravelPhraseExpertAgentName = "travel-phrases-expert";
    private const string TravelPhraseExpertAgentInstructions = """
        あなたは旅行に役立つフレーズを表す JSON を返すジェネレーターです。
        利用者が求めているシチュエーションで役に立つフレーズの一覧を提供してください。
        旅行に役立つフレーズを提供した後にはユーザーにフィードバックや追加の提案は行わないでください。
        目標、求められる振る舞い、求められない振る舞いに書いてあることを守ってください。
        あなたに出来ないことは他の人が担当するので安心して自分に求められていることだけに集中してください。

        JSON の形式は phrases というキーを持つオブジェクトにしてください。

        例:
        {
            "phrases": [
                { "situation": "ホテルでのチェックイン", "phrase": "こんにちは、チェックインをしたいのですが。" },
                { "situation": "レストランでの注文", "phrase": "メニューをください。" }
            ]
        }
        """;

    private const string HiroshimaDialectAgentName = "hiroshima-dialect-translator";
    private const string HiroshimaDialectAgentInstructions = """
        あなたは標準語から広島弁への翻訳結果の JSON を返すジェネレーターです。
        これまでの会話の中から、ユーザーが広島弁で話せた方が便利な文章やフレーズを抽出して広島弁に翻訳してください。
        状況を説明するための文章は翻訳対象に含めずにユーザーが実際に話す必要がある言葉だけに絞って翻訳を行ってください。
                        
        JSON の形式は phrases というキーを持つオブジェクトにしてください。
        phrases は標準語 (standard) と広島弁(hiroshima) というキーを持ったオブジェクトを配列形式で指定してください。

        例:
        {
            "phrases": [
                { "standard": "ビールが無くなってますけど？", "hiroshima": "ビールがみてとるけど？" },
                { "standard": "自転車を壊しました。", "hiroshima": "自転車をめいだわ。" }
            ]
        }
        """;

    private ILoggerFactory LoggerFactory => loggerFactory ?? NullLoggerFactory.Instance;

    public AgentGroupChat CreateTravelAgentGroupChat()
    {
        var travelExpertAgent = CreateTravelExpertAgent();
        var travelManagerAgent = CreateTravelManagerAgent();
        return new AgentGroupChat(
            travelExpertAgent,
            CreateFlightExpertAgent(),
            CreateTravelPhraseExpertAgent(),
            CreateHiroshimaDialectAgent(),
            travelManagerAgent)
        {
            ExecutionSettings = new()
            {
                TerminationStrategy = new KernelFunctionTerminationStrategy(
                        CreateTerminationKernelFunction(),
                        kernel)
                {
                    Agents = [travelManagerAgent],
                    ResultParser = result => result.GetValue<string>()?.Trim() == "はい",
                    MaximumIterations = 30,
                },
                SelectionStrategy = new KernelFunctionSelectionStrategy(
                        CreateAgentSelectionKernelFunction(),
                        kernel)
                {
                    InitialAgent = travelExpertAgent,
                    UseInitialAgentAsFallback = true,
                },
            },
            LoggerFactory = LoggerFactory,
        };
    }

    public Agent CreateTravelExpertAgent() =>
        new ChatCompletionAgent
        {
            Name = TravelExpertAgentName,
            Instructions = TravelExpertAgentInstructions,
            Kernel = kernel,
            Arguments = new(new AzureOpenAIPromptExecutionSettings
            {
                Temperature = 0,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
            }),
            LoggerFactory = LoggerFactory,
        };

    public Agent CreateFlightExpertAgent() =>
        new ChatCompletionAgent
        {
            Name = FlightExpertAgentName,
            Instructions = FlightExpertAgentInstructions,
            Kernel = kernel,
            Arguments = new(new AzureOpenAIPromptExecutionSettings
            {
                Temperature = 0,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
            }),
            LoggerFactory = LoggerFactory,
        };

    public Agent CreateTravelManagerAgent() =>
        new ChatCompletionAgent
        {
            Name = TravelManagerAgentName,
            Instructions = TravelManagerAgentInstructions,
            Kernel = kernel,
            Arguments = new(new AzureOpenAIPromptExecutionSettings
            {
                Temperature = 0,
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            }),
            LoggerFactory = LoggerFactory,
        };

    public Agent CreateTravelPhraseExpertAgent() =>
        new ChatCompletionAgent
        {
            Name = TravelPhraseExpertAgentName,
            Instructions = TravelPhraseExpertAgentInstructions,
            Kernel = kernel,
            Arguments = new(new AzureOpenAIPromptExecutionSettings
            {
                Temperature = 0,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
            }),
            LoggerFactory = LoggerFactory,
        };

    public Agent CreateHiroshimaDialectAgent()
    {
        var kernelForHiroshimaDialectAgent = kernel.Clone();
        kernelForHiroshimaDialectAgent.Plugins.AddFromObject(hiroshimaDialectPlugin);
        return new ChatCompletionAgent
        {
            Name = HiroshimaDialectAgentName,
            Instructions = HiroshimaDialectAgentInstructions,
            Kernel = kernelForHiroshimaDialectAgent,
            Arguments = new KernelArguments(new AzureOpenAIPromptExecutionSettings
            {
                Temperature = 0,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            }),
        };
    }

    public KernelFunction CreateTerminationKernelFunction() =>
        kernel.CreateFunctionFromPrompt($$$"""
            旅行プランが承認されたかどうかを確認してください。承認された場合、単語で「はい」と答えてください。
            承認されていない場合は単語で「いいえ」と答えてください。

            History:
            {{${{{KernelFunctionTerminationStrategy.DefaultHistoryVariableName}}}}}
            """);

    public KernelFunction CreateAgentSelectionKernelFunction() =>
        kernel.CreateFunctionFromPrompt($$$"""
            あなたの仕事は、会話の中で最新の参加者の行動に従って、次にどの参加者がターンを取るかを決定することです。
            次にターンを取る参加者の名前のみを述べてください。
            以下のフォーマットで参加者の名前と役割が記載されています。
            { "name": "参加者の名前", "instructions": "参加者の役割" }
            参加者の役割と会話履歴を加味して {{{TravelManagerAgentName}}} の指示を実行するのに最適な参加者を選択してください。

            ### 参加者の名前と役割の一覧
            { "name": "{{{TravelExpertAgentName}}}", "instructions": "{{{TravelExpertAgentInstructions}}}" }
            { "name": "{{{FlightExpertAgentName}}}", "instructions": "{{{FlightExpertAgentInstructions}}}" }
            { "name": "{{{HiroshimaDialectAgentName}}}", "instructions": "{{{HiroshimaDialectAgentInstructions}}}" }
            { "name": "{{{TravelManagerAgentName}}}", "instructions": "{{{TravelManagerAgentInstructions}}}" }
            { "name": "{{{TravelPhraseExpertAgentName}}}", "instructions": "{{{TravelPhraseExpertAgentInstructions}}}" }
            
            ### 常に以下の手順に従って参加者を選択してください
            - ユーザーの入力後は {{{TravelExpertAgentName}}} のターンです。
            - {{{TravelExpertAgentName}}} が応答した後、{{{FlightExpertAgentName}}} のターンです。
            - {{{FlightExpertAgentName}}} が応答した後、ユーザーの要望に対応するために必要な場合は {{{TravelPhraseExpertAgentName}}}, {{{HiroshimaDialectAgentName}}} のターンにしてください。
            - プランが出そろったら {{{TravelManagerAgentName}}} がプランのレビューをして承認します。
            - プランが承認された場合は、会話は終了します。
            - プランが承認された場合は {{{TravelExpertAgentName}}} のターンに戻ります。

            ### 会話履歴：
            {{${{{KernelFunctionSelectionStrategy.DefaultHistoryVariableName}}}}}
            """);
}
