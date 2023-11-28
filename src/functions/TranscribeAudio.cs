using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Logging;

namespace AudioTranscriptionFunction
{
    public static class AudioTranscriptionFunction
    {
        private static string cognitiveServiceKey = Environment.GetEnvironmentVariable("SPEECH_SERVICE_KEY");
        private static string cognitiveServiceRegion = Environment.GetEnvironmentVariable("SPEECH_SERVICE_REGION");
        private static string outputStorageConnectionString = Environment.GetEnvironmentVariable("TranscriptionStorage");
        private static string outputContainerName = Environment.GetEnvironmentVariable("transcribbed-files");

        [Function("AudioTranscriptionFunction")]
        public static async Task RunAsync([BlobTrigger("audio-files/{name}", Connection = "TranscriptionStorage")] Stream inputBlob, string name, FunctionContext context)
        {
            var logger = context.GetLogger("AudioTranscriptionFunction");

            var speechConfig = SpeechConfig.FromSubscription(cognitiveServiceKey, cognitiveServiceRegion);
            using (var audioInputStream = AudioInputStream.CreatePushStream())
            {
                using (var reader = new BinaryReader(inputBlob))
                {
                    byte[] buffer = new byte[4096];
                    int bytesRead;
                    while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
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
                    var outputBlobClient = new BlobClient(outputStorageConnectionString, outputContainerName, $"{name}.txt");
                    await outputBlobClient.UploadAsync(new BinaryData(result.Text), overwrite: true);
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
                    }
                }
                else
                {
                    var cancellation = NoMatchDetails.FromResult(result);
                    logger.LogError($"Speech recognition failed. Reason: {result.Reason}");

                    logger.LogError($"FAILED: Reason={cancellation.Reason}");
                }
            }
        }
    }
}
