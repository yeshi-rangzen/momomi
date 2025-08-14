using static MomomiAPI.Common.Constants.AppConstants;


namespace MomomiAPI.Common.Results
{
    public class LastMessageData
    {
        public Guid Id { get; set; }
        public Guid SenderId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string MessageType { get; set; } = "text"; // text, image, emoji - currently only text is used
        public bool IsRead { get; set; }
        public DateTime SentAt { get; set; }
    }
    public class MatchConversationData
    {
        public Guid ConversationId { get; set; }
        public DateTime MatchedAt { get; set; }
        public Guid OtherUserId { get; set; }
        public bool IsOtherUserActive { get; set; }
        public string? PrimaryPhotoUrl { get; set; }
        public string? OtherUserFirstName { get; set; }
        public string? OtherUserLastName { get; set; }
        public LastMessageData? LastMessage { get; set; }
        public int UnreadCount { get; set; }
        public bool IsFromSuperLike { get; set; }
    }
    public class MatchData
    {
        public List<MatchConversationData> Matches { get; set; } = new List<MatchConversationData>();
        public int TotalCount { get; set; } = 0;
        public bool FromCache { get; set; } = false;
    }

    public class MatchResult : OperationResult<MatchData>
    {
        private MatchResult(bool success, MatchData? data, string? errorCode = null,
            string? errorMessage = null, Dictionary<string, object>? metadata = null)
            : base(success, data, errorCode, errorMessage, metadata)
        {
        }

        public static MatchResult FoundMatches(List<MatchConversationData> matchConvList, bool? fromCache, Dictionary<string, object>? metadata = null)
        {
            var matchData = new MatchData
            {
                Matches = matchConvList,
                FromCache = fromCache ?? false,
                TotalCount = matchConvList.Count
            };

            return new MatchResult(true, matchData, null, null, metadata: metadata);
        }

        public static MatchResult NoMatchesFound()
            => new(true, null);

        public static MatchResult UserNotFound()
            => new(false, null, ErrorCodes.USER_NOT_FOUND, "User not found");

        public static MatchResult Error(string errorMessage)
            => new(false, null, ErrorCodes.INTERNAL_SERVER_ERROR, errorMessage);
    }
}
