using Remora.Rest.Core;

namespace Boyfriend.Data;

public struct Reminder {
    public DateTimeOffset RemindAt;
    public string         Text;
    public Snowflake      Channel;
}
