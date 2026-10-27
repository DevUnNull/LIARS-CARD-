namespace Back_End.Exceptions
{
    public sealed class MatchResultPersistenceException : Exception
    {
        public MatchResultPersistenceException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public sealed class MatchResultConflictException : Exception
    {
        public MatchResultConflictException(string message)
            : base(message)
        {
        }
    }
}
