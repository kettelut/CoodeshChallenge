using OrderProcessing.Domain;
using Xunit;

namespace OrderProcessing.Domain.Tests;

public class AtomicityTests
{
    [Theory]
    [InlineData(OrderProcessingStage.BeforeValidation, false)]
    [InlineData(OrderProcessingStage.AfterDecision, false)]
    [InlineData(OrderProcessingStage.AfterExposurePreparation, false)]
    [InlineData(OrderProcessingStage.BeforePublication, false)]
    [InlineData(OrderProcessingStage.BeforeValidation, true)]
    [InlineData(OrderProcessingStage.AfterDecision, true)]
    [InlineData(OrderProcessingStage.AfterExposurePreparation, true)]
    [InlineData(OrderProcessingStage.BeforePublication, true)]
    public void Process_PrecommitFailure_LeavesExposureAndHistoryIntactAndRetryProcesses(
        OrderProcessingStage failureStage, bool wouldReject)
    {
        bool armed = false;
        bool injected = false;
        var processor = new OrderProcessor(stage =>
        {
            if (armed && stage == failureStage)
            {
                armed = false;
                injected = true;
                throw new InjectedFailureException();
            }
        });

        TestOrders.SeedOneCentInsideLimit(processor, OrderSide.Buy);
        TestOrders.Accepted(processor, TestOrders.Create("fill", price: 0.01m));
        var earlier = TestOrders.Create("earlier", "VALE3", OrderSide.Sell, 5m, 2m);
        OrderProcessingResult earlierResult = TestOrders.Accepted(processor, earlier);
        var earlierRejected = TestOrders.Create("earlier-rejected", "VIIA4", price: 1.001m);
        OrderProcessingResult earlierRejectedResult = processor.Process(earlierRejected);
        TestOrders.Rejected(earlierRejectedResult, OrderRejectionReason.InvalidPrice);
        var target = TestOrders.Create("target", "PETR4",
            wouldReject ? OrderSide.Buy : OrderSide.Sell, price: 0.01m);

        armed = true;
        Assert.Throws<InjectedFailureException>(() => processor.Process(target));
        Assert.True(injected);
        Assert.Equal(100_000_000m, processor.GetExposure("PETR4"));
        Assert.Equal(-10m, processor.GetExposure("VALE3"));
        Assert.Equal(0m, processor.GetExposure("VIIA4"));
        TestOrders.SameDecision(earlierResult, processor.Process(earlier));
        TestOrders.SameDecision(earlierRejectedResult, processor.Process(earlierRejected));

        if (wouldReject)
        {
            // After a failed rejected decision, capacity changes; a stale registered
            // rejection would now be observable on retry.
            TestOrders.Accepted(processor, TestOrders.Create("release", side: OrderSide.Sell, price: 0.01m));
        }

        OrderProcessingResult retry = TestOrders.Accepted(processor, target);
        decimal expected = wouldReject ? 100_000_000m : 99_999_999.99m;
        Assert.Equal(expected, processor.GetExposure("PETR4"));
        TestOrders.SameDecision(retry, processor.Process(target));
        OrderProcessingResult conflict = processor.Process(target with { Symbol = "VALE3" });
        Assert.Equal(OrderProcessingStatus.Conflict, conflict.Status);
        Assert.Equal(OrderRejectionReason.IdentifierConflict, conflict.RejectionReason);
        Assert.Equal(expected, processor.GetExposure("PETR4"));
        Assert.Equal(-10m, processor.GetExposure("VALE3"));
    }

    [Fact]
    public void Process_ResponseLostAfterCommit_IdenticalRetryReturnsCommittedResultWithoutNewTransition()
    {
        var processor = new OrderProcessor();
        var order = TestOrders.Create("lost-response", quantity: 3m, price: 1.10m);
        OrderProcessingResult? committed = null;

        void SubmitAndLoseResponse()
        {
            committed = processor.Process(order);
            throw new InjectedFailureException();
        }

        Assert.Throws<InjectedFailureException>(SubmitAndLoseResponse);
        Assert.NotNull(committed);
        Assert.Equal(OrderProcessingStatus.Accepted, committed.Status);
        Assert.Equal(3.30m, processor.GetExposure("PETR4"));
        TestOrders.SameDecision(committed, processor.Process(order with { Price = 1.1000m }));
        Assert.Equal(3.30m, processor.GetExposure("PETR4"));
    }

    private sealed class InjectedFailureException : Exception;
}
