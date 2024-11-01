namespace Sample.Common
{
    public static class CommonDefine
    {
        // HACK: test <-> dev, default: 0, false
        public const int RedisDatabase = 0;
        public const int MAX_SHARD = 2;
        // Setting
        static public IConfiguration? Configuration { get; set; }
        public static bool IsDevelopment;
        // Redis
        public const double SESSION_TIME_OUT = 3600 * 12;
    }

    public static class RC
    {
        public const int SUCCESS = 1;
        public const int UNKNOWN = -1;
        public const int MYSQL_SQL_EXCEPTION = -2;
        public const int REDISL_EXCEPTION = -3;
    }
}
