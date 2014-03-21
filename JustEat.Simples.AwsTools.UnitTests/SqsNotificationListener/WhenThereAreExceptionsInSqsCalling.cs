using System;
using Amazon.SQS;
using Amazon.SQS.Model;
using AwsTools.UnitTests.MessageStubs;
using JustEat.Simples.NotificationStack.AwsTools;
using JustEat.Simples.NotificationStack.Messaging.MessageHandling;
using JustEat.Simples.NotificationStack.Messaging.MessageSerialisation;
using JustEat.Simples.NotificationStack.Messaging.Monitoring;
using JustEat.Testing;
using NSubstitute;
using SimpleMessageMule.TestingFramework;

namespace AwsTools.UnitTests.SqsNotificationListener
{
    public class WhenThereAreExceptionsInSqsCalling : BaseQueuePollingTest
    {
        private int _sqsCallCounter;
        private readonly string _messageTypeString = typeof(GenericMessage).ToString();
        protected override void Given()
        {
            Sqs = Substitute.For<IAmazonSQS>();
            Serialiser = Substitute.For<IMessageSerialiser<GenericMessage>>();
            MessageFootprintStore = Substitute.For<IMessageFootprintStore>();
            SerialisationRegister = Substitute.For<IMessageSerialisationRegister>();
            Monitor = Substitute.For<IMessageMonitor>();
            Handler = Substitute.For<IHandler<GenericMessage>>();
            var response = GenerateResponseMessage(_messageTypeString, Guid.NewGuid());

            SerialisationRegister.GetSerialiser(_messageTypeString).Returns(Serialiser);
            DeserialisedMessage = new GenericMessage { RaisingComponent = "Component" };
            Serialiser.Deserialise(Arg.Any<string>()).Returns(x => DeserialisedMessage);
            Sqs.When(x => x.ReceiveMessage(Arg.Any<ReceiveMessageRequest>()))
                .Do(_ =>
                {
                    _sqsCallCounter++;
                    throw new Exception();
                });
        }

        protected override void When()
        {
            SystemUnderTest.AddMessageHandler(Handler);
            SystemUnderTest.Listen();
            
        }

        [Then]
        public void QueueIsPolledMoreThanOnce()
        {
            Patiently.AssertThat(() => _sqsCallCounter > 1);
        }

        public override void PostAssertTeardown()
        {
            SystemUnderTest.StopListening();
            base.PostAssertTeardown();
        }
    }
}