using Asahi.Database.Models;

namespace Asahi.Modules.Highlights
{
    public static class ThresholdCalculator
    {
        public static int CalculateThreshold(
            HighlightThreshold thresholdConfig,
            IReadOnlyCollection<HighlightsTrackingService.CachedMessage> messages,
            DateTimeOffset messageSentAt,
            out HighlightsTrackingService.ThresholdInfo debugInfo
        )
        {
            Dictionary<ulong, double> userWeights = [];

            var orderedMessages = messages.OrderByDescending(x => x.Timestamp).ToArray();

            var userWeightMessages = orderedMessages
                .Where(x =>
                    x.Timestamp <= messageSentAt
                    && x.Timestamp
                    >= messageSentAt
                    - TimeSpan.FromSeconds(thresholdConfig.UniqueUserMessageMaxAgeSeconds)
                )
                .ToArray();

            foreach (var message in userWeightMessages)
            {
                var userId = message.AuthorId;
                if (userWeights.ContainsKey(userId))
                    continue;

                var timeSinceLastMessage = messageSentAt - message.Timestamp;
                double weight = 1f;

                if (!(timeSinceLastMessage.TotalSeconds <= thresholdConfig.UniqueUserDecayDelaySeconds))
                {
                    weight =
                        1
                        - (
                            timeSinceLastMessage.TotalSeconds
                            - thresholdConfig.UniqueUserDecayDelaySeconds
                        )
                        / (
                            thresholdConfig.UniqueUserMessageMaxAgeSeconds
                            - thresholdConfig.UniqueUserDecayDelaySeconds
                        );
                }

                userWeights.TryAdd(userId, weight);
            }

            var highActivity =
                orderedMessages.Length >= thresholdConfig.HighActivityMessageLookBack
                && (
                    messageSentAt
                    - orderedMessages[thresholdConfig.HighActivityMessageLookBack - 1].Timestamp
                ).TotalSeconds < thresholdConfig.HighActivityMessageMaxAgeSeconds;

            var weightedUserCount = userWeights.Sum(kvp => kvp.Value);

            var highActivityMultiplier = highActivity ? thresholdConfig.HighActivityMultiplier : 1f;

            var rawThreshold =
            (
                thresholdConfig.BaseThreshold
                + weightedUserCount * thresholdConfig.UniqueUserMultiplier
            ) * highActivityMultiplier;

            var thresholdDecimal = rawThreshold % 1;
            var roundedThreshold = Math.Min(
                thresholdConfig.MaxThreshold,
                thresholdDecimal < thresholdConfig.RoundingThreshold
                    ? Math.Floor(rawThreshold)
                    : Math.Ceiling(rawThreshold)
            );

            debugInfo = new HighlightsTrackingService.ThresholdInfo()
            {
                CurrentThreshold = roundedThreshold,
                RawThreshold = rawThreshold,
                WeightedUserCount = weightedUserCount,
                UnweightedUserCount = userWeights.Count,
                IsHighActivity = highActivity,
                TotalCachedMessages = orderedMessages.Length,
                CachedMessagesBeingConsidered = userWeightMessages.Length,
            };

            return (int)roundedThreshold;
        }

        public static HighlightThreshold? ResolveThreshold(HighlightBoard board, ulong channelId, ulong? parentId,
            ulong? categoryId, ulong guildId)
        {
            var threshold = board.Thresholds.FirstOrDefault(x =>
                x.OverrideId == channelId
            );
            
            if (parentId != null)
            {
                threshold ??= board.Thresholds.FirstOrDefault(x =>
                    x.OverrideId == parentId
                );
            }

            if(categoryId != null)
            {
                threshold ??= board.Thresholds.FirstOrDefault(x =>
                    x.OverrideId == categoryId
                );
            }
            
            threshold ??= board.Thresholds.FirstOrDefault(x =>
                x.OverrideId == guildId
            );

            return threshold;
        }
    }
}
