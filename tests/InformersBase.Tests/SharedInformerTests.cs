using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Reactive.Testing;
using NSubstitute;
using Steeltoe.Informers.InformersBase.Cache;
using Steeltoe.Informers.InformersBase.Tests.Utils;
using Xunit;
using Xunit.Abstractions;
using static Steeltoe.Informers.InformersBase.EventTypeFlags;

namespace Steeltoe.Informers.InformersBase.Tests
{
    public class SharedInformerTests
    {
        private readonly ILogger _log;

        public SharedInformerTests(ITestOutputHelper output)
        {
            _log = new XunitLogger<SharedInformerTests>(output);
        }

        public static IEnumerable<object[]> GetComputedTestScenarios()
        {
            var scenariosNamesUnaffectedByComputeOptions = InformersTestData.Events.AllScenarios
                .Where(x => !x.Item1.Contains("Sync"))
                .Where(x => Regex.Matches(x.Item1, "Reset").Count < 2);
            // with computed option that shouldn't act any differently then non computed
            foreach (var (description, events) in scenariosNamesUnaffectedByComputeOptions)
                yield return new object[]
                {
                    $"Computed_{description}", // description
                    events,
                    events.ToBasicExpected() // expecting same as master informer
                };
            // yield break;
            yield return new object[]
            {
                $"Computed__{nameof(InformersTestData.Events.ResetWith1_Delay_ResetWith2_ImplicitAddition)}", // description
                InformersTestData.Events.EmptyReset_Delay_Add, // events
                new [] // expected
                {
                    ResourceEvent<string, TestResource>.ResetEmpty,
                    new TestResource("1").ToResourceEvent(Add)
                }
            };

            yield return new object[]
            {
                $"Computed_{nameof(InformersTestData.Events.ResetWith1_Delay_ResetWith2_ImplicitAddition)}", // description
                InformersTestData.Events.ResetWith1_Delay_ResetWith2_ImplicitAddition, // events
                new [] // expected
                {
                    new TestResource("1").ToResourceEvent(ResetStart | ResetEnd),
                    new TestResource("1").ToResourceEvent(ResetStart | ResetEnd),
                    new TestResource("2").ToResourceEvent(Add | Computed),
                }
            };

            yield return new object[]
            {
                $"Computed_{nameof(InformersTestData.Events.ResetWith2_Delay_ResetWith1One_ImplicitDeletion)}", // description
                InformersTestData.Events.ResetWith2_Delay_ResetWith1One_ImplicitDeletion,  // events
                new [] // expected
                {
                    new TestResource("1").ToResourceEvent(ResetStart),
                    new TestResource("2").ToResourceEvent(ResetEnd),
                    new TestResource("2").ToResourceEvent(Delete | Computed),
                    new TestResource("1").ToResourceEvent(ResetStart | ResetEnd)
                }
            };

            yield return new object[]
            {
                $"Computed_ImplicitUpdateAfterReset /w Comparer", // description
                InformersTestData.Events.ResetWith2_Delay_ResetWith2OneDifferentVersion_ImplicitUpdate,  // events
                new [] // expected
                {
                    new TestResource("1",1).ToResourceEvent(ResetStart),
                    new TestResource("2",1).ToResourceEvent(ResetEnd),
                    new ResourceEvent<string, TestResource>( Modify | Computed, "2", new TestResource("2", 2), new TestResource("2", 1)),
                    new TestResource("1", 1).ToResourceEvent(ResetStart | ResetEnd)
                },
                TestResource.KeyVersionComparer
            };
            yield return new object[]
            {
                $"Computed_ImplicitUpdateAfterReset /wo Comparer", // description
                InformersTestData.Events.ResetWith2_Delay_ResetWith2OneDifferentVersion_ImplicitUpdate,  // events
                new [] // expected
                {
                    new TestResource("1",1).ToResourceEvent(ResetStart),
                    new TestResource("2",1).ToResourceEvent(ResetEnd),
                    new TestResource("1",1).ToResourceEvent(ResetStart),
                    new TestResource("2",2).ToResourceEvent(ResetEnd),
                }
            };
        }

        public static IEnumerable<object[]> GetTestScenarios()
        {
            var masterInformerScenarios = InformersTestData.Events.AllScenarios
                .Where(x => !x.Item1.Contains("Sync"))
                .ToList();
            foreach (var (description, events) in masterInformerScenarios)
                yield return new object[]
                {
                    $"{description}", // description
                    events,
                    events.ToBasicExpected() // expecting same as master informer
                };

        }

        [Theory]
        [MemberData(nameof(GetTestScenarios))]
        public async Task FirstSubscriber(string description, ScheduledEvent<TestResource>[] scenario, ResourceEvent<string, TestResource>[] expected)
        {

            _log.LogInformation("===============================================================================");
            _log.LogInformation(description);
            var cache = new SimpleCache<string, TestResource>();
            var masterInformer = Substitute.For<IInformer<string, TestResource>>();
            masterInformer.ListWatch().Returns(scenario.ToTestObservable());
            var sharedInformer = new SharedInformer<string, TestResource>(masterInformer, _log, cache);

            var observable = sharedInformer
                .ListWatch()
                .TimeoutIfNotDebugging()
                .ToList();
            var results = await observable;

            results.Should().NotBeEmpty();
            results.Should().BeEquivalentTo(expected);

        }

