using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Extensions.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Logging;

namespace AudioTranscriptionFunction
{
    public static class AudioTranscriptionFunction
    {
        private static string cognitiveServiceKey = Environment.GetEnvironmentVariable("SPEECH_SERVICE_KEY");
        private static string cognitiveServiceRegion = Environment.GetEnvironmentVariable("SPEECH_SERVICE_REGION");

        [Function("AudioTranscriptionFunction")]
        [BlobOutput("transcribbed-files/{name}-output.txt", Connection = "TranscriptionStorage")]
        public static async Task<string> RunAsync([BlobTrigger("audio-files/{name}", Connection = "TranscriptionStorage")] byte[] inputBlob, string name, FunctionContext context)
        {
            var logger = context.GetLogger("AudioTranscriptionFunction");

            var speechConfig = SpeechConfig.FromSubscription(cognitiveServiceKey, cognitiveServiceRegion);
            using (var audioInputStream = AudioInputStream.CreatePushStream())
            {
                using (var stream = new MemoryStream(inputBlob))
                {
                    byte[] buffer = new byte[4096];
                    int bytesRead;
                    while ((bytesRead =stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        audioInputStream.Write(buffer, bytesRead);
                    }
                    audioInputStream.Close();
                }

                speechConfig.SpeechRecognitionLanguage = "en-CA";
                speechConfig.SetProperty(PropertyId.Speech_SegmentationSilenceTimeoutMs, "5000");
                speechConfig.SetProperty(PropertyId.SpeechServiceConnection_InitialSilenceTimeoutMs, "10000");

                var audioConfig = AudioConfig.FromStreamInput(audioInputStream);
                var speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);

            
                var result = await speechRecognizer.RecognizeOnceAsync();

                if (result.Reason == ResultReason.RecognizedSpeech)
                {
                    logger.LogInformation($"Transcribed text: {result.Text}");

                    // Writing transcribed text to the output blob
                    return "result.Text";
                }
                else if (result.Reason == ResultReason.Canceled)
                {
                    var cancellation = CancellationDetails.FromResult(result);
                    logger.LogError($"Speech recognition canceled. Reason: {cancellation.Reason}");

                    if (cancellation.Reason == CancellationReason.Error)
                    {
                        logger.LogError($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                        logger.LogError($"CANCELED: ErrorDetails={cancellation.ErrorDetails}");
                        logger.LogError($"CANCELED: Did you update the subscription info?");
                        throw new InvalidOperationException(cancellation.ErrorDetails);
                    }
                    else
                    {
                        throw new InvalidOperationException("just cuz");
                    }
                }
                else
                {
                    var cancellation = NoMatchDetails.FromResult(result);
                    logger.LogError($"Speech recognition failed. Reason: {result.Reason}");

                    logger.LogError($"FAILED: Reason={cancellation.Reason}");

                    throw new InvalidOperationException(result.Reason.ToString());
                }
            }
        }
    }
}
