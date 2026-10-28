namespace Back_End.DTOs
{
    public class ChallengeResponse
    {
        public bool ChallengeSucceeded { get; set; }
        public string PenalizedPlayerId { get; set; } = string.Empty;
        public bool Survived { get; set; }
        public bool Eliminated { get; set; }
        public string NextPhase { get; set; } = string.Empty;
        public bool AwaitingTriggerPull { get; set; } = true;
        public int StateVersion { get; set; }
    }
}
