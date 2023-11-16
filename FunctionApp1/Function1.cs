// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Azure.Messaging.EventGrid;
using Azure.Storage.Blobs;
using System.IO;
using System.Text.RegularExpressions;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using Azure.Messaging.EventGrid.SystemEvents;
using Newtonsoft.Json.Linq;
using System.Drawing;
using System.Drawing.Imaging;

namespace FunctionApp1
{
    public static class Function1
    {
        private static readonly string BLOB_STORAGE_CONNECTION_STRING = Environment.GetEnvironmentVariable("StgAcctConString");
        [FunctionName("Function1")]
        public static void Run([EventGridTrigger] EventGridEvent eventGridEvent,
            [Blob("{data.url}", FileAccess.Read, Connection = "StgAcctConString")] Stream input, ILogger log, ExecutionContext context)
        {
            var createdEvent = JObject.Parse(eventGridEvent.Data.ToString());
            var extension = Path.GetExtension(createdEvent["url"].Value<string>());

            var imgFormat = GetImageFormat(extension);
            using (Image image = Image.FromStream(input))
            using (Image watermarkImage = Image.FromFile(Path.Combine(context.FunctionAppDirectory, "Watermark.png")))
            using (Graphics imageGraphics = Graphics.FromImage(image))
            using (var output = new MemoryStream())
            using (TextureBrush watermarkBrush = new TextureBrush(watermarkImage))
            {
                int x = (image.Width / 2 - watermarkImage.Width / 2);
                int y = (image.Height / 2 - watermarkImage.Height / 2);
                watermarkBrush.TranslateTransform(x, y);
                imageGraphics.FillRectangle(watermarkBrush, new Rectangle(new Point(x, y), new Size(watermarkImage.Width + 1, watermarkImage.Height)));
                image.Save(output, imgFormat);


                var blobServiceClient = new BlobServiceClient(BLOB_STORAGE_CONNECTION_STRING);
                var blobContainerClient = blobServiceClient.GetBlobContainerClient("watermarked");
                var blobName = GetBlobNameFromUrl(createdEvent["url"].Value<string>());
                output.Position = 0;
                blobContainerClient.UploadBlob(blobName, output);
            }

        }

        private static string GetBlobNameFromUrl(string bloblUrl)
        {
            var uri = new Uri(bloblUrl);
            var blobClient = new BlobClient(uri);
            return blobClient.Name;
        }
        private static ImageFormat GetImageFormat(string extension)
        {

            extension = extension.Replace(".", "");

            var isSupported = Regex.IsMatch(extension, "gif|png|jpe?g", RegexOptions.IgnoreCase);

            if (isSupported)
            {
                switch (extension.ToLower())
                {
                    case "png":
                        return ImageFormat.Png;
                    case "jpg":
                        return ImageFormat.Jpeg;
                    case "jpeg":
                        return ImageFormat.Jpeg;
                    case "gif":
                        return ImageFormat.Gif;
                    default:
                        return null;
                }
            }
            return null;
        }
    }

}
