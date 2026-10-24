using System.Net;

namespace Back_End.Exceptions
{
    public class GameRuleException : Exception
    {
        public string Code { get; }
        public HttpStatusCode StatusCode { get; }
        public int? CurrentVersion { get; }

        public GameRuleException(string code, string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest, int? currentVersion = null)
            : base(message)
        {
            Code = code;
            StatusCode = statusCode;
            CurrentVersion = currentVersion;
        }

        public static GameRuleException NotYourTurn(int? version = null) =>
            new("NOT_YOUR_TURN", "Không phải lượt của bạn.", HttpStatusCode.BadRequest, version);

        public static GameRuleException InvalidCardCount(int? version = null) =>
            new("INVALID_CARD_COUNT", "Mỗi lượt chỉ được đánh từ 1 đến 3 lá bài.", HttpStatusCode.BadRequest, version);

        public static GameRuleException CardNotOwned(int? version = null) =>
            new("CARD_NOT_OWNED", "Bạn không sở hữu những lá bài này.", HttpStatusCode.BadRequest, version);

        public static GameRuleException CannotChallenge(string message = "Không thể tố cáo ở thời điểm hiện tại.", int? version = null) =>
            new("CANNOT_CHALLENGE", message, HttpStatusCode.BadRequest, version);

        public static GameRuleException AlreadyResolved(int? version = null) =>
            new("ALREADY_RESOLVED", "Lượt đánh này đã được giải quyết.", HttpStatusCode.Conflict, version);

        public static GameRuleException StaleVersion(int currentVersion) =>
            new("STALE_VERSION", "Match state has changed.", HttpStatusCode.Conflict, currentVersion);

        public static GameRuleException DuplicateAction(int? version = null) =>
            new("DUPLICATE_ACTION", "Action ID đã được sử dụng với nội dung khác.", HttpStatusCode.Conflict, version);

        public static GameRuleException MatchNotFound() =>
            new("MATCH_NOT_FOUND", "Không tìm thấy trận đấu.", HttpStatusCode.NotFound);

        public static GameRuleException NotMatchMember() =>
            new("NOT_MATCH_MEMBER", "Người chơi không thuộc trận đấu này.", HttpStatusCode.Forbidden);

        public static GameRuleException InvalidPhase(string message = "Hành động không hợp lệ ở phase hiện tại.", int? version = null) =>
            new("INVALID_PHASE", message, HttpStatusCode.BadRequest, version);
    }
}
