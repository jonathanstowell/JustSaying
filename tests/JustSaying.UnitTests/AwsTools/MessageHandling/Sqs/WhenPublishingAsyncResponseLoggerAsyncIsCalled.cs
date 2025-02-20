using System.Net;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.SQS.Model;
using JustSaying.Messaging;
using JustSaying.AwsTools.MessageHandling;
using JustSaying.Messaging.MessageSerialization;
using JustSaying.TestingFramework;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.Core;
using Shouldly;
using Xunit;
using Message = JustSaying.Models.Message;

namespace JustSaying.UnitTests.AwsTools.MessageHandling.Sqs
{
    public class WhenPublishingAsyncResponseLoggerAsyncIsCalled : WhenPublishingTestBase
    {
        private readonly IMessageSerializationRegister _serializationRegister = Substitute.For<IMessageSerializationRegister>();
        private const string Url = "https://blablabla/" + QueueName;
        private readonly SimpleMessage _testMessage = new SimpleMessage { Content = "Hello" };
        private const string QueueName = "queuename";

        private const string MessageId = "TestMessage12345";
        private const string RequestId = "TestRequesteId23456";

        private static MessageResponse _response;
        private static Message _message;

        private protected override async Task<SqsPublisher> CreateSystemUnderTestAsync()
        {
            var sqs = new SqsPublisher(RegionEndpoint.EUWest1, QueueName, Sqs, 0, _serializationRegister, Substitute.For<ILoggerFactory>())
            {
                MessageResponseLogger = (r, m) =>
                {
                    _response = r;
                    _message = m;
                }
            };
            await sqs.ExistsAsync();
            return sqs;
        }

        protected override void Given()
        {
            Sqs.GetQueueUrlAsync(Arg.Any<string>())
                .Returns(new GetQueueUrlResponse { QueueUrl = Url });

            Sqs.GetQueueAttributesAsync(Arg.Any<GetQueueAttributesRequest>())
                .Returns(new GetQueueAttributesResponse());

            _serializationRegister.Serialize(_testMessage, false)
                .Returns("serialized_contents");

            Sqs.SendMessageAsync(Arg.Any<SendMessageRequest>())
                .Returns(PublishResult);
        }

        protected override async Task WhenAsync()
        {
            await SystemUnderTest.PublishAsync(_testMessage);
        }

        private static Task<SendMessageResponse> PublishResult(CallInfo arg)
        {
            var response = new SendMessageResponse
            {
                MessageId = MessageId,
                HttpStatusCode = HttpStatusCode.OK,
                ResponseMetadata = new ResponseMetadata
                {
                    RequestId = RequestId
                }
            };

            return Task.FromResult(response);
        }

        [Fact]
        public void ResponseLoggerIsCalled()
        {
            _response.ShouldNotBeNull();
        }

        [Fact]
        public void ResponseIsForwardedToResponseLogger()
        {
            _response.MessageId.ShouldBe(MessageId);
            _response.HttpStatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public void ResponseShouldContainMetadata()
        {
            _response.ResponseMetadata.ShouldNotBeNull();
            _response.ResponseMetadata.RequestId.ShouldNotBeNull();
            _response.ResponseMetadata.RequestId.ShouldBe(RequestId);
        }

        [Fact]
        public void MessageIsForwardedToResponseLogger()
        {
            _message.ShouldNotBeNull();
        }
    }
}
