namespace Asahi.BotEmoteManagement;

public class ReflectionBasedBotEmoteManagerService<TEmoteSpecification, TEmoteModel>(IEmoteResolver emoteResolver) where TEmoteModel : class, new()
{
    private TEmoteModel? emotes;
    public TEmoteModel Emotes => emotes ?? throw new InvalidOperationException("InitializeAsync has not been called yet.");
    
    public async Task InitializeAsync(TEmoteSpecification emoteSpecification,
        IList<InternalCustomEmoteTracking> internalEmoteTracking)
    {
        var discoveredEmotes = DiscoverEmoteSpecifications(emoteSpecification);

        var resolvedEmotes = await emoteResolver.ResolveAsync(discoveredEmotes, internalEmoteTracking);

        var model = new TEmoteModel();
        
        foreach (var resolvedEmote in resolvedEmotes)
        {
            typeof(TEmoteModel).GetProperty(resolvedEmote.Key)!.SetValue(model, resolvedEmote.Value);
        }
        
        emotes = model;
    }

    private static List<IEmoteResolver.EmoteSpecMapping> DiscoverEmoteSpecifications(TEmoteSpecification emoteSpecification)
    {
        var discoveredEmotes = new List<IEmoteResolver.EmoteSpecMapping>();

        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var specPropertyInfo in typeof(TEmoteSpecification).GetProperties()
                     .Where(x => x.PropertyType == typeof(IEmoteSpecification)))
        {
            var spec = (specPropertyInfo.GetValue(emoteSpecification) as IEmoteSpecification)!;

            var modelProp = typeof(TEmoteModel).GetProperty(specPropertyInfo.Name);
            if (modelProp == null) continue;

            var discovered = new IEmoteResolver.EmoteSpecMapping(specPropertyInfo.Name, spec);
            discoveredEmotes.Add(discovered);
        }

        return discoveredEmotes;
    }
}
