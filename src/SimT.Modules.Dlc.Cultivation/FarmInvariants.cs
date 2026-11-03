using SimT.Contracts.Data;
using SimT.Contracts.Diagnostics;

namespace SimT.Modules.Dlc.Cultivation;

internal static class FarmInvariants
{
    public sealed class CropDatesOrdered : IInvariant
    {
        public string Id => "cultivation.crop.dates_ordered";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong("SELECT COUNT(*) FROM fm_crop WHERE ready_day <= planted_day");
            return bad == 0 ? null : $"{bad} crop(s) are ready on or before the planting day";
        }
    }

    public sealed class OneLiveCropPerPlot : IInvariant
    {
        public string Id => "cultivation.crop.one_live_per_plot";

        public string? Check(IInvariantContext context)
        {
            var duplicates = context.Db.ScalarLong(
                """
                SELECT COUNT(*) FROM (
                    SELECT plot_id FROM fm_crop
                    WHERE status IN ('GROWING', 'READY')
                    GROUP BY plot_id HAVING COUNT(*) > 1
                )
                """);

            return duplicates == 0 ? null : $"{duplicates} plot(s) have more than one live crop";
        }
    }

    public sealed class HarvestedCropsHaveYield : IInvariant
    {
        public string Id => "cultivation.crop.yield_positive";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong("SELECT COUNT(*) FROM fm_crop WHERE yield_qty <= 0");
            return bad == 0 ? null : $"{bad} crop(s) record a non-positive yield";
        }
    }

    public static IEnumerable<IInvariant> All()
    {
        yield return new CropDatesOrdered();
        yield return new OneLiveCropPerPlot();
        yield return new HarvestedCropsHaveYield();
    }
}
