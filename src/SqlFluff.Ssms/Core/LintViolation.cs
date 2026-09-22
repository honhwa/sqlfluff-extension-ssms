namespace SqlFluff.Ssms.Core
{
    internal sealed class LintViolation
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        // 1-based; EndLine/EndColumn are 0 when sqlfluff did not report an end position.
        public int StartLine { get; set; }
        public int StartColumn { get; set; }
        public int EndLine { get; set; }
        public int EndColumn { get; set; }

        public bool IsParseError => Code == "PRS" || Code == "LXR" || Code == "TMP";

        public string Message =>
            string.IsNullOrEmpty(Name) ? Code + ": " + Description : Code + ": " + Description + " (" + Name + ")";
    }
}
