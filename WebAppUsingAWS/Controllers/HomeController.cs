using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using WebAppUsingAWS.Models;

namespace WebAppUsingAWS.Controllers
{
    public class HomeController : Controller
    {
        private const string BUCKET_NAME = "borislav-bucket-1";
        private static readonly RegionEndpoint _bucketRegion = RegionEndpoint.USWest1;

        private const string KEY_NAME1 = "borislav-object-1";
        private const string KEY_NAME2 = "borislav-object-2";

        private static IAmazonS3 _s3Client;
        private static IAmazonSQS _sqsClient;
        private static IAmazonSimpleNotificationService _snsClient;
        private static string _environmentPath;
        private readonly ILogger<HomeController> _logger;

        public HomeController(IAmazonS3 s3Client, IAmazonSQS sqsClient, IAmazonSimpleNotificationService snsClient,
                                IWebHostEnvironment hostEnvironment, ILogger<HomeController> logger)
        {
            _s3Client = s3Client;
            _sqsClient = sqsClient;
            _snsClient = snsClient;
            _environmentPath = hostEnvironment.WebRootPath.ToString();
            _logger = logger;
        }
        public async Task<IActionResult> CreateBucket()
        {
            ViewData["Title"] = await CreateBucketAsync();
            return View();
        }

        private async Task<string> CreateBucketAsync()
        {
            string ret;
            try
            {
                string bucketStatus;
                if (await AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, BUCKET_NAME))
                    bucketStatus = $"Bucket '{BUCKET_NAME}' already exists ";
                else
                {
                    var putBucketRequest = new PutBucketRequest
                    {
                        BucketName = BUCKET_NAME,
                        UseClientRegion = true
                    };

                    PutBucketResponse putBucketResponse = await _s3Client.PutBucketAsync(putBucketRequest);
                    bucketStatus = $"New bucket '{BUCKET_NAME}' is created ";
                }

                // Retrieve the bucket location.
                string bucketLocation = await FindBucketLocationAsync();
                string region = RegionEndpoint.GetBySystemName(bucketLocation).DisplayName;
                ret = $"{bucketStatus} in the region of {region}.";
            }
            catch (AmazonS3Exception e)
            {
                ret = $"Error encountered on server. Message:'{e.Message}' when writing an object";
            }
            catch (Exception e)
            {
                ret = $"Unknown error encountered on server. Message:'{e.Message}' when writing an object";
            }

            return ret;
        }

        private static async Task<string> FindBucketLocationAsync()
        {
            var request = new GetBucketLocationRequest() { BucketName = BUCKET_NAME };
            GetBucketLocationResponse response = await _s3Client.GetBucketLocationAsync(request);
            string bucketLocation = response.Location.ToString();
            return bucketLocation;
        }

        public async Task<IActionResult> PutObjects()
        {
            ViewBag.Results = new List<string>();
            ViewBag.Results.AddRange(await WritingObjectsAsync());
            ViewBag.Results.AddRange(await ListingObjectsAsync());

            return View();
        }

        private static async Task<List<string>> WritingObjectsAsync()
        {
            var ret = new List<string>();
            string filePath = _environmentPath + @"\Objects\BorislavTextFile.txt";

            try
            {
                // 1. Put object-specify only key name for the new object.
                var putRequest1 = new PutObjectRequest
                {
                    BucketName = BUCKET_NAME,
                    Key = KEY_NAME1,
                    ContentBody = "sample text from Borislav"
                };
                PutObjectResponse response1 = await _s3Client.PutObjectAsync(putRequest1);
                ret.Add($"Object {putRequest1.Key} uploaded with response {response1.HttpStatusCode}");

                // 2. Put the object-set ContentType and add metadata.
                var putRequest2 = new PutObjectRequest
                {
                    BucketName = BUCKET_NAME,
                    Key = KEY_NAME2,
                    FilePath = filePath,
                    ContentType = "text/plain"
                };
                putRequest2.Metadata.Add("x-amz-meta-title", "someTitle");
                PutObjectResponse response2 = await _s3Client.PutObjectAsync(putRequest2);
                ret.Add($"Object {putRequest2.Key} uploaded with response {response2.HttpStatusCode}");
                ret.Add("");
            }
            catch (AmazonS3Exception e)
            {
                ret.Add($"Error encountered ***. Message:'{e.Message}' when writing an object");
            }
            catch (Exception e)
            {
                ret.Add($"Unknown error encountered on server. Message:'{e.Message}' when writing an object");
            }

            return ret;
        }

