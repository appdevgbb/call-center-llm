using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Text;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using HtmlAgilityPack;

namespace CallCenterChatBot
{
    public class IdentityExtractor
    {
        private readonly ILogger _logger;
        private readonly IKernel _kernel;

        // should probably be done in startup as a singleton/service injection
        private readonly string _pluginDirectory = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Plugins");

        private readonly IDictionary<string, ISKFunction> _semanticPlugins;
        private readonly ISKFunction _identityExtractorPlugin;
        private readonly string _memoryCollectionName = "customer-calls";

        public IdentityExtractor(ILoggerFactory loggerFactory, IKernel kernel)
        {
            _logger = loggerFactory.CreateLogger<IdentityExtractor>();
            _kernel = kernel;
            // should probably be done in startup as a singleton/service injection
            _semanticPlugins = _kernel.ImportSemanticSkillFromDirectory(_pluginDirectory, "SemanticPlugin");
            _identityExtractorPlugin = _semanticPlugins["IdentityExtractor"];

        }

        [Function("IdentityExtractor")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] 
            HttpRequestData req,
            FunctionContext executionContext)
        {
            _logger.LogInformation("IdentityExtractor function triggered.");
            
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            response.WriteString(summaryTextList[0]);

            return response;
        }

        private static void ValidateUri(string docUrl)
        {
            if (string.IsNullOrEmpty(docUrl))
            {
                throw new ArgumentException("docUrl cannot be null or empty");
            }

            if (!Uri.IsWellFormedUriString(docUrl, UriKind.Absolute))
            {
                throw new ArgumentException("docUrl is not a valid absolute uri");
            }
        }

        private static async Task<(string mainDocTitle, string html, string innerTextOutput)> ProcessHtmlDocFromUrl(string docUrl)
        {
            // create a http client request and call docurl endpoint
            var request = new HttpRequestMessage(HttpMethod.Get, docUrl);
            var client = new HttpClient();
            var httpDocResponse = await client.SendAsync(request);
            var html = await httpDocResponse.Content.ReadAsStringAsync();

            // // parse the html with htmlagilitypack
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);
            
            var mainDocTitle = htmlDocument.DocumentNode.SelectSingleNode("//title").InnerText;
            var mainContent = htmlDocument.DocumentNode.SelectSingleNode("//main[@id='main']");

            if (mainContent == null)
            {
                mainContent = htmlDocument.DocumentNode.SelectSingleNode("//body");
            }

            var mainContentHtml = mainContent.InnerHtml;
            var mainContentText = mainContent.InnerText;

            return(mainDocTitle, mainContentHtml, mainContentText);
        }

        private static List<string> ChunkText(string contentText, int maxLinetokens, int maxParagraphTokens)
        {
            // should probably use blingfire here https://www.nuget.org/packages/BlingFireNuget/
            var lineChunks = TextChunker.SplitPlainTextLines(contentText, maxLinetokens);
            var paragraphChunks = TextChunker.SplitPlainTextParagraphs(lineChunks, maxParagraphTokens);

            return paragraphChunks;
        }

        private async Task<string> ExtractCustomerIdentity(string text)
        {
            var context = new ContextVariables();
            
            context.Set("conversation", text);
            
            var extractedInfo = await _kernel.RunAsync(context, _identityExtractorPlugin);
            var responseJson = extractedInfo.ToString();
    
            return responseJson;
        }
    }
}
