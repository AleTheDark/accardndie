namespace AccardND.GameCore
{
    public interface IRandomSource
    {
        int NextInclusive(int minimum, int maximum);
    }
}