        private static async Task<List<string>> ListingObjectsAsync()
        {
            var ret = new List<string>();
            try
            {
                ListObjectsV2Request request = new ListObjectsV2Request
                {
                    BucketName = BUCKET_NAME,
                    MaxKeys = 10
                };
                ret.Add($"Objects on bucket '{request.BucketName}' :");
                ret.Add("");
                ListObjectsV2Response response;
                do
                {
                    response = await _s3Client.ListObjectsV2Async(request);

                    // Process the response.
                    foreach (S3Object entry in response.S3Objects)
                        ret.Add($"Key = {entry.Key}   Size = {entry.Size}");

                    request.ContinuationToken = response.NextContinuationToken;
                } while (response.IsTruncated);
            }
            catch (AmazonS3Exception amazonS3Exception)
            {
                ret.Add($"S3 error occurred. Exception: " + amazonS3Exception.ToString());
            }
            catch (Exception e)
            {
                ret.Add($"Exception: " + e.ToString());
            }

            return ret;
        }

        public async Task<IActionResult> ReadObject()
        {
            ViewBag.Results = new List<string>();
            ViewBag.Results.AddRange(await ReadObjectAsync());

            return View();
        }

        private static async Task<List<string>> ReadObjectAsync()
        {
            var ret = new List<string>();
            try
            {
                GetObjectRequest request = new GetObjectRequest
                {
                    BucketName = BUCKET_NAME,
                    Key = KEY_NAME2
                };
                using (GetObjectResponse response = await _s3Client.GetObjectAsync(request))
                using (Stream responseStream = response.ResponseStream)
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    string title = response.Metadata["x-amz-meta-title"]; // Assume you have "title" as medata added to the object.
                    string contentType = response.Headers["Content-Type"];
                    string content = reader.ReadToEnd();

                    ret.Add($"Object metadata, Title: {title}");
                    ret.Add($"Content type: {contentType}");
                    ret.Add($"Content: {content}");
                }
            }
            catch (AmazonS3Exception e)
            {
                ret.Add($"Error encountered ***. Message:'{e.Message}' when reading an object");
            }
            catch (Exception e)
            {
                ret.Add($"Unknown error encountered on server. Message:'{e.Message}' when reading an object");
            }

