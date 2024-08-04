namespace Asahi.Modules.RssAtomFeed.Models
{
    class UtcTimestampConverter : TimestampConverterBase
    {
        public override long ConvertToSeconds(DateTime dateTime)
        {
            return new DateTimeOffset(dateTime).ToUnixTimeSeconds();
        }

        public override DateTime ParseDateFromSeconds(long seconds)
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
        }
    }
}
