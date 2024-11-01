namespace Sample.Common
{
    public class Singleton<T> where T : class, new()
    {
        private static Lazy<T>? instance;
        private static object singletonLock = new Object();        

        public static T Instance
        {
            get
            {
                if (IsExists() == false)
                {
                    lock (singletonLock)
                    {
                        if (IsExists() == false)
                        {
                            T singleton = new T();
                            instance = new Lazy<T>(() => singleton);
                        }
                    }
                }

                return instance?.Value ?? throw new NullReferenceException();
            }
        }

        private static bool IsExists()
        {
            return instance != null && instance.IsValueCreated == true;
        }
    }
}