            return ret;
        }

        private static async Task<string> DeleteObjectNonVersionedBucketAsync()
        {
            string ret;
            try
            {
                var deleteObjectRequest = new DeleteObjectRequest
                {
                    BucketName = BUCKET_NAME,
                    Key = KEY_NAME2
                };
                
                await _s3Client.DeleteObjectAsync(deleteObjectRequest);
                ret = $"Object {KEY_NAME2} is deleted.";
            }
            catch (AmazonS3Exception e)
            {
                ret = $"Error encountered on server. Message:'{e.Message}' when deleting an object";
            }
            catch (Exception e)
            {
                ret = $"Unknown error encountered on server. Message:'{e.Message}' when deleting an object";
            }

            return ret;
        }

        public async Task<IActionResult> SQS()
        {
            var results = new List<string>();

            var attrs = new Dictionary<string, string>();
            attrs.Add(QueueAttributeName.VisibilityTimeout, "10");
            var createQueueRequest = new CreateQueueRequest
            {
                QueueName = "borislav_queue-1",
                Attributes = attrs
            };
            CreateQueueResponse createQueueResponse = await _sqsClient.CreateQueueAsync(createQueueRequest);
            results.Add($"Queue '{createQueueRequest.QueueName}' created with status {createQueueResponse.HttpStatusCode}");

            var request = new GetQueueUrlRequest
            {
                QueueName = "borislav_queue-1",
                QueueOwnerAWSAccountId = "363590124413"
            };
            GetQueueUrlResponse urlResponse = await _sqsClient.GetQueueUrlAsync(request);

            string url = urlResponse.QueueUrl;
            var sendMessageRequest1 = new SendMessageRequest()
            {
                QueueUrl = url,
                MessageBody = "{SQS_QUEUE_MESSAGE_1}"
            };
            var sendMessageResponse1 = await _sqsClient.SendMessageAsync(sendMessageRequest1);
            results.Add($"Message {sendMessageRequest1.MessageBody} sent with status {sendMessageResponse1.HttpStatusCode}");

            var sendMessageRequest2 = new SendMessageRequest()
            {
                QueueUrl = url,
                MessageBody = "{SQS_QUEUE_MESSAGE_2}"
            };
            var sendMessageResponse2 = await _sqsClient.SendMessageAsync(sendMessageRequest2);
            results.Add($"Message {sendMessageRequest2.MessageBody} sent with status {sendMessageResponse2.HttpStatusCode}");

            var receiveMessageRequest = new ReceiveMessageRequest { QueueUrl = url };
            var receiveMessageResponse1 = await _sqsClient.ReceiveMessageAsync(receiveMessageRequest);
            var receiveMessageResponse2 = await _sqsClient.ReceiveMessageAsync(receiveMessageRequest);
            var receiveMessageResponse3 = await _sqsClient.ReceiveMessageAsync(receiveMessageRequest);

            results.AddRange(await ProcessMessages(receiveMessageResponse1, url));
            results.AddRange(await ProcessMessages(receiveMessageResponse2, url));
            //results.AddRange(await ProcessMessages(receiveMessageResponse3, url));

            ViewBag.Results = new List<string>();
            ViewBag.Results.AddRange(results);

            return View();
        }

        private static async Task<List<string>> ProcessMessages(ReceiveMessageResponse response, string queueUrl)
        {
            var results = new List<string>();

            foreach (var message in response.Messages)
            {
                results.Add($"Message {message.Body} received with status {response.HttpStatusCode}");
                results.Add(await DeleteMessage(results, message, queueUrl));
            }

            return results;
        }

        private static async Task<string> DeleteMessage(List<string> results, Message message, string queueUrl)
        {
            var deleteMessageRequest = new DeleteMessageRequest()
            {
                QueueUrl = queueUrl,
                ReceiptHandle = message.ReceiptHandle
            };
            DeleteMessageResponse delResponse =  await _sqsClient.DeleteMessageAsync(deleteMessageRequest);

            return $"Message {message.Body} deleted with status {delResponse.HttpStatusCode}";
        }

        public async Task<IActionResult> SNS()
        {
            var results = new List<string>();  

            var topicRequest = new CreateTopicRequest { Name = "borislav-topic-1" };
            var topicResponse = await _snsClient.CreateTopicAsync(topicRequest);
            results.Add($"Topic '{topicRequest.Name}' created with status {topicResponse.HttpStatusCode}");

            var topicAttrRequest = new SetTopicAttributesRequest
            {
                TopicArn = topicResponse.TopicArn,
                AttributeName = "DisplayName",
                AttributeValue = "Coding Test Results"
            };
            await _snsClient.SetTopicAttributesAsync(topicAttrRequest);

            var subscribeRequest = new SubscribeRequest
            {
                Endpoint = "borislav.k.slav@gmail.com",
                Protocol = "email",
                TopicArn = topicResponse.TopicArn
            };
            SubscribeResponse subscribeResponse = await _snsClient.SubscribeAsync(subscribeRequest);
            results.Add($"Invitation sent by email with status {subscribeResponse.HttpStatusCode}");
            results.Add("Wait for up to 2 min for the user to confirm the subscription");

            // Wait for up to 2 minutes for the user to confirm the subscription.
            DateTime latest = DateTime.Now + TimeSpan.FromMinutes(2);

            while (DateTime.Now < latest)
            {
                var subsRequest = new ListSubscriptionsByTopicRequest
                {
                    TopicArn = topicResponse.TopicArn
                };

                var subs = _snsClient.ListSubscriptionsByTopicAsync(subsRequest).Result.Subscriptions;

                if (!string.Equals(subs[0].SubscriptionArn, "PendingConfirmation", StringComparison.Ordinal))
                    break;

                // Wait 15 seconds before trying again.
                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(15));
            }

            var publishRequest = new PublishRequest
            {
                Subject = "Coding Test Results for " + DateTime.Today.ToShortDateString(),
                Message = "All of today's coding tests passed",
                TopicArn = topicResponse.TopicArn
            };
            PublishResponse publishResponse = await _snsClient.PublishAsync(publishRequest);
            results.Add($"Message '{publishRequest.Message}' published with status {topicResponse.HttpStatusCode}");

            ViewBag.Results = new List<string>();
            ViewBag.Results.AddRange(results);

            return View();
        }

        public IActionResult Index()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
