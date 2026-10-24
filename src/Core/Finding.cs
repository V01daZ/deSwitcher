namespace DeSwitcher.Core
{
    public enum Severity
    {
        Info,
        Suspicious,
        Dangerous
    }

    public sealed class Finding
    {
        public string RuleId;
        public Severity Severity;
        public string Detail;

        public Finding(string ruleId, Severity severity, string detail)
        {
            RuleId = ruleId;
            Severity = severity;
            Detail = detail;
        }

        public override string ToString() => Severity + " " + RuleId + ": " + Detail;
    }
}
