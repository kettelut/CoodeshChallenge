using OrderProcessing.Domain;
using Xunit;

namespace OrderProcessing.Domain.Tests;

public class OrderTypeAndIdentityTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public void Process_DefaultAndExplicitLimit_ReplaySameAcceptedOrder()
    {
        var processor = new OrderProcessor();
        var defaultLimit = new OrderRequest("limit", "PETR4", OrderSide.Buy, 3m, 1.10m);
        var explicitLimit = defaultLimit with { OrderType = OrderType.Limit, Price = 1.1000m };

        OrderProcessingResult original = TestOrders.Accepted(processor, defaultLimit);
        OrderProcessingResult replay = processor.Process(explicitLimit);

        Assert.Equal(OrderType.Limit, defaultLimit.OrderType);
        TestOrders.SameDecision(original, replay);
        Assert.Equal(3.30m, processor.GetExposure("PETR4"));

        var second = explicitLimit with { ClOrdId = "explicit-limit" };
        OrderProcessingResult explicitResult = TestOrders.Accepted(processor, second);
        Assert.False(string.IsNullOrWhiteSpace(explicitResult.OrderId));
        Assert.NotEqual(original.OrderId, explicitResult.OrderId);
        Assert.Equal(6.60m, processor.GetExposure("PETR4"));
    }

    public static TheoryData<string, decimal?> UnsupportedTypesAndPrices => new()
    {
        { "1", null }, { "1", 10m }, // Market
        { "3", null }, { "3", 10m },
        { "P", null }, { "P", 10m }
    };

    [Theory]
    [MemberData(nameof(UnsupportedTypesAndPrices))]
    public void Process_UnsupportedOrderType_RejectsAndReplaysOriginalRegardlessOfPrice(
        string originalType, decimal? price)
    {
        var processor = new OrderProcessor();
        TestOrders.Accepted(processor, TestOrders.Create("seed", quantity: 2m, price: 5m));
        var order = new OrderRequest("unsupported", "PETR4", OrderSide.Buy, 3m, price,
            OrderType.Unsupported(originalType));

        OrderProcessingResult original = processor.Process(order);
        OrderProcessingResult replay = processor.Process(order with { });

        TestOrders.Rejected(original, OrderRejectionReason.UnsupportedOrderType);
        Assert.Equal(originalType, original.Order.OrderType.UnsupportedValue);
        TestOrders.SameDecision(original, replay);
        Assert.Equal(10m, processor.GetExposure("PETR4"));
        Assert.Equal(0m, processor.GetExposure("VALE3"));
    }

    [Fact]
    public void Process_LimitWithoutPrice_RejectsAndReplaysWithoutExposureChange()
    {
        var processor = new OrderProcessor();
        TestOrders.Accepted(processor, TestOrders.Create("seed", price: 5m));
        var order = new OrderRequest("missing-price", "PETR4", OrderSide.Buy, 3m, null, OrderType.Limit);

        OrderProcessingResult original = processor.Process(order);

        TestOrders.Rejected(original, OrderRejectionReason.InvalidPrice);
        TestOrders.SameDecision(original, processor.Process(order with { }));
        Assert.Equal(5m, processor.GetExposure("PETR4"));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Process_SameIdentifierWithChangedPricePresence_ConflictsAndPreservesOriginal(
        bool unsupportedType, bool originalHasPrice)
    {
        var processor = new OrderProcessor();
        OrderType type = unsupportedType ? OrderType.Unsupported("1") : OrderType.Limit;
        var order = new OrderRequest("shared", "PETR4", OrderSide.Buy, 3m,
            originalHasPrice ? 10m : null, type);
        OrderRequest changed = order with { Price = originalHasPrice ? null : 10m };

        OrderProcessingResult original = processor.Process(order);
        OrderProcessingResult conflict = processor.Process(changed);

        if (unsupportedType)
        {
            TestOrders.Rejected(original, OrderRejectionReason.UnsupportedOrderType);
        }
        else if (!originalHasPrice)
        {
            TestOrders.Rejected(original, OrderRejectionReason.InvalidPrice);
        }
        else
        {
            Assert.Equal(OrderProcessingStatus.Accepted, original.Status);
            Assert.Null(original.RejectionReason);
        }

        Assert.Equal(OrderProcessingStatus.Conflict, conflict.Status);
        Assert.Equal(OrderRejectionReason.IdentifierConflict, conflict.RejectionReason);
        Assert.False(string.IsNullOrWhiteSpace(conflict.RejectionText));
        TestOrders.SameDecision(original, processor.Process(order));
        Assert.Equal(!unsupportedType && originalHasPrice ? 30m : 0m, processor.GetExposure("PETR4"));
        Assert.Equal(0m, processor.GetExposure("VALE3"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Process_SameIdentifierWithChangedType_ConflictsAndPreservesOriginal(bool unsupportedFirst)
    {
        var processor = new OrderProcessor();
        var order = new OrderRequest("shared", "PETR4", OrderSide.Buy, 3m, 10m,
            unsupportedFirst ? OrderType.Unsupported("1") : OrderType.Limit);
        OrderRequest changed = order with
        {
            OrderType = unsupportedFirst ? OrderType.Limit : OrderType.Unsupported("1")
        };

        OrderProcessingResult original = processor.Process(order);
        OrderProcessingResult conflict = processor.Process(changed);

        if (unsupportedFirst)
        {
            TestOrders.Rejected(original, OrderRejectionReason.UnsupportedOrderType);
        }
        else
        {
            Assert.Equal(OrderProcessingStatus.Accepted, original.Status);
            Assert.Null(original.RejectionReason);
        }
        Assert.Equal(OrderProcessingStatus.Conflict, conflict.Status);
        Assert.Equal(OrderRejectionReason.IdentifierConflict, conflict.RejectionReason);
        TestOrders.SameDecision(original, processor.Process(order));
        Assert.Equal(unsupportedFirst ? 0m : 30m, processor.GetExposure("PETR4"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Process_DifferentUnsupportedOriginalValues_ConflictEvenWhenBothWouldBeRejected(bool hasPrice)
    {
        var processor = new OrderProcessor();
        var order = new OrderRequest("shared", "PETR4", OrderSide.Buy, 3m,
            hasPrice ? 10m : null, OrderType.Unsupported("1"));
        var changed = order with { OrderType = OrderType.Unsupported("3") };

        OrderProcessingResult original = processor.Process(order);
        OrderProcessingResult conflict = processor.Process(changed);

        TestOrders.Rejected(original, OrderRejectionReason.UnsupportedOrderType);
        Assert.Equal(OrderProcessingStatus.Conflict, conflict.Status);
        Assert.Equal(OrderRejectionReason.IdentifierConflict, conflict.RejectionReason);
        TestOrders.SameDecision(original, processor.Process(order));
        Assert.Equal(0m, processor.GetExposure("PETR4"));
    }

    [Fact]
    public void Process_DifferentOriginals_HaveDistinctOrderIdsAndStableReplays()
    {
        var processor = new OrderProcessor();
        OrderRequest[] orders =
        [
            new("accepted-1", "PETR4", OrderSide.Buy, 3m, 10m),
            new("accepted-2", "PETR4", OrderSide.Buy, 3m, 10m, OrderType.Limit),
            new("rejected-1", "PETR4", OrderSide.Buy, 3m, null, OrderType.Limit),
            new("rejected-2", "PETR4", OrderSide.Buy, 3m, null, OrderType.Unsupported("1"))
        ];

        OrderProcessingResult[] originals = orders.Select(processor.Process).ToArray();

        Assert.Equal(OrderProcessingStatus.Accepted, originals[0].Status);
        Assert.Equal(OrderProcessingStatus.Accepted, originals[1].Status);
        TestOrders.Rejected(originals[2], OrderRejectionReason.InvalidPrice);
        TestOrders.Rejected(originals[3], OrderRejectionReason.UnsupportedOrderType);
        Assert.All(originals, result => Assert.False(string.IsNullOrWhiteSpace(result.OrderId)));
        Assert.Equal(orders.Length, originals.Select(result => result.OrderId).Distinct().Count());
        for (int i = 0; i < orders.Length; i++)
        {
            TestOrders.SameDecision(originals[i], processor.Process(orders[i]));
        }
        Assert.Equal(60m, processor.GetExposure("PETR4"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Process_SecondWorkerReadyWhileFirstPrepared_PreservesDifferentTypeDecision(bool unsupportedFirst)
    {
        using var prepared = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        using var competingAboutToCall = new ManualResetEventSlim(false);
        var processor = new OrderProcessor(stage =>
        {
            if (stage == OrderProcessingStage.AfterExposurePreparation)
            {
                prepared.Set();
                if (!release.Wait(Timeout))
                {
                    throw new TimeoutException("Prepared order was not released.");
                }
            }
        });
        var first = new OrderRequest("shared", "PETR4", OrderSide.Buy, 3m, 10m,
            unsupportedFirst ? OrderType.Unsupported("1") : OrderType.Limit);
        var competing = first with
        {
            OrderType = unsupportedFirst ? OrderType.Limit : OrderType.Unsupported("1")
        };
        Task<OrderProcessingResult> firstTask = Task.Factory.StartNew(
            () => processor.Process(first), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Task<OrderProcessingResult> competingTask;
        try
        {
            Assert.True(prepared.Wait(Timeout), "First order did not reach the pre-publication point.");
            competingTask = Task.Factory.StartNew(() =>
            {
                competingAboutToCall.Set();
                return processor.Process(competing);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            // This signal precedes Process: it does not prove that the worker reached the lock.
            Assert.True(competingAboutToCall.Wait(Timeout), "Second worker did not reach the call site.");
        }
        finally
        {
            release.Set();
        }

        OrderProcessingResult[] results = await Task.WhenAll(firstTask, competingTask).WaitAsync(Timeout);
        if (unsupportedFirst)
        {
            TestOrders.Rejected(results[0], OrderRejectionReason.UnsupportedOrderType);
        }
        else
        {
            Assert.Equal(OrderProcessingStatus.Accepted, results[0].Status);
            Assert.Null(results[0].RejectionReason);
        }
        Assert.Equal(OrderProcessingStatus.Conflict, results[1].Status);
        Assert.Equal(OrderRejectionReason.IdentifierConflict, results[1].RejectionReason);
        Assert.False(string.IsNullOrWhiteSpace(results[1].RejectionText));
        TestOrders.SameDecision(results[0], processor.Process(first));
        Assert.Equal(OrderProcessingStatus.Conflict, processor.Process(competing).Status);
        Assert.Equal(unsupportedFirst ? 0m : 30m, processor.GetExposure("PETR4"));
        Assert.Equal(0m, processor.GetExposure("VALE3"));
    }
}