        [Fact]
        public void IncompleteResetOnMasterWithException_ReceivedExceptionWithNoData()
        {

            var cache = new SimpleCache<string, TestResource>();
            var masterInformer = Substitute.For<IInformer<string, TestResource>>();
            masterInformer.ListWatch().Returns(
                Observable.Create<ResourceEvent<string, TestResource>>(obs =>
                {
                    obs.OnNext(new TestResource("1").ToResourceEvent(EventTypeFlags.ResetStart));
                    obs.OnError(new TestCompleteException());
                    return Disposable.Empty;
                }));
            var sharedInformer = new SharedInformer<string, TestResource>(masterInformer, _log, cache);

            var observable = sharedInformer
                .ListWatch()
                .TimeoutIfNotDebugging()
                .ToList();

            var dataReceived = false;
            var testComplete = new TaskCompletionSource<bool>();
            observable.Subscribe(
                x => dataReceived = true,
                e => testComplete.TrySetException(e),
                () => testComplete.SetResult(true));

            Func<Task<bool>> act = async () => await testComplete.Task.TimeoutIfNotDebugging();
            act.Should().Throw<TestCompleteException>();
            dataReceived.Should().BeFalse();
        }

        [Fact]
        public async Task WhenSecondSubscriber_ReuseMasterConnection()
        {

            var cache = new SimpleCache<string, TestResource>();
            var masterInformer = Substitute.For<IInformer<string, TestResource>>();
            var scheduler = new TestScheduler();
            masterInformer.ListWatch()
                .Returns(InformersTestData.Events.ResetWith2_Delay_2xUpdateTo1.ToTestObservable(scheduler));

            var sharedInformer = new SharedInformer<string, TestResource>(masterInformer, _log, cache);

            var tcs = new TaskCompletionSource<IList<ResourceEvent<string, TestResource>>>();
            // we attach after first subscription messages are established, but before any "watch" updates come in
            scheduler.ScheduleAbsolute(50, async () =>
            {
                scheduler.Stop();
                // need to pause virtual time brifly since child subscribers runs on separate thread and needs to be in position before we resume sending messages to master
                var _ = Task.Delay(10).ContinueWith(x => scheduler.Start());
                var second = await sharedInformer
                    .ListWatch()
                    .TimeoutIfNotDebugging()
                    .ToList();
                tcs.SetResult(second);
            });

            await sharedInformer
                .ListWatch()
                .TimeoutIfNotDebugging()
                .ToList();

            await masterInformer.Received(1).ListWatch();
        }


        [Theory]
        [MemberData(nameof(GetTestScenarios))]
        public async Task SecondSubscriber(string description, ScheduledEvent<TestResource>[] scenario, ResourceEvent<string, TestResource>[] expected)
        {
            _log.LogInformation("===============================================================================");
            _log.LogInformation(description);
            var cache = new SimpleCache<string, TestResource>();
            var masterInformer = Substitute.For<IInformer<string, TestResource>>();
            var scheduler = new TestScheduler();
            masterInformer.ListWatch()
                .Returns(scenario.ToTestObservable(scheduler));

            var sharedInformer = new SharedInformer<string, TestResource>(masterInformer, _log, cache);

            var tcs = new TaskCompletionSource<IList<ResourceEvent<string, TestResource>>>();
            // we attach after first subscription messages are established, but before any "watch" updates come in
            scheduler.ScheduleAbsolute(50, async () =>
            {
                scheduler.Stop();
                // need to pause virtual time briefly since child subscribers runs on separate thread and needs to be in position before we resume sending messages to master
                var pause = Task.Delay(10).ContinueWith(x => scheduler.Start());
                var second = await sharedInformer
                    .ListWatch()
                    .TimeoutIfNotDebugging()
                    .ToList();
                tcs.SetResult(second);
            });

            await sharedInformer
                .ListWatch()
                .TimeoutIfNotDebugging()
                .ToList();

            var secondResults = await tcs.Task;

            secondResults.Should().NotBeEmpty();
            secondResults.Should().BeEquivalentTo(expected);
        }

        [Theory]
        [MemberData(nameof(GetComputedTestScenarios))]
        public async Task ComputedEvents(string description, ScheduledEvent<TestResource>[] scenario, ResourceEvent<string, TestResource>[] expected, IEqualityComparer<TestResource> comparer = null)
        {
            var masterInformer = Substitute.For<IInformer<string, TestResource>>();
            masterInformer.ListWatch().Returns(scenario.ToTestObservable());
            _log.LogInformation("===============================================================================");
            _log.LogInformation(description);
            var cache = new SimpleCache<string, TestResource>();

            var sharedInformer = new SharedInformer<string, TestResource>(masterInformer, _log, cache);

            var observable = sharedInformer
                .ListWatch()
                .ComputeMissedEventsBetweenResets(comparer)
                .TimeoutIfNotDebugging()
                .ToList();
            var results = await observable;

            results.Should().NotBeEmpty();
            results.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public async Task SubscribeAndUnsubscribe_WhenLastSubscriber_ClosesMasterConnection()
        {
            var cache = new SimpleCache<string, TestResource>();
            var masterInformer = Substitute.For<IInformer<string, TestResource>>();
            var closeCalled = false;
            var scheduler = new TestScheduler();
            masterInformer.ListWatch()
                .Returns(Observable.Create<ResourceEvent<string, TestResource>>(observer =>
                    new CompositeDisposable(InformersTestData.Events
                    .EmptyReset_Delay_Add
                    .ToTestObservable(scheduler)
                    .TimeoutIfNotDebugging()
                    .Subscribe(observer), Disposable.Create(() => closeCalled = true))));
            var sharedInformer = new SharedInformer<string, TestResource>(masterInformer, _log, cache);

            await sharedInformer
                .ListWatch()
                .TimeoutIfNotDebugging()
                .ToList();

            closeCalled.Should().BeTrue();
        }
    }

}
