
namespace PopFree.Pop3
{
    /// <summary>
    /// Represents the result of a pop STAT command or a line in a pop LIST command.
    /// </summary>
    public struct PopStat
    {
        public int MessageNumber;
        public int Size;
    }
}
