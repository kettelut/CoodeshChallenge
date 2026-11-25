using OrderProcessing.Domain;
using Xunit;

namespace OrderProcessing.Domain.Tests;

public class ConcurrencyTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Theory]
    [InlineData("buy")]
    [InlineData("sell")]
    [InlineData("mixed")]
    public async Task Process_ConcurrentSameSymbolOrders_AppliesEveryAcceptedNotional(string scenario)
    {
        var processor = new OrderProcessor();
        OrderRequest[] orders = Enumerable.Range(0, 40)
            .Select(i => TestOrders.Create(
                $"order-{i}", side: scenario == "sell" || (scenario == "mixed" && i % 2 == 1)
                    ? OrderSide.Sell : OrderSide.Buy, quantity: 3m, price: 0.10m))
            .ToArray();

        OrderProcessingResult[] results = await ProcessTogether(processor, orders);

        Assert.All(results, result => Assert.Equal(OrderProcessingStatus.Accepted, result.Status));
        decimal expected = scenario == "buy" ? 12m : scenario == "sell" ? -12m : 0m;
        Assert.Equal(expected, processor.GetExposure("PETR4"));
        Assert.Equal(0m, processor.GetExposure("VALE3"));
    }

    [Fact]
    public async Task Process_ConcurrentDifferentSymbols_MaintainsIndependentBalances()
    {
        var processor = new OrderProcessor();
        string[] symbols = ["PETR4", "VALE3", "VIIA4"];
        OrderRequest[] orders = Enumerable.Range(0, 36)
            .Select(i => TestOrders.Create($"order-{i}", symbols[i % 3],
                i % 3 == 1 ? OrderSide.Sell : OrderSide.Buy, 7m, 0.10m))
            .ToArray();

        OrderProcessingResult[] results = await ProcessTogether(processor, orders);

        Assert.All(results, result => Assert.Equal(OrderProcessingStatus.Accepted, result.Status));
        Assert.Equal(8.40m, processor.GetExposure("PETR4"));
        Assert.Equal(-8.40m, processor.GetExposure("VALE3"));
        Assert.Equal(8.40m, processor.GetExposure("VIIA4"));
    }

    [Theory]
    [InlineData(OrderSide.Buy, 1)]
    [InlineData(OrderSide.Sell, -1)]
    public async Task Process_ConcurrentOrdersCompetingForRemainingCapacity_OnlyOneIsAccepted(OrderSide side, int sign)
    {
        var processor = new OrderProcessor();
        TestOrders.Accepted(processor, TestOrders.Create("seed-1", side: side, quantity: 99_999m, price: 999.99m));
        TestOrders.Accepted(processor, TestOrders.Create("seed-2", side: side, price: 999.99m));
        Assert.Equal(sign * 99_999_000m, processor.GetExposure("PETR4"));
        OrderRequest[] orders =
        [
            TestOrders.Create("a", side: side, quantity: 100m, price: 10m),
            TestOrders.Create("b", side: side, quantity: 100m, price: 10m)
        ];

        OrderProcessingResult[] results = await ProcessTogether(processor, orders);

        Assert.Single(results, result => result.Status == OrderProcessingStatus.Accepted);
        OrderProcessingResult rejected = Assert.Single(results, result => result.Status == OrderProcessingStatus.Rejected);
        Assert.Equal(OrderRejectionReason.ExposureLimitExceeded, rejected.RejectionReason);
        Assert.Equal(sign * 100_000_000m, processor.GetExposure("PETR4"));
        TestOrders.SameDecision(rejected, processor.Process(rejected.Order));
    }

    [Theory]
    [InlineData(OrderSide.Buy, 1)]
    [InlineData(OrderSide.Sell, -1)]
    public async Task Process_ManyIndividuallyValidOrdersEnforceCollectiveLimit(OrderSide side, int sign)
    {
        var processor = new OrderProcessor();
        OrderRequest[] orders = Enumerable.Range(0, 20)
            .Select(i => TestOrders.Create($"large-{i}", side: side, quantity: 99_999m, price: 100m))
            .ToArray();

        OrderProcessingResult[] results = await ProcessTogether(processor, orders);

        Assert.Equal(10, results.Count(result => result.Status == OrderProcessingStatus.Accepted));
        OrderProcessingResult[] rejected = results.Where(result => result.Status == OrderProcessingStatus.Rejected).ToArray();
        Assert.Equal(10, rejected.Length);
        Assert.All(rejected, result => Assert.Equal(OrderRejectionReason.ExposureLimitExceeded, result.RejectionReason));
        Assert.Equal(sign * 99_999_000m, processor.GetExposure("PETR4"));
        Assert.InRange(processor.GetExposure("PETR4"), -100_000_000m, 100_000_000m);
    }

    [Fact]
    public async Task Process_ConcurrentIdenticalRequests_ReplaysOneDecisionAndOneTransition()
    {
        var processor = new OrderProcessor();
        var order = TestOrders.Create("shared", quantity: 100m, price: 1.10m);
        OrderRequest[] orders = Enumerable.Range(0, 24)
            .Select(i => order with { Price = i % 2 == 0 ? 1.10m : 1.1000m })
            .ToArray();

        OrderProcessingResult[] results = await ProcessTogether(processor, orders);

        Assert.All(results, result => TestOrders.SameDecision(results[0], result));
        Assert.Equal(OrderProcessingStatus.Accepted, results[0].Status);
        Assert.Equal(110m, processor.GetExposure("PETR4"));
    }

    [Fact]
    public async Task Process_ConcurrentIdenticalRejectedRequests_ReplayRejectionWithoutTransition()
    {
        var processor = new OrderProcessor();
        TestOrders.SeedOneCentInsideLimit(processor, OrderSide.Buy);
        TestOrders.Accepted(processor, TestOrders.Create("fill", price: 0.01m));
        var order = TestOrders.Create("shared-rejected", price: 0.01m);
        OrderProcessingResult[] results = await ProcessTogether(processor, Enumerable.Repeat(order, 16).ToArray());

        Assert.All(results, result => TestOrders.SameDecision(results[0], result));
        TestOrders.Rejected(results[0], OrderRejectionReason.ExposureLimitExceeded);
        Assert.Equal(100_000_000m, processor.GetExposure("PETR4"));
        TestOrders.Accepted(processor, TestOrders.Create("release", side: OrderSide.Sell, price: 0.01m));
        TestOrders.SameDecision(results[0], processor.Process(order));
        Assert.Equal(99_999_999.99m, processor.GetExposure("PETR4"));
    }

    [Theory]
    [InlineData("VALE3")]
    [InlineData("INVALID")]
    public async Task Process_ConcurrentConflictingCrossSymbolRequests_CommitsOnlyOneOriginal(string otherSymbol)
    {
        var processor = new OrderProcessor();
        var petr = TestOrders.Create("shared", "PETR4", quantity: 100m, price: 10m);
        var other = petr with { Symbol = otherSymbol };
        OrderRequest[] orders = Enumerable.Range(0, 24).Select(i => i % 2 == 0 ? petr : other).ToArray();

        OrderProcessingResult[] results = await ProcessTogether(processor, orders);

        IGrouping<OrderRequest, OrderProcessingResult> winningGroup =
            Assert.Single(results.Where(r => r.Status != OrderProcessingStatus.Conflict).GroupBy(r => r.Order));
        Assert.Equal(12, winningGroup.Count());
        OrderProcessingResult winner = winningGroup.First();
        Assert.All(winningGroup, result => TestOrders.SameDecision(winner, result));
        OrderProcessingResult[] conflicts = results.Where(r => r.Status == OrderProcessingStatus.Conflict).ToArray();
        Assert.Equal(12, conflicts.Length);
        Assert.All(conflicts, result => Assert.Equal(OrderRejectionReason.IdentifierConflict, result.RejectionReason));
        if (winner.Order.Symbol == "INVALID")
        {
            TestOrders.Rejected(winner, OrderRejectionReason.UnsupportedSymbol);
        }
        else
        {
            Assert.Equal(OrderProcessingStatus.Accepted, winner.Status);
            Assert.Null(winner.RejectionReason);
        }

        TestOrders.SameDecision(winner, processor.Process(winner.Order));
        Assert.Equal(OrderProcessingStatus.Conflict, processor.Process(conflicts[0].Order).Status);
        Assert.Equal(winner.Order.Symbol == "PETR4" ? 1000m : 0m, processor.GetExposure("PETR4"));
        Assert.Equal(winner.Order.Symbol == "VALE3" ? 1000m : 0m, processor.GetExposure("VALE3"));
        Assert.Equal(0m, processor.GetExposure("VIIA4"));
    }

    [Theory]
    [InlineData("PETR4", "VALE3")]
    [InlineData("VALE3", "PETR4")]
    [InlineData("INVALID", "PETR4")]
    public async Task Process_SecondWorkerReadyWhileFirstPrepared_PreservesCrossSymbolDecision(string firstSymbol, string secondSymbol)
    {
        using var prepared = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        using var secondAboutToCall = new ManualResetEventSlim(false);
        int firstVisit = 0;
        var processor = new OrderProcessor(stage =>
        {
            if (stage == OrderProcessingStage.AfterExposurePreparation && Interlocked.Exchange(ref firstVisit, 1) == 0)
            {
                prepared.Set();
                if (!release.Wait(Timeout))
                {
                    throw new TimeoutException("Prepared order was not released.");
                }
            }
        });
        var first = TestOrders.Create("shared", firstSymbol, quantity: 100m, price: 10m);
        var second = first with { Symbol = secondSymbol };
        Task<OrderProcessingResult> firstTask = Task.Factory.StartNew(
            () => processor.Process(first), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Task<OrderProcessingResult> secondTask;
        try
        {
            Assert.True(prepared.Wait(Timeout), "First order did not reach the pre-publication point.");
            secondTask = Task.Factory.StartNew(() =>
            {
                secondAboutToCall.Set();
                return processor.Process(second);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            // This signal precedes Process: it does not prove that the worker reached the lock.
            Assert.True(secondAboutToCall.Wait(Timeout), "Second worker did not reach the call site.");
        }
        finally
        {
            release.Set();
        }

        OrderProcessingResult[] results = await Task.WhenAll(firstTask, secondTask).WaitAsync(Timeout);
        if (firstSymbol == "INVALID")
        {
            TestOrders.Rejected(results[0], OrderRejectionReason.UnsupportedSymbol);
        }
        else
        {
            Assert.Equal(OrderProcessingStatus.Accepted, results[0].Status);
        }
        Assert.Equal(OrderProcessingStatus.Conflict, results[1].Status);
        Assert.Equal(OrderRejectionReason.IdentifierConflict, results[1].RejectionReason);
        Assert.False(string.IsNullOrWhiteSpace(results[1].RejectionText));
        TestOrders.SameDecision(results[0], processor.Process(first));
        Assert.Equal(OrderProcessingStatus.Conflict, processor.Process(second).Status);
        Assert.Equal(firstSymbol == "PETR4" ? 1000m : 0m, processor.GetExposure("PETR4"));
        Assert.Equal(firstSymbol == "VALE3" ? 1000m : 0m, processor.GetExposure("VALE3"));
        Assert.Equal(0m, processor.GetExposure("VIIA4"));
    }

    [Theory]
    [InlineData("identical-at-limit")]
    [InlineData("rejected-at-limit")]
    [InlineData("different-symbol")]
    public async Task Process_FailedPreparedOrderThenSecondSubmission_CommitsOnlySecondDecision(string scenario)
    {
        using var firstPrepared = new ManualResetEventSlim(false);
        using var releaseFirst = new ManualResetEventSlim(false);
        using var secondAboutToCall = new ManualResetEventSlim(false);
        using var secondPrepared = new ManualResetEventSlim(false);
        using var releaseSecond = new ManualResetEventSlim(false);
        bool armed = false;
        int visits = 0;
        var processor = new OrderProcessor(stage =>
        {
            if (!armed || stage != OrderProcessingStage.BeforePublication)
            {
                return;
            }

            if (Interlocked.Increment(ref visits) == 1)
            {
                firstPrepared.Set();
                if (!releaseFirst.Wait(Timeout))
                {
                    throw new TimeoutException("First prepared order was not released.");
                }

                throw new InjectedFailureException();
            }

            secondPrepared.Set();
            if (!releaseSecond.Wait(Timeout))
            {
                throw new TimeoutException("Second prepared order was not released.");
            }
        });

        TestOrders.SeedOneCentInsideLimit(processor, OrderSide.Buy);
        if (scenario == "rejected-at-limit")
        {
            TestOrders.Accepted(processor, TestOrders.Create("fill", price: 0.01m));
        }

        var first = TestOrders.Create("shared-failure", price: 0.01m);
        var second = scenario == "different-symbol" ? first with { Symbol = "VALE3" } : first;
        armed = true;
        Task<OrderProcessingResult> firstTask = Task.Factory.StartNew(
            () => processor.Process(first), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Task<OrderProcessingResult>? secondTask = null;
        try
        {
            Assert.True(firstPrepared.Wait(Timeout), "First order did not reach the publication hook.");
            secondTask = Task.Factory.StartNew(() =>
            {
                secondAboutToCall.Set();
                return processor.Process(second);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            // Readiness alone is not evidence that the second Process call is contending.
            Assert.True(secondAboutToCall.Wait(Timeout), "Second worker did not reach the call site.");
            releaseFirst.Set();

            await Assert.ThrowsAsync<InjectedFailureException>(async () => await firstTask.WaitAsync(Timeout));
            Assert.True(secondPrepared.Wait(Timeout), "Second submission did not reach the publication hook.");
            releaseSecond.Set();
            OrderProcessingResult committed = await secondTask.WaitAsync(Timeout);
            Assert.Equal(2, Volatile.Read(ref visits));
            armed = false;

            if (scenario == "rejected-at-limit")
            {
                TestOrders.Rejected(committed, OrderRejectionReason.ExposureLimitExceeded);
                TestOrders.Accepted(processor, TestOrders.Create("release", side: OrderSide.Sell, price: 0.01m));
                TestOrders.SameDecision(committed, processor.Process(first));
                Assert.Equal(99_999_999.99m, processor.GetExposure("PETR4"));
                Assert.Equal(0m, processor.GetExposure("VALE3"));
            }
            else
            {
                Assert.Equal(OrderProcessingStatus.Accepted, committed.Status);
                TestOrders.SameDecision(committed, processor.Process(second));
                if (scenario == "different-symbol")
                {
                    OrderProcessingResult conflict = processor.Process(first);
                    Assert.Equal(OrderProcessingStatus.Conflict, conflict.Status);
                    Assert.Equal(OrderRejectionReason.IdentifierConflict, conflict.RejectionReason);
                    Assert.Equal(99_999_999.99m, processor.GetExposure("PETR4"));
                    Assert.Equal(0.01m, processor.GetExposure("VALE3"));
                }
                else
                {
                    TestOrders.SameDecision(committed, processor.Process(first));
                    Assert.Equal(100_000_000m, processor.GetExposure("PETR4"));
                    Assert.Equal(0m, processor.GetExposure("VALE3"));
                }
            }

            Assert.Equal(0m, processor.GetExposure("VIIA4"));
        }
        finally
        {
            releaseFirst.Set();
            releaseSecond.Set();
            try
            {
                await firstTask.WaitAsync(Timeout);
            }
            catch (InjectedFailureException)
            {
                // This is the expected pre-publication failure.
            }

            if (secondTask is not null)
            {
                await secondTask.WaitAsync(Timeout);
            }
        }
    }

    private sealed class InjectedFailureException : Exception;

    private static async Task<OrderProcessingResult[]> ProcessTogether(OrderProcessor processor, OrderRequest[] orders)
    {
        using var ready = new CountdownEvent(orders.Length);
        using var start = new ManualResetEventSlim(false);
        Task<OrderProcessingResult>[] tasks = orders.Select(order => Task.Factory.StartNew(() =>
        {
            ready.Signal();
            if (!start.Wait(Timeout))
            {
                throw new TimeoutException("Concurrent test start was not released.");
            }

            return processor.Process(order);
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();

        // The start gate coordinates readiness, not entry into the processor's lock.
        try
        {
            Assert.True(ready.Wait(Timeout), "Concurrent workers did not reach the start gate.");
        }
        finally
        {
            start.Set();
        }

        return await Task.WhenAll(tasks).WaitAsync(Timeout);
    }
}
