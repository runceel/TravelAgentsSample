using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text;

namespace TravelAgentsSample.Agents;
public class HiroshimaDialectPlugin(
    IHttpClientFactory httpClientFactory,
    ILogger<HiroshimaDialectPlugin> logger)
{
    private static string? _hiroshimaDialectList;

    [KernelFunction]
    [Description("標準語の一覧を広島弁に翻訳します。")]
    public async Task<string> TranslateToHiroshimaDialect(
        [Description("翻訳したい標準語のリスト (Markdown 形式)")]
        string phrases,
        Kernel kernel)
    {
        var translateToHiroshimaDialectFunction = await CreateTranslateToHiroshimaDialectFunction(kernel);
        var hiroshimaDialect = await translateToHiroshimaDialectFunction.InvokeAsync<string>(kernel, new()
        {
            ["input"] = phrases,
        }) ?? "";
        logger.LogInformation("{pharases} を翻訳して {hirosimaDialect} になりました。", phrases, hiroshimaDialect);
        return hiroshimaDialect;
    }

    private async Task<KernelFunction> CreateTranslateToHiroshimaDialectFunction(Kernel kernel)
    {
        if (_hiroshimaDialectList == null)
        {
            var httpClient = httpClientFactory.CreateClient();
            var html = await httpClient.GetStringAsync("https://www.pref.hiroshima.lg.jp/soshiki/19/1178070843217.html");
            var parser = new HtmlParser();
            var doc = await parser.ParseDocumentAsync(html);
            var items = doc.QuerySelectorAll("table tbody tr");
            var sb = new StringBuilder();
            foreach (var item in items)
            {
                var columns = item.QuerySelectorAll("td").ToArray();
                if (columns.Length != 0)
                {
                    sb.AppendLine($"- {columns[1].Text().Trim()}: {columns[0].Text().Trim()}");
                }
            }

            _hiroshimaDialectList = sb.ToString();
        }

        return kernel.CreateFunctionFromPrompt($$$"""
            広島弁の一覧を参考にして、標準語の一覧を、マークダウン形式の標準語と広島弁との対応表に翻訳してください。

            ### 標準語のリスト
            {{$input}}

            ### 広島弁一覧
            {{{_hiroshimaDialectList}}}
            """);
    }
}
