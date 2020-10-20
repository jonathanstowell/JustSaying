using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JustSaying.AwsTools.MessageHandling;
using JustSaying.Fluent;
using JustSaying.Messaging.MessageHandling;
using JustSaying.Messaging.Middleware.Handle;
using JustSaying.TestingFramework;
using JustSaying.UnitTests.Messaging.Channels.Fakes;
using JustSaying.UnitTests.Messaging.Channels.SubscriptionGroupTests.Support;
using JustSaying.UnitTests.Messaging.Channels.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace JustSaying.UnitTests.Messaging.Channels.SubscriptionGroupTests
{
    [ExactlyOnce(TimeOut = 5)]
    public class ExactlyOnceHandler : InspectableHandler<SimpleMessage>
    {

    }

    public class WhenExactlyOnceIsAppliedToHandler : BaseSubscriptionGroupTests
    {
        private ISqsQueue _queue;
        private readonly int _expectedTimeout = 5;

        public WhenExactlyOnceIsAppliedToHandler(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        { }

        protected override void Given()
        {
            _queue = CreateSuccessfulTestQueue("TestQueue",  new TestMessage());

            Queues.Add(_queue);
            MessageLock = new FakeMessageLock();

            var servicesBuilder = new ServicesBuilder(new MessagingBusBuilder());
            var serviceResolver = new FakeServiceResolver(sc =>
                sc.AddSingleton<IMessageLockAsync>(MessageLock)
                    .AddSingleton<IHandlerAsync<SimpleMessage>>(Handler)
                    .AddLogging(x => x.AddXUnit(OutputHelper)));

            var middlewareBuilder = new HandlerMiddlewareBuilder(serviceResolver, serviceResolver, servicesBuilder);

            var middleware = middlewareBuilder.Configure(pipe =>
            {
                pipe.UseExactlyOnce<SimpleMessage>("a-unique-lock-key", TimeSpan.FromSeconds(5));
                pipe.UseHandler<SimpleMessage>();
            }).Build();

            Middleware = middleware;
        }

        protected override async Task WhenAsync()
        {
            MiddlewareMap.Add<SimpleMessage>(_queue.QueueName, () => Middleware);

            using var cts = new CancellationTokenSource();

            var completion = SystemUnderTest.RunAsync(cts.Token);

            // wait until it's done
            await Patiently.AssertThatAsync(OutputHelper,
                () => Handler.ReceivedMessages.Any());

            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => completion);
        }

        [Fact]
        public void ProcessingIsPassedToTheHandler()
        {
            Handler.ReceivedMessages.ShouldNotBeEmpty();
        }

        [Fact]
        public void MessageIsLocked()
        {
            var messageId = SerializationRegister.DefaultDeserializedMessage().Id.ToString();

            var tempLockRequests = MessageLock.MessageLockRequests.Where(lr => !lr.isPermanent);
            tempLockRequests.Count().ShouldBeGreaterThan(0);
            tempLockRequests.ShouldAllBe(pair =>
                pair.key.Contains(messageId, StringComparison.OrdinalIgnoreCase) &&
                pair.howLong == TimeSpan.FromSeconds(_expectedTimeout));
        }
    }
}
