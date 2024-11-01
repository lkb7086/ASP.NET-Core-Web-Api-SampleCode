namespace Sample.Common
{
    public static class ExceptionExtensions
    {
        public static string ToSimpleString(this Exception ex)
        {
            //public static string ToSimpleString(this Exception ex)
            //{
            //    var trace = new StackTrace(ex, true);
            //    var frames = trace.GetFrames();
            //    string stackTrace = "";

            //    if (frames != null && frames.Length > 0)
            //    {
            //        // 첫 번째 프레임만 포함
            //        var frame = frames[0];
            //        stackTrace = $"{frame.GetMethod().Name} in {frame.GetFileName()}:line {frame.GetFileLineNumber()}";
            //    }

            //    return $"{ex.Message}\n{stackTrace}";
            return ex.ToString();
        }
    }
}
