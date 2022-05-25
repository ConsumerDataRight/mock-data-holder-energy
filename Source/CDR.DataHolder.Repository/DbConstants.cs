namespace CDR.DataHolder.Repository
{
    public static class DbConstants
    {
        public static class ConnectionStringNames
        {
            public static class Resource
            {
                public const string Default = "DataHolder_Energy_DB";
                public const string Migrations = "DataHolder_Energy_Migrations_DB";
                public const string Logging = "DataHolder_Energy_Logging_DB";
            }

            public static class Identity
            {
                public const string Default = "DataHolder_Energy_IDP_DB";
                public const string Migrations = "DataHolder_Energy_IDP_Migrations_DB";
            }

            public static class Cache
            {
                public const string Default = "DataHolder_Energy_Cache";
            }
        }
    }
}
