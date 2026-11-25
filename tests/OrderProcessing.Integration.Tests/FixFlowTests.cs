using QuickFix;
using QuickFix.Fields;
using Xunit;

namespace OrderProcessing.Integration.Tests;

public sealed class FixFlowTests
{
    [Theory]
    [InlineData("1", 1000)]
    [InlineData("2", -1000)]
    public async Task ValidOrder_RealSession_ReturnsNewReportAndAccountsNotional(string side, int exposure)
    {
        await using var fix = await FixSession.StartAsync();
        Message report = await fix.SendAsync("valid", side);

        Assert.Equal("8", report.Header.GetString(Tags.MsgType));
        Assert.Equal("valid", report.GetString(Tags.ClOrdID));
        Assert.Equal("0", report.GetString(Tags.ExecType));
        Assert.Equal("0", report.GetString(Tags.OrdStatus));
        Assert.Equal("PETR4", report.GetString(Tags.Symbol));
        Assert.Equal(side, report.GetString(Tags.Side));
        Assert.Equal(100m, report.GetDecimal(Tags.OrderQty));
        Assert.Equal(10m, report.GetDecimal(Tags.Price));
        Assert.Equal(0m, report.GetDecimal(Tags.CumQty));
        Assert.Equal(100m, report.GetDecimal(Tags.LeavesQty));
        Assert.Equal(0m, report.GetDecimal(Tags.AvgPx));
        Assert.False(string.IsNullOrWhiteSpace(report.GetString(Tags.OrderID)));
        Assert.False(string.IsNullOrWhiteSpace(report.GetString(Tags.ExecID)));
        Assert.False(string.IsNullOrWhiteSpace(report.GetString(Tags.TransactTime)));
        Assert.Equal((decimal)exposure, fix.Processor.GetExposure("PETR4"));
    }

    [Theory]
    [InlineData("100", "1.101")]
    [InlineData("100000", "10.00")]
    [InlineData("0", "10.00")]
    [InlineData("1.5", "10.00")]
    public async Task InvalidOrder_RealSession_RejectsWithoutExposure(string quantity, string price)
    {
        await using var fix = await FixSession.StartAsync();
        Message report = await fix.SendAsync("invalid", quantity: quantity, price: price);
        AssertRejected(report, "invalid");
        Assert.Equal(0m, fix.Processor.GetExposure("PETR4"));
    }

    [Fact]
    public async Task DuplicateAndConflict_RealSession_PreserveOriginalDecisionAndExposure()
    {
        await using var fix = await FixSession.StartAsync();
        Message original = await fix.SendAsync("same", quantity: "3", price: "1.10");
        Message replay = await fix.SendAsync("same", quantity: "3.0", price: "1.1000");
        Message conflict = await fix.SendAsync("same", quantity: "3", price: "1.10", symbol: "VALE3");
        Message again = await fix.SendAsync("same", quantity: "3", price: "1.10");

        Assert.Equal("0", original.GetString(Tags.ExecType));
        Assert.Equal("0", replay.GetString(Tags.ExecType));
        Assert.Equal(original.GetString(Tags.OrderID), replay.GetString(Tags.OrderID));
        AssertRejected(conflict, "same");
        Assert.Equal(6, conflict.GetInt(Tags.OrdRejReason));
        Assert.Equal(original.GetString(Tags.OrderID), again.GetString(Tags.OrderID));
        Assert.Equal(3.30m, fix.Processor.GetExposure("PETR4"));
        Assert.Equal(0m, fix.Processor.GetExposure("VALE3"));
    }

    [Fact]
    public async Task ExposureLimit_RealSession_RejectsExcessAndReplaysOriginalRejection()
    {
        await using var fix = await FixSession.StartAsync();
        Assert.Equal("0", (await fix.SendAsync("large", quantity: "99999", price: "999.99")).GetString(Tags.ExecType));
        Assert.Equal(99_998_000.01m, fix.Processor.GetExposure("PETR4"));
        Assert.Equal("0", (await fix.SendAsync("at-limit", quantity: "1", price: "999.99")).GetString(Tags.ExecType));
        Assert.Equal("0", (await fix.SendAsync("finish", quantity: "1000", price: "1.00")).GetString(Tags.ExecType));
        Assert.Equal(100_000_000m, fix.Processor.GetExposure("PETR4"));
        Message rejected = await fix.SendAsync("over", quantity: "1", price: "0.01");
        AssertRejected(rejected, "over");
        Assert.Equal(100_000_000m, fix.Processor.GetExposure("PETR4"));
        Assert.Equal("0", (await fix.SendAsync("reduce", side: "2", quantity: "1", price: "0.01")).GetString(Tags.ExecType));
        Message replay = await fix.SendAsync("over", quantity: "1", price: "0.01");
        AssertRejected(replay, "over");
        Assert.Equal(rejected.GetString(Tags.OrderID), replay.GetString(Tags.OrderID));
        Assert.Equal(99_999_999.99m, fix.Processor.GetExposure("PETR4"));
    }

    [Fact]
    public async Task MissingLimitPrice_IsProtocolRejectedWithoutConsumingIdentifier()
    {
        await using var fix = await FixSession.StartAsync();
        Message rejected = await fix.SendAsync("correctable", price: null);
        Assert.Contains(rejected.Header.GetString(Tags.MsgType), new[] { "3", "j" });
        Assert.Equal(0m, fix.Processor.GetExposure("PETR4"));
        Message corrected = await fix.SendAsync("correctable");
        Assert.Equal("8", corrected.Header.GetString(Tags.MsgType));
        Assert.Equal("correctable", corrected.GetString(Tags.ClOrdID));
        Assert.Equal("0", corrected.GetString(Tags.ExecType));
        Assert.Equal(1000m, fix.Processor.GetExposure("PETR4"));
    }

