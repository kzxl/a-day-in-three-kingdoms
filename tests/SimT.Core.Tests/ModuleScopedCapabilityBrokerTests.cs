using SimT.Contracts.Modules;
using SimT.Core.Modules;
using Xunit;

namespace SimT.Core.Tests;

public sealed class ModuleScopedCapabilityBrokerTests
{
    private interface IFoo { }
    private interface IBar { }

    private sealed class FooImpl : IFoo { }
    private sealed class BarImpl : IBar { }

    private static ModuleManifest CreateManifest(string id, string[] provides, string[] consumes)
    {
        return new ModuleManifest(
            Id: id,
            Version: "1.0.0",
            ApiVersion: ModuleManifest.CurrentApiVersion,
            Kind: "dlc",
            Requires: Array.Empty<string>(),
            Provides: provides,
            Consumes: consumes,
            Owns: new ModuleOwnership(Array.Empty<string>(), Array.Empty<string>()),
            Screens: Array.Empty<string>());
    }

    [Fact]
    public void Get_allowed_when_capability_is_in_consumes()
    {
        var root = new CapabilityBroker();
        root.Provide("cap.foo", new FooImpl());

        var manifest = CreateManifest("mod.a", provides: [], consumes: ["cap.foo"]);
        var scoped = root.CreateScope(manifest);

        var foo = scoped.Get<IFoo>("cap.foo");
        Assert.NotNull(foo);
    }

    [Fact]
    public void Get_throws_CapabilityAccessDeniedException_when_not_in_consumes()
    {
        var root = new CapabilityBroker();
        root.Provide("cap.foo", new FooImpl());

        var manifest = CreateManifest("mod.a", provides: [], consumes: ["cap.other"]);
        var scoped = root.CreateScope(manifest);

        var ex = Assert.Throws<CapabilityAccessDeniedException>(() => scoped.Get<IFoo>("cap.foo"));
        Assert.Equal("mod.a", ex.ModuleId);
        Assert.Equal("cap.foo", ex.CapabilityId);
        Assert.Equal("consume", ex.Operation);
    }

    [Fact]
    public void TryGet_throws_CapabilityAccessDeniedException_when_not_in_consumes()
    {
        var root = new CapabilityBroker();
        root.Provide("cap.foo", new FooImpl());

        var manifest = CreateManifest("mod.a", provides: [], consumes: []);
        var scoped = root.CreateScope(manifest);

        var ex = Assert.Throws<CapabilityAccessDeniedException>(() => scoped.TryGet<IFoo>("cap.foo", out _));
        Assert.Equal("mod.a", ex.ModuleId);
        Assert.Equal("cap.foo", ex.CapabilityId);
        Assert.Equal("consume", ex.Operation);
    }

    [Fact]
    public void TryGet_returns_false_when_consumed_capability_is_not_provided()
    {
        var root = new CapabilityBroker();
        var manifest = CreateManifest("mod.a", provides: [], consumes: ["cap.foo"]);
        var scoped = root.CreateScope(manifest);

        var found = scoped.TryGet<IFoo>("cap.foo", out var provider);
        Assert.False(found);
        Assert.Null(provider);
    }

    [Fact]
    public void TryGet_returns_true_when_consumed_capability_is_provided()
    {
        var root = new CapabilityBroker();
        root.Provide("cap.foo", new FooImpl());

        var manifest = CreateManifest("mod.a", provides: [], consumes: ["cap.foo"]);
        var scoped = root.CreateScope(manifest);

        var found = scoped.TryGet<IFoo>("cap.foo", out var provider);
        Assert.True(found);
        Assert.NotNull(provider);
    }

    [Fact]
    public void Provide_allowed_when_in_provides_manifest()
    {
        var root = new CapabilityBroker();
        var manifest = CreateManifest("mod.a", provides: ["cap.foo"], consumes: []);
        var scoped = root.CreateScope(manifest);

        scoped.Provide("cap.foo", new FooImpl());

        Assert.True(root.IsProvided("cap.foo"));
    }

    [Fact]
    public void Provide_throws_CapabilityAccessDeniedException_when_not_in_provides()
    {
        var root = new CapabilityBroker();
        var manifest = CreateManifest("mod.a", provides: ["cap.allowed"], consumes: []);
        var scoped = root.CreateScope(manifest);

        var ex = Assert.Throws<CapabilityAccessDeniedException>(() => scoped.Provide("cap.unauthorized", new FooImpl()));
        Assert.Equal("mod.a", ex.ModuleId);
        Assert.Equal("cap.unauthorized", ex.CapabilityId);
        Assert.Equal("provide", ex.Operation);
    }

    [Fact]
    public void Module_can_consume_its_own_provided_capabilities()
    {
        var root = new CapabilityBroker();
        var manifest = CreateManifest("mod.a", provides: ["cap.foo"], consumes: []);
        var scoped = root.CreateScope(manifest);

        scoped.Provide("cap.foo", new FooImpl());

        var foo = scoped.Get<IFoo>("cap.foo");
        Assert.NotNull(foo);

        Assert.True(scoped.TryGet<IFoo>("cap.foo", out var tryFoo));
        Assert.NotNull(tryFoo);

        Assert.True(scoped.IsProvided("cap.foo"));
    }

    [Fact]
    public void IsProvided_throws_when_not_in_consumes()
    {
        var root = new CapabilityBroker();
        var manifest = CreateManifest("mod.a", provides: [], consumes: []);
        var scoped = root.CreateScope(manifest);

        var ex = Assert.Throws<CapabilityAccessDeniedException>(() => scoped.IsProvided("cap.foo"));
        Assert.Equal("mod.a", ex.ModuleId);
        Assert.Equal("cap.foo", ex.CapabilityId);
        Assert.Equal("consume", ex.Operation);
    }
}
