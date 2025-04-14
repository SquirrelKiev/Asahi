namespace April.Config;

public class ConfigFile
{
    public Guid? rollCommandDefaultBox;
    /// <remarks>In seconds.</remarks>
    public int cooldownTime = 60;

    public int rollCommandCost = 10;
    public string coinEmote = "<a:legocoingold:1219485909729149048>";
    public string coinName = "Stud(s)";
    public uint defaultEmbedColor = 0xb78b21;

    public List<RewardBox> boxes = [];
    public List<RewardPool> pools = [];
    public List<ItemCategory> categories = [];
    public List<RewardItem> items = [];
    public List<ShopWare> shopWares = [];
}