    [Theory]
    [InlineData("1", "0.00000000000000000000000000001", 44)]
    [InlineData("1", "1.10000000000000000000000000001", 44)]
    [InlineData("1", "79228162514264337593543950336", 44)]
    [InlineData("79228162514264337593543950336", "1.00", 38)]
    [InlineData("0.00000000000000000000000000001", "1.00", 38)]
    public async Task ExtremeNumericValue_StandardDictionaryPeerReceivesRejectedReportWithoutUnrepresentableField(
        string quantity, string price, int omittedTag)
    {
        await using var fix = await FixSession.StartAsync();
        Message report = await fix.SendAsync("extreme", quantity: quantity, price: price);
        AssertRejected(report, "extreme");
        Assert.False(report.IsSetField(omittedTag));
        Assert.Equal(0m, fix.Processor.GetExposure("PETR4"));
    }

    [Theory]
    [InlineData("1.2.3")]
    [InlineData("1e2")]
    public async Task MalformedPrice_RealSession_IsProtocolRejectedAndIdentifierRemainsAvailable(string price)
    {
        await using var fix = await FixSession.StartAsync();
        Message rejected = await fix.SendAsync("malformed", price: price);
        Assert.Contains(rejected.Header.GetString(Tags.MsgType), new[] { "3", "j" });
        Assert.Equal(0m, fix.Processor.GetExposure("PETR4"));
        Message corrected = await fix.SendAsync("malformed", price: "10.00");
        Assert.Equal("0", corrected.GetString(Tags.ExecType));
        Assert.Equal(1000m, fix.Processor.GetExposure("PETR4"));
    }

    [Fact]
    public async Task HighScaleEquivalentPrice_IsNotRoundedOrMistakenForConflict()
    {
        await using var fix = await FixSession.StartAsync();
        Message original = await fix.SendAsync("scale", quantity: "1", price: "1.10");
        Message replay = await fix.SendAsync("scale", quantity: "1", price: "1.1000000000000000000000000000000000");
        Assert.Equal("0", replay.GetString(Tags.ExecType));
        Assert.Equal(original.GetString(Tags.OrderID), replay.GetString(Tags.OrderID));
        Assert.Equal(1.10m, fix.Processor.GetExposure("PETR4"));
    }

    [Fact]
    public async Task InitiatorRestart_RealSession_ReconnectsWithPersistedSequencesAndProcessesAnotherOrder()
    {
        await using var fix = await FixSession.StartAsync();
        Message before = await fix.SendAsync("before-reconnect", quantity: "2", price: "10.00");
        Assert.Equal("0", before.GetString(Tags.ExecType));
        var initiatorId = new SessionID("FIX.4.4", fix.InitiatorId, fix.AcceptorId);
        var acceptorId = new SessionID("FIX.4.4", fix.AcceptorId, fix.InitiatorId);
        Session initiator = Assert.IsType<Session>(Session.LookupSession(initiatorId));
        Session acceptor = Assert.IsType<Session>(Session.LookupSession(acceptorId));
        ulong initiatorSent = initiator.NextSenderMsgSeqNum;
        ulong initiatorReceived = initiator.NextTargetMsgSeqNum;
        ulong acceptorSent = acceptor.NextSenderMsgSeqNum;
        ulong acceptorReceived = acceptor.NextTargetMsgSeqNum;
        Assert.True(initiatorSent > 1 && initiatorReceived > 1);
        Assert.True(acceptorSent > 1 && acceptorReceived > 1);
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(fix.Root, "initiator", "store"), "*.seqnums"));
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(fix.Root, "acceptor", "store"), "*.seqnums"));

        await fix.RestartInitiatorAsync();
        initiator = Assert.IsType<Session>(Session.LookupSession(initiatorId));
        acceptor = Assert.IsType<Session>(Session.LookupSession(acceptorId));
        Assert.True(initiator.IsLoggedOn);
        Assert.True(acceptor.IsLoggedOn);
        Assert.True(initiator.NextSenderMsgSeqNum > initiatorSent);
        Assert.True(initiator.NextTargetMsgSeqNum > initiatorReceived);
        Assert.True(acceptor.NextSenderMsgSeqNum > acceptorSent);
        Assert.True(acceptor.NextTargetMsgSeqNum > acceptorReceived);

        Message after = await fix.SendAsync("after-reconnect", quantity: "3", price: "10.00");
        Assert.Equal("after-reconnect", after.GetString(Tags.ClOrdID));
        Assert.Equal("0", after.GetString(Tags.ExecType));
        Assert.Equal("0", after.GetString(Tags.OrdStatus));
        Assert.Equal(0m, after.GetDecimal(Tags.CumQty));
        Assert.Equal(3m, after.GetDecimal(Tags.LeavesQty));
        Assert.Equal(50m, fix.Processor.GetExposure("PETR4"));
    }

    private static void AssertRejected(Message report, string id)
    {
        Assert.Equal("8", report.Header.GetString(Tags.MsgType));
        Assert.Equal(id, report.GetString(Tags.ClOrdID));
        Assert.Equal("8", report.GetString(Tags.ExecType));
        Assert.Equal("8", report.GetString(Tags.OrdStatus));
        Assert.True(report.IsSetField(Tags.OrdRejReason));
        Assert.False(string.IsNullOrWhiteSpace(report.GetString(Tags.Text)));
    }
}
