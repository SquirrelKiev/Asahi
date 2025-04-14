namespace April.Config;

public class RewardItem : UniqueObject
{
    public string name = "Rubber Duck";
    public string description = "Quack.";
    public Guid? categoryGuid;
    public bool shouldAlwaysShowInInventory = false;
    public string imageUrl = "https://files.catbox.moe/7cpezu.webp";
    public string imageSilhouetteUrl = "https://files.catbox.moe/7xeyp6.webp";
    public bool hasUseActions = false;
    public List<RewardActionContainer> useActions = [];
    public bool hasEquipActions = false;
    public List<RewardActionContainer> equipActions = [];
    public List<RewardActionContainer> deEquipActions = [];
    public int sellPrice;
}