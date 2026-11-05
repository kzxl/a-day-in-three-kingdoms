using SimT.Contracts.Events;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;
using SimT.Core.Events;

namespace SimT.Core.Tests;

public sealed class CommandUriAndEventTests
{
    [Fact]
    public void CommandUri_parses_single_action()
    {
        var uri = CommandUri.Parse("rest");
        Assert.Equal("rest", uri.Action);
        Assert.Null(uri.Target);
        Assert.Null(uri.Parameter);
        Assert.Null(uri.Extra);
        Assert.Equal("rest", uri.ToString());
    }

    [Fact]
    public void CommandUri_parses_action_with_target_and_parameter()
    {
        var uri = CommandUri.Parse("buy:12:10");
        Assert.Equal("buy", uri.Action);
        Assert.Equal("12", uri.Target);
        Assert.Equal("10", uri.Parameter);
        Assert.True(uri.TryGetTargetLong(out var itemId));
        Assert.Equal(12L, itemId);
        Assert.True(uri.TryGetParamInt(out var qty));
        Assert.Equal(10, qty);
        Assert.Equal("buy:12:10", uri.ToString());
    }

    [Fact]
    public void CommandUri_parses_namespaced_modal_action()
    {
        var uri = CommandUri.Parse("item:modal:7");
        Assert.Equal("item", uri.Action);
        Assert.Equal("modal", uri.Target);
        Assert.Equal("7", uri.Parameter);
        Assert.True(uri.TryGetParamLong(out var id));
        Assert.Equal(7L, id);
    }

    [Fact]
    public void CommandUri_handles_empty_or_whitespace()
    {
        var uri = CommandUri.Parse("   ");
        Assert.Equal(string.Empty, uri.Action);
        Assert.Null(uri.Target);
    }

    [Fact]
    public void CommandUri_format_builds_expected_string()
    {
        var formatted = CommandUri.Format("sell", 15, 20);
        Assert.Equal("sell:15:20", formatted);

        var simple = CommandUri.Format("advance");
        Assert.Equal("advance", simple);
    }

    [Fact]
    public void EventPublisher_dispatches_domain_events_to_subscribers()
    {
        var bus = new EventPublisher();
        var received = new List<MarketTradeExecutedEvent>();

        using var sub = bus.Subscribe<MarketTradeExecutedEvent>(evt => received.Add(evt));

        var trade = new MarketTradeExecutedEvent(
            CityId: 1,
            ItemId: 10,
            Quantity: 5,
            PricePerUnit: 25,
            IsBuy: true,
            Date: new GameDate(184, 1, 1));

        bus.Publish(trade);

        Assert.Single(received);
        Assert.Equal(1L, received[0].CityId);
        Assert.Equal(10L, received[0].ItemId);
        Assert.Equal(5, received[0].Quantity);
        Assert.True(received[0].IsBuy);
    }

    [Fact]
    public void EventPublisher_unsubscribes_cleanly_on_dispose()
    {
        var bus = new EventPublisher();
        var callCount = 0;

        var sub = bus.Subscribe<PlayerRelocatedEvent>(_ => callCount++);

        bus.Publish(new PlayerRelocatedEvent(1, 1, 2, new GameDate(184, 1, 2)));
        Assert.Equal(1, callCount);

        sub.Dispose();

        bus.Publish(new PlayerRelocatedEvent(1, 2, 3, new GameDate(184, 1, 3)));
        Assert.Equal(1, callCount); // no further calls
    }
}
