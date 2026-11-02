using System;
using System.Collections.Generic;

namespace RuinRunner.Backend
{
    // Snapshot the delivered, run-pinned catalog. Recipe data is edited only in
    // Backend/Content; callers cannot mutate it or switch it during an expedition.
    public sealed class PinnedExchangeRecipe
    {
        public string Id { get; }
        public string OutputId { get; }
        public IReadOnlyList<string> InputIds { get; }
        internal PinnedExchangeRecipe(ExchangeRecipe recipe)
        {
            Id=recipe.id;OutputId=recipe.outputId;
            InputIds=Array.AsReadOnly((string[])recipe.inputIds.Clone());
        }
    }
    public static class ExchangeCatalogBinding
    {
        public static IReadOnlyList<PinnedExchangeRecipe> Prepare(ContentCatalog catalog)
        {
            if(catalog?.items==null||string.IsNullOrWhiteSpace(catalog.version))
                throw new ArgumentException("Missing pinned exchange catalog");
            var accessories=new HashSet<string>(StringComparer.Ordinal);
            foreach(var item in catalog.items)
                if(item!=null&&item.kind=="accessory")accessories.Add(item.id);
            var ids=new HashSet<string>(StringComparer.Ordinal);
            var next=new List<PinnedExchangeRecipe>();
            foreach(var recipe in catalog.exchangeRecipes??Array.Empty<ExchangeRecipe>())
            {
                if(recipe==null||string.IsNullOrWhiteSpace(recipe.id)||!ids.Add(recipe.id)
                    ||recipe.inputIds==null||recipe.inputIds.Length!=2
                    ||recipe.inputIds[0]==recipe.inputIds[1]
                    ||!accessories.Contains(recipe.inputIds[0])||!accessories.Contains(recipe.inputIds[1])
                    ||!accessories.Contains(recipe.outputId))
                    throw new ArgumentException("Invalid pinned exchange recipe");
                next.Add(new PinnedExchangeRecipe(recipe));
            }
            return next.AsReadOnly();
        }
    }
}
