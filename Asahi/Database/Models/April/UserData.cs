namespace Asahi.Database.Models.April;

public class UserData
{
    public ulong UserId { get; set; }
    public ulong GuildId { get; set; }
    public int CoinBalance { get; set; } = 0;
    
    public ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();

    public int AddCoinsToUser(int coinsToAdd)
    {
        if (coinsToAdd < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(coinsToAdd), coinsToAdd, $"{nameof(coinsToAdd)} cannot be less than 0.");
        }

        CoinBalance += coinsToAdd;

        return CoinBalance;
    }

    /// <returns>Whether the attempt to remove coins from the user's account was successful or not. (might fail for debt prevention reasons)</returns>
    /// <exception cref="ArgumentOutOfRangeException">If <see cref="coinsToRemove"/> is below 0.</exception>
    public bool RemoveCoinsFromUser(int coinsToRemove)
    {
        if (coinsToRemove < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(coinsToRemove), coinsToRemove, $"{nameof(coinsToRemove)} cannot be less than 0.");
        }

        var coinCount = CoinBalance;
        coinCount -= coinsToRemove;

        if (coinCount < 0)
        {
            return false;
        }

        CoinBalance = coinCount;
        return true;
    }
}
