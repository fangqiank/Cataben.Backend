namespace Cataben.Shared.Constants
{
    public static class ApplicationConstants
    {
        public const string AppName = "Cataben";
        public const string AppVersion = "1.0.0";

        public static class CacheKeys
        {
            public const string UserPrefix = "user:";
            public const string ChallengePrefix = "challenge:";
            public const string SubmissionPrefix = "submission:";
            public const string AchievementPrefix = "achievement:";
            public const string LeaderboardKey = "leaderboard";
            public const string RecentSubmissions = "recent_submissions";
        }

        public static class QueueNames
        {
            public const string CodeExecution = "code.execute";
            public const string CodeResult = "code.result";                 // base; results published as code.result.{executionId}
            public const string ChallengeSubmission = "challenge.submit";
            public const string NotificationQueue = "notification";
            public const string SubmissionStatusUpdate = "submission.status.update";
            public const string WorkerHealth = "worker.health";
            public const string WorkerHealthResponsePrefix = "worker.health.response.";
        }

        /// <summary>
        /// NATS topology: queue groups (core, load-balanced), the JetStream stream backing
        /// the critical code.execute subject, and the shared durable consumer name workers
        /// use to scale horizontally.
        /// </summary>
        public static class Nats
        {
            // Core NATS queue groups
            public const string ResultQueueGroup = "cataben-results";   // API ResultReceiver(s) share code.result.>

            // JetStream (code.execute persistence)
            public const string ExecutionsStream = "EXECUTIONS";
            public const string ExecutionsStreamSubject = "code.execute";
            public const string ExecutionsDurableConsumer = "executions-worker"; // shared by all worker replicas → load-balanced
        }

        public static class RateLimits
        {
            public const int DefaultPermitLimit = 100;
            public const int DefaultWindowMinutes = 1;
            public const int ExecutionPermitLimit = 10;
            public const int ExecutionWindowSeconds = 30;
        }

        public static class DefaultValues
        {
            public const int DefaultPageSize = 20;
            public const int MaxPageSize = 100;
            public const int DefaultTimeoutSeconds = 10;
            public const int DefaultMemoryLimitMb = 256;
        }
    }
}
