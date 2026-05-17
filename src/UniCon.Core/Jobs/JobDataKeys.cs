namespace UniCon.Core.Jobs
{
    /// <summary>
    /// 定义任务中使用的常量键名，避免魔法字符串 (RULE 1.1)
    /// </summary>
    public static class JobDataKeys
    {
        // HttpJob 相关
        public const string HttpUrl = "Job_Http_Url";
        public const string HttpMethod = "Job_Http_Method";
        public const string HttpHeaders = "Job_Http_Headers"; // JSON string: Dictionary<string, string>
        public const string HttpBody = "Job_Http_Body";
        public const string HttpQueryParams = "Job_Http_QueryParams"; // JSON string: Dictionary<string, string>

        // CommunicationJob 相关
        public const string CommDriverId = "Job_Comm_DriverId";
        public const string CommAddress = "Job_Comm_Address";
        public const string CommOperation = "Job_Comm_Operation"; // "Read" or "Write"
        public const string CommValue = "Job_Comm_Value";
        public const string CommDataType = "Job_Comm_DataType"; // e.g. "System.Single"
    }
}
