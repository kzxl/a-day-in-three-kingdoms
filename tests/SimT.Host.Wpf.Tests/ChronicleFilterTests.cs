using System.Reflection;
using System.Windows;
using SimT.Contracts;
using SimT.Contracts.Playing;
using SimT.Host.Wpf.Controls;
using SimT.Testing;

namespace SimT.Host.Wpf.Tests;

public sealed class ChronicleFilterTests
{
    [Fact]
    public void Chronicle_view_filters_events_by_macro_pillars_and_search_query()
    {
        WpfTestContext.RunOnSta(() =>
        {
            using var host = new TestHost("chronicle-filter");
            using var kernel = host.Boot();

            var view = new GameChronicleView();
            view.Bind(kernel);

            // Access internal _allEvents via reflection
            var allEventsField = typeof(GameChronicleView).GetField("_allEvents", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(allEventsField);
            var allEvents = allEventsField.GetValue(view) as System.Collections.IList;
            Assert.NotNull(allEvents);

            // Method GetFilteredEvents
            var getFilteredMethod = typeof(GameChronicleView).GetMethod("GetFilteredEvents", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(getFilteredMethod);

            // 1. All filter returns all events
            view.SetFilter("all");
            var filteredAll = getFilteredMethod.Invoke(view, null) as System.Collections.ICollection;
            Assert.NotNull(filteredAll);
            Assert.Equal(allEvents.Count, filteredAll.Count);

            // 2. Military filter
            view.SetFilter("military");
            var filteredMil = getFilteredMethod.Invoke(view, null) as System.Collections.ICollection;
            Assert.NotNull(filteredMil);

            // 3. Economy filter
            view.SetFilter("economy");
            var filteredEco = getFilteredMethod.Invoke(view, null) as System.Collections.ICollection;
            Assert.NotNull(filteredEco);

            // 4. Social filter
            view.SetFilter("social");
            var filteredSoc = getFilteredMethod.Invoke(view, null) as System.Collections.ICollection;
            Assert.NotNull(filteredSoc);

            // 5. Test search query
            var searchField = typeof(GameChronicleView).GetField("_searchQuery", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(searchField);
            searchField.SetValue(view, "NON_EXISTENT_HERO_NAME_XYZ");
            var filteredNone = getFilteredMethod.Invoke(view, null) as System.Collections.ICollection;
            Assert.NotNull(filteredNone);
            Assert.Empty(filteredNone);

            // Reset search query
            searchField.SetValue(view, string.Empty);
        });
    }
}
