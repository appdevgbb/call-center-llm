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

        public IdentityExtractor(ILoggerFactory loggerFactory, IKernel kernel)
        {
            _logger = loggerFactory.CreateLogger<IdentityExtractor>();
            _kernel = kernel;
            // should probably be done in startup as a singleton/service injection
            _semanticPlugins = _kernel.ImportSemanticSkillFromDirectory(_pluginDirectory, "SemanticPlugin");
            _identityExtractorPlugin = _semanticPlugins["IdentityExtractor"];

        }

        [Function("IdentityExtractor")]
        public async Task<string> RunAsync(
            [BlobTrigger("transcribbed-files/{name}", Connection = "TranscriptionStorage")] Stream inputBlob, string name, FunctionContext context
        )
        {
            _logger.LogInformation("IdentityExtractor function triggered.");
            
             using (StreamReader reader = new StreamReader(inputBlob))
            {
                string text = await reader.ReadToEndAsync();
                var response = await ExtractCustomerIdentity(text);
                _logger.LogInformation($"{response}");
                return response;
            }
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
