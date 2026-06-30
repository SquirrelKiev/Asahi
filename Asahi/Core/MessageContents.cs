namespace Asahi;

public struct MessageContents
{
    public string? body;
    public Embed[]? embeds;
    public MessageComponent? components;

    public MessageContents(string body, Embed[] embeds, ComponentBuilder? components)
    {
        this.body = body;
        this.embeds = embeds;

        this.components = components?.Build();
    }

    public MessageContents(string body = "", Embed? embed = null, ComponentBuilder? components = null)
    {
        this.body = body;
        embeds = embed == null ? null : [embed];

        if (components != null)
            this.components = components.Build();
    }

    public MessageContents(EmbedBuilder embed, ComponentBuilder? components = null, string body = "")
    {
        this.body = body;
        embeds = new[] { embed.Build() };

        if (components != null)
            this.components = components.Build();
    }

    public MessageContents(MessageComponent component)
    {
        this.components = component;
    }

    public MessageContents SetEmbed(Embed embed)
    {
        embeds = new[] { embed };

        return this;
    }
}
